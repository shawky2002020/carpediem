# Sequence 008: Attendance Finalization, Pending Attendance & Administrative Correction

* **Context**: Ground-truth attendance recording by the on-site Chief Leader, decoupled event completion, evaluation of the `Pending Attendance` operational condition, and audited post-event Admin corrections.
* **Lifecycle Transitions Covered**:
  - `AttendanceOutcome` (Dimension C): `Unrecorded` $\rightarrow$ `Attended` / `NoShow`
  - `AttendanceOutcome` (Correction): `Attended` $\leftrightarrow$ `NoShow` (via Audited Admin Override with Mandatory Reason)
  - `Event`: Evaluates non-persisted operational condition `Pending Attendance` if attendance remains unfinalized after conclusion.
* **Authoritative Inputs**:
  - `LIFECYCLE_SPECIFICATION.md` (Sections 2.B, 6, & 7 Normalization Correction 3)
  - `ADR-003` (Authorization: Tier 2 `EventChiefLeaderRequirement` & `AdminOverrideRequirement`)
  - `ADR-005` (Audit Trail Strategy — Mandatory Correction Justification)
  - `ADR-009` (SQL Server Persistence & Schema Organization)

---

## 1. Sequence Overview

This sequence illustrates the operational finalization of attendance and administrative corrections:
1. **Initial State (Unrecorded Attendance)**:
   - For every active team placement on the roster, an `AttendanceOutcome` record exists in initial status `Unrecorded`.
2. **Decoupled Event Completion & `Pending Attendance`**:
   - In accordance with the ratified lifecycle, an Event transitions to `Completed` when its scheduled time window concludes (`Now > EndTime`) or via explicit Admin closeout.
   - **Operational Decoupling (Approved Rule)**: Attendance submission is **not** a prerequisite for Event completion. An event can finish/complete even if attendance has not yet been finalized.
   - If one or more active team assignments still have `AttendanceOutcome == Unrecorded`, the system surfaces a non-persisted, derived operational condition: **`Pending Attendance`** on Admin and Chief Leader dashboards.
3. **Chief Leader Attendance Finalization**:
   - The on-site Chief Leader conducts roll call and finalizes attendance by submitting outcomes (`Attended` or `NoShow`) for all active roster members.
   - **Immediate Finalization (Approved Rule)**: Submission takes immediate effect in the database. There is **no routine Admin approval queue** or secondary review gate.
   - Submitting final roll call clears the `Pending Attendance` operational condition.
4. **Administrative Audited Correction**:
   - If an error occurred (e.g., a Leader was marked `NoShow` but actually served in a different area), an Admin executes an audited correction.
   - The Admin specifies the target record, the new outcome, and a **mandatory justification reason** (Approved Rule).
   - The system updates the outcome and appends an immutable audit log recording the prior value, new value, acting Admin, timestamp, and justification.

---

## 2. Sequence Diagram

```mermaid
sequenceDiagram
    autonumber
    actor CL as Active Chief Leader
    actor Admin as Operations Admin
    participant SPA as Angular Web App
    participant Host as ASP.NET Core API
    participant ModAuthz as ADR-003 Authz
    participant ModAttend as Attendance Module
    participant ModStaffing as Staffing Module
    participant ModEvents as Events Module
    participant Audit as Audit Persistence
    participant DB as Persistence Store

    %% ------------------------------------------------------------------------
    %% PHASE 1: EVENT COMPLETION & PENDING ATTENDANCE CONDITION
    %% ------------------------------------------------------------------------
    rect rgb(245, 245, 255)
    Note over Admin, DB: PHASE 1: Event Concluded (Decoupled from Attendance)
    Note over ModEvents, DB: Scheduled window concluded or Admin closeout => Event is 'Completed'
    Note over ModEvents, DB: Attendance is NOT yet submitted; Active assignments are 'Unrecorded'
    Admin->>SPA: View Operations Dashboard
    SPA->>Host: GET /api/events/dashboard-summary
    Host->>ModEvents: GetRecentEvents()
    Host->>ModAttend: HasUnfinalizedAttendance(EventId)
    ModAttend->>DB: Query Count of Unrecorded Attendance Outcomes for Event
    DB-->>ModAttend: Unfinalized Count > 0
    ModAttend-->>Host: HasPendingAttendance = True
    Host-->>SPA: DashboardDto (Event.Status='Completed', OperationalFlag='Pending Attendance')
    SPA-->>Admin: Display Warning Badge: "Event Completed — Pending Attendance Submission"
    end

    %% ------------------------------------------------------------------------
    %% PHASE 2: CHIEF LEADER ATTENDANCE FINALIZATION
    %% ------------------------------------------------------------------------
    rect rgb(240, 255, 240)
    Note over CL, DB: PHASE 2: Chief Leader Roll Call Finalization (On-Site)
    CL->>SPA: Open Attendance Roll Sheet for Event
    SPA->>Host: GET /api/events/{id}/attendance
    Host->>ModAuthz: Tier 2 Check (EventChiefLeaderRequirement)
    ModAuthz-->>Host: Authorized
    Host->>ModAttend: GetRollSheet(EventId)
    ModAttend->>ModStaffing: GetActiveTeamRoster(EventId)
    ModStaffing-->>ModAttend: Active Team Assignments
    ModAttend-->>Host: RollSheetDto
    Host-->>SPA: 200 OK (Roll Sheet)
    
    CL->>SPA: Mark Outcomes ("Attended" / "No-Show") & Click "Finalize Attendance"
    SPA->>Host: POST /api/events/{id}/attendance/finalize (Entries: [{Id, Status}])
    Host->>ModAuthz: Tier 2 Check (EventChiefLeaderRequirement)
    ModAuthz-->>Host: Authorized (Active Chief Leader for Event)
    Host->>ModAttend: FinalizeAttendance(EventId, Entries, CL_UserId)
    
    Note over ModAttend, DB: Invariant: Immediate Effect! NO ROUTINE ADMIN APPROVAL GATE
    Note over ModAttend, DB: Transaction 1: Atomic Roll Sheet Submission + Audit Evidence
    ModAttend->>DB: Batch Update AttendanceOutcomes (Status=FinalValues, FinalizedAt=UtcNow)
    ModAttend->>Audit: Record Required Audit Evidence (Action='Attendance.Finalized', Actor=CL_UserId, Target=EventId)
    ModAttend-->>Host: FinalizationResult (FinalizedCount)
    Host-->>SPA: 200 OK (FinalizedCount)
    SPA-->>CL: Confirmation: "Attendance officially finalized"
    Note over Admin, SPA: Dashboard updates: 'Pending Attendance' condition cleared
    end

    %% ------------------------------------------------------------------------
    %% PHASE 3: ADMIN AUDITED CORRECTION
    %% ------------------------------------------------------------------------
    rect rgb(255, 245, 235)
    Note over Admin, DB: PHASE 3: Administrative Audited Correction
    Admin->>SPA: Open Event Attendance Record for Volunteer Leader X (Status: NoShow)
    SPA->>Admin: Require Correction Justification (Mandatory)
    Admin->>SPA: Select New Status ("Attended") & Enter Reason ("Transit delay; served full shift")
    SPA->>Host: POST /api/attendance/{id}/correct (Command: CorrectAttendance)
    Host->>ModAuthz: Tier 2 Check (AdminOverrideRequirement: RequireAdmin + Non-empty Reason)
    ModAuthz-->>Host: Authorized
    Host->>ModAttend: RecordAdminAttendanceCorrection(AttendanceId, NewStatus='Attended', AdminId, Reason)
    
    alt Concurrency Conflict (Candidate for ERD Review)
        ModAttend->>DB: Attempt Update AttendanceOutcome (Status='Attended')
        DB-->>ModAttend: Concurrency Conflict Exception
        ModAttend-->>Host: ConcurrencyConflictException
        Host-->>SPA: Domain Error (HTTP 4xx: Conflict)
    else Transition Succeeded
        Note over ModAttend, DB: Transaction 2: Atomic Correction + Immutable Audit Trail
        ModAttend->>DB: Update AttendanceOutcome (Status='Attended')
        ModAttend->>Audit: Record Required Audit Evidence (Action='Attendance.AdminCorrected', Actor=AdminId, OldValue='NoShow', NewValue='Attended', Reason=Reason, Target=AttendanceId)
        ModAttend-->>Host: CorrectionResult (AttendanceId, Status='Attended')
        Host-->>SPA: 200 OK (Status='Attended')
        SPA-->>Admin: Display: "Attendance corrected. Full audit trail recorded."
    end
    end
```

---

## 3. Architecture & Boundary Analysis

### A. Authorization Points (ADR-003)
1. **Finalization (Step 11 & 18)**: Evaluated under `EventChiefLeaderRequirement`. Verifies that the actor has an `Active` `ChiefLeaderAssignment` for this specific event.
2. **Correction (Step 29)**: Evaluated under `AdminOverrideRequirement`. Verifies that the actor possesses the `Admin` role AND that a non-empty `MandatoryReason` string is supplied in the command payload.

### B. Domain Invariants & Guard Checks
1. **Decoupled Completion (Normalization Correction 3)**: Event completion is triggered by time/window closure or Admin closeout. It is **never** blocked by missing attendance submissions. Missing attendance surfaces purely as an operational flag (`Pending Attendance`) on management dashboards.
2. **Immediate Finalization (Approved Rule)**: Chief Leader submission takes immediate, direct effect in participation records. No administrative approval queue exists.
3. **Immutable Audit Trail for Corrections (Approved Rule)**: Admin corrections require an explicit recorded justification. The system logs prior outcome, new outcome, acting Admin, timestamp, and mandatory reason.

### C. Concurrency-Sensitive Points (ERD Review Candidates)
- **Attendance Record Concurrency**: Optimistic concurrency control ensures an Admin correction cannot silently collide with another Admin’s simultaneous override or a delayed Chief Leader submission.

### D. Transaction Boundaries & Atomicity
- **Transaction 1 (Roll Sheet Finalization)**:
  - Batch updates active roster entries in `AttendanceOutcome` from `Unrecorded` to final values (`Attended` or `NoShow`).
  - Commits operational audit record.
  - Executed as an atomic unit of work via `SaveChangesAsync()`.
- **Transaction 2 (Administrative Correction)**:
  - Updates the single `AttendanceOutcome` record.
  - Commits the detailed correction record containing prior/new values and reason.
  - Executed as an atomic unit of work.
