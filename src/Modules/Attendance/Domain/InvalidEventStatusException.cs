namespace Carpediem.Modules.Attendance.Domain;

public sealed class InvalidEventStatusException(Guid eventId, string currentStatus)
    : Exception($"Attendance operations are permitted only on 'Published' or 'Completed' events. Event {eventId} is currently in '{currentStatus}' status.")
{
    public Guid EventId { get; } = eventId;
    public string CurrentStatus { get; } = currentStatus;
}
