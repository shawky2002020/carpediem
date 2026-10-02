using System.Text.Json;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Notifications.Domain;

namespace Carpediem.Infrastructure.Notifications;

public sealed class EfTeamMemberCancelledNotificationWriter : ITeamMemberCancelledNotificationWriter
{
    public InPlatformNotification StageTeamMemberCancelled(
        Guid recipientUserId,
        Guid eventId,
        string eventTitle,
        string volunteerName,
        string justification,
        DateTimeOffset createdAt)
    {
        var payloadObj = new
        {
            eventTitle,
            volunteerName,
            justification
        };
        var payload = JsonSerializer.Serialize(payloadObj);
        if (payload.Length > 500)
        {
            var truncated = justification.Length > 100 ? justification[..100] + "..." : justification;
            payload = JsonSerializer.Serialize(new { eventTitle, volunteerName, justification = truncated });
            if (payload.Length > 500)
            {
                payload = JsonSerializer.Serialize(new { eventTitle, volunteerName });
            }
        }
        return InPlatformNotification.TeamMemberCancelled(recipientUserId, eventId, createdAt, payload);
    }
}
