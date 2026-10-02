using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Notifications.Domain;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Staffing;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class EventSelfCancellationTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task VolunteerCanQueryOwnAssignmentContextReturns200Ok()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.leader1", true, CarpediemRoles.Leader);
        var assignmentId = await SeedActiveTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        harness.Factory.ActorUserId = volunteerUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var response = await harness.Client.GetAsync($"/api/events/{eventId}/my-assignment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MyAssignmentResponseBody>();
        Assert.NotNull(body);
        Assert.Equal(assignmentId, body.AssignmentId);
        Assert.Equal(eventId, body.EventId);
        Assert.Equal(volunteerUserId, body.UserId);
        Assert.Equal("Active", body.Status);
        Assert.True(body.IsWindowOpen);
        Assert.False(string.IsNullOrWhiteSpace(body.Version));
        Assert.Null(body.CancelledAt);
        Assert.Null(body.CancellationReason);
    }

    [Fact]
    public async Task QueryMyAssignmentOnDraftEventFailsWith409InvalidEventStatus()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var draftEventId = await harness.CreateDefaultDraftAsync();
        var volunteerUserId = await harness.SeedUserAsync("volunteer.draft", true, CarpediemRoles.Leader);

        harness.Factory.ActorUserId = volunteerUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var response = await harness.Client.GetAsync($"/api/events/{draftEventId}/my-assignment");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task VolunteerCanSelfCancelAssignmentWithMandatoryJustificationReturns200Ok()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);
        var initialAdminId = harness.ActorUserId;

        var volunteerUserId = await harness.SeedUserAsync("volunteer.selfcancel", true, CarpediemRoles.Leader);
        var assignmentId = await SeedActiveTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        harness.Factory.ActorUserId = volunteerUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        const string justification = "  Unavoidable exam schedule conflict on event date.  ";
        var response = await PostCancelAssignmentAsync(harness, eventId, assignmentId, justification);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CancelledAssignmentResponseBody>();
        Assert.NotNull(body);
        Assert.Equal(assignmentId, body.Id);
        Assert.Equal("Cancelled", body.Status);
        Assert.Equal("Unavoidable exam schedule conflict on event date.", body.CancellationReason);

        // Verify Database State
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

            var assignment = await db.TeamAssignments.SingleAsync(a => a.Id == assignmentId);
            Assert.Equal(TeamAssignmentStatus.Cancelled, assignment.Status);
            Assert.Equal("Unavoidable exam schedule conflict on event date.", assignment.CancellationReason);
            Assert.NotNull(assignment.CancelledAt);

            // Notifications to Chief Leader and Admin
            var notifications = await db.InPlatformNotifications
                .Where(n => n.EventId == eventId && n.NotificationType == InPlatformNotification.TeamMemberCancelledType)
                .ToListAsync();
            Assert.Contains(notifications, n => n.RecipientUserId == chiefLeaderId);
            Assert.Contains(notifications, n => n.RecipientUserId == initialAdminId);

            // Audit Log
            var audit = await db.AuditLogs
                .SingleOrDefaultAsync(a => a.ResourceId == assignmentId && a.Action == "VolunteerSelfCancelled");
            Assert.NotNull(audit);
            Assert.Equal(volunteerUserId, audit.ActorUserId);
            Assert.Equal("Active", audit.PreviousValue);
            Assert.Equal("Cancelled", audit.NewValue);
            Assert.Equal("VolunteerSelfCancellation", audit.Source);
            Assert.Equal("Unavoidable exam schedule conflict on event date.", audit.MandatoryReason);
        }
    }

    [Fact]
    public async Task AdminCanCancelAssignmentWithOverrideAuthorityReturns200Ok()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.admincancel", true, CarpediemRoles.Leader);
        var assignmentId = await SeedActiveTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        harness.Factory.ActorUserId = harness.ActorUserId;
        harness.Factory.ActorRole = CarpediemRoles.Admin;

        const string adminReason = "Administrative roster removal requested via phone support.";
        var response = await PostCancelAssignmentAsync(harness, eventId, assignmentId, adminReason);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var audit = await db.AuditLogs
                .SingleOrDefaultAsync(a => a.ResourceId == assignmentId && a.Action == "VolunteerSelfCancelled");
            Assert.NotNull(audit);
            Assert.Equal(harness.ActorUserId, audit.ActorUserId);
            Assert.Equal("AdminOverrideCancellation", audit.Source);
            Assert.Equal(adminReason, audit.MandatoryReason);
        }
    }

    [Fact]
    public async Task NonOwnerAndNonAdminIsRejectedWith403Forbidden()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var assignedVolunteerId = await harness.SeedUserAsync("volunteer.owner", true, CarpediemRoles.Leader);
        var assignmentId = await SeedActiveTeamAssignmentAsync(harness, eventId, assignedVolunteerId, chiefLeaderId);

        var intruderId = await harness.SeedUserAsync("volunteer.intruder", true, CarpediemRoles.Leader);
        harness.Factory.ActorUserId = intruderId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var response = await PostCancelAssignmentAsync(harness, eventId, assignmentId, "Unauthorized attempt");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EmptyOrWhitespaceJustificationReturns400BadRequest()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.emptyreason", true, CarpediemRoles.Leader);
        var assignmentId = await SeedActiveTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        harness.Factory.ActorUserId = volunteerUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var emptyResponse = await PostCancelAssignmentAsync(harness, eventId, assignmentId, "   ");
        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);
    }

    [Fact]
    public async Task ExcessiveJustificationReturns400BadRequest()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.longreason", true, CarpediemRoles.Leader);
        var assignmentId = await SeedActiveTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        harness.Factory.ActorUserId = volunteerUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var excessiveReason = new string('x', 501);
        var response = await PostCancelAssignmentAsync(harness, eventId, assignmentId, excessiveReason);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancellationWhenEventAlreadyStartedFailsWith409WindowClosed()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.paststart", true, CarpediemRoles.Leader);
        var assignmentId = await SeedActiveTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        // Advance Event StartDateTime into the past
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var eventItem = await db.Events.SingleAsync(e => e.Id == eventId);
            typeof(Event).GetProperty(nameof(Event.StartDateTime))!
                .SetValue(eventItem, DateTimeOffset.UtcNow.AddHours(-1));
            await db.SaveChangesAsync();
        }

        harness.Factory.ActorUserId = volunteerUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var response = await PostCancelAssignmentAsync(harness, eventId, assignmentId, "I can no longer attend");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task IdempotentReplayReturns200OkWithHeader()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.replay", true, CarpediemRoles.Leader);
        var assignmentId = await SeedActiveTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        harness.Factory.ActorUserId = volunteerUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        const string reason = "Genuine medical absence.";

        // First cancellation
        var response1 = await PostCancelAssignmentAsync(harness, eventId, assignmentId, reason);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.False(response1.Headers.Contains("Idempotent-Replay"));

        // Idempotent replay with identical reason
        var response2 = await PostCancelAssignmentAsync(harness, eventId, assignmentId, reason);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.True(response2.Headers.Contains("Idempotent-Replay"));
        Assert.Equal("true", response2.Headers.GetValues("Idempotent-Replay").First());

        // Verify no duplicate notifications or audit logs were created
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var auditCount = await db.AuditLogs
                .CountAsync(a => a.ResourceId == assignmentId && a.Action == "VolunteerSelfCancelled");
            Assert.Equal(1, auditCount);
        }
    }

    [Fact]
    public async Task MismatchedConcurrencyVersionReturns412PreconditionFailed()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.concurrency", true, CarpediemRoles.Leader);
        var assignmentId = await SeedActiveTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        harness.Factory.ActorUserId = volunteerUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var bogusVersion = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var response = await PostCancelAssignmentAsync(harness, eventId, assignmentId, "Valid reason", ifMatch: bogusVersion);
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostCancelAssignmentAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        Guid assignmentId,
        string justification,
        string? ifMatch = null)
    {
        var afToken = await GetFreshAntiforgeryTokenAsync(harness);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/assignments/{assignmentId}/cancel")
        {
            Content = JsonContent.Create(new { Justification = justification })
        };
        request.Headers.Add("X-XSRF-TOKEN", afToken);
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        }
        return await harness.Client.SendAsync(request);
    }

    private static async Task<string> GetFreshAntiforgeryTokenAsync(DraftApiTestHarness harness)
    {
        var afResponse = await harness.Client.GetAsync("/api/session/antiforgery");
        afResponse.EnsureSuccessStatusCode();
        var afBody = await afResponse.Content.ReadFromJsonAsync<AntiforgeryBody>();
        return afBody!.RequestToken;
    }

    private static async Task<(Guid EventId, Guid ChiefLeaderId)> SetupPublishedEventWithChiefLeaderIdAsync(
        DraftApiTestHarness harness)
    {
        var draftEventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync($"chief.{Guid.NewGuid():N}", true,
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

    private sealed record AntiforgeryBody(string RequestToken);
    private sealed record DraftDetailsResponse(Guid Id, string Version);
}

public sealed record MyAssignmentResponseBody(
    Guid AssignmentId,
    Guid EventId,
    Guid StaffingCycleId,
    Guid UserId,
    string UserName,
    string EventTitle,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    string Status,
    DateTimeOffset AssignedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    bool IsWindowOpen,
    string Version);

public sealed record CancelledAssignmentResponseBody(
    Guid Id,
    Guid EventId,
    Guid StaffingCycleId,
    Guid UserId,
    string Status,
    DateTimeOffset AssignedAt,
    Guid AssignedByUserId,
    string CancellationReason,
    DateTimeOffset CancelledAt,
    string Version);
