using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Identity;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class AccountManagementTests(SqlServerFixture sqlServer)
{
    private static readonly string[] PromotedRoles = ["Leader", "ChiefLeader"];
    private static readonly string[] LeaderOnlyRoles = ["Leader"];

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

    private static async Task<HttpResponseMessage> SendAsync(DraftApiTestHarness harness,
        HttpMethod method, string path, object body, string? token = null, string? forwardedFor = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token ?? harness.AntiforgeryToken);
        if (forwardedFor is not null) request.Headers.Add("X-Forwarded-For", forwardedFor);
        return await harness.Client.SendAsync(request);
    }

    private sealed record AccountSummary(Guid UserId, string UserName, string Email,
        bool IsActive, string[] Roles);
    private sealed record AccountPage(AccountSummary[] Items, int Page, int PageSize, bool HasMore);
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
