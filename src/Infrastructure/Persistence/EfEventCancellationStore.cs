using System.Text.Json;
using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Application.Cancellation;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Notifications.Domain;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfEventCancellationStore(
    CarpediemDbContext db, UserManager<CarpediemUser> userManager) : IEventCancellationStore
{
    public async Task<EventCancellationContext?> GetContextAsync(
        Guid eventId, Guid actorUserId, CancellationToken cancellationToken)
    {
        await RequireAdminAsync(actorUserId);
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        return item is null ? null : new EventCancellationContext(
            item.Id, item.Title, item.Status.ToString(), Convert.ToBase64String(item.RowVersion));
    }

    public async Task<EventCancellationResult> CancelAsync(
        Guid eventId, Guid actorUserId, byte[] expectedVersion,
        string reason, Guid correlationId, DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        await RequireAdminAsync(actorUserId);
        var trimmed = reason.Trim();
        if (trimmed.Length is < 10 or > 500)
            throw new ArgumentException("Cancellation justification must be 10 to 500 characters.", nameof(reason));

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var item = await db.Events.Include(e => e.StaffingCycles)
                .SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken)
                ?? throw new KeyNotFoundException("Event not found.");
            if (!item.RowVersion.AsSpan().SequenceEqual(expectedVersion))
                throw new DbUpdateConcurrencyException("The Event was modified concurrently.");
            var previousStatus = item.Status;
            if (previousStatus is not (EventStatus.Draft or EventStatus.Published))
                throw new InvalidOperationException("Only Draft or Published Events can be cancelled.");

            var assignments = new List<TeamAssignment>();
            var recipients = new HashSet<Guid>();
            if (previousStatus == EventStatus.Published)
            {
                var cycle = item.StaffingCycles.Single(c => c.IsCurrent);
                assignments = await db.TeamAssignments
                    .Where(a => a.StaffingCycleId == cycle.Id && a.Status == TeamAssignmentStatus.Active)
                    .ToListAsync(cancellationToken);
                foreach (var assignment in assignments) recipients.Add(assignment.UserId);
                var chiefIds = await db.ChiefLeaderAssignments.AsNoTracking()
                    .Where(a => a.EventId == eventId && a.Status == ChiefLeaderAssignmentStatus.Active)
                    .Select(a => a.ChiefLeaderUserId).ToListAsync(cancellationToken);
                foreach (var chiefId in chiefIds) recipients.Add(chiefId);
            }

            item.Cancel(actorUserId, utcNow);
            db.Entry(item).Property(e => e.RowVersion).OriginalValue = expectedVersion;
            foreach (var assignment in assignments) assignment.CancelForEvent(trimmed, utcNow);
            db.AuditLogs.Add(AuditLog.EventCancelled(eventId, actorUserId,
                correlationId, utcNow, previousStatus.ToString(), trimmed));

            var payload = JsonSerializer.Serialize(new { eventTitle = item.Title, reason = trimmed });
            if (payload.Length > 500) payload = JsonSerializer.Serialize(new { eventId });
            foreach (var recipient in recipients)
                db.InPlatformNotifications.Add(InPlatformNotification.EventCancelled(recipient, eventId, utcNow, payload));

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new EventCancellationResult(eventId, "Cancelled", assignments.Count, recipients.Count);
        });
    }

    private async Task RequireAdminAsync(Guid actorUserId)
    {
        var actor = await userManager.FindByIdAsync(actorUserId.ToString());
        if (actor is null || !actor.IsActive || !await userManager.IsInRoleAsync(actor, CarpediemRoles.Admin))
            throw new UnauthorizedAccessException("Only an active Admin can cancel Events.");
    }
}
