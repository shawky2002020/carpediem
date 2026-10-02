# Data Model: SLICE-10 — Leader Self-Cancellation with Mandatory Justification

## 1. Entity State Lifecycle: `staffing.TeamAssignments`

```
  +-------------------------------------------------------+
  |                   [ No Assignment ]                   |
  +-------------------------------------------------------+
                              |
                              | SLICE-08 (Select Volunteer) or
                              | SLICE-09 (Direct Assign Leader)
                              v
  +-------------------------------------------------------+
  |                        Active                         |
  |  - Status = Active                                    |
  |  - AssignedAt = UTC                                   |
  |  - CancelledAt = NULL                                 |
  |  - CancellationReason = NULL                          |
  +-------------------------------------------------------+
                              |
                              | SLICE-10: Self-Cancel (Leader) or
                              |           Admin Override (Admin)
                              v
  +-------------------------------------------------------+
  |                      Cancelled                        |
  |  - Status = Cancelled                                 |
  |  - CancelledAt = UTC                                  |
  |  - CancellationReason = Non-empty text (max 500)      |
  +-------------------------------------------------------+
                              |
                              | (Permanent Terminal State;
                              |  Never deleted, never reactivated)
                              v
  +-------------------------------------------------------+
  |              Preserved Factual History                |
  +-------------------------------------------------------+
```

## 2. Table Schema: `staffing.TeamAssignments`

| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | Primary Key, NOT NULL | Assignment UUID |
| `StaffingCycleId` | `uniqueidentifier` | NOT NULL | Associated staffing cycle |
| `EventId` | `uniqueidentifier` | NOT NULL | Associated published event |
| `UserId` | `uniqueidentifier` | NOT NULL | Assigned volunteer leader |
| `Status` | `nvarchar(32)` | NOT NULL, CHECK in ('Active','Cancelled') | Lifecycle status |
| `AssignedAt` | `datetimeoffset(7)` | NOT NULL | UTC assignment timestamp |
| `AssignedByUserId` | `uniqueidentifier` | NOT NULL | User who made assignment |
| `CancellationReason` | `nvarchar(500)` | NULL | Mandatory cancellation reason |
| `CancelledAt` | `datetimeoffset(7)` | NULL | UTC cancellation timestamp |
| `RowVersion` | `rowversion` | NOT NULL | Optimistic concurrency token |

### Existing Indexes:
- `UX_TeamAssignments_ActiveLeaderPerCycle`: Unique filtered index on `(StaffingCycleId, UserId)` WHERE `[Status] = 'Active'`.
  *Note*: When an assignment transitions to `Cancelled`, this unique filtered index automatically stops indexing it, which re-enables subsequent replacement assignment in `SLICE-11`!
- `IX_TeamAssignments_Event_Status`: Index on `(EventId, Status)`.

---

## 3. In-Platform Notifications: `notifications.InPlatformNotifications`

- `NotificationType`: `'TeamMemberCancelled'`
- `RecipientUserId`: Active Chief Leader ID and each active Admin ID
- `EventId`: Published Event UUID
- `ContextPayload`: JSON string:
  ```json
  {
    "eventTitle": "Summer Festival 2026",
    "assignmentId": "b1a2c3...",
    "volunteerName": "Sarah Connor",
    "cancellationReason": "Unavoidable academic conflict."
  }
  ```

---

## 4. Audit Log Schema: `audit.AuditLogs`

- `Action`: `'VolunteerSelfCancelled'`
- `ResourceType`: `'TeamAssignment'`
- `ResourceId`: `TeamAssignment.Id`
- `ActorUserId`: User ID of caller (Volunteer or Admin)
- `PreviousValue`: `'Active'`
- `NewValue`: `'Cancelled'`
- `MandatoryReason`: Submitter's justification reason
- `Source`: `'VolunteerSelfCancellation'` (when owner) or `'AdminOverrideCancellation'` (when Admin)
