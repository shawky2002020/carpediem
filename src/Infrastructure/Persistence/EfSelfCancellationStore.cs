using System.Data;
using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Staffing.Application.SelfCancellation;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfSelfCancellationStore(
    CarpediemDbContext db,
    ITeamMemberCancelledNotificationWriter notificationWriter,
    UserManager<CarpediemUser> userManager) : ISelfCancellationStore
{
    public async Task<MyAssignmentContextResult?> GetMyAssignmentAsync(
        Guid eventId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await db.Events.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (eventItem is null)
        {
            return null;
        }

        if (eventItem.Status != EventStatus.Published)
        {
            throw new InvalidEventStatusException(eventId, eventItem.Status.ToString());
        }

        var cycle = await db.StaffingCycles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.EventId == eventId && c.IsCurrent, cancellationToken);

        if (cycle is null)
        {
            return null;
        }

        var assignment = await db.TeamAssignments.AsNoTracking()
            .Where(t => t.StaffingCycleId == cycle.Id && t.UserId == actorUserId)
            .OrderByDescending(t => t.AssignedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment is null)
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(actorUserId.ToString());
        var userName = user?.UserName ?? "Volunteer";
        var now = DateTimeOffset.UtcNow;
        var isWindowOpen = eventItem.StartDateTime > now;

        return new MyAssignmentContextResult(
            AssignmentId: assignment.Id,
            EventId: eventItem.Id,
            StaffingCycleId: cycle.Id,
            UserId: assignment.UserId,
            UserName: userName,
            EventTitle: eventItem.Title,
            StartDateTime: eventItem.StartDateTime,
            EndDateTime: eventItem.EndDateTime,
            Location: eventItem.Location,
            Status: assignment.Status.ToString(),
            AssignedAt: assignment.AssignedAt,
            CancelledAt: assignment.CancelledAt,
            CancellationReason: assignment.CancellationReason,
            IsWindowOpen: isWindowOpen,
            Version: Convert.ToBase64String(assignment.RowVersion));
    }

    public async Task<SelfCancellationResult> CancelAssignmentAsync(
        Guid eventId,
        Guid assignmentId,
        Guid actorUserId,
        string justification,
        byte[]? expectedRowVersion,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(justification))
        {
            throw new ArgumentException("A non-empty justification is required for cancellation.", nameof(justification));
        }

        var trimmed = justification.Trim();
        if (trimmed.Length > 500)
        {
            throw new ArgumentException("Cancellation justification cannot exceed 500 characters.", nameof(justification));
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();

            var eventItem = await db.Events
                .SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken)
                ?? throw new KeyNotFoundException($"Event {eventId} was not found.");

            if (eventItem.Status != EventStatus.Published)
            {
                throw new InvalidEventStatusException(eventId, eventItem.Status.ToString());
            }

            var now = DateTimeOffset.UtcNow;
            if (eventItem.StartDateTime <= now)
            {
                throw new CancellationWindowClosedException(eventId);
            }

            var assignment = await db.TeamAssignments
                .SingleOrDefaultAsync(t => t.Id == assignmentId && t.EventId == eventId, cancellationToken)
                ?? throw new KeyNotFoundException($"Assignment {assignmentId} was not found on event {eventId}.");

            if (!isAdmin && assignment.UserId != actorUserId)
            {
                throw new UnauthorizedAccessException("Only the assigned leader or an Admin can cancel this assignment.");
            }

            // Idempotent Replay Check
            if (assignment.Status == TeamAssignmentStatus.Cancelled)
            {
                if (string.Equals(assignment.CancellationReason, trimmed, StringComparison.Ordinal))
                {
                    return new SelfCancellationResult(
                        assignment.Id,
                        assignment.EventId,
                        assignment.StaffingCycleId,
                        assignment.UserId,
                        assignment.Status.ToString(),
                        assignment.AssignedAt,
                        assignment.AssignedByUserId,
                        assignment.CancellationReason!,
                        assignment.CancelledAt!.Value,
                        Convert.ToBase64String(assignment.RowVersion),
                        IsIdempotentReplay: true);
                }

                throw new AssignmentAlreadyCancelledException(assignment.Id);
            }

            // Optimistic concurrency check
            if (expectedRowVersion is not null && expectedRowVersion.Length > 0)
            {
                if (!assignment.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
                {
                    throw new DbUpdateConcurrencyException("The assignment has been modified concurrently.");
                }
            }

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            assignment.Cancel(trimmed, now, eventItem.StartDateTime);

            // Fetch volunteer username
            var volunteer = await userManager.FindByIdAsync(assignment.UserId.ToString());
            var volunteerName = volunteer?.UserName ?? "Volunteer";

            // Stage notification for active Chief Leader
            var activeChief = await db.ChiefLeaderAssignments.AsNoTracking()
                .SingleOrDefaultAsync(a => a.EventId == eventId && a.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);

            if (activeChief is not null)
            {
                var chiefNotif = notificationWriter.StageTeamMemberCancelled(
                    activeChief.ChiefLeaderUserId,
                    eventId,
                    eventItem.Title,
                    volunteerName,
                    trimmed,
                    now);
                db.InPlatformNotifications.Add(chiefNotif);
            }

            // Stage notifications for active Admins
            var admins = await userManager.GetUsersInRoleAsync(CarpediemRoles.Admin);
            foreach (var admin in admins.Where(a => a.IsActive))
            {
                if (activeChief is null || admin.Id != activeChief.ChiefLeaderUserId)
                {
                    var adminNotif = notificationWriter.StageTeamMemberCancelled(
                        admin.Id,
                        eventId,
                        eventItem.Title,
                        volunteerName,
                        trimmed,
                        now);
                    db.InPlatformNotifications.Add(adminNotif);
                }
            }

            // Stage audit log
            var audit = AuditLog.VolunteerSelfCancelled(
                assignment.Id,
                actorUserId,
                Guid.NewGuid(),
                now,
                trimmed,
                isAdminActor: isAdmin);
            db.AuditLogs.Add(audit);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new SelfCancellationResult(
                assignment.Id,
                assignment.EventId,
                assignment.StaffingCycleId,
                assignment.UserId,
                assignment.Status.ToString(),
                assignment.AssignedAt,
                assignment.AssignedByUserId,
                assignment.CancellationReason!,
                assignment.CancelledAt!.Value,
                Convert.ToBase64String(assignment.RowVersion),
                IsIdempotentReplay: false);
        });
    }
}
