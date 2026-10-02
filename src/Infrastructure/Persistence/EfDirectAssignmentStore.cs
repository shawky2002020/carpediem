using System.Data;
using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Staffing.Application.DirectAssignment;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfDirectAssignmentStore(
    CarpediemDbContext db,
    IDirectLeaderAssignedNotificationWriter notificationWriter,
    UserManager<CarpediemUser> userManager) : IDirectAssignmentStore
{
    public async Task<DirectAssignmentCandidatesResult?> GetCandidatesAsync(
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

        var actingUser = await userManager.FindByIdAsync(actorUserId.ToString());
        var isAdmin = actingUser is not null && await userManager.IsInRoleAsync(actingUser, CarpediemRoles.Admin);

        var activeChief = await db.ChiefLeaderAssignments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.EventId == eventId && a.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);

        if (!isAdmin && (activeChief is null || activeChief.ChiefLeaderUserId != actorUserId))
        {
            throw new UnauthorizedAccessException("Only the active Chief Leader or an Admin can query direct assignment candidates.");
        }

        var cycle = await db.StaffingCycles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.EventId == eventId && c.IsCurrent, cancellationToken);

        if (cycle is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var isWindowOpen = eventItem.StartDateTime > now;

        var activeTeamUserIds = await db.TeamAssignments.AsNoTracking()
            .Where(t => t.StaffingCycleId == cycle.Id && t.Status == TeamAssignmentStatus.Active)
            .Select(t => t.UserId)
            .ToListAsync(cancellationToken);

        var excludedUserIds = activeTeamUserIds.ToHashSet();
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

        var candidateList = new List<DirectAssignmentCandidateResult>();
        foreach (var leader in eligibleLeaders)
        {
            if (responses.TryGetValue(leader.Id, out var response))
            {
                if (response.Response == AvailabilityResponseType.NotAvailable)
                {
                    candidateList.Add(new DirectAssignmentCandidateResult(
                        leader.Id,
                        leader.UserName!,
                        "NotAvailable",
                        response.DeclaredAt));
                }
                // If Available, they are in the available pool, not direct draft pool
            }
            else
            {
                candidateList.Add(new DirectAssignmentCandidateResult(
                    leader.Id,
                    leader.UserName!,
                    "NoResponse",
                    null));
            }
        }

        var sortedCandidates = candidateList
            .OrderBy(c => c.UserName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.UserId)
            .ToList();

        return new DirectAssignmentCandidatesResult(
            EventId: eventId,
            StaffingCycleId: cycle.Id,
            EventTitle: eventItem.Title,
            IsWindowOpen: isWindowOpen,
            Candidates: sortedCandidates);
    }

    public async Task<DirectAssignmentResult> DirectAssignLeaderAsync(
        Guid eventId,
        Guid candidateUserId,
        Guid actorUserId,
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
                throw new UnauthorizedAccessException("Only the active Chief Leader or an Admin can directly assign team members.");
            }

            // CHANGE-001 Same-Event Exclusivity
            if (activeChief is not null && activeChief.ChiefLeaderUserId == candidateUserId)
            {
                throw new ChiefLeaderExclusivityException(eventId, candidateUserId);
            }

            var candidateUser = await userManager.FindByIdAsync(candidateUserId.ToString());
            if (candidateUser is null || !candidateUser.IsActive || !await userManager.IsInRoleAsync(candidateUser, CarpediemRoles.Leader))
            {
                throw new KeyNotFoundException($"Candidate user {candidateUserId} was not found or is not eligible.");
            }

            var cycle = await db.StaffingCycles
                .SingleOrDefaultAsync(c => c.EventId == eventId && c.IsCurrent, cancellationToken)
                ?? throw new InvalidOperationException($"Event {eventId} has no active staffing cycle.");

            // Idempotent Replay Check
            var existingAssignment = await db.TeamAssignments
                .SingleOrDefaultAsync(t => t.StaffingCycleId == cycle.Id && t.UserId == candidateUserId && t.Status == TeamAssignmentStatus.Active, cancellationToken);

            if (existingAssignment is not null)
            {
                return new DirectAssignmentResult(
                    existingAssignment.Id,
                    existingAssignment.EventId,
                    existingAssignment.StaffingCycleId,
                    existingAssignment.UserId,
                    existingAssignment.Status.ToString(),
                    existingAssignment.AssignedAt,
                    existingAssignment.AssignedByUserId,
                    Convert.ToBase64String(existingAssignment.RowVersion),
                    IsIdempotentReplay: true);
            }

            var priorResponse = await db.AvailabilityResponses.AsNoTracking()
                .SingleOrDefaultAsync(a => a.StaffingCycleId == cycle.Id && a.UserId == candidateUserId, cancellationToken);

            var priorAvailability = priorResponse?.Response.ToString() ?? "NoResponse";

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            var teamAssignment = TeamAssignment.CreateDirectAssignment(
                cycle.Id,
                eventId,
                candidateUserId,
                actorUserId,
                eventItem.StartDateTime,
                now,
                isCandidateChiefLeaderOnEvent: false,
                eventStatus: eventItem.Status.ToString());

            db.TeamAssignments.Add(teamAssignment);

            var notification = notificationWriter.StageDirectLeaderAssigned(
                candidateUserId,
                eventId,
                eventItem.Title,
                now);
            db.InPlatformNotifications.Add(notification);

            var audit = AuditLog.DirectLeaderAssignmentExecuted(
                teamAssignment.Id,
                actorUserId,
                Guid.NewGuid(),
                now,
                priorAvailability,
                isAdminActor: isAdmin);
            db.AuditLogs.Add(audit);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new DirectAssignmentResult(
                teamAssignment.Id,
                teamAssignment.EventId,
                teamAssignment.StaffingCycleId,
                teamAssignment.UserId,
                teamAssignment.Status.ToString(),
                teamAssignment.AssignedAt,
                teamAssignment.AssignedByUserId,
                Convert.ToBase64String(teamAssignment.RowVersion),
                IsIdempotentReplay: false);
        });
    }
}
