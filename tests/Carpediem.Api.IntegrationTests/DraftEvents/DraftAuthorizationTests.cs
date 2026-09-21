using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.DraftEvents;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class DraftAuthorizationTests(SqlServerFixture sqlServer)
{
    [Theory]
    [InlineData(CarpediemRoles.ChiefLeader)]
    [InlineData(CarpediemRoles.Leader)]
    public async Task NonAdminCannotCreateOrUpdateDrafts(string role)
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var created = await CreateAsync(harness);
        harness.Factory.ActorRole = role;

        var deniedCreate = await harness.CreateAsync(Guid.NewGuid(), ValidCreateBody());
        var deniedUpdate = await harness.UpdateAsync(created.Id, ValidUpdateBody(created.Version));

        Assert.Equal(HttpStatusCode.Forbidden, deniedCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deniedUpdate.StatusCode);
        await AssertSingleUnchangedDraftAsync(harness);
    }

    [Fact]
    public async Task AnonymousActorCannotCreateOrUpdateDrafts()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var created = await CreateAsync(harness);
        harness.Factory.ActorUserId = null;
        harness.Factory.ActorRole = null;

        var deniedCreate = await harness.CreateAsync(Guid.NewGuid(), ValidCreateBody());
        var deniedUpdate = await harness.UpdateAsync(created.Id, ValidUpdateBody(created.Version));

        Assert.Equal(HttpStatusCode.Unauthorized, deniedCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deniedUpdate.StatusCode);
        await AssertSingleUnchangedDraftAsync(harness);
    }

    [Fact]
    public async Task MutationWithoutAntiforgeryTokenIsRejectedWithoutWrites()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);

        var response = await harness.CreateAsync(Guid.NewGuid(), ValidCreateBody(), includeAntiforgery: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal("antiforgery_failed", problem!.Code);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(0, await db.Events.CountAsync());
    }

    private static async Task<DraftBody> CreateAsync(DraftApiTestHarness harness)
    {
        var response = await harness.CreateAsync(Guid.NewGuid(), ValidCreateBody());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DraftBody>())!;
    }

    private static object ValidCreateBody() => new
    {
        title = "Beach cleanup",
        start = new { localDateTime = "2026-10-10T14:30" },
        end = new { localDateTime = "2026-10-10T16:30" },
        location = "North shore",
    };

    private static object ValidUpdateBody(string version) => new
    {
        title = "Unauthorized change",
        start = new { localDateTime = "2026-10-10T14:30" },
        end = new { localDateTime = "2026-10-10T16:30" },
        location = "North shore",
        version,
    };

    private static async Task AssertSingleUnchangedDraftAsync(DraftApiTestHarness harness)
    {
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var draft = await db.Events.SingleAsync();
        Assert.Equal("Beach cleanup", draft.Title);
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    private sealed record DraftBody(Guid Id, string Version);
    private sealed record ProblemBody(string Code);
}
