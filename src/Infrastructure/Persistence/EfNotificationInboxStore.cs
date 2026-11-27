using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfNotificationInboxStore(CarpediemDbContext db) : INotificationInboxStore
{
    private const int UnreadPageSize = 30;

    public async Task<IReadOnlyList<NotificationInboxItem>> GetLatestAsync(Guid recipientUserId, CancellationToken cancellationToken) =>
        await GetItemsAsync(OwnedBy(recipientUserId), 100, cancellationToken);

    public async Task<NotificationUnreadPage?> GetUnreadAsync(Guid recipientUserId, Guid? beforeId, CancellationToken cancellationToken)
    {
        var owned = OwnedBy(recipientUserId);
        var unread = owned.Where(item => item.ReadAt == null);
        if (beforeId is { } id)
        {
            // The cursor remains usable after its notification is marked as read.
            var cursor = await owned.Where(item => item.Id == id)
                .Select(item => new { item.CreatedAt, item.Id }).SingleOrDefaultAsync(cancellationToken);
            if (cursor is null) return null;
            unread = unread.Where(item => item.CreatedAt < cursor.CreatedAt ||
                (item.CreatedAt == cursor.CreatedAt && item.Id.CompareTo(cursor.Id) < 0));
        }

        var items = await GetItemsAsync(unread, UnreadPageSize + 1, cancellationToken);
        if (items.Count <= UnreadPageSize) return new NotificationUnreadPage(items, null);
        items.RemoveAt(UnreadPageSize);
        return new NotificationUnreadPage(items, items[^1].Id);
    }

    private IQueryable<InPlatformNotification> OwnedBy(Guid recipientUserId) =>
        db.InPlatformNotifications.AsNoTracking().Where(item => item.RecipientUserId == recipientUserId);

    private static Task<List<NotificationInboxItem>> GetItemsAsync(IQueryable<InPlatformNotification> source,
        int limit, CancellationToken cancellationToken) =>
        source
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(limit)
            .Select(item => new NotificationInboxItem(item.Id, item.EventId, item.NotificationType,
                item.ContextPayload, item.CreatedAt, item.ReadAt))
            .ToListAsync(cancellationToken);

    public Task<int> GetUnreadCountAsync(Guid recipientUserId, CancellationToken cancellationToken) =>
        OwnedBy(recipientUserId).CountAsync(item => item.ReadAt == null, cancellationToken);

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
