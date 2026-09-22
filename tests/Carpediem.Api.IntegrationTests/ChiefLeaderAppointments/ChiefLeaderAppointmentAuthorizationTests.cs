using System.Net;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.ChiefLeaderAppointments;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class ChiefLeaderAppointmentAuthorizationTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task AnonymousActorCannotReadCandidatesOrAppoint()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.anonymous", true, CarpediemRoles.ChiefLeader);
        harness.Factory.ActorUserId = null;

        var read = await harness.Client.GetAsync($"/api/events/drafts/{eventId}/chief-leader-appointment");
        var write = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
    }

    [Fact]
    public async Task InactiveAdminCannotReadCandidatesOrAppoint()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.inactive.admin", true, CarpediemRoles.ChiefLeader);
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            (await db.Users.SingleAsync(item => item.Id == harness.ActorUserId)).IsActive = false;
            await db.SaveChangesAsync();
        }

        var read = await harness.Client.GetAsync($"/api/events/drafts/{eventId}/chief-leader-appointment");
        var write = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        await using var verificationScope = harness.Factory.Services.CreateAsyncScope();
        Assert.Equal(0, await verificationScope.ServiceProvider.GetRequiredService<CarpediemDbContext>()
            .ChiefLeaderAssignments.CountAsync());
    }

    [Theory]
    [InlineData(CarpediemRoles.ChiefLeader)]
    [InlineData(CarpediemRoles.Leader)]
    public async Task NonAdminCannotReadCandidatesOrAppoint(string role)
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.denied", true, CarpediemRoles.ChiefLeader);
        harness.Factory.ActorRole = role;

        var read = await harness.Client.GetAsync($"/api/events/drafts/{eventId}/chief-leader-appointment");
        var write = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<CarpediemDbContext>()
            .Set<ChiefLeaderAssignment>().CountAsync());
    }

    [Fact]
    public async Task MissingAntiforgeryIsRejected()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.csrf", true, CarpediemRoles.ChiefLeader);

        var response = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid(), false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
