using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Application;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfLeaderHistoryStore(
    CarpediemDbContext db,
    UserManager<CarpediemUser> userManager) : ILeaderHistoryStore
{
    public async Task<LeaderHistoryResult?> GetOwnAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await userManager.FindByIdAsync(actorUserId.ToString());
        if (actor is null || !actor.IsActive || !await userManager.IsInRoleAsync(actor, CarpediemRoles.Leader))
        {
            throw new UnauthorizedAccessException("A Leader account is required to view this profile.");
        }

        return await BuildHistoryAsync(actor, cancellationToken);
    }

    public async Task<LeaderHistoryResult?> GetLeaderAsync(
        Guid actorUserId,
        Guid leaderUserId,
        Guid? eventId,
        CancellationToken cancellationToken = default)
    {
        var actor = await userManager.FindByIdAsync(actorUserId.ToString());
        if (actor is null || !actor.IsActive)
        {
            throw new UnauthorizedAccessException("An active account is required to view Leader history.");
        }

        var isAdmin = await userManager.IsInRoleAsync(actor, CarpediemRoles.Admin);
        var currentCycleId = Guid.Empty;
        if (!isAdmin)
        {
            if (!await userManager.IsInRoleAsync(actor, CarpediemRoles.ChiefLeader) || eventId is null)
            {
                throw new UnauthorizedAccessException("An active Chief Leader event context is required.");
            }

            // Check event authority before looking up the requested person. A foreign Chief
            // Leader receives the same response regardless of whether the person exists.
            currentCycleId = await db.Events.AsNoTracking()
                .Where(e => e.Id == eventId && e.Status == EventStatus.Published)
                .Join(db.StaffingCycles.AsNoTracking().Where(c => c.IsCurrent),
                    e => e.Id, c => c.EventId, (e, c) => c.Id)
                .SingleOrDefaultAsync(cancellationToken);
            var leadsEvent = currentCycleId != Guid.Empty && await db.ChiefLeaderAssignments.AsNoTracking()
                .AnyAsync(a => a.EventId == eventId &&
                    a.ChiefLeaderUserId == actorUserId &&
                    a.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);
            if (!leadsEvent)
            {
                throw new UnauthorizedAccessException("An active Chief Leader event context is required.");
            }

            // Every active Leader is in the event's available, direct assignment, or
            // roster pool. The active Chief Leader is excluded by same-event exclusivity.
            var isActiveChiefTarget = await db.ChiefLeaderAssignments.AsNoTracking()
                .AnyAsync(a => a.EventId == eventId &&
                    a.ChiefLeaderUserId == leaderUserId &&
                    a.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);
            if (isActiveChiefTarget)
            {
                return null;
            }
        }

        var leader = await userManager.FindByIdAsync(leaderUserId.ToString());
        if (leader is null || !await userManager.IsInRoleAsync(leader, CarpediemRoles.Leader))
        {
            return null;
        }

        if (!isAdmin && !leader.IsActive && !await db.TeamAssignments.AsNoTracking()
            .AnyAsync(a => a.StaffingCycleId == currentCycleId &&
                a.UserId == leaderUserId && a.Status == TeamAssignmentStatus.Active, cancellationToken))
        {
            return null;
        }

        return await BuildHistoryAsync(leader, cancellationToken);
    }

    private async Task<LeaderHistoryResult> BuildHistoryAsync(
        CarpediemUser leader,
        CancellationToken cancellationToken)
    {
        var assignments = await (
            from assignment in db.TeamAssignments.AsNoTracking()
            where assignment.UserId == leader.Id
            join eventItem in db.Events.AsNoTracking() on assignment.EventId equals eventItem.Id
            join cycle in db.StaffingCycles.AsNoTracking() on assignment.StaffingCycleId equals cycle.Id
            join outcome in db.AttendanceOutcomes.AsNoTracking()
                on assignment.Id equals outcome.TeamAssignmentId into outcomeGroup
            from outcome in outcomeGroup.DefaultIfEmpty()
            orderby cycle.EffectiveEventDate descending, assignment.AssignedAt descending
            select new
            {
                assignment.Id,
                assignment.EventId,
                EventTitle = eventItem.Title,
                cycle.CycleNumber,
                cycle.EffectiveEventDate,
                cycle.IsCurrent,
                assignment.Status,
                assignment.AssignedAt,
                assignment.CancelledAt,
                Outcome = outcome == null ? (Carpediem.Modules.Attendance.Domain.AttendanceOutcomeValue?)null : outcome.Outcome,
            }).ToListAsync(cancellationToken);

        var assignmentIds = assignments.Select(a => a.Id).ToList();
        var selfCancelledIds = await db.AuditLogs.AsNoTracking()
            .Where(a => assignmentIds.Contains(a.ResourceId) &&
                a.ResourceType == "TeamAssignment" &&
                a.Action == "VolunteerSelfCancelled" &&
                a.Source == "VolunteerSelfCancellation")
            .Select(a => a.ResourceId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var selfCancelledSet = selfCancelledIds.ToHashSet();

        var entries = assignments.Select(a => new LeaderHistoryEntry(
            a.Id,
            a.EventId,
            a.EventTitle,
            a.CycleNumber,
            a.EffectiveEventDate,
            a.IsCurrent,
            a.Status.ToString(),
            a.AssignedAt,
            a.CancelledAt,
            a.Outcome?.ToString(),
            selfCancelledSet.Contains(a.Id))).ToList();

        return new LeaderHistoryResult(
            leader.Id,
            leader.UserName ?? "Unknown",
            entries.Count(e => e.AttendanceOutcome == "Attended"),
            entries.Count(e => e.AttendanceOutcome == "NoShow"),
            selfCancelledSet.Count,
            entries);
    }
}
