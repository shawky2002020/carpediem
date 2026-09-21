using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.DraftEvents;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class ReadDraftEventTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task AdminListsAndReopensOnlyDraftEvents()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var first = await CreateAsync(harness, "First");
        await CreateAsync(harness, "Second");
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var second = await db.Events.SingleAsync(eventItem => eventItem.Title == "Second");
            db.Entry(second).Property(eventItem => eventItem.Status).CurrentValue = EventStatus.Published;
            await db.SaveChangesAsync();
        }

        var listResponse = await harness.Client.GetAsync(new Uri("/api/events/drafts?page=1&pageSize=20", UriKind.Relative));
        var detailResponse = await harness.Client.GetAsync(new Uri($"/api/events/drafts/{first.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<DraftListBody>();
        Assert.Single(list!.Items);
        Assert.Equal(first.Id, list.Items[0].Id);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<DraftBody>();
        Assert.Equal(first.Id, detail!.Id);
        Assert.Equal("First", detail.Title);
        Assert.Equal("2026-10-10T14:30:00", detail.Start.LocalDateTime);
        Assert.Equal("+03:00", detail.Start.Offset);
        Assert.False(string.IsNullOrWhiteSpace(detail.Version));
    }

    [Fact]
    public async Task MissingEventIsNotFoundAndNonDraftIsConflict()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var created = await CreateAsync(harness, "First");
        var missing = await harness.Client.GetAsync(new Uri($"/api/events/drafts/{Guid.NewGuid()}", UriKind.Relative));
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var draft = await db.Events.SingleAsync();
            db.Entry(draft).Property(eventItem => eventItem.Status).CurrentValue = EventStatus.Published;
            await db.SaveChangesAsync();
        }
        var nonDraft = await harness.Client.GetAsync(new Uri($"/api/events/drafts/{created.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, nonDraft.StatusCode);
    }

    [Fact]
    public async Task NonAdminAndAnonymousCannotReadDrafts()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var created = await CreateAsync(harness, "First");
        harness.Factory.ActorRole = CarpediemRoles.Leader;

        var deniedList = await harness.Client.GetAsync(new Uri("/api/events/drafts", UriKind.Relative));
        var deniedDetail = await harness.Client.GetAsync(new Uri($"/api/events/drafts/{created.Id}", UriKind.Relative));
        harness.Factory.ActorUserId = null;
        var anonymous = await harness.Client.GetAsync(new Uri("/api/events/drafts", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, deniedList.StatusCode);
        using (var problem = JsonDocument.Parse(await deniedList.Content.ReadAsStringAsync()))
        {
            Assert.Equal("forbidden", problem.RootElement.GetProperty("code").GetString());
            Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        }
        Assert.Equal(HttpStatusCode.Forbidden, deniedDetail.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task DeactivatedAdminIsDeniedOnNextDraftRequest()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        await CreateAsync(harness, "First");
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var actor = await db.Users.SingleAsync();
            actor.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await harness.Client.GetAsync(new Uri("/api/events/drafts", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RemovedAdminRoleIsDeniedDespiteOldSessionClaim()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        await CreateAsync(harness, "First");
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            db.UserRoles.Remove(await db.UserRoles.SingleAsync());
            await db.SaveChangesAsync();
        }

        var response = await harness.Client.GetAsync(new Uri("/api/events/drafts", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<DraftBody> CreateAsync(DraftApiTestHarness harness, string title)
    {
        var response = await harness.CreateAsync(Guid.NewGuid(), new
        {
            title,
            start = new { localDateTime = "2026-10-10T14:30" },
            end = new { localDateTime = "2026-10-10T16:30" },
            location = "North shore",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DraftBody>())!;
    }

    private sealed record CairoTimeBody(string LocalDateTime, string Offset);
    private sealed record DraftBody(Guid Id, string Title, CairoTimeBody Start, string Version);
    private sealed record DraftSummaryBody(Guid Id);
    private sealed record DraftListBody(DraftSummaryBody[] Items, int Page, int PageSize);
}
