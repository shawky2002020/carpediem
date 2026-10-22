using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Audit;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Attendance.Domain;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Identity;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class LeaderHistoryTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task ProfileUsesCurrentCorrectionAndOnlyVolunteerCancellationAudit()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var leaderId = await harness.SeedUserAsync($"history.leader.{Guid.NewGuid():N}", true, CarpediemRoles.Leader);
        var chiefId = await harness.SeedUserAsync($"history.chief.{Guid.NewGuid():N}", true, CarpediemRoles.ChiefLeader);
        var eventId = await SeedPublishedEventAsync(harness, chiefId);
        var now = DateTimeOffset.UtcNow;

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var eventItem = await db.Events.Include(e => e.StaffingCycles).SingleAsync(e => e.Id == eventId);
            var first = eventItem.StaffingCycles.Single(c => c.IsCurrent);
            var volunteerCancellation = TeamAssignment.CreateDirectAssignment(
                first.Id, eventId, leaderId, chiefId, eventItem.StartDateTime, now, false, "Published");
            volunteerCancellation.Cancel("Unable to attend", now.AddMinutes(1), eventItem.StartDateTime);
            db.TeamAssignments.Add(volunteerCancellation);
            db.AuditLogs.Add(AuditLog.VolunteerSelfCancelled(
                volunteerCancellation.Id, leaderId, Guid.NewGuid(), now.AddMinutes(1), "Unable to attend", false));
            await db.SaveChangesAsync();

            var second = eventItem.Reschedule(now.AddDays(14), now.AddDays(14).AddHours(2), harness.ActorUserId, now.AddMinutes(2));
            await db.SaveChangesAsync();
            var adminCancellation = TeamAssignment.CreateDirectAssignment(
                second.Id, eventId, leaderId, chiefId, eventItem.StartDateTime, now.AddMinutes(3), false, "Published");
            adminCancellation.Cancel("Admin override", now.AddMinutes(4), eventItem.StartDateTime);
            db.TeamAssignments.Add(adminCancellation);
            db.AuditLogs.Add(AuditLog.VolunteerSelfCancelled(
                adminCancellation.Id, harness.ActorUserId, Guid.NewGuid(), now.AddMinutes(4), "Admin override", true));
            await db.SaveChangesAsync();

            var third = eventItem.Reschedule(now.AddDays(21), now.AddDays(21).AddHours(2), harness.ActorUserId, now.AddMinutes(5));
            await db.SaveChangesAsync();
            var attendedAssignment = TeamAssignment.CreateDirectAssignment(
                third.Id, eventId, leaderId, chiefId, eventItem.StartDateTime, now.AddMinutes(6), false, "Published");
            db.TeamAssignments.Add(attendedAssignment);
            db.AttendanceOutcomes.Add(AttendanceOutcome.Create(
                attendedAssignment.Id, AttendanceOutcomeValue.NoShow, chiefId, now.AddMinutes(7)));
            await db.SaveChangesAsync();
        }

        var before = await harness.Client.GetFromJsonAsync<LeaderHistoryBody>($"/api/leaders/{leaderId}/history");
        Assert.NotNull(before);
        Assert.Equal(0, before.EventsAttended);
        Assert.Equal(1, before.NoShows);
        Assert.Equal(1, before.SelfCancellations);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var outcome = await db.AttendanceOutcomes.SingleAsync();
            outcome.CorrectOutcome(AttendanceOutcomeValue.Attended, harness.ActorUserId, now.AddMinutes(8));
            await db.SaveChangesAsync();
        }

        var response = await harness.Client.GetAsync($"/api/leaders/{leaderId}/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var after = await response.Content.ReadFromJsonAsync<LeaderHistoryBody>();
        Assert.NotNull(after);
        Assert.Equal(1, after.EventsAttended);
        Assert.Equal(0, after.NoShows);
        Assert.Equal(1, after.SelfCancellations);
        Assert.Equal(3, after.Events.Length);
        Assert.Equal(2, after.Events.Count(e => !e.IsCurrentCycle && e.AttendanceOutcome is null));
        Assert.Equal(1, after.Events.Count(e => e.SelfCancelled));

        harness.Factory.ActorUserId = leaderId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;
        var own = await harness.Client.GetAsync("/api/profile/me");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal("no-store", own.Headers.CacheControl?.ToString());
        Assert.Equal(leaderId, (await own.Content.ReadFromJsonAsync<LeaderHistoryBody>())!.UserId);
    }

    [Fact]
    public async Task ChiefNeedsActiveEventAuthorityAndAdminCanInspectAnyLeader()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var leaderId = await harness.SeedUserAsync($"history.candidate.{Guid.NewGuid():N}", true, CarpediemRoles.Leader);
        var chiefId = await harness.SeedUserAsync($"history.chief.{Guid.NewGuid():N}", true, CarpediemRoles.ChiefLeader);
        var otherChiefId = await harness.SeedUserAsync($"history.otherchief.{Guid.NewGuid():N}", true, CarpediemRoles.ChiefLeader);
        var eventId = await SeedPublishedEventAsync(harness, chiefId);

        harness.Factory.ActorUserId = chiefId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        Assert.Equal(HttpStatusCode.OK,
            (await harness.Client.GetAsync($"/api/leaders/{leaderId}/history?eventId={eventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await harness.Client.GetAsync($"/api/leaders/{leaderId}/history")).StatusCode);

        // A volunteer can be deactivated after joining the active roster; the Chief
        // still needs to see that roster member's factual history.
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var eventItem = await db.Events.SingleAsync(e => e.Id == eventId);
            var cycle = await db.StaffingCycles.SingleAsync(c => c.EventId == eventId && c.IsCurrent);
            db.TeamAssignments.Add(TeamAssignment.CreateDirectAssignment(
                cycle.Id, eventId, leaderId, chiefId, eventItem.StartDateTime,
                DateTimeOffset.UtcNow, false, "Published"));
            (await db.Users.SingleAsync(u => u.Id == leaderId)).IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK,
            (await harness.Client.GetAsync($"/api/leaders/{leaderId}/history?eventId={eventId}")).StatusCode);

        harness.Factory.ActorUserId = otherChiefId;
        var foreign = await harness.Client.GetAsync($"/api/leaders/{leaderId}/history?eventId={eventId}");
        var absent = await harness.Client.GetAsync($"/api/leaders/{Guid.NewGuid()}/history?eventId={eventId}");
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal("no-store", foreign.Headers.CacheControl?.ToString());
        Assert.Equal(foreign.StatusCode, absent.StatusCode);

        harness.Factory.ActorUserId = leaderId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;
        Assert.Equal(HttpStatusCode.Forbidden,
            (await harness.Client.GetAsync($"/api/leaders/{otherChiefId}/history?eventId={eventId}")).StatusCode);

        harness.Factory.ActorUserId = harness.ActorUserId;
        harness.Factory.ActorRole = CarpediemRoles.Admin;
        Assert.Equal(HttpStatusCode.OK,
            (await harness.Client.GetAsync($"/api/leaders/{leaderId}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await harness.Client.GetAsync($"/api/leaders/{Guid.NewGuid()}/history")).StatusCode);
    }

    private static async Task<Guid> SeedPublishedEventAsync(DraftApiTestHarness harness, Guid chiefId)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(7);
        var eventItem = Event.CreateDraft(
            "History event", start, start.AddHours(2), "Cairo", null,
            harness.ActorUserId, now.AddDays(-1), DateOnly.FromDateTime(start.Date));
        var chief = eventItem.AppointChiefLeader(chiefId, harness.ActorUserId, now.AddHours(-2));
        chief.AcceptDirect(chiefId, now.AddHours(-1), false);
        eventItem.Publish(harness.ActorUserId, now);
        db.Events.Add(eventItem);
        await db.SaveChangesAsync();
        return eventItem.Id;
    }

    private sealed record LeaderHistoryBody(
        Guid UserId,
        string UserName,
        int EventsAttended,
        int NoShows,
        int SelfCancellations,
        LeaderHistoryEntryBody[] Events);
    private sealed record LeaderHistoryEntryBody(bool IsCurrentCycle, string? AttendanceOutcome, bool SelfCancelled);
}
