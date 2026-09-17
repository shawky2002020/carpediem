# Sequence 002: Leader Availability Declaration, Candidate Pooling & Team Selection

* **Context**: Volunteer availability collection and Chief Leader team roster selection for a published event.
* **Lifecycle Transitions Covered**:
  - `AvailabilityResponse` (Dimension A): `[No Record]` $\rightarrow$ `Available`
  - `TeamAssignment` (Dimension B): `[No Record]` $\rightarrow$ `Active`
* **Authoritative Inputs**:
  - `LIFECYCLE_SPECIFICATION.md` (Sections 4 & 5)
  - `CHANGE-001` (Chief Leader Dual-Role Participation & Same-Event Exclusivity)
  - `ADR-002` (Authentication & Session Strategy)
  - `ADR-003` (Authorization: Tier 1, Tier 2, Tier 3)
  - `ADR-005` (Audit Trail Strategy)
  - `ADR-009` (SQL Server Persistence & Schema Organization)

---

## 1. Sequence Overview

This sequence details the two distinct dimensions of event staffing:
1. **Dimension A: Availability Declaration**:
   - A volunteer Leader views a `Published` Event.
   - The Leader submits affirmative availability (`Available`).
   - The system validates that the user possesses the `Leader` role and is **not** the active Chief Leader for this same event (per CHANGE-001).
   - An `AvailabilityResponse` record in status `Available` is persisted for the current active staffing context. Absence of response was previously treated as `No Record` (displayed as "Response Needed" in UI).
2. **Candidate Pooling**:
   - The candidate pool for the Chief Leader dynamically derives from all volunteer Leaders who have submitted an `Available` response for the event's current active staffing context.
3. **Dimension B: Team Selection**:
   - The assigned Chief Leader (holding an `Active` `ChiefLeaderAssignment` for this specific event) views the candidate pool and selects an available Leader.
   - The `Staffing` module immediately creates an `Active` `TeamAssignment`.
   - **Immediate Placement (Approved Rule)**: There is no secondary invitation or confirmation queue; selection immediately establishes official, operational team membership.
   - **Notification (Approved Requirement)**: The selected Leader receives an in-platform notification confirming placement.
4. **Dimension Decoupling**:
   - The `AvailabilityResponse` remains `Available` (it is not deleted, mutated, or converted into an assignment). Both dimensions coexist independently.

---

## 2. Sequence Diagram

```mermaid
sequenceDiagram
    autonumber
    actor Leader as Volunteer Leader
    actor CL as Active Chief Leader
    participant SPA as Angular Web App
    participant Host as ASP.NET Core API
    participant ModAuthz as ADR-003 Authz
    participant ModStaffing as Staffing Module
    participant ModEvents as Events Module
    participant ModNotif as Notifications Module
    participant Audit as Audit Persistence
    participant DB as Persistence Store

    %% ------------------------------------------------------------------------
    %% PHASE 1: LEADER DECLARES AVAILABILITY (DIMENSION A)
    %% ------------------------------------------------------------------------
    rect rgb(240, 248, 255)
    Note over Leader, DB: PHASE 1: Declare Schedule Availability (Dimension A)
    Leader->>SPA: Navigate to Event & Click "Available"
    SPA->>Host: POST /api/events/{id}/availability (Command: SetAvailability)
    Host->>ModAuthz: Tier 1 & Tier 2 Gate (RequireLeader + EventLeaderParticipationRequirement)
    ModAuthz->>ModEvents: IsActiveChiefLeader(EventId, LeaderUserId)
    alt User is Active Chief Leader for this Event (CHANGE-001 Invariant)
        ModEvents-->>ModAuthz: True (Conflict Detected)
        ModAuthz-->>Host: 403 Forbidden ("Active Chief Leader cannot participate as Leader on same event")
        Host-->>SPA: 403 Forbidden
        SPA-->>Leader: Show Error: "You cannot declare volunteer availability for an event you lead"
    else Authorized (Not Active Chief Leader)
        ModEvents-->>ModAuthz: False
        ModAuthz-->>Host: Authorized
        Host->>ModStaffing: SetAvailability(EventId, LeaderUserId, State='Available')
        ModStaffing->>ModEvents: GetCurrentStaffingContext(EventId)
        ModEvents-->>ModStaffing: Current Staffing Context (Active)
        ModStaffing->>ModStaffing: Tier 3 Invariant: Event is Published & Staffing Window Open
        Note over ModStaffing, DB: Transaction 1: Persist AvailabilityResponse
        ModStaffing->>DB: Persist AvailabilityResponse (LeaderId, Status='Available')
        ModStaffing-->>Host: AvailabilityResult (Status='Available')
        Host-->>SPA: 200 OK (Status='Available')
        SPA-->>Leader: Update UI: "You are marked Available for this Event"
    end
    end

    %% ------------------------------------------------------------------------
    %% PHASE 2: CHIEF LEADER REVIEWS CANDIDATE POOL
    %% ------------------------------------------------------------------------
    rect rgb(255, 250, 240)
    Note over CL, DB: PHASE 2: Chief Leader Views Candidate Pool
    CL->>SPA: Open Event Staffing Workspace
    SPA->>Host: GET /api/events/{id}/candidates
    Host->>ModAuthz: Tier 2 Gate (EventChiefLeaderRequirement)
    ModAuthz->>ModEvents: HasActiveChiefLeaderAssignment(EventId, CurrentUserId)
    ModEvents-->>ModAuthz: True (Authorized)
    ModAuthz-->>Host: Authorized
    Host->>ModStaffing: GetAvailableCandidatePool(EventId)
    Note over ModStaffing, DB: Read Model: Join active staffing context + Available responses
    ModStaffing->>DB: Query Available Candidates
    DB-->>ModStaffing: Candidate List
    ModStaffing-->>Host: CandidatePoolDto
    Host-->>SPA: 200 OK (Candidate List)
    SPA-->>CL: Render Available Candidate Cards
    end

    %% ------------------------------------------------------------------------
    %% PHASE 3: CHIEF LEADER SELECTS LEADER (DIMENSION B)
    %% ------------------------------------------------------------------------
    rect rgb(240, 255, 240)
    Note over CL, DB: PHASE 3: Team Roster Selection (Dimension B)
    CL->>SPA: Click "Select to Team" on Candidate Card
    SPA->>Host: POST /api/events/{id}/roster/select (Command: SelectCandidate)
    Host->>ModAuthz: Tier 2 Check (EventChiefLeaderRequirement)
    ModAuthz-->>Host: Authorized (Active Chief Leader for Event)
    Host->>ModStaffing: SelectCandidate(EventId, TargetLeaderId, ChiefLeaderUserId)
    ModStaffing->>ModEvents: Verify Same-Event Exclusivity: IsActiveChiefLeader(EventId, TargetLeaderId)
    alt Target is Chief Leader (Sanity Check)
        ModEvents-->>ModStaffing: True
        ModStaffing-->>Host: DomainRuleViolationException ("Target is active Chief Leader")
        Host-->>SPA: Domain Error (HTTP 4xx)
    else Target Eligible
        ModEvents-->>ModStaffing: False
        ModStaffing->>ModStaffing: Invariant: Target does not already possess Active TeamAssignment
        alt Concurrency Collision (Duplicate Assignment Race — Candidate for ERD Review)
            ModStaffing->>DB: Attempt Persist TeamAssignment (Status='Active')
            DB-->>ModStaffing: Constraint / Concurrency Conflict
            ModStaffing-->>Host: ConcurrencyConflictException ("Leader already assigned to roster")
            Host-->>SPA: Domain Error (HTTP 4xx: Conflict)
        else Assignment Valid
            Note over ModStaffing, DB: Transaction 2: Atomic TeamAssignment + Audit Evidence
            ModStaffing->>DB: Persist TeamAssignment (EventId, LeaderUserId, Status='Active')
            ModStaffing->>Audit: Record Required Audit Evidence (Action='Staffing.CandidateSelected', Actor=ChiefLeaderUserId)
            ModStaffing->>ModNotif: Trigger TeamMemberSelectedEvent (Approved Notification Requirement)
            ModNotif->>DB: Record Notification Intent (Target=LeaderUserId)
            ModStaffing-->>Host: SelectionResult (AssignmentId, Status='Active')
            Host-->>SPA: 200 OK (AssignmentId, Status='Active')
            SPA-->>CL: Update Roster: Leader moved to "Active Team Members"
            Note over Leader, SPA: Leader receives notification of confirmed placement
        end
    end
    end
```

---

## 3. Architecture & Boundary Analysis

### A. Authorization Points (ADR-003)
1. **Set Availability (Step 3)**: Tier 1 `RequireRole("Leader")` + Tier 2 `EventLeaderParticipationRequirement`. Verifies that the volunteer holds the `Leader` role capability AND is not currently the active Chief Leader on this specific event.
2. **Candidate Review & Selection (Step 18 & 31)**: Tier 2 `EventChiefLeaderRequirement`. Verifies that the actor has an `Active` `ChiefLeaderAssignment` for the target Event.

### B. Domain Invariants & Guard Checks
1. **Same-Event Exclusivity (CHANGE-001)**: $\text{ActiveChiefLeaderAssignment}(E, U) \implies \neg\text{ActiveTeamAssignment}(E, U)$. A user cannot submit availability for an event they are actively leading, nor can they be placed on the volunteer team roster.
2. **Immediate Active Assignment**: When a candidate is selected, the `TeamAssignment` is created directly in `Active` status. No intermediate invitation, staging, or secondary confirmation state exists.
3. **Decoupled Dimensions**: The Leader's `AvailabilityResponse` remains recorded as `Available`. The new `TeamAssignment` is recorded independently. Neither record overwrites or erases the other.

### C. Concurrency-Sensitive Points (ERD Review Candidates)
- **Roster Slot & Duplicate Assignment Race**: Invariant: at most one `Active` `TeamAssignment` for the same Leader/Event context. Potential SQL enforcement mechanisms (e.g., unique filtered indexes or rowversion concurrency tokens) will be formally evaluated during the ERD review.

### D. Transaction Boundaries & Atomicity
- **Transaction 1 (Availability Submission)**: Persists `AvailabilityResponse` bound to current staffing context.
- **Transaction 2 (Team Selection)**: Atomically commits the new `TeamAssignment` (`Active`) and records required audit evidence.
- *Notification intent*: Triggered as part of the successful workflow. Reliable notification delivery mechanics are governed by ADR-006.
