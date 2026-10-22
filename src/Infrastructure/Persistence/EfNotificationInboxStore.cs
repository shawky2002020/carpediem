using Carpediem.Modules.Notifications.Application;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfNotificationInboxStore(CarpediemDbContext db) : INotificationInboxStore
{
    public async Task<IReadOnlyList<NotificationInboxItem>> GetLatestAsync(Guid recipientUserId, CancellationToken cancellationToken) =>
        await db.InPlatformNotifications.AsNoTracking()
            .Where(item => item.RecipientUserId == recipientUserId)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(100)
            .Select(item => new NotificationInboxItem(item.Id, item.EventId, item.NotificationType,
                item.ContextPayload, item.CreatedAt, item.ReadAt))
            .ToListAsync(cancellationToken);

    public Task<int> GetUnreadCountAsync(Guid recipientUserId, CancellationToken cancellationToken) =>
        db.InPlatformNotifications.AsNoTracking()
            .CountAsync(item => item.RecipientUserId == recipientUserId && item.ReadAt == null, cancellationToken);

    public async Task<bool> MarkReadAsync(Guid recipientUserId, Guid notificationId, DateTimeOffset readAt, CancellationToken cancellationToken)
    {
        var item = await db.InPlatformNotifications.SingleOrDefaultAsync(
            item => item.Id == notificationId && item.RecipientUserId == recipientUserId, cancellationToken);
        if (item is null) return false;
        item.MarkRead(readAt);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<int> MarkAllReadAsync(Guid recipientUserId, DateTimeOffset readAt, CancellationToken cancellationToken) =>
        db.InPlatformNotifications
            .Where(item => item.RecipientUserId == recipientUserId && item.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAt, readAt), cancellationToken);
}
