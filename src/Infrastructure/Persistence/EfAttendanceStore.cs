using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Attendance.Application;
using Carpediem.Modules.Attendance.Domain;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfAttendanceStore(
    CarpediemDbContext db,
    UserManager<CarpediemUser> userManager) : IAttendanceStore
{
    public async Task<AttendanceRollSheetResult?> GetRollSheetAsync(
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

        if (eventItem.Status != EventStatus.Published && eventItem.Status != EventStatus.Completed)
        {
            throw new InvalidEventStatusException(eventId, eventItem.Status.ToString());
        }

        var actorUser = await userManager.FindByIdAsync(actorUserId.ToString());
        var isAdmin = actorUser is not null && actorUser.IsActive && await userManager.IsInRoleAsync(actorUser, CarpediemRoles.Admin);
        var isActiveChief = await db.ChiefLeaderAssignments.AsNoTracking().AnyAsync(a =>
            a.EventId == eventId && a.ChiefLeaderUserId == actorUserId && a.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);

        if (!isAdmin && !isActiveChief)
        {
            throw new UnauthorizedAccessException("Only the active Chief Leader or an Admin can access attendance for this event.");
        }

        var cycle = await db.StaffingCycles.AsNoTracking()
            .SingleOrDefaultAsync(c => c.EventId == eventId && c.IsCurrent, cancellationToken);

        if (cycle is null)
        {
            return new AttendanceRollSheetResult(
                EventId: eventItem.Id,
                EventTitle: eventItem.Title,
                Status: eventItem.Status.ToString(),
                StartDateTime: eventItem.StartDateTime,
                EndDateTime: eventItem.EndDateTime,
                Location: eventItem.Location,
                StaffingCycleId: Guid.Empty,
                IsCompleted: eventItem.Status == EventStatus.Completed,
                IsAllFinalized: false,
                Entries: []);
        }

        var activeAssignments = await db.TeamAssignments.AsNoTracking()
            .Where(t => t.EventId == eventId && t.StaffingCycleId == cycle.Id && t.Status == TeamAssignmentStatus.Active)
            .Join(db.Users.AsNoTracking(), t => t.UserId, u => u.Id, (t, u) => new
            {
                Assignment = t,
                UserName = u.UserName ?? "Unknown"
            })
            .OrderBy(x => x.Assignment.AssignedAt)
            .ToListAsync(cancellationToken);

        var assignmentIds = activeAssignments.Select(x => x.Assignment.Id).ToList();
        var outcomes = await db.AttendanceOutcomes.AsNoTracking()
            .Where(o => assignmentIds.Contains(o.TeamAssignmentId))
            .ToDictionaryAsync(o => o.TeamAssignmentId, cancellationToken);

        var entries = activeAssignments.Select(x =>
        {
            outcomes.TryGetValue(x.Assignment.Id, out var outcome);
            return new AttendanceRollSheetEntryResult(
                TeamAssignmentId: x.Assignment.Id,
                VolunteerUserId: x.Assignment.UserId,
                VolunteerName: x.UserName,
                Outcome: outcome?.Outcome.ToString(),
                FinalizedAt: outcome?.FinalizedAt,
                RowVersion: outcome?.RowVersion);
        }).ToList();

        var isAllFinalized = entries.Count > 0 && entries.All(e => e.Outcome != null);

        return new AttendanceRollSheetResult(
            EventId: eventItem.Id,
            EventTitle: eventItem.Title,
            Status: eventItem.Status.ToString(),
            StartDateTime: eventItem.StartDateTime,
            EndDateTime: eventItem.EndDateTime,
            Location: eventItem.Location,
            StaffingCycleId: cycle.Id,
            IsCompleted: eventItem.Status == EventStatus.Completed,
            IsAllFinalized: isAllFinalized,
            Entries: entries);
    }

    public async Task<FinalizeAttendanceResult> FinalizeAttendanceAsync(
        Guid eventId,
        IReadOnlyList<FinalizeAttendanceItem> submissions,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();

            var eventItem = await db.Events
                .SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken)
                ?? throw new KeyNotFoundException($"Event {eventId} was not found.");

            if (eventItem.Status != EventStatus.Published && eventItem.Status != EventStatus.Completed)
            {
                throw new InvalidEventStatusException(eventId, eventItem.Status.ToString());
            }

            var actorUser = await userManager.FindByIdAsync(actorUserId.ToString());
            var isAdmin = actorUser is not null && actorUser.IsActive && await userManager.IsInRoleAsync(actorUser, CarpediemRoles.Admin);
            var isActiveChief = await db.ChiefLeaderAssignments.AsNoTracking().AnyAsync(a =>
                a.EventId == eventId && a.ChiefLeaderUserId == actorUserId && a.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);

            if (!isAdmin && !isActiveChief)
            {
                throw new UnauthorizedAccessException("Only the active Chief Leader or an Admin can finalize attendance for this event.");
            }

            var cycle = await db.StaffingCycles.AsNoTracking()
                .SingleOrDefaultAsync(c => c.EventId == eventId && c.IsCurrent, cancellationToken)
                ?? throw new InvalidOperationException($"No current staffing cycle found for event {eventId}.");

            var activeAssignments = await db.TeamAssignments
                .Where(t => t.EventId == eventId && t.StaffingCycleId == cycle.Id && t.Status == TeamAssignmentStatus.Active)
                .ToListAsync(cancellationToken);

            var activeAssignmentIds = activeAssignments.Select(a => a.Id).ToHashSet();

            if (submissions == null || submissions.Count == 0)
            {
                throw new ArgumentException("At least one attendance outcome must be submitted.", nameof(submissions));
            }

            var submissionIds = new HashSet<Guid>();
            foreach (var sub in submissions)
            {
                if (!submissionIds.Add(sub.TeamAssignmentId))
                {
                    throw new ArgumentException($"Duplicate submission for team assignment {sub.TeamAssignmentId}.", nameof(submissions));
                }

                if (!activeAssignmentIds.Contains(sub.TeamAssignmentId))
                {
                    throw new InvalidOperationException($"Team assignment {sub.TeamAssignmentId} is not an active assignment on the current staffing cycle.");
                }

                if (!Enum.TryParse<AttendanceOutcomeValue>(sub.Outcome, ignoreCase: true, out var outcomeVal) || !Enum.IsDefined(outcomeVal))
                {
                    throw new ArgumentException($"Invalid attendance outcome '{sub.Outcome}'. Permitted values are 'Attended' or 'NoShow'.", nameof(submissions));
                }
            }

            var existingOutcomes = await db.AttendanceOutcomes
                .Where(o => activeAssignmentIds.Contains(o.TeamAssignmentId))
                .ToListAsync(cancellationToken);

            var existingByAssignmentId = existingOutcomes.ToDictionary(o => o.TeamAssignmentId);

            foreach (var sub in submissions)
            {
                Enum.TryParse<AttendanceOutcomeValue>(sub.Outcome, ignoreCase: true, out var outcomeVal);
                if (existingByAssignmentId.TryGetValue(sub.TeamAssignmentId, out var existing))
                {
                    existing.UpdateOutcome(outcomeVal, actorUserId, utcNow);
                }
                else
                {
                    var newOutcome = AttendanceOutcome.Create(sub.TeamAssignmentId, outcomeVal, actorUserId, utcNow);
                    db.AttendanceOutcomes.Add(newOutcome);
                    existingByAssignmentId[sub.TeamAssignmentId] = newOutcome;
                }
            }

            var totalActive = activeAssignments.Count;
            var attendedCount = existingByAssignmentId.Values.Count(o => o.Outcome == AttendanceOutcomeValue.Attended);
            var noShowCount = existingByAssignmentId.Values.Count(o => o.Outcome == AttendanceOutcomeValue.NoShow);
            var unrecordedCount = totalActive - (attendedCount + noShowCount);
            if (unrecordedCount < 0)
            {
                unrecordedCount = 0;
            }

            var audit = AuditLog.AttendanceFinalized(
                eventId: eventId,
                actorUserId: actorUserId,
                correlationId: correlationId,
                occurredAt: utcNow,
                attendedCount: attendedCount,
                noShowCount: noShowCount);

            db.AuditLogs.Add(audit);

            await db.SaveChangesAsync(cancellationToken);

            return new FinalizeAttendanceResult(
                EventId: eventId,
                FinalizedAt: utcNow,
                TotalActiveAssignments: totalActive,
                AttendedCount: attendedCount,
                NoShowCount: noShowCount,
                UnrecordedCount: unrecordedCount);
        });
    }
}
