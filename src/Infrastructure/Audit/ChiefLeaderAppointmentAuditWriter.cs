using System.Text.Json;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;

namespace Carpediem.Infrastructure.Audit;

public static class ChiefLeaderAppointmentAuditWriter
{
    public static AuditLog Appointed(ChiefLeaderAssignment assignment, Guid actorUserId,
        Guid correlationId, DateTimeOffset occurredAt)
    {
        var value = JsonSerializer.Serialize(new
        {
            eventId = assignment.EventId,
            candidateUserId = assignment.ChiefLeaderUserId,
            status = assignment.Status.ToString(),
        });
        return AuditLog.Create(actorUserId, "ChiefLeaderAppointed", assignment.Id,
            correlationId, occurredAt, null, value, "ChiefLeaderAssignment");
    }
}
