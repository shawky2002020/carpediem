using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.DraftEvents;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class CreateDraftIdempotencyTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task SameIntentReplaysWithoutAnotherEventOrAudit()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var key = Guid.NewGuid();
        var body = ValidBody();

        var first = await harness.CreateAsync(key, body);
        var replay = await harness.CreateAsync(key, body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotent-Replay").Single());
        var firstId = (await first.Content.ReadFromJsonAsync<IdBody>())!.Id;
        var replayId = (await replay.Content.ReadFromJsonAsync<IdBody>())!.Id;
        Assert.Equal(firstId, replayId);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(1, await db.Events.CountAsync());
        Assert.Equal(1, await db.StaffingCycles.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync());
        Assert.Equal(1, await db.DraftCreateRequests.CountAsync());
    }

    [Fact]
    public async Task ChangedPayloadWithSameKeyConflictsWithoutCreatingAnEvent()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var key = Guid.NewGuid();

        var first = await harness.CreateAsync(key, ValidBody());
        var changed = await harness.CreateAsync(key, ValidBody("Different title"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(1, await db.Events.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ConcurrentSameIntentCreatesOneEvent()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var key = Guid.NewGuid();
        var body = ValidBody();

        var responses = await Task.WhenAll(
            harness.CreateAsync(key, body),
            harness.CreateAsync(key, body));

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(1, await db.Events.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task DistinctIntentsMayCreateIdenticalBusinessValues()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var body = ValidBody();

        var first = await harness.CreateAsync(Guid.NewGuid(), body);
        var second = await harness.CreateAsync(Guid.NewGuid(), body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(2, await db.Events.CountAsync());
        Assert.Equal(2, await db.AuditLogs.CountAsync());
    }

    private static object ValidBody(string title = "Beach cleanup") => new
    {
        title,
        start = new { localDateTime = "2026-10-10T14:30" },
        end = new { localDateTime = "2026-10-10T16:30" },
        location = "North shore",
    };

    private sealed record IdBody(Guid Id);
}
