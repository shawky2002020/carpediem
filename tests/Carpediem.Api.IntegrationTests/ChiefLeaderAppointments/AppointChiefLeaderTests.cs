using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Audit;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Events.Infrastructure.Persistence;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Carpediem.Modules.Notifications.Application;

namespace Carpediem.Api.IntegrationTests.ChiefLeaderAppointments;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class AppointChiefLeaderTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task AdminAppointmentCommitsPendingAuditNotificationAndRequest()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.candidate", true, CarpediemRoles.ChiefLeader);

        var response = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AppointmentBody>();
        Assert.NotNull(body);
        Assert.Equal(eventId, body.EventId);
        Assert.Equal(candidateId, body.Candidate.UserId);
        Assert.Equal("chief.candidate", body.Candidate.UserName);
        Assert.Equal("Pending", body.Status);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var assignment = await db.Set<ChiefLeaderAssignment>().SingleAsync();
        Assert.Equal(ChiefLeaderAssignmentStatus.Pending, assignment.Status);
        Assert.Equal(harness.ActorUserId, assignment.AppointedByUserId);
        Assert.Equal(EventStatus.Draft, (await db.Set<Event>().SingleAsync()).Status);
        var audit = await db.Set<AuditLog>().SingleAsync(item => item.Action == "ChiefLeaderAppointed");
        Assert.Equal("ChiefLeaderAssignment", audit.ResourceType);
        Assert.Equal(assignment.Id, audit.ResourceId);
        var notification = await db.Set<InPlatformNotification>().SingleAsync();
        Assert.Equal(InPlatformNotification.ChiefLeaderAppointmentType, notification.NotificationType);
        Assert.Equal(candidateId, notification.RecipientUserId);
        Assert.Null(notification.ReadAt);
        Assert.Equal(1, await db.Set<ChiefLeaderAppointmentRequest>().CountAsync());
    }

    [Fact]
    public async Task RequiredSideEffectFailureCommitsNoPartialAppointment()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer,
            configureServices: services =>
            {
                services.RemoveAll<IAppointmentNotificationWriter>();
                services.AddScoped<IAppointmentNotificationWriter, FailingNotificationWriter>();
            });
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.rollback", true, CarpediemRoles.ChiefLeader);

        var response = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(0, await db.ChiefLeaderAssignments.CountAsync());
        Assert.Equal(0, await db.InPlatformNotifications.CountAsync());
        Assert.Equal(0, await db.ChiefLeaderAppointmentRequests.CountAsync());
        Assert.Equal(0, await db.AuditLogs.CountAsync(item => item.Action == "ChiefLeaderAppointed"));
    }

    private sealed class FailingNotificationWriter : IAppointmentNotificationWriter
    {
        public InPlatformNotification StageChiefLeaderAppointment(Guid recipientUserId, Guid eventId,
            string eventTitle, DateTimeOffset createdAt) => throw new InvalidOperationException("Forced failure.");
    }

    private sealed record CandidateBody(Guid UserId, string UserName);
    private sealed record AppointmentBody(Guid Id, Guid EventId, CandidateBody Candidate, string Status);
}
