namespace Carpediem.Infrastructure.Audit;

public sealed class AuditLog
{
    private AuditLog() { }

    public Guid Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string? MandatoryReason { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string? PreviousValue { get; private set; }
    public string? NewValue { get; private set; }

    internal static AuditLog Create(
        Guid actorUserId,
        string action,
        Guid resourceId,
        Guid correlationId,
        DateTimeOffset occurredAt,
        string? previousValue,
        string? newValue,
        string resourceType = "Event") => new()
        {
            Id = Guid.NewGuid(),
            OccurredAt = occurredAt,
            ActorUserId = actorUserId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            CorrelationId = correlationId,
            Source = "Direct",
            PreviousValue = previousValue,
            NewValue = newValue,
        };
}
