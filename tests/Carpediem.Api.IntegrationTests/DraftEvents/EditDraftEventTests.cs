using System.Net;
using System.Net.Http.Json;
using Carpediem.Api.IntegrationTests.Infrastructure;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Api.IntegrationTests.DraftEvents;

[Collection(nameof(SqlServerFixtureGroup))]
public sealed class EditDraftEventTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task ValidEditPersistsFieldsAndRotatesCycleOnCairoDateChange()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var original = await CreateAsync(harness);

        var response = await harness.UpdateAsync(original.Id, UpdatedBody(original.Version, "Updated event", "2026-10-11T14:30"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<DraftBody>();
        Assert.NotNull(updated);
        Assert.Equal("Updated event", updated.Title);
        Assert.Equal("Draft", updated.Status);
        Assert.NotEqual(original.Version, updated.Version);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var persisted = await db.Events.Include(eventItem => eventItem.StaffingCycles).SingleAsync();
        Assert.Equal("Updated event", persisted.Title);
        Assert.Equal(2, persisted.StaffingCycles.Count);
        Assert.Single(persisted.StaffingCycles, cycle => cycle.IsCurrent && cycle.CycleNumber == 2);
        Assert.Equal(2, await db.AuditLogs.CountAsync());
        var updateAudit = await db.AuditLogs.SingleAsync(log => log.Action == "Events.DraftEventUpdated");
        Assert.DoesNotContain("New notes", updateAudit.NewValue ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("New notes", updateAudit.PreviousValue ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("notesChanged", updateAudit.NewValue ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidEditDoesNotChangePreviousDraftOrAudit()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var original = await CreateAsync(harness);

        var response = await harness.UpdateAsync(original.Id, UpdatedBody(original.Version, " \u2003 ", "2026-10-11T14:30"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        var persisted = await db.Events.Include(eventItem => eventItem.StaffingCycles).SingleAsync();
        Assert.Equal("Beach cleanup", persisted.Title);
        Assert.Single(persisted.StaffingCycles);
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task StaleEditConflictsAndPreservesFirstSavedChange()
    {
        await using var harness = await DraftApiTestHarness.StartAsync(sqlServer);
        var original = await CreateAsync(harness);

        var first = await harness.UpdateAsync(original.Id, UpdatedBody(original.Version, "First change", "2026-10-10T14:30"));
        var stale = await harness.UpdateAsync(original.Id, UpdatedBody(original.Version, "Stale change", "2026-10-10T14:30"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var scope = harness.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        Assert.Equal("First change", (await db.Events.SingleAsync()).Title);
        Assert.Equal(2, await db.AuditLogs.CountAsync());
    }

    private static async Task<DraftBody> CreateAsync(DraftApiTestHarness harness)
    {
        var response = await harness.CreateAsync(Guid.NewGuid(), new
        {
            title = "Beach cleanup",
            start = new { localDateTime = "2026-10-10T14:30" },
            end = new { localDateTime = "2026-10-10T16:30" },
            location = "North shore",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DraftBody>())!;
    }

    private static object UpdatedBody(string version, string title, string start) => new
    {
        title,
        start = new { localDateTime = start },
        end = new { localDateTime = "2026-10-11T16:30" },
        location = "South shore",
        notes = "New notes",
        version,
    };

    private sealed record DraftBody(Guid Id, string Title, string Status, string Version);
}
