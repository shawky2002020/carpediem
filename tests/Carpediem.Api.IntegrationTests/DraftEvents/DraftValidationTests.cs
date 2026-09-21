using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.DraftEvents;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class DraftValidationTests(SqlServerFixture sqlServer)
{
    public static TheoryData<object, string> InvalidRequiredValues => new()
    {
        { Body(title: null), "title" },
        { Body(title: "\u2003"), "title" },
        { Body(location: null), "location" },
        { Body(location: "\t \u2003"), "location" },
        { Body(start: null), "start" },
        { Body(end: null), "end" },
        { Body(start: "not-a-date"), "start" },
        { Body(start: "2026-04-24T00:30"), "start" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequiredValues))]
    public async Task InvalidRequiredOrUnusableValueIsRejectedWithoutWrites(object body, string field)
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);

        var response = await harness.CreateAsync(Guid.NewGuid(), body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_failed", problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _));
        await AssertNoDraftWritesAsync(harness);
    }

    [Theory]
    [InlineData("2026-10-10T14:30", "2026-10-10T14:30")]
    [InlineData("2026-10-10T16:30", "2026-10-10T14:30")]
    public async Task NonIncreasingWindowIsRejectedWithoutWrites(string start, string end)
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);

        var response = await harness.CreateAsync(Guid.NewGuid(), Body(start: start, end: end));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("end", out _));
        await AssertNoDraftWritesAsync(harness);
    }

    [Fact]
    public async Task PastScheduleIsAccepted()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);

        var response = await harness.CreateAsync(Guid.NewGuid(),
            Body(start: "2020-01-01T10:00", end: "2020-01-01T11:00"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task MalformedJsonReturnsSafeBadRequestWithoutWrites()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/events/drafts")
        {
            Content = new StringContent("{\"title\":", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response = await harness.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("malformed_request", problem.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("Json", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        await AssertNoDraftWritesAsync(harness);
    }

    [Fact]
    public async Task AmbiguousCairoTimeReturnsAllowedOffsetsAndAcceptsASelectedOffset()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var local = FindAmbiguousCairoTime();
        var end = DateTime.ParseExact(local, "yyyy-MM-dd'T'HH:mm", null)
            .AddHours(2).ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var first = await harness.CreateAsync(Guid.NewGuid(), new
        {
            title = "Ambiguous-time Draft",
            start = new { localDateTime = local },
            end = new { localDateTime = end },
            location = "Cairo",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, first.StatusCode);
        using var problem = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var offsets = problem.RootElement.GetProperty("allowedOffsets").GetProperty("start")
            .EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Equal(2, offsets.Length);
        await AssertNoDraftWritesAsync(harness);

        var corrected = await harness.CreateAsync(Guid.NewGuid(), new
        {
            title = "Ambiguous-time Draft",
            start = new { localDateTime = local, offset = offsets[0] },
            end = new { localDateTime = end },
            location = "Cairo",
        });

        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
    }

    private static object Body(
        string? title = "Beach cleanup",
        string? start = "2026-10-10T14:30",
        string? end = "2026-10-10T16:30",
        string? location = "North shore") => new
        {
            title,
            start = start is null ? null : new { localDateTime = start },
            end = end is null ? null : new { localDateTime = end },
            location,
        };

    private static async Task AssertNoDraftWritesAsync(DraftApiTestHarness harness)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(0, await db.Events.CountAsync());
        Assert.Equal(0, await db.StaffingCycles.CountAsync());
        Assert.Equal(0, await db.AuditLogs.CountAsync());
        Assert.Equal(0, await db.DraftCreateRequests.CountAsync());
    }

    private static string FindAmbiguousCairoTime()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        for (var month = 9; month <= 12; month++)
        {
            for (var day = 1; day <= DateTime.DaysInMonth(2026, month); day++)
            {
                for (var hour = 0; hour < 24; hour++)
                {
                    var local = new DateTime(2026, month, day, hour, 30, 0, DateTimeKind.Unspecified);
                    if (zone.IsAmbiguousTime(local))
                    {
                        return local.ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        throw new InvalidOperationException("Expected a Cairo overlap in 2026.");
    }
}
