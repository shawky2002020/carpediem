using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Staffing.Application.ReplacementAssignment;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfReplacementAssignmentStore(
    CarpediemDbContext db,
    IReplacementLeaderAssignedNotificationWriter notificationWriter,
    UserManager<CarpediemUser> userManager) : IReplacementAssignmentStore
{
    public async Task<ReplacementAssignmentContextResult> GetReplacementContextAsync(
        Guid eventId,
        Guid actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await db.Events.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken)
            ?? throw new KeyNotFoundException($"Event {eventId} was not found.");

        if (eventItem.Status != EventStatus.Published)
        {
            throw new InvalidEventStatusException(eventId, eventItem.Status.ToString());
        }

        var activeChief = await db.ChiefLeaderAssignments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.EventId == eventId && a.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);

        if (!isAdmin && (activeChief is null || activeChief.ChiefLeaderUserId != actorUserId))
        {
            throw new UnauthorizedAccessException("Only the active Chief Leader or an Admin can query replacement context.");
        }

        var cycle = await db.StaffingCycles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.EventId == eventId && c.IsCurrent, cancellationToken)
            ?? throw new KeyNotFoundException($"Active staffing cycle for event {eventId} was not found.");

        var now = DateTimeOffset.UtcNow;
        var isWindowOpen = eventItem.StartDateTime > now;

        var assignments = await db.TeamAssignments.AsNoTracking()
            .Where(t => t.EventId == eventId && t.StaffingCycleId == cycle.Id)
            .ToListAsync(cancellationToken);

        var assignmentUserIds = assignments.Select(t => t.UserId).Distinct().ToList();
        var assignedUsers = await db.Users.AsNoTracking()
            .Where(u => assignmentUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? string.Empty, cancellationToken);

        var activeTeam = assignments
            .Where(t => t.Status == TeamAssignmentStatus.Active)
            .OrderBy(t => t.AssignedAt)
            .Select(t => new ReplacementTeamMemberItem(
                t.Id,
                t.UserId,
                assignedUsers.GetValueOrDefault(t.UserId, string.Empty),
                t.Status.ToString(),
                t.AssignedAt,
                Convert.ToBase64String(t.RowVersion)))
            .ToList();

        var cancelledAssignments = assignments
            .Where(t => t.Status == TeamAssignmentStatus.Cancelled)
            .OrderBy(t => t.CancelledAt ?? t.AssignedAt)
            .Select(t => new ReplacementCancelledMemberItem(
                t.Id,
                t.UserId,
                assignedUsers.GetValueOrDefault(t.UserId, string.Empty),
                t.Status.ToString(),
                t.CancelledAt ?? t.AssignedAt,
                t.CancellationReason ?? string.Empty))
            .ToList();

        var activeTeamUserIds = activeTeam.Select(t => t.UserId).ToHashSet();
        var excludedUserIds = new HashSet<Guid>(activeTeamUserIds);
        if (activeChief is not null)
        {
            excludedUserIds.Add(activeChief.ChiefLeaderUserId);
        }

        var leaders = await userManager.GetUsersInRoleAsync(CarpediemRoles.Leader);
        var eligibleLeaders = leaders
            .Where(l => l.IsActive && !string.IsNullOrWhiteSpace(l.UserName) && !excludedUserIds.Contains(l.Id))
            .ToList();

        var eligibleLeaderIds = eligibleLeaders.Select(l => l.Id).ToList();
        var responses = await db.AvailabilityResponses.AsNoTracking()
            .Where(a => a.StaffingCycleId == cycle.Id && eligibleLeaderIds.Contains(a.UserId))
            .ToDictionaryAsync(a => a.UserId, cancellationToken);

        var candidateList = new List<ReplacementCandidateItem>();
        foreach (var leader in eligibleLeaders)
        {
            if (responses.TryGetValue(leader.Id, out var response))
            {
                candidateList.Add(new ReplacementCandidateItem(
                    leader.Id,
                    leader.UserName!,
                    response.Response.ToString(),
                    response.DeclaredAt));
            }
            else
            {
                candidateList.Add(new ReplacementCandidateItem(
                    leader.Id,
                    leader.UserName!,
                    "NoResponse",
                    null));
            }
        }

        var sortedCandidates = candidateList
            .OrderByDescending(c => c.AvailabilityStatus == "Available")
            .ThenBy(c => c.UserName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.UserId)
            .ToList();

        return new ReplacementAssignmentContextResult(
            EventId: eventId,
            EventTitle: eventItem.Title,
            StaffingCycleId: cycle.Id,
            StartDateTime: eventItem.StartDateTime,
            EndDateTime: eventItem.EndDateTime,
            Location: eventItem.Location,
            IsWindowOpen: isWindowOpen,
            ActiveTeam: activeTeam,
            CancelledAssignments: cancelledAssignments,
            Candidates: sortedCandidates);
    }

    public async Task<AssignReplacementResult> AssignReplacementLeaderAsync(
        Guid eventId,
        Guid candidateUserId,
        Guid actorUserId,
        Guid? replacedAssignmentId,
        string? justification,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
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
                throw new TeamSelectionWindowClosedException(eventId);
            }

            var activeChief = await db.ChiefLeaderAssignments.AsNoTracking()
                .SingleOrDefaultAsync(a => a.EventId == eventId && a.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);

            if (!isAdmin && (activeChief is null || activeChief.ChiefLeaderUserId != actorUserId))
            {
                throw new UnauthorizedAccessException("Only the active Chief Leader or an Admin can assign replacement leaders.");
            }

            if (activeChief is not null && activeChief.ChiefLeaderUserId == candidateUserId)
            {
                throw new ChiefLeaderExclusivityException(eventId, candidateUserId);
            }

            var cycle = await db.StaffingCycles
                .SingleOrDefaultAsync(c => c.EventId == eventId && c.IsCurrent, cancellationToken)
                ?? throw new KeyNotFoundException($"Active staffing cycle for event {eventId} was not found.");

            var existingActive = await db.TeamAssignments.AsNoTracking()
                .FirstOrDefaultAsync(t => t.StaffingCycleId == cycle.Id && t.UserId == candidateUserId && t.Status == TeamAssignmentStatus.Active, cancellationToken);

            if (existingActive is not null)
            {
                return new AssignReplacementResult(
                    Id: existingActive.Id,
                    EventId: existingActive.EventId,
                    StaffingCycleId: existingActive.StaffingCycleId,
                    UserId: existingActive.UserId,
                    Status: existingActive.Status.ToString(),
                    AssignedAt: existingActive.AssignedAt,
                    AssignedByUserId: existingActive.AssignedByUserId,
                    Version: Convert.ToBase64String(existingActive.RowVersion),
                    IsIdempotentReplay: true);
            }

            if (replacedAssignmentId.HasValue)
            {
                var replaced = await db.TeamAssignments.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == replacedAssignmentId.Value && t.EventId == eventId, cancellationToken);

                if (replaced is null)
                {
                    throw new KeyNotFoundException($"Replaced assignment {replacedAssignmentId.Value} was not found on event {eventId}.");
                }

                if (replaced.Status != TeamAssignmentStatus.Cancelled)
                {
                    throw new InvalidOperationException($"Replaced assignment {replacedAssignmentId.Value} is not in Cancelled status.");
                }
            }

            var candidateUser = await userManager.FindByIdAsync(candidateUserId.ToString());
            if (candidateUser is null || !candidateUser.IsActive || !await userManager.IsInRoleAsync(candidateUser, CarpediemRoles.Leader))
            {
                throw new InvalidOperationException($"Candidate {candidateUserId} is not an active volunteer leader.");
            }

            var newAssignment = TeamAssignment.CreateReplacementAssignment(
                staffingCycleId: cycle.Id,
                eventId: eventId,
                candidateUserId: candidateUserId,
                assignedByUserId: actorUserId,
                eventStartUtc: eventItem.StartDateTime,
                assignedAtUtc: now,
                isCandidateChiefLeaderOnEvent: false,
                eventStatus: eventItem.Status.ToString());

            db.TeamAssignments.Add(newAssignment);

            var notification = notificationWriter.StageReplacementLeaderAssigned(
                candidateUserId,
                eventId,
                eventItem.Title,
                now);
            db.InPlatformNotifications.Add(notification);

            var audit = AuditLog.ReplacementLeaderAssigned(
                teamAssignmentId: newAssignment.Id,
                actorUserId: actorUserId,
                correlationId: Guid.NewGuid(),
                occurredAt: now,
                replacedAssignmentId: replacedAssignmentId,
                mandatoryReason: justification,
                isAdminActor: isAdmin);
            db.AuditLogs.Add(audit);

            await db.SaveChangesAsync(cancellationToken);

            return new AssignReplacementResult(
                Id: newAssignment.Id,
                EventId: newAssignment.EventId,
                StaffingCycleId: newAssignment.StaffingCycleId,
                UserId: newAssignment.UserId,
                Status: newAssignment.Status.ToString(),
                AssignedAt: newAssignment.AssignedAt,
                AssignedByUserId: newAssignment.AssignedByUserId,
                Version: Convert.ToBase64String(newAssignment.RowVersion),
                IsIdempotentReplay: false);
        });
    }
}
