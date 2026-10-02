using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Modules.Notifications.Application;

public interface IReplacementLeaderAssignedNotificationWriter
{
    InPlatformNotification StageReplacementLeaderAssigned(
        Guid recipientUserId,
        Guid eventId,
        string eventTitle,
        DateTimeOffset createdAt);
}
