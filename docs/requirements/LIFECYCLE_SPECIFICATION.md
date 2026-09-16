# Carpediem Formal Lifecycle Specification

> [!IMPORTANT]
> **Status: LOCKED / RATIFIED**
> **Governance Authority**: Stakeholder Approved & Ratified
> **Precedence**: Authoritative baseline for all downstream Architecture, Data, and Implementation artifacts.
> In accordance with project governance:
> 1. No implementation agent or architecture model may alter the states, transitions, or invariants codified herein without a formal Architecture / Requirement Clarification Note.
> 2. No physical database schemas, column mappings, or SQL keys are established in this document.
> 3. All states and transitions are strictly grounded in the locked canonical source of truth and approved stakeholder product decisions.

---

## 1. Domain Decomposition Overview

Carpediem separates operational coordination into five distinct, orthogonal lifecycles and an immutable schedule boundary to prevent lifecycle conflation:

1. **Event Lifecycle**: Governs the organizational readiness, scheduling, execution, and closeout of a gathering.
2. **ChiefLeaderAssignment Lifecycle**: Manages the formal appointment, notification, acceptance/decline workflow, and operational ownership of an event Chief Leader.
3. **AvailabilityResponse Lifecycle (Dimension A)**: Captures a volunteer Leader's declared schedule availability for an event staffing cycle.
4. **TeamAssignment Lifecycle (Dimension B)**: Records leadership's operational appointment of a specific Leader to an event team slot.
5. **AttendanceOutcome Lifecycle (Dimension C)**: Records the factual on-the-ground physical service outcome of an actively assigned Leader.
6. **StaffingCycle Schedule Revision Boundary**: Scopes availability and staffing to a specific event calendar date, archiving prior cycles immutably upon a material date change.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              EVENT LIFECYCLE                                │
│   [Draft] ─────────► [Published] ─────────► [Completed]                     │
│      │                     │                                                │
│      └──────────────┬──────┴──────────────► [Cancelled]                     │
│                     │                                                       │
│                     ▼                                                       │
│            [StaffingCycle Rotated on Reschedule]                            │
└─────────────────────┬───────────────────────────────────────────────────────┘
                      │
       ┌──────────────┴──────────────┬──────────────────────────────┐
       ▼                             ▼                              ▼
┌─────────────────────────┐   ┌────────────────────────┐   ┌──────────────────┐
│   CHIEF LEADER          │   │   DIMENSION A:         │   │   DIMENSION B:   │
│   ASSIGNMENT            │   │   AvailabilityResponse │   │   TeamAssignment │
│                         │   │                        │   │                  │
│   (No Assignment)       │   │   (No Record)          │   │   (No Record)    │
│       │                 │   │       │                │   │       │          │
│       ▼                 │   │       ▼                │   │       ▼          │
│   Pending               │   │   Available            │   │   Active         │
│     ├── Accept ──►Active│   │       ▲                │   │       │          │
│     └── Decline──►Decl. │   │       │                │   │       ▼          │
│                         │   │       ▼                │   │   Cancelled      │
│                         │   │   NotAvailable         │   │                  │
└─────────────────────────┘   └────────────────────────┘   └────────┬─────────┘
                                                                    │
                                                                    ▼
                                                           ┌──────────────────┐
                                                           │   DIMENSION C:   │
                                                           │   Attendance     │
                                                           │   Outcome        │
                                                           │                  │
                                                           │   Unrecorded     │
                                                           │       │          │
                                                           │       ▼          │
                                                           │   Attended /     │
                                                           │   NoShow         │
                                                           └──────────────────┘
```

---

## 2. Event Lifecycle

### A. Persisted States

| State | Description | Entry Criteria | Exit Criteria |
| :--- | :--- | :--- | :--- |
| **`Draft`** | Event is being authored or prepared by an Admin. Invisible to volunteer Leaders. Chief Leader assignment may be uninitiated or pending. | Event created by Admin. | Event is Published by Admin, or Cancelled/Discarded. |
| **`Published`** | Event is officially scheduled and visible to authorized Leaders. Availability collection and team selection are active. | Admin triggers publish action. **Strict Precondition**: Event must possess an accepted, `Active` Chief Leader assignment (`Event.Status == Published => Event has exactly one active accepted ChiefLeaderAssignment`) and scheduled time window must be defined. | Scheduled time window concludes (transitions to `Completed`), or Admin executes cancellation. |
| **`Completed`** | Event scheduled window has concluded, or Admin has formally closed out the event. | Current system time exceeds `Event.EndTime` (`Now > EndTime`), or explicit Admin closeout. | Terminal state. Historical records preserved. |
| **`Cancelled`** | Event is formally called off by an Admin. Roster and staffing are disbanded. | Admin triggers cancellation with mandatory recorded justification. | Terminal state. Notifications dispatched; historical records preserved. |

### B. Derived & Operational Conditions (Non-Persisted Statuses)

These conditions are evaluated dynamically at runtime and are **not** stored as separate database status strings:

1. **`In Progress`**:
   - *Definition*: Evaluated dynamically when `Event.Status == Published` and `Event.StartTime <= CurrentTime <= Event.EndTime`.
   - *Meaning*: Event is physically underway.
2. **`Pending Attendance`**:
   - *Definition*: Evaluated dynamically when `Event.Status == Completed` and one or more `Active` `TeamAssignments` have `AttendanceOutcome == Unrecorded`.
   - *Meaning*: Operational flag on Admin and Chief Leader dashboards signaling that on-site attendance finalization is overdue. Decoupled from event completion.

### C. Rescheduling Boundary: `StaffingCycle`

- When an Admin changes the **date** of an event (material reschedule):
  1. The event status remains `Published`.
  2. The current `StaffingCycle` is sealed and archived immutably in historical logs.
  3. Prior availability responses are invalidated for the new date.
  4. A fresh `StaffingCycle` is opened for the new date, resetting availability to `No AvailabilityResponse` for all Leaders.
  5. Leadership rebuilds staffing against the new cycle.
  6. Affected Leaders and Chief Leaders receive high-priority in-platform notifications.

### D. Event Transition Table

| Current State | Transition Event / Action | Actor | Preconditions / Guard Rules | Next State | Side Effects |
| :--- | :--- | :--- | :--- | :--- | :--- |
| *[None]* | Create Event | Admin | Valid title, scheduled window. | `Draft` | Draft record created. |
| `Draft` | Initiate Chief Leader Assignment | Admin | Selected Chief Leader must be in predefined pool. | `Draft` | Creates `ChiefLeaderAssignment` in `Pending` state. |
| `Draft` | Publish Event | Admin | **Strict Invariant**: Event has exactly one `Active` (accepted) `ChiefLeaderAssignment`, `StartTime < EndTime`, required fields present. | `Published` | Opens initial `StaffingCycle`; dispatches notification to Chief Leader and Leaders. |
| `Draft` | Cancel / Discard | Admin | None. | `Cancelled` | Event marked cancelled without Leader-facing notifications. |
| `Published` | Schedule Window Concludes | System Clock / Admin | `CurrentTime > Event.EndTime`. | `Completed` | Sets event completed; raises `Pending Attendance` if attendance unrecorded. |
| `Published` | Material Date Change | Admin | New start/end dates specified. | `Published` (Same state) | Seals prior `StaffingCycle`; opens new `StaffingCycle`; invalidates old availability; notifies roster. |
| `Published` | Cancel Event | Admin | Mandatory cancellation reason provided. | `Cancelled` | Transitions all `Active` assignments to `Cancelled`; notifies Chief Leader and Leaders. |

---

## 3. ChiefLeaderAssignment Lifecycle

Manages the appointment, acceptance/decline workflow, and operational ownership of an event Chief Leader.

### A. States & Representation

- **`No ChiefLeaderAssignment` (Absence of Record)**:
  - Initial condition for a newly drafted event before an Admin appoints a Chief Leader.
  - Not stored as a persisted status value.
- **`Pending`**:
  - Admin has appointed a specific candidate from the predefined Chief Leader group.
  - Triggers an in-platform notification to the appointed Chief Leader.
  - Awaiting explicit acceptance or decline.
- **`Active`**:
  - The Chief Leader has accepted the assignment, OR an Admin recorded an externally confirmed acceptance with mandatory audit note.
  - Grants the Chief Leader operational ownership of the event (team selection, attendance logging).
  - Satisfies the mandatory publishing prerequisite for the Event.
- **`Declined`**:
  - The Chief Leader explicitly declined the assignment.
  - Preserved immutably as historical evidence.
  - Does NOT grant operational ownership; leaves the Event without an active Chief Leader. Admin must appoint another candidate before publication.

### B. Invariants & Rules

1. **Predefined Pool Constraint**: Chief Leaders are a predefined group of people in the organization. No qualification workflow exists.
2. **Publishing Prerequisite**: An Event cannot transition from `Draft` to `Published` without an accepted `Active` Chief Leader assignment (`Event.Status == Published => Event has exactly one active accepted ChiefLeaderAssignment`).
3. **Single Active Assignment**: An Event may possess at most one `Active` `ChiefLeaderAssignment` at any given time.
4. **External Confirmation Exception**:
   - If a Chief Leader confirms verbally or through an external channel, an Admin may record acceptance on their behalf.
   - Must NOT behave like a silent Admin override.
   - Admin must explicitly invoke the action: `"Record external acceptance"`.
   - The system strictly records: acting Admin ID, timestamp, Chief Leader ID, Event ID, and a mandatory audit note / justification.
   - The audit log explicitly differentiates:
     - `Chief Leader accepted directly`
     - `Admin recorded externally confirmed acceptance`
5. **Historical Preservation**: `Declined` assignments are retained immutably in the historical audit trail rather than deleted or overwritten.
6. **Post-Publish Replacement Boundary**:
   - The initial appointment and acceptance workflow is locked.
   - Emergency reassignment or replacement of an `Active` Chief Leader after an event is already `Published` is non-blocking for core lifecycles and is formally deferred to the Event Management feature specification.

### C. Transition Table

| Current State | Trigger / Action | Actor | Preconditions / Rules | Next State | Side Effects |
| :--- | :--- | :--- | :--- | :--- | :--- |
| *No Assignment* | Appoint Chief Leader | Admin | Target user is in predefined Chief Leader pool. | `Pending` | In-platform notification dispatched to Chief Leader. |
| `Pending` | Accept Assignment | Chief Leader | Chief Leader acts directly in-platform. | `Active` | Chief Leader becomes operational event owner; satisfies publishing prerequisite; audit source: `Direct`. |
| `Pending` | Record External Acceptance | Admin | Chief Leader confirmed externally; mandatory audit note provided. | `Active` | Chief Leader becomes operational event owner; satisfies publishing prerequisite; audit source: `AdminExternal`. |
| `Pending` | Decline Assignment | Chief Leader | Chief Leader declines in-platform. | `Declined` | Assignment closed as historical record; in-platform alert sent to Admin; event remains unstaffed. |
| `Declined` | Appoint New Candidate | Admin | Target user is in predefined Chief Leader pool. | `Pending` (New Record) | A new `ChiefLeaderAssignment` record is created in `Pending` status. |

---

## 4. AvailabilityResponse Lifecycle (Dimension A)

Represents a volunteer Leader's declared schedule availability for a specific `StaffingCycle`.

### A. States & Representation

- **`No AvailabilityResponse` (Absence of Record)**:
  - Default initial condition when an event is published or when a new `StaffingCycle` begins.
  - Not stored as a database row or persisted enum value.
  - Presented in Leader UI as **"Response Needed"** and in Chief Leader filtering as **"No Response"**.
- **`Available`**:
  - Leader submitted affirmative schedule availability.
  - Automatically qualifies the Leader as a candidate in the Chief Leader's selection pool.
- **`NotAvailable`**:
  - Leader submitted negative schedule availability (cannot work).

### B. Invariants & Rules

1. **Self-Service Mutability**: A Leader may freely toggle between `Available` and `NotAvailable` while the event's availability window remains open prior to event start.
2. **Decoupled from Assignment**: Submitting availability does not create, modify, or erase a `TeamAssignment`. An unselected `Available` Leader remains simply `Available`.
3. **Reschedule Invalidation**: When an event date changes, all availability responses for the previous date are archived under the old `StaffingCycle`, and the new cycle begins with `No AvailabilityResponse`.

### C. Transition Table

| Current State | Action | Actor | Precondition | Next State |
| :--- | :--- | :--- | :--- | :--- |
| *No Record* | Mark Available | Leader | Event is `Published`; availability window open. | `Available` (Record Created) |
| *No Record* | Mark Not Available | Leader | Event is `Published`; availability window open. | `NotAvailable` (Record Created) |
| `Available` | Change to Not Available | Leader | Event is `Published`; availability window open. | `NotAvailable` (Record Updated) |
| `NotAvailable` | Change to Available | Leader | Event is `Published`; availability window open. | `Available` (Record Updated) |

---

## 5. TeamAssignment Lifecycle (Dimension B)

Records leadership's operational appointment of a specific Leader to an event team slot.

### A. States & Representation

- **`No TeamAssignment` (Absence of Record)**:
  - Default condition for any Leader who has not been placed on the team roster.
  - Not stored as a database row or persisted enum value.
- **`Active`**:
  - The Leader is currently staffed on the official event team roster.
  - Selection or direct drafting immediately places the Leader into `Active` status. There is no intermediate "Invited" or "Pending Confirmation" state for standard selection.
- **`Cancelled`**:
  - The Leader was staffed in `Active` status, but the assignment was formally terminated (via Leader self-cancellation, Admin override, or event cancellation).
  - An immutable historical record storing actor, timestamp, and mandatory cancellation reason.

### B. Invariants & Rules

1. **Direct Placement Authority**:
   - Chief Leaders select team members from the `Available` pool with immediate `Active` effect.
   - Chief Leaders and Admins may directly draft a Leader who is `NotAvailable` or has *No Record*; this also creates an immediate `Active` assignment (Approved Blocker 1).
2. **Controlled Self-Cancellation**:
   - An `Active` Leader may cancel their own assignment at any time via a controlled UI modal requiring explicit confirmation and a mandatory reason text.
   - Self-cancellation immediately transitions the assignment from `Active` to `Cancelled` and alerts the assigned Chief Leader and Admin.
   - Leaders cannot assign their own replacement.
3. **Immutable Replacement Flow**:
   - A `Cancelled` assignment is never deleted, reactivated, or overwritten.
   - When the Chief Leader drafts a replacement, the original assignment remains `Cancelled`, and a *new* distinct `TeamAssignment` record in `Active` status is created for the replacement Leader.
4. **Admin Override Rights**:
   - Admins possess audited override authority to add or remove any Leader from the roster. All Admin overrides require mandatory justification logged in the audit trail.

### C. Transition Table

| Current State | Trigger / Action | Actor | Preconditions / Rules | Next State | Side Effects |
| :--- | :--- | :--- | :--- | :--- | :--- |
| *No Record* | Select Candidate | Chief Leader | Leader marked `Available` in current cycle. | `Active` (Created) | Immediate confirmation; in-app notification sent to Leader. |
| *No Record* | Direct Draft | Chief Leader / Admin | Any active Leader account (even if not marked available). | `Active` (Created) | Immediate confirmation; in-app notification sent to Leader. |
| `Active` | Self-Cancel | Leader | Mandatory reason provided. | `Cancelled` | Assignment closed; notification dispatched to Chief Leader and Admin; slot reopened. |
| `Active` | Admin Removal | Admin | Mandatory administrative reason provided. | `Cancelled` | Immutable audit log written; notification dispatched to Leader and Chief Leader. |
| `Active` | Event Cancelled | System / Admin | Event transitions to `Cancelled`. | `Cancelled` | All active assignments cancelled; notification dispatched to roster. |
| `Cancelled` | Select Replacement | Chief Leader | Chief Leader selects replacement candidate. | `Cancelled` (Unchanged) | A **new** `TeamAssignment` is created with status `Active` for the replacement. |

---

## 6. AttendanceOutcome Lifecycle (Dimension C)

Factual ground-truth record of physical event service for an assigned Leader.

### A. States & Representation

- **`Unrecorded`**: Initial default value for any `Active` `TeamAssignment` prior to post-event roll call / attendance finalization.
- **`Attended`**: The assigned Leader physically attended, reported for duty, and completed service.
- **`NoShow`**: The assigned Leader was active on the roster, did not cancel in advance, and failed to appear for the event.

### B. Invariants & Rules

1. **Strict Dependency on Active Assignment**: Attendance exists strictly as an attribute or child of an `Active` `TeamAssignment`. It cannot exist for cancelled assignments or unassigned Leaders.
2. **Chief Leader Primary Finalization**:
   - The assigned Chief Leader has primary authority to mark and finalize attendance on-site or immediately post-event.
   - Submission by the Chief Leader takes immediate effect. There is no routine Admin approval gate or pending review state.
3. **Admin Audited Correction**:
   - Admins possess override and correction rights to modify an attendance outcome post-finalization (e.g., to correct an honest error or grant an excused absence).
   - Any Admin correction creates an immutable audit log capturing `OldOutcome`, `NewOutcome`, `ChangedByAdminId`, and `MandatoryReason`.
4. **Factual Integrity (No Synthetic Scores)**:
   - Attendance outcomes feed raw, factual counts into the Leader's profile (`Events Attended`, `No-Shows`, `Cancellations`).
   - No synthetic reliability scores, weighting formulas, or algorithms are calculated.
5. **Operational Decoupling**:
   - Missing attendance after an event has finished does not prevent the Event from completing.
   - It surfaces as an operational condition (`Pending Attendance`) on dashboards until submitted.

### C. Transition Table

| Current State | Trigger / Action | Actor | Rules / Preconditions | Next State | Side Effects |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Unrecorded` | Finalize Attended | Chief Leader | Assigned event has commenced or ended; assignment is `Active`. | `Attended` | Updates Leader's factual attended count immediately. |
| `Unrecorded` | Finalize No-Show | Chief Leader | Assigned event has commenced or ended; Leader failed to appear. | `NoShow` | Updates Leader's factual no-show count immediately. |
| `Attended` ↔ `NoShow` | Admin Correction | Admin | Mandatory correction justification recorded. | Corrected State | Immutable audit trail recorded; updates Leader factual metrics. |

---

## 7. Matrix of Approved Decisions Mapped to Lifecycle Invariants

| Approved Decision | Lifecycle Impact & Concrete Rule | Grounding Document |
| :--- | :--- | :--- |
| **Chief Leader Role Model** | Predefined pool; assigned by Admin; exclusive leadership role (cannot be standard Leader on any event). | Section 2 & Blocker 2 |
| **Chief Leader Assignment Flow** | Admin appoints (`Pending`); Chief Leader accepts (`Active`) or declines (`Declined`); external acceptance recorded by Admin with mandatory audit log; `Declined` preserved in history. | Chief Leader Flow Decision |
| **Publishing Prerequisite** | An Event cannot be published unless it possesses an accepted, `Active` Chief Leader assignment (`Event.Status == Published => Event has exactly one active accepted ChiefLeaderAssignment`). | Decision 1 & Chief Leader Flow |
| **Availability vs. Application** | Single unified response (`Available` / `NotAvailable`); marking `Available` directly qualifies for selection; no separate Application form. | Decision Group 2 |
| **Team-Selection Authority** | Chief Leader selects directly with immediate effect; Admin holds audited oversight and override. | Decision Group 3 |
| **Cancellation & Replacement** | Leader self-cancels with mandatory reason; Chief Leader directly selects replacement; full history preserved; replacements generate new assignment records. | Decision Group 4 |
| **Final Confirmation** | Selection immediately creates confirmed `Active` team placement; no redundant confirmation step. | Decision Group 5 |
| **Direct Assignment** | Chief Leader or Admin can draft any Leader regardless of availability response; takes immediate effect; Leader must cancel if unable to serve. | Blocker 1 |
| **Rescheduling / Material Edits** | Date changes rotate `StaffingCycle`, invalidate old availability, reopen availability for new date, and preserve prior cycle immutably. | Blocker 3 |
| **Attendance Authority & Finalization** | Chief Leader directly finalizes attendance without an Admin approval gate; Admin possesses audited correction rights; decoupled from event completion. | Decision 2 |
| **Absence Modeling Normalization** | Absence of availability = `No AvailabilityResponse` (UI shows "Response Needed"); Absence of assignment = `No TeamAssignment` (Unassigned). No synthetic `Unanswered` or `None` stored. | Normalization Corrections 1 & 2 |
| **Event Completion Decoupling** | Event completes by schedule window; missing attendance is an operational condition (`Pending Attendance`), not an event state. | Normalization Correction 3 |

---

## 8. Governance Status: LOCKED & RATIFIED

> [!CAUTION]
> **Lifecycle Foundation Locked — Architecture Modeling Authorized; Code Implementation Remains Blocked.**
> This document completes and formally locks the Carpediem text-level lifecycle and state machine discovery.
> In compliance with Carpediem governance:
> - **AUTHORIZED**: Architectural modeling (C4 Context, C4 Container, Modular Monolith boundaries, ADR backlog).
> - **BLOCKED**: No ASP.NET Core entity models, DbContext classes, or EF migrations.
> - **BLOCKED**: No Angular state management services, routes, or UI components.
> - **BLOCKED**: No physical database schemas, SQL DDL, or application implementation code.
