namespace Carpediem.Modules.Staffing.Application.ReplacementAssignment;

public interface IReplacementAssignmentStore
{
    Task<ReplacementAssignmentContextResult> GetReplacementContextAsync(
        Guid eventId,
        Guid actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<AssignReplacementResult> AssignReplacementLeaderAsync(
        Guid eventId,
        Guid candidateUserId,
        Guid actorUserId,
        Guid? replacedAssignmentId,
        string? justification,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}

public sealed record ReplacementTeamMemberItem(
    Guid AssignmentId,
    Guid UserId,
    string UserName,
    string Status,
    DateTimeOffset AssignedAt,
    string Version);

public sealed record ReplacementCancelledMemberItem(
    Guid AssignmentId,
    Guid UserId,
    string UserName,
    string Status,
    DateTimeOffset CancelledAt,
    string CancellationReason);

public sealed record ReplacementCandidateItem(
    Guid UserId,
    string UserName,
    string AvailabilityStatus,
    DateTimeOffset? DeclaredAt);

public sealed record ReplacementAssignmentContextResult(
    Guid EventId,
    string EventTitle,
    Guid StaffingCycleId,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    bool IsWindowOpen,
    IReadOnlyList<ReplacementTeamMemberItem> ActiveTeam,
    IReadOnlyList<ReplacementCancelledMemberItem> CancelledAssignments,
    IReadOnlyList<ReplacementCandidateItem> Candidates);

public sealed record AssignReplacementResult(
    Guid Id,
    Guid EventId,
    Guid StaffingCycleId,
    Guid UserId,
    string Status,
    DateTimeOffset AssignedAt,
    Guid AssignedByUserId,
    string Version,
    bool IsIdempotentReplay);
