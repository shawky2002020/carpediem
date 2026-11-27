using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Notifications;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class NotificationInboxTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task InboxAndReadOperationsAreScopedToRecipient()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var otherUserId = await harness.SeedUserAsync("other.notification.user", true);
        var eventResponse = await harness.CreateAsync(Guid.NewGuid(), new
        {
            title = "Notification test event",
            start = new { localDateTime = "2027-10-10T14:30" },
            end = new { localDateTime = "2027-10-10T16:30" },
            location = "North shore",
        });
        eventResponse.EnsureSuccessStatusCode();
        var eventId = (await eventResponse.Content.ReadFromJsonAsync<EventBody>())!.Id;
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var mine = InPlatformNotification.ChiefLeaderAppointment(
            harness.ActorUserId, eventId, now, "{\"eventTitle\":\"Mine\"}");
        var theirs = InPlatformNotification.ChiefLeaderAppointment(
            otherUserId, eventId, now.AddSeconds(1), "{\"eventTitle\":\"Theirs\"}");
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            db.InPlatformNotifications.AddRange(mine, theirs);
            await db.SaveChangesAsync();
        }

        var listResponse = await harness.Client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal("no-store", listResponse.Headers.CacheControl?.ToString());
        var list = await listResponse.Content.ReadFromJsonAsync<InboxItem[]>();
        Assert.Single(list!);
        Assert.Equal(mine.Id, list![0].Id);

        var countResponse = await harness.Client.GetAsync("/api/notifications/unread-count");
        Assert.Equal("no-store", countResponse.Headers.CacheControl?.ToString());
        Assert.Equal(1, (await countResponse.Content.ReadFromJsonAsync<CountBody>())!.Count);

        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(harness, $"/api/notifications/{theirs.Id}/read")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(harness, $"/api/notifications/{mine.Id}/read")).StatusCode);
        DateTimeOffset? firstReadAt;
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            firstReadAt = await db.InPlatformNotifications.Where(item => item.Id == mine.Id)
                .Select(item => item.ReadAt).SingleAsync();
            Assert.NotNull(firstReadAt);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(harness, $"/api/notifications/{mine.Id}/read")).StatusCode);
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            Assert.Equal(firstReadAt, await db.InPlatformNotifications.Where(item => item.Id == mine.Id)
                .Select(item => item.ReadAt).SingleAsync());
        }

        var ownSecond = InPlatformNotification.EventPublished(harness.ActorUserId, eventId, now,
            "{\"eventTitle\":\"Next\"}");
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            db.InPlatformNotifications.Add(ownSecond);
            await db.SaveChangesAsync();
        }
        var allResponse = await PostAsync(harness, "/api/notifications/mark-all-read");
        Assert.Equal(HttpStatusCode.OK, allResponse.StatusCode);
        Assert.Equal(1, (await allResponse.Content.ReadFromJsonAsync<UpdatedBody>())!.Updated);
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            Assert.Null(await db.InPlatformNotifications.Where(item => item.Id == theirs.Id)
                .Select(item => item.ReadAt).SingleAsync());
        }
    }

    [Fact]
    public async Task ReadPostRequiresAntiforgery()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var response = await harness.Client.PostAsJsonAsync($"/api/notifications/{Guid.NewGuid()}/read", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(CarpediemRoles.Admin)]
    [InlineData(CarpediemRoles.ChiefLeader)]
    [InlineData(CarpediemRoles.Leader)]
    public async Task UnreadInboxReachesPastRecentHistoryAndOnlyReturnsTheRecipientsItems(string role)
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var otherUserId = await harness.SeedUserAsync("other.notification.user", true);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var unread = Enumerable.Range(0, 101).Select(_ => InPlatformNotification.EventPublished(
            harness.ActorUserId, eventId, now.AddDays(-2), "{}")).ToArray();
        var newerRead = Enumerable.Range(0, 100).Select(_ =>
        {
            var item = InPlatformNotification.EventPublished(harness.ActorUserId, eventId, now, "{}");
            item.MarkRead(now);
            return item;
        }).ToArray();
        var theirs = InPlatformNotification.EventPublished(otherUserId, eventId, now, "{}");
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            db.InPlatformNotifications.AddRange(unread.Concat(newerRead).Append(theirs));
            await db.SaveChangesAsync();
        }
        harness.Factory.ActorRole = role;

        var recent = (await harness.Client.GetFromJsonAsync<InboxItem[]>("/api/notifications"))!;
        Assert.Equal(100, recent.Length);
        Assert.DoesNotContain(recent, item => item.ReadAt is null);
        Assert.Equal(unread.Length, (await harness.Client.GetFromJsonAsync<CountBody>("/api/notifications/unread-count"))!.Count);

        var firstResponse = await harness.Client.GetAsync("/api/notifications/unread");
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal("no-store", firstResponse.Headers.CacheControl?.ToString());
        var page = (await firstResponse.Content.ReadFromJsonAsync<UnreadPage>())!;
        Assert.NotNull(page.NextBeforeId);
        var firstCursor = page.NextBeforeId!.Value;
        Assert.Equal(page.Items.Last().Id, firstCursor);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(harness, $"/api/notifications/{firstCursor}/read")).StatusCode);
        Assert.Equal(unread.Length - 1, (await harness.Client.GetFromJsonAsync<CountBody>("/api/notifications/unread-count"))!.Count);

        var pages = 1;
        var seen = new HashSet<Guid>();
        while (true)
        {
            Assert.All(page.Items, item => Assert.Null(item.ReadAt));
            Assert.All(page.Items, item => Assert.True(seen.Add(item.Id), "A notification crossed the page boundary twice."));
            if (page.NextBeforeId is not { } next) break;
            Assert.True(++pages <= unread.Length);
            page = (await harness.Client.GetFromJsonAsync<UnreadPage>($"/api/notifications/unread?beforeId={next}"))!;
        }
        Assert.Equal(unread.Select(item => item.Id).Order(), seen.Order());
        Assert.Equal(HttpStatusCode.NotFound, (await harness.Client.GetAsync($"/api/notifications/unread?beforeId={theirs.Id}")).StatusCode);
        harness.Factory.ActorUserId = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await harness.Client.GetAsync("/api/notifications/unread")).StatusCode);
    }

    private static async Task<HttpResponseMessage> PostAsync(DraftApiTestHarness harness, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) };
        request.Headers.Add("X-XSRF-TOKEN", harness.AntiforgeryToken);
        return await harness.Client.SendAsync(request);
    }

    private sealed record InboxItem(Guid Id, DateTimeOffset? ReadAt);
    private sealed record UnreadPage(InboxItem[] Items, Guid? NextBeforeId);
    private sealed record EventBody(Guid Id);
    private sealed record CountBody(int Count);
    private sealed record UpdatedBody(int Updated);
}
