using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.ChiefLeaderAppointments;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class ReviewChiefLeaderAppointmentTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task ReopeningShowsTheCurrentPendingCandidate()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.review", true, CarpediemRoles.ChiefLeader);
        Assert.Equal(HttpStatusCode.Created,
            (await harness.AppointAsync(eventId, candidateId, Guid.NewGuid())).StatusCode);

        var context = await harness.Client.GetFromJsonAsync<ContextBody>(
            $"/api/events/drafts/{eventId}/chief-leader-appointment");

        Assert.NotNull(context?.CurrentAppointment);
        Assert.Equal(candidateId, context.CurrentAppointment.Candidate.UserId);
        Assert.Equal("Pending", context.CurrentAppointment.Status);
        Assert.Empty(context.Candidates);
    }

    [Fact]
    public async Task ASecondAppointmentLeavesTheCurrentPendingAssignmentUnchanged()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var firstCandidate = await harness.SeedUserAsync("chief.current", true, CarpediemRoles.ChiefLeader);
        var secondCandidate = await harness.SeedUserAsync("chief.competing", true, CarpediemRoles.ChiefLeader);
        Assert.Equal(HttpStatusCode.Created,
            (await harness.AppointAsync(eventId, firstCandidate, Guid.NewGuid())).StatusCode);

        var response = await harness.AppointAsync(eventId, secondCandidate, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var assignment = await db.ChiefLeaderAssignments.SingleAsync();
        Assert.Equal(firstCandidate, assignment.ChiefLeaderUserId);
        Assert.Equal(ChiefLeaderAssignmentStatus.Pending, assignment.Status);
    }

    [Fact]
    public async Task DeclinedHistoryIsPreservedWhenANewPendingAppointmentIsCreated()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var declinedCandidate = await harness.SeedUserAsync("chief.declined", true, CarpediemRoles.ChiefLeader);
        var nextCandidate = await harness.SeedUserAsync("chief.next", true, CarpediemRoles.ChiefLeader);
        Assert.Equal(HttpStatusCode.Created,
            (await harness.AppointAsync(eventId, declinedCandidate, Guid.NewGuid())).StatusCode);

        Guid declinedAssignmentId;
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            declinedAssignmentId = await db.ChiefLeaderAssignments.Select(item => item.Id).SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [events].[ChiefLeaderAssignments] SET [Status] = 'Declined', [DeclinedAt] = {DateTimeOffset.UtcNow} WHERE [Id] = {declinedAssignmentId}");
        }

        var response = await harness.AppointAsync(eventId, nextCandidate, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var verificationScope = harness.Factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var assignments = await verificationDb.ChiefLeaderAssignments.ToArrayAsync();
        Assert.Equal(2, assignments.Length);
        var declined = Assert.Single(assignments, item => item.Status == ChiefLeaderAssignmentStatus.Declined);
        var pending = Assert.Single(assignments, item => item.Status == ChiefLeaderAssignmentStatus.Pending);
        Assert.Equal(declinedAssignmentId, declined.Id);
        Assert.Equal(declinedCandidate, declined.ChiefLeaderUserId);
        Assert.Equal(nextCandidate, pending.ChiefLeaderUserId);
    }

    private sealed record CandidateBody(Guid UserId, string UserName);
    private sealed record AppointmentBody(CandidateBody Candidate, string Status);
    private sealed record ContextBody(AppointmentBody? CurrentAppointment, CandidateBody[] Candidates);
}
