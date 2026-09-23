using System.Text.Json;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Infrastructure.Notifications;

public sealed class EfChiefLeaderDeclineNotificationWriter(CarpediemDbContext db)
    : IChiefLeaderDeclineNotificationWriter
{
    public InPlatformNotification Stage(Guid recipientUserId, Guid eventId,
        string eventTitle, DateTimeOffset createdAt)
    {
        var context = JsonSerializer.Serialize(new { eventTitle });
        if (context.Length > 500) context = JsonSerializer.Serialize(new { eventId });
        var notification = InPlatformNotification.ChiefLeaderDeclined(
            recipientUserId, eventId, createdAt, context);
        db.InPlatformNotifications.Add(notification);
        return notification;
    }
}
