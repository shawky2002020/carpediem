# Technical Research & Architectural Findings: SLICE-08 — Chief Leader Team Selection from Available Pool

## 1. Context & Architectural Positioning

SLICE-08 represents the team staffing phase of Milestone M1. Following Event publication (`SLICE-06`) and Volunteer Availability declaration (`SLICE-07`), the assigned Chief Leader selects team members from the pool of volunteers who declared `Available` in the event's current active `StaffingCycle`.

### Architecture Boundaries (`MODULE_BOUNDARIES.md` Section 3)
- **Owning Module**: `Carpediem.Modules.Staffing`.
- **Entities**:
  - `TeamAssignment` in schema `staffing`.
- **Collaborating Modules**:
  - `Carpediem.Modules.Events`: Provides Event status verification (`Status == Published`), schedule temporal window verification (`StartDateTime > UtcNow`), active `StaffingCycle` reference (`IsCurrent == true`), and verifies acting user is the active Chief Leader (`ChiefLeaderAssignment.Status == Active`).
  - `Carpediem.Modules.Identity`: Resolves candidate names/usernames and verifies role qualifications.
  - `Carpediem.Modules.Notifications`: Creates in-platform notification intent (`VolunteerSelected`) for the selected volunteer per ADR-006.
  - `Carpediem.Infrastructure.Audit`: Appends immutable audit records (`Action = 'VolunteerSelected'`) per ADR-005.

---

## 2. Domain Invariants & Guard Rules

1. **Authorization & Chief Leader Verification**:
   - The acting user must either be the active Chief Leader on Event $E$ (`ChiefLeaderAssignment.Status == Active` and `UserId == actingUserId`) or an Operations Administrator (`Admin` role).
   - Other users (even Leaders or Chief Leaders on other events) receive HTTP 403 Forbidden (`unauthorized_chief_leader`).
2. **Event Status Invariant**:
   - Team selection is permitted ONLY when `Event.Status == EventStatus.Published`.
   - If Event is `Draft`, `Completed`, or `Cancelled`, selection is rejected with HTTP 409 Conflict (`invalid_event_status`).
3. **Temporal Window Invariant**:
   - Team selection is permitted strictly while `Event.StartDateTime > DateTimeOffset.UtcNow`.
   - Once event start time arrives or passes, requests are rejected with HTTP 409 Conflict (`window_closed`).
4. **CHANGE-001 Same-Event Exclusivity Invariant**:
   - A user who is the active Chief Leader for Event $E$ cannot be selected as a volunteer team member on Event $E$.
   - Any attempt to select the active Chief Leader is rejected with HTTP 409 Conflict (`chief_leader_conflict`).
5. **Candidate Availability Eligibility Invariant**:
   - Candidates must have an `Available` response in the current active `StaffingCycle` (`IsCurrent == true`).
   - If a candidate has no response or marked `NotAvailable`, the selection is rejected with HTTP 409 Conflict (`candidate_not_available`).
6. **Decision Group 5 Immediate Active Roster Placement**:
   - Selection immediately creates an `Active` `TeamAssignment`. No intermediate "Invited" or "Pending Confirmation" state exists.
7. **Single Active Assignment per Cycle Invariant**:
   - Filtered unique index `UX_TeamAssignments_ActiveLeaderPerCycle` on `(StaffingCycleId, UserId)` WHERE `Status = 'Active'` guarantees no candidate can be placed more than once on the active roster in the same cycle.

---

## 3. Concurrency, Indexing & Storage Strategy (ADR-009)

### Persistence Schema: `staffing.TeamAssignments`
- `Id`: `uniqueidentifier` NOT NULL PRIMARY KEY
- `StaffingCycleId`: `uniqueidentifier` NOT NULL FK to `events.StaffingCycles(Id)`
- `EventId`: `uniqueidentifier` NOT NULL FK to `events.Events(Id)`
- `UserId`: `uniqueidentifier` NOT NULL FK to `identity.Users(Id)`
- `Status`: `nvarchar(32)` NOT NULL (`'Active'` or `'Cancelled'`)
- `AssignedAt`: `datetimeoffset(7)` NOT NULL
- `AssignedByUserId`: `uniqueidentifier` NOT NULL FK to `identity.Users(Id)`
- `CancellationReason`: `nvarchar(500)` NULL (reserved for `SLICE-10`)
- `CancelledAt`: `datetimeoffset(7)` NULL (reserved for `SLICE-10`)
- `RowVersion`: `rowversion` NOT NULL

### Database Indexes:
- **`UX_TeamAssignments_ActiveLeaderPerCycle`**: Filtered unique index on `(StaffingCycleId, UserId)` WHERE `Status = 'Active'` ensuring a Leader cannot hold duplicate active assignments on the same event cycle.
- **`IX_TeamAssignments_Event_Status`**: Non-clustered index on `(EventId, Status)` for fast active team roster retrieval.

### Idempotency & Concurrency:
- If a client replays an identical selection request for a candidate who is already active on the team roster from an earlier request by the same actor, the server detects matching state and returns HTTP 200 OK with `Idempotent-Replay: true` without appending duplicate audit logs or duplicate notifications.
- Atomic SQL Server transaction with RCSI guarantees consistency across `staffing.TeamAssignments`, `notifications.InPlatformNotifications`, and `audit.AuditLogs`.

---

## 4. Notifications & Audit Mechanics

1. **In-Platform Notification**:
   - When a volunteer is selected, an `InPlatformNotification` record is created:
     - `NotificationType = 'VolunteerSelected'`
     - `RecipientUserId = candidateUserId`
     - `EventId = eventId`
     - `ContextPayload`: summary with Event title and Cairo schedule.
2. **Audit Logging**:
   - `AuditLog.Record(...)` appends:
     - `Action = 'VolunteerSelected'`
     - `ResourceType = 'TeamAssignment'`
     - `ResourceId = teamAssignment.Id`
     - `ActorUserId = actingUserId`
     - `AuditSource = (actingUserIsAdmin ? 'AdminSelection' : 'Direct')`
     - `NewValue = 'Active'`
