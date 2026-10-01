namespace Carpediem.Modules.Staffing.Domain.Availability;

public sealed class AvailabilityResponse
{
    private AvailabilityResponse() { }

    public Guid Id { get; private set; }
    public Guid StaffingCycleId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public AvailabilityResponseType Response { get; private set; }
    public DateTimeOffset DeclaredAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static AvailabilityResponse Declare(
        Guid staffingCycleId,
        Guid eventId,
        Guid userId,
        AvailabilityResponseType response,
        DateTimeOffset eventStartDateTime,
        DateTimeOffset declaredAt,
        bool isChiefLeaderOnEvent,
        string eventStatus)
    {
        if (staffingCycleId == Guid.Empty) throw new ArgumentException("StaffingCycleId cannot be empty.", nameof(staffingCycleId));
        if (eventId == Guid.Empty) throw new ArgumentException("EventId cannot be empty.", nameof(eventId));
        if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (!Enum.IsDefined(response)) throw new ArgumentOutOfRangeException(nameof(response), "Invalid availability response.");

        if (!string.Equals(eventStatus, "Published", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidEventStatusException(eventId, eventStatus);
        }

        if (declaredAt >= eventStartDateTime)
        {
            throw new AvailabilityWindowClosedException(eventId, eventStartDateTime, declaredAt);
        }

        if (isChiefLeaderOnEvent)
        {
            throw new ChiefLeaderExclusivityException(eventId, userId);
        }

        return new AvailabilityResponse
        {
            Id = Guid.NewGuid(),
            StaffingCycleId = staffingCycleId,
            EventId = eventId,
            UserId = userId,
            Response = response,
            DeclaredAt = declaredAt,
        };
    }

    public void UpdateResponse(
        AvailabilityResponseType newResponse,
        DateTimeOffset eventStartDateTime,
        DateTimeOffset declaredAt,
        bool isChiefLeaderOnEvent,
        string eventStatus)
    {
        if (!Enum.IsDefined(newResponse)) throw new ArgumentOutOfRangeException(nameof(newResponse), "Invalid availability response.");

        if (!string.Equals(eventStatus, "Published", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidEventStatusException(EventId, eventStatus);
        }

        if (declaredAt >= eventStartDateTime)
        {
            throw new AvailabilityWindowClosedException(EventId, eventStartDateTime, declaredAt);
        }

        if (isChiefLeaderOnEvent)
        {
            throw new ChiefLeaderExclusivityException(EventId, UserId);
        }

        Response = newResponse;
        DeclaredAt = declaredAt;
    }
}
