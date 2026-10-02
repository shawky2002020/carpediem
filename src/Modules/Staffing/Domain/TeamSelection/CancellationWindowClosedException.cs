namespace Carpediem.Modules.Staffing.Domain.TeamSelection;

public sealed class CancellationWindowClosedException(Guid eventId)
    : InvalidOperationException($"Assignment cancellation for event '{eventId}' is closed because the event has already started.")
{
    public Guid EventId { get; } = eventId;
}
