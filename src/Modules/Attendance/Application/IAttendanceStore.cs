namespace Carpediem.Modules.Attendance.Application;

public sealed record AttendanceRollSheetResult(
    Guid EventId,
    string EventTitle,
    string Status,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    Guid StaffingCycleId,
    bool IsCompleted,
    bool IsAllFinalized,
    IReadOnlyList<AttendanceRollSheetEntryResult> Entries);

public sealed record AttendanceRollSheetEntryResult(
    Guid TeamAssignmentId,
    Guid VolunteerUserId,
    string VolunteerName,
    Guid? AttendanceOutcomeId,
    string? Outcome,
    DateTimeOffset? FinalizedAt,
    byte[]? RowVersion);

public sealed record FinalizeAttendanceItem(
    Guid TeamAssignmentId,
    string Outcome);

public sealed record FinalizeAttendanceResult(
    Guid EventId,
    DateTimeOffset FinalizedAt,
    int TotalActiveAssignments,
    int AttendedCount,
    int NoShowCount,
    int UnrecordedCount);

public sealed record CorrectAttendanceResult(
    Guid AttendanceOutcomeId,
    Guid TeamAssignmentId,
    string PreviousOutcome,
    string NewOutcome,
    DateTimeOffset CorrectedAt,
    string RowVersionBase64);

public interface IAttendanceStore
{
    Task<AttendanceRollSheetResult?> GetRollSheetAsync(
        Guid eventId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<FinalizeAttendanceResult> FinalizeAttendanceAsync(
        Guid eventId,
        IReadOnlyList<FinalizeAttendanceItem> submissions,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    Task<CorrectAttendanceResult> CorrectAttendanceAsync(
        Guid attendanceOutcomeId,
        string newOutcome,
        string mandatoryReason,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset utcNow,
        byte[]? expectedRowVersion = null,
        CancellationToken cancellationToken = default);
}
