using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Engagement;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class EngagementApiTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task RatingsAndIncidentsRespectEventScopeAndAdminReview()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var chiefId = await harness.SeedUserAsync("feedback.chief", true, CarpediemRoles.ChiefLeader);
        var otherChiefId = await harness.SeedUserAsync("feedback.other", true, CarpediemRoles.ChiefLeader);
        var leaderId = await harness.SeedUserAsync("feedback.leader", true, CarpediemRoles.Leader);
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(1);
        Guid eventId;
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var eventItem = Event.CreateDraft("Feedback event", start, start.AddHours(2), "Cairo", null,
                harness.ActorUserId, now.AddDays(-1), DateOnly.FromDateTime(start.Date));
            var chief = eventItem.AppointChiefLeader(chiefId, harness.ActorUserId, now.AddHours(-2));
            chief.AcceptDirect(chiefId, now.AddHours(-1), false);
            eventItem.Publish(harness.ActorUserId, now);
            db.Events.Add(eventItem);
            await db.SaveChangesAsync();
            eventId = eventItem.Id;
            var cycle = eventItem.StaffingCycles.Single(item => item.IsCurrent);
            db.TeamAssignments.Add(TeamAssignment.CreateDirectAssignment(cycle.Id, eventId,
                leaderId, chiefId, start, now, false, "Published"));
            eventItem.Complete(now);
            await db.SaveChangesAsync();
        }

        harness.Factory.ActorUserId = leaderId;
        harness.Factory.ActorRole = CarpediemRoles.Leader;
        var leaderToken = await TokenAsync(harness);
        var leaderRating = await SendAsync(harness, HttpMethod.Post, $"/api/events/{eventId}/feedback/event",
            new { rating = 4, comment = "Well organized" }, leaderToken);
        Assert.Equal(HttpStatusCode.Created, leaderRating.StatusCode);
        var repeated = await SendAsync(harness, HttpMethod.Post, $"/api/events/{eventId}/feedback/event",
            new { rating = 5, comment = "" }, leaderToken);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        var incident = await SendAsync(harness, HttpMethod.Post, $"/api/events/{eventId}/incidents",
            new { title = "Late transport", description = "Vehicle arrived after scheduled departure.", occurredAt = (DateTimeOffset?)null }, leaderToken);
        Assert.Equal(HttpStatusCode.Created, incident.StatusCode);
        var incidentId = (await incident.Content.ReadFromJsonAsync<IncidentBody>())!.Id;

        harness.Factory.ActorUserId = chiefId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var chiefToken = await TokenAsync(harness);
        var chiefRating = await SendAsync(harness, HttpMethod.Post,
            $"/api/events/{eventId}/feedback/leaders/{leaderId}", new { rating = 5, comment = "Reliable" }, chiefToken);
        Assert.Equal(HttpStatusCode.Created, chiefRating.StatusCode);
        var chiefIncidents = await harness.Client.GetFromJsonAsync<IncidentBody[]>($"/api/events/{eventId}/incidents");
        Assert.Equal(incidentId, Assert.Single(chiefIncidents!).Id);

        harness.Factory.ActorUserId = otherChiefId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        Assert.Equal(HttpStatusCode.Forbidden,
            (await harness.Client.GetAsync($"/api/events/{eventId}/incidents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await harness.Client.GetAsync($"/api/events/{eventId}/feedback/context")).StatusCode);

        harness.Factory.ActorUserId = harness.ActorUserId;
        harness.Factory.ActorRole = CarpediemRoles.Admin;
        var adminToken = await TokenAsync(harness);
        var feedback = await harness.Client.GetFromJsonAsync<PagedResult<FeedbackBody>>("/api/admin/feedback");
        Assert.Equal(2, feedback!.Items.Length);
        var adminIncidents = await harness.Client.GetFromJsonAsync<PagedResult<IncidentBody>>("/api/admin/incidents");
        Assert.Equal(incidentId, Assert.Single(adminIncidents!.Items).Id);
        var reviewed = await SendAsync(harness, HttpMethod.Post,
            $"/api/admin/incidents/{incidentId}/review", new { note = "Followed up with transport" }, adminToken);
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        await using var finalScope = harness.Factory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.True((await finalDb.IncidentReports.SingleAsync(item => item.Id == incidentId)).ReviewedAt.HasValue);
        Assert.True(await finalDb.AuditLogs.AnyAsync(item => item.ResourceId == incidentId && item.Action == "IncidentReviewed"));
    }

    private static async Task<string> TokenAsync(DraftApiTestHarness harness) =>
        (await harness.Client.GetFromJsonAsync<TokenBody>("/api/session/antiforgery"))!.RequestToken;

    private static async Task<HttpResponseMessage> SendAsync(DraftApiTestHarness harness,
        HttpMethod method, string path, object body, string token)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await harness.Client.SendAsync(request);
    }

    private sealed record TokenBody(string RequestToken);
    private sealed record IncidentBody(Guid Id);
    private sealed record FeedbackBody(Guid Id);
    private sealed record PagedResult<T>(T[] Items, int Page, int PageSize, bool HasMore);
}
