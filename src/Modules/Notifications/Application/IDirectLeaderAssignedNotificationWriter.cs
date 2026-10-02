using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Modules.Notifications.Application;

public interface IDirectLeaderAssignedNotificationWriter
{
    InPlatformNotification StageDirectLeaderAssigned(
        Guid recipientUserId,
        Guid eventId,
        string eventTitle,
        DateTimeOffset createdAt);
}
