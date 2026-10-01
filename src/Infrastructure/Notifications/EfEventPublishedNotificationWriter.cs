using System.Text.Json;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Infrastructure.Notifications;

public sealed class EfEventPublishedNotificationWriter : IEventPublishedNotificationWriter
{
    public InPlatformNotification StageEventPublished(
        Guid recipientUserId,
        Guid eventId,
        string eventTitle,
        DateTimeOffset createdAt)
    {
        var payload = JsonSerializer.Serialize(new { eventTitle });
        return InPlatformNotification.EventPublished(recipientUserId, eventId, createdAt, payload);
    }
}
