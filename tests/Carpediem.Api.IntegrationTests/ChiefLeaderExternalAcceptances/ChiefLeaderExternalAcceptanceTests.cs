using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Audit;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Application.ChiefLeaderResponses;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Carpediem.Api.IntegrationTests.ChiefLeaderExternalAcceptances;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class ChiefLeaderExternalAcceptanceTests(SqlServerFixture sqlServer)
{
    [Theory]
    [InlineData("x")]
    [InlineData(null)]
    public async Task ValidBoundaryReasonActivatesWithCompleteAudit(string? reason)
    {
        reason ??= new string('x', 1000);
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, candidateId, appointment) = await PendingAsync(harness);

        var response = await RecordAsync(harness, eventId, appointment.Id,
            appointment.Version, reason);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AppointmentBody>();
        Assert.Equal("Active", result!.Status);
        Assert.Equal("AdminExternal", result.AcceptanceSource);
        Assert.Equal(harness.ActorUserId, result.AcceptedByUserId);
        Assert.NotNull(result.AcceptedAt);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var stored = await db.ChiefLeaderAssignments.SingleAsync();
        Assert.Equal(ChiefLeaderAssignmentStatus.Active, stored.Status);
        Assert.Equal(harness.ActorUserId, stored.AcceptedByUserId);
        Assert.Equal("AdminExternal", stored.AcceptanceSource);
        Assert.Equal(EventStatus.Draft, (await db.Events.SingleAsync()).Status);
        var audit = await db.AuditLogs.SingleAsync(item =>
            item.Action == ChiefLeaderExternalAcceptanceAuditWriter.Action);
        Assert.Equal(reason, audit.MandatoryReason);
        Assert.Equal("AdminExternal", audit.Source);
        Assert.Equal(candidateId.ToString(), ExtractCandidate(audit.NewValue));
        var ownership = scope.ServiceProvider.GetRequiredService<IActiveChiefLeaderLookup>();
        Assert.True(await ownership.IsActiveChiefLeaderAsync(eventId, candidateId,
            CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankReasonIsRejectedWithoutMutation(string reason)
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, _, appointment) = await PendingAsync(harness);

        var response = await RecordAsync(harness, eventId, appointment.Id,
            appointment.Version, reason);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await AssertPendingWithoutAuditAsync(harness);
    }

    [Fact]
    public async Task OverLimitReasonIsRejectedWithoutTruncation()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, _, appointment) = await PendingAsync(harness);

        var response = await RecordAsync(harness, eventId, appointment.Id,
            appointment.Version, new string('x', 1001));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await AssertPendingWithoutAuditAsync(harness);
    }

    [Fact]
    public async Task SameEvidenceReplaysButDifferentEvidenceConflicts()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, _, appointment) = await PendingAsync(harness);

        Assert.Equal(HttpStatusCode.OK,
            (await RecordAsync(harness, eventId, appointment.Id, appointment.Version,
                "Confirmed by phone")).StatusCode);
        var replay = await RecordAsync(harness, eventId, appointment.Id, appointment.Version,
            "  Confirmed by phone  ");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotent-Replay").Single());
        Assert.Equal(HttpStatusCode.Conflict,
            (await RecordAsync(harness, eventId, appointment.Id, appointment.Version,
                "Confirmed by chat")).StatusCode);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(1, await db.AuditLogs.CountAsync(item =>
            item.Action == ChiefLeaderExternalAcceptanceAuditWriter.Action));
    }

    [Theory]
    [InlineData("accept", "Active", "Direct")]
    [InlineData("decline", "Declined", null)]
    public async Task CandidateDirectResponseWinsOverStaleAdminSubmission(
        string action, string expectedStatus, string? expectedSource)
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, candidateId, appointment) = await PendingAsync(harness);
        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var candidateToken = (await harness.Client.GetFromJsonAsync<AntiforgeryBody>(
            "/api/session/antiforgery"))!.RequestToken;
        using var direct = new HttpRequestMessage(HttpMethod.Post,
            $"/api/chief-leader/appointments/{appointment.Id}/{action}");
        direct.Headers.TryAddWithoutValidation("If-Match", $"\"{appointment.Version}\"");
        direct.Headers.Add("X-XSRF-TOKEN", candidateToken);
        Assert.Equal(HttpStatusCode.OK, (await harness.Client.SendAsync(direct)).StatusCode);

        harness.Factory.ActorUserId = harness.ActorUserId;
        harness.Factory.ActorRole = CarpediemRoles.Admin;
        Assert.Equal(HttpStatusCode.Conflict,
            (await RecordAsync(harness, eventId, appointment.Id, appointment.Version,
                "Confirmed externally")).StatusCode);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var stored = await db.ChiefLeaderAssignments.SingleAsync();
        Assert.Equal(expectedStatus, stored.Status.ToString());
        Assert.Equal(expectedSource, stored.AcceptanceSource);
        Assert.Equal(0, await db.AuditLogs.CountAsync(item =>
            item.Action == ChiefLeaderExternalAcceptanceAuditWriter.Action));
    }

    [Fact]
    public async Task AdminExternalAcceptanceWinsOverStaleCandidateResponse()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, candidateId, appointment) = await PendingAsync(harness);
        Assert.Equal(HttpStatusCode.OK,
            (await RecordAsync(harness, eventId, appointment.Id, appointment.Version,
                "Confirmed externally")).StatusCode);

        harness.Factory.ActorUserId = candidateId;
        harness.Factory.ActorRole = CarpediemRoles.ChiefLeader;
        var candidateToken = (await harness.Client.GetFromJsonAsync<AntiforgeryBody>(
            "/api/session/antiforgery"))!.RequestToken;
        using var direct = new HttpRequestMessage(HttpMethod.Post,
            $"/api/chief-leader/appointments/{appointment.Id}/accept");
        direct.Headers.TryAddWithoutValidation("If-Match", $"\"{appointment.Version}\"");
        direct.Headers.Add("X-XSRF-TOKEN", candidateToken);

        Assert.Equal(HttpStatusCode.Conflict, (await harness.Client.SendAsync(direct)).StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var stored = await db.ChiefLeaderAssignments.SingleAsync();
        Assert.Equal(ChiefLeaderAssignmentStatus.Active, stored.Status);
        Assert.Equal("AdminExternal", stored.AcceptanceSource);
        Assert.Equal(1, await db.AuditLogs.CountAsync(item =>
            item.Action == ChiefLeaderExternalAcceptanceAuditWriter.Action));
    }

    [Fact]
    public async Task RejectsUnauthorizedInactiveUnknownAndMalformedRequests()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var (eventId, _, appointment) = await PendingAsync(harness);

        harness.Factory.ActorRole = CarpediemRoles.Leader;
        Assert.Equal(HttpStatusCode.Forbidden,
            (await RecordAsync(harness, eventId, appointment.Id, appointment.Version,
                "Confirmed externally")).StatusCode);
        harness.Factory.ActorRole = CarpediemRoles.Admin;
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            (await db.Users.SingleAsync(item => item.Id == harness.ActorUserId)).IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden,
            (await RecordAsync(harness, eventId, appointment.Id, appointment.Version,
                "Confirmed externally")).StatusCode);
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            (await db.Users.SingleAsync(item => item.Id == harness.ActorUserId)).IsActive = true;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound,
            (await RecordAsync(harness, Guid.NewGuid(), Guid.NewGuid(), appointment.Version,
                "Confirmed externally")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await RecordAsync(harness, eventId, appointment.Id, "bad-version",
                "Confirmed externally")).StatusCode);
        await AssertPendingWithoutAuditAsync(harness);
    }

    [Fact]
    public async Task AuditFailureRollsBackActivation()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer,
            configureServices: services =>
            {
                services.RemoveAll<IChiefLeaderExternalAcceptanceAuditWriter>();
                services.AddScoped<IChiefLeaderExternalAcceptanceAuditWriter, FailingAuditWriter>();
            });
        var (eventId, _, appointment) = await PendingAsync(harness);

        Assert.Equal(HttpStatusCode.InternalServerError,
            (await RecordAsync(harness, eventId, appointment.Id, appointment.Version,
                "Confirmed externally")).StatusCode);
        await AssertPendingWithoutAuditAsync(harness);
    }

    private static async Task<(Guid EventId, Guid CandidateId, AppointmentBody Appointment)> PendingAsync(
        DraftApiTestHarness harness)
    {
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("external.candidate", true,
            CarpediemRoles.ChiefLeader);
        var response = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());
        response.EnsureSuccessStatusCode();
        return (eventId, candidateId,
            (await response.Content.ReadFromJsonAsync<AppointmentBody>())!);
    }

    private static async Task<HttpResponseMessage> RecordAsync(DraftApiTestHarness harness,
        Guid eventId, Guid assignmentId, string version, string justification)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/events/drafts/{eventId}/chief-leader-appointment/{assignmentId}/external-acceptance")
        {
            Content = JsonContent.Create(new { justification }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);
        return await harness.Client.SendAsync(request);
    }

    private static async Task AssertPendingWithoutAuditAsync(DraftApiTestHarness harness)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(ChiefLeaderAssignmentStatus.Pending,
            (await db.ChiefLeaderAssignments.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditLogs.CountAsync(item =>
            item.Action == ChiefLeaderExternalAcceptanceAuditWriter.Action));
    }

    private static string ExtractCandidate(string? value)
    {
        using var json = System.Text.Json.JsonDocument.Parse(value!);
        return json.RootElement.GetProperty("candidateUserId").GetString()!;
    }

    private sealed class FailingAuditWriter : IChiefLeaderExternalAcceptanceAuditWriter
    {
        public AuditLog Create(ChiefLeaderAssignment assignment, Guid actorUserId,
            string justification, Guid correlationId, DateTimeOffset occurredAt) =>
            throw new InvalidOperationException("Forced audit failure.");
    }

    private sealed record AppointmentBody(Guid Id, string Status, string Version,
        DateTimeOffset? AcceptedAt, string? AcceptanceSource, Guid? AcceptedByUserId);
    private sealed record AntiforgeryBody(string RequestToken);
}
