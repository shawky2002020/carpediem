using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Application.Reschedule;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfEventRescheduleStore(
    CarpediemDbContext db,
    IEventRescheduledNotificationWriter notificationWriter,
    UserManager<CarpediemUser> userManager) : IEventRescheduleStore
{
    public async Task<RescheduleEventResult> RescheduleEventAsync(
        Guid eventId,
        DateTimeOffset newStartDateTime,
        DateTimeOffset newEndDateTime,
        string justification,
        byte[] expectedVersion,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();

            var actorUser = await userManager.FindByIdAsync(actorUserId.ToString());
            if (actorUser is null || !actorUser.IsActive || !await userManager.IsInRoleAsync(actorUser, CarpediemRoles.Admin))
            {
                throw new UnauthorizedAccessException("Only Platform Administrators can reschedule events.");
            }

            if (string.IsNullOrWhiteSpace(justification) || justification.Trim().Length < 10 || justification.Trim().Length > 500)
            {
                throw new ArgumentException("Administrative justification must be between 10 and 500 characters.", nameof(justification));
            }

            var trimmedJustification = justification.Trim();
            var now = DateTimeOffset.UtcNow;

            if (newStartDateTime <= now)
            {
                throw new ArgumentException("New start time must be in the future.", nameof(newStartDateTime));
            }

            if (newEndDateTime <= newStartDateTime)
            {
                throw new ArgumentException("New end time must be after the new start time.", nameof(newEndDateTime));
            }

            var eventItem = await db.Events
                .Include(e => e.StaffingCycles)
                .SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken)
                ?? throw new KeyNotFoundException($"Event {eventId} was not found.");

            if (!eventItem.RowVersion.AsSpan().SequenceEqual(expectedVersion))
            {
                throw new DbUpdateConcurrencyException("Concurrency mismatch on event.");
            }

            if (eventItem.Status != EventStatus.Published)
            {
                throw new InvalidEventStatusForRescheduleException(eventItem.Id, eventItem.Status.ToString());
            }

            if (eventItem.StartDateTime <= now)
            {
                throw new EventRescheduleWindowClosedException(eventItem.Id);
            }

            var previousStart = eventItem.StartDateTime;
            var previousEnd = eventItem.EndDateTime;

            var currentCycle = eventItem.StaffingCycles.Single(c => c.IsCurrent);

            // Collect distinct stakeholder recipient IDs from the current (sealed) cycle
            var activeTeamUserIds = await db.TeamAssignments.AsNoTracking()
                .Where(a => a.StaffingCycleId == currentCycle.Id && a.Status == TeamAssignmentStatus.Active)
                .Select(a => a.UserId)
                .ToListAsync(cancellationToken);

            var activeChiefLeaderUserIds = await db.ChiefLeaderAssignments.AsNoTracking()
                .Where(a => a.EventId == eventId && a.Status == ChiefLeaderAssignmentStatus.Active)
                .Select(a => a.ChiefLeaderUserId)
                .ToListAsync(cancellationToken);

            var candidateUserIds = await db.AvailabilityResponses.AsNoTracking()
                .Where(a => a.StaffingCycleId == currentCycle.Id)
                .Select(a => a.UserId)
                .ToListAsync(cancellationToken);

            var distinctRecipientIds = activeTeamUserIds
                .Concat(activeChiefLeaderUserIds)
                .Concat(candidateUserIds)
                .Distinct()
                .ToList();

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var nextCycle = eventItem.Reschedule(newStartDateTime, newEndDateTime, actorUserId, now);

            db.Entry(eventItem).Property(item => item.RowVersion).OriginalValue = expectedVersion;

            foreach (var recipientId in distinctRecipientIds)
            {
                var notification = notificationWriter.StageEventRescheduled(
                    recipientId,
                    eventId,
                    eventItem.Title,
                    previousStart,
                    previousEnd,
                    newStartDateTime,
                    newEndDateTime,
                    trimmedJustification,
                    now);

                db.InPlatformNotifications.Add(notification);
            }

            var audit = AuditLog.EventRescheduled(
                eventId,
                actorUserId,
                correlationId,
                now,
                previousStart,
                previousEnd,
                newStartDateTime,
                newEndDateTime,
                trimmedJustification);

            db.AuditLogs.Add(audit);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new RescheduleEventResult(
                eventItem.Id,
                eventItem.Title,
                eventItem.Status.ToString(),
                eventItem.StartDateTime,
                eventItem.EndDateTime,
                eventItem.Location,
                eventItem.Notes,
                nextCycle.Id,
                nextCycle.CycleNumber,
                eventItem.RowVersion,
                now,
                distinctRecipientIds.Count);
        });
    }
}
