# Draft Conceptual Domain Model

> [!IMPORTANT]
> **Conceptual Candidates Only — NOT Physical Database Tables.**
> These definitions model problem-space concepts, responsibilities, known invariants, and open ambiguities. No physical database schemas, column mappings, or SQL keys are established in this document.

---

## 1. Conceptual Domain Entities

### User
- **Responsibility**: Represents an authenticated human identity capable of logging into Carpediem.
- **Relationships**:
  - Has one or more `Roles`.
  - Has zero or one associated `LeaderProfile` (if authorized as Leader / Chief Leader).
  - Initiator of actions recorded in `AuditLogs`.
- **Known Invariants**:
  - Must have a verified unique identifier (email/username).
  - Must be in an active state to authenticate and perform operations.
- **Unresolved Questions**:
  - Can a single User simultaneously hold Admin and Leader capabilities, or are roles mutually exclusive per session?
  - Does User lifecycle include explicit states like `Invited`, `Active`, `Suspended`, `Deactivated`?

### Role
- **Responsibility**: Categorizes system permissions and operational scope (`Admin`, `Chief Leader`, `Leader`).
- **Relationships**:
  - Associated with `User` via RBAC mapping.
- **Known Invariants**:
  - Exactly three distinct roles exist in V1 (`Admin`, `Chief Leader`, `Leader`).
  - Chief Leaders are a predefined set of people in the organization. They are not Leaders who become qualified through the system, and there is no system qualification workflow.
  - Admins choose one of these known Chief Leaders for an event, initiating a `Pending` assignment that requires explicit acceptance (or audited external confirmation) to become `Active`.
  - Role Exclusivity Model (SUPERSEDED by [CHANGE-001](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md)): *Originally locked under Decision Blocker 2 ("Chief Leaders exclusively lead events. They cannot mark availability, apply, or participate as normal Leaders on any event").* Superseded by CHANGE-001: Role membership is non-exclusive; a user belonging to the predefined Chief Leader pool may also hold the `Leader` role if granted by an Admin. Dual-role users can participate as normal Leaders on events where they are not the active Chief Leader.
  - Same-Event Exclusivity Invariant (LOCKED per CHANGE-001): For any Event $E$ and User $U$, holding an `Active` `ChiefLeaderAssignment(E)` and an `Active` `TeamAssignment(E)` is strictly mutually exclusive ($\text{Active ChiefLeaderAssignment}(E, U) \implies \neg\text{Active TeamAssignment}(E, U)$). Any active team assignment for $U$ on $E$ must be resolved before a `Pending` Chief Leader assignment can transition to `Active`.
  - Role-based permissions are enforced server-side.
- **Unresolved Questions**:
  - Can an Admin also hold Leader capabilities, or are roles strictly single-role per user in V1 (outside the approved ChiefLeader + Leader dual-role model)?

### LeaderProfile
- **Responsibility**: Holds operational domain information regarding a Leader (contact details, qualifications, participation history, factual commitment indicators).
- **Relationships**:
  - Belongs to one `User`.
  - Has many `Participation` records.
  - Has many `Attendance` records.
  - Has many `Evaluations`.
- **Known Invariants**:
  - Maintains factual counts (events attended, cancellations, no-shows); no synthetic rating formula in V1.
- **Unresolved Questions**:
  - What personal fields are required (phone number, shirt size, emergency contact, dietary restrictions, skills/certifications)?
  - What are the privacy boundaries for profile data across roles?

### Event
- **Responsibility**: Represents an organized gathering requiring staffing, coordination, and leadership.
- **Relationships**:
  - Created by an `Admin`.
  - Associated with `ChiefLeaderAssignment` records (must have exactly one `Active` assignment to transition to `Published`).
  - Has many `Participations` / `TeamAssignments`.
  - Has one `AttendanceSheet` / many `Attendance` records.
  - Has zero or more `Incidents`.
- **Known Invariants**:
  - Must specify a defined scheduled window (Start Time, End Time).
  - Must be associated with an event status lifecycle.
  - Critical changes must be audited.
  - Publishing Prerequisite (APPROVED): An Event cannot transition from `Draft` to `Published` unless there is an accepted, `Active` Chief Leader assignment (`Event.Status == Published => Event has exactly one active accepted ChiefLeaderAssignment`).
  - Event Modifications & Rescheduling (APPROVED - Blocker 3):
    - *Minor edit* (notes, description, meeting details): Preserves all availability and team assignments.
    - *Same-day time/location change*: Preserves availability and team assignments; immediately dispatches clear notification to affected personnel.
    - *Date change / true reschedule*: Invalidates availability for the new date; previous responses and assignments are retained immutably in historical logs; a new staffing cycle is opened for the new date.
  - Decoupling of Event Completion & Attendance Finalization:
    - An Event reaches completion based on the conclusion of its scheduled window (or explicit administrative closeout), independent of whether attendance records have been submitted.
    - Missing or unsubmitted attendance after an event has ended is an operational condition (`Pending Attendance`), not an Event lifecycle state.
- **Conceptual Sub-Component: StaffingCycle (Schedule Revision)**:
  - *Responsibility*: Scopes availability responses and team assignments to a specific scheduled event date.
  - When an event date is changed, the current `StaffingCycle` is archived historically, and a fresh `StaffingCycle` opens for the new date, preventing state corruption.
- **Unresolved Questions**:
  - What are the mandatory fields at creation time?

### ChiefLeaderAssignment
- **Responsibility**: Manages the formal appointment, notification, acceptance/decline workflow, and operational ownership of a Chief Leader for an Event.
- **Relationships**:
  - Belongs to an `Event`.
  - References a `User` in the predefined Chief Leader pool.
  - Initiated by an `Admin`.
- **States / Model**:
  - `No ChiefLeaderAssignment`: Absence of assignment record prior to Admin selection. Not persisted as a status.
  - `Pending`: Admin appointed a Chief Leader. Dispatches an in-platform notification. Awaiting explicit response.
  - `Active`: Chief Leader accepted, or Admin recorded an externally confirmed acceptance with mandatory audit justification. Chief Leader becomes operational event owner; satisfies the publishing prerequisite.
  - `Declined`: Chief Leader explicitly declined. Preserved immutably as historical evidence; does NOT grant ownership; event remains without an active Chief Leader. Admin must appoint another candidate.
- **Known Invariants**:
  - Predefined Pool: Only users in the predefined Chief Leader pool can be assigned. No qualification workflow exists.
  - Single Active Assignment: An Event may have at most one `Active` ChiefLeaderAssignment at any time.
  - External Confirmation Exception: Admin may record acceptance on behalf of a Chief Leader ONLY when the Chief Leader confirmed outside Carpediem. Requires explicit action "Record external acceptance" and mandatory audit record: acting Admin, timestamp, Chief Leader, Event, and mandatory audit note/reason. Audit trail must strictly distinguish direct Chief Leader acceptance from Admin-recorded external acceptance.
  - Historical Preservation: `Declined` assignments are retained immutably in history rather than deleted or overwritten.
- **Deferred to Event Management Feature Specification**:
  - The procedure and rules for replacing an already `Active` Chief Leader after an Event has been Published do not block core lifecycles and are deferred to the Event Management feature specification.

---

## 2. Normalized Participation Model (Three Independent Dimensions)

Participation is decomposed into three distinct, orthogonal business dimensions to prevent lifecycle conflation:

### Dimension A: AvailabilityResponse
- **Responsibility**: Captures a Leader’s declared availability for an event staffing cycle.
- **States / Model**:
  - `No AvailabilityResponse`: Absence of a response record. The UI dynamically presents this condition as "Response Needed". Not persisted as a status value.
  - `Available`: Leader stated willingness/schedule match to work (enters candidate pool).
  - `NotAvailable`: Leader stated they cannot work this schedule.
- **Known Invariants**:
  - Lifecycle: `No AvailabilityResponse` → `Available` ↔ `NotAvailable`.
  - Independent of Assignment: Marking `Available` or `NotAvailable` is purely an expression of volunteer schedule. It is never overwritten or erased when leadership creates an assignment.
  - Mutability: Leaders may update their response while the event's availability window remains open.

### Dimension B: TeamAssignment
- **Responsibility**: Records leadership's operational appointment of a specific Leader to an event team slot.
- **States / Model**:
  - `No TeamAssignment`: Absence of an assignment record. The Leader has not been placed on the team roster. Not persisted as a status value.
  - `Active`: Leader is currently staffed on the event team (immediate placement upon selection or direct assignment).
  - `Cancelled`: Leader was staffed, but participation was cancelled (either by self-cancellation with reason or Admin override).
- **Known Invariants**:
  - Lifecycle: `No TeamAssignment` → `Active` → `Cancelled`.
  - Orthogonal to Availability: An `Active` assignment can be created for a Leader regardless of whether an `AvailabilityResponse` exists (`Available`, `NotAvailable`, or no response) (Approved Blocker 1).
  - Controlled Cancellation: Cancellation transitions `TeamAssignment` from `Active` to `Cancelled`, capturing timestamp, actor, and mandatory reason. Full history is preserved.
  - Replacement: When Chief Leader selects a replacement, the original `TeamAssignment` remains in `Cancelled` status, and a *new* `TeamAssignment` in `Active` status is created for the replacement Leader.
  - Selection Authority: Chief Leader selects team directly with immediate effect. Admins possess auditable oversight and override authority.

### Dimension C: AttendanceOutcome
- **Responsibility**: Factual ground-truth record of physical event service for a specific `Active` `TeamAssignment`.
- **States / Values**:
  - `Unrecorded`: Initial default for an active team assignment prior to event execution / attendance logging.
  - `Attended`: The assigned Leader physically attended and served.
  - `NoShow`: The assigned Leader was active on the roster, did not cancel in advance, and failed to attend.
- **Known Invariants**:
  - Bound to Active Assignment: Attendance is an outcome of an `Active` `TeamAssignment`. It cannot exist for a `Cancelled` assignment or for a Leader without an assignment.
  - Attendance Authority & Immediate Finalization (APPROVED): Assigned Chief Leaders have primary operational authority to finalize attendance directly on-site or post-event. There is no intermediate administrative approval gate.
  - Admin Audited Correction (APPROVED): Admins possess override and post-finalization correction rights to adjust attendance outcomes (e.g., in case of dispute or excused absence). All corrections require an immutable audit trail capturing previous state, new state, acting admin, and mandatory justification reason.
  - Ground-Truth Record: Factual attendance logging without synthetic formulas or arbitrary rating algorithms.
  - Operational Decoupling: Missing attendance after an event has ended is an operational condition (`Pending Attendance`), not an Event lifecycle state.

### Evaluation
- **Responsibility**: Post-event performance feedback and operational commentary recorded by Chief Leaders.
- **Relationships**:
  - Authored by `Chief Leader` regarding a `LeaderProfile` for a specific `Event`.
- **Known Invariants**:
  - Factual feedback; must not be calculated into an arbitrary synthetic rating formula in V1.
- **Unresolved Questions**:
  - Is evaluation content visible to the Leader, or strictly restricted to Chief Leaders and Admins?
  - Can Leaders evaluate the Chief Leader or the Event itself?

### Incident
- **Responsibility**: Records unexpected operational disruptions, safety concerns, or behavioral issues occurring during an event.
- **Relationships**:
  - Associated with an `Event`; may optionally reference involved `LeaderProfiles`.
  - Authored by `Chief Leader` or `Admin`.
- **Known Invariants**:
  - Sensitive record with restricted access.
- **Unresolved Questions**:
  - Who has permission to view incidents?
  - Can an Incident record be edited after submission, or does it require an append-only addendum?

### Notification
- **Responsibility**: In-platform operational message informing a user of a relevant event or action required.
- **Relationships**:
  - Directed to a `User`.
  - Optionally references an `Event` or `TeamAssignment`.
- **Known Invariants**:
  - Restricted strictly to in-platform display in V1 (no SMS, push, or WhatsApp).
- **Unresolved Questions**:
  - What are the explicit triggers and expiration rules for notifications?
  - Does a notification track read/unread and dismissal states?
