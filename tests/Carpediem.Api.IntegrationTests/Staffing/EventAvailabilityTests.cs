using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Domain.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Staffing;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class EventAvailabilityTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task LeaderCanDeclareAvailableOnPublishedEventReturns201CreatedAndPersistsRecordAndAudit()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var eventId = await SetupPublishedEventAsync(harness);

        var leaderUserId = await harness.SeedUserAsync("leader.alex", true, CarpediemRoles.Leader);
        harness.Factory.ActorUserId = leaderUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var myInitial = await harness.Client.GetFromJsonAsync<MyAvailabilityBody>(
            $"/api/events/{eventId}/availability/me");
        Assert.NotNull(myInitial);
        Assert.Null(myInitial.Response);
        Assert.True(myInitial.IsWindowOpen);
        Assert.False(myInitial.IsChiefLeaderOnEvent);
        Assert.Equal("Published", myInitial.EventStatus);

        var response = await PostAvailabilityAsync(harness, eventId, "Available");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AvailabilityResultBody>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.Equal(leaderUserId, body.UserId);
        Assert.Equal("Available", body.Response);
        Assert.NotEmpty(body.Version);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var stored = await db.AvailabilityResponses.SingleAsync(a => a.EventId == eventId && a.UserId == leaderUserId);
        Assert.Equal(AvailabilityResponseType.Available, stored.Response);

        var audit = await db.AuditLogs.SingleAsync(a => a.ResourceId == stored.Id && a.Action == "AvailabilityDeclared");
        Assert.Equal(leaderUserId, audit.ActorUserId);
        Assert.Null(audit.PreviousValue);
        Assert.Equal("Available", audit.NewValue);
    }

    [Fact]
    public async Task LeaderCanDeclareNotAvailableOnPublishedEventReturns201Created()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var eventId = await SetupPublishedEventAsync(harness);

        var leaderUserId = await harness.SeedUserAsync("leader.sam", true, CarpediemRoles.Leader);
        harness.Factory.ActorUserId = leaderUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var response = await PostAvailabilityAsync(harness, eventId, "NotAvailable");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AvailabilityResultBody>();
        Assert.NotNull(body);
        Assert.Equal("NotAvailable", body.Response);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var stored = await db.AvailabilityResponses.SingleAsync(a => a.EventId == eventId && a.UserId == leaderUserId);
        Assert.Equal(AvailabilityResponseType.NotAvailable, stored.Response);
    }

    [Fact]
    public async Task LeaderCanUpdateAvailabilityWithMatchingRowVersionReturns200AndUpdatesAudit()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var eventId = await SetupPublishedEventAsync(harness);

        var leaderUserId = await harness.SeedUserAsync("leader.taylor", true, CarpediemRoles.Leader);
        harness.Factory.ActorUserId = leaderUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var initialResponse = await PostAvailabilityAsync(harness, eventId, "Available");
        Assert.Equal(HttpStatusCode.Created, initialResponse.StatusCode);
        var initialBody = await initialResponse.Content.ReadFromJsonAsync<AvailabilityResultBody>();

        var updateResponse = await PostAvailabilityAsync(harness, eventId, "NotAvailable", ifMatch: initialBody!.Version);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updateBody = await updateResponse.Content.ReadFromJsonAsync<AvailabilityResultBody>();
        Assert.NotNull(updateBody);
        Assert.Equal("NotAvailable", updateBody.Response);
        Assert.NotEqual(initialBody.Version, updateBody.Version);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var stored = await db.AvailabilityResponses.SingleAsync(a => a.EventId == eventId && a.UserId == leaderUserId);
        Assert.Equal(AvailabilityResponseType.NotAvailable, stored.Response);

        var audits = await db.AuditLogs
            .Where(a => a.ResourceId == stored.Id && a.Action == "AvailabilityDeclared")
            .OrderBy(a => a.OccurredAt)
            .ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Equal("Available", audits[1].PreviousValue);
        Assert.Equal("NotAvailable", audits[1].NewValue);
    }

    [Fact]
    public async Task UpdateWithStaleRowVersionReturns409StaleVersion()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var eventId = await SetupPublishedEventAsync(harness);

        var leaderUserId = await harness.SeedUserAsync("leader.jordan", true, CarpediemRoles.Leader);
        harness.Factory.ActorUserId = leaderUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var initialResponse = await PostAvailabilityAsync(harness, eventId, "Available");
        Assert.Equal(HttpStatusCode.Created, initialResponse.StatusCode);

        var fakeStaleVersion = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);
        var staleResponse = await PostAvailabilityAsync(harness, eventId, "NotAvailable", ifMatch: fakeStaleVersion);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }

    [Fact]
    public async Task DeclarationWhenWindowClosedReturns409WindowClosed()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var eventId = await SetupPublishedEventAsync(harness);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var pastTime = DateTimeOffset.UtcNow.AddHours(-1);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events.Events SET StartDateTime = {pastTime} WHERE Id = {eventId}");
        }

        var leaderUserId = await harness.SeedUserAsync("leader.casey", true, CarpediemRoles.Leader);
        harness.Factory.ActorUserId = leaderUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var response = await PostAvailabilityAsync(harness, eventId, "Available");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeclarationOnNonPublishedEventReturns409InvalidEventStatus()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var draftEventId = await harness.CreateDefaultDraftAsync();

        var leaderUserId = await harness.SeedUserAsync("leader.riley", true, CarpediemRoles.Leader);
        harness.Factory.ActorUserId = leaderUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var response = await PostAvailabilityAsync(harness, draftEventId, "Available");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ActiveChiefLeaderCannotDeclareAvailabilityOnOwnEventReturns409ChiefLeaderConflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var response = await PostAvailabilityAsync(harness, eventId, "Available");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ActiveChiefLeaderCanDeclareAvailabilityOnOtherEventReturns201Created()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventAId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);
        var eventBId = await SetupPublishedEventAsync(harness);

        harness.Factory.ActorUserId = chiefLeaderId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var responseA = await PostAvailabilityAsync(harness, eventAId, "Available");
        Assert.Equal(HttpStatusCode.Conflict, responseA.StatusCode);

        var responseB = await PostAvailabilityAsync(harness, eventBId, "Available");
        Assert.Equal(HttpStatusCode.Created, responseB.StatusCode);
    }

    [Fact]
    public async Task IdempotentReplayIdenticalSubmissionReturns200WithIdempotentReplayHeaderAndNoDuplicateAudit()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var eventId = await SetupPublishedEventAsync(harness);

        var leaderUserId = await harness.SeedUserAsync("leader.morgan", true, CarpediemRoles.Leader);
        harness.Factory.ActorUserId = leaderUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var firstResponse = await PostAvailabilityAsync(harness, eventId, "Available");
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var replayResponse = await PostAvailabilityAsync(harness, eventId, "Available");
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.True(replayResponse.Headers.Contains("Idempotent-Replay"));
        Assert.Equal("true", replayResponse.Headers.GetValues("Idempotent-Replay").First());

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var audits = await db.AuditLogs
            .Where(a => a.ActorUserId == leaderUserId && a.Action == "AvailabilityDeclared")
            .ToListAsync();
        Assert.Single(audits);
    }

    [Fact]
    public async Task UnauthorizedUserWithoutLeaderRoleReturns403Forbidden()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var eventId = await SetupPublishedEventAsync(harness);

        var adminOnlyUserId = await harness.SeedUserAsync("admin.only", true, CarpediemRoles.Admin);
        harness.Factory.ActorUserId = adminOnlyUserId;
        harness.Factory.ActorRole = CarpediemRoles.Admin;

        var response = await PostAvailabilityAsync(harness, eventId, "Available");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPayloadReturns422ValidationFailed()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var eventId = await SetupPublishedEventAsync(harness);

        var leaderUserId = await harness.SeedUserAsync("leader.quinn", true, CarpediemRoles.Leader);
        harness.Factory.ActorUserId = leaderUserId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var response = await PostAvailabilityAsync(harness, eventId, "Tentative");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostAvailabilityAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        string availabilityResponse,
        string? ifMatch = null,
        string? token = null)
    {
        token ??= await TokenAsync(harness);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/availability")
        {
            Content = JsonContent.Create(new { response = availabilityResponse })
        };
        request.Headers.Add("X-XSRF-TOKEN", token);
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        }
        return await harness.Client.SendAsync(request);
    }

    private static async Task<string> TokenAsync(DraftApiTestHarness harness) =>
        (await harness.Client.GetFromJsonAsync<AntiforgeryBody>("/api/session/antiforgery"))!.RequestToken;

    private static async Task<Guid> SetupPublishedEventAsync(DraftApiTestHarness harness)
    {
        var (eventId, _) = await SetupPublishedEventWithChiefLeaderIdAsync(harness);
        return eventId;
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

        using var publishRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/events/drafts/{eventId}/publish");
        publishRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{draftJson!.Version}\"");
        publishRequest.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);
        var pubResult = await harness.Client.SendAsync(publishRequest);
        pubResult.EnsureSuccessStatusCode();

        return (eventId, candidateId);
    }

    private sealed record AntiforgeryBody(string RequestToken);
    private sealed record DraftDetailsResponse(Guid Id, string Version);
    private sealed record MyAvailabilityBody(
        Guid EventId,
        Guid StaffingCycleId,
        string? Response,
        DateTimeOffset? DeclaredAt,
        string? Version,
        bool IsWindowOpen,
        string EventStatus,
        bool IsChiefLeaderOnEvent);
    private sealed record AvailabilityResultBody(
        Guid Id,
        Guid EventId,
        Guid StaffingCycleId,
        Guid UserId,
        string Response,
        DateTimeOffset DeclaredAt,
        string Version);
}
