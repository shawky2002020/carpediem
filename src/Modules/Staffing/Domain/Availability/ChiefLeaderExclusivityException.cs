namespace Carpediem.Modules.Staffing.Domain.Availability;

public sealed class ChiefLeaderExclusivityException(Guid eventId, Guid userId)
    : Exception($"User {userId} is the active Chief Leader for Event {eventId} and cannot declare volunteer availability on the same event per CHANGE-001.")
{
    public Guid EventId { get; } = eventId;
    public Guid UserId { get; } = userId;
}
