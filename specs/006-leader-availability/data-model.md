# Data Model: SLICE-07 — Leader Availability Declaration

## 1. Overview

SLICE-07 introduces the `staffing.AvailabilityResponses` entity to record volunteer Leaders' schedule availability for a published event within its active `StaffingCycle`.

---

## 2. Relational Schema & Entities

### `staffing.AvailabilityResponses`

| Column | Type | Nullability | Constraints / Description |
|:---|:---|:---|:---|
| `Id` | `uniqueidentifier` | NOT NULL | Primary Key (`PK_AvailabilityResponses`) |
| `StaffingCycleId` | `uniqueidentifier` | NOT NULL | Foreign Key to `events.StaffingCycles(Id)` (`FK_AvailabilityResponses_StaffingCycles`) |
| `EventId` | `uniqueidentifier` | NOT NULL | Foreign Key to `events.Events(Id)` (`FK_AvailabilityResponses_Events`) |
| `UserId` | `uniqueidentifier` | NOT NULL | Foreign Key to `identity.Users(Id)` (`FK_AvailabilityResponses_Users`) |
| `Response` | `nvarchar(32)` | NOT NULL | Enum: `'Available'`, `'NotAvailable'` (`CK_AvailabilityResponses_Response`) |
| `DeclaredAt` | `datetimeoffset(7)` | NOT NULL | Timestamp when declared / updated |
| `RowVersion` | `rowversion` | NOT NULL | Optimistic concurrency token |

### Indexes & Uniqueness Constraints:
1. **`UX_AvailabilityResponses_StaffingCycle_User`**:
   - `UNIQUE NONCLUSTERED INDEX ([StaffingCycleId], [UserId])`
   - Guarantees at most one availability response per volunteer per staffing cycle.
2. **`IX_AvailabilityResponses_Event_Response`**:
   - `NONCLUSTERED INDEX ([EventId], [Response]) INCLUDE ([UserId], [DeclaredAt])`
   - Optimizes Chief Leader queries when fetching the available candidate pool in `SLICE-08`.

---

## 3. Related Entities & Read Verification

### `events.Events`
- Read-only check:
  - `Status == EventStatus.Published`
  - `StartDateTime > DateTimeOffset.UtcNow`
  - If not published, reject with HTTP 409 Conflict (`invalid_event_status`).
  - If past start time, reject with HTTP 409 Conflict (`window_closed`).

### `events.ChiefLeaderAssignments`
- Read-only check (CHANGE-001 Same-Event Exclusivity Invariant):
  - Check if `ChiefLeaderAssignments` has any record where `EventId == targetEventId`, `ChiefLeaderUserId == actingUserId`, and `Status == 'Active'`.
  - If true: reject with HTTP 409 Conflict (`chief_leader_conflict`).

### `events.StaffingCycles`
- Read-only query:
  - Query current active cycle where `EventId == targetEventId` and `IsCurrent == 1`.
  - Assigns `StaffingCycleId` for the availability response.

### `audit.AuditLogs`
- One row appended per declaration / update:
  - `Id`: New GUID
  - `OccurredAt`: UTC declaration timestamp
  - `ActorUserId`: Acting Leader GUID
  - `Action`: `'AvailabilityDeclared'`
  - `ResourceType`: `'AvailabilityResponse'`
  - `ResourceId`: `AvailabilityResponse.Id`
  - `Source`: `'Direct'`
  - `PreviousValue`: Previous response string (or `'None'`)
  - `NewValue`: `'Available'` or `'NotAvailable'`

---

## 4. Transaction Boundary

All database mutations execute inside an EF Core execution strategy transaction with `IsolationLevel.ReadCommitted`:
1. Verify Event is `Published` and `StartDateTime > UtcNow`.
2. Verify acting user is NOT the active Chief Leader on this Event.
3. Query current active `StaffingCycle`.
4. Upsert `AvailabilityResponse` with optimistic concurrency check (`RowVersion`).
5. Append `AuditLog` entry.
6. Commit transaction atomically.
