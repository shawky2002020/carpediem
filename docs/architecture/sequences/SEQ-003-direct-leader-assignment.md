# Sequence 003: Direct Assignment of Unanswered / Not Available Leader

* **Context**: Operational necessity workflow allowing Chief Leaders or Admins to directly draft a Leader onto the roster despite an absence of availability or a declared `NotAvailable` response.
* **Lifecycle Transitions Covered**:
  - `AvailabilityResponse` (Dimension A): **Unmodified** (preserves factual `[No Record]` or `NotAvailable`)
  - `TeamAssignment` (Dimension B): `[No Record]` $\rightarrow$ `Active`
* **Authoritative Inputs**:
  - `LIFECYCLE_SPECIFICATION.md` (Section 5.B & Section 7 Approved Blocker 1)
  - `CHANGE-001` (Chief Leader Dual-Role Participation & Same-Event Exclusivity)
  - `ADR-003` (Authorization & Resource Policies)
  - `ADR-005` (Audit Trail Strategy — Direct Drafting vs. Admin Override)
  - `ADR-009` (SQL Server Persistence & Schema Organization)

---

## 1. Sequence Overview

In operational environments, unexpected staffing shortages require leaders to recruit volunteers directly:
1. **Target Candidate State**: The target Leader has either:
   - **No AvailabilityResponse** (never responded; displayed as "Response Needed"), OR
   - **NotAvailable** (explicitly declared inability to serve on this date).
2. **Authority & Exception (Approved Blocker 1)**:
   - Both the assigned **Chief Leader** and **Operations Admins** possess approved authority to directly draft a Leader.
   - Direct drafting creates an immediate `Active` `TeamAssignment`. No invitation, staging, or secondary acceptance flow exists in V1.
3. **Preservation of Availability Truth**:
   - The system strictly **DOES NOT mutate** `AvailabilityResponse` to `Available`.
   - The volunteer's declared response (or absence thereof) remains historically truthful.
4. **Audit & Accountability (ADR-005)**:
   - When executed by a **Chief Leader**, it is recorded as an operational direct draft (`Staffing.LeaderDirectlyDrafted`, Source: `DirectUserAction`).
   - When executed by an **Admin**, it is recorded as an administrative staffing intervention (`Staffing.AdminDirectDraft`, Source: `AdminOverride`).
5. **Notification (Approved Requirement)**: An immediate in-platform notification is dispatched alerting the Leader of their placement on the roster.

---

## 2. Sequence Diagram

```mermaid
sequenceDiagram
    autonumber
    actor Actor as Chief Leader or Admin
    actor TargetLeader as Volunteer Leader (Not Available)
    participant SPA as Angular Web App
    participant Host as ASP.NET Core API
    participant ModAuthz as ADR-003 Authz
    participant ModStaffing as Staffing Module
    participant ModEvents as Events Module
    participant ModNotif as Notifications Module
    participant Audit as Audit Persistence
    participant DB as Persistence Store

    %% ------------------------------------------------------------------------
    %% DIRECT DRAFT TRIGGER
    %% ------------------------------------------------------------------------
    rect rgb(240, 248, 255)
    Note over Actor, DB: Direct Draft Initiation
    Actor->>SPA: Select Leader from Directory & Click "Direct Draft to Team"
    SPA->>Host: POST /api/events/{id}/roster/direct-draft (Command: DirectDraftLeader)
    end

    %% ------------------------------------------------------------------------
    %% AUTHORIZATION & SAME-EVENT EXCLUSIVITY GATE
    %% ------------------------------------------------------------------------
    rect rgb(255, 250, 240)
    Note over Actor, Host: Authorization & Governance Verification
    Host->>ModAuthz: Evaluate Authorization (Actor, TargetEventId, CommandPayload)
    alt Actor is Chief Leader
        ModAuthz->>ModEvents: HasActiveChiefLeaderAssignment(TargetEventId, ActorId)
        ModEvents-->>ModAuthz: True (Active Chief Leader)
        ModAuthz-->>Host: Authorized (Operational Direct Draft)
    else Actor is Admin
        ModAuthz->>ModAuthz: Verify Admin Role
        ModAuthz-->>Host: Authorized (Admin Override)
    end

    Host->>ModStaffing: DirectDraftLeader(EventId, TargetLeaderId, ActorId)
    ModStaffing->>ModEvents: Check Same-Event Exclusivity: IsActiveChiefLeader(EventId, TargetLeaderId)
    alt Target is Active Chief Leader for this Event (CHANGE-001 Violation)
        ModEvents-->>ModStaffing: True (Target is Chief Leader)
        ModStaffing-->>Host: DomainRuleViolationException ("Cannot draft the active Chief Leader to team roster")
        Host-->>SPA: Domain Error (HTTP 4xx)
    else Target is Eligible
        ModEvents-->>ModStaffing: False
        ModStaffing->>ModStaffing: Check Target Status: Verify Target NOT already Active on Roster
    end
    end

    %% ------------------------------------------------------------------------
    %% PERSISTENCE & AUDIT (PRESERVING AVAILABILITY FACT)
    %% ------------------------------------------------------------------------
    rect rgb(240, 255, 240)
    Note over ModStaffing, DB: Atomic Placement & Integrity Preservation
    Note over ModStaffing, DB: Invariant: AvailabilityResponse is UNTOUCHED (remains NotAvailable or No Record)
    Note over ModStaffing, DB: Transaction 1: Atomic Assignment + Audit Evidence
    ModStaffing->>DB: Persist TeamAssignment (EventId, LeaderUserId=TargetLeaderId, Status='Active')
    alt Triggered by Chief Leader
        ModStaffing->>Audit: Record Required Audit Evidence (Action='Staffing.LeaderDirectlyDrafted', Actor=ActorId, Source='DirectUserAction', Target=TargetLeaderId)
    else Triggered by Admin
        ModStaffing->>Audit: Record Required Audit Evidence (Action='Staffing.AdminDirectDraft', Actor=ActorId, Source='AdminOverride', Target=TargetLeaderId)
    end
    ModStaffing->>ModNotif: Trigger LeaderDirectlyDraftedEvent (Approved Notification Requirement)
    ModNotif->>DB: Record Notification Intent (Target=TargetLeaderId)
    ModStaffing-->>Host: DirectDraftResult (AssignmentId, Status='Active')
    Host-->>SPA: 201 Created (AssignmentId, Status='Active')
    SPA-->>Actor: Display "Leader successfully drafted to team roster"
    Note over TargetLeader, SPA: Target Leader receives in-platform alert of confirmed active placement
    end
```

---

## 3. Architecture & Boundary Analysis

### A. Authorization Points (ADR-003)
1. **Chief Leader Path**: Handled under `EventChiefLeaderRequirement`. Verifies that the Chief Leader holds an `Active` `ChiefLeaderAssignment` for the specific target Event.
2. **Admin Path**: Handled under administrative override policies. Verifies that the caller holds the `Admin` role.

### B. Domain Invariants & Guard Checks
1. **Preservation of Availability Truth**: The system does **NOT** alter the volunteer's `AvailabilityResponse` record. If the Leader was marked `NotAvailable` or had no record, that historical response remains exactly as declared. The direct draft simply establishes an authoritative placement in `TeamAssignment`.
2. **Same-Event Exclusivity (CHANGE-001)**: The system verifies that the target Leader is not the active Chief Leader for the target event.
3. **No Intermediate Confirmation**: The resulting `TeamAssignment` is immediately `Active`. If the drafted Leader cannot fulfill the assignment, they must use the approved cancellation workflow (SEQ-004).

### C. Concurrency-Sensitive Points (ERD Review Candidates)
- **Duplicate Direct Drafts**: Invariant: at most one `Active` `TeamAssignment` per volunteer per event. Database enforcement mechanisms (unique constraints or concurrency checks) are designated as candidates for ERD review.

### D. Transaction Boundaries & Atomicity
- **Transaction 1 (Direct Draft Placement)**:
  - Persists the new active record in `TeamAssignment`.
  - Appends the required audit record (distinguishing between `DirectUserAction` and `AdminOverride`).
  - Executed as an atomic unit of work via `SaveChangesAsync()`.
  - Notification intent is produced as part of the successful workflow; transport/delivery is governed by ADR-006.
