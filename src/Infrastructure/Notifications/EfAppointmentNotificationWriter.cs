using System.Text.Json;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Notifications.Domain;
using Carpediem.Infrastructure.Persistence;

namespace Carpediem.Infrastructure.Notifications;

public sealed class EfAppointmentNotificationWriter(CarpediemDbContext db)
    : IAppointmentNotificationWriter
{
    public InPlatformNotification StageChiefLeaderAppointment(
        Guid recipientUserId,
        Guid eventId,
        string eventTitle,
        DateTimeOffset createdAt)
    {
        var context = JsonSerializer.Serialize(new { eventTitle });
        if (context.Length > 500) context = JsonSerializer.Serialize(new { eventId });
        var notification = InPlatformNotification.ChiefLeaderAppointment(
            recipientUserId, eventId, createdAt, context);
        db.InPlatformNotifications.Add(notification);
        return notification;
    }
}
