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
public sealed class ChiefLeaderCandidateTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task CandidatePoolContainsActiveChiefLeadersIncludingDualRoleOnly()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var chiefId = await harness.SeedUserAsync("chief.active", true, CarpediemRoles.ChiefLeader);
        var dualId = await harness.SeedUserAsync("chief.dual", true, CarpediemRoles.ChiefLeader, CarpediemRoles.Leader);
        await harness.SeedUserAsync("chief.inactive", false, CarpediemRoles.ChiefLeader);
        await harness.SeedUserAsync("leader.only", true, CarpediemRoles.Leader);

        var result = await harness.Client.GetFromJsonAsync<ContextBody>(
            $"/api/events/drafts/{eventId}/chief-leader-appointment");

        Assert.NotNull(result);
        Assert.Equal(new[] { chiefId, dualId }.Order(), result.Candidates.Select(item => item.UserId).Order());
        Assert.All(result.Candidates, item => Assert.DoesNotContain("@", item.UserName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CandidateLosingEligibilityIsRejectedWithoutPartialWrites()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.stale", true, CarpediemRoles.ChiefLeader);
        await harness.Client.GetAsync($"/api/events/drafts/{eventId}/chief-leader-appointment");
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            (await db.Users.SingleAsync(item => item.Id == candidateId)).IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await using var verificationScope = harness.Factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(0, await verification.Set<ChiefLeaderAssignment>().CountAsync());
        Assert.Equal(0, await verification.InPlatformNotifications.CountAsync());
        Assert.Equal(1, await verification.AuditLogs.CountAsync()); // Draft creation only.
    }

    private sealed record CandidateBody(Guid UserId, string UserName);
    private sealed record ContextBody(CandidateBody[] Candidates);
}
