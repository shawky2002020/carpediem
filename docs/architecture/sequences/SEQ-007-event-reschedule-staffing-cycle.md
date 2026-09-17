# Sequence 007: True Date Reschedule & Staffing Context Rotation

* **Context**: Material event date modification, immutable preservation of prior staffing history, and rotation of the event's staffing context/revision.
* **Lifecycle Transitions Covered**:
  - `Event`: Remains `Published` (updated schedule dates)
  - `Staffing Context`: Prior staffing context/revision remains historical; new staffing context applies to the new date
  - `AvailabilityResponse`: Preserved under prior staffing context; new-date availability begins with `[No AvailabilityResponse]`
  - `TeamAssignment`: Preserved under prior staffing context; new date requires rebuilding roster
* **Authoritative Inputs**:
  - `LIFECYCLE_SPECIFICATION.md` (Section 2.C & Section 7 Approved Blocker 3)
  - `ADR-003` (Authorization: Tier 1 `RequireAdmin`)
  - `ADR-005` (Audit Trail Strategy — Audited Material Event Modifications)
  - `ADR-009` (SQL Server Persistence & Schema Organization)

---

## 1. Sequence Overview

This sequence details the architectural handling of an administrative date change (True Reschedule):
1. **Material Date Change (True Reschedule)**:
   - An Admin changes the calendar date of a `Published` Event (e.g., postponing an event by two weeks).
   - Because volunteer availability is strictly date-dependent, schedule availability and team assignments submitted for the original date are **inapplicable** to the new date.
2. **Immutable Staffing Context Boundary**:
   - The prior staffing context/revision remains **historical**.
   - All historical `AvailabilityResponses` and `TeamAssignments` tied to the prior context are **preserved immutably**. They are **never deleted, overwritten, or mutated in place**.
3. **Staffing Context Rotation & Fresh Availability**:
   - A new current staffing context/revision applies to the new date.
   - For the new date, all volunteer Leaders begin with **No AvailabilityResponse** (displayed as "Response Needed" in UI).
   - Volunteer availability reopens for the new date, and leadership rebuilds the team roster against the new context.
   - *Data Modeling Note*: Physical representation of staffing contexts/revisions will be determined during the ERD phase.
4. **Contrast with Minor Schedule Adjustments (Approved Rule)**:
   - If an Admin modifies only same-day hours (e.g., shifting start time from 09:00 to 10:00 on the same date) or updates venue instructions:
     - The existing staffing context is **retained**.
     - Existing availability and team assignments remain active.
     - Affected personnel are notified according to the approved rule.
5. **Audit & Notifications**:
   - Material Event changes/reschedules are auditable where required by the audit architecture; no mandatory free-text reason is required unless an approved Event Management requirement later introduces one.
   - In-platform notification intent is recorded alerting the assigned Chief Leader and volunteers previously engaged with the event.

---

## 2. Sequence Diagram

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Operations Admin
    actor CL as Assigned Chief Leader
    actor Leaders as Volunteer Leaders
    participant SPA as Angular Web App
    participant Host as ASP.NET Core API
    participant ModAuthz as ADR-003 Authz
    participant ModEvents as Events Module
    participant ModStaffing as Staffing Module
    participant ModNotif as Notifications Module
    participant Audit as Audit Persistence
    participant DB as Persistence Store

    %% ------------------------------------------------------------------------
    %% RESCHEDULE TRIGGER
    %% ------------------------------------------------------------------------
    rect rgb(255, 245, 235)
    Note over Admin, DB: Administrative True Reschedule (Date Change)
    Admin->>SPA: Edit Event Dates (Old: Oct 10 -> New: Oct 24)
    SPA->>Host: POST /api/events/{id}/reschedule (Command: RescheduleEvent)
    Host->>ModAuthz: Tier 1 Gate (RequireAdmin)
    ModAuthz-->>Host: Authorized
    Host->>ModEvents: RescheduleEvent(EventId, NewStartTime, NewEndTime, AdminId)
    end

    %% ------------------------------------------------------------------------
    %% DOMAIN EVALUATION & STAFFING CONTEXT ROTATION
    %% ------------------------------------------------------------------------
    rect rgb(240, 248, 255)
    Note over ModEvents, DB: Evaluate Schedule Change & Rotate Staffing Context
    ModEvents->>ModEvents: Compare Dates: NewDate != OldDate (TRUE RESCHEDULE DETECTED)
    
    Note over ModEvents, DB: Invariant: History Preserved! Prior staffing context records remain historical
    Note over ModEvents, DB: Transaction 1: Atomic Reschedule + Rotate Context + Audit Evidence
    ModEvents->>DB: Update Event Schedule Dates
    ModEvents->>DB: Establish New Current Staffing Context (Prior Context Marked Historical)
    ModEvents->>Audit: Record Required Audit Evidence (Action='Events.EventRescheduled', Actor=AdminId, OldValue=@oldDates, NewValue=@newDates)
    
    ModEvents->>ModNotif: Trigger EventRescheduledEvent (Notification Intent Recorded)
    ModNotif->>DB: Record Notification Intent (Target=CL & Prior Roster)
    ModEvents-->>Host: RescheduleResult (EventId, NewContextId)
    Host-->>SPA: 200 OK
    SPA-->>Admin: Display: "Event rescheduled. Prior staffing preserved; new context opened for availability."
    end

    %% ------------------------------------------------------------------------
    %% IMPACT ON READ MODELS & WORKFLOWS
    %% ------------------------------------------------------------------------
    rect rgb(240, 255, 240)
    Note over CL, Leaders: Post-Reschedule Operational State
    Note over Leaders, SPA: Leaders receive notification of event reschedule
    Leaders->>SPA: Open Event Page for New Date
    SPA->>Host: GET /api/events/{id}/my-status
    Host->>ModStaffing: GetLeaderEventStatus(EventId, LeaderUserId)
    ModStaffing->>ModEvents: GetCurrentStaffingContext(EventId)
    ModEvents-->>ModStaffing: New Current Staffing Context
    ModStaffing->>DB: Query Availability for Current Context & LeaderId
    DB-->>ModStaffing: None (No Record in New Context)
    ModStaffing-->>Host: StatusDto (Availability='No Response', Assignment='None')
    Host-->>SPA: 200 OK
    SPA-->>Leaders: Display "Response Needed: Are you available on Oct 24?"

    Note over CL, SPA: Chief Leader views Event Roster
    CL->>SPA: Open Staffing Workspace
    SPA->>Host: GET /api/events/{id}/roster
    Host->>ModStaffing: GetActiveRoster(EventId)
    ModStaffing->>ModEvents: GetCurrentStaffingContext(EventId)
    ModEvents-->>ModStaffing: New Current Staffing Context
    ModStaffing->>DB: Query TeamAssignments for Current Context
    DB-->>ModStaffing: Empty Roster (New context is fresh)
    ModStaffing-->>Host: Empty Roster List
    Host-->>SPA: 200 OK
    SPA-->>CL: Display "Roster open: awaiting volunteer availability for rescheduled date"
    end
```

---

## 3. Architecture & Boundary Analysis

### A. Authorization Points (ADR-003)
- **Reschedule Command**: Tier 1 `RequireAdmin`. Material Event changes/reschedules are auditable where required by the audit architecture; no mandatory free-text reason is required unless an approved Event Management requirement later introduces one.

### B. Domain Invariants & Guard Checks
1. **Material Date Change vs. Time Adjustment**:
   - If `NewStartTime.Date == OldStartTime.Date`, the change is classified as a minor schedule adjustment; the existing staffing context is retained.
   - If `NewStartTime.Date != OldStartTime.Date`, the system classifies the change as a **True Reschedule**, rotating the staffing context.
2. **Immutable Historical Preservation**:
   - `AvailabilityResponses` and `TeamAssignments` associated with the prior context remain permanently recorded.
   - This ensures full auditability of volunteer commitments: an organization can inspect who was originally scheduled on the moved date.
3. **Absence-Based Availability in New Context**:
   - The new staffing context begins with **No AvailabilityResponse** for all Leaders. The system does not guess or copy forward prior availability.

### C. Concurrency-Sensitive Points (ERD Review Candidates)
- **Concurrent Administrative Edits**: Optimistic concurrency control ensures an Admin does not reschedule an event while another Admin is simultaneously modifying event details.

### D. Transaction Boundaries & Atomicity
- **Transaction 1 (Event Reschedule)**:
  - Updates Event schedule dates.
  - Rotates staffing context (designating prior context as historical).
  - Appends required audit record with old/new dates.
  - Executed as a single atomic unit of work via `SaveChangesAsync()`.
- *Notification Intent*: Produced upon successful reschedule; delivery governed by ADR-006.
