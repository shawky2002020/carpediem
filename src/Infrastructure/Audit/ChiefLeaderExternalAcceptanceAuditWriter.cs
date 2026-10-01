using System.Text.Json;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;

namespace Carpediem.Infrastructure.Audit;

public interface IChiefLeaderExternalAcceptanceAuditWriter
{
    AuditLog Create(ChiefLeaderAssignment assignment, Guid actorUserId,
        string justification, Guid correlationId, DateTimeOffset occurredAt);
}

public sealed class ChiefLeaderExternalAcceptanceAuditWriter : IChiefLeaderExternalAcceptanceAuditWriter
{
    public const string Action = "ChiefLeaderExternalAcceptanceRecorded";

    public AuditLog Create(ChiefLeaderAssignment assignment, Guid actorUserId,
        string justification, Guid correlationId, DateTimeOffset occurredAt)
    {
        var reason = justification.Trim();
        if (reason.Length is < 1 or > 1000)
            throw new ArgumentException("A justification between 1 and 1,000 characters is required.",
                nameof(justification));
        if (assignment.Status != ChiefLeaderAssignmentStatus.Active ||
            assignment.AcceptanceSource != "AdminExternal" ||
            assignment.AcceptedByUserId != actorUserId)
            throw new ChiefLeaderResponseStateException();

        var value = JsonSerializer.Serialize(new
        {
            eventId = assignment.EventId,
            candidateUserId = assignment.ChiefLeaderUserId,
            status = assignment.Status.ToString(),
            acceptanceSource = assignment.AcceptanceSource,
            acceptedByUserId = assignment.AcceptedByUserId,
        });
        return AuditLog.Create(actorUserId, Action, assignment.Id, correlationId,
            occurredAt, "{\"status\":\"Pending\"}", value, "ChiefLeaderAssignment",
            "AdminExternal", reason);
    }
}
