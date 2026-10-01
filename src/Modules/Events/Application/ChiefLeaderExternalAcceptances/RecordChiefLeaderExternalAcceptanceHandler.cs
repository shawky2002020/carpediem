namespace Carpediem.Modules.Events.Application.ChiefLeaderExternalAcceptances;

public sealed class RecordChiefLeaderExternalAcceptanceHandler(
    IChiefLeaderExternalAcceptanceStore store)
{
    public Task<ChiefLeaderExternalAcceptanceResult> HandleAsync(
        Guid eventId,
        Guid assignmentId,
        Guid actorUserId,
        byte[] expectedVersion,
        string? justification,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var normalized = justification?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 1000)
            throw new ChiefLeaderExternalAcceptanceValidationException(
                "Provide a justification between 1 and 1,000 characters.");

        return store.RecordAsync(eventId, assignmentId, actorUserId, expectedVersion,
            normalized, correlationId, DateTimeOffset.UtcNow, cancellationToken);
    }
}
