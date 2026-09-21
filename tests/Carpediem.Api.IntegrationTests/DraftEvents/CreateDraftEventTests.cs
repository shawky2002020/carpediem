using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Events.Infrastructure.Persistence;
using Carpediem.Modules.Events.Application.Drafts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.DraftEvents;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class CreateDraftEventTests(SqlServerFixture sqlServer)
{
    [Theory]
    [InlineData("Bring gloves")]
    [InlineData(null)]
    public async Task AdminCreatesOneDraftWithInitialCurrentCycle(string? notes)
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var response = await harness.CreateAsync(Guid.NewGuid(), new
        {
            title = "  Beach cleanup  ",
            start = new { localDateTime = "2026-10-10T14:30" },
            end = new { localDateTime = "2026-10-10T16:30" },
            location = "  North shore  ",
            notes,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DraftBody>();
        Assert.NotNull(result);
        Assert.Equal("Draft", result.Status);
        Assert.Equal("  Beach cleanup  ", result.Title);
        Assert.Equal("  North shore  ", result.Location);
        Assert.Equal(notes, result.Notes);
        Assert.Equal(new DateTimeOffset(2026, 10, 10, 11, 30, 0, TimeSpan.Zero), result.StartDateTimeUtc);

        await using var verificationScope = harness.Factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var draft = await verification.Set<Event>().Include(eventItem => eventItem.StaffingCycles).SingleAsync();
        Assert.Equal(EventStatus.Draft, draft.Status);
        Assert.Equal(harness.ActorUserId, draft.CreatedByUserId);
        var cycle = Assert.Single(draft.StaffingCycles);
        Assert.True(cycle.IsCurrent);
        Assert.Equal(1, cycle.CycleNumber);
        Assert.Equal(new DateOnly(2026, 10, 10), cycle.EffectiveEventDate);
        var audit = await verification.Set<AuditLog>().SingleAsync();
        Assert.Equal("Events.EventCreated", audit.Action);
        Assert.Equal(draft.Id, audit.ResourceId);
        if (notes is not null)
        {
            Assert.DoesNotContain(notes, audit.NewValue ?? string.Empty, StringComparison.Ordinal);
        }
        var intent = await verification.Set<DraftCreateRequest>().SingleAsync();
        Assert.Equal(harness.ActorUserId, intent.ActorUserId);
        Assert.Equal(draft.Id, intent.EventId);
    }

    [Fact]
    public async Task PersistenceFailureRollsBackEventCycleAuditAndIntent()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDraftEventStore>();
        var now = DateTimeOffset.UtcNow;
        var draft = Event.CreateDraft("Valid title", now.AddHours(1), now.AddHours(2), "Valid location",
            null, harness.ActorUserId, now, DateOnly.FromDateTime(now.UtcDateTime));

        // Bypass the aggregate solely to force SQL Server to reject the atomic persistence unit.
        typeof(Event).GetProperty(nameof(Event.Title))!.SetValue(draft, new string('x', 201));

        await Assert.ThrowsAsync<DbUpdateException>(() => store.CreateAsync(draft, harness.ActorUserId,
            Guid.NewGuid(), new byte[32], Guid.NewGuid(), CancellationToken.None));

        await using var verificationScope = harness.Factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal(0, await verification.Events.CountAsync());
        Assert.Equal(0, await verification.StaffingCycles.CountAsync());
        Assert.Equal(0, await verification.AuditLogs.CountAsync());
        Assert.Equal(0, await verification.DraftCreateRequests.CountAsync());
    }

    private sealed record DraftBody(Guid Id, string Title, string Location, string? Notes, string Status, DateTimeOffset StartDateTimeUtc);
}
