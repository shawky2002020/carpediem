namespace Carpediem.Modules.Events.Application.Dashboard;

public sealed record OperationalEvent(
    Guid EventId,
    string Title,
    string Status,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    bool InProgress,
    bool PendingAttendance,
    int ActiveRosterCount,
    int AvailableUnassignedCount);

public interface IOperationalDashboardStore
{
    Task<IReadOnlyList<OperationalEvent>> GetAsync(
        Guid actorUserId, DateTimeOffset utcNow, CancellationToken cancellationToken);
}
