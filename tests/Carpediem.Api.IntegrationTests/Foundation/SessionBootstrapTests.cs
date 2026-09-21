using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;

namespace Carpediem.Api.IntegrationTests.Foundation;

public sealed class SessionBootstrapTests
{
    private const string UnusedConnectionString = "Server=localhost;Database=Unused;User Id=sa;Password=Unused;TrustServerCertificate=True";

    [Fact]
    public async Task IssuesAntiforgeryTokenWithoutCachingIt()
    {
        await using var factory = new CarpediemWebApplicationFactory(UnusedConnectionString);
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/api/session/antiforgery", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadFromJsonAsync<AntiforgeryBody>();
        Assert.False(string.IsNullOrWhiteSpace(body?.RequestToken));
    }

    [Fact]
    public async Task CurrentSessionRequiresAuthentication()
    {
        await using var factory = new CarpediemWebApplicationFactory(UnusedConnectionString);
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/api/session/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("unauthenticated", body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task LoginRequiresAntiforgeryToken()
    {
        await using var factory = new CarpediemWebApplicationFactory(UnusedConnectionString);
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync(new Uri("/api/session/login", UriKind.Relative), new { userName = "admin", password = "example" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record AntiforgeryBody(string RequestToken);
}
