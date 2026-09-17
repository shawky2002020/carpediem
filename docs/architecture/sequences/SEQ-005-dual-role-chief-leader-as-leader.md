# Sequence 005: Dual-Role Chief Leader Participating as Normal Leader

* **Context**: Architectural proof of CHANGE-001 allowing a user holding both `ChiefLeader` and `Leader` role capabilities to act as a normal volunteer on non-led events while strictly enforcing same-event mutual exclusivity.
* **Lifecycle Transitions Covered**:
  - **Event A (Led Event)**:
    - User holds `Active` `ChiefLeaderAssignment`
    - Availability declaration: **Rejected (403 Forbidden)**
    - Team roster placement: **Rejected (Domain Error: Same-Event Exclusivity Violation)**
  - **Event B (Non-Led Event)**:
    - User has no active leadership assignment
    - `AvailabilityResponse`: `[No Record]` $\rightarrow$ `Available`
    - `TeamAssignment`: `[No Record]` $\rightarrow$ `Active`
* **Authoritative Inputs**:
  - `CHANGE-001` (Chief Leader Dual-Role Participation)
  - `LIFECYCLE_SPECIFICATION.md` (Section 3.B & 7)
  - `ADR-003` (Authorization: Tier 2 `EventLeaderParticipationRequirement` & `EventChiefLeaderRequirement`)
  - `ADR-009` (SQL Server Persistence & Schema Organization)

---

## 1. Sequence Overview

Under CHANGE-001, membership in the predefined Chief Leader pool is non-exclusive: an Administrator may grant a Chief Leader the `Leader` role capability, enabling them to serve as a regular volunteer on events they are not leading:
1. **Separation of Global Capability from Contextual Responsibility**:
   - A user’s global role list contains both `ChiefLeader` and `Leader`.
   - Permissions on any specific event are **dynamic and contextual**, determined by whether the user holds an active leadership appointment on that specific event.
2. **Event A Context (Active Leadership)**:
   - User $U$ has an `Active` `ChiefLeaderAssignment` for Event $A$.
   - Attempting to submit schedule availability on Event $A$ is blocked at Tier 2 (`EventLeaderParticipationRequirement`).
   - Attempting to place $U$ on Event $A$'s team roster is blocked by the same-event exclusivity invariant:
     $$\text{ActiveChiefLeaderAssignment}(A, U) \implies \neg\text{ActiveTeamAssignment}(A, U)$$
3. **Event B Context (Regular Participation)**:
   - User $U$ has no leadership assignment on Event $B$ (led by a different Chief Leader).
   - User $U$ navigates to Event $B$ and submits availability. Tier 2 checks confirm $U$ holds the `Leader` role and is not the active Chief Leader for Event $B$. An `AvailabilityResponse` in status `Available` is created.
   - Event $B$'s Chief Leader selects User $U$ for the team roster. An `Active` `TeamAssignment` is created.
   - User $U$ successfully serves as a regular team member on Event $B$.

---

## 2. Sequence Diagram

```mermaid
sequenceDiagram
    autonumber
    actor UserU as Dual-Role User (Roles: ChiefLeader, Leader)
    actor CL_B as Chief Leader for Event B
    participant SPA as Angular Web App
    participant Host as ASP.NET Core API
    participant ModAuthz as ADR-003 Authz
    participant ModStaffing as Staffing Module
    participant ModEvents as Events Module
    participant ModNotif as Notifications Module
    participant DB as Persistence Store

    %% ------------------------------------------------------------------------
    %% SCENARIO 1: ATTEMPTED LEADER PARTICIPATION ON EVENT A (LED BY USER U)
    %% ------------------------------------------------------------------------
    rect rgb(255, 240, 240)
    Note over UserU, DB: SCENARIO 1: Event A — User U is Active Chief Leader
    Note over UserU, DB: Invariant Test: ActiveChiefLeaderAssignment(A, U) => NOT ActiveTeamAssignment(A, U)
    UserU->>SPA: View Event A & Attempt to Click "Mark Available"
    SPA->>Host: POST /api/events/{EventA_Id}/availability (State='Available')
    Host->>ModAuthz: Evaluate EventLeaderParticipationRequirement(EventA_Id, UserU_Id)
    ModAuthz->>ModAuthz: Tier 1: User has 'Leader' role? Yes.
    ModAuthz->>ModEvents: Tier 2: IsActiveChiefLeader(EventA_Id, UserU_Id)
    ModEvents-->>ModAuthz: True (User U is the Active Chief Leader for Event A)
    ModAuthz-->>Host: 403 Forbidden ("Active Chief Leader cannot participate as volunteer on same event")
    Host-->>SPA: 403 Forbidden
    SPA-->>UserU: Error: "You are the Chief Leader for this event; you cannot register as a volunteer"
    end

    %% ------------------------------------------------------------------------
    %% SCENARIO 2: SUCCESSFUL LEADER PARTICIPATION ON EVENT B (NOT LED BY USER U)
    %% ------------------------------------------------------------------------
    rect rgb(240, 255, 240)
    Note over UserU, DB: SCENARIO 2: Event B — Led by CL_B (User U is a Regular Volunteer)
    UserU->>SPA: View Event B & Click "Mark Available"
    SPA->>Host: POST /api/events/{EventB_Id}/availability (State='Available')
    Host->>ModAuthz: Evaluate EventLeaderParticipationRequirement(EventB_Id, UserU_Id)
    ModAuthz->>ModAuthz: Tier 1: User has 'Leader' role? Yes.
    ModAuthz->>ModEvents: Tier 2: IsActiveChiefLeader(EventB_Id, UserU_Id)
    ModEvents-->>ModAuthz: False (User U is NOT the Chief Leader for Event B; CL_B is)
    ModAuthz-->>Host: Authorized
    Host->>ModStaffing: SetAvailability(EventB_Id, UserU_Id, State='Available')
    ModStaffing->>ModEvents: GetCurrentStaffingContext(EventB_Id)
    ModEvents-->>ModStaffing: Current Staffing Context (Active)
    Note over ModStaffing, DB: Transaction 1: Persist Availability for Event B
    ModStaffing->>DB: Persist AvailabilityResponse (LeaderId=UserU_Id, Status='Available')
    ModStaffing-->>Host: AvailabilityResult (Status='Available')
    Host-->>SPA: 200 OK (Status='Available')
    SPA-->>UserU: Display: "Marked Available for Event B"
    end

    %% ------------------------------------------------------------------------
    %% SCENARIO 3: CL_B SELECTS USER U ON EVENT B's ROSTER
    %% ------------------------------------------------------------------------
    rect rgb(240, 248, 255)
    Note over CL_B, DB: SCENARIO 3: CL_B Selects User U for Event B Roster
    CL_B->>SPA: Open Event B Candidate Pool & Select User U
    SPA->>Host: POST /api/events/{EventB_Id}/roster/select (TargetLeader=UserU_Id)
    Host->>ModAuthz: Tier 2 Check (EventChiefLeaderRequirement for CL_B on Event B)
    ModAuthz->>ModEvents: HasActiveChiefLeaderAssignment(EventB_Id, CL_B_UserId)
    ModEvents-->>ModAuthz: True (CL_B is Authorized)
    ModAuthz-->>Host: Authorized
    Host->>ModStaffing: SelectCandidate(EventB_Id, TargetLeader=UserU_Id, Actor=CL_B_UserId)
    ModStaffing->>ModEvents: Verify Same-Event Exclusivity: IsActiveChiefLeader(EventB_Id, UserU_Id)
    ModEvents-->>ModStaffing: False (User U is NOT Chief Leader for Event B)
    Note over ModStaffing, DB: Transaction 2: Atomic Assignment + Audit Evidence
    ModStaffing->>DB: Persist TeamAssignment (EventId=EventB_Id, LeaderUserId=UserU_Id, Status='Active')
    ModStaffing->>ModNotif: Trigger TeamMemberSelectedEvent (Approved Notification Requirement)
    ModNotif->>DB: Record Notification Intent (Target=UserU_Id)
    ModStaffing-->>Host: SelectionResult (Status='Active')
    Host-->>SPA: 200 OK
    SPA-->>CL_B: Roster Updated: User U placed on Active Team
    Note over UserU, SPA: User U receives notification and is staffed as regular Leader on Event B
    end
```

---

## 3. Architecture & Boundary Analysis

### A. Authorization Points (ADR-003)
1. **Tier 1 vs. Tier 2 Decoupling**: Having the `Leader` role passes the endpoint entry filter (`RequireRole("Leader")`). However, the operation succeeds or fails dynamically based on the Tier 2 resource handler:
   - On Event $A$: `EventLeaderParticipationRequirement` fails because `EventsService.IsActiveChiefLeader(EventA_Id, UserU_Id)` returns `true`.
   - On Event $B$: `EventLeaderParticipationRequirement` passes because `EventsService.IsActiveChiefLeader(EventB_Id, UserU_Id)` returns `false`.

### B. Domain Invariants & Guard Checks
1. **Formal Invariant Guarantee**:
   $$\text{ActiveChiefLeaderAssignment}(E, U) \land \text{ActiveTeamAssignment}(E, U) = \emptyset$$
   A user can never hold both active roles on the same event at the same time.
2. **Dual-Role Independence**: The user maintains both credentials simultaneously without needing separate logins, account switches, or artificial system boundaries.

### C. Concurrency-Sensitive Points (ERD Review Candidates)
- **Simultaneous Cross-Role Assignment Race**: If an Admin attempts to assign User $U$ as Chief Leader on Event $B$ at the exact moment Event $B$'s existing Chief Leader is drafting User $U$ onto the volunteer roster, optimistic concurrency checks and same-event validation prevent an inconsistent state.

### D. Transaction Boundaries & Atomicity
- **Transaction 1 (Event B Availability)**: Persists `AvailabilityResponse`.
- **Transaction 2 (Event B Roster Selection)**: Atomically persists `TeamAssignment` and required audit evidence.
- *Notification Intent*: Produced upon successful selection; delivery is governed by ADR-006.
