using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Attendance.Application;
using Carpediem.Modules.Attendance.Domain;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Carpediem.Web.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Attendance;

#pragma warning disable CA1707 // Identifiers should not contain underscores

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class AttendanceCorrectionTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task CorrectAttendance_WhenAdminWithValidReason_CorrectsPriorOutcomeAndWritesAuditLog()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.alice", true, CarpediemRoles.Leader);
        var assignmentId = await SeedTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        var outcomeId = await SeedAttendanceOutcomeAsync(harness, assignmentId, AttendanceOutcomeValue.NoShow, chiefLeaderId);

        var afToken = await GetFreshAntiforgeryTokenAsync(harness);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/attendance/{outcomeId}/correct");
        request.Headers.Add("X-XSRF-TOKEN", afToken);
        request.Content = JsonContent.Create(new AttendanceEndpoints.CorrectAttendanceRequest("Attended", "Excused late arrival due to weather"));

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AttendanceEndpoints.CorrectAttendanceResponse>();
        Assert.NotNull(body);
        Assert.Equal(outcomeId, body.AttendanceOutcomeId);
        Assert.Equal(assignmentId, body.TeamAssignmentId);
        Assert.Equal("NoShow", body.PreviousOutcome);
        Assert.Equal("Attended", body.NewOutcome);

        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var outcome = await db.AttendanceOutcomes.SingleAsync(o => o.Id == outcomeId);
            Assert.Equal(AttendanceOutcomeValue.Attended, outcome.Outcome);
            Assert.Equal(harness.ActorUserId, outcome.CorrectedByUserId);

            var auditCount = await db.AuditLogs.CountAsync(a => a.ResourceId == outcomeId && a.Action == "AttendanceCorrected");
            Assert.Equal(1, auditCount);
        }
    }

    [Fact]
    public async Task CorrectAttendance_WhenMissingReason_ReturnsBadRequest()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.bob", true, CarpediemRoles.Leader);
        var assignmentId = await SeedTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        var outcomeId = await SeedAttendanceOutcomeAsync(harness, assignmentId, AttendanceOutcomeValue.NoShow, chiefLeaderId);

        var afToken = await GetFreshAntiforgeryTokenAsync(harness);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/attendance/{outcomeId}/correct");
        request.Headers.Add("X-XSRF-TOKEN", afToken);
        request.Content = JsonContent.Create(new AttendanceEndpoints.CorrectAttendanceRequest("Attended", null!));

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CorrectAttendance_WhenEmptyReason_ReturnsBadRequest()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.charlie", true, CarpediemRoles.Leader);
        var assignmentId = await SeedTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        var outcomeId = await SeedAttendanceOutcomeAsync(harness, assignmentId, AttendanceOutcomeValue.NoShow, chiefLeaderId);

        var afToken = await GetFreshAntiforgeryTokenAsync(harness);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/attendance/{outcomeId}/correct");
        request.Headers.Add("X-XSRF-TOKEN", afToken);
        request.Content = JsonContent.Create(new AttendanceEndpoints.CorrectAttendanceRequest("Attended", "   "));

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CorrectAttendance_WhenAttendanceRecordNotFound_ReturnsNotFound()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var afToken = await GetFreshAntiforgeryTokenAsync(harness);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/attendance/{Guid.NewGuid()}/correct");
        request.Headers.Add("X-XSRF-TOKEN", afToken);
        request.Content = JsonContent.Create(new AttendanceEndpoints.CorrectAttendanceRequest("Attended", "Valid reason"));

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CorrectAttendance_WhenNotAdmin_ReturnsForbidden()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.ChiefLeader);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.dave", true, CarpediemRoles.Leader);
        var assignmentId = await SeedTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        var outcomeId = await SeedAttendanceOutcomeAsync(harness, assignmentId, AttendanceOutcomeValue.NoShow, chiefLeaderId);

        var afToken = await GetFreshAntiforgeryTokenAsync(harness);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/attendance/{outcomeId}/correct");
        request.Headers.Add("X-XSRF-TOKEN", afToken);
        request.Content = JsonContent.Create(new AttendanceEndpoints.CorrectAttendanceRequest("Attended", "Valid reason"));

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CorrectAttendance_WhenConcurrencyConflict_ReturnsConflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.eve", true, CarpediemRoles.Leader);
        var assignmentId = await SeedTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        var outcomeId = await SeedAttendanceOutcomeAsync(harness, assignmentId, AttendanceOutcomeValue.NoShow, chiefLeaderId);

        var afToken = await GetFreshAntiforgeryTokenAsync(harness);

        // Invalid base64 will be ignored, but if we provide an old rowversion, it'll conflict
        byte[] oldRowVersion;
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var outcome = await db.AttendanceOutcomes.SingleAsync(o => o.Id == outcomeId);
            oldRowVersion = outcome.RowVersion;
            
            // Advance the rowversion out-of-band
            outcome.UpdateOutcome(AttendanceOutcomeValue.Attended, chiefLeaderId, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/attendance/{outcomeId}/correct");
        request.Headers.Add("X-XSRF-TOKEN", afToken);
        request.Headers.Add("If-Match", $"\"{Convert.ToBase64String(oldRowVersion)}\"");
        request.Content = JsonContent.Create(new AttendanceEndpoints.CorrectAttendanceRequest("Attended", "Valid reason"));

        var response = await harness.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CorrectAttendance_WhenReplayedCorrection_UpdatesToLatestOutcome()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, role: CarpediemRoles.Admin);
        var (eventId, chiefLeaderId) = await SetupPublishedEventWithChiefLeaderAsync(harness);

        var volunteerUserId = await harness.SeedUserAsync("volunteer.frank", true, CarpediemRoles.Leader);
        var assignmentId = await SeedTeamAssignmentAsync(harness, eventId, volunteerUserId, chiefLeaderId);

        var outcomeId = await SeedAttendanceOutcomeAsync(harness, assignmentId, AttendanceOutcomeValue.NoShow, chiefLeaderId);

        var afToken = await GetFreshAntiforgeryTokenAsync(harness);

        // First correction
        using (var request1 = new HttpRequestMessage(HttpMethod.Post, $"/api/attendance/{outcomeId}/correct"))
        {
            request1.Headers.Add("X-XSRF-TOKEN", afToken);
            request1.Content = JsonContent.Create(new AttendanceEndpoints.CorrectAttendanceRequest("Attended", "Excused late arrival"));
            var res1 = await harness.Client.SendAsync(request1);
            Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        }

        // Second correction (e.g. changing back, or idempotent)
        using (var request2 = new HttpRequestMessage(HttpMethod.Post, $"/api/attendance/{outcomeId}/correct"))
        {
            request2.Headers.Add("X-XSRF-TOKEN", afToken);
            request2.Content = JsonContent.Create(new AttendanceEndpoints.CorrectAttendanceRequest("Attended", "Just confirming"));
            var res2 = await harness.Client.SendAsync(request2);
            Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
            var body = await res2.Content.ReadFromJsonAsync<AttendanceEndpoints.CorrectAttendanceResponse>();
            Assert.Equal("Attended", body!.NewOutcome);
        }
    }

    // --- Helpers ---

    private static async Task<(Guid EventId, Guid ChiefLeaderId)> SetupPublishedEventWithChiefLeaderAsync(
        DraftApiTestHarness harness)
    {
        var chiefLeaderId = await harness.SeedUserAsync(
            $"chief.leader.{Guid.NewGuid():N}",
            isActive: true,
            CarpediemRoles.ChiefLeader);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var start = DateTimeOffset.UtcNow.AddDays(7);
        var end = start.AddHours(4);
        var effectiveDate = DateOnly.FromDateTime(start.Date);

        var draft = Event.CreateDraft(
            "Community Gala",
            start,
            end,
            "Grand Ballroom",
            "Notes",
            harness.ActorUserId,
            DateTimeOffset.UtcNow.AddDays(-2),
            effectiveDate);

        var assignment = draft.AppointChiefLeader(chiefLeaderId, harness.ActorUserId, DateTimeOffset.UtcNow.AddDays(-2));
        assignment.AcceptDirect(chiefLeaderId, DateTimeOffset.UtcNow.AddDays(-1), hasActiveTeamAssignmentOnCurrentCycle: false);
        draft.Publish(harness.ActorUserId, DateTimeOffset.UtcNow.AddHours(-1));

        db.Events.Add(draft);
        await db.SaveChangesAsync();

        return (draft.Id, chiefLeaderId);
    }

    private static async Task<Guid> SeedTeamAssignmentAsync(
        DraftApiTestHarness harness,
        Guid eventId,
        Guid volunteerUserId,
        Guid chiefLeaderId)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var currentCycle = await db.StaffingCycles.SingleAsync(c => c.EventId == eventId && c.IsCurrent);

        var assignment = TeamAssignment.Create(
            currentCycle.Id,
            eventId,
            volunteerUserId,
            chiefLeaderId,
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow,
            "Available",
            false,
            "Published");

        db.TeamAssignments.Add(assignment);
        await db.SaveChangesAsync();

        return assignment.Id;
    }

    private static async Task<Guid> SeedAttendanceOutcomeAsync(
        DraftApiTestHarness harness,
        Guid teamAssignmentId,
        AttendanceOutcomeValue outcome,
        Guid chiefLeaderId)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();

        var o = AttendanceOutcome.Create(teamAssignmentId, outcome, chiefLeaderId, DateTimeOffset.UtcNow);
        db.AttendanceOutcomes.Add(o);
        await db.SaveChangesAsync();

        return o.Id;
    }

    private static async Task<string> GetFreshAntiforgeryTokenAsync(DraftApiTestHarness harness)
    {
        var afResponse = await harness.Client.GetAsync("/api/session/antiforgery");
        afResponse.EnsureSuccessStatusCode();
        var afBody = await afResponse.Content.ReadFromJsonAsync<AntiforgeryBody>();
        return afBody!.RequestToken;
    }

    private sealed record AntiforgeryBody(string RequestToken);
}
