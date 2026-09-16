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
  - Exactly three distinct roles exist in V1.
  - Role-based permissions are enforced server-side.
- **Unresolved Questions**:
  - Is `Chief Leader` an overarching user role, or is it an event-specific assignment granted to a qualified Leader for a specific Event?

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
  - Assigned to zero or one `Chief Leader`.
  - Has many `Participations` / `TeamAssignments`.
  - Has one `AttendanceSheet` / many `Attendance` records.
  - Has zero or more `Incidents`.
- **Known Invariants**:
  - Must specify a defined scheduled window (Start Time, End Time).
  - Must be associated with an event status lifecycle.
  - Critical changes must be audited.
- **Unresolved Questions**:
  - What are the mandatory fields at creation time?
  - Can an Event be published before a Chief Leader is assigned?
  - What exact state machine governs an Event from inception to completion?
  - Does changing date/location invalidate existing confirmations?

### Participation (Candidate Aggregate / Concept)
- **Responsibility**: Captures a Leader’s journey and state in relation to a specific Event.
- **Relationships**:
  - Connects one `LeaderProfile` to one `Event`.
  - May encompass or relate to `AvailabilityResponse`, `Application`, `TeamAssignment`, `Confirmation`, and `Attendance`.
- **Known Invariants**:
  - A Leader can have at most one active participation context per Event.
  - State changes must be auditable and transition-validated.
- **Unresolved Questions**:
  - Is `Participation` a single overarching aggregate with a lifecycle state machine, or is it broken into discrete entities (`AvailabilityResponse`, `Application`, `TeamAssignment`)?

### AvailabilityResponse
- **Responsibility**: Records a Leader’s declared availability/unavailability for an upcoming Event window.
- **Relationships**:
  - Associated with a `LeaderProfile` and an `Event` (or date range).
- **Known Invariants**:
  - A Leader can update their availability response while the availability window is open.
- **Unresolved Questions**:
  - Is availability passive (calendar preference) or active (requesting to work)?
  - What are the allowed values (`Available`, `Not Available`, `Maybe`)?
  - Does availability automatically qualify a Leader for team selection, or must they submit an explicit `Application`?

### Application
- **Responsibility**: A formal expression of interest by a Leader to work a specific Event.
- **Relationships**:
  - Submitted by a `LeaderProfile` for an `Event`.
- **Known Invariants**:
  - Cannot apply to an event after the application deadline has closed.
- **Unresolved Questions**:
  - Do Leaders apply to events, or does the Chief Leader draft from the pool of "Available" leaders?
  - Can a Leader withdraw an application before team selection without consequence?

### TeamAssignment
- **Responsibility**: The designation of a specific Leader to an event staffing slot by a Chief Leader or Admin.
- **Relationships**:
  - Relates one `Event` to one selected `LeaderProfile`.
  - Managed by the assigned `Chief Leader` or `Admin`.
- **Known Invariants**:
  - Only authorized roles can create or modify assignments.
  - History of assignment (including replacements) must be preserved.
- **Unresolved Questions**:
  - Does an assignment immediately guarantee the slot, or is it tentative pending `Confirmation`?
  - What is the exact workflow and authorization for replacing an assigned Leader?

### Confirmation
- **Responsibility**: Explicit final commitment by a selected Leader that they will attend the Event.
- **Relationships**:
  - Pertains to a `TeamAssignment` on an `Event`.
- **Known Invariants**:
  - Can only occur after a Leader has been selected.
- **Unresolved Questions**:
  - Is confirmation mandatory, or does assignment assume attendance unless rejected?
  - What happens if a Leader fails to confirm before the deadline? Does the system auto-cancel or alert the Chief Leader?

### Attendance
- **Responsibility**: Ground-truth operational record of whether an assigned Leader was physically present and served at the Event.
- **Relationships**:
  - Recorded for a `LeaderProfile` on an `Event`.
  - Logged by the `Chief Leader` (or `Admin`).
- **Known Invariants**:
  - Attendance can only be recorded during or after the Event execution window.
  - Possible states include `Attended`, `Late`, `Excused Absence`, `No-Show`.
- **Unresolved Questions**:
  - Who can modify attendance records post-event?
  - Can a Leader dispute a `No-Show` mark?

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
