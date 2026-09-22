using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Modules.Notifications.Application;

public interface IAppointmentNotificationWriter
{
    InPlatformNotification StageChiefLeaderAppointment(
        Guid recipientUserId,
        Guid eventId,
        string eventTitle,
        DateTimeOffset createdAt);
}
