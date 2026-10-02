using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Application.Completion;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfEventCompletionStore(
    CarpediemDbContext db,
    UserManager<CarpediemUser> userManager) : IEventCompletionStore
{
    public async Task<int> ProcessScheduledCompletionsAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        // 1. Discover published events whose scheduled end time has concluded
        var eligibleEventIds = await db.Events.AsNoTracking()
            .Where(e => e.Status == EventStatus.Published && e.EndDateTime <= utcNow)
            .OrderBy(e => e.EndDateTime)
            .Take(50)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (eligibleEventIds.Count == 0)
        {
            return 0;
        }

        var completedCount = 0;

        foreach (var eventId in eligibleEventIds)
        {
            try
            {
                var strategy = db.Database.CreateExecutionStrategy();
                var completed = await strategy.ExecuteAsync(async () =>
                {
                    db.ChangeTracker.Clear();

                    var eventItem = await db.Events
                        .SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken);

                    if (eventItem is null || eventItem.Status != EventStatus.Published || eventItem.EndDateTime > utcNow)
                    {
                        // Event was already completed by another replica/Admin or schedule was extended
                        return false;
                    }

                    eventItem.Complete(utcNow);
                    var audit = AuditLog.EventCompleted(
                        eventItem.Id,
                        actorUserId: null,
                        correlationId: Guid.NewGuid(),
                        occurredAt: utcNow,
                        isManualCloseout: false);

                    db.AuditLogs.Add(audit);
                    await db.SaveChangesAsync(cancellationToken);
                    return true;
                });

                if (completed)
                {
                    completedCount++;
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                // Multi-replica race condition: another replica or Admin closed it out concurrently.
                // Safely drop and no-op per ADR-007 / Race 9.
            }
        }

        return completedCount;
    }

    public async Task<CompleteEventResult> CompleteEventManuallyAsync(
        Guid eventId,
        byte[] expectedVersion,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();

            var actorUser = await userManager.FindByIdAsync(actorUserId.ToString());
            if (actorUser is null || !actorUser.IsActive || !await userManager.IsInRoleAsync(actorUser, CarpediemRoles.Admin))
            {
                throw new UnauthorizedAccessException("Only Platform Administrators can execute administrative event closeout.");
            }

            var eventItem = await db.Events
                .SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken)
                ?? throw new KeyNotFoundException($"Event {eventId} was not found.");

            // Check for idempotent replay
            if (eventItem.Status == EventStatus.Completed)
            {
                if (eventItem.RowVersion.AsSpan().SequenceEqual(expectedVersion))
                {
                    return new CompleteEventResult(
                        eventItem.Id,
                        eventItem.Title,
                        eventItem.Status.ToString(),
                        eventItem.StartDateTime,
                        eventItem.EndDateTime,
                        eventItem.Location,
                        eventItem.Notes,
                        utcNow,
                        eventItem.RowVersion,
                        IsReplay: true);
                }

                throw new DbUpdateConcurrencyException("Concurrency mismatch on completed event.");
            }

            if (eventItem.Status != EventStatus.Published)
            {
                throw new InvalidEventStatusForCompletionException(eventItem.Id, eventItem.Status.ToString());
            }

            if (!eventItem.RowVersion.AsSpan().SequenceEqual(expectedVersion))
            {
                throw new DbUpdateConcurrencyException("Concurrency mismatch on event.");
            }

            eventItem.Complete(utcNow, actorUserId);

            var audit = AuditLog.EventCompleted(
                eventItem.Id,
                actorUserId,
                correlationId,
                utcNow,
                isManualCloseout: true);

            db.AuditLogs.Add(audit);
            await db.SaveChangesAsync(cancellationToken);

            return new CompleteEventResult(
                eventItem.Id,
                eventItem.Title,
                eventItem.Status.ToString(),
                eventItem.StartDateTime,
                eventItem.EndDateTime,
                eventItem.Location,
                eventItem.Notes,
                utcNow,
                eventItem.RowVersion,
                IsReplay: false);
        });
    }
}
