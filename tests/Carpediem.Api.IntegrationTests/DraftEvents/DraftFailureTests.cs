using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Application.Drafts;
using Carpediem.Modules.Events.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Carpediem.Api.IntegrationTests.DraftEvents;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class DraftFailureTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task UnexpectedFailureReturnsSafeTraceableProblemWithoutWrites()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer, configureServices: services =>
        {
            services.RemoveAll<IDraftEventStore>();
            services.AddScoped<IDraftEventStore>(_ => new ThrowingDraftEventStore());
        });

        var response = await harness.Client.GetAsync("/api/events/drafts");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.NotNull(problem);
        Assert.Equal("unexpected_error", problem.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
        Assert.DoesNotContain("forced storage failure", problem.Detail, StringComparison.OrdinalIgnoreCase);

        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(0, await db.Events.CountAsync());
        Assert.Equal(0, await db.AuditLogs.CountAsync());
    }

    private sealed record ProblemBody(string Code, string TraceId, string Detail);

    private sealed class ThrowingDraftEventStore : IDraftEventStore
    {
        private static InvalidOperationException Failure() => new("forced storage failure");

        public Task<IReadOnlyList<Event>> ListAsync(int page, int pageSize, Guid actorUserId,
            CancellationToken cancellationToken) => throw Failure();

        public Task<Event> GetAsync(Guid eventId, Guid actorUserId, CancellationToken cancellationToken) =>
            throw Failure();

        public Task<DraftCreateStoreResult> CreateAsync(Event draft, Guid actorUserId, Guid requestToken,
            byte[] fingerprint, Guid correlationId, CancellationToken cancellationToken) => throw Failure();

        public Task<Event> UpdateAsync(Guid eventId, DraftEditValues values, byte[] expectedVersion,
            Guid actorUserId, Guid correlationId, CancellationToken cancellationToken) => throw Failure();
    }
}
