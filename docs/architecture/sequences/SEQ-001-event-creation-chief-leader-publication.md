# Sequence 001: Event Creation, Chief Leader Appointment, Acceptance & Publication

* **Context**: Event setup, operational leadership assignment, and public activation.
* **Lifecycle Transitions Covered**:
  - `Event`: `[None]` $\rightarrow$ `Draft` $\rightarrow$ `Published`
  - `ChiefLeaderAssignment`: `[None]` $\rightarrow$ `Pending` $\rightarrow$ `Active` (and alternate `Pending` $\rightarrow$ `Declined`)
* **Authoritative Inputs**:
  - `LIFECYCLE_SPECIFICATION.md` (Sections 2 & 3)
  - `ADR-002` (Authentication & Unified Origin)
  - `ADR-003` (Authorization: Tier 1, Tier 2, Tier 3)
  - `ADR-005` (Audit Trail Strategy)
  - `ADR-009` (SQL Server Persistence & Schema Organization)

---

## 1. Sequence Overview

This sequence details the logical workflow required to bring an Event from initial draft to official public release:
1. **Event Creation**: An Admin creates a new Event in `Draft` state within the `Events` module.
2. **Chief Leader Appointment**: The Admin selects a candidate from the predefined Chief Leader pool. The `Events` module creates a `Pending` `ChiefLeaderAssignment`.
3. **Notification (Approved Requirement)**: The candidate Chief Leader receives an in-platform notification of the appointment.
4. **Acceptance / Decline Workflow**:
   - **Direct Acceptance (Approved Requirement)**: The appointed Chief Leader accepts via the Angular web app. The assignment transitions from `Pending` to `Active`, conferring operational leadership over the event.
   - **External Confirmation (Approved Exception)**: If confirmed verbally or off-platform, the Admin records the acceptance with a mandatory recorded audit note (`AdminExternalConfirmation`).
   - **Decline Path (Approved Requirement)**: The candidate Chief Leader declines the appointment. The assignment transitions to `Declined` and is immutably preserved in history. The Event remains unstaffed in `Draft` state; the Admin must appoint another candidate before publication.
5. **Event Publication**: The Admin triggers Event publication. The `Events` module enforces the **strict publishing prerequisite**: the event must possess exactly one `Active` (accepted) `ChiefLeaderAssignment`. Upon passing, `Event.Status` transitions from `Draft` to `Published`, and the initial staffing context opens.

---

## 2. Sequence Diagram

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Operations Admin
    actor CL as Candidate Chief Leader
    participant SPA as Angular Web App
    participant Host as ASP.NET Core API
    participant ModAuthz as ADR-003 Authz
    participant ModEvents as Events Module
    participant ModIdentity as Identity Module
    participant ModNotif as Notifications Module
    participant Audit as Audit Persistence
    participant DB as Persistence Store

    %% ------------------------------------------------------------------------
    %% PHASE 1: EVENT CREATION (DRAFT)
    %% ------------------------------------------------------------------------
    rect rgb(240, 248, 255)
    Note over Admin, DB: PHASE 1: Event Creation (Draft)
    Admin->>SPA: Submit Event Details (Title, Time Window, Location)
    SPA->>Host: POST /api/events (Command: CreateEvent)
    Host->>ModAuthz: Tier 1 Role Check (RequireAdmin)
    alt Authz Denied (Not Admin)
        ModAuthz-->>Host: 403 Forbidden
        Host-->>SPA: 403 Forbidden
    else Authz Passed
        ModAuthz-->>Host: Authorized
        Host->>ModEvents: Execute CreateEvent(Title, Window, Location)
        ModEvents->>ModEvents: Invariant Check: StartTime < EndTime
        Note over ModEvents, DB: Transaction 1A: Commit Draft Event
        ModEvents->>DB: Persist Event (Status='Draft')
        ModEvents-->>Host: EventCreatedResult (EventId, Status='Draft')
        Host-->>SPA: 201 Created (EventId, Status='Draft')
        SPA-->>Admin: Display Event Draft in Admin Console
    end
    end

    %% ------------------------------------------------------------------------
    %% PHASE 2: APPOINT CHIEF LEADER (PENDING)
    %% ------------------------------------------------------------------------
    rect rgb(255, 250, 240)
    Note over Admin, DB: PHASE 2: Appoint Predefined Chief Leader
    Admin->>SPA: Select Chief Leader candidate from directory
    SPA->>Host: POST /api/events/{id}/chief-leader (Command: AppointChiefLeader)
    Host->>ModAuthz: Tier 1 Role Check (RequireAdmin)
    ModAuthz-->>Host: Authorized
    Host->>ModEvents: AppointChiefLeader(EventId, TargetUserId)
    ModEvents->>ModIdentity: Verify InChiefLeaderPool(TargetUserId)
    alt User not in Predefined Pool
        ModIdentity-->>ModEvents: Invalid Candidate
        ModEvents-->>Host: DomainRuleViolationException ("User not in predefined Chief Leader pool")
        Host-->>SPA: Domain Error (HTTP 4xx)
    else User in Pool
        ModIdentity-->>ModEvents: Valid Candidate
        ModEvents->>ModEvents: Invariant Check: Event is in Draft; no Active Chief Leader exists
        Note over ModEvents, DB: Transaction 1B: Atomic Appointment + Audit
        ModEvents->>DB: Persist ChiefLeaderAssignment (Status='Pending')
        ModEvents->>ModNotif: Trigger ChiefLeaderAppointedEvent (Approved Notification Requirement)
        ModNotif->>DB: Record Notification Intent (Target=TargetUserId)
        ModEvents-->>Host: ChiefLeaderAppointedResult (AssignmentId, Status='Pending')
        Host-->>SPA: 200 OK (Status='Pending')
        SPA-->>Admin: Display "Pending Chief Leader: Awaiting Acceptance"
    end
    end

    %% ------------------------------------------------------------------------
    %% PHASE 3A: DIRECT ACCEPTANCE (HAPPY PATH)
    %% ------------------------------------------------------------------------
    rect rgb(240, 255, 240)
    Note over CL, DB: PHASE 3A: Chief Leader Acceptance (Direct)
    CL->>SPA: View Notification & Click "Accept Assignment"
    SPA->>Host: POST /api/events/{id}/chief-leader/accept (Command: AcceptChiefLeaderAssignment)
    Host->>ModAuthz: Tier 1 & Tier 2 Check (ChiefLeader Role + Resource Ownership)
    alt Authz Denied (Not the assigned candidate)
        ModAuthz-->>Host: 403 Forbidden
        Host-->>SPA: 403 Forbidden
    else Authz Passed
        ModAuthz-->>Host: Authorized
        Host->>ModEvents: AcceptAssignment(AssignmentId, UserId)
        ModEvents->>ModEvents: Tier 3 Invariant Check: Assignment.Status == Pending
        ModEvents->>ModEvents: Check Same-Event Exclusivity (No Active TeamAssignment on this event)
        alt Concurrency Collision (Candidate for ERD Review)
            ModEvents->>DB: Update ChiefLeaderAssignment (Optimistic Concurrency Check)
            DB-->>ModEvents: Concurrency Exception
            ModEvents-->>Host: ConcurrencyConflictException
            Host-->>SPA: Domain Error (HTTP 4xx: Conflict)
        else Transition Valid
            Note over ModEvents, DB: Transaction 2: Atomic Transition + Audit Evidence
            ModEvents->>DB: Update ChiefLeaderAssignment (Status='Active')
            ModEvents->>Audit: Record Required Audit Evidence (Action='ChiefLeaderAccepted', Source='DirectUserAction')
            ModEvents-->>Host: AssignmentAcceptedResult (Status='Active')
            Host-->>SPA: 200 OK (Status='Active')
            SPA-->>CL: Display "You are Chief Leader for this Event"
        end
    end
    end

    %% ------------------------------------------------------------------------
    %% PHASE 3B: DECLINE ALTERNATIVE BRANCH
    %% ------------------------------------------------------------------------
    rect rgb(255, 240, 245)
    Note over CL, DB: PHASE 3B: Alternate Path — Chief Leader Declines
    CL->>SPA: Click "Decline Assignment" (Optional Note)
    SPA->>Host: POST /api/events/{id}/chief-leader/decline
    Host->>ModAuthz: Tier 1 & Tier 2 Check (Assigned Candidate)
    ModAuthz-->>Host: Authorized
    Host->>ModEvents: DeclineAssignment(AssignmentId, UserId, OptionalNote)
    Note over ModEvents, DB: Transaction 2-Alt: Historical Preservation of Decline + Audit
    ModEvents->>DB: Update ChiefLeaderAssignment (Status='Declined')
    ModEvents->>Audit: Record Required Audit Evidence (Action='ChiefLeaderDeclined', Source='DirectUserAction')
    ModEvents->>ModNotif: Trigger ChiefLeaderDeclinedEvent (Candidate Notification Trigger — pending spec)
    ModNotif->>DB: Record Notification Intent (Target=Admin)
    ModEvents-->>Host: AssignmentDeclinedResult (Status='Declined')
    Host-->>SPA: 200 OK (Status='Declined')
    SPA-->>CL: Confirmation: Assignment Declined
    Note over Admin, ModEvents: Event remains in Draft; Publishing prerequisite CANNOT be satisfied.
    end

    %% ------------------------------------------------------------------------
    %% PHASE 4: EVENT PUBLICATION
    %% ------------------------------------------------------------------------
    rect rgb(245, 245, 255)
    Note over Admin, DB: PHASE 4: Event Publication
    Admin->>SPA: Click "Publish Event"
    SPA->>Host: POST /api/events/{id}/publish (Command: PublishEvent)
    Host->>ModAuthz: Tier 1 Check (RequireAdmin)
    ModAuthz-->>Host: Authorized
    Host->>ModEvents: PublishEvent(EventId, AdminId)
    ModEvents->>ModEvents: Tier 3 Guard: HasExactlyOneActiveChiefLeader(EventId)
    alt Prerequisite Failed (No Active Chief Leader)
        ModEvents-->>Host: DomainRuleViolationException ("Event cannot be published without an Active Chief Leader")
        Host-->>SPA: Domain Error (HTTP 4xx: Unprocessable)
        SPA-->>Admin: Show Error: "Must have an accepted Active Chief Leader to publish"
    else Prerequisite Satisfied
        Note over ModEvents, DB: Transaction 3: Atomic Publish + Initialize Staffing Context + Audit Evidence
        ModEvents->>DB: Update Event (Status='Published')
        ModEvents->>DB: Initialize Initial Staffing Context
        ModEvents->>Audit: Record Required Audit Evidence (Action='EventPublished', Actor=AdminId)
        ModEvents->>ModNotif: Trigger EventPublishedEvent (Candidate Notification Trigger — pending spec)
        ModNotif->>DB: Record Notification Intent
        ModEvents-->>Host: EventPublishedResult (Status='Published')
        Host-->>SPA: 200 OK (Status='Published')
        SPA-->>Admin: Display "Event Successfully Published"
    end
    end
```

---

## 3. Architecture & Boundary Analysis

### A. Authorization Points (ADR-003)
1. **Event Creation (Step 3)**: Tier 1 Global Policy `RequireAdmin`. Volunteer Leaders cannot initiate event drafts.
2. **Chief Leader Appointment (Step 10)**: Tier 1 `RequireAdmin`. Only administrators may designate candidates from the predefined pool.
3. **Chief Leader Acceptance (Step 21)**: Tier 2 Resource-based authorization. Evaluates `ChiefLeader` role and verifies that the calling user's ID matches the candidate on the target `Pending` `ChiefLeaderAssignment`.
4. **Publish Event (Step 43)**: Tier 1 `RequireAdmin`.

### B. Domain Invariants & Guard Checks
1. **Predefined Chief Leader Pool Constraint**: The appointed user must exist in the organization's predefined Chief Leader pool.
2. **Publishing Prerequisite**: `Event.Status == Published` strictly requires that the Event possesses exactly one `Active` (accepted) `ChiefLeaderAssignment`. If in `Draft`, `Pending`, or `Declined`, publication is rejected.
3. **Historical Record Preservation**: When an assignment is declined (Phase 3B), the record is **not** deleted. Its status is updated to `Declined`, preserving historical evidence.

### C. Concurrency-Sensitive Points (ERD Review Candidates)
- **Chief Leader Assignment State Transition**: When the Chief Leader accepts (`Pending` $\rightarrow$ `Active`), optimistic concurrency control is evaluated to protect against concurrent reassignment or conflicting status modifications. Exact database enforcement mechanisms will be finalized in the ERD and data model.

### D. Transaction Boundaries & Atomicity
- **Transaction 1A (Draft Event)**: Persists Event in `Draft` status.
- **Transaction 1B (Appointment)**: Persists `ChiefLeaderAssignment` (`Pending`). Notification intent is triggered as part of the successful workflow.
- **Transaction 2 (Acceptance)**: Transitions `ChiefLeaderAssignment` (`Active`) and records required audit evidence atomically.
- **Transaction 3 (Publication)**: Atomically transitions Event to `Published`, initializes initial staffing context, and records required audit evidence.
- *Reliable notification delivery*: Decoupled from the domain mutation transaction; exact persistence and delivery mechanism is governed by ADR-006.
