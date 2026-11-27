using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Audit;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Identity.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Identity;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class AccountManagementTests(SqlServerFixture sqlServer)
{
    private static readonly string[] PromotedRoles = ["Leader", "ChiefLeader"];
    private static readonly string[] LeaderOnlyRoles = ["Leader"];

    [Fact]
    public async Task AccountDirectoryQueriesTheAuthorizedDatasetBeforePaging()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var first = await harness.SeedUserAsync("pending.first", false, "Leader");
        var second = await harness.SeedUserAsync("pending.second", false, "ChiefLeader");
        var inactive = await harness.SeedUserAsync("pending.previously-approved", false, "Leader");
        var longName = new string('z', 120);
        var tail = await harness.SeedUserAsync(longName, true, PromotedRoles);
        var noName = Guid.NewGuid();
        var requested = DateTimeOffset.UtcNow.AddDays(-5);
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var leaderId = await db.Roles.Where(role => role.Name == "Leader").Select(role => role.Id).SingleAsync();
            for (var i = 0; i < 155; i++)
            {
                var id = Guid.NewGuid();
                db.Users.Add(new CarpediemUser
                {
                    Id = id, UserName = $"fixture.{i:D3}", NormalizedUserName = $"FIXTURE.{i:D3}",
                    Email = $"fixture.{i:D3}@example.invalid", NormalizedEmail = $"FIXTURE.{i:D3}@EXAMPLE.INVALID",
                    IsActive = true,
                });
                db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = id, RoleId = leaderId });
            }
            db.Users.Add(new CarpediemUser
            {
                Id = noName, Email = "missing%name@example.invalid", NormalizedEmail = "MISSING%NAME@EXAMPLE.INVALID",
                IsActive = false,
            });
            db.AuditLogs.AddRange(AuditLog.AccountRegistrationRequested(first, requested),
                AuditLog.AccountRegistrationRequested(second, requested.AddDays(1)),
                AuditLog.AccountRegistrationRequested(inactive, requested.AddDays(-1)),
                AuditLog.AccountChanged(inactive, harness.ActorUserId, Guid.NewGuid(), requested,
                    "AccountApproved", "False", "True"),
                AuditLog.AccountChanged(inactive, harness.ActorUserId, Guid.NewGuid(), requested.AddDays(2),
                    "AccountDeactivated", "True", "False"));
            await db.SaveChangesAsync();
        }

        var firstPage = await GetPageAsync(harness);
        Assert.Equal(161, firstPage.TotalAccounts);
        Assert.Equal(2, firstPage.ReviewCount);
        Assert.Equal(161, firstPage.MatchingAccounts);
        Assert.Equal(20, firstPage.Items.Length);
        Assert.DoesNotContain(firstPage.Items, item => item.UserId == tail);
        Assert.Equal(tail, Assert.Single((await GetPageAsync(harness, "?search=%20ZZZZ%20")).Items).UserId);
        Assert.Equal(noName, Assert.Single((await GetPageAsync(harness, "?search=%25name&status=inactive")).Items).UserId);
        using (var privateSearch = new HttpRequestMessage(HttpMethod.Get, "/api/admin/accounts"))
        {
            privateSearch.Headers.Add("X-Account-Search", Uri.EscapeDataString("missing%name"));
            using var response = await harness.Client.SendAsync(privateSearch);
            response.EnsureSuccessStatusCode();
            Assert.Equal(noName, Assert.Single((await response.Content.ReadFromJsonAsync<AccountPage>())!.Items).UserId);
        }

        var combined = await GetPageAsync(harness, "?roles=Leader&roles=ChiefLeader&status=active&sort=name-desc");
        Assert.Equal(156, combined.MatchingAccounts);
        Assert.Equal(tail, combined.Items[0].UserId);
        Assert.Equal(2, combined.Items[0].Roles.Length);
        Assert.Equal(combined.Items.Length, combined.Items.Select(item => item.UserId).Distinct().Count());

        var review = await GetPageAsync(harness, "?view=review");
        Assert.Equal(new[] { first, second }, review.Items.Select(item => item.UserId));
        Assert.Equal(requested, review.Items[0].PendingSince);
        Assert.Equal("pending", review.Items[0].Status);
        Assert.Equal(inactive, Assert.Single((await GetPageAsync(harness, "?search=pending&status=inactive")).Items).UserId);
        var lastPage = await GetPageAsync(harness, "?page=9999");
        Assert.Equal(9, lastPage.Page);
        Assert.False(lastPage.HasMore);
        Assert.Single(lastPage.Items);
        Assert.Empty((await GetPageAsync(harness, "?search=no-matches&page=5")).Items);

        foreach (var invalid in new[] { "?page=0", "?sort=email", "?roles=Operator", "?view=review&status=active" })
            Assert.Equal(HttpStatusCode.BadRequest, (await harness.Client.GetAsync($"/api/admin/accounts{invalid}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(harness, HttpMethod.Post,
            $"/api/admin/accounts/{first}/approve", new { })).StatusCode);
        var afterApproval = await GetPageAsync(harness, "?view=review");
        Assert.Equal(1, afterApproval.ReviewCount);
        Assert.Equal(1, afterApproval.MatchingAccounts);
        Assert.Equal(second, Assert.Single(afterApproval.Items).UserId);

        harness.Factory.ActorRole = "Leader";
        Assert.Equal(HttpStatusCode.Forbidden, (await harness.Client.GetAsync("/api/admin/accounts?search=fixture")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await harness.Client.GetAsync($"/api/admin/accounts/{tail}")).StatusCode);
        var denied = await SendAsync(harness, HttpMethod.Put, $"/api/admin/accounts/{tail}/roles", new { roles = LeaderOnlyRoles });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        harness.Factory.ActorRole = "Admin";
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var actor = await db.Users.SingleAsync(user => user.Id == harness.ActorUserId);
            actor.IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await harness.Client.GetAsync("/api/admin/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(harness, HttpMethod.Post,
            $"/api/admin/accounts/{second}/approve", new { })).StatusCode);
    }

    [Fact]
    public async Task StaleDetailsCannotOverwriteRoleOrAccessChanges()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var target = await harness.SeedUserAsync("original.leader", true, "Leader");
        var url = $"/api/admin/accounts/{target}";
        var before = (await harness.Client.GetFromJsonAsync<AccountDetail>(url))!;
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(harness, HttpMethod.Put,
            $"{url}/roles", new { roles = PromotedRoles }, version: before.Version)).StatusCode);
        var staleRoles = await SendAsync(harness, HttpMethod.Put, $"{url}/roles", new { roles = LeaderOnlyRoles }, version: before.Version);
        Assert.Equal(HttpStatusCode.Conflict, staleRoles.StatusCode);
        Assert.Equal("account_conflict", (await staleRoles.Content.ReadFromJsonAsync<ProblemBody>())!.Code);
        var staleAccess = await SendAsync(harness, HttpMethod.Post, $"{url}/deactivate", new { }, version: before.Version);
        Assert.Equal(HttpStatusCode.Conflict, staleAccess.StatusCode);
        var after = (await harness.Client.GetFromJsonAsync<AccountDetail>(url))!;
        Assert.True(after.IsActive);
        Assert.Equal(PromotedRoles.Order(), after.Roles.Order());
        Assert.Single(after.History, entry => entry.Action == "AccountRolesChanged");
    }

    [Fact]
    public async Task RegistrationRequiresApprovalAndAdminCanChangeAccess()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var healthResponse = await harness.Client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        harness.Factory.ActorUserId = null;
        var anonymousToken = (await harness.Client.GetFromJsonAsync<AntiforgeryBody>("/api/session/antiforgery"))!.RequestToken;
        var registration = await SendAsync(harness, HttpMethod.Post, "/api/accounts/register",
            new { userName = "new.leader", email = "new.leader@localhost.invalid", password = "NewLeader123!" }, anonymousToken);
        Assert.True(registration.StatusCode == HttpStatusCode.Accepted, await registration.Content.ReadAsStringAsync());
        var duplicate = await SendAsync(harness, HttpMethod.Post, "/api/accounts/register",
            new { userName = "new.leader", email = "new.leader@localhost.invalid", password = "NewLeader123!" }, anonymousToken);
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        Assert.Equal(await registration.Content.ReadAsStringAsync(), await duplicate.Content.ReadAsStringAsync());
        var invalidExisting = await SendAsync(harness, HttpMethod.Post, "/api/accounts/register",
            new { userName = "invalid*name", email = "new.leader@localhost.invalid", password = "NewLeader123!" }, anonymousToken);
        var invalidNew = await SendAsync(harness, HttpMethod.Post, "/api/accounts/register",
            new { userName = "invalid*name", email = "different@localhost.invalid", password = "NewLeader123!" }, anonymousToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidExisting.StatusCode);
        Assert.Equal(invalidExisting.StatusCode, invalidNew.StatusCode);
        Assert.Equal(await invalidExisting.Content.ReadFromJsonAsync<ProblemBody>(),
            await invalidNew.Content.ReadFromJsonAsync<ProblemBody>());
        await using (var scope = harness.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            var user = await db.Users.SingleAsync(item => item.UserName == "new.leader");
            Assert.False(user.IsActive);
            Assert.Equal(1, await db.Users.CountAsync(item => item.UserName == "new.leader"));
            Assert.True(await db.UserRoles.Join(db.Roles, ur => ur.RoleId, role => role.Id,
                (ur, role) => new { ur.UserId, role.Name }).AnyAsync(row => row.UserId == user.Id && row.Name == "Leader"));
        }
        var login = await SendAsync(harness, HttpMethod.Post, "/api/session/login",
            new { userName = "new.leader", password = "NewLeader123!" }, anonymousToken);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        harness.Factory.ActorUserId = harness.ActorUserId;
        var adminToken = (await harness.Client.GetFromJsonAsync<AntiforgeryBody>("/api/session/antiforgery"))!.RequestToken;
        var accounts = await harness.Client.GetAsync("/api/admin/accounts");
        Assert.Equal(HttpStatusCode.OK, accounts.StatusCode);
        var registered = (await accounts.Content.ReadFromJsonAsync<AccountPage>())!.Items
            .Single(item => item.UserName == "new.leader");
        Assert.False(registered.IsActive);

        var approved = await SendAsync(harness, HttpMethod.Post,
            $"/api/admin/accounts/{registered.UserId}/approve", new { }, adminToken);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        harness.Factory.ActorUserId = null;
        var approvedToken = (await harness.Client.GetFromJsonAsync<AntiforgeryBody>("/api/session/antiforgery"))!.RequestToken;
        var approvedLogin = await SendAsync(harness, HttpMethod.Post, "/api/session/login",
            new { userName = "new.leader", password = "NewLeader123!" }, approvedToken);
        Assert.Equal(HttpStatusCode.OK, approvedLogin.StatusCode);
        harness.Factory.ActorUserId = harness.ActorUserId;
        adminToken = (await harness.Client.GetFromJsonAsync<AntiforgeryBody>("/api/session/antiforgery"))!.RequestToken;
        var promoted = await SendAsync(harness, HttpMethod.Put,
            $"/api/admin/accounts/{registered.UserId}/roles", new { roles = PromotedRoles }, adminToken);
        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
        var deactivated = await SendAsync(harness, HttpMethod.Post,
            $"/api/admin/accounts/{registered.UserId}/deactivate", new { }, adminToken);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        harness.Factory.ActorUserId = null;
        var inactiveToken = (await harness.Client.GetFromJsonAsync<AntiforgeryBody>("/api/session/antiforgery"))!.RequestToken;
        var inactiveLogin = await SendAsync(harness, HttpMethod.Post, "/api/session/login",
            new { userName = "new.leader", password = "NewLeader123!" }, inactiveToken);
        Assert.Equal(HttpStatusCode.Unauthorized, inactiveLogin.StatusCode);
        harness.Factory.ActorUserId = harness.ActorUserId;
        adminToken = (await harness.Client.GetFromJsonAsync<AntiforgeryBody>("/api/session/antiforgery"))!.RequestToken;
        var self = await SendAsync(harness, HttpMethod.Post,
            $"/api/admin/accounts/{harness.ActorUserId}/deactivate", new { }, adminToken);
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
        var selfDemotion = await SendAsync(harness, HttpMethod.Put,
            $"/api/admin/accounts/{harness.ActorUserId}/roles", new { roles = LeaderOnlyRoles }, adminToken);
        Assert.Equal(HttpStatusCode.Conflict, selfDemotion.StatusCode);
    }

    [Fact]
    public async Task PublicRegistrationIsRateLimited()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var healthResponse = await harness.Client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        harness.Factory.ActorUserId = null;
        var token = (await harness.Client.GetFromJsonAsync<AntiforgeryBody>("/api/session/antiforgery"))!.RequestToken;
        for (var index = 0; index < 5; index++)
        {
            var response = await SendAsync(harness, HttpMethod.Post, "/api/accounts/register",
                new { userName = $"limited{index}", email = $"limited{index}@localhost.invalid", password = "NewLeader123!" }, token);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }
        var rejected = await SendAsync(harness, HttpMethod.Post, "/api/accounts/register",
            new { userName = "limited5", email = "limited5@localhost.invalid", password = "NewLeader123!" }, token);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task PublicRegistrationUsesTrustedForwardedClientIpForRateLimit()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer,
            configureServices: services => services.AddSingleton<IStartupFilter>(
                new TestRemoteIpStartupFilter(IPAddress.Loopback)),
            additionalConfiguration: new Dictionary<string, string?>
            {
                ["ReverseProxy:TrustedNetworks:0"] = "127.0.0.0/8",
                ["ReverseProxy:TrustedNetworks:1"] = "203.0.113.0/24",
            });
        harness.Factory.ActorUserId = null;
        var token = (await harness.Client.GetFromJsonAsync<AntiforgeryBody>("/api/session/antiforgery"))!.RequestToken;
        for (var index = 0; index < 5; index++)
        {
            var response = await SendAsync(harness, HttpMethod.Post, "/api/accounts/register",
                new { userName = $"forwarded{index}", email = $"forwarded{index}@localhost.invalid", password = "NewLeader123!" },
                token, "198.51.100.11, 203.0.113.1");
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }
        var rejected = await SendAsync(harness, HttpMethod.Post, "/api/accounts/register",
            new { userName = "forwarded5", email = "forwarded5@localhost.invalid", password = "NewLeader123!" },
            token, "198.51.100.11, 203.0.113.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        var otherViewer = await SendAsync(harness, HttpMethod.Post, "/api/accounts/register",
            new { userName = "otherviewer", email = "otherviewer@localhost.invalid", password = "NewLeader123!" },
            token, "198.51.100.12, 203.0.113.1");
        Assert.Equal(HttpStatusCode.Accepted, otherViewer.StatusCode);
    }

    private static async Task<AccountPage> GetPageAsync(DraftApiTestHarness harness, string query = "") =>
        (await harness.Client.GetFromJsonAsync<AccountPage>($"/api/admin/accounts{query}"))!;

    private static async Task<HttpResponseMessage> SendAsync(DraftApiTestHarness harness,
        HttpMethod method, string path, object body, string? token = null, string? forwardedFor = null, string? version = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token ?? harness.AntiforgeryToken);
        if (forwardedFor is not null) request.Headers.Add("X-Forwarded-For", forwardedFor);
        if (version is not null) request.Headers.IfMatch.ParseAdd($"\"{version}\"");
        return await harness.Client.SendAsync(request);
    }

    private sealed record AccountSummary(Guid UserId, string UserName, string Email,
        bool IsActive, string[] Roles, string Status, DateTimeOffset? PendingSince);
    private sealed record AccountPage(AccountSummary[] Items, int Page, int PageSize, bool HasMore,
        int TotalAccounts, int ReviewCount, int MatchingAccounts);
    private sealed record AccountDetail(bool IsActive, string[] Roles, string Version, AccountHistory[] History);
    private sealed record AccountHistory(string Action);
    private sealed record AntiforgeryBody(string RequestToken);
    private sealed record ProblemBody(string Code, string? Detail);

    private sealed class TestRemoteIpStartupFilter(IPAddress remoteIpAddress) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) =>
            {
                context.Connection.RemoteIpAddress = remoteIpAddress;
                return following();
            });
            next(app);
        };
    }
}
