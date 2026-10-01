namespace Carpediem.Modules.Staffing.Domain.Availability;

public sealed class AvailabilityWindowClosedException(Guid eventId, DateTimeOffset startDateTime, DateTimeOffset attemptedAt)
    : Exception($"Availability declaration window is closed for Event {eventId}. Event start time was {startDateTime:O}, attempted at {attemptedAt:O}.")
{
    public Guid EventId { get; } = eventId;
    public DateTimeOffset StartDateTime { get; } = startDateTime;
    public DateTimeOffset AttemptedAt { get; } = attemptedAt;
}
