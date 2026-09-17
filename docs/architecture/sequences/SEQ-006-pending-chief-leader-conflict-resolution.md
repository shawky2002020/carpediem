# Sequence 006: Pending Chief Leader Appointment with Existing Team Assignment & Conflict Resolution

* **Context**: Resolution sequence when a dual-role user holding an active volunteer team assignment on an event is subsequently appointed as the event's Chief Leader.
* **Lifecycle Transitions Covered**:
  - `ChiefLeaderAssignment`: `[No Record]` $\rightarrow$ `Pending` $\rightarrow$ *(Attempted Acceptance Blocked)* $\rightarrow$ `Active` (after conflict resolution)
  - `TeamAssignment`: `Active` $\rightarrow$ `Cancelled` (via approved resolution transition with mandatory reason)
* **Authoritative Inputs**:
  - `CHANGE-001` (Chief Leader Dual-Role Participation & Same-Event Exclusivity)
  - `LIFECYCLE_SPECIFICATION.md` (Section 3.B, Item 7)
  - `ADR-003` (Authorization: Tier 2 & Tier 3 Domain Lifecycle Invariants)
  - `ADR-005` (Audit Trail Strategy — Mandatory Reason on Cancellation)
  - `ADR-009` (SQL Server Persistence & Schema Organization)

---

## 1. Sequence Overview

This sequence evaluates the precise governance rule codified in CHANGE-001 and ratified in the lifecycle specification:
1. **Initial Condition**:
   - Dual-role User $U$ (holding both `ChiefLeader` and `Leader` roles) has an `Active` `TeamAssignment` on Event $E$ as a regular volunteer.
2. **Pending Appointment**:
   - An Operations Admin appoints User $U$ as the Chief Leader for Event $E$.
   - A `ChiefLeaderAssignment` is created in status `Pending`.
   - **Approved Governance Rule**: *No additional restriction has been defined for a Pending ChiefLeaderAssignment. Behavior remains governed by existing Leader participation rules until activation.*
3. **Attempted Acceptance & Invariant Gate**:
   - User $U$ navigates to accept the Chief Leader appointment.
   - The `Events` module checks the same-event exclusivity invariant:
     $$\text{ActiveChiefLeaderAssignment}(E, U) \implies \neg\text{ActiveTeamAssignment}(E, U)$$
   - The system detects that User $U$ currently holds an `Active` `TeamAssignment` on Event $E$.
   - **Transition Blocked**: `Pending` $\rightarrow$ `Active` **MUST NOT** proceed while an active team assignment exists.
   - **No Silent Auto-Mutation**: The system does **not** silently or automatically erase or cancel the team assignment. Such implicit actions would violate audit and reason-capture mandates. The acceptance is rejected with an actionable domain error (`HTTP 4xx: Conflict`).
4. **Legitimate Conflict Resolution**:
   - **Approved Rule**: *The conflicting Active TeamAssignment must be resolved through an approved TeamAssignment transition before ChiefLeaderAssignment activation can succeed.*
   - In accordance with approved cancellation rules, the active volunteer assignment is cancelled with an explicit, mandatory justification reason.
   - The `TeamAssignment` transitions from `Active` to `Cancelled` with required audit evidence recorded.
5. **Successful Activation**:
   - With the existing team assignment resolved (`Cancelled`), the acceptance action is retried.
   - The invariant check passes; `ChiefLeaderAssignment` transitions to `Active`.
   - User $U$ is now the operational Chief Leader for Event $E$.

---

## 2. Sequence Diagram

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Operations Admin
    actor UserU as Dual-Role User (Leader + ChiefLeader)
    participant SPA as Angular Web App
    participant Host as ASP.NET Core API
    participant ModAuthz as ADR-003 Authz
    participant ModEvents as Events Module
    participant ModStaffing as Staffing Module
    participant ModNotif as Notifications Module
    participant Audit as Audit Persistence
    participant DB as Persistence Store

    %% ------------------------------------------------------------------------
    %% PHASE 1: ADMIN APPOINTS USER U (PENDING CHIEF LEADER)
    %% ------------------------------------------------------------------------
    rect rgb(240, 248, 255)
    Note over Admin, DB: PHASE 1: Admin Appoints User U (Already on Team Roster)
    Note over UserU, DB: User U currently holds an ACTIVE TeamAssignment on Event E
    Admin->>SPA: Select User U as Chief Leader for Event E
    SPA->>Host: POST /api/events/{E}/chief-leader (TargetUserId=UserU_Id)
    Host->>ModEvents: AppointChiefLeader(EventId=E, TargetUserId=UserU_Id)
    Note over ModEvents, DB: Transaction 1: Persist Pending Assignment
    ModEvents->>DB: Persist ChiefLeaderAssignment (Status='Pending')
    ModEvents->>ModNotif: Trigger ChiefLeaderAppointedEvent (Approved Notification Requirement)
    ModNotif->>DB: Record Notification Intent
    ModEvents-->>Host: Result (Status='Pending')
    Host-->>SPA: 200 OK (Status='Pending')
    Note over UserU, SPA: User U receives notification: "Appointed Chief Leader for Event E"
    end

    %% ------------------------------------------------------------------------
    %% PHASE 2: ATTEMPTED ACCEPTANCE (REJECTED DUE TO CONFLICT)
    %% ------------------------------------------------------------------------
    rect rgb(255, 240, 240)
    Note over UserU, DB: PHASE 2: Attempted Acceptance While TeamAssignment is Active
    UserU->>SPA: Click "Accept Chief Leader Assignment"
    SPA->>Host: POST /api/events/{E}/chief-leader/accept
    Host->>ModAuthz: Tier 1 & Tier 2 Authz (ChiefLeader Role + Candidate Ownership)
    ModAuthz-->>Host: Authorized
    Host->>ModEvents: AcceptChiefLeaderAssignment(AssignmentId, UserU_Id)
    ModEvents->>ModStaffing: HasActiveTeamAssignment(EventId=E, UserId=UserU_Id)
    ModStaffing-->>ModEvents: True (Active TeamAssignment Exists!)
    
    Note over ModEvents, Host: Invariant Violation: Cannot activate while Active TeamAssignment exists
    Note over ModEvents, Host: SYSTEM DOES NOT AUTOMATICALLY CANCEL TEAM ASSIGNMENT
    ModEvents-->>Host: DomainRuleViolationException ("Active TeamAssignment must be resolved before activation can succeed")
    Host-->>SPA: Domain Error (HTTP 4xx: Conflict)
    SPA-->>UserU: Error: "You currently hold an Active Volunteer assignment for this event. That assignment must be resolved before accepting Chief Leader responsibilities."
    end

    %% ------------------------------------------------------------------------
    %% PHASE 3: CONFLICT RESOLUTION (APPROVED TRANSITION)
    %% ------------------------------------------------------------------------
    rect rgb(255, 250, 240)
    Note over UserU, DB: PHASE 3: Conflicting Active TeamAssignment Resolved via Approved Transition
    UserU->>SPA: Initiate Approved TeamAssignment Cancellation (with Mandatory Reason)
    SPA->>Host: POST /api/assignments/{TeamAssignmentId}/cancel (Reason="Stepping up to assume Chief Leader role")
    Host->>ModStaffing: CancelAssignment(TeamAssignmentId, UserU_Id, Reason)
    Note over ModStaffing, DB: Transaction 2: Atomic Cancellation + Audit Evidence
    ModStaffing->>DB: Update TeamAssignment (Status='Cancelled')
    ModStaffing->>Audit: Record Required Audit Evidence (Action='Staffing.AssignmentCancelled', Reason=Reason)
    ModStaffing-->>Host: CancellationResult (Status='Cancelled')
    Host-->>SPA: 200 OK (Status='Cancelled')
    SPA-->>UserU: Confirmation: "Volunteer assignment resolved"
    end

    %% ------------------------------------------------------------------------
    %% PHASE 4: SUCCESSFUL CHIEF LEADER ACTIVATION
    %% ------------------------------------------------------------------------
    rect rgb(240, 255, 240)
    Note over UserU, DB: PHASE 4: Re-Attempt Acceptance (Conflict Resolved)
    UserU->>SPA: Return to Chief Leader Notification & Click "Accept Assignment"
    SPA->>Host: POST /api/events/{E}/chief-leader/accept
    Host->>ModEvents: AcceptChiefLeaderAssignment(AssignmentId, UserU_Id)
    ModEvents->>ModStaffing: HasActiveTeamAssignment(EventId=E, UserId=UserU_Id)
    ModStaffing-->>ModEvents: False (TeamAssignment is now 'Cancelled'; No Active Assignment)
    
    Note over ModEvents, DB: Transaction 3: Atomic ChiefLeader Activation + Audit Evidence
    ModEvents->>DB: Update ChiefLeaderAssignment (Status='Active')
    ModEvents->>Audit: Record Required Audit Evidence (Action='ChiefLeaderAccepted', Source='DirectUserAction')
    ModEvents-->>Host: Result (Status='Active')
    Host-->>SPA: 200 OK (Status='Active')
    SPA-->>UserU: Success: "You are now the Active Chief Leader for Event E"
    end
```

---

## 3. Architecture & Boundary Analysis

### A. Authorization Points (ADR-003)
1. **Appointment (Step 3)**: Handled under `RequireAdmin`.
2. **Acceptance (Step 11 & 35)**: Handled under `EventChiefLeaderRequirement`. Verifies that User $U$ is the candidate on the `Pending` assignment.
3. **Cancellation (Step 23)**: Handled under authorized resource cancellation policies.

### B. Domain Invariants & Guard Checks
1. **No Implicit/Silent Auto-Cancellation**: The system never guesses or automatically cancels an existing team assignment upon Chief Leader acceptance. The conflicting active assignment must be resolved through an approved transition before activation can succeed.
2. **Pending Assignment Does Not Restrict**: Holding a `Pending` Chief Leader appointment does **not** strip the user of volunteer participation rights. It is only at the moment of **activation** (`Pending` $\rightarrow$ `Active`) that same-event exclusivity is strictly enforced.
3. **Cross-Module Verification**: `Events` coordinates with `Staffing` via a public query contract (`HasActiveTeamAssignment(eventId, userId)`) to verify that no active roster placement exists before committing the state change.

### C. Concurrency-Sensitive Points (ERD Review Candidates)
- **Concurrency on Activation**: If another coordinator attempts to draft User $U$ onto the team roster between Phase 3 and Phase 4, concurrency control catches the race and prevents simultaneous active states.

### D. Transaction Boundaries & Atomicity
- **Transaction 1 (Appointment)**: Persists `ChiefLeaderAssignment` (`Pending`).
- **Transaction 2 (Conflict Resolution)**: Transitions `TeamAssignment` to `Cancelled` and commits audit evidence with the mandatory reason.
- **Transaction 3 (Activation)**: Transitions `ChiefLeaderAssignment` to `Active` and records required audit evidence.
- *Notification Intent*: Produced upon appointment; delivery governed by ADR-006.
