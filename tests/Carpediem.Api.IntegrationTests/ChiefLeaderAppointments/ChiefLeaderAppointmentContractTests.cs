using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Modules.Identity.Authorization;

namespace Carpediem.Api.IntegrationTests.ChiefLeaderAppointments;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class ChiefLeaderAppointmentContractTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task ReadAndCreateMatchTheDocumentedShapes()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var eventId = await harness.CreateDefaultDraftAsync();
        var candidateId = await harness.SeedUserAsync("chief.contract", true, CarpediemRoles.ChiefLeader);

        var read = await harness.Client.GetAsync($"/api/events/drafts/{eventId}/chief-leader-appointment");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var context = await JsonDocument.ParseAsync(await read.Content.ReadAsStreamAsync());
        Assert.Equal(eventId, context.RootElement.GetProperty("eventId").GetGuid());
        Assert.Equal("Draft", context.RootElement.GetProperty("eventStatus").GetString());
        Assert.Equal(JsonValueKind.Null, context.RootElement.GetProperty("currentAppointment").ValueKind);
        Assert.Contains(context.RootElement.GetProperty("candidates").EnumerateArray(),
            item => item.GetProperty("userId").GetGuid() == candidateId);

        var created = await harness.AppointAsync(eventId, candidateId, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        using var appointment = await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync());
        Assert.Equal("Pending", appointment.RootElement.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(appointment.RootElement.GetProperty("version").GetString()));
    }

    [Fact]
    public async Task MalformedEventIdentifierReturnsSafeProblem()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);

        var response = await harness.Client.GetAsync("/api/events/drafts/not-a-guid/chief-leader-appointment");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal("malformed_request", problem!.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    private sealed record ProblemBody(string Code, string TraceId);
}
