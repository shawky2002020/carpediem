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
public sealed class EventDirectAssignmentTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task ChiefLeaderCanQueryDirectAssignmentCandidatesReturns200Ok()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        // Candidate 1: NotAvailable
        var candidate1Id = await harness.SeedUserAsync("leader.notavailable", true, CarpediemRoles.Leader);
        await SeedAvailabilityAsync(harness, eventId, candidate1Id, AvailabilityResponseType.NotAvailable);

        // Candidate 2: No Response
        var candidate2Id = await harness.SeedUserAsync("leader.noresponse", true, CarpediemRoles.Leader);

        // Candidate 3: Available (should be excluded from direct draft candidates)
        var candidate3Id = await harness.SeedUserAsync("leader.available", true, CarpediemRoles.Leader);
        await SeedAvailabilityAsync(harness, eventId, candidate3Id, AvailabilityResponseType.Available);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var response = await harness.Client.GetAsync($"/api/events/{eventId}/direct-assignment/candidates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DirectAssignmentCandidatesResponseBody>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.True(body.IsWindowOpen);

        // Verify candidate1 (NotAvailable) and candidate2 (NoResponse) are present, candidate3 (Available) is excluded
        var c1 = body.Candidates.SingleOrDefault(c => c.UserId == candidate1Id);
        Assert.NotNull(c1);
        Assert.Equal("NotAvailable", c1.AvailabilityStatus);
        Assert.NotNull(c1.DeclaredAt);

        var c2 = body.Candidates.SingleOrDefault(c => c.UserId == candidate2Id);
        Assert.NotNull(c2);
        Assert.Equal("NoResponse", c2.AvailabilityStatus);
        Assert.Null(c2.DeclaredAt);

        Assert.DoesNotContain(body.Candidates, c => c.UserId == candidate3Id);
        Assert.DoesNotContain(body.Candidates, c => c.UserId == chiefLeaderId);
    }

    [Fact]
    public async Task NonChiefLeaderAndNonAdminUserIsRejectedWith403Forbidden()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, _) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var otherLeaderId = await harness.SeedUserAsync("leader.other", true, CarpediemRoles.Leader);
        harness.Factory.ActorUserId = otherLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var getResponse = await harness.Client.GetAsync($"/api/events/{eventId}/direct-assignment/candidates");
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);

        var postResponse = await PostDirectAssignLeaderAsync(harness, eventId, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
    }

    [Fact]
    public async Task DirectAssignmentOnDraftEventFailsWith409InvalidEventStatus()
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

        var getResponse = await harness.Client.GetAsync($"/api/events/{draftEventId}/direct-assignment/candidates");
        Assert.Equal(HttpStatusCode.Conflict, getResponse.StatusCode);

        var postResponse = await PostDirectAssignLeaderAsync(harness, draftEventId, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, postResponse.StatusCode);
    }

    [Fact]
    public async Task ChiefLeaderCanDirectlyAssignNonRespondentReturns201CreatedAndPersistsAssignmentAuditAndNotification()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var candidateUserId = await harness.SeedUserAsync("leader.nonrespondent", true, CarpediemRoles.Leader);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var response = await PostDirectAssignLeaderAsync(harness, eventId, candidateUserId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TeamAssignmentResponseBody>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.Equal(candidateUserId, body.UserId);
        Assert.Equal("Active", body.Status);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

            var assignment = await db.TeamAssignments.SingleOrDefaultAsync(t => t.Id == body.Id);
            Assert.NotNull(assignment);
            Assert.Equal(TeamAssignmentStatus.Active, assignment.Status);

            var notification = await db.InPlatformNotifications
                .SingleOrDefaultAsync(n => n.RecipientUserId == candidateUserId && n.NotificationType == InPlatformNotification.DirectLeaderAssignedType);
            Assert.NotNull(notification);

            var audit = await db.AuditLogs
                .SingleOrDefaultAsync(a => a.ResourceId == body.Id && a.Action == "DirectLeaderAssignmentExecuted");
            Assert.NotNull(audit);
            Assert.Equal("ChiefLeaderDirectDraft", audit.Source);
            Assert.Equal("NoResponse", audit.PreviousValue);
            Assert.Equal("Active", audit.NewValue);
        }
    }

    [Fact]
    public async Task ChiefLeaderCanDirectlyAssignNotAvailableLeaderReturns201CreatedAndPersistsAssignmentAuditNotingNotAvailable()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var candidateUserId = await harness.SeedUserAsync("leader.drafted_not_avail", true, CarpediemRoles.Leader);
        await SeedAvailabilityAsync(harness, eventId, candidateUserId, AvailabilityResponseType.NotAvailable);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var response = await PostDirectAssignLeaderAsync(harness, eventId, candidateUserId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TeamAssignmentResponseBody>();
        Assert.NotNull(body);
        Assert.Equal("Active", body.Status);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

            var audit = await db.AuditLogs
                .SingleOrDefaultAsync(a => a.ResourceId == body.Id && a.Action == "DirectLeaderAssignmentExecuted");
            Assert.NotNull(audit);
            Assert.Equal("ChiefLeaderDirectDraft", audit.Source);
            Assert.Equal("NotAvailable", audit.PreviousValue);
            Assert.Equal("Active", audit.NewValue);
        }
    }

    [Fact]
    public async Task SelectingActiveChiefLeaderFailsWith409ChiefLeaderConflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var response = await PostDirectAssignLeaderAsync(harness, eventId, chiefLeaderId);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ApiProblemBody>();
        Assert.NotNull(problem);
        Assert.Equal("chief_leader_conflict", problem.Code);
    }

    [Fact]
    public async Task DirectAssignLeaderAfterEventStartFailsWith409WindowClosed()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var candidateUserId = await harness.SeedUserAsync("leader.past", true, CarpediemRoles.Leader);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var pastTime = DateTimeOffset.UtcNow.AddMinutes(-5);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events.Events SET StartDateTime = {pastTime} WHERE Id = {eventId}");
        }

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var response = await PostDirectAssignLeaderAsync(harness, eventId, candidateUserId);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ApiProblemBody>();
        Assert.NotNull(problem);
        Assert.Equal("window_closed", problem.Code);
    }

    [Fact]
    public async Task IdempotentReplayReturns200WithHeaderAndNoExtraAuditOrNotification()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var candidateUserId = await harness.SeedUserAsync("leader.replay", true, CarpediemRoles.Leader);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var firstResponse = await PostDirectAssignLeaderAsync(harness, eventId, candidateUserId);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await PostDirectAssignLeaderAsync(harness, eventId, candidateUserId);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.True(secondResponse.Headers.Contains("Idempotent-Replay"));
        Assert.Equal("true", secondResponse.Headers.GetValues("Idempotent-Replay").First());

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

            var assignments = await db.TeamAssignments
                .Where(t => t.EventId == eventId && t.UserId == candidateUserId)
                .ToListAsync();
            Assert.Single(assignments);

            var audits = await db.AuditLogs
                .Where(a => a.ResourceId == assignments[0].Id && a.Action == "DirectLeaderAssignmentExecuted")
                .ToListAsync();
            Assert.Single(audits);

            var notifications = await db.InPlatformNotifications
                .Where(n => n.RecipientUserId == candidateUserId && n.NotificationType == InPlatformNotification.DirectLeaderAssignedType)
                .ToListAsync();
            Assert.Single(notifications);
        }
    }

    [Fact]
    public async Task AdminCanQueryAndDirectAssignLeaders()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, _) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        var candidateUserId = await harness.SeedUserAsync("leader.admin_drafted", true, CarpediemRoles.Leader);

        var getResponse = await harness.Client.GetAsync($"/api/events/{eventId}/direct-assignment/candidates");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var postResponse = await PostDirectAssignLeaderAsync(harness, eventId, candidateUserId);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var body = await postResponse.Content.ReadFromJsonAsync<TeamAssignmentResponseBody>();
        Assert.NotNull(body);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var audit = await db.AuditLogs.SingleOrDefaultAsync(a => a.ResourceId == body.Id && a.Action == "DirectLeaderAssignmentExecuted");
            Assert.NotNull(audit);
            Assert.Equal("AdminDirectDraft", audit.Source);
        }
    }

    private static async Task<HttpResponseMessage> PostDirectAssignLeaderAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        Guid candidateUserId)
    {
        var afToken = await GetFreshAntiforgeryTokenAsync(harness);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/direct-assignment")
        {
            Content = JsonContent.Create(new { candidateUserId })
        };
        message.Headers.Add("X-XSRF-TOKEN", afToken);
        return await harness.Client.SendAsync(message);
    }

    private static async Task<string> GetFreshAntiforgeryTokenAsync(DraftApiTestHarness harness)
    {
        var afResponse = await harness.Client.GetAsync("/api/session/antiforgery");
        afResponse.EnsureSuccessStatusCode();
        var afBody = await afResponse.Content.ReadFromJsonAsync<AntiforgeryBody>();
        return afBody!.RequestToken;
    }

    private static async Task<(Guid EventId, Guid ChiefLeaderId)> SetupPublishedEventWithChiefLeaderIdAsync(DraftApiTestHarness harness)
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

    private static async Task SeedAvailabilityAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        Guid userId,
        AvailabilityResponseType responseType)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var cycle = await db.StaffingCycles.SingleAsync(c => c.EventId == eventId && c.IsCurrent);

        var availability = AvailabilityResponse.Declare(
            cycle.Id,
            eventId,
            userId,
            responseType,
            DateTimeOffset.UtcNow.AddDays(10),
            DateTimeOffset.UtcNow,
            isChiefLeaderOnEvent: false,
            eventStatus: "Published");

        db.AvailabilityResponses.Add(availability);
        await db.SaveChangesAsync();
    }

    private sealed record AntiforgeryBody(string RequestToken);
    private sealed record DraftDetailsResponse(Guid Id, string Version);

    private sealed record DirectAssignmentCandidateItemBody(
        Guid UserId,
        string UserName,
        string AvailabilityStatus,
        DateTimeOffset? DeclaredAt);

    private sealed record DirectAssignmentCandidatesResponseBody(
        Guid EventId,
        Guid StaffingCycleId,
        bool IsWindowOpen,
        List<DirectAssignmentCandidateItemBody> Candidates);

    private sealed record TeamAssignmentResponseBody(
        Guid Id,
        Guid EventId,
        Guid StaffingCycleId,
        Guid UserId,
        string Status,
        DateTimeOffset AssignedAt,
        Guid AssignedByUserId,
        string Version);

    private sealed record ApiProblemBody(string Code, string Detail, int Status);
}
