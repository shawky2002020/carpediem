using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.ChiefLeaderAppointments;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class ChiefLeaderAppointmentIdempotencyTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task IdenticalRequestReplaysOneBusinessResult()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.replay", true, CarpediemRoles.ChiefLeader);
        var key = Guid.NewGuid();

        var first = await harness.AppointAsync(eventId, candidateId, key);
        var second = await harness.AppointAsync(eventId, candidateId, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("true", second.Headers.GetValues("Idempotent-Replay").Single());
        var firstBody = await first.Content.ReadFromJsonAsync<AppointmentBody>();
        var secondBody = await second.Content.ReadFromJsonAsync<AppointmentBody>();
        Assert.Equal(firstBody!.Id, secondBody!.Id);
        await AssertSingleResultAsync(harness);
    }

    [Fact]
    public async Task SameTokenWithDifferentCandidateConflicts()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var firstCandidate = await harness.SeedUserAsync("chief.first", true, CarpediemRoles.ChiefLeader);
        var secondCandidate = await harness.SeedUserAsync("chief.second", true, CarpediemRoles.ChiefLeader);
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Created, (await harness.AppointAsync(eventId, firstCandidate, key)).StatusCode);

        var response = await harness.AppointAsync(eventId, secondCandidate, key);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertSingleResultAsync(harness);
    }

    [Fact]
    public async Task SecondAppointmentWithAnotherKeyIsRejectedAndReviewShowsPending()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var firstCandidate = await harness.SeedUserAsync("chief.pending", true, CarpediemRoles.ChiefLeader);
        var secondCandidate = await harness.SeedUserAsync("chief.other", true, CarpediemRoles.ChiefLeader);
        await harness.AppointAsync(eventId, firstCandidate, Guid.NewGuid());

        var response = await harness.AppointAsync(eventId, secondCandidate, Guid.NewGuid());
        var context = await harness.Client.GetFromJsonAsync<ContextBody>(
            $"/api/events/drafts/{eventId}/chief-leader-appointment");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Pending", context!.CurrentAppointment.Status);
        Assert.Equal(firstCandidate, context.CurrentAppointment.Candidate.UserId);
        Assert.Empty(context.Candidates);
        await AssertSingleResultAsync(harness);
    }

    [Fact]
    public async Task ConcurrentSameIntentCreatesOneAppointment()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.concurrent", true, CarpediemRoles.ChiefLeader);
        var key = Guid.NewGuid();

        var responses = await Task.WhenAll(
            harness.AppointAsync(eventId, candidateId, key),
            harness.AppointAsync(eventId, candidateId, key));

        Assert.Contains(responses, item => item.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, item => Assert.Contains(item.StatusCode,
            new[] { HttpStatusCode.Created, HttpStatusCode.OK }));
        await AssertSingleResultAsync(harness);
    }

    [Fact]
    public async Task ConcurrentDifferentCandidatesProduceOneWinnerAndOneConflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var first = await harness.SeedUserAsync("chief.concurrent.one", true, CarpediemRoles.ChiefLeader);
        var second = await harness.SeedUserAsync("chief.concurrent.two", true, CarpediemRoles.ChiefLeader);

        var responses = await Task.WhenAll(
            harness.AppointAsync(eventId, first, Guid.NewGuid()),
            harness.AppointAsync(eventId, second, Guid.NewGuid()));

        Assert.Contains(responses, item => item.StatusCode == HttpStatusCode.Created);
        Assert.Contains(responses, item => item.StatusCode == HttpStatusCode.Conflict);
        await AssertSingleResultAsync(harness);
    }

    private static async Task AssertSingleResultAsync(DraftApiTestHarness harness)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(1, await db.Set<ChiefLeaderAssignment>().CountAsync());
        Assert.Equal(1, await db.Set<InPlatformNotification>().CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.Action == "ChiefLeaderAppointed"));
        Assert.Equal(1, await db.ChiefLeaderAppointmentRequests.CountAsync());
    }

    private sealed record CandidateBody(Guid UserId, string UserName);
    private sealed record AppointmentBody(Guid Id, CandidateBody Candidate, string Status);
    private sealed record ContextBody(AppointmentBody CurrentAppointment, CandidateBody[] Candidates);
}
