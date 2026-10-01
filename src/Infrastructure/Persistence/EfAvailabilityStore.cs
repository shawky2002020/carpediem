using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Staffing.Application.Availability;
using Carpediem.Modules.Staffing.Domain.Availability;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfAvailabilityStore(CarpediemDbContext db) : IAvailabilityStore
{
    public async Task<MyAvailabilityDto?> GetMyAvailabilityAsync(Guid eventId, Guid userId, CancellationToken cancellationToken)
    {
        var eventItem = await db.Events.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (eventItem is null)
        {
            return null;
        }

        var cycle = await db.StaffingCycles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.EventId == eventId && c.IsCurrent, cancellationToken);

        if (cycle is null)
        {
            return null;
        }

        var isChiefLeaderOnEvent = await db.ChiefLeaderAssignments.AsNoTracking()
            .AnyAsync(a => a.EventId == eventId && a.ChiefLeaderUserId == userId && a.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var isWindowOpen = eventItem.Status == EventStatus.Published && eventItem.StartDateTime > now;

        var existing = await db.AvailabilityResponses.AsNoTracking()
            .SingleOrDefaultAsync(a => a.StaffingCycleId == cycle.Id && a.UserId == userId, cancellationToken);

        return new MyAvailabilityDto(
            EventId: eventId,
            StaffingCycleId: cycle.Id,
            Response: existing?.Response,
            DeclaredAt: existing?.DeclaredAt,
            Version: existing is not null ? Convert.ToBase64String(existing.RowVersion) : null,
            IsWindowOpen: isWindowOpen,
            EventStatus: eventItem.Status.ToString(),
            IsChiefLeaderOnEvent: isChiefLeaderOnEvent);
    }

    public async Task<AvailabilityDeclarationResult> DeclareAvailabilityAsync(DeclareAvailabilityCommand command, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();

            var eventItem = await db.Events
                .SingleOrDefaultAsync(e => e.Id == command.EventId, cancellationToken)
                ?? throw new KeyNotFoundException($"Event {command.EventId} was not found.");

            if (eventItem.Status != EventStatus.Published)
            {
                throw new InvalidEventStatusException(eventItem.Id, eventItem.Status.ToString());
            }

            var now = DateTimeOffset.UtcNow;
            if (eventItem.StartDateTime <= now)
            {
                throw new AvailabilityWindowClosedException(eventItem.Id, eventItem.StartDateTime, now);
            }

            var isChiefLeader = await db.ChiefLeaderAssignments.AsNoTracking()
                .AnyAsync(a => a.EventId == command.EventId && a.ChiefLeaderUserId == command.UserId && a.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);

            if (isChiefLeader)
            {
                throw new ChiefLeaderExclusivityException(command.EventId, command.UserId);
            }

            var cycle = await db.StaffingCycles
                .SingleOrDefaultAsync(c => c.EventId == command.EventId && c.IsCurrent, cancellationToken)
                ?? throw new InvalidOperationException($"No active staffing cycle found for Event {command.EventId}.");

            var existing = await db.AvailabilityResponses
                .SingleOrDefaultAsync(a => a.StaffingCycleId == cycle.Id && a.UserId == command.UserId, cancellationToken);

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            if (existing is not null)
            {
                // Idempotent replay check
                if (existing.Response == command.Response)
                {
                    return new AvailabilityDeclarationResult(
                        Id: existing.Id,
                        EventId: command.EventId,
                        StaffingCycleId: cycle.Id,
                        UserId: command.UserId,
                        Response: existing.Response,
                        DeclaredAt: existing.DeclaredAt,
                        Version: Convert.ToBase64String(existing.RowVersion),
                        IsIdempotentReplay: true,
                        IsNew: false);
                }

                // Concurrency rowversion check
                if (command.RowVersion is not null && !existing.RowVersion.AsSpan().SequenceEqual(command.RowVersion))
                {
                    throw new StaleAvailabilityVersionException();
                }

                var previousResponse = existing.Response;
                existing.UpdateResponse(
                    newResponse: command.Response,
                    eventStartDateTime: eventItem.StartDateTime,
                    declaredAt: now,
                    isChiefLeaderOnEvent: isChiefLeader,
                    eventStatus: eventItem.Status.ToString());

                db.AuditLogs.Add(AuditLog.AvailabilityDeclared(
                    availabilityResponseId: existing.Id,
                    actorUserId: command.UserId,
                    correlationId: command.CorrelationId,
                    occurredAt: now,
                    previousValue: previousResponse.ToString(),
                    newValue: command.Response.ToString()));

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    throw new StaleAvailabilityVersionException("Optimistic concurrency conflict while updating availability.", ex);
                }

                return new AvailabilityDeclarationResult(
                    Id: existing.Id,
                    EventId: command.EventId,
                    StaffingCycleId: cycle.Id,
                    UserId: command.UserId,
                    Response: existing.Response,
                    DeclaredAt: existing.DeclaredAt,
                    Version: Convert.ToBase64String(existing.RowVersion),
                    IsIdempotentReplay: false,
                    IsNew: false);
            }
            else
            {
                var newRecord = AvailabilityResponse.Declare(
                    staffingCycleId: cycle.Id,
                    eventId: command.EventId,
                    userId: command.UserId,
                    response: command.Response,
                    eventStartDateTime: eventItem.StartDateTime,
                    declaredAt: now,
                    isChiefLeaderOnEvent: isChiefLeader,
                    eventStatus: eventItem.Status.ToString());

                db.AvailabilityResponses.Add(newRecord);

                db.AuditLogs.Add(AuditLog.AvailabilityDeclared(
                    availabilityResponseId: newRecord.Id,
                    actorUserId: command.UserId,
                    correlationId: command.CorrelationId,
                    occurredAt: now,
                    previousValue: null,
                    newValue: command.Response.ToString()));

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    throw new StaleAvailabilityVersionException("Optimistic concurrency conflict while creating availability.", ex);
                }

                return new AvailabilityDeclarationResult(
                    Id: newRecord.Id,
                    EventId: command.EventId,
                    StaffingCycleId: cycle.Id,
                    UserId: command.UserId,
                    Response: newRecord.Response,
                    DeclaredAt: newRecord.DeclaredAt,
                    Version: Convert.ToBase64String(newRecord.RowVersion),
                    IsIdempotentReplay: false,
                    IsNew: true);
            }
        });
    }
}
