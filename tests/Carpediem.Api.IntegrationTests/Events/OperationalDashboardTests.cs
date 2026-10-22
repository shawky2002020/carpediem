using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Attendance.Domain;
using Carpediem.Modules.Events.Application.Dashboard;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Events;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class OperationalDashboardTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task DashboardUsesCurrentCycleAndClearsPendingAttendanceAfterRollCall()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var chiefId = await harness.SeedUserAsync($"dashboard.chief.{Guid.NewGuid():N}", true, CarpediemRoles.ChiefLeader);
        var leaderId = await harness.SeedUserAsync($"dashboard.leader.{Guid.NewGuid():N}", true, CarpediemRoles.Leader);
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(7);
        Guid eventId;
        Guid currentAssignmentId;

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var eventItem = Event.CreateDraft("Dashboard event", start, start.AddHours(2), "Cairo", null,
                harness.ActorUserId, now.AddDays(-1), DateOnly.FromDateTime(start.Date));
            var chief = eventItem.AppointChiefLeader(chiefId, harness.ActorUserId, now.AddHours(-2));
            chief.AcceptDirect(chiefId, now.AddHours(-1), false);
            eventItem.Publish(harness.ActorUserId, now);
            db.Events.Add(eventItem);
            await db.SaveChangesAsync();
            eventId = eventItem.Id;

            var oldCycle = eventItem.StaffingCycles.Single(cycle => cycle.IsCurrent);
            db.TeamAssignments.Add(TeamAssignment.CreateDirectAssignment(oldCycle.Id, eventId, leaderId,
                chiefId, eventItem.StartDateTime, now, false, "Published"));
            await db.SaveChangesAsync();

            var newStart = now.AddDays(14);
            var newCycle = eventItem.Reschedule(newStart, newStart.AddHours(2), harness.ActorUserId, now);
            await db.SaveChangesAsync();
            var currentAssignment = TeamAssignment.CreateDirectAssignment(newCycle.Id, eventId, leaderId,
                chiefId, eventItem.StartDateTime, now, false, "Published");
            currentAssignmentId = currentAssignment.Id;
            db.TeamAssignments.Add(currentAssignment);
            await db.SaveChangesAsync();

            var store = scope.ServiceProvider.GetRequiredService<IOperationalDashboardStore>();
            var during = await store.GetAsync(harness.ActorUserId, newStart.AddHours(1), default);
            Assert.Single(during);
            Assert.True(during[0].InProgress);
            Assert.Equal(1, during[0].ActiveRosterCount);

            eventItem.Complete(now);
            await db.SaveChangesAsync();
        }

        var response = await harness.Client.GetAsync("/api/dashboard/events");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var rows = await response.Content.ReadFromJsonAsync<OperationalEvent[]>();
        Assert.Single(rows!);
        Assert.True(rows![0].PendingAttendance);
        Assert.False(rows[0].InProgress);
        Assert.Equal(1, rows[0].ActiveRosterCount);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            db.AttendanceOutcomes.Add(AttendanceOutcome.Create(
                currentAssignmentId, AttendanceOutcomeValue.Attended, chiefId, now));
            await db.SaveChangesAsync();
        }
        var refreshed = await harness.Client.GetFromJsonAsync<OperationalEvent[]>("/api/dashboard/events");
        Assert.False(Assert.Single(refreshed!).PendingAttendance);

        harness.Factory.ActorUserId = chiefId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var chiefRows = await harness.Client.GetFromJsonAsync<OperationalEvent[]>("/api/dashboard/events");
        Assert.Equal(eventId, Assert.Single(chiefRows!).EventId);

        harness.Factory.ActorUserId = leaderId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;
        Assert.Equal(HttpStatusCode.Forbidden,
            (await harness.Client.GetAsync("/api/dashboard/events")).StatusCode);
    }
}
