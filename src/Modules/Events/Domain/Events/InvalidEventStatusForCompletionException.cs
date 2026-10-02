namespace Carpediem.Modules.Events.Domain.Events;

public sealed class InvalidEventStatusForCompletionException(Guid eventId, string currentStatus)
    : Exception($"Event {eventId} cannot be completed because its current status is '{currentStatus}'. Only Published events may transition to Completed.")
{
    public Guid EventId { get; } = eventId;
    public string CurrentStatus { get; } = currentStatus;
}
