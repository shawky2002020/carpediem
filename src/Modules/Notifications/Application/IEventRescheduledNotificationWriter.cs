using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Modules.Notifications.Application;

public interface IEventRescheduledNotificationWriter
{
    InPlatformNotification StageEventRescheduled(
        Guid recipientUserId,
        Guid eventId,
        string eventTitle,
        DateTimeOffset previousStart,
        DateTimeOffset previousEnd,
        DateTimeOffset newStart,
        DateTimeOffset newEnd,
        string justification,
        DateTimeOffset createdAt);
}
