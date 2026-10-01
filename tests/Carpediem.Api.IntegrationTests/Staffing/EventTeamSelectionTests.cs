using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Notifications.Domain;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Staffing;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class EventTeamSelectionTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task ChiefLeaderCanQueryTeamSelectionContextOnPublishedEventReturns200WithCandidatesAndRoster()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var candidateUserId = await harness.SeedUserAsync("leader.candidate1", true, CarpediemRoles.Leader);
        await SeedAvailabilityAsync(harness, eventId, candidateUserId, AvailabilityResponseType.Available);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var contextResponse = await harness.Client.GetAsync($"/api/events/{eventId}/team-selection");
        Assert.Equal(HttpStatusCode.OK, contextResponse.StatusCode);

        var context = await contextResponse.Content.ReadFromJsonAsync<TeamSelectionContextBody>();
        Assert.NotNull(context);
        Assert.Equal(eventId, context.EventId);
        Assert.True(context.IsWindowOpen);
        Assert.Single(context.AvailableCandidates);
        Assert.Equal(candidateUserId, context.AvailableCandidates[0].UserId);
        Assert.Empty(context.ActiveTeam);
    }

    [Fact]
    public async Task NonChiefLeaderAndNonAdminUserIsRejectedWith403Forbidden()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, _) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var otherLeaderId = await harness.SeedUserAsync("leader.other", true, CarpediemRoles.Leader);
        harness.Factory.ActorUserId = otherLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var getResponse = await harness.Client.GetAsync($"/api/events/{eventId}/team-selection");
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);

        var postResponse = await PostSelectVolunteerAsync(harness, eventId, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
    }

    [Fact]
    public async Task TeamSelectionOnDraftEventFailsWith409InvalidEventStatus()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
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

        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var response = await PostSelectVolunteerAsync(harness, draftEventId, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChiefLeaderCanSelectAvailableVolunteerReturns201CreatedAndPersistsAssignmentAuditAndNotification()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var candidateUserId = await harness.SeedUserAsync("leader.selected", true, CarpediemRoles.Leader);
        await SeedAvailabilityAsync(harness, eventId, candidateUserId, AvailabilityResponseType.Available);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var response = await PostSelectVolunteerAsync(harness, eventId, candidateUserId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TeamAssignmentBody>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.Equal(candidateUserId, body.UserId);
        Assert.Equal("Active", body.Status);
        Assert.NotEmpty(body.Version);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var stored = await db.TeamAssignments.SingleAsync(t => t.EventId == eventId && t.UserId == candidateUserId);
        Assert.Equal(TeamAssignmentStatus.Active, stored.Status);
        Assert.Equal(chiefLeaderId, stored.AssignedByUserId);

        var audit = await db.AuditLogs.SingleAsync(a => a.ResourceId == stored.Id && a.Action == "VolunteerSelected");
        Assert.Equal(chiefLeaderId, audit.ActorUserId);
        Assert.Equal("Active", audit.NewValue);

        var notification = await db.InPlatformNotifications.SingleAsync(n =>
            n.EventId == eventId && n.RecipientUserId == candidateUserId && n.NotificationType == InPlatformNotification.VolunteerSelectedType);
        Assert.NotNull(notification);
    }

    [Fact]
    public async Task ChiefLeaderCannotSelectSelfOnLedEventReturns409ChiefLeaderConflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        await SeedAvailabilityAsync(harness, eventId, chiefLeaderId, AvailabilityResponseType.Available);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var response = await PostSelectVolunteerAsync(harness, eventId, chiefLeaderId);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SelectingCandidateWhoIsNotAvailableFailsWith409CandidateNotAvailable()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var notAvailableUserId = await harness.SeedUserAsync("leader.notavail", true, CarpediemRoles.Leader);
        await SeedAvailabilityAsync(harness, eventId, notAvailableUserId, AvailabilityResponseType.NotAvailable);

        var noResponseUserId = await harness.SeedUserAsync("leader.noresponse", true, CarpediemRoles.Leader);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var res1 = await PostSelectVolunteerAsync(harness, eventId, notAvailableUserId);
        Assert.Equal(HttpStatusCode.Conflict, res1.StatusCode);

        var res2 = await PostSelectVolunteerAsync(harness, eventId, noResponseUserId);
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task SelectingAfterEventStartFailsWith409WindowClosed()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var candidateUserId = await harness.SeedUserAsync("leader.toolate", true, CarpediemRoles.Leader);
        await SeedAvailabilityAsync(harness, eventId, candidateUserId, AvailabilityResponseType.Available);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var pastTime = DateTimeOffset.UtcNow.AddHours(-1);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events.Events SET StartDateTime = {pastTime} WHERE Id = {eventId}");
        }

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var response = await PostSelectVolunteerAsync(harness, eventId, candidateUserId);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task IdempotentReplayReturns200WithHeaderAndNoExtraAuditEntry()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var candidateUserId = await harness.SeedUserAsync("leader.replay", true, CarpediemRoles.Leader);
        await SeedAvailabilityAsync(harness, eventId, candidateUserId, AvailabilityResponseType.Available);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var firstResponse = await PostSelectVolunteerAsync(harness, eventId, candidateUserId);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var replayResponse = await PostSelectVolunteerAsync(harness, eventId, candidateUserId);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.True(replayResponse.Headers.Contains("Idempotent-Replay"));
        Assert.Equal("true", replayResponse.Headers.GetValues("Idempotent-Replay").First());

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var audits = await db.AuditLogs.Where(a => a.Action == "VolunteerSelected" && a.ActorUserId == chiefLeaderId).ToListAsync();
        Assert.Single(audits);
    }

    [Fact]
    public async Task AdminCanSelectAvailableVolunteerOnBehalfOfEventReturns201WithAdminSource()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, _) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var candidateUserId = await harness.SeedUserAsync("leader.adminselected", true, CarpediemRoles.Leader);
        await SeedAvailabilityAsync(harness, eventId, candidateUserId, AvailabilityResponseType.Available);

        var response = await PostSelectVolunteerAsync(harness, eventId, candidateUserId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var assignment = await db.TeamAssignments.SingleAsync(t => t.EventId == eventId && t.UserId == candidateUserId);
        var audit = await db.AuditLogs.SingleAsync(a => a.ResourceId == assignment.Id && a.Action == "VolunteerSelected");
        Assert.Equal("AdminSelection", audit.Source);
    }

    private static async Task<HttpResponseMessage> PostSelectVolunteerAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        Guid candidateUserId)
    {
        var afToken = await GetFreshAntiforgeryTokenAsync(harness);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/team-selection")
        {
            Content = JsonContent.Create(new { candidateUserId })
        };
        request.Headers.Add("X-XSRF-TOKEN", afToken);
        return await harness.Client.SendAsync(request);
    }

    private static async Task<string> GetFreshAntiforgeryTokenAsync(DraftApiTestHarness harness)
    {
        var afResponse = await harness.Client.GetAsync("/api/session/antiforgery");
        afResponse.EnsureSuccessStatusCode();
        var afBody = await afResponse.Content.ReadFromJsonAsync<AntiforgeryBody>();
        return afBody!.RequestToken;
    }

    private static async Task SeedAvailabilityAsync(
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
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow,
            false,
            "Published");
        db.AvailabilityResponses.Add(response);
        await db.SaveChangesAsync();
    }

    private static async Task<(Guid EventId, Guid ChiefLeaderId)> SetupPublishedEventWithChiefLeaderIdAsync(
        DraftApiTestHarness harness)
    {
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync($"chief.{Guid.NewGuid():N}", true,
            CarpediemRoles.ChiefLeader, CarpediemRoles.Leader);

        var appointResponse = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());
        appointResponse.EnsureSuccessStatusCode();

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var assignment = await db.ChiefLeaderAssignments.SingleAsync(a => a.EventId == eventId);
            assignment.AcceptDirect(candidateId, DateTimeOffset.UtcNow, false);
            await db.SaveChangesAsync();
        }

        var draftResponse = await harness.Client.GetAsync($"/api/events/drafts/{eventId}");
        draftResponse.EnsureSuccessStatusCode();
        var draftJson = await draftResponse.Content.ReadFromJsonAsync<DraftDetailsResponse>();

        var afToken = await GetFreshAntiforgeryTokenAsync(harness);
        using var publishRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/events/drafts/{eventId}/publish");
        publishRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{draftJson!.Version}\"");
        publishRequest.Headers.Add("X-XSRF-TOKEN", afToken);
        var pubResult = await harness.Client.SendAsync(publishRequest);
        pubResult.EnsureSuccessStatusCode();

        return (eventId, candidateId);
    }

    private sealed record AntiforgeryBody(string RequestToken);
    private sealed record DraftDetailsResponse(Guid Id, string Version);
    private sealed record TeamSelectionContextBody(
        Guid EventId,
        Guid StaffingCycleId,
        string EventTitle,
        DateTimeOffset StartDateTime,
        DateTimeOffset EndDateTime,
        string Location,
        bool IsWindowOpen,
        AvailableCandidateBody[] AvailableCandidates,
        ActiveTeamMemberBody[] ActiveTeam);
    private sealed record AvailableCandidateBody(
        Guid UserId,
        string UserName,
        DateTimeOffset DeclaredAt);
    private sealed record ActiveTeamMemberBody(
        Guid AssignmentId,
        Guid UserId,
        string UserName,
        DateTimeOffset AssignedAt,
        string Version);
    private sealed record TeamAssignmentBody(
        Guid Id,
        Guid EventId,
        Guid StaffingCycleId,
        Guid UserId,
        string Status,
        DateTimeOffset AssignedAt,
        Guid AssignedByUserId,
        string Version);
}
