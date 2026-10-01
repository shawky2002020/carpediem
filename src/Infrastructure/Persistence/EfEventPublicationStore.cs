using System.Security.Cryptography;
using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Events.Application.EventPublication;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Notifications.Application;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfEventPublicationStore(
    CarpediemDbContext db,
    IEventPublishedNotificationWriter notifications) : IEventPublicationStore
{
    public async Task<PublishedEventResult> PublishAsync(
        Guid eventId,
        Guid actorUserId,
        byte[] expectedVersion,
        Guid correlationId,
        DateTimeOffset publishedAt,
        CancellationToken cancellationToken)
    {
        await EnsureActiveAdminAsync(actorUserId, cancellationToken);

        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var eventItem = await db.Events
                    .Include(e => e.ChiefLeaderAssignments)
                    .SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken)
                    ?? throw new EventPublicationNotFoundException();

                if (eventItem.Status == EventStatus.Published)
                {
                    return await BuildReplayResultAsync(eventItem, publishedAt, cancellationToken);
                }

                if (eventItem.Status != EventStatus.Draft)
                {
                    throw new EventPublicationConflictException($"Cannot publish an Event in '{eventItem.Status}' status.");
                }

                if (!eventItem.RowVersion.AsSpan().SequenceEqual(expectedVersion))
                {
                    throw new EventPublicationConflictException("The draft event has been modified by another operation.");
                }

                if (string.IsNullOrWhiteSpace(eventItem.Title) || string.IsNullOrWhiteSpace(eventItem.Location))
                {
                    throw new EventPublicationValidationException("Title and Location are required to publish.");
                }

                if (eventItem.StartDateTime >= eventItem.EndDateTime)
                {
                    throw new EventPublicationValidationException("End time must be after start time.");
                }

                // Domain invariant check
                eventItem.Publish(actorUserId, publishedAt);

                var activeChiefLeaderAssignment = eventItem.ChiefLeaderAssignments
                    .Single(a => a.Status == ChiefLeaderAssignmentStatus.Active);

                var chiefLeaderUser = await db.Users.AsNoTracking()
                    .Where(u => u.Id == activeChiefLeaderAssignment.ChiefLeaderUserId)
                    .Select(u => new { u.Id, UserName = u.UserName ?? u.Id.ToString() })
                    .SingleAsync(cancellationToken);

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                // Audit log
                db.AuditLogs.Add(AuditLog.EventPublished(actorUserId, eventItem.Id, correlationId, publishedAt));

                // Eligible Leader notifications
                var leaderUserIds = await (
                    from user in db.Users.AsNoTracking()
                    join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                    join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                    where user.IsActive && role.Name == CarpediemRoles.Leader
                    select user.Id).Distinct().ToListAsync(cancellationToken);

                foreach (var leaderId in leaderUserIds)
                {
                    var notification = notifications.StageEventPublished(leaderId, eventItem.Id, eventItem.Title, publishedAt);
                    db.InPlatformNotifications.Add(notification);
                }

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                var activeChiefLeaderInfo = new ActiveChiefLeaderInfo(
                    chiefLeaderUser.Id,
                    chiefLeaderUser.UserName,
                    activeChiefLeaderAssignment.Id);

                return new PublishedEventResult(
                    eventItem.Id,
                    eventItem.Title,
                    eventItem.StartDateTime,
                    eventItem.EndDateTime,
                    eventItem.Location,
                    eventItem.Notes,
                    eventItem.Status,
                    activeChiefLeaderInfo,
                    eventItem.RowVersion,
                    publishedAt,
                    IsReplay: false);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var reloaded = await db.Events.AsNoTracking()
                .Include(e => e.ChiefLeaderAssignments)
                .SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken);
            if (reloaded is not null && reloaded.Status == EventStatus.Published)
            {
                return await BuildReplayResultAsync(reloaded, publishedAt, cancellationToken);
            }
            throw new EventPublicationConflictException("The draft event has been modified by another operation.");
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

        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("The actor does not have active Admin authority.");
        }
    }

    private async Task<PublishedEventResult> BuildReplayResultAsync(
        Event eventItem,
        DateTimeOffset fallbackTime,
        CancellationToken cancellationToken)
    {
        var activeChiefLeaderAssignment = eventItem.ChiefLeaderAssignments
            .FirstOrDefault(a => a.Status == ChiefLeaderAssignmentStatus.Active);

        ActiveChiefLeaderInfo activeChiefLeaderInfo;
        if (activeChiefLeaderAssignment is not null)
        {
            var chiefLeaderUser = await db.Users.AsNoTracking()
                .Where(u => u.Id == activeChiefLeaderAssignment.ChiefLeaderUserId)
                .Select(u => new { u.Id, UserName = u.UserName ?? u.Id.ToString() })
                .SingleOrDefaultAsync(cancellationToken);

            activeChiefLeaderInfo = new ActiveChiefLeaderInfo(
                chiefLeaderUser?.Id ?? activeChiefLeaderAssignment.ChiefLeaderUserId,
                chiefLeaderUser?.UserName ?? activeChiefLeaderAssignment.ChiefLeaderUserId.ToString(),
                activeChiefLeaderAssignment.Id);
        }
        else
        {
            activeChiefLeaderInfo = new ActiveChiefLeaderInfo(Guid.Empty, "Unknown", Guid.Empty);
        }

        var publishedAudit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.ResourceId == eventItem.Id && a.Action == "EventPublished")
            .OrderByDescending(a => a.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

        var publishedAt = publishedAudit?.OccurredAt ?? fallbackTime;

        return new PublishedEventResult(
            eventItem.Id,
            eventItem.Title,
            eventItem.StartDateTime,
            eventItem.EndDateTime,
            eventItem.Location,
            eventItem.Notes,
            eventItem.Status,
            activeChiefLeaderInfo,
            eventItem.RowVersion,
            publishedAt,
            IsReplay: true);
    }
}
