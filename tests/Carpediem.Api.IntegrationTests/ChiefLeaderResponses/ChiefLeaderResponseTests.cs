using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Audit;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Events.Application.ChiefLeaderResponses;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Notifications.Domain;
using Carpediem.Modules.Notifications.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Carpediem.Api.IntegrationTests.ChiefLeaderResponses;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class ChiefLeaderResponseTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task CandidateListsAndReadsOnlyTheirPendingAppointment()
    {
        await using var harness = await PreparedAsync();
        var (candidateId, assignmentId, _) = await AppointCandidateAsync(harness);
        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;

        var list = await harness.Client.GetFromJsonAsync<ResponseBody[]>(
            "/api/chief-leader/appointments");
        Assert.Equal(assignmentId, Assert.Single(list!).Id);
        var detail = await harness.Client.GetFromJsonAsync<ResponseBody>(
            $"/api/chief-leader/appointments/{assignmentId}");
        Assert.Equal("Pending", detail!.Status);
        Assert.Equal("Beach cleanup", detail.EventTitle);
        Assert.Equal("North shore", detail.Location);
        Assert.NotEmpty(detail.Version);
        Assert.Equal(HttpStatusCode.NotFound,
            (await harness.Client.GetAsync($"/api/chief-leader/appointments/{Guid.NewGuid()}")).StatusCode);

        harness.Factory.ActorUserId = await harness.SeedUserAsync("other.chief", true,
            CarpediemRoles.ChiefLeader);
        Assert.Equal(HttpStatusCode.NotFound,
            (await harness.Client.GetAsync($"/api/chief-leader/appointments/{assignmentId}")).StatusCode);
        Assert.Empty((await harness.Client.GetFromJsonAsync<ResponseBody[]>(
            "/api/chief-leader/appointments"))!);
    }

    [Fact]
    public async Task AcceptCommitsOneActiveAssignmentAndDirectAuditWithoutPublishing()
    {
        await using var harness = await PreparedAsync();
        var (candidateId, assignmentId, _) = await AppointCandidateAsync(harness);
        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var current = await GetAsync(harness, assignmentId);
        var token = await TokenAsync(harness);

        var response = await RespondAsync(harness, assignmentId, "accept", current.Version, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Active", (await response.Content.ReadFromJsonAsync<ResponseBody>())!.Status);
        Assert.Equal(HttpStatusCode.OK,
            (await RespondAsync(harness, assignmentId, "accept", current.Version, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await RespondAsync(harness, assignmentId, "decline", current.Version, token)).StatusCode);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var assignment = await db.ChiefLeaderAssignments.SingleAsync();
        Assert.Equal(ChiefLeaderAssignmentStatus.Active, assignment.Status);
        Assert.Equal(candidateId, assignment.AcceptedByUserId);
        Assert.Equal("Direct", assignment.AcceptanceSource);
        Assert.NotNull(assignment.AcceptedAt);
        Assert.Equal(EventStatus.Draft, (await db.Events.SingleAsync()).Status);
        var ownership = scope.ServiceProvider.GetRequiredService<IActiveChiefLeaderLookup>();
        Assert.True(await ownership.IsActiveChiefLeaderAsync(assignment.EventId, candidateId,
            CancellationToken.None));
        Assert.False(await ownership.IsActiveChiefLeaderAsync(assignment.EventId, Guid.NewGuid(),
            CancellationToken.None));
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.Action == "ChiefLeaderAccepted"));
        Assert.Equal(0, await db.InPlatformNotifications.CountAsync(
            item => item.NotificationType == InPlatformNotification.ChiefLeaderDeclinedType));
    }

    [Fact]
    public async Task DeclinePreservesHistoryAndAlertsActiveAdminOnce()
    {
        await using var harness = await PreparedAsync();
        var (candidateId, assignmentId, _) = await AppointCandidateAsync(harness);
        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var current = await GetAsync(harness, assignmentId);
        var token = await TokenAsync(harness);

        Assert.Equal(HttpStatusCode.OK,
            (await RespondAsync(harness, assignmentId, "decline", current.Version, token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await RespondAsync(harness, assignmentId, "decline", current.Version, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await RespondAsync(harness, assignmentId, "accept", current.Version, token)).StatusCode);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var assignment = await db.ChiefLeaderAssignments.SingleAsync();
        Assert.Equal(ChiefLeaderAssignmentStatus.Declined, assignment.Status);
        Assert.NotNull(assignment.DeclinedAt);
        Assert.Null(assignment.AcceptedAt);
        Assert.Equal(EventStatus.Draft, (await db.Events.SingleAsync()).Status);
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.Action == "ChiefLeaderDeclined"));
        var alert = await db.InPlatformNotifications.SingleAsync(item =>
            item.NotificationType == InPlatformNotification.ChiefLeaderDeclinedType);
        Assert.Equal(harness.ActorUserId, alert.RecipientUserId);
        Assert.Null(alert.ReadAt);
    }

    [Fact]
    public async Task RejectsStaleVersionWrongActorAndMissingAntiforgery()
    {
        await using var harness = await PreparedAsync();
        var (candidateId, assignmentId, _) = await AppointCandidateAsync(harness);
        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var current = await GetAsync(harness, assignmentId);
        var token = await TokenAsync(harness);

        Assert.Equal(HttpStatusCode.Conflict,
            (await RespondAsync(harness, assignmentId, "accept", Convert.ToBase64String(new byte[8]), token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await RespondAsync(harness, assignmentId, "accept", current.Version, null)).StatusCode);
        harness.Factory.ActorRole = CarpediemRoles.Leader;
        Assert.Equal(HttpStatusCode.Forbidden,
            (await RespondAsync(harness, assignmentId, "accept", current.Version, token)).StatusCode);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(ChiefLeaderAssignmentStatus.Pending,
            (await db.ChiefLeaderAssignments.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditLogs.CountAsync(item =>
            item.Action == "ChiefLeaderAccepted" || item.Action == "ChiefLeaderDeclined"));
    }

    [Fact]
    public async Task RevokedOrInactiveCandidateCannotReadOrRespond()
    {
        await using var harness = await PreparedAsync();
        var (candidateId, assignmentId, _) = await AppointCandidateAsync(harness);
        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var current = await GetAsync(harness, assignmentId);
        var token = await TokenAsync(harness);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == candidateId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden,
            (await harness.Client.GetAsync($"/api/chief-leader/appointments/{assignmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await RespondAsync(harness, assignmentId, "accept", current.Version, token)).StatusCode);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == candidateId);
            user.IsActive = true;
            db.UserRoles.RemoveRange(db.UserRoles.Where(item => item.UserId == candidateId));
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden,
            (await RespondAsync(harness, assignmentId, "decline", current.Version, token)).StatusCode);
    }

    [Fact]
    public async Task DeclineAlertFailureRollsBackAssignmentAndAudit()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer,
            configureServices: services =>
            {
                services.RemoveAll<IChiefLeaderDeclineNotificationWriter>();
                services.AddScoped<IChiefLeaderDeclineNotificationWriter, FailingDeclineNotificationWriter>();
            });
        var (candidateId, assignmentId, _) = await AppointCandidateAsync(harness);
        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var current = await GetAsync(harness, assignmentId);
        var token = await TokenAsync(harness);

        Assert.Equal(HttpStatusCode.InternalServerError,
            (await RespondAsync(harness, assignmentId, "decline", current.Version, token)).StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(ChiefLeaderAssignmentStatus.Pending,
            (await db.ChiefLeaderAssignments.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditLogs.CountAsync(item => item.Action == "ChiefLeaderDeclined"));
        Assert.Equal(0, await db.InPlatformNotifications.CountAsync(item =>
            item.NotificationType == InPlatformNotification.ChiefLeaderDeclinedType));
    }

    [Fact]
    public async Task RacingOppositeDecisionsCommitExactlyOneResult()
    {
        await using var harness = await PreparedAsync();
        var (candidateId, assignmentId, _) = await AppointCandidateAsync(harness);
        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var current = await GetAsync(harness, assignmentId);
        var token = await TokenAsync(harness);

        var outcomes = await Task.WhenAll(
            RespondAsync(harness, assignmentId, "accept", current.Version, token),
            RespondAsync(harness, assignmentId, "decline", current.Version, token));
        Assert.Contains(outcomes, item => item.StatusCode == HttpStatusCode.OK);
        Assert.Contains(outcomes, item => item.StatusCode == HttpStatusCode.Conflict);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var assignment = await db.ChiefLeaderAssignments.SingleAsync();
        Assert.True(assignment.Status is ChiefLeaderAssignmentStatus.Active or ChiefLeaderAssignmentStatus.Declined);
        Assert.Equal(1, await db.AuditLogs.CountAsync(item =>
            item.Action == "ChiefLeaderAccepted" || item.Action == "ChiefLeaderDeclined"));
        Assert.Equal(assignment.Status == ChiefLeaderAssignmentStatus.Declined ? 1 : 0,
            await db.InPlatformNotifications.CountAsync(item =>
                item.NotificationType == InPlatformNotification.ChiefLeaderDeclinedType));
    }

    [Fact]
    public async Task DeclineAlertsOnlyActiveAdmins()
    {
        await using var harness = await PreparedAsync();
        var secondAdmin = await harness.SeedUserAsync("second.admin", true, CarpediemRoles.Admin);
        await harness.SeedUserAsync("inactive.admin", false, CarpediemRoles.Admin);
        var (candidateId, assignmentId, _) = await AppointCandidateAsync(harness);
        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var current = await GetAsync(harness, assignmentId);
        var token = await TokenAsync(harness);

        Assert.Equal(HttpStatusCode.OK,
            (await RespondAsync(harness, assignmentId, "decline", current.Version, token)).StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var recipients = await db.InPlatformNotifications
            .Where(item => item.NotificationType == InPlatformNotification.ChiefLeaderDeclinedType)
            .Select(item => item.RecipientUserId).ToArrayAsync();
        Assert.Equal(2, recipients.Length);
        Assert.Contains(harness.ActorUserId, recipients);
        Assert.Contains(secondAdmin, recipients);
    }

    [Fact]
    public async Task ValidTitleWithEscapedCharactersDoesNotPreventAlertCommit()
    {
        await using var harness = await PreparedAsync();
        var response = await harness.CreateAsync(Guid.NewGuid(), new
        {
            title = new string('\u0001', 100),
            start = new { localDateTime = "2026-10-10T14:30" },
            end = new { localDateTime = "2026-10-10T16:30" },
            location = "North shore",
        });
        response.EnsureSuccessStatusCode();
        var eventId = (await response.Content.ReadFromJsonAsync<EventBody>())!.Id;
        var candidateId = await harness.SeedUserAsync("escaped.candidate", true,
            CarpediemRoles.ChiefLeader);
        var appointment = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());
        appointment.EnsureSuccessStatusCode();
        var assignmentId = (await appointment.Content.ReadFromJsonAsync<AppointmentBody>())!.Id;
        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var current = await GetAsync(harness, assignmentId);
        var token = await TokenAsync(harness);

        Assert.Equal(HttpStatusCode.OK,
            (await RespondAsync(harness, assignmentId, "decline", current.Version, token)).StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var alert = await db.InPlatformNotifications.SingleAsync(item =>
            item.NotificationType == InPlatformNotification.ChiefLeaderDeclinedType);
        Assert.True(alert.ContextPayload!.Length <= 500);
        Assert.Contains(eventId.ToString(), alert.ContextPayload);
    }

    [Fact]
    public async Task AdminCanReviewDeclinedHistoryBeforeAnotherAppointment()
    {
        await using var harness = await PreparedAsync();
        var (candidateId, assignmentId, eventId) = await AppointCandidateAsync(harness);
        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var current = await GetAsync(harness, assignmentId);
        var token = await TokenAsync(harness);
        Assert.Equal(HttpStatusCode.OK,
            (await RespondAsync(harness, assignmentId, "decline", current.Version, token)).StatusCode);

        harness.Factory.ActorUserId = harness.ActorUserId;
        harness.Factory.ActorRole = CarpediemRoles.Admin;
        var response = await harness.Client.GetAsync(
            $"/api/events/drafts/{eventId}/chief-leader-appointment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var context = await response.Content.ReadFromJsonAsync<AdminContextBody>();
        Assert.NotNull(context);
        Assert.Null(context.CurrentAppointment);
        Assert.Equal(assignmentId, Assert.Single(context.DeclinedHistory).Id);
        Assert.Equal("Declined", context.DeclinedHistory[0].Status);
        Assert.Contains(context.Candidates, item => item.UserId == candidateId);
    }

    private sealed class FailingDeclineNotificationWriter : IChiefLeaderDeclineNotificationWriter
    {
        public InPlatformNotification Stage(Guid recipientUserId, Guid eventId,
            string eventTitle, DateTimeOffset createdAt) =>
            throw new InvalidOperationException("Forced decline alert failure.");
    }

    private async Task<DraftApiTestHarness> PreparedAsync() =>
        await DraftApiTestHarness.StartAsync(sqlServer);

    private static async Task<(Guid CandidateId, Guid AssignmentId, Guid EventId)> AppointCandidateAsync(
        DraftApiTestHarness harness)
    {
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("candidate", true, CarpediemRoles.ChiefLeader);
        var response = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AppointmentBody>();
        return (candidateId, body!.Id, eventId);
    }

    private static async Task<ResponseBody> GetAsync(DraftApiTestHarness harness, Guid assignmentId) =>
        (await harness.Client.GetFromJsonAsync<ResponseBody>(
            $"/api/chief-leader/appointments/{assignmentId}"))!;

    private static async Task<string> TokenAsync(DraftApiTestHarness harness) =>
        (await harness.Client.GetFromJsonAsync<AntiforgeryBody>("/api/session/antiforgery"))!.RequestToken;

    private static async Task<HttpResponseMessage> RespondAsync(DraftApiTestHarness harness,
        Guid assignmentId, string action, string version, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/chief-leader/appointments/{assignmentId}/{action}");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        if (token is not null) request.Headers.Add("X-XSRF-TOKEN", token);
        return await harness.Client.SendAsync(request);
    }

    private sealed record AppointmentBody(Guid Id, string Status);
    private sealed record CandidateBody(Guid UserId);
    private sealed record AdminContextBody(AppointmentBody? CurrentAppointment,
        AppointmentBody[] DeclinedHistory, CandidateBody[] Candidates);
    private sealed record EventBody(Guid Id);
    private sealed record AntiforgeryBody(string RequestToken);
    private sealed record ResponseBody(Guid Id, Guid EventId, string EventTitle,
        string Location, string Status, string Version);
}
