using System.Net.Http.Json;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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
    public string DefaultDraftDate { get; } = DateTime.UtcNow.AddDays(14)
        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static async Task<DraftApiTestHarness> StartAsync(SqlServerFixture sqlServer,
        string role = CarpediemRoles.Admin,
        Action<IServiceCollection>? configureServices = null,
        IReadOnlyDictionary<string, string?>? additionalConfiguration = null)
    {
        var factory = new CarpediemWebApplicationFactory(sqlServer.ConnectionString);
        factory.ConfigureServicesForTest = configureServices;
        factory.AdditionalConfiguration = additionalConfiguration;
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

    public async Task<Guid> CreateDefaultDraftAsync()
    {
        var response = await CreateAsync(Guid.NewGuid(), new
        {
            title = "Beach cleanup",
            start = new { localDateTime = $"{DefaultDraftDate}T14:30" },
            end = new { localDateTime = $"{DefaultDraftDate}T16:30" },
            location = "North shore",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedDraftBody>())!.Id;
    }

    public async Task<Guid> SeedUserAsync(string userName, bool isActive, params string[] roles)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var user = new CarpediemUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = $"{userName}@localhost.invalid",
            NormalizedEmail = $"{userName}@localhost.invalid".ToUpperInvariant(),
            IsActive = isActive,
        };
        db.Users.Add(user);
        foreach (var roleName in roles)
        {
            var role = await db.Roles.SingleOrDefaultAsync(item => item.Name == roleName);
            if (role is null)
            {
                role = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant(),
                };
                db.Roles.Add(role);
            }
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
        }
        await db.SaveChangesAsync();
        return user.Id;
    }

    public async Task<HttpResponseMessage> AppointAsync(Guid eventId, Guid candidateUserId,
        Guid requestToken, bool includeAntiforgery = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/events/drafts/{eventId}/chief-leader-appointment")
        {
            Content = JsonContent.Create(new { candidateUserId }),
        };
        request.Headers.Add("Idempotency-Key", requestToken.ToString());
        if (includeAntiforgery) request.Headers.Add("X-XSRF-TOKEN", AntiforgeryToken);
        return await Client.SendAsync(request);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    private sealed record AntiforgeryBody(string RequestToken);
    private sealed record CreatedDraftBody(Guid Id);
}
