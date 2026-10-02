namespace Carpediem.Modules.Events.Domain.Events;

public sealed class EventRescheduleWindowClosedException(Guid eventId)
    : Exception($"Event {eventId} cannot be rescheduled because its scheduled start time has already passed.")
{
    public Guid EventId { get; } = eventId;
}
