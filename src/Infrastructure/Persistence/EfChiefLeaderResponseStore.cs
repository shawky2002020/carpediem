using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Application.ChiefLeaderResponses;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Application;
using Carpediem.Modules.Notifications.Application;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfChiefLeaderResponseStore(
    CarpediemDbContext db,
    IChiefLeaderResponseDirectory identity,
    IChiefLeaderDeclineNotificationWriter notifications) : IChiefLeaderResponseStore
{
    public async Task<IReadOnlyList<ChiefLeaderResponseView>> ListPendingAsync(
        Guid actorUserId, CancellationToken cancellationToken)
    {
        await EnsureActiveChiefLeaderAsync(actorUserId, cancellationToken);
        var rows = await (
            from assignment in db.ChiefLeaderAssignments.AsNoTracking()
            join eventItem in db.Events.AsNoTracking() on assignment.EventId equals eventItem.Id
            where assignment.ChiefLeaderUserId == actorUserId &&
                assignment.Status == ChiefLeaderAssignmentStatus.Pending &&
                eventItem.Status == EventStatus.Draft
            orderby eventItem.StartDateTime, assignment.Id
            select new { assignment, eventItem }).ToListAsync(cancellationToken);
        return rows.Select(row => ToView(row.assignment, row.eventItem)).ToArray();
    }

    public async Task<ChiefLeaderResponseView> GetAsync(
        Guid assignmentId, Guid actorUserId, CancellationToken cancellationToken)
    {
        await EnsureActiveChiefLeaderAsync(actorUserId, cancellationToken);
        var assignment = await db.ChiefLeaderAssignments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        if (assignment is null || assignment.ChiefLeaderUserId != actorUserId)
            throw new ChiefLeaderResponseNotFoundException();
        var eventItem = await db.Events.AsNoTracking()
            .SingleAsync(item => item.Id == assignment.EventId, cancellationToken);
        return ToView(assignment, eventItem);
    }

    public async Task<ChiefLeaderResponseView> RespondAsync(
        Guid assignmentId, Guid actorUserId, byte[] expectedVersion,
        ChiefLeaderDecision decision, Guid correlationId, DateTimeOffset respondedAt,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await EnsureActiveChiefLeaderAsync(actorUserId, cancellationToken);
                var assignment = await db.ChiefLeaderAssignments
                    .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
                if (assignment is null || assignment.ChiefLeaderUserId != actorUserId)
                    throw new ChiefLeaderResponseNotFoundException();
                var eventItem = await db.Events.AsNoTracking()
                    .SingleAsync(item => item.Id == assignment.EventId, cancellationToken);

                if (IsFinalStateFor(decision, assignment, actorUserId))
                    return ToView(assignment, eventItem);
                if (assignment.Status != ChiefLeaderAssignmentStatus.Pending ||
                    eventItem.Status != EventStatus.Draft ||
                    !assignment.RowVersion.AsSpan().SequenceEqual(expectedVersion))
                    throw new ChiefLeaderResponseConflictException();

                // No TeamAssignment storage or creation path exists before the Staffing slice.
                // The Staffing module must replace this fact with its authoritative same-Event lookup.
                const bool hasActiveTeamAssignmentOnCurrentCycle = false;

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                if (decision == ChiefLeaderDecision.Accept)
                {
                    assignment.AcceptDirect(actorUserId, respondedAt, hasActiveTeamAssignmentOnCurrentCycle);
                }
                else
                {
                    assignment.DeclineDirect(actorUserId, respondedAt);
                    var adminIds = await identity.ListActiveAdminIdsAsync(cancellationToken);
                    foreach (var adminId in adminIds)
                        notifications.Stage(adminId, eventItem.Id, eventItem.Title, respondedAt);
                }
                db.AuditLogs.Add(ChiefLeaderResponseAuditWriter.Create(assignment,
                    actorUserId, correlationId, respondedAt));
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ToView(assignment, eventItem);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            await EnsureActiveChiefLeaderAsync(actorUserId, cancellationToken);
            var current = await db.ChiefLeaderAssignments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
            if (current is null || current.ChiefLeaderUserId != actorUserId)
                throw new ChiefLeaderResponseNotFoundException();
            if (IsFinalStateFor(decision, current, actorUserId))
            {
                var eventItem = await db.Events.AsNoTracking()
                    .SingleAsync(item => item.Id == current.EventId, cancellationToken);
                return ToView(current, eventItem);
            }
            throw new ChiefLeaderResponseConflictException();
        }
    }

    private async Task EnsureActiveChiefLeaderAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        if (!await identity.IsActiveChiefLeaderAsync(actorUserId, cancellationToken))
            throw new UnauthorizedAccessException("Active Chief Leader access is required.");
    }

    private static bool IsFinalStateFor(ChiefLeaderDecision decision,
        ChiefLeaderAssignment assignment, Guid actorUserId) =>
        decision == ChiefLeaderDecision.Accept &&
            assignment.Status == ChiefLeaderAssignmentStatus.Active &&
            assignment.AcceptanceSource == "Direct" &&
            assignment.AcceptedByUserId == actorUserId ||
        decision == ChiefLeaderDecision.Decline &&
            assignment.Status == ChiefLeaderAssignmentStatus.Declined;

    private static ChiefLeaderResponseView ToView(
        ChiefLeaderAssignment assignment, Event eventItem) => new(
            assignment.Id, eventItem.Id, eventItem.Title,
            eventItem.StartDateTime, eventItem.EndDateTime, eventItem.Location,
            assignment.Status, assignment.AppointedAt, assignment.AcceptedAt,
            assignment.DeclinedAt, assignment.RowVersion);
}
