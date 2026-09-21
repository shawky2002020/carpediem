using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.DraftEvents;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class DraftContractTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task FirstCreateAndReplayExposeRequiredStatusHeadersAndShape()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var key = Guid.NewGuid();

        var created = await harness.CreateAsync(key, ValidCreateBody());
        var replay = await harness.CreateAsync(key, ValidCreateBody());

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        using (var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            foreach (var property in new[] { "id", "title", "start", "end", "startDateTimeUtc", "endDateTimeUtc", "location", "status", "version" })
            {
                Assert.True(body.RootElement.TryGetProperty(property, out _), $"Missing property {property}.");
            }
        }
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotent-Replay").Single());
    }

    [Fact]
    public async Task MissingAndNonDraftUpdatesReturnDistinctSafeConflicts()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var missing = await harness.UpdateAsync(Guid.NewGuid(), ValidUpdateBody(Convert.ToBase64String(new byte[8])));
        var created = await harness.CreateAsync(Guid.NewGuid(), ValidCreateBody());
        var draft = (await created.Content.ReadFromJsonAsync<DraftBody>())!;
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var persisted = await db.Events.SingleAsync();
            db.Entry(persisted).Property(item => item.Status).CurrentValue = EventStatus.Published;
            await db.SaveChangesAsync();
        }
        var nonDraft = await harness.UpdateAsync(draft.Id, ValidUpdateBody(draft.Version));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("event_not_found", (await missing.Content.ReadFromJsonAsync<ProblemBody>())!.Code);
        Assert.Equal(HttpStatusCode.Conflict, nonDraft.StatusCode);
        var problem = (await nonDraft.Content.ReadFromJsonAsync<ProblemBody>())!;
        Assert.Equal("not_draft", problem.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
        Assert.Null(problem.Detail);
    }

    [Fact]
    public async Task InvalidVersionReturnsMalformedRequestProblem()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var created = await harness.CreateAsync(Guid.NewGuid(), ValidCreateBody());
        var draft = (await created.Content.ReadFromJsonAsync<DraftBody>())!;

        var response = await harness.UpdateAsync(draft.Id, ValidUpdateBody("not-base64"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemBody>())!;
        Assert.Equal("malformed_request", problem.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task InvalidEventIdentifierReturnsMalformedRequestProblem()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);

        var response = await harness.Client.GetAsync(new Uri("/api/events/drafts/not-a-guid", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("malformed_request", (await response.Content.ReadFromJsonAsync<ProblemBody>())!.Code);
    }

    private static object ValidCreateBody() => new
    {
        title = "Beach cleanup",
        start = new { localDateTime = "2026-10-10T14:30" },
        end = new { localDateTime = "2026-10-10T16:30" },
        location = "North shore",
    };

    private static object ValidUpdateBody(string version) => new
    {
        title = "Updated cleanup",
        start = new { localDateTime = "2026-10-10T14:30" },
        end = new { localDateTime = "2026-10-10T16:30" },
        location = "North shore",
        version,
    };

    private sealed record DraftBody(Guid Id, string Version);
    private sealed record ProblemBody(string Code, string TraceId, string? Detail);
}
