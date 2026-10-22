namespace Carpediem.Modules.Events.Application.Cancellation;

public sealed record EventCancellationContext(Guid EventId, string Title, string Status, string Version);
public sealed record EventCancellationResult(Guid EventId, string Status, int CancelledAssignments, int NotificationsCreated);

public interface IEventCancellationStore
{
    Task<EventCancellationContext?> GetContextAsync(Guid eventId, Guid actorUserId, CancellationToken cancellationToken);
    Task<EventCancellationResult> CancelAsync(Guid eventId, Guid actorUserId, byte[] expectedVersion,
        string reason, Guid correlationId, DateTimeOffset utcNow, CancellationToken cancellationToken);
}
