# Sequence 004: Leader Self-Cancellation, Staffing Gap & Replacement Selection

* **Context**: Volunteer withdrawal, operational alert handling, and roster replacement by the Chief Leader.
* **Lifecycle Transitions Covered**:
  - `TeamAssignment` (Original): `Active` $\rightarrow$ `Cancelled` (Immutable historical record)
  - `TeamAssignment` (Replacement): `[No Record]` $\rightarrow$ `Active` (New, distinct record)
* **Authoritative Inputs**:
  - `LIFECYCLE_SPECIFICATION.md` (Section 5.B & 5.C)
  - `ADR-003` (Authorization: Tier 2 `OwnResourceRequirement` & `EventChiefLeaderRequirement`)
  - `ADR-005` (Audit Trail Strategy — Mandatory Justification Capture)
  - `ADR-009` (SQL Server Persistence — Rowversion & Immutability)

---

## 1. Sequence Overview

This sequence details how volunteer self-cancellations are managed and how the resulting staffing gap is resolved:
1. **Self-Cancellation Initiation**:
   - An actively assigned Leader navigates to their event assignment in the Angular SPA and initiates cancellation.
   - The UI enforces an explicit confirmation modal requiring a **mandatory cancellation reason** text (Approved Requirement).
2. **Cancellation Execution & Audit**:
   - The `Staffing` module transitions the assignment from `Active` to `Cancelled`.
   - The record is **never deleted or reactivated**. It is retained immutably to maintain complete historical accountability.
   - A mandatory audit record is appended capturing the actor, timestamp, assignment identifier, and explanation.
3. **Staffing Gap & Alerts (Approved Requirement)**:
   - In-platform notifications are dispatched to both the assigned **Chief Leader** and **Operations Admins** alerting them of the cancellation.
   - An operational staffing gap condition appears on the Chief Leader's dashboard.
4. **Replacement Selection**:
   - The Chief Leader reviews the roster gap and selects a replacement candidate.
   - The replacement may be selected from the existing `Available` candidate pool OR via direct drafting (SEQ-003).
   - **Immutable Replacement Rule**: The cancelled assignment remains untouched in historical records. The replacement action creates a **brand-new `TeamAssignment` record** in `Active` status.
   - *Lineage Note*: ERD must determine whether explicit replacement lineage (linking replacement to cancelled record) is useful/required.

---

## 2. Sequence Diagram

```mermaid
sequenceDiagram
    autonumber
    actor Leader as Assigned Leader
    actor CL as Active Chief Leader
    actor Admin as Operations Admin
    participant SPA as Angular Web App
    participant Host as ASP.NET Core API
    participant ModAuthz as ADR-003 Authz
    participant ModStaffing as Staffing Module
    participant ModEvents as Events Module
    participant ModNotif as Notifications Module
    participant Audit as Audit Persistence
    participant DB as Persistence Store

    %% ------------------------------------------------------------------------
    %% PHASE 1: LEADER SELF-CANCELLATION
    %% ------------------------------------------------------------------------
    rect rgb(255, 240, 240)
    Note over Leader, DB: PHASE 1: Leader Self-Cancellation
    Leader->>SPA: Click "Cancel My Assignment"
    SPA->>Leader: Render Modal: Require Explicit Confirmation & Mandatory Reason
    Leader->>SPA: Enter Reason ("Family emergency out of town") & Confirm
    SPA->>Host: POST /api/assignments/{id}/cancel (Command: SelfCancelAssignment)
    Host->>ModAuthz: Tier 2 Check (OwnResourceRequirement: TargetAssignment.LeaderUserId == CurrentUserId)
    alt Authz Denied (Attempting to cancel someone else's assignment)
        ModAuthz-->>Host: 403 Forbidden
        Host-->>SPA: 403 Forbidden
    else Authz Passed
        ModAuthz-->>Host: Authorized
        Host->>ModStaffing: SelfCancelAssignment(AssignmentId, LeaderUserId, Reason)
        ModStaffing->>ModStaffing: Tier 3 Invariant Check: Assignment.Status == Active
        alt Reason is Blank or Empty
            ModStaffing-->>Host: ValidationException ("Mandatory cancellation reason missing")
            Host-->>SPA: 400 Bad Request
        else Valid Reason & Active State
            alt Concurrency Collision (Cancellation vs Override Race — Candidate for ERD Review)
                ModStaffing->>DB: Attempt Update TeamAssignment (Status='Cancelled')
                DB-->>ModStaffing: Concurrency Conflict Exception
                ModStaffing-->>Host: ConcurrencyConflictException
                Host-->>SPA: Domain Error (HTTP 4xx: Conflict)
            else Transition Succeeded
                Note over ModStaffing, DB: Transaction 1: Atomic Cancellation + Audit Evidence
                ModStaffing->>DB: Update TeamAssignment (Status='Cancelled')
                ModStaffing->>Audit: Record Required Audit Evidence (Action='Staffing.LeaderSelfCancelled', Actor=LeaderUserId, Reason=Reason, Target=AssignmentId)
                ModStaffing->>ModNotif: Trigger AssignmentCancelledEvent (Approved Notification Requirement)
                ModNotif->>DB: Record Notification Intent (Target=ChiefLeader & Admin)
                ModStaffing-->>Host: CancellationResult (Status='Cancelled')
                Host-->>SPA: 200 OK (Status='Cancelled')
                SPA-->>Leader: Display Confirmation: "Your assignment has been cancelled"
            end
        end
    end
    end

    %% ------------------------------------------------------------------------
    %% PHASE 2: CHIEF LEADER SELECTS REPLACEMENT
    %% ------------------------------------------------------------------------
    rect rgb(240, 255, 240)
    Note over CL, DB: PHASE 2: Roster Gap Alert & Replacement Selection
    Note over CL, SPA: Chief Leader receives in-platform alert of vacancy
    CL->>SPA: Open Event Roster (Displays "Staffing Gap / Vacancy Detected")
    CL->>SPA: Select Replacement Candidate from Pool & Click "Confirm Replacement"
    SPA->>Host: POST /api/assignments/{cancelledId}/replace (Command: SelectReplacement)
    Host->>ModAuthz: Tier 2 Check (EventChiefLeaderRequirement)
    ModAuthz->>ModEvents: HasActiveChiefLeaderAssignment(EventId, CL_UserId)
    ModEvents-->>ModAuthz: True (Authorized)
    ModAuthz-->>Host: Authorized
    Host->>ModStaffing: SelectReplacement(CancelledAssignmentId, ReplacementLeaderId, CL_UserId)

    ModStaffing->>ModStaffing: Invariant Check: Prior Assignment is in Cancelled Status
    ModStaffing->>ModStaffing: Invariant Check: Replacement candidate does not hold Active assignment
    ModStaffing->>ModEvents: Verify Same-Event Exclusivity: IsActiveChiefLeader(EventId, ReplacementLeaderId)
    ModEvents-->>ModStaffing: False (Eligible)

    Note over ModStaffing, DB: Transaction 2: Create New Assignment (Never Reactivate Cancelled)
    Note over ModStaffing, DB: Original TeamAssignment remains CANCELLED in historical persistence
    ModStaffing->>DB: Persist New TeamAssignment (EventId, LeaderUserId=ReplacementLeaderId, Status='Active')
    ModStaffing->>Audit: Record Required Audit Evidence (Action='Staffing.ReplacementSelected', Actor=CL_UserId, Target=ReplacementLeaderId)
    ModStaffing->>ModNotif: Trigger ReplacementSelectedEvent (Approved Notification Requirement)
    ModNotif->>DB: Record Notification Intent (Target=ReplacementLeaderId)
    ModStaffing-->>Host: ReplacementResult (NewAssignmentId, Status='Active')
    Host-->>SPA: 201 Created (NewAssignmentId, Status='Active')
    SPA-->>CL: Update Roster UI: Staffing gap resolved with replacement leader
    Note over Admin, ModNotif: Admin dashboard updates; staffing gap cleared
    end
```

---

## 3. Architecture & Boundary Analysis

### A. Authorization Points (ADR-003)
1. **Cancellation (Step 5)**: Evaluated under `OwnResourceRequirement`. Only the assigned Leader whose `LeaderUserId` matches the assignment (or an Admin via authorized override) can execute the cancellation.
2. **Replacement (Step 24)**: Evaluated under `EventChiefLeaderRequirement`. Only the active Chief Leader (or an Admin override) holds authority to select a replacement.

### B. Domain Invariants & Guard Checks
1. **Mandatory Justification (Approved Rule)**: Self-cancellation strictly requires non-empty explanation text. Empty submissions fail validation before database submission.
2. **Immutable History & Replacement Record**: A cancelled assignment is **never deleted or overwritten**. The replacement creates a **new distinct `TeamAssignment` record** in `Active` status.
3. **Lineage Storage**: ERD review must determine whether explicit replacement lineage (e.g., storing a previous assignment reference) is useful/required for downstream reporting.
4. **Commitment & Penalties**: No cancellation penalty, late-cancellation threshold, or commitment effect is currently approved. These remain unresolved and must not be invented.

### C. Concurrency-Sensitive Points (ERD Review Candidates)
- **Concurrent Cancellation vs. Override Collision**: Optimistic concurrency control ensures that an administrative removal colliding with a volunteer self-cancellation on the same slot resolves cleanly without race conditions.

### D. Transaction Boundaries & Atomicity
- **Transaction 1 (Self-Cancellation)**:
  - Updates original `TeamAssignment` to `Cancelled`.
  - Appends required audit record with the captured reason.
  - Executed as a single atomic unit of work via `SaveChangesAsync()`.
- **Transaction 2 (Replacement Selection)**:
  - Persists new `TeamAssignment` (`Status = 'Active'`).
  - Appends required audit entry recording the selection.
  - Executed as an independent atomic transaction.
- *Notification Intent*: Staged as part of the successful workflows; reliable transport/delivery is governed by ADR-006.
