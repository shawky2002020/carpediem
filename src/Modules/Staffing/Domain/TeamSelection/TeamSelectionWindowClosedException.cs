namespace Carpediem.Modules.Staffing.Domain.TeamSelection;

public sealed class TeamSelectionWindowClosedException(Guid eventId)
    : InvalidOperationException($"Team selection for event '{eventId}' is closed because the event has already started.")
{
    public Guid EventId { get; } = eventId;
}
