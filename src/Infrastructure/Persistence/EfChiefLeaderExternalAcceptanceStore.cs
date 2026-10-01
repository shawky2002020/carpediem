using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Application.ChiefLeaderAppointments;
using Carpediem.Modules.Events.Application.ChiefLeaderExternalAcceptances;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfChiefLeaderExternalAcceptanceStore(
    CarpediemDbContext db,
    IChiefLeaderExternalAcceptanceAuditWriter auditWriter) : IChiefLeaderExternalAcceptanceStore
{
    public async Task<ChiefLeaderExternalAcceptanceResult> RecordAsync(
        Guid eventId,
        Guid assignmentId,
        Guid actorUserId,
        byte[] expectedVersion,
        string justification,
        Guid correlationId,
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await EnsureActiveAdminAsync(actorUserId, cancellationToken);
                var eventItem = await db.Events.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken)
                    ?? throw new ChiefLeaderExternalAcceptanceNotFoundException();
                var assignment = await db.ChiefLeaderAssignments
                    .SingleOrDefaultAsync(item => item.Id == assignmentId && item.EventId == eventId,
                        cancellationToken)
                    ?? throw new ChiefLeaderExternalAcceptanceNotFoundException();

                if (await IsSameIntentReplayAsync(assignment, actorUserId, justification, cancellationToken))
                    return new ChiefLeaderExternalAcceptanceResult(
                        await ToViewAsync(assignment, cancellationToken), true);
                if (eventItem.Status != EventStatus.Draft ||
                    assignment.Status != ChiefLeaderAssignmentStatus.Pending ||
                    !assignment.RowVersion.AsSpan().SequenceEqual(expectedVersion))
                    throw new ChiefLeaderExternalAcceptanceConflictException();

                // No TeamAssignment storage or creation path exists before the Staffing slice.
                // The Staffing module must replace this fact with its authoritative same-Event lookup.
                const bool hasActiveTeamAssignmentOnCurrentCycle = false;

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                assignment.AcceptExternal(actorUserId, acceptedAt,
                    hasActiveTeamAssignmentOnCurrentCycle);
                db.AuditLogs.Add(auditWriter.Create(assignment, actorUserId, justification,
                    correlationId, acceptedAt));
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new ChiefLeaderExternalAcceptanceResult(
                    await ToViewAsync(assignment, cancellationToken), false);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ResolveConflictOrReplayAsync(eventId, assignmentId, actorUserId,
                justification, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return await ResolveConflictOrReplayAsync(eventId, assignmentId, actorUserId,
                justification, cancellationToken);
        }
    }

    private async Task<ChiefLeaderExternalAcceptanceResult> ResolveConflictOrReplayAsync(
        Guid eventId, Guid assignmentId, Guid actorUserId, string justification,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var assignment = await db.ChiefLeaderAssignments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == assignmentId && item.EventId == eventId,
                cancellationToken)
            ?? throw new ChiefLeaderExternalAcceptanceNotFoundException();
        if (await IsSameIntentReplayAsync(assignment, actorUserId, justification, cancellationToken))
            return new ChiefLeaderExternalAcceptanceResult(
                await ToViewAsync(assignment, cancellationToken), true);
        throw new ChiefLeaderExternalAcceptanceConflictException();
    }

    private Task<bool> IsSameIntentReplayAsync(
        ChiefLeaderAssignment assignment, Guid actorUserId, string justification,
        CancellationToken cancellationToken)
    {
        if (assignment.Status != ChiefLeaderAssignmentStatus.Active ||
            assignment.AcceptanceSource != "AdminExternal" ||
            assignment.AcceptedByUserId != actorUserId)
            return Task.FromResult(false);

        return db.AuditLogs.AsNoTracking().AnyAsync(log =>
            log.Action == ChiefLeaderExternalAcceptanceAuditWriter.Action &&
            log.ResourceType == "ChiefLeaderAssignment" &&
            log.ResourceId == assignment.Id &&
            log.ActorUserId == actorUserId &&
            log.Source == "AdminExternal" &&
            log.MandatoryReason == justification, cancellationToken);
    }

    private async Task EnsureActiveAdminAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        var isAdmin = await (
            from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.Id == actorUserId && user.IsActive && role.Name == CarpediemRoles.Admin
            select user.Id).AnyAsync(cancellationToken);
        if (!isAdmin) throw new UnauthorizedAccessException("Active Admin authority is required.");
    }

    private async Task<ChiefLeaderAppointmentView> ToViewAsync(
        ChiefLeaderAssignment assignment, CancellationToken cancellationToken)
    {
        var candidate = await db.Users.AsNoTracking()
            .Where(user => user.Id == assignment.ChiefLeaderUserId)
            .Select(user => new AppointmentCandidate(user.Id, user.UserName ?? user.Id.ToString()))
            .SingleAsync(cancellationToken);
        return new ChiefLeaderAppointmentView(assignment.Id, assignment.EventId, candidate,
            assignment.Status, assignment.AppointedAt, assignment.AppointedByUserId,
            assignment.AcceptedAt, assignment.AcceptanceSource, assignment.AcceptedByUserId,
            assignment.RowVersion);
    }
}
