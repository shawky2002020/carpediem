using System.Data;
using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Staffing.Application.TeamSelection;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfTeamSelectionStore(
    CarpediemDbContext db,
    IVolunteerSelectedNotificationWriter notificationWriter,
    UserManager<CarpediemUser> userManager) : ITeamSelectionStore
{
    public async Task<TeamSelectionContext?> GetTeamSelectionContextAsync(
        Guid eventId,
        Guid actingUserId,
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

        var actingUser = await userManager.FindByIdAsync(actingUserId.ToString());
        var isAdmin = actingUser is not null && await userManager.IsInRoleAsync(actingUser, CarpediemRoles.Admin);

        var activeChiefAssignment = await db.ChiefLeaderAssignments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.EventId == eventId && a.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);

        if (!isAdmin && (activeChiefAssignment is null || activeChiefAssignment.ChiefLeaderUserId != actingUserId))
        {
            throw new UnauthorizedAccessException("Only the active Chief Leader or an Admin can access team selection for this event.");
        }

        var cycle = await db.StaffingCycles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.EventId == eventId && c.IsCurrent, cancellationToken);

        if (cycle is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var isWindowOpen = eventItem.StartDateTime > now;

        var activeTeamList = await db.TeamAssignments.AsNoTracking()
            .Where(t => t.StaffingCycleId == cycle.Id && t.Status == TeamAssignmentStatus.Active)
            .Join(db.Users.AsNoTracking(), t => t.UserId, u => u.Id, (t, u) => new
            {
                Assignment = t,
                UserName = u.UserName ?? "Unknown"
            })
            .OrderBy(x => x.Assignment.AssignedAt)
            .ToListAsync(cancellationToken);

        var activeUserIds = activeTeamList.Select(x => x.Assignment.UserId).ToHashSet();
        if (activeChiefAssignment is not null)
        {
            activeUserIds.Add(activeChiefAssignment.ChiefLeaderUserId);
        }

        var availableResponses = await db.AvailabilityResponses.AsNoTracking()
            .Where(a => a.StaffingCycleId == cycle.Id && a.Response == AvailabilityResponseType.Available && !activeUserIds.Contains(a.UserId))
            .Join(db.Users.AsNoTracking(), a => a.UserId, u => u.Id, (a, u) => new
            {
                Response = a,
                UserName = u.UserName ?? "Unknown"
            })
            .OrderBy(x => x.Response.DeclaredAt)
            .ToListAsync(cancellationToken);

        var candidatesDto = availableResponses
            .Select(x => new AvailableCandidateDto(x.Response.UserId, x.UserName, x.Response.DeclaredAt))
            .ToList();

        var activeTeamDto = activeTeamList
            .Select(x => new ActiveTeamMemberDto(
                x.Assignment.Id,
                x.Assignment.UserId,
                x.UserName,
                x.Assignment.AssignedAt,
                Convert.ToBase64String(x.Assignment.RowVersion)))
            .ToList();

        return new TeamSelectionContext(
            EventId: eventId,
            StaffingCycleId: cycle.Id,
            EventTitle: eventItem.Title,
            StartDateTime: eventItem.StartDateTime,
            EndDateTime: eventItem.EndDateTime,
            Location: eventItem.Location,
            IsWindowOpen: isWindowOpen,
            AvailableCandidates: candidatesDto,
            ActiveTeam: activeTeamDto);
    }

    public async Task<SelectVolunteerResult> SelectVolunteerAsync(
        Guid eventId,
        Guid candidateUserId,
        Guid actingUserId,
        bool isAdminActor,
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

            if (!isAdminActor && (activeChief is null || activeChief.ChiefLeaderUserId != actingUserId))
            {
                throw new UnauthorizedAccessException("Only the active Chief Leader or an Admin can select team members.");
            }

            // CHANGE-001 Same-Event Exclusivity
            if (activeChief is not null && activeChief.ChiefLeaderUserId == candidateUserId)
            {
                throw new ChiefLeaderExclusivityException(eventId, candidateUserId);
            }

            var cycle = await db.StaffingCycles
                .SingleOrDefaultAsync(c => c.EventId == eventId && c.IsCurrent, cancellationToken)
                ?? throw new InvalidOperationException($"Event {eventId} has no active staffing cycle.");

            // Idempotent Replay Check
            var existingAssignment = await db.TeamAssignments
                .SingleOrDefaultAsync(t => t.StaffingCycleId == cycle.Id && t.UserId == candidateUserId && t.Status == TeamAssignmentStatus.Active, cancellationToken);

            if (existingAssignment is not null)
            {
                return new SelectVolunteerResult(
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

            // Candidate Availability Check
            var availability = await db.AvailabilityResponses.AsNoTracking()
                .SingleOrDefaultAsync(a => a.StaffingCycleId == cycle.Id && a.UserId == candidateUserId, cancellationToken);

            if (availability is null || availability.Response != AvailabilityResponseType.Available)
            {
                throw new CandidateNotAvailableException(eventId, candidateUserId);
            }

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            var teamAssignment = TeamAssignment.Create(
                cycle.Id,
                eventId,
                candidateUserId,
                actingUserId,
                eventItem.StartDateTime,
                now,
                availability.Response.ToString(),
                isCandidateChiefLeaderOnEvent: false,
                eventStatus: eventItem.Status.ToString());

            db.TeamAssignments.Add(teamAssignment);

            var notification = notificationWriter.StageVolunteerSelected(
                candidateUserId,
                eventId,
                eventItem.Title,
                now);
            db.InPlatformNotifications.Add(notification);

            var audit = AuditLog.VolunteerSelected(
                teamAssignment.Id,
                actingUserId,
                Guid.NewGuid(),
                now,
                isAdminActor: isAdminActor);
            db.AuditLogs.Add(audit);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new SelectVolunteerResult(
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
