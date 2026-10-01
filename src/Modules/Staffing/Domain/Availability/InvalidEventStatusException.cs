namespace Carpediem.Modules.Staffing.Domain.Availability;

public sealed class InvalidEventStatusException(Guid eventId, string currentStatus)
    : Exception($"Event {eventId} is in status '{currentStatus}'. Availability declarations are permitted only on 'Published' events.")
{
    public Guid EventId { get; } = eventId;
    public string CurrentStatus { get; } = currentStatus;
}
