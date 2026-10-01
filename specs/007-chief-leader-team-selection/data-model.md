# Data Model: SLICE-08 — Chief Leader Team Selection from Available Pool

## 1. Relational Entities

### `staffing.TeamAssignments`
Stores the operational team assignments of volunteer Leaders to events in the active `StaffingCycle`.

```sql
CREATE TABLE [staffing].[TeamAssignments] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [StaffingCycleId] UNIQUEIDENTIFIER NOT NULL,
    [EventId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [Status] NVARCHAR(32) NOT NULL,
    [AssignedAt] DATETIMEOFFSET(7) NOT NULL,
    [AssignedByUserId] UNIQUEIDENTIFIER NOT NULL,
    [CancellationReason] NVARCHAR(500) NULL,
    [CancelledAt] DATETIMEOFFSET(7) NULL,
    [RowVersion] ROWVERSION NOT NULL,
    CONSTRAINT [PK_TeamAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TeamAssignments_StaffingCycles_StaffingCycleId] FOREIGN KEY ([StaffingCycleId]) REFERENCES [events].[StaffingCycles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TeamAssignments_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [events].[Events] ([Id]),
    CONSTRAINT [FK_TeamAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]),
    CONSTRAINT [FK_TeamAssignments_Users_AssignedByUserId] FOREIGN KEY ([AssignedByUserId]) REFERENCES [identity].[Users] ([Id])
);

-- Unique filtered index preventing duplicate active assignments per leader per cycle
CREATE UNIQUE NONCLUSTERED INDEX [UX_TeamAssignments_ActiveLeaderPerCycle]
    ON [staffing].[TeamAssignments] ([StaffingCycleId], [UserId])
    WHERE [Status] = N'Active';

-- Lookup index for team roster queries by event
CREATE NONCLUSTERED INDEX [IX_TeamAssignments_Event_Status]
    ON [staffing].[TeamAssignments] ([EventId], [Status]);
```

---

## 2. Domain Model & Enumerations

### `TeamAssignmentStatus` (Enum)
```csharp
namespace Carpediem.Modules.Staffing.Domain.TeamSelection;

public enum TeamAssignmentStatus
{
    Active = 1,
    Cancelled = 2
}
```

### `TeamAssignment` (Entity / Aggregate Root)
```csharp
namespace Carpediem.Modules.Staffing.Domain.TeamSelection;

public sealed class TeamAssignment
{
    public Guid Id { get; private set; }
    public Guid StaffingCycleId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public TeamAssignmentStatus Status { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public Guid AssignedByUserId { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static TeamAssignment Create(
        Guid staffingCycleId,
        Guid eventId,
        Guid candidateUserId,
        Guid assignedByUserId,
        DateTimeOffset assignedAt)
    {
        // Enforce invariants: valid IDs, UTC timestamp
        return new TeamAssignment
        {
            Id = Guid.NewGuid(),
            StaffingCycleId = staffingCycleId,
            EventId = eventId,
            UserId = candidateUserId,
            Status = TeamAssignmentStatus.Active,
            AssignedAt = assignedAt,
            AssignedByUserId = assignedByUserId
        };
    }
}
```

---

## 3. Query Models (DTOs)

### `TeamSelectionContextResponse`
```json
{
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "staffingCycleId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventTitle": "Weekly Youth Gathering",
  "startDateTime": "2030-10-10T14:30:00Z",
  "endDateTime": "2030-10-10T16:30:00Z",
  "location": "Main Hall",
  "isWindowOpen": true,
  "availableCandidates": [
    {
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "userName": "leader.alex",
      "declaredAt": "2026-10-01T18:00:00Z"
    }
  ],
  "activeTeam": [
    {
      "assignmentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "userName": "leader.sam",
      "assignedAt": "2026-10-01T18:30:00Z",
      "version": "AAAAAAAAF2M="
    }
  ]
}
```

### `SelectVolunteerRequest`
```json
{
  "candidateUserId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

### `TeamAssignmentResponse`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "staffingCycleId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Active",
  "assignedAt": "2026-10-01T18:30:00Z",
  "assignedByUserId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "version": "AAAAAAAAF2M="
}
```
