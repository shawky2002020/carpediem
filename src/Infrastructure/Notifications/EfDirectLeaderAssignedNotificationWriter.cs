using System.Text.Json;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Infrastructure.Notifications;

public sealed class EfDirectLeaderAssignedNotificationWriter : IDirectLeaderAssignedNotificationWriter
{
    public InPlatformNotification StageDirectLeaderAssigned(
        Guid recipientUserId,
        Guid eventId,
        string eventTitle,
        DateTimeOffset createdAt)
    {
        var payload = JsonSerializer.Serialize(new { eventTitle });
        return InPlatformNotification.DirectLeaderAssigned(recipientUserId, eventId, createdAt, payload);
    }
}
