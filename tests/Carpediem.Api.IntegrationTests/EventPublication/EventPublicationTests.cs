using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.EventPublication;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class EventPublicationTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task AdminPublishesWithActiveChiefLeaderTransitionsToPublished()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, chiefLeaderId, version) = await SetupDraftWithActiveChiefLeaderAsync(harness);

        // Seed 2 active Leaders to verify notification staging
        var leader1Id = await harness.SeedUserAsync("leader.one", true, CarpediemRoles.Leader);
        var leader2Id = await harness.SeedUserAsync("leader.two", true, CarpediemRoles.Leader);

        var response = await PublishAsync(harness, eventId, version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PublishedEventBody>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.Id);
        Assert.Equal("Published", body.Status);
        Assert.Equal(chiefLeaderId, body.ActiveChiefLeader.UserId);
        Assert.NotEmpty(body.Version);
        Assert.NotEqual(version, body.Version);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var storedEvent = await db.Events.SingleAsync(e => e.Id == eventId);
        Assert.Equal(EventStatus.Published, storedEvent.Status);

        var audits = await db.AuditLogs.Where(a => a.ResourceId == eventId && a.Action == "EventPublished").ToListAsync();
        Assert.Single(audits);
        Assert.Equal(harness.ActorUserId, audits[0].ActorUserId);
        Assert.Equal("Draft", audits[0].PreviousValue);
        Assert.Equal("Published", audits[0].NewValue);

        var notifications = await db.InPlatformNotifications
            .Where(n => n.EventId == eventId && n.NotificationType == "EventPublished")
            .ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.RecipientUserId == leader1Id);
        Assert.Contains(notifications, n => n.RecipientUserId == leader2Id);
    }

    [Fact]
    public async Task PublishWithoutActiveChiefLeaderReturns422UnprocessableEntity()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var version = await GetDraftVersionAsync(harness, eventId);

        var response = await PublishAsync(harness, eventId, version);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var storedEvent = await db.Events.SingleAsync(e => e.Id == eventId);
        Assert.Equal(EventStatus.Draft, storedEvent.Status);
        Assert.False(await db.AuditLogs.AnyAsync(a => a.ResourceId == eventId && a.Action == "EventPublished"));
    }

    [Fact]
    public async Task PublishWithPendingChiefLeaderReturns422UnprocessableEntity()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("pending.candidate", true, CarpediemRoles.ChiefLeader);
        var appointResponse = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());
        appointResponse.EnsureSuccessStatusCode();

        var version = await GetDraftVersionAsync(harness, eventId);
        var response = await PublishAsync(harness, eventId, version);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PublishWithDeclinedChiefLeaderReturns422UnprocessableEntity()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("declined.candidate", true, CarpediemRoles.ChiefLeader);
        var appointResponse = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());
        appointResponse.EnsureSuccessStatusCode();

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var assignment = await db.ChiefLeaderAssignments.SingleAsync(a => a.EventId == eventId);
            assignment.DeclineDirect(candidateId, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var version = await GetDraftVersionAsync(harness, eventId);
        var response = await PublishAsync(harness, eventId, version);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task NonAdminReturns403Forbidden()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Leader);
        var response = await PublishAsync(harness, Guid.NewGuid(), Convert.ToBase64String(new byte[8]));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MissingAntiforgeryReturns400BadRequest()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, _, version) = await SetupDraftWithActiveChiefLeaderAsync(harness);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/drafts/{eventId}/publish");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingIfMatchReturns400BadRequest()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, _, _) = await SetupDraftWithActiveChiefLeaderAsync(harness);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/drafts/{eventId}/publish");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);
        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StaleRowVersionReturns409Conflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, _, initialVersion) = await SetupDraftWithActiveChiefLeaderAsync(harness);

        // Modify the draft to advance the rowversion
        var updateResponse = await harness.UpdateAsync(eventId, new
        {
            title = "Updated Beach Cleanup",
            start = new { localDateTime = "2026-10-10T14:30" },
            end = new { localDateTime = "2026-10-10T16:30" },
            location = "North shore updated",
            version = initialVersion,
        });
        updateResponse.EnsureSuccessStatusCode();

        // Attempt publish with stale version
        var response = await PublishAsync(harness, eventId, initialVersion);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task IdempotentReplayReturns200WithHeader()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, chiefLeaderId, version) = await SetupDraftWithActiveChiefLeaderAsync(harness);
        await harness.SeedUserAsync("leader.one", true, CarpediemRoles.Leader);

        var firstResponse = await PublishAsync(harness, eventId, version);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.False(firstResponse.Headers.Contains("Idempotent-Replay"));

        // Replay with same published event
        var replayResponse = await PublishAsync(harness, eventId, version);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.True(replayResponse.Headers.Contains("Idempotent-Replay"));
        Assert.Equal("true", replayResponse.Headers.GetValues("Idempotent-Replay").Single());

        var replayBody = await replayResponse.Content.ReadFromJsonAsync<PublishedEventBody>();
        Assert.NotNull(replayBody);
        Assert.Equal("Published", replayBody.Status);
        Assert.Equal(chiefLeaderId, replayBody.ActiveChiefLeader.UserId);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var audits = await db.AuditLogs.Where(a => a.ResourceId == eventId && a.Action == "EventPublished").ToListAsync();
        Assert.Single(audits);

        var notifications = await db.InPlatformNotifications
            .Where(n => n.EventId == eventId && n.NotificationType == "EventPublished")
            .ToListAsync();
        Assert.Single(notifications);
    }

    [Fact]
    public async Task DraftEditingLockedAfterPublication()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, _, version) = await SetupDraftWithActiveChiefLeaderAsync(harness);

        var publishResponse = await PublishAsync(harness, eventId, version);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        var publishedBody = await publishResponse.Content.ReadFromJsonAsync<PublishedEventBody>();

        var editResponse = await harness.UpdateAsync(eventId, new
        {
            title = "Attempted edit on published event",
            start = new { localDateTime = "2026-10-10T14:30" },
            end = new { localDateTime = "2026-10-10T16:30" },
            location = "North shore",
            version = publishedBody!.Version,
        });

        Assert.Equal(HttpStatusCode.Conflict, editResponse.StatusCode);
    }

    [Fact]
    public async Task NonDraftStatesRejectPublicationWith409Conflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, _, version) = await SetupDraftWithActiveChiefLeaderAsync(harness);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events.Events SET Status = 'Cancelled' WHERE Id = {eventId}");
        }

        var response = await PublishAsync(harness, eventId, version);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static async Task<(Guid EventId, Guid ChiefLeaderId, string Version)> SetupDraftWithActiveChiefLeaderAsync(
        DraftApiTestHarness harness)
    {
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.candidate", true, CarpediemRoles.ChiefLeader);

        var appointResponse = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());
        appointResponse.EnsureSuccessStatusCode();

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var assignment = await db.ChiefLeaderAssignments.SingleAsync(a => a.EventId == eventId);
            assignment.AcceptDirect(candidateId, DateTimeOffset.UtcNow, false);
            await db.SaveChangesAsync();
        }

        var version = await GetDraftVersionAsync(harness, eventId);
        return (eventId, candidateId, version);
    }

    private static async Task<string> GetDraftVersionAsync(DraftApiTestHarness harness, Guid eventId)
    {
        var response = await harness.Client.GetAsync($"/api/events/drafts/{eventId}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<DraftDetailsResponse>();
        return json!.Version;
    }

    private static async Task<HttpResponseMessage> PublishAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        string version)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/drafts/{eventId}/publish");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);
        return await harness.Client.SendAsync(request);
    }

    private sealed record DraftDetailsResponse(Guid Id, string Version);
    private sealed record ActiveChiefLeaderBody(Guid UserId, string UserName, Guid AssignmentId);
    private sealed record PublishedEventBody(
        Guid Id,
        string Title,
        string Status,
        ActiveChiefLeaderBody ActiveChiefLeader,
        string Version,
        string PublishedAt);
}
