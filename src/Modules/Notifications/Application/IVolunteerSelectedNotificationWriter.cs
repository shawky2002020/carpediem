using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Modules.Notifications.Application;

public interface IVolunteerSelectedNotificationWriter
{
    InPlatformNotification StageVolunteerSelected(
        Guid recipientUserId,
        Guid eventId,
        string eventTitle,
        DateTimeOffset createdAt);
}
