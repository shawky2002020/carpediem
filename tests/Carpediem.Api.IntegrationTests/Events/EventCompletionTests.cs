using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Application.Completion;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Carpediem.Web.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Events;

#pragma warning disable CA1707 // Identifiers should not contain underscores

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class EventCompletionTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task AdminManuallyCompletesPublishedEvent_ReturnsOkAndRecordsAudit()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId, initialVersion) = await SetupPublishedEventAsync(harness);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/complete");
        request.Headers.Add("If-Match", $"\"{initialVersion}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CompleteEventResponse>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.Equal("Completed", body.Status);
        Assert.NotEqual(initialVersion, body.Version);
        Assert.True(response.Headers.Contains("ETag"));

        // Verify database persistence
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var updatedEvent = await db.Events.SingleAsync(e => e.Id == eventId);
        Assert.Equal(EventStatus.Completed, updatedEvent.Status);

        // Verify audit log entry
        var audits = await db.AuditLogs
            .Where(a => a.ResourceId == eventId && a.Action == "EventCompleted")
            .ToListAsync();

        Assert.Single(audits);
        Assert.Equal(harness.ActorUserId, audits[0].ActorUserId);
        Assert.Equal("AdminManualCloseout", audits[0].Source);
        Assert.Equal("Published", audits[0].PreviousValue);
        Assert.Equal("Completed", audits[0].NewValue);
    }

    [Fact]
    public async Task AdminManualCloseout_WhenAlreadyCompletedWithMatchingVersion_ReturnsIdempotentReplay()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, _, initialVersion) = await SetupPublishedEventAsync(harness);

        // First completion
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/complete");
        firstRequest.Headers.Add("If-Match", $"\"{initialVersion}\"");
        firstRequest.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);
        var firstResponse = await harness.Client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var completedBody = await firstResponse.Content.ReadFromJsonAsync<CompleteEventResponse>();
        Assert.NotNull(completedBody);
        var completedVersion = completedBody.Version;

        // Idempotent replay with current completed version
        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/complete");
        replayRequest.Headers.Add("If-Match", $"\"{completedVersion}\"");
        replayRequest.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);
        var replayResponse = await harness.Client.SendAsync(replayRequest);

        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.True(replayResponse.Headers.Contains("Idempotent-Replay"));
        Assert.Equal("true", replayResponse.Headers.GetValues("Idempotent-Replay").First());

        // Verify exactly one audit entry exists (no duplicate)
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var audits = await db.AuditLogs
            .Where(a => a.ResourceId == eventId && a.Action == "EventCompleted")
            .ToListAsync();
        Assert.Single(audits);
    }

    [Fact]
    public async Task AdminManualCloseout_WithStaleVersion_Returns412PreconditionFailed()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, _, _) = await SetupPublishedEventAsync(harness);
        var staleVersion = Convert.ToBase64String(new byte[] { 0, 0, 0, 0, 0, 0, 0, 99 });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/complete");
        request.Headers.Add("If-Match", $"\"{staleVersion}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Fact]
    public async Task AdminManualCloseout_WhenEventIsDraft_Returns409Conflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var start = DateTimeOffset.UtcNow.AddDays(7);
        var draft = Event.CreateDraft("Draft Only", start, start.AddHours(4), "Venue", null,
            harness.ActorUserId, DateTimeOffset.UtcNow, DateOnly.FromDateTime(start.Date));

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            db.Events.Add(draft);
            await db.SaveChangesAsync();
        }

        var draftVersion = Convert.ToBase64String(draft.RowVersion);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{draft.Id}/complete");
        request.Headers.Add("If-Match", $"\"{draftVersion}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task NonAdmin_CannotExecuteManualCloseout_Returns403Forbidden()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Leader);
        var (eventId, _, initialVersion) = await SetupPublishedEventAsync(harness);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/complete");
        request.Headers.Add("If-Match", $"\"{initialVersion}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ScheduledCompletion_ViaStore_TransitionsExpiredEventsAndIgnoresFutureEvents()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);

        var now = DateTimeOffset.UtcNow;
        var pastEventId = Guid.NewGuid();
        var futureEventId = Guid.NewGuid();

        var clPastUserId = await harness.SeedUserAsync($"cl.past.{Guid.NewGuid():N}", true, CarpediemRoles.ChiefLeader);
        var clFutureUserId = await harness.SeedUserAsync($"cl.future.{Guid.NewGuid():N}", true, CarpediemRoles.ChiefLeader);

        // Seed 1 past event and 1 future event directly in DB
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

            // Past event (ended 2 hours ago)
            var pastEvent = Event.CreateDraft("Concluded Event", now.AddHours(-6), now.AddHours(-2), "Main Hall", null,
                harness.ActorUserId, now.AddDays(-14), DateOnly.FromDateTime(now.AddHours(-6).Date));
            var clPast = pastEvent.AppointChiefLeader(clPastUserId, harness.ActorUserId, now.AddDays(-13));
            clPast.AcceptDirect(clPastUserId, now.AddDays(-12), false);
            pastEvent.Publish(harness.ActorUserId, now.AddDays(-11));

            // Future event (ends tomorrow)
            var futureEvent = Event.CreateDraft("Future Event", now.AddDays(1), now.AddDays(1).AddHours(4), "Main Hall", null,
                harness.ActorUserId, now.AddDays(-14), DateOnly.FromDateTime(now.AddDays(1).Date));
            var clFuture = futureEvent.AppointChiefLeader(clFutureUserId, harness.ActorUserId, now.AddDays(-13));
            clFuture.AcceptDirect(clFutureUserId, now.AddDays(-12), false);
            futureEvent.Publish(harness.ActorUserId, now.AddDays(-11));

            db.Events.AddRange(pastEvent, futureEvent);
            await db.SaveChangesAsync();

            pastEventId = pastEvent.Id;
            futureEventId = futureEvent.Id;
        }

        // Execute scheduled completion
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IEventCompletionStore>();
            var completedCount = await store.ProcessScheduledCompletionsAsync(now);
            Assert.Equal(1, completedCount);
        }

        // Verify database statuses
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

            var pastEvent = await db.Events.SingleAsync(e => e.Id == pastEventId);
            var futureEvent = await db.Events.SingleAsync(e => e.Id == futureEventId);

            Assert.Equal(EventStatus.Completed, pastEvent.Status);
            Assert.Equal(EventStatus.Published, futureEvent.Status);

            var pastAudit = await db.AuditLogs
                .Where(a => a.ResourceId == pastEventId && a.Action == "EventCompleted")
                .ToListAsync();
            Assert.Single(pastAudit);
            Assert.Equal("SystemLifecycleWorker", pastAudit[0].Source);
        }
    }

    [Fact]
    public async Task ScheduledCompletion_PreservesActiveTeamAssignmentsAndAttendanceIndependence()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId, _) = await SetupPublishedEventAsync(harness);

        var volunteerId = await harness.SeedUserAsync("volunteer.attendee", true, CarpediemRoles.Leader);

        Guid assignmentId;
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var currentCycle = await db.StaffingCycles.SingleAsync(c => c.EventId == eventId && c.IsCurrent);

            var assignment = TeamAssignment.Create(
                currentCycle.Id,
                eventId,
                volunteerId,
                chiefLeaderId,
                DateTimeOffset.UtcNow.AddDays(7),
                DateTimeOffset.UtcNow,
                "Available",
                false,
                "Published");
            db.TeamAssignments.Add(assignment);
            await db.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        // Complete the event (attendance unrecorded)
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IEventCompletionStore>();
            var result = await store.ProcessScheduledCompletionsAsync(DateTimeOffset.UtcNow.AddYears(1));
            Assert.Equal(1, result);
        }

        // Verify event is Completed and TeamAssignment is still Active (decoupled attendance invariant)
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var @event = await db.Events.SingleAsync(e => e.Id == eventId);
            var assignment = await db.TeamAssignments.SingleAsync(a => a.Id == assignmentId);

            Assert.Equal(EventStatus.Completed, @event.Status);
            Assert.Equal(TeamAssignmentStatus.Active, assignment.Status);
        }
    }

    private static async Task<(Guid EventId, Guid ChiefLeaderId, string Version)> SetupPublishedEventAsync(
        DraftApiTestHarness harness)
    {
        var chiefLeaderId = await harness.SeedUserAsync(
            $"chief.leader.{Guid.NewGuid():N}",
            isActive: true,
            CarpediemRoles.ChiefLeader);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var start = DateTimeOffset.UtcNow.AddDays(7);
        var end = start.AddHours(4);
        var effectiveDate = DateOnly.FromDateTime(start.Date);

        var draft = Event.CreateDraft(
            "Community Gala",
            start,
            end,
            "Grand Ballroom",
            "Bring decorations",
            harness.ActorUserId,
            DateTimeOffset.UtcNow.AddDays(-2),
            effectiveDate);

        var assignment = draft.AppointChiefLeader(chiefLeaderId, harness.ActorUserId, DateTimeOffset.UtcNow.AddDays(-2));
        assignment.AcceptDirect(chiefLeaderId, DateTimeOffset.UtcNow.AddDays(-1), hasActiveTeamAssignmentOnCurrentCycle: false);
        draft.Publish(harness.ActorUserId, DateTimeOffset.UtcNow.AddHours(-1));

        db.Events.Add(draft);
        await db.SaveChangesAsync();

        return (draft.Id, chiefLeaderId, Convert.ToBase64String(draft.RowVersion));
    }
}
