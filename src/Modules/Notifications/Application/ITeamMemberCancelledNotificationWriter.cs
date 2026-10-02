using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Modules.Notifications.Application;

public interface ITeamMemberCancelledNotificationWriter
{
    InPlatformNotification StageTeamMemberCancelled(
        Guid recipientUserId,
        Guid eventId,
        string eventTitle,
        string volunteerName,
        string justification,
        DateTimeOffset createdAt);
}
