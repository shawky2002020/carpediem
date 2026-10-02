# Data Model: SLICE-15 — Chief Leader On-Site Attendance Recording

## 1. Domain Entities & Value Objects

### `AttendanceOutcome` Aggregate (`src/Modules/Attendance/Domain/AttendanceOutcome.cs`)
```csharp
namespace Carpediem.Modules.Attendance.Domain;

public enum AttendanceOutcomeValue
{
    Attended = 1,
    NoShow = 2
}

public sealed class AttendanceOutcome
{
    private AttendanceOutcomeValue _outcome;

    private AttendanceOutcome() { }

    public Guid Id { get; private set; }
    public Guid TeamAssignmentId { get; private set; }
    public AttendanceOutcomeValue Outcome => _outcome;
    public Guid FinalizedByUserId { get; private set; }
    public DateTimeOffset FinalizedAt { get; private set; }
    public Guid? CorrectedByUserId { get; private set; }
    public DateTimeOffset? CorrectedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static AttendanceOutcome Create(
        Guid teamAssignmentId,
        AttendanceOutcomeValue outcome,
        Guid finalizedByUserId,
        DateTimeOffset finalizedAt)
    {
        if (teamAssignmentId == Guid.Empty) throw new ArgumentException("TeamAssignmentId cannot be empty.", nameof(teamAssignmentId));
        if (finalizedByUserId == Guid.Empty) throw new ArgumentException("FinalizedByUserId cannot be empty.", nameof(finalizedByUserId));
        if (!Enum.IsDefined(outcome)) throw new ArgumentException("Invalid outcome value.", nameof(outcome));

        return new AttendanceOutcome
        {
            Id = Guid.NewGuid(),
            TeamAssignmentId = teamAssignmentId,
            _outcome = outcome,
            FinalizedByUserId = finalizedByUserId,
            FinalizedAt = finalizedAt
        };
    }

    public void UpdateOutcome(AttendanceOutcomeValue newOutcome, Guid updatedByUserId, DateTimeOffset updatedAt)
    {
        if (!Enum.IsDefined(newOutcome)) throw new ArgumentException("Invalid outcome value.", nameof(newOutcome));
        _outcome = newOutcome;
        FinalizedByUserId = updatedByUserId;
        FinalizedAt = updatedAt;
    }
}
```

---

## 2. Relational Schema & EF Core Mapping

### Schema: `attendance`
### Table: `attendance.AttendanceOutcomes`
- `Id`: `uniqueidentifier` PK, `NEWSEQUENTIALID()`
- `TeamAssignmentId`: `uniqueidentifier` NOT NULL, Unique Index `UX_AttendanceOutcomes_Assignment`, FK to `staffing.TeamAssignments(Id)`
- `Outcome`: `varchar(20)` NOT NULL (Check: `Outcome IN ('Attended', 'NoShow')`)
- `FinalizedByUserId`: `uniqueidentifier` NOT NULL, FK to `identity.Users(Id)`
- `FinalizedAt`: `datetimeoffset` NOT NULL
- `CorrectedByUserId`: `uniqueidentifier` NULL, FK to `identity.Users(Id)`
- `CorrectedAt`: `datetimeoffset` NULL
- `RowVersion`: `rowversion` NOT NULL

---

## 3. DTO Contracts

### Roll Sheet Response (`AttendanceRollSheetResponse`)
```csharp
public sealed record AttendanceRollSheetResponse(
    Guid EventId,
    string EventTitle,
    string Status,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    Guid StaffingCycleId,
    bool IsCompleted,
    bool IsAllFinalized,
    IReadOnlyList<AttendanceRollSheetEntryResponse> Entries);

public sealed record AttendanceRollSheetEntryResponse(
    Guid TeamAssignmentId,
    Guid VolunteerUserId,
    string VolunteerName,
    string? Outcome, // "Attended", "NoShow", or null (Unrecorded)
    DateTimeOffset? FinalizedAt,
    string? Version);
```

### Roll Call Finalization Request (`FinalizeAttendanceRequest`)
```csharp
public sealed record FinalizeAttendanceRequest(
    IReadOnlyList<AttendanceOutcomeSubmission> Entries);

public sealed record AttendanceOutcomeSubmission(
    Guid TeamAssignmentId,
    string Outcome); // "Attended" or "NoShow"
```

### Roll Call Finalization Response (`FinalizeAttendanceResponse`)
```csharp
public sealed record FinalizeAttendanceResponse(
    Guid EventId,
    DateTimeOffset FinalizedAt,
    int TotalActiveAssignments,
    int AttendedCount,
    int NoShowCount,
    int UnrecordedCount);
```
