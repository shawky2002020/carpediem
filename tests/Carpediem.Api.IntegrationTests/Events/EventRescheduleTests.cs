using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Carpediem.Web.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Events;

#pragma warning disable CA1707 // Identifiers should not contain underscores

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class EventRescheduleTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task AdminReschedulesPublishedEvent_UpdatesScheduleAndRotatesCycle()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId, initialVersion) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        // Seed 1 active volunteer in current cycle and 1 candidate who declared availability
        var volunteerId = await harness.SeedUserAsync("volunteer.one", true, CarpediemRoles.Leader);
        var candidateId = await harness.SeedUserAsync("candidate.one", true, CarpediemRoles.Leader);

        await SeedActiveTeamAssignmentAsync(harness, eventId, volunteerId, chiefLeaderId);
        await SeedAvailabilityResponseAsync(harness, eventId, candidateId, AvailabilityResponseType.Available);

        var newStart = DateTimeOffset.UtcNow.AddDays(14);
        var newEnd = newStart.AddHours(5);
        var justification = "Venue unavailable on original date due to urgent maintenance.";

        var requestBody = new RescheduleEventRequest(newStart, newEnd, justification);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/reschedule")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("If-Match", $"\"{initialVersion}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RescheduleEventResponse>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.Equal("Published", body.Status);
        Assert.Equal(2, body.CycleNumber);
        Assert.NotEqual(initialVersion, body.Version);
        Assert.Equal(3, body.NotificationsDispatched); // Chief Leader, active volunteer, candidate

        // Verify database persistence and cycle rotation
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var updatedEvent = await db.Events
            .Include(e => e.StaffingCycles)
            .SingleAsync(e => e.Id == eventId);

        Assert.Equal(newStart, updatedEvent.StartDateTime);
        Assert.Equal(newEnd, updatedEvent.EndDateTime);
        Assert.Equal(EventStatus.Published, updatedEvent.Status);

        Assert.Equal(2, updatedEvent.StaffingCycles.Count);
        var cycle1 = updatedEvent.StaffingCycles.Single(c => c.CycleNumber == 1);
        var cycle2 = updatedEvent.StaffingCycles.Single(c => c.CycleNumber == 2);

        Assert.False(cycle1.IsCurrent);
        Assert.True(cycle2.IsCurrent);
        Assert.Equal(DateOnly.FromDateTime(newStart.Date), cycle2.EffectiveEventDate);

        // Verify notifications created
        var notifications = await db.InPlatformNotifications
            .Where(n => n.EventId == eventId && n.NotificationType == "EventRescheduled")
            .ToListAsync();

        Assert.Equal(3, notifications.Count);
        Assert.Contains(notifications, n => n.RecipientUserId == chiefLeaderId);
        Assert.Contains(notifications, n => n.RecipientUserId == volunteerId);
        Assert.Contains(notifications, n => n.RecipientUserId == candidateId);

        // Verify audit log entry
        var audits = await db.AuditLogs
            .Where(a => a.ResourceId == eventId && a.Action == "EventRescheduled")
            .ToListAsync();

        Assert.Single(audits);
        Assert.Equal(harness.ActorUserId, audits[0].ActorUserId);
        Assert.Equal(justification, audits[0].MandatoryReason);
        Assert.Equal("AdminReschedule", audits[0].Source);

        // Verify new cycle has 0 assignments and 0 availability responses
        var cycle2Assignments = await db.TeamAssignments
            .Where(a => a.StaffingCycleId == cycle2.Id)
            .ToListAsync();
        Assert.Empty(cycle2Assignments);

        var cycle2Responses = await db.AvailabilityResponses
            .Where(a => a.StaffingCycleId == cycle2.Id)
            .ToListAsync();
        Assert.Empty(cycle2Responses);

        // Historical assignments and responses remain intact under cycle 1
        var cycle1Assignments = await db.TeamAssignments
            .Where(a => a.StaffingCycleId == cycle1.Id)
            .ToListAsync();
        Assert.Single(cycle1Assignments);
        Assert.Equal(volunteerId, cycle1Assignments[0].UserId);

        var cycle1Responses = await db.AvailabilityResponses
            .Where(a => a.StaffingCycleId == cycle1.Id)
            .ToListAsync();
        Assert.Single(cycle1Responses);
        Assert.Equal(candidateId, cycle1Responses[0].UserId);
    }

    [Fact]
    public async Task Reschedule_WhenEventInDraftStatus_Returns409Conflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var draftEventId = await harness.CreateDefaultDraftAsync();
        var version = await GetEventVersionAsync(harness, draftEventId);

        var newStart = DateTimeOffset.UtcNow.AddDays(7);
        var newEnd = newStart.AddHours(4);
        var requestBody = new RescheduleEventRequest(newStart, newEnd, "Attempting to reschedule a draft event.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{draftEventId}/reschedule")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("If-Match", $"\"{version}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reschedule_WhenEventAlreadyStarted_Returns409Conflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, _, version) = await SetupPastPublishedEventWithChiefLeaderAsync(harness);

        var newStart = DateTimeOffset.UtcNow.AddDays(7);
        var newEnd = newStart.AddHours(4);
        var requestBody = new RescheduleEventRequest(newStart, newEnd, "Attempting to reschedule an event already started.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/reschedule")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("If-Match", $"\"{version}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reschedule_NonAdminUser_Returns403Forbidden()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId, version) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        // Switch actor to Chief Leader
        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var newStart = DateTimeOffset.UtcNow.AddDays(7);
        var newEnd = newStart.AddHours(4);
        var requestBody = new RescheduleEventRequest(newStart, newEnd, "Chief Leader attempting to reschedule.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/reschedule")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("If-Match", $"\"{version}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reschedule_StaleVersion_Returns412PreconditionFailed()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, _, _) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var staleVersion = Convert.ToBase64String(new byte[8] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var newStart = DateTimeOffset.UtcNow.AddDays(7);
        var newEnd = newStart.AddHours(4);
        var requestBody = new RescheduleEventRequest(newStart, newEnd, "Reschedule with stale token.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/reschedule")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("If-Match", $"\"{staleVersion}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Fact]
    public async Task Reschedule_MissingOrShortJustification_Returns400BadRequest()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, _, version) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var newStart = DateTimeOffset.UtcNow.AddDays(7);
        var newEnd = newStart.AddHours(4);
        var requestBody = new RescheduleEventRequest(newStart, newEnd, "Too short"); // 9 chars < 10

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/reschedule")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("If-Match", $"\"{version}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reschedule_PastNewStartOrInvalidSchedule_Returns400BadRequest()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, _, version) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var pastStart = DateTimeOffset.UtcNow.AddDays(-2);
        var pastEnd = pastStart.AddHours(4);
        var requestBody = new RescheduleEventRequest(pastStart, pastEnd, "Reschedule with past new start date.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/reschedule")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("If-Match", $"\"{version}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reschedule_MissingOrInvalidIfMatchHeader_Returns400BadRequest()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, _, _) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var newStart = DateTimeOffset.UtcNow.AddDays(7);
        var newEnd = newStart.AddHours(4);
        var requestBody = new RescheduleEventRequest(newStart, newEnd, "Reschedule without If-Match header.");

        // Missing If-Match
        using var request1 = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/reschedule")
        {
            Content = JsonContent.Create(requestBody)
        };
        request1.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response1 = await harness.Client.SendAsync(request1);
        Assert.Equal(HttpStatusCode.BadRequest, response1.StatusCode);

        // Invalid base64
        using var request2 = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/reschedule")
        {
            Content = JsonContent.Create(requestBody)
        };
        request2.Headers.TryAddWithoutValidation("If-Match", "\"invalid-base64-content!\"");
        request2.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);

        var response2 = await harness.Client.SendAsync(request2);
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
    }

    private static async Task<(Guid eventId, Guid chiefLeaderId, string version)> SetupPublishedEventWithChiefLeaderAsync(DraftApiTestHarness harness)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var adminId = harness.ActorUserId;
        var chiefLeaderId = await harness.SeedUserAsync("chief.reschedule", true, CarpediemRoles.ChiefLeader, CarpediemRoles.Leader);

        var start = DateTimeOffset.UtcNow.AddDays(7);
        var end = start.AddHours(4);

        var eventItem = Event.CreateDraft("Operational Gala", start, end, "Grand Ballroom", null,
            adminId, DateTimeOffset.UtcNow, DateOnly.FromDateTime(start.Date));

        var assignment = eventItem.AppointChiefLeader(chiefLeaderId, adminId, DateTimeOffset.UtcNow);
        assignment.AcceptDirect(chiefLeaderId, DateTimeOffset.UtcNow, hasActiveTeamAssignmentOnCurrentCycle: false);

        eventItem.Publish(adminId, DateTimeOffset.UtcNow);

        db.Events.Add(eventItem);
        await db.SaveChangesAsync();

        var version = Convert.ToBase64String(eventItem.RowVersion);
        return (eventItem.Id, chiefLeaderId, version);
    }

    private static async Task<(Guid eventId, Guid chiefLeaderId, string version)> SetupPastPublishedEventWithChiefLeaderAsync(DraftApiTestHarness harness)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var adminId = harness.ActorUserId;
        var chiefLeaderId = await harness.SeedUserAsync("chief.past", true, CarpediemRoles.ChiefLeader, CarpediemRoles.Leader);

        var start = DateTimeOffset.UtcNow.AddDays(-2);
        var end = start.AddHours(4);

        var eventItem = Event.CreateDraft("Past Gala", start, end, "Grand Ballroom", null,
            adminId, DateTimeOffset.UtcNow.AddDays(-3), DateOnly.FromDateTime(start.Date));

        var assignment = eventItem.AppointChiefLeader(chiefLeaderId, adminId, DateTimeOffset.UtcNow.AddDays(-3));
        assignment.AcceptDirect(chiefLeaderId, DateTimeOffset.UtcNow.AddDays(-3), hasActiveTeamAssignmentOnCurrentCycle: false);

        eventItem.Publish(adminId, DateTimeOffset.UtcNow.AddDays(-3));

        db.Events.Add(eventItem);
        await db.SaveChangesAsync();

        var version = Convert.ToBase64String(eventItem.RowVersion);
        return (eventItem.Id, chiefLeaderId, version);
    }

    private static async Task SeedActiveTeamAssignmentAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        Guid volunteerId,
        Guid assignedByUserId)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var cycle = await db.StaffingCycles.SingleAsync(c => c.EventId == eventId && c.IsCurrent);
        var eventItem = await db.Events.SingleAsync(e => e.Id == eventId);

        var assignment = TeamAssignment.CreateDirectAssignment(
            cycle.Id,
            eventId,
            volunteerId,
            assignedByUserId,
            eventItem.StartDateTime,
            DateTimeOffset.UtcNow,
            isCandidateChiefLeaderOnEvent: false,
            eventStatus: eventItem.Status.ToString());

        db.TeamAssignments.Add(assignment);
        await db.SaveChangesAsync();
    }

    private static async Task SeedAvailabilityResponseAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        Guid volunteerId,
        AvailabilityResponseType responseType)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var cycle = await db.StaffingCycles.SingleAsync(c => c.EventId == eventId && c.IsCurrent);

        var response = AvailabilityResponse.Declare(
            cycle.Id,
            eventId,
            volunteerId,
            responseType,
            DateTimeOffset.UtcNow.AddDays(10),
            DateTimeOffset.UtcNow,
            isChiefLeaderOnEvent: false,
            eventStatus: "Published");

        db.AvailabilityResponses.Add(response);
        await db.SaveChangesAsync();
    }

    private static async Task<string> GetEventVersionAsync(DraftApiTestHarness harness, Guid eventId)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var eventItem = await db.Events.SingleAsync(e => e.Id == eventId);
        return Convert.ToBase64String(eventItem.RowVersion);
    }
}
