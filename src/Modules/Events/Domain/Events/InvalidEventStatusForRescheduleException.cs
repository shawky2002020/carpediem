namespace Carpediem.Modules.Events.Domain.Events;

public sealed class InvalidEventStatusForRescheduleException(Guid eventId, string currentStatus)
    : Exception($"Event {eventId} is in status '{currentStatus}'. Rescheduling is permitted only on 'Published' events.")
{
    public Guid EventId { get; } = eventId;
    public string CurrentStatus { get; } = currentStatus;
}
