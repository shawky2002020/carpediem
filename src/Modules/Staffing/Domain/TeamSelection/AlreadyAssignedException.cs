namespace Carpediem.Modules.Staffing.Domain.TeamSelection;

public sealed class AlreadyAssignedException(Guid eventId, Guid userId)
    : InvalidOperationException($"User '{userId}' already holds an active team assignment on event '{eventId}'.")
{
    public Guid EventId { get; } = eventId;
    public Guid UserId { get; } = userId;
}
