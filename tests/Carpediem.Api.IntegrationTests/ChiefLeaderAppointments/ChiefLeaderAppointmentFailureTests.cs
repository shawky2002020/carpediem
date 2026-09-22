using System.Net;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.ChiefLeaderAppointments;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class ChiefLeaderAppointmentFailureTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task UnknownEventIsSafeNotFound()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var candidateId = await harness.SeedUserAsync("chief.missing", true, CarpediemRoles.ChiefLeader);

        var response = await harness.AppointAsync(Guid.NewGuid(), candidateId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<CarpediemDbContext>()
            .ChiefLeaderAssignments.CountAsync());
    }

    [Fact]
    public async Task NonDraftEventIsConflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.nondraft", true, CarpediemRoles.ChiefLeader);
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [events].[Events] SET [Status] = 'Published' WHERE [Id] = {eventId}");
        }

        var response = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
