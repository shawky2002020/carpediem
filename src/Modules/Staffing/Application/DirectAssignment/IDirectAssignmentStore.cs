namespace Carpediem.Modules.Staffing.Application.DirectAssignment;

public sealed record DirectAssignmentCandidateResult(
    Guid UserId,
    string UserName,
    string AvailabilityStatus,
    DateTimeOffset? DeclaredAt);

public sealed record DirectAssignmentCandidatesResult(
    Guid EventId,
    Guid StaffingCycleId,
    string EventTitle,
    bool IsWindowOpen,
    IReadOnlyList<DirectAssignmentCandidateResult> Candidates);

public sealed record DirectAssignmentResult(
    Guid Id,
    Guid EventId,
    Guid StaffingCycleId,
    Guid UserId,
    string Status,
    DateTimeOffset AssignedAt,
    Guid AssignedByUserId,
    string Version,
    bool IsIdempotentReplay);

public interface IDirectAssignmentStore
{
    Task<DirectAssignmentCandidatesResult?> GetCandidatesAsync(
        Guid eventId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<DirectAssignmentResult> DirectAssignLeaderAsync(
        Guid eventId,
        Guid candidateUserId,
        Guid actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
