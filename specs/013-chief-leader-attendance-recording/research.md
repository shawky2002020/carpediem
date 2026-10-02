# Architectural Research: SLICE-15 — Chief Leader On-Site Attendance Recording

## 1. Domain & Architecture Decisions

### Decision 1: Pure Absence Semantics vs. Persisted 'Unrecorded' Enum
- **Option A**: Add an `Unrecorded` value to `AttendanceOutcome` enum and insert rows for all active volunteers when a cycle is created.
  - *Cons*: Bloats database with placeholder rows, requires cascading inserts/deletes whenever replacements occur or volunteers self-cancel, violates the sparse normalization guidelines in `LOGICAL_DATA_MODEL.md`.
- **Option B (Selected - Ratified in ADR-009 & LOGICAL_DATA_MODEL.md)**: Unrecorded attendance is represented strictly by the **absence of an `attendance.AttendanceOutcomes` row** for a given `TeamAssignmentId`.
  - *Pros*: Zero row overhead until roll call occurs. Clean decoupled model. An assignment created 5 minutes before the event start seamlessly participates without background provisioning. The UI renders missing outcomes as "Unrecorded".

### Decision 2: Decoupled Lifecycle Boundary
- **Ratification**: In `SEQ-008` and `V1_DEPENDENCY_GRAPH.md`, attendance recording is decoupled from event completion.
- A Chief Leader may record attendance while the event is `Published` (e.g., as volunteers arrive on-site) or after the event has completed (`Completed`).
- If an event concludes without attendance, the derived operational flag `Pending Attendance` is calculated at request time:
  ```csharp
  // Derived condition:
  bool hasPendingAttendance = @event.Status == EventStatus.Completed &&
      await db.TeamAssignments.AnyAsync(a => a.EventId == @event.Id &&
                                             a.StaffingCycleId == currentCycle.Id &&
                                             a.Status == TeamAssignmentStatus.Active &&
                                             !db.AttendanceOutcomes.Any(o => o.TeamAssignmentId == a.Id));
  ```

### Decision 3: Authorization Strategy (ADR-003 Tier 2)
- Endpoint authorization: `AuthorizationPolicies.RequireChiefLeaderOrAdmin`.
- Resource-level verification in Application Store:
  - If the actor is an Administrator (`CarpediemRoles.Admin`), access is permitted.
  - If the actor is not an Admin, they must have an `Active` `ChiefLeaderAssignment` on the target event (`Status == ChiefLeaderAssignmentStatus.Active`).
  - If not active Chief Leader, throw `UnauthorizedAccessException` yielding `403 Forbidden` (`unauthorized_chief_leader`).

### Decision 4: Concurrency & Replay Semantics
- Table `attendance.AttendanceOutcomes` contains `RowVersion` (`rowversion` / `byte[]`).
- Submissions are atomic. If roll call is submitted multiple times (e.g., Chief Leader adds late arrivals or updates status), existing records update their outcome, and new records are inserted.
- Submissions are idempotent: submitting identical outcomes produces the same final state and returns `200 OK`.

---

## 2. Telemetry & Observability
- Source: `Carpediem.Attendance` ActivitySource.
- Activities:
  - `GetRollSheet`: tracks fetch duration and active volunteer counts.
  - `FinalizeAttendance`: tracks finalization duration, attended counts, and no-show counts.
- Metrics:
  - `attendance.outcomes.recorded`: counter tagged with `outcome` (`Attended` | `NoShow`) and `role` (`ChiefLeader` | `Admin`).
