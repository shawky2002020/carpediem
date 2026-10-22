using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Events;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class EventCancellationTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task PublishedCancellationClosesRosterAndNotifiesAffectedPeople()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var chiefId = await harness.SeedUserAsync($"cancel.chief.{Guid.NewGuid():N}", true, CarpediemRoles.ChiefLeader);
        var leaderId = await harness.SeedUserAsync($"cancel.leader.{Guid.NewGuid():N}", true, CarpediemRoles.Leader);
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(7);
        Guid eventId;
        Guid assignmentId;
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var eventItem = Event.CreateDraft("Cancelled event", start, start.AddHours(2), "Cairo", null,
                harness.ActorUserId, now.AddDays(-1), DateOnly.FromDateTime(start.Date));
            var chief = eventItem.AppointChiefLeader(chiefId, harness.ActorUserId, now.AddHours(-2));
            chief.AcceptDirect(chiefId, now.AddHours(-1), false);
            eventItem.Publish(harness.ActorUserId, now);
            db.Events.Add(eventItem);
            await db.SaveChangesAsync();
            eventId = eventItem.Id;
            var cycle = eventItem.StaffingCycles.Single(c => c.IsCurrent);
            var assignment = TeamAssignment.CreateDirectAssignment(cycle.Id, eventId, leaderId,
                chiefId, eventItem.StartDateTime, now, false, "Published");
            assignmentId = assignment.Id;
            db.TeamAssignments.Add(assignment);
            await db.SaveChangesAsync();
        }

        var context = await harness.Client.GetFromJsonAsync<CancellationContext>($"/api/events/{eventId}/cancellation");
        Assert.NotNull(context);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/cancellation")
        { Content = JsonContent.Create(new { reason = "Venue is unavailable" }) };
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{context.Version}\"");
        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            Assert.Equal(EventStatus.Cancelled, (await db.Events.SingleAsync(e => e.Id == eventId)).Status);
            var assignment = await db.TeamAssignments.SingleAsync(a => a.Id == assignmentId);
            Assert.Equal(TeamAssignmentStatus.Cancelled, assignment.Status);
            Assert.Equal("Venue is unavailable", assignment.CancellationReason);
            Assert.Equal(2, await db.InPlatformNotifications.CountAsync(n =>
                n.EventId == eventId && n.NotificationType == "EventCancelled"));
            var audit = await db.AuditLogs.SingleAsync(a => a.ResourceId == eventId && a.Action == "EventCancelled");
            Assert.Equal("Venue is unavailable", audit.MandatoryReason);
        }

        var repeat = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/cancellation")
        { Content = JsonContent.Create(new { reason = "Venue is unavailable" }) };
        repeat.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);
        repeat.Headers.TryAddWithoutValidation("If-Match", $"\"{context.Version}\"");
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await harness.Client.SendAsync(repeat)).StatusCode);
    }

    [Fact]
    public async Task DraftCancellationHasNoLeaderNotificationsAndRequiresReason()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var context = await harness.Client.GetFromJsonAsync<CancellationContext>($"/api/events/{eventId}/cancellation");
        Assert.NotNull(context);
        using var invalid = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/cancellation")
        { Content = JsonContent.Create(new { reason = "short" }) };
        invalid.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);
        invalid.Headers.TryAddWithoutValidation("If-Match", $"\"{context.Version}\"");
        Assert.Equal(HttpStatusCode.BadRequest, (await harness.Client.SendAsync(invalid)).StatusCode);

        using var valid = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/cancellation")
        { Content = JsonContent.Create(new { reason = "Planning has stopped" }) };
        valid.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);
        valid.Headers.TryAddWithoutValidation("If-Match", $"\"{context.Version}\"");
        Assert.Equal(HttpStatusCode.OK, (await harness.Client.SendAsync(valid)).StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.False(await db.InPlatformNotifications.AnyAsync(n => n.EventId == eventId && n.NotificationType == "EventCancelled"));
    }

    private sealed record CancellationContext(string Version);
}
