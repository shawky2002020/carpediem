using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Modules.Notifications.Application;

public interface IEventPublishedNotificationWriter
{
    InPlatformNotification StageEventPublished(
        Guid recipientUserId,
        Guid eventId,
        string eventTitle,
        DateTimeOffset createdAt);
}
