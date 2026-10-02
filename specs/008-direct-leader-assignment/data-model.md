# Data Model: SLICE-09 — Direct Leader Assignment

## 1. Relational Entities & Mappings

Direct leader assignment utilizes the existing `staffing.TeamAssignments` table created in `SLICE-08`, persisting rows in `Status = TeamAssignmentStatus.Active`.

### `staffing.TeamAssignments`
- `Id` (`uniqueidentifier`, PK)
- `EventId` (`uniqueidentifier`, FK -> `events.Events.Id`)
- `StaffingCycleId` (`uniqueidentifier`, FK -> `events.StaffingCycles.Id`)
- `UserId` (`uniqueidentifier`, FK -> `identity.AspNetUsers.Id`)
- `Status` (`nvarchar(32)`, value `'Active'`)
- `AssignedAt` (`datetimeoffset`)
- `AssignedByUserId` (`uniqueidentifier`, FK -> `identity.AspNetUsers.Id`)
- `Version` (`rowversion`, concurrency token)

### Filtered Unique Index
```sql
CREATE UNIQUE NONCLUSTERED INDEX [UX_TeamAssignments_ActiveLeaderPerCycle]
ON [staffing].[TeamAssignments] ([StaffingCycleId], [UserId])
WHERE [Status] = 'Active';
```
This index guarantees that no Leader can have duplicate active assignments in the same cycle.

---

## 2. In-Platform Notifications

Direct leader assignment inserts into `notifications.InPlatformNotifications`:
- `RecipientUserId`: Candidate `UserId`
- `Type`: `'DirectLeaderAssigned'` (or `'VolunteerSelected'`)
- `Title`: `"Assigned to Event Team"`
- `Message`: `"You have been directly assigned to the team for {EventTitle}."`
- `DataJson`: `{"EventId":"...","StaffingCycleId":"...","AssignmentId":"..."}`
- `CreatedAt`: UTC timestamp

---

## 3. Audit Trail

Direct leader assignment inserts into `audit.AuditLogs`:
- `ActorUserId`: Acting Chief Leader or Administrator ID
- `Action`: `'DirectLeaderAssignmentExecuted'`
- `ResourceType`: `'TeamAssignment'`
- `ResourceId`: `TeamAssignment.Id`
- `Timestamp`: UTC timestamp
- `Source`: `'ChiefLeaderDirectDraft'` or `'AdminDirectDraft'`
- `Details`: JSON object recording prior availability state:
  ```json
  {
    "PriorAvailability": "NotAvailable" | "NoResponse",
    "CandidateUserId": "...",
    "EventId": "...",
    "StaffingCycleId": "..."
  }
  ```
