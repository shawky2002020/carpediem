namespace Carpediem.Modules.Notifications.Application;

public sealed record NotificationInboxItem(
    Guid Id,
    Guid? EventId,
    string NotificationType,
    string? ContextPayload,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public interface INotificationInboxStore
{
    Task<IReadOnlyList<NotificationInboxItem>> GetLatestAsync(Guid recipientUserId, CancellationToken cancellationToken);
    Task<int> GetUnreadCountAsync(Guid recipientUserId, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(Guid recipientUserId, Guid notificationId, DateTimeOffset readAt, CancellationToken cancellationToken);
    Task<int> MarkAllReadAsync(Guid recipientUserId, DateTimeOffset readAt, CancellationToken cancellationToken);
}
