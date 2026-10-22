namespace Carpediem.Modules.Identity.Application;

public interface ILeaderHistoryStore
{
    Task<LeaderHistoryResult?> GetOwnAsync(Guid actorUserId, CancellationToken cancellationToken = default);

    Task<LeaderHistoryResult?> GetLeaderAsync(
        Guid actorUserId,
        Guid leaderUserId,
        Guid? eventId,
        CancellationToken cancellationToken = default);
}

public sealed record LeaderHistoryResult(
    Guid UserId,
    string UserName,
    int EventsAttended,
    int NoShows,
    int SelfCancellations,
    IReadOnlyList<LeaderHistoryEntry> Events);

public sealed record LeaderHistoryEntry(
    Guid AssignmentId,
    Guid EventId,
    string EventTitle,
    int StaffingCycleNumber,
    DateOnly EffectiveEventDate,
    bool IsCurrentCycle,
    string AssignmentStatus,
    DateTimeOffset AssignedAt,
    DateTimeOffset? CancelledAt,
    string? AttendanceOutcome,
    bool SelfCancelled);
