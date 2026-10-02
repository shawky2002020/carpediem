# Data Model: SLICE-11 — Replacement Leader Assignment

**Feature Branch**: `codex/010-replacement-leader-assignment`  
**Date**: 2026-10-02  
**Status**: Completed  

---

## 1. Entity Lifecycle & State Transitions

### `staffing.TeamAssignments` Lifecycle

```
[Initial Pool / Declared Available]
               │
               ▼
   [New TeamAssignment Created]  <─── SLICE-08 (Selection) or SLICE-09 (Direct)
               │
               ▼
            [Active]
               │
               ▼ SLICE-10 (Self-Cancellation or Admin Cancellation)
          [Cancelled] (Immutable historical record)
               │
               │ (Reopens cycle slot via filtered index)
               ▼
   [SLICE-11: Replacement Selection]
               │
               ▼
   [Brand New Distinct TeamAssignment: Active]
```

---

## 2. Relational Schema & Constraints

### `staffing.TeamAssignments` (Existing Schema)

| Column | Type | Nullable | Description |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | No | Primary Key |
| `EventId` | `uniqueidentifier` | No | Foreign Key to `events.Events` |
| `StaffingCycleId` | `uniqueidentifier` | No | Foreign Key to `events.StaffingCycles` |
| `UserId` | `uniqueidentifier` | No | Foreign Key to `identity.Users` (The assigned volunteer leader) |
| `Status` | `nvarchar(32)` | No | Placement status: `'Active'`, `'Cancelled'` |
| `AssignedAt` | `datetimeoffset` | No | UTC timestamp when placement was created |
| `AssignedByUserId`| `uniqueidentifier` | No | User ID of the actor (Chief Leader or Admin) |
| `CancellationReason` | `nvarchar(500)` | Yes | Mandatory justification provided upon cancellation |
| `CancelledAt` | `datetimeoffset` | Yes | UTC timestamp of cancellation |
| `RowVersion` | `rowversion` | No | Optimistic concurrency token |

**Indexes**:
- `UX_TeamAssignments_ActiveLeaderPerCycle` (`StaffingCycleId`, `UserId`) `WHERE [Status] = 'Active'` (Unique).
- `IX_TeamAssignments_EventId_Status` (`EventId`, `Status`).

---

## 3. Audit Log Model (`audit.AuditLogs`)

| Field | Value |
| :--- | :--- |
| `Action` | `"ReplacementLeaderAssigned"` |
| `ActorUserId` | Chief Leader or Admin user ID |
| `ResourceId` | Newly created `TeamAssignment.Id` |
| `ResourceType` | `"TeamAssignment"` |
| `MandatoryReason` | User-provided justification or `"Drafted as replacement leader"` |
| `Source` | `"ChiefLeaderReplacement"` or `"AdminOverrideReplacement"` |
| `NewValue` | JSON representation of new `TeamAssignment` |
| `PreviousValue` | JSON representation of linked cancelled `TeamAssignment` (if provided), or `null` |

---

## 4. Notification Intent Model (`notifications.InPlatformNotifications`)

| Field | Value |
| :--- | :--- |
| `RecipientUserId` | Replacement Leader's user ID |
| `NotificationType` | `"ReplacementLeaderAssigned"` |
| `EventId` | Target Event ID |
| `ContextPayload` | JSON `{ eventId, eventTitle, assignedAt, assignedByUserName }` |
