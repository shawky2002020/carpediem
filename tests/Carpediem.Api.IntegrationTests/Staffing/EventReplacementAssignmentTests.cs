using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Notifications.Domain;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Carpediem.Web.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Staffing;

#pragma warning disable CA1707 // Identifiers should not contain underscores

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class EventReplacementAssignmentTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task GetReplacementContext_ReturnsPublishedEventWithActiveCancelledAndCandidates()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var activeVolunteerId = await harness.SeedUserAsync("volunteer.active1", true, CarpediemRoles.Leader);
        var cancelledVolunteerId = await harness.SeedUserAsync("volunteer.cancelled1", true, CarpediemRoles.Leader);
        var candidateLeaderId = await harness.SeedUserAsync("volunteer.candidate1", true, CarpediemRoles.Leader);

        await SeedActiveTeamAssignmentAsync(harness, eventId, activeVolunteerId, chiefLeaderId);
        var cancelledAssignId = await SeedCancelledTeamAssignmentAsync(harness, eventId, cancelledVolunteerId, chiefLeaderId, "Exam conflict");
        await SeedAvailabilityResponseAsync(harness, eventId, candidateLeaderId, AvailabilityResponseType.Available);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var response = await harness.Client.GetAsync($"/api/events/{eventId}/replacement-assignment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReplacementAssignmentContextResponse>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.True(body.IsWindowOpen);

        Assert.Single(body.ActiveTeam);
        Assert.Equal(activeVolunteerId, body.ActiveTeam[0].UserId);
        Assert.Equal("Active", body.ActiveTeam[0].Status);

        Assert.Single(body.CancelledAssignments);
        Assert.Equal(cancelledAssignId, body.CancelledAssignments[0].AssignmentId);
        Assert.Equal(cancelledVolunteerId, body.CancelledAssignments[0].UserId);
        Assert.Equal("Cancelled", body.CancelledAssignments[0].Status);
        Assert.Equal("Exam conflict", body.CancelledAssignments[0].CancellationReason);

        Assert.Contains(body.Candidates, c => c.UserId == candidateLeaderId && c.AvailabilityStatus == "Available");
    }

    [Fact]
    public async Task GetReplacementContext_ReopensAllLinkedAssignmentsWithTheirCurrentStatus()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);
        var originalUser = await harness.SeedUserAsync("history.original", true, CarpediemRoles.Leader);
        var firstUser = await harness.SeedUserAsync("history.first", true, CarpediemRoles.Leader);
        var secondUser = await harness.SeedUserAsync("history.second", true, CarpediemRoles.Leader);
        var unlinkedUser = await harness.SeedUserAsync("history.unlinked", true, CarpediemRoles.Leader);
        var cancelledId = await SeedCancelledTeamAssignmentAsync(harness, eventId, originalUser, chiefLeaderId, "Schedule change");
        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var first = await PostReplacementAssignmentAsync(harness, eventId, new(firstUser, cancelledId));
        var second = await PostReplacementAssignmentAsync(harness, eventId, new(secondUser, cancelledId));
        var unlinked = await PostReplacementAssignmentAsync(harness, eventId, new(unlinkedUser));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Created, unlinked.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<TeamAssignmentResponse>())!.Id;
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var firstAssignment = await db.TeamAssignments.SingleAsync(a => a.Id == firstId);
            var activeEvent = await db.Events.SingleAsync(item => item.Id == eventId);
            firstAssignment.Cancel("Travel delay", DateTimeOffset.UtcNow, activeEvent.StartDateTime);
            await db.SaveChangesAsync();
        }

        using var response = await harness.Client.GetAsync($"/api/events/{eventId}/replacement-assignment");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var original = payload.GetProperty("cancelledAssignments").EnumerateArray()
            .Single(item => item.GetProperty("assignmentId").GetGuid() == cancelledId);
        Assert.True(original.TryGetProperty("linkedReplacements", out var links));
        Assert.Collection(links.EnumerateArray(),
            link => AssertLink(link, "history.first", "Cancelled"),
            link => AssertLink(link, "history.second", "Active"));
    }

    [Fact]
    public async Task AssignReplacementLeader_ConcurrentRetriesKeepOneAssignmentAndOneLink()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);
        var originalUser = await harness.SeedUserAsync("concurrent.original", true, CarpediemRoles.Leader);
        var candidateUser = await harness.SeedUserAsync("concurrent.candidate", true, CarpediemRoles.Leader);
        var cancelledId = await SeedCancelledTeamAssignmentAsync(harness, eventId, originalUser, chiefLeaderId, "Schedule change");
        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var token = await GetFreshAntiforgeryTokenAsync(harness);
        var requests = Enumerable.Range(0, 4).Select(_ =>
            PostReplacementAssignmentAsync(harness, eventId, new(candidateUser, cancelledId), token));

        var responses = await Task.WhenAll(requests);
        var created = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        var createdBody = await created.Content.ReadFromJsonAsync<TeamAssignmentResponse>();
        Assert.NotNull(createdBody);
        foreach (var replay in responses.Where(response => response != created))
        {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.True(replay.Headers.Contains("Idempotent-Replay"));
            var replayBody = await replay.Content.ReadFromJsonAsync<TeamAssignmentResponse>();
            Assert.Equal(createdBody.Id, replayBody!.Id);
        }

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(1, await db.TeamAssignments.CountAsync(item => item.EventId == eventId && item.UserId == candidateUser));
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.Action == "ReplacementLeaderAssigned" && item.ResourceId == createdBody.Id));
        Assert.Equal(1, await db.InPlatformNotifications.CountAsync(item => item.EventId == eventId && item.RecipientUserId == candidateUser &&
            item.NotificationType == InPlatformNotification.ReplacementLeaderAssignedType));
    }

    [Fact]
    public async Task GetReplacementContext_WhenEventInDraftStatus_Returns409Conflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var draftEventId = await harness.CreateDefaultDraftAsync();
        var chiefLeaderId = await harness.SeedUserAsync("chief.draft", true, CarpediemRoles.ChiefLeader, CarpediemRoles.Leader);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var response = await harness.Client.GetAsync($"/api/events/{draftEventId}/replacement-assignment");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetReplacementContext_NonChiefLeaderNonAdmin_Returns403Forbidden()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, _) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);
        var otherLeaderId = await harness.SeedUserAsync("other.leader", true, CarpediemRoles.Leader);

        harness.Factory.ActorUserId = otherLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var response = await harness.Client.GetAsync($"/api/events/{eventId}/replacement-assignment");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignReplacementLeader_ChiefLeaderSelectsCandidate_SucceedsWith201AndPreservesCancelledAssignment()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var cancelledVolunteerId = await harness.SeedUserAsync("volunteer.cancelled2", true, CarpediemRoles.Leader);
        var candidateLeaderId = await harness.SeedUserAsync("volunteer.replacement2", true, CarpediemRoles.Leader);

        var cancelledAssignId = await SeedCancelledTeamAssignmentAsync(harness, eventId, cancelledVolunteerId, chiefLeaderId, "Medical emergency");

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var requestBody = new AssignReplacementLeaderRequest(
            CandidateUserId: candidateLeaderId,
            ReplacedAssignmentId: cancelledAssignId,
            Justification: "Assigned as replacement for hospital emergency");

        var response = await PostReplacementAssignmentAsync(harness, eventId, requestBody);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TeamAssignmentResponse>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.Equal(candidateLeaderId, body.UserId);
        Assert.Equal("Active", body.Status);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        // Verify original cancelled assignment remains untouched
        var oldAssignment = await db.TeamAssignments.SingleAsync(t => t.Id == cancelledAssignId);
        Assert.Equal(TeamAssignmentStatus.Cancelled, oldAssignment.Status);
        Assert.Equal("Medical emergency", oldAssignment.CancellationReason);
        Assert.NotNull(oldAssignment.CancelledAt);

        // Verify new active replacement assignment exists
        var newAssignment = await db.TeamAssignments.SingleAsync(t => t.Id == body.Id);
        Assert.Equal(TeamAssignmentStatus.Active, newAssignment.Status);
        Assert.Equal(candidateLeaderId, newAssignment.UserId);
        Assert.Equal(chiefLeaderId, newAssignment.AssignedByUserId);

        // Verify notification
        var notification = await db.InPlatformNotifications.FirstOrDefaultAsync(
            n => n.RecipientUserId == candidateLeaderId && n.NotificationType == InPlatformNotification.ReplacementLeaderAssignedType);
        Assert.NotNull(notification);
        Assert.Equal(eventId, notification.EventId);

        // Verify audit log
        var audit = await db.AuditLogs.FirstOrDefaultAsync(
            a => a.Action == "ReplacementLeaderAssigned" && a.ResourceId == newAssignment.Id);
        Assert.NotNull(audit);
        Assert.Equal("ChiefLeaderReplacement", audit.Source);
        Assert.Equal(cancelledAssignId.ToString(), audit.PreviousValue);
    }

    [Fact]
    public async Task AssignReplacementLeader_AdminOverride_SucceedsWith201()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);
        var adminId = harness.ActorUserId;

        var candidateLeaderId = await harness.SeedUserAsync("volunteer.replacement3", true, CarpediemRoles.Leader);

        harness.Factory.ActorUserId = adminId;
        harness.Factory.ActorRole = CarpediemRoles.Admin;

        var requestBody = new AssignReplacementLeaderRequest(
            CandidateUserId: candidateLeaderId,
            ReplacedAssignmentId: null,
            Justification: "Admin override replacement");

        var response = await PostReplacementAssignmentAsync(harness, eventId, requestBody);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var audit = await db.AuditLogs.FirstOrDefaultAsync(
            a => a.Action == "ReplacementLeaderAssigned" && a.ActorUserId == adminId);
        Assert.NotNull(audit);
        Assert.Equal("AdminOverrideReplacement", audit.Source);
    }

    [Fact]
    public async Task AssignReplacementLeader_WhenEventAlreadyStarted_Returns409WindowClosed()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);
        var candidateLeaderId = await harness.SeedUserAsync("volunteer.replacement4", true, CarpediemRoles.Leader);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var eventItem = await db.Events.SingleAsync(e => e.Id == eventId);
            typeof(Event).GetProperty(nameof(Event.StartDateTime))!
                .SetValue(eventItem, DateTimeOffset.UtcNow.AddHours(-1));
            await db.SaveChangesAsync();
        }

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var requestBody = new AssignReplacementLeaderRequest(candidateLeaderId);
        var response = await PostReplacementAssignmentAsync(harness, eventId, requestBody);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AssignReplacementLeader_WhenCandidateIsChiefLeader_Returns409ChiefLeaderConflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var requestBody = new AssignReplacementLeaderRequest(CandidateUserId: chiefLeaderId);
        var response = await PostReplacementAssignmentAsync(harness, eventId, requestBody);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AssignReplacementLeader_WhenCandidateAlreadyActive_Returns200IdempotentReplay()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);
        var activeVolunteerId = await harness.SeedUserAsync("volunteer.alreadyactive", true, CarpediemRoles.Leader);

        var existingId = await SeedActiveTeamAssignmentAsync(harness, eventId, activeVolunteerId, chiefLeaderId);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var requestBody = new AssignReplacementLeaderRequest(CandidateUserId: activeVolunteerId);
        var response = await PostReplacementAssignmentAsync(harness, eventId, requestBody);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Idempotent-Replay"));

        var body = await response.Content.ReadFromJsonAsync<TeamAssignmentResponse>();
        Assert.NotNull(body);
        Assert.Equal(existingId, body.Id);
        Assert.Equal("Active", body.Status);
    }

    [Fact]
    public async Task AssignReplacementLeader_WithNonCancelledReplacedAssignmentId_Returns400BadRequest()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var activeVolunteerId = await harness.SeedUserAsync("volunteer.active5", true, CarpediemRoles.Leader);
        var candidateLeaderId = await harness.SeedUserAsync("volunteer.replacement5", true, CarpediemRoles.Leader);

        var activeAssignId = await SeedActiveTeamAssignmentAsync(harness, eventId, activeVolunteerId, chiefLeaderId);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var requestBody = new AssignReplacementLeaderRequest(
            CandidateUserId: candidateLeaderId,
            ReplacedAssignmentId: activeAssignId); // Not cancelled!

        var response = await PostReplacementAssignmentAsync(harness, eventId, requestBody);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostReplacementAssignmentAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        AssignReplacementLeaderRequest request,
        string? antiforgeryToken = null)
    {
        var afToken = antiforgeryToken ?? await GetFreshAntiforgeryTokenAsync(harness);
        using var postRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/replacement-assignment")
        {
            Content = JsonContent.Create(request),
        };
        postRequest.Headers.Add("X-XSRF-TOKEN", afToken);
        return await harness.Client.SendAsync(postRequest);
    }

    private static void AssertLink(JsonElement link, string userName, string status)
    {
        Assert.Equal(userName, link.GetProperty("userName").GetString());
        Assert.Equal(status, link.GetProperty("status").GetString());
    }

    private static async Task<string> GetFreshAntiforgeryTokenAsync(DraftApiTestHarness harness)
    {
        var response = await harness.Client.GetAsync("/api/session/antiforgery");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AntiforgeryBody>();
        return body!.RequestToken;
    }

    private static async Task<(Guid EventId, Guid ChiefLeaderUserId)> SetupPublishedEventWithChiefLeaderIdAsync(
        DraftApiTestHarness harness)
    {
        var draftEventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync(
            $"chief.replacement.{Guid.NewGuid():N}",
            true,
            CarpediemRoles.ChiefLeader, CarpediemRoles.Leader);

        var appointResponse = await harness.AppointAsync(draftEventId, candidateId, Guid.NewGuid());
        appointResponse.EnsureSuccessStatusCode();

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var assignment = await db.ChiefLeaderAssignments.SingleAsync(a => a.EventId == draftEventId);
            assignment.AcceptDirect(candidateId, DateTimeOffset.UtcNow, false);
            await db.SaveChangesAsync();
        }

        var draftResponse = await harness.Client.GetAsync($"/api/events/drafts/{draftEventId}");
        draftResponse.EnsureSuccessStatusCode();
        var draftJson = await draftResponse.Content.ReadFromJsonAsync<DraftDetailsResponse>();

        var afToken = await GetFreshAntiforgeryTokenAsync(harness);
        using var publishRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/events/drafts/{draftEventId}/publish");
        publishRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{draftJson!.Version}\"");
        publishRequest.Headers.Add("X-XSRF-TOKEN", afToken);
        var pubResult = await harness.Client.SendAsync(publishRequest);
        pubResult.EnsureSuccessStatusCode();

        return (draftEventId, candidateId);
    }

    private static async Task<Guid> SeedActiveTeamAssignmentAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        Guid volunteerUserId,
        Guid assignedByUserId)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var cycle = await db.StaffingCycles.SingleAsync(c => c.EventId == eventId && c.IsCurrent);
        var eventItem = await db.Events.SingleAsync(e => e.Id == eventId);

        var assignment = TeamAssignment.CreateDirectAssignment(
            cycle.Id,
            eventId,
            volunteerUserId,
            assignedByUserId,
            eventItem.StartDateTime,
            DateTimeOffset.UtcNow,
            isCandidateChiefLeaderOnEvent: false,
            eventStatus: eventItem.Status.ToString());

        db.TeamAssignments.Add(assignment);
        await db.SaveChangesAsync();
        return assignment.Id;
    }

    private static async Task<Guid> SeedCancelledTeamAssignmentAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        Guid volunteerUserId,
        Guid assignedByUserId,
        string justification)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var cycle = await db.StaffingCycles.SingleAsync(c => c.EventId == eventId && c.IsCurrent);
        var eventItem = await db.Events.SingleAsync(e => e.Id == eventId);

        var assignment = TeamAssignment.CreateDirectAssignment(
            cycle.Id,
            eventId,
            volunteerUserId,
            assignedByUserId,
            eventItem.StartDateTime,
            DateTimeOffset.UtcNow.AddHours(-2),
            isCandidateChiefLeaderOnEvent: false,
            eventStatus: eventItem.Status.ToString());

        assignment.Cancel(justification, DateTimeOffset.UtcNow.AddHours(-1), eventItem.StartDateTime);

        db.TeamAssignments.Add(assignment);
        await db.SaveChangesAsync();
        return assignment.Id;
    }

    private static async Task SeedAvailabilityResponseAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        Guid userId,
        AvailabilityResponseType responseType)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var cycle = await db.StaffingCycles.SingleAsync(c => c.EventId == eventId && c.IsCurrent);

        var response = AvailabilityResponse.Declare(
            cycle.Id,
            eventId,
            userId,
            responseType,
            DateTimeOffset.UtcNow.AddDays(10),
            DateTimeOffset.UtcNow,
            isChiefLeaderOnEvent: false,
            eventStatus: "Published");

        db.AvailabilityResponses.Add(response);
        await db.SaveChangesAsync();
    }

    private sealed record AntiforgeryBody(string RequestToken);
    private sealed record DraftDetailsResponse(Guid Id, string Version);
}
