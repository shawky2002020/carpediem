using System.Security.Cryptography;
using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Application.ChiefLeaderAppointments;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Events.Infrastructure.Persistence;
using Carpediem.Modules.Identity.Application;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Notifications.Application;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfChiefLeaderAppointmentStore(
    CarpediemDbContext db,
    IChiefLeaderCandidateDirectory candidates,
    IAppointmentNotificationWriter notifications) : IChiefLeaderAppointmentStore
{
    public async Task<ChiefLeaderAppointmentContext> GetContextAsync(
        Guid eventId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await EnsureActiveAdminAsync(actorUserId, cancellationToken);
        var eventItem = await db.Events.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken)
            ?? throw new ChiefLeaderAppointmentNotFoundException();
        if (eventItem.Status != EventStatus.Draft) throw new NotDraftException();

        var current = await db.ChiefLeaderAssignments.AsNoTracking()
            .Where(item => item.EventId == eventId &&
                (item.Status == ChiefLeaderAssignmentStatus.Pending ||
                 item.Status == ChiefLeaderAssignmentStatus.Active))
            .OrderByDescending(item => item.AppointedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var currentView = current is null ? null : await ToViewAsync(current, cancellationToken);
        var eligible = current is null
            ? (await candidates.ListEligibleAsync(cancellationToken))
                .Select(item => new AppointmentCandidate(item.UserId, item.UserName)).ToArray()
            : Array.Empty<AppointmentCandidate>();
        return new ChiefLeaderAppointmentContext(eventItem.Id, eventItem.Title,
            eventItem.Status.ToString(), currentView, eligible);
    }

    public async Task<ChiefLeaderAppointmentStoreResult> AppointAsync(
        Guid eventId,
        Guid candidateUserId,
        Guid actorUserId,
        Guid requestToken,
        byte[] fingerprint,
        Guid correlationId,
        DateTimeOffset appointedAt,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await EnsureActiveAdminAsync(actorUserId, cancellationToken);
                var existing = await db.ChiefLeaderAppointmentRequests.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.ActorUserId == actorUserId &&
                        item.RequestToken == requestToken, cancellationToken);
                if (existing is not null)
                {
                    return await ReplayAsync(existing, fingerprint, cancellationToken);
                }

                var eventItem = await db.Events.Include(item => item.ChiefLeaderAssignments)
                    .SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken)
                    ?? throw new ChiefLeaderAppointmentNotFoundException();
                var candidate = await candidates.FindEligibleAsync(candidateUserId, cancellationToken)
                    ?? throw new CandidateIneligibleException();

                var assignment = eventItem.AppointChiefLeader(candidate.UserId, actorUserId, appointedAt);
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                notifications.StageChiefLeaderAppointment(candidate.UserId, eventItem.Id,
                    eventItem.Title, appointedAt);
                db.AuditLogs.Add(ChiefLeaderAppointmentAuditWriter.Appointed(assignment,
                    actorUserId, correlationId, appointedAt));
                db.ChiefLeaderAppointmentRequests.Add(ChiefLeaderAppointmentRequest.Create(
                    actorUserId, requestToken, fingerprint, assignment.Id, appointedAt));
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new ChiefLeaderAppointmentStoreResult(
            ToView(assignment, new AppointmentCandidate(candidate.UserId, candidate.UserName)), false);
            });
        }
        catch (DbUpdateException error) when (IsDuplicateKey(error))
        {
            db.ChangeTracker.Clear();
            var existing = await db.ChiefLeaderAppointmentRequests.AsNoTracking()
                .SingleOrDefaultAsync(item => item.ActorUserId == actorUserId &&
                    item.RequestToken == requestToken, cancellationToken);
            if (existing is not null)
            {
                return await ReplayAsync(existing, fingerprint, cancellationToken);
            }
            throw new AppointmentConflictException();
        }
    }

    private async Task EnsureActiveAdminAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        var isAdmin = await (
            from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.Id == actorUserId && user.IsActive && role.Name == CarpediemRoles.Admin
            select user.Id).AnyAsync(cancellationToken);
        if (!isAdmin) throw new UnauthorizedAccessException("The actor does not have active Admin authority.");
    }

    private async Task<ChiefLeaderAppointmentStoreResult> ReplayAsync(
        ChiefLeaderAppointmentRequest existing,
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        if (!CryptographicOperations.FixedTimeEquals(existing.PayloadFingerprint, fingerprint))
        {
            throw new AppointmentRequestConflictException();
        }
        var assignment = await db.ChiefLeaderAssignments.AsNoTracking()
            .SingleAsync(item => item.Id == existing.AssignmentId, cancellationToken);
        return new ChiefLeaderAppointmentStoreResult(
            await ToViewAsync(assignment, cancellationToken), true);
    }

    private async Task<ChiefLeaderAppointmentView> ToViewAsync(
        ChiefLeaderAssignment assignment,
        CancellationToken cancellationToken)
    {
        var candidate = await db.Users.AsNoTracking()
            .Where(user => user.Id == assignment.ChiefLeaderUserId)
            .Select(user => new AppointmentCandidate(user.Id, user.UserName ?? user.Id.ToString()))
            .SingleAsync(cancellationToken);
        return ToView(assignment, candidate);
    }

    private static ChiefLeaderAppointmentView ToView(
        ChiefLeaderAssignment assignment,
        AppointmentCandidate candidate) => new(
            assignment.Id,
            assignment.EventId,
            candidate,
            assignment.Status,
            assignment.AppointedAt,
            assignment.AppointedByUserId,
            assignment.RowVersion);

    private static bool IsDuplicateKey(DbUpdateException error) =>
        error.InnerException is SqlException sql && sql.Number is 2601 or 2627;
}
