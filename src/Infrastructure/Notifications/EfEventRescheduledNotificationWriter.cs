using System.Text.Json;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Infrastructure.Notifications;

public sealed class EfEventRescheduledNotificationWriter : IEventRescheduledNotificationWriter
{
    public InPlatformNotification StageEventRescheduled(
        Guid recipientUserId,
        Guid eventId,
        string eventTitle,
        DateTimeOffset previousStart,
        DateTimeOffset previousEnd,
        DateTimeOffset newStart,
        DateTimeOffset newEnd,
        string justification,
        DateTimeOffset createdAt)
    {
        var payloadObj = new
        {
            eventTitle,
            previousStart = previousStart.ToString("O"),
            previousEnd = previousEnd.ToString("O"),
            newStart = newStart.ToString("O"),
            newEnd = newEnd.ToString("O"),
            justification
        };

        var payload = JsonSerializer.Serialize(payloadObj);
        if (payload.Length > 500)
        {
            var truncated = justification.Length > 100 ? justification[..100] + "..." : justification;
            payload = JsonSerializer.Serialize(new
            {
                eventTitle,
                previousStart = previousStart.ToString("O"),
                previousEnd = previousEnd.ToString("O"),
                newStart = newStart.ToString("O"),
                newEnd = newEnd.ToString("O"),
                justification = truncated
            });

            if (payload.Length > 500)
            {
                payload = JsonSerializer.Serialize(new
                {
                    eventTitle,
                    previousStart = previousStart.ToString("O"),
                    newStart = newStart.ToString("O")
                });
            }
        }

        return InPlatformNotification.EventRescheduled(recipientUserId, eventId, createdAt, payload);
    }
}
