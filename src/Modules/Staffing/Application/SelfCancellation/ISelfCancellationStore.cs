namespace Carpediem.Modules.Staffing.Application.SelfCancellation;

public interface ISelfCancellationStore
{
    Task<MyAssignmentContextResult?> GetMyAssignmentAsync(
        Guid eventId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<SelfCancellationResult> CancelAssignmentAsync(
        Guid eventId,
        Guid assignmentId,
        Guid actorUserId,
        string justification,
        byte[]? expectedRowVersion,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}

public sealed record MyAssignmentContextResult(
    Guid AssignmentId,
    Guid EventId,
    Guid StaffingCycleId,
    Guid UserId,
    string UserName,
    string EventTitle,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    string Status,
    DateTimeOffset AssignedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    bool IsWindowOpen,
    string Version);

public sealed record SelfCancellationResult(
    Guid Id,
    Guid EventId,
    Guid StaffingCycleId,
    Guid UserId,
    string Status,
    DateTimeOffset AssignedAt,
    Guid AssignedByUserId,
    string CancellationReason,
    DateTimeOffset CancelledAt,
    string Version,
    bool IsIdempotentReplay);
