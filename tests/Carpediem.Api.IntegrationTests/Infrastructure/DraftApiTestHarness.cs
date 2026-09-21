using System.Net.Http.Json;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.Infrastructure;

public sealed class DraftApiTestHarness : IAsyncDisposable
{
    private DraftApiTestHarness(CarpediemWebApplicationFactory factory, HttpClient client,
        Guid actorUserId, string antiforgeryToken)
    {
        Factory = factory;
        Client = client;
        ActorUserId = actorUserId;
        AntiforgeryToken = antiforgeryToken;
    }

    public CarpediemWebApplicationFactory Factory { get; }
    public HttpClient Client { get; }
    public Guid ActorUserId { get; }
    public string AntiforgeryToken { get; }

    public static async Task<DraftApiTestHarness> StartAsync(SqlServerFixture sqlServer,
        string role = CarpediemRoles.Admin,
        Action<IServiceCollection>? configureServices = null)
    {
        var factory = new CarpediemWebApplicationFactory(sqlServer.ConnectionString);
        factory.ConfigureServicesForTest = configureServices;
        var actorId = Guid.NewGuid();
        factory.ActorUserId = actorId;
        factory.ActorRole = role;
        var client = factory.CreateHttpsClient();
        await SqlServerFixture.ResetAsync(factory.Services);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
            db.Users.Add(new CarpediemUser
            {
                Id = actorId,
                UserName = "test.actor",
                NormalizedUserName = "TEST.ACTOR",
                Email = "actor@localhost.invalid",
                NormalizedEmail = "ACTOR@LOCALHOST.INVALID",
                IsActive = true,
            });
            var roleId = Guid.NewGuid();
            db.Roles.Add(new IdentityRole<Guid>
            {
                Id = roleId,
                Name = role,
                NormalizedName = role.ToUpperInvariant(),
            });
            db.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = actorId,
                RoleId = roleId,
            });
            await db.SaveChangesAsync();
        }

        var token = await client.GetFromJsonAsync<AntiforgeryBody>(
            new Uri("/api/session/antiforgery", UriKind.Relative));
        return new DraftApiTestHarness(factory, client, actorId, token!.RequestToken);
    }

    public async Task<HttpResponseMessage> CreateAsync(Guid key, object body, bool includeAntiforgery = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/events/drafts")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", key.ToString());
        if (includeAntiforgery) request.Headers.Add("X-XSRF-TOKEN", AntiforgeryToken);
        return await Client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> UpdateAsync(Guid eventId, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/events/drafts/{eventId}")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-XSRF-TOKEN", AntiforgeryToken);
        return await Client.SendAsync(request);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    private sealed record AntiforgeryBody(string RequestToken);
}
