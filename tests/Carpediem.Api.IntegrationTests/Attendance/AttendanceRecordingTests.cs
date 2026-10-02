using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Attendance.Application;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Carpediem.Web.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Attendance;

#pragma warning disable CA1707 // Identifiers should not contain underscores

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class AttendanceRecordingTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task ChiefLeaderCanQueryRollSheetOnPublishedEvent_Returns200WithUnrecordedEntries()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.alice", true, CarpediemRoles.Leader);
        var assignmentId = await SeedTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var response = await harness.Client.GetAsync($"/api/events/{eventId}/attendance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AttendanceRollSheetResponse>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.Equal("Published", body.Status);
        Assert.False(body.IsCompleted);
        Assert.False(body.IsAllFinalized);
        Assert.Single(body.Entries);

        var entry = body.Entries[0];
        Assert.Equal(assignmentId, entry.TeamAssignmentId);
        Assert.Equal(volunteerUserId, entry.VolunteerUserId);
        Assert.Null(entry.Outcome);
        Assert.Null(entry.FinalizedAt);
    }

    [Fact]
    public async Task AdminCanQueryRollSheetOnCompletedEvent_Returns200()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.bob", true, CarpediemRoles.Leader);
        await SeedTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        // Mark event as completed
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var evt = await db.Events.SingleAsync(e => e.Id == eventId);
            evt.Complete(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        harness.Factory.ActorUserId = harness.ActorUserId;
        harness.Factory.ActorRole = CarpediemRoles.Admin;

        var response = await harness.Client.GetAsync($"/api/events/{eventId}/attendance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AttendanceRollSheetResponse>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.Equal("Completed", body.Status);
        Assert.True(body.IsCompleted);
    }

    [Fact]
    public async Task NonChiefLeaderAndNonAdminUser_IsRejectedWith403Forbidden()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var otherLeaderId = await harness.SeedUserAsync("other.leader", true, CarpediemRoles.Leader);
        harness.Factory.ActorUserId = otherLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var getResponse = await harness.Client.GetAsync($"/api/events/{eventId}/attendance");
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);

        var afTokenOther = await GetFreshAntiforgeryTokenAsync(harness);
        using var postRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/attendance");
        postRequest.Headers.Add("X-XSRF-TOKEN", afTokenOther);
        postRequest.Content = JsonContent.Create(new FinalizeAttendanceRequest([
            new AttendanceOutcomeSubmission(Guid.NewGuid(), "Attended")
        ]));

        var postResponse = await harness.Client.SendAsync(postRequest);
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
    }

    [Fact]
    public async Task DraftOrCancelledEvent_Returns409Conflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);

        // 1. Test Draft Event
        var draftEventId = await SetupDraftEventAsync(harness);
        harness.Factory.ActorUserId = harness.ActorUserId;
        harness.Factory.ActorRole = CarpediemRoles.Admin;

        var draftGetResponse = await harness.Client.GetAsync($"/api/events/{draftEventId}/attendance");
        Assert.Equal(HttpStatusCode.Conflict, draftGetResponse.StatusCode);

        var draftAfToken = await GetFreshAntiforgeryTokenAsync(harness);
        using var draftPostRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{draftEventId}/attendance");
        draftPostRequest.Headers.Add("X-XSRF-TOKEN", draftAfToken);
        draftPostRequest.Content = JsonContent.Create(new FinalizeAttendanceRequest([
            new AttendanceOutcomeSubmission(Guid.NewGuid(), "Attended")
        ]));
        var draftPostResponse = await harness.Client.SendAsync(draftPostRequest);
        Assert.Equal(HttpStatusCode.Conflict, draftPostResponse.StatusCode);

        // 2. Test Cancelled Event
        var (cancelledEventId, _) = await SetupPublishedEventWithChiefLeaderAsync(harness);
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var evt = await db.Events.SingleAsync(e => e.Id == cancelledEventId);
            db.Entry(evt).Property(e => e.Status).CurrentValue = EventStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        var cancelledGetResponse = await harness.Client.GetAsync($"/api/events/{cancelledEventId}/attendance");
        Assert.Equal(HttpStatusCode.Conflict, cancelledGetResponse.StatusCode);
    }

    [Fact]
    public async Task ChiefLeaderFinalizesAttendance_WithAttendedAndNoShow_Returns200AndPersistsOutcomesAndAudit()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var volunteerA = await harness.SeedUserAsync("volunteer.alpha", true, CarpediemRoles.Leader);
        var volunteerB = await harness.SeedUserAsync("volunteer.beta", true, CarpediemRoles.Leader);

        var assignmentA = await SeedTeamAssignmentAsync(harness, eventId, volunteerA, chiefLeaderId);
        var assignmentB = await SeedTeamAssignmentAsync(harness, eventId, volunteerB, chiefLeaderId);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var afToken = await GetFreshAntiforgeryTokenAsync(harness);
        using var postRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/attendance");
        postRequest.Headers.Add("X-XSRF-TOKEN", afToken);
        postRequest.Content = JsonContent.Create(new FinalizeAttendanceRequest([
            new AttendanceOutcomeSubmission(assignmentA, "Attended"),
            new AttendanceOutcomeSubmission(assignmentB, "NoShow")
        ]));

        var response = await harness.Client.SendAsync(postRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<FinalizeAttendanceResponse>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.Equal(2, body.TotalActiveAssignments);
        Assert.Equal(1, body.AttendedCount);
        Assert.Equal(1, body.NoShowCount);
        Assert.Equal(0, body.UnrecordedCount);

        // Verify Database Persistence
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

            var outcomes = await db.AttendanceOutcomes
                .Where(o => o.TeamAssignmentId == assignmentA || o.TeamAssignmentId == assignmentB)
                .ToDictionaryAsync(o => o.TeamAssignmentId);

            Assert.Equal(2, outcomes.Count);
            Assert.Equal("Attended", outcomes[assignmentA].Outcome.ToString());
            Assert.Equal("NoShow", outcomes[assignmentB].Outcome.ToString());
            Assert.Equal(chiefLeaderId, outcomes[assignmentA].FinalizedByUserId);

            // Verify Audit Log
            var audits = await db.AuditLogs
                .Where(a => a.ResourceId == eventId && a.Action == "AttendanceFinalized")
                .ToListAsync();

            Assert.Single(audits);
            Assert.Equal(chiefLeaderId, audits[0].ActorUserId);
            Assert.Equal("ChiefLeaderRollCall", audits[0].Source);
            Assert.Equal("Unrecorded", audits[0].PreviousValue);
            Assert.Equal("{\"Attended\":1,\"NoShow\":1}", audits[0].NewValue);
        }

        // Verify Subsequent GET Roll Sheet reflects outcomes and IsAllFinalized
        var getResponse = await harness.Client.GetAsync($"/api/events/{eventId}/attendance");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var rollSheet = await getResponse.Content.ReadFromJsonAsync<AttendanceRollSheetResponse>();
        Assert.NotNull(rollSheet);
        Assert.True(rollSheet.IsAllFinalized);
        Assert.Equal(2, rollSheet.Entries.Count);

        var entryA = rollSheet.Entries.Single(e => e.TeamAssignmentId == assignmentA);
        Assert.Equal("Attended", entryA.Outcome);
        Assert.NotNull(entryA.FinalizedAt);
        Assert.NotNull(entryA.Version);

        var entryB = rollSheet.Entries.Single(e => e.TeamAssignmentId == assignmentB);
        Assert.Equal("NoShow", entryB.Outcome);
        Assert.NotNull(entryB.FinalizedAt);
        Assert.NotNull(entryB.Version);
    }

    [Fact]
    public async Task IdempotentReplayAndOutcomeCorrection_UpdatesExistingOutcomesWithoutDuplication()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var volunteerA = await harness.SeedUserAsync("volunteer.cor1", true, CarpediemRoles.Leader);
        var volunteerB = await harness.SeedUserAsync("volunteer.cor2", true, CarpediemRoles.Leader);

        var assignmentA = await SeedTeamAssignmentAsync(harness, eventId, volunteerA, chiefLeaderId);
        var assignmentB = await SeedTeamAssignmentAsync(harness, eventId, volunteerB, chiefLeaderId);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        // First submission: Both Attended
        var replayAfToken = await GetFreshAntiforgeryTokenAsync(harness);
        using (var firstRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/attendance"))
        {
            firstRequest.Headers.Add("X-XSRF-TOKEN", replayAfToken);
            firstRequest.Content = JsonContent.Create(new FinalizeAttendanceRequest([
                new AttendanceOutcomeSubmission(assignmentA, "Attended"),
                new AttendanceOutcomeSubmission(assignmentB, "Attended")
            ]));
            var firstResponse = await harness.Client.SendAsync(firstRequest);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        }

        // Second submission: volunteerB corrected to NoShow
        using (var secondRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/attendance"))
        {
            secondRequest.Headers.Add("X-XSRF-TOKEN", replayAfToken);
            secondRequest.Content = JsonContent.Create(new FinalizeAttendanceRequest([
                new AttendanceOutcomeSubmission(assignmentA, "Attended"),
                new AttendanceOutcomeSubmission(assignmentB, "NoShow")
            ]));
            var secondResponse = await harness.Client.SendAsync(secondRequest);
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

            var secondBody = await secondResponse.Content.ReadFromJsonAsync<FinalizeAttendanceResponse>();
            Assert.NotNull(secondBody);
            Assert.Equal(1, secondBody.AttendedCount);
            Assert.Equal(1, secondBody.NoShowCount);
        }

        // Verify exactly 2 rows exist in database
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var count = await db.AttendanceOutcomes.CountAsync(o => o.TeamAssignmentId == assignmentA || o.TeamAssignmentId == assignmentB);
            Assert.Equal(2, count);

            var outcomeB = await db.AttendanceOutcomes.SingleAsync(o => o.TeamAssignmentId == assignmentB);
            Assert.Equal("NoShow", outcomeB.Outcome.ToString());

            // 2 audit entries recorded
            var auditCount = await db.AuditLogs.CountAsync(a => a.ResourceId == eventId && a.Action == "AttendanceFinalized");
            Assert.Equal(2, auditCount);
        }
    }

    [Fact]
    public async Task FinalizeAttendance_WithInvalidTeamAssignmentId_Returns409Conflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var invalidAfToken = await GetFreshAntiforgeryTokenAsync(harness);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/attendance");
        request.Headers.Add("X-XSRF-TOKEN", invalidAfToken);
        request.Content = JsonContent.Create(new FinalizeAttendanceRequest([
            new AttendanceOutcomeSubmission(Guid.NewGuid(), "Attended")
        ]));

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task FinalizeAttendance_MissingAntiforgeryToken_Returns400()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/attendance");
        request.Content = JsonContent.Create(new FinalizeAttendanceRequest([
            new AttendanceOutcomeSubmission(Guid.NewGuid(), "Attended")
        ]));

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Helpers ---

    private static async Task<(Guid EventId, Guid ChiefLeaderId)> SetupPublishedEventWithChiefLeaderAsync(
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
            "Notes",
            harness.ActorUserId,
            DateTimeOffset.UtcNow.AddDays(-2),
            effectiveDate);

        var assignment = draft.AppointChiefLeader(chiefLeaderId, harness.ActorUserId, DateTimeOffset.UtcNow.AddDays(-2));
        assignment.AcceptDirect(chiefLeaderId, DateTimeOffset.UtcNow.AddDays(-1), hasActiveTeamAssignmentOnCurrentCycle: false);
        draft.Publish(harness.ActorUserId, DateTimeOffset.UtcNow.AddHours(-1));

        db.Events.Add(draft);
        await db.SaveChangesAsync();

        return (draft.Id, chiefLeaderId);
    }

    private static async Task<Guid> SetupDraftEventAsync(DraftApiTestHarness harness)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var start = DateTimeOffset.UtcNow.AddDays(7);
        var end = start.AddHours(4);
        var effectiveDate = DateOnly.FromDateTime(start.Date);

        var draft = Event.CreateDraft(
            "Draft Gala",
            start,
            end,
            "Small Room",
            null,
            harness.ActorUserId,
            DateTimeOffset.UtcNow.AddDays(-2),
            effectiveDate);

        db.Events.Add(draft);
        await db.SaveChangesAsync();

        return draft.Id;
    }

    private static async Task<Guid> SeedTeamAssignmentAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        Guid volunteerUserId,
        Guid chiefLeaderId)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var currentCycle = await db.StaffingCycles.SingleAsync(c => c.EventId == eventId && c.IsCurrent);

        var assignment = TeamAssignment.Create(
            currentCycle.Id,
            eventId,
            volunteerUserId,
            chiefLeaderId,
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow,
            "Available",
            false,
            "Published");

        db.TeamAssignments.Add(assignment);
        await db.SaveChangesAsync();

        return assignment.Id;
    }

    private static async Task<string> GetFreshAntiforgeryTokenAsync(DraftApiTestHarness harness)
    {
        var afResponse = await harness.Client.GetAsync("/api/session/antiforgery");
        afResponse.EnsureSuccessStatusCode();
        var afBody = await afResponse.Content.ReadFromJsonAsync<AntiforgeryBody>();
        return afBody!.RequestToken;
    }

    private sealed record AntiforgeryBody(string RequestToken);
}
