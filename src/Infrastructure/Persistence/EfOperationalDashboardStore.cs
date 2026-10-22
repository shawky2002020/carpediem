using Carpediem.Modules.Events.Application.Dashboard;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfOperationalDashboardStore(
    CarpediemDbContext db,
    UserManager<CarpediemUser> userManager) : IOperationalDashboardStore
{
    public async Task<IReadOnlyList<OperationalEvent>> GetAsync(
        Guid actorUserId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var actor = await userManager.FindByIdAsync(actorUserId.ToString());
        if (actor is null || !actor.IsActive)
        {
            throw new UnauthorizedAccessException("An active account is required to view the operational dashboard.");
        }

        var isAdmin = await userManager.IsInRoleAsync(actor, CarpediemRoles.Admin);
        if (!isAdmin && !await userManager.IsInRoleAsync(actor, CarpediemRoles.ChiefLeader))
        {
            throw new UnauthorizedAccessException("Only Platform Administrators and Chief Leaders can view the operational dashboard.");
        }

        var eventsQuery = db.Events.AsNoTracking();
        if (!isAdmin)
        {
            var activeChiefEventIds = db.ChiefLeaderAssignments.AsNoTracking()
                .Where(a => a.ChiefLeaderUserId == actorUserId && a.Status == ChiefLeaderAssignmentStatus.Active)
                .Select(a => a.EventId);
            eventsQuery = eventsQuery.Where(e => activeChiefEventIds.Contains(e.Id));
        }

        var rawEvents = await (
            from eventItem in eventsQuery
            join cycle in db.StaffingCycles.AsNoTracking().Where(c => c.IsCurrent)
                on eventItem.Id equals cycle.EventId
            orderby eventItem.StartDateTime ascending, eventItem.CreatedAt ascending
            select new
            {
                eventItem.Id,
                eventItem.Title,
                eventItem.Status,
                eventItem.StartDateTime,
                eventItem.EndDateTime,
                eventItem.Location,
                CurrentCycleId = cycle.Id,
                CurrentCycleNumber = cycle.CycleNumber,
                cycle.EffectiveEventDate,
            }).ToListAsync(cancellationToken);

        if (rawEvents.Count == 0)
        {
            return [];
        }

        var eventIds = rawEvents.Select(e => e.Id).ToList();
        var currentCycleIds = rawEvents.Select(e => e.CurrentCycleId).ToList();

        var activeChiefs = await (
            from assignment in db.ChiefLeaderAssignments.AsNoTracking()
            where eventIds.Contains(assignment.EventId) && assignment.Status == ChiefLeaderAssignmentStatus.Active
            join user in db.Users.AsNoTracking() on assignment.ChiefLeaderUserId equals user.Id
            select new
            {
                assignment.EventId,
                assignment.ChiefLeaderUserId,
                UserName = user.UserName ?? "Unknown",
            }).ToDictionaryAsync(x => x.EventId, cancellationToken);

        var activeAssignments = await db.TeamAssignments.AsNoTracking()
            .Where(t => currentCycleIds.Contains(t.StaffingCycleId) && t.Status == TeamAssignmentStatus.Active)
            .Select(t => new { t.Id, t.StaffingCycleId, t.UserId })
            .ToListAsync(cancellationToken);

        var activeAssignmentIds = activeAssignments.Select(a => a.Id).ToList();
        var recordedAssignmentIds = activeAssignmentIds.Count == 0
            ? []
            : (await db.AttendanceOutcomes.AsNoTracking()
                .Where(o => activeAssignmentIds.Contains(o.TeamAssignmentId))
                .Select(o => o.TeamAssignmentId)
                .ToListAsync(cancellationToken)).ToHashSet();

        var availableResponses = await db.AvailabilityResponses.AsNoTracking()
            .Where(r => currentCycleIds.Contains(r.StaffingCycleId) && r.Response == AvailabilityResponseType.Available)
            .Select(r => new { r.StaffingCycleId, r.UserId })
            .ToListAsync(cancellationToken);

        var assignmentsByCycle = activeAssignments.ToLookup(a => a.StaffingCycleId);
        var availableByCycle = availableResponses.ToLookup(r => r.StaffingCycleId);

        var items = rawEvents.Select(eventItem =>
        {
            activeChiefs.TryGetValue(eventItem.Id, out var activeChief);
            var cycleAssignments = assignmentsByCycle[eventItem.CurrentCycleId].ToList();
            var activeUserIds = cycleAssignments.Select(a => a.UserId).ToHashSet();
            if (activeChief is not null)
            {
                activeUserIds.Add(activeChief.ChiefLeaderUserId);
            }

            var activeRosterCount = cycleAssignments.Count;
            var unrecordedAttendanceCount = cycleAssignments.Count(a => !recordedAssignmentIds.Contains(a.Id));
            var availableUnassignedCount = availableByCycle[eventItem.CurrentCycleId]
                .Count(r => !activeUserIds.Contains(r.UserId));

            var isInProgress = eventItem.Status == EventStatus.Published &&
                eventItem.StartDateTime <= utcNow &&
                utcNow <= eventItem.EndDateTime;

            var isPendingAttendance = eventItem.Status == EventStatus.Completed &&
                unrecordedAttendanceCount > 0;

            return new OperationalEvent(
                EventId: eventItem.Id,
                Title: eventItem.Title,
                Status: eventItem.Status.ToString(),
                StartDateTime: eventItem.StartDateTime,
                EndDateTime: eventItem.EndDateTime,
                Location: eventItem.Location,
                InProgress: isInProgress,
                PendingAttendance: isPendingAttendance,
                ActiveRosterCount: activeRosterCount,
                AvailableUnassignedCount: availableUnassignedCount);
        }).ToList();

        return items;
    }
}
