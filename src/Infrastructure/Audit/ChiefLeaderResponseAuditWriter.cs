using System.Text.Json;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;

namespace Carpediem.Infrastructure.Audit;

public static class ChiefLeaderResponseAuditWriter
{
    public static AuditLog Create(ChiefLeaderAssignment assignment, Guid actorUserId,
        Guid correlationId, DateTimeOffset occurredAt)
    {
        var action = assignment.Status switch
        {
            ChiefLeaderAssignmentStatus.Active => "ChiefLeaderAccepted",
            ChiefLeaderAssignmentStatus.Declined => "ChiefLeaderDeclined",
            _ => throw new ChiefLeaderResponseStateException(),
        };
        var value = JsonSerializer.Serialize(new
        {
            eventId = assignment.EventId,
            candidateUserId = assignment.ChiefLeaderUserId,
            status = assignment.Status.ToString(),
        });
        return AuditLog.Create(actorUserId, action, assignment.Id, correlationId,
            occurredAt, "{\"status\":\"Pending\"}", value, "ChiefLeaderAssignment");
    }
}
