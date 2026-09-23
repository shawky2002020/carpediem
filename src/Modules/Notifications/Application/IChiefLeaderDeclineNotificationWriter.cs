using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Modules.Notifications.Application;

public interface IChiefLeaderDeclineNotificationWriter
{
    InPlatformNotification Stage(Guid recipientUserId, Guid eventId,
        string eventTitle, DateTimeOffset createdAt);
}
