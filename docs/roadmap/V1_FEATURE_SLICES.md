# Carpediem V1 Vertical Feature Slices

> **Implementation update (2026-10-22):** The decision gates described below for SLICE-13 and SLICE-20–22 were resolved in [CHANGE-003](../decisions/CHANGE-003-v1-remaining-slice-policy.md). See [V1_IMPLEMENTATION_STATUS.md](V1_IMPLEMENTATION_STATUS.md) for delivered behavior.

> [!IMPORTANT]
> **Governance Status**: Authoritative Feature Slicing Specification  
> **Precedence**: Aligned strictly with [ARCHITECTURE_LOCK.md](file:///c:/dev/web/Carpediem/docs/architecture/ARCHITECTURE_LOCK.md), [LIFECYCLE_SPECIFICATION.md](file:///c:/dev/web/Carpediem/docs/requirements/LIFECYCLE_SPECIFICATION.md), [CHANGE-001](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md), and [CHANGE-002](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-002-draft-event-minimum-fields.md).  
> **Scope**: Master inventory of **FOUNDATION-01** (Technical Bootstrap) plus exactly **21 Business Feature Slices**.

---

## 1. Technical Bootstrap vs. Business Feature Slices

Carpediem strictly distinguishes between **technical scaffolding** and **business vertical slices**:

### FOUNDATION-01: First Vertical Slice Technical Bootstrap
- **Classification**: Platform Engineering Infrastructure (NOT a business feature slice; not run through product `/specify`).
- **Purpose**: Accompanies `SLICE-02` during initial implementation to establish the executing environment.
- **Included Scaffolding**:
  - ASP.NET Core 10 Web API host and Modular Monolith directory/assembly structure.
  - Angular 22 SPA application shell, navigation frame, and theme layout.
  - Microsoft SQL Server 2025 connectivity with RCSI enabled (`READ_COMMITTED_SNAPSHOT ON`).
  - Single-host `CarpediemDbContext` and EF Core migrations baseline.
  - ASP.NET Core Identity cookie authentication (`AddIdentityCookies`, `HttpOnly = true`, `SameSite = Lax`, `Secure = true`), sliding expiration (14 days), `SecurityStampValidator` revocation, and Antiforgery middleware per ADR-002.
  - Standardized `ProblemDetails` exception handling, structured logging, and health endpoint baselines.
  - Integration and End-to-End (E2E) testing harness.
  - Deterministic seed/test user fixtures (1 Operations Administrator, 2 Chief Leaders, 5 Leaders).

---

## 2. Master Business Feature Slice Inventory (21 Slices)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                 CARPEDIEM V1 BUSINESS FEATURE SLICES (21 SLICES)            │
│                                                                             │
│  [Core Event & Chief Leader Lifecycle]                                      │
│  ├── SLICE-02: Draft Event Creation & Draft Editing                         │
│  ├── SLICE-03: Chief Leader Appointment & Notification                      │
│  ├── SLICE-04: Chief Leader In-Platform Response (Accept / Decline)         │
│  ├── SLICE-05: Chief Leader Admin-Recorded External Acceptance              │
│  └── SLICE-06: Event Publication & Publishing Prerequisite Invariant        │
│                                                                             │
│  [Volunteer Availability & Team Staffing]                                   │
│  ├── SLICE-07: Leader Availability Declaration (Available / NotAvailable)    │
│  ├── SLICE-08: Chief Leader Team Selection from Available Pool              │
│  └── SLICE-09: Direct Leader Assignment                                     │
│                                                                             │
│  [Volunteer Cancellation & Replacement]                                     │
│  ├── SLICE-10: Leader Self-Cancellation with Mandatory Justification        │
│  └── SLICE-11: Replacement Leader Assignment                                │
│                                                                             │
│  [Schedule Disruption & Event Completion]                                   │
│  ├── SLICE-12: Event Reschedule & Staffing Cycle Rotation                   │
│  ├── SLICE-13: Event Cancellation                                           │
│  └── SLICE-14: Event Scheduled Completion & Administrative Closeout         │
│                                                                             │
│  [Attendance Tracking & Correction]                                         │
│  ├── SLICE-15: Chief Leader On-Site Attendance Recording                    │
│  └── SLICE-16: Admin Attendance Correction & Audit Logging                  │
│                                                                             │
│  [Notifications, Profiles & Dashboards]                                     │
│  ├── SLICE-17: In-Platform Notification Delivery & Inbox Management         │
│  ├── SLICE-18: Leader Profile & Factual Participation History                │
│  └── SLICE-19: Admin & Chief Leader Operational Dashboard                   │
│                                                                             │
│  [Stakeholder-Gated V1 Capabilities]                                        │
│  ├── SLICE-20: Authorized Account & Role Administration                     │
│  ├── SLICE-21: Post-Event Feedback & Evaluations                            │
│  └── SLICE-22: Incident Logging & Management                                │
└─────────────────────────────────────────────────────────────────────────────┘
```

Total Business Feature Slices: **21**.

---

## 3. Detailed Business Slice Specifications

### SLICE-02: Draft Event Creation & Draft Editing
- **Owning Module**: `Events`
- **Primary Persona**: Platform Administrator
- **Business Scope**:
  - Admin creates a new Event in `Draft` state by supplying the ratified minimum fields per CHANGE-002: `Title`, `StartDateTime`, `EndDateTime`, `Location`, and optional `Notes`.
  - Admin edits draft fields while the Event remains in `Draft` status.
  - Server-side validation enforces `StartDateTime < EndDateTime` and non-empty required strings.
  - Admin views validation, error, and success states.
- **Explicit Exclusions**: Chief Leader appointment, event publication, event cancellation, rescheduling, notifications, volunteer staffing, attendance.
- **Persisted Entity States**: `events.Events` (`Status = 'Draft'`), `events.StaffingCycles` (initial cycle, `IsCurrent = 1`).
- **Audit & Notification**: Transactional audit record written (`EventCreated`, `DraftEventUpdated`). Zero notifications dispatched.

### SLICE-03: Chief Leader Appointment & Notification
- **Owning Module**: `Staffing` (coordinating with `Events`)
- **Primary Persona**: Platform Administrator
- **Business Scope**: Admin appoints a candidate from the predefined Chief Leader pool to a `Draft` Event. Creates a `Pending` `ChiefLeaderAssignment` and records notification intent. Incorporates CHANGE-001 cross-cutting check.
- **Persisted Entity States**: `events.ChiefLeaderAssignments` (`Status = 'Pending'`).
- **Audit & Notification**: Audit logged (`ChiefLeaderAppointed`). Notification intent created for appointed Chief Leader.

### SLICE-04: Chief Leader In-Platform Response (Accept / Decline)
- **Owning Module**: `Staffing`
- **Primary Persona**: Chief Leader
- **Business Scope**: Appointed Chief Leader views pending appointment and explicitly **Accepts** (transitioning assignment to `Active`, granting operational ownership) or **Declines** (transitioning assignment to `Declined`, preserved immutably as historical evidence; alert generated for Admin).
- **Persisted Entity States**: `events.ChiefLeaderAssignments` (`Status = 'Active'` or `'Declined'`).
- **Audit & Notification**: Audit logged with actor attribution (`Direct`). Filtered index `UX_ChiefLeaderAssignments_ActivePerEvent` enforces max 1 Active assignment.

### SLICE-05: Chief Leader Admin-Recorded External Acceptance
- **Owning Module**: `Staffing` (coordinating with `Events`)
- **Primary Persona**: Platform Administrator
- **Business Scope**: When a Chief Leader confirms verbally or offline, an Admin records external acceptance on their behalf. Requires a mandatory audit justification note. Assignment transitions to `Active`.
- **Persisted Entity States**: `events.ChiefLeaderAssignments` (`Status = 'Active'`).
- **Audit & Notification**: Audit log explicitly records `AuditSource = 'AdminExternal'` and mandatory justification note.

### SLICE-06: Event Publication & Publishing Prerequisite Invariant
- **Owning Module**: `Events`
- **Primary Persona**: Platform Administrator
- **Business Scope**: Admin publishes a `Draft` Event. Server strictly verifies the publishing prerequisite invariant: Event must have exactly one accepted `Active` `ChiefLeaderAssignment` and valid schedule window (`StartTime < EndTime`). Event becomes visible to Leaders for availability declaration.
- **Persisted Entity States**: `events.Events` (`Status = 'Published'`).
- **Audit & Notification**: Audit logged (`EventPublished`). Notification intent created to inform Leaders of publication.

### SLICE-07: Leader Availability Declaration (Available / NotAvailable)
- **Owning Module**: `Staffing`
- **Primary Persona**: Volunteer Leader, Chief Leader (acting as Leader)
- **Business Scope**: Leader views a `Published` Event and submits availability: `Available` or `NotAvailable`. Leader maintains self-service mutability to change declaration prior to event execution. Absence of declaration represents `No AvailabilityResponse` ("Response Needed" in UI).
- **Persisted Entity States**: `staffing.AvailabilityResponses` (`Response = 'Available'` or `'NotAvailable'`).
- **Audit & Notification**: Availability logged per cycle. No intermediate application entities exist.

### SLICE-08: Chief Leader Team Selection from Available Pool
- **Owning Module**: `Staffing`
- **Primary Persona**: Chief Leader
- **Business Scope**: Chief Leader views the pool of `Available` Leaders and selects a volunteer. Selection **immediately** creates an `Active` `TeamAssignment` (ratified per Decision Group 5 in `PRODUCT_DISCOVERY.md` Item 8 and `LIFECYCLE_SPECIFICATION.md` Section 5 & 7). The volunteer is placed directly on the active team roster without redundant confirmation steps. Enforces CHANGE-001 same-event exclusivity invariant.
- **Persisted Entity States**: `staffing.TeamAssignments` (`Status = 'Active'`).
- **Audit & Notification**: Audit logged (`VolunteerSelected`). Notification intent created to inform volunteer of active placement. Filtered index `UX_TeamAssignments_ActiveLeaderPerCycle` prevents duplicate active assignments.

### SLICE-09: Direct Leader Assignment
- **Owning Module**: `Staffing`
- **Primary Persona**: Chief Leader, Platform Administrator
- **Business Scope**: Chief Leader or Admin directly assigns a Leader who has not responded or who marked `NotAvailable`. Chief Leader assignment is a standard direct staffing action; Admin assignment is an audited administrative action. Both create an immediate `Active` `TeamAssignment` (ratified per Decision Group 5 and Blocker 1 in `LIFECYCLE_SPECIFICATION.md` Section 5 & 7) and notify the Leader. Enforces CHANGE-001 same-event exclusivity invariant.
- **Persisted Entity States**: `staffing.TeamAssignments` (`Status = 'Active'`).
- **Audit & Notification**: Audit logged (`DirectLeaderAssignmentExecuted`, noting non-response/NotAvailable state). Notification intent created for volunteer.

### SLICE-10: Leader Self-Cancellation with Mandatory Justification
- **Owning Module**: `Staffing`
- **Primary Persona**: Volunteer Leader
- **Business Scope**: An actively staffed Leader self-cancels their assignment via a controlled UI modal requiring explicit confirmation and a mandatory free-text justification reason. Assignment immediately transitions to `Cancelled`. Alerts Chief Leader and Admin.
- **Persisted Entity States**: `staffing.TeamAssignments` (`Status = 'Cancelled'`, `CancellationReason`, `CancelledAt`).
- **Audit & Notification**: Audit logged (`VolunteerSelfCancelled`, recording justification). Notification intent created for Chief Leader and Admin.

### SLICE-11: Replacement Leader Assignment
- **Owning Module**: `Staffing`
- **Primary Persona**: Chief Leader
- **Business Scope**: Following a cancellation, Chief Leader selects a replacement Leader from the pool. Creates a **brand new** `TeamAssignment` record with `Status = 'Active'`. The original `Cancelled` assignment remains preserved immutably for historical tracking.
- **Persisted Entity States**: New `staffing.TeamAssignments` (`Status = 'Active'`).
- **Audit & Notification**: Audit logged (`ReplacementSelected`). Notification intent created for replacement volunteer.

### SLICE-12: Event Reschedule & Staffing Cycle Rotation
- **Owning Module**: `Events` (coordinating with `Staffing`)
- **Primary Persona**: Platform Administrator
- **Business Scope**: Admin executes a material date change on a `Published` Event. Event status remains `Published`. The current `StaffingCycle` is sealed and archived immutably (`IsCurrent = 0`). A new `StaffingCycle` is created (`IsCurrent = 1`). Prior availability responses are invalidated for the new date. Prior active roster assignments are archived under the old cycle. Notifications alert participants.
- **Persisted Entity States**: Prior `events.StaffingCycles` updated to `IsCurrent = 0`; new `StaffingCycles` created with `IsCurrent = 1`.
- **Audit & Notification**: Audit logged (`EventRescheduled`). High-priority notification intent created for prior roster and candidates.

### SLICE-13: Event Cancellation *(V1 — Needs Stakeholder Decision)*
- **Owning Module**: `Events` (coordinating with `Staffing`)
- **Primary Persona**: Platform Administrator
- **Business Scope**: Admin cancels an Event with mandatory administrative justification. Event transitions to terminal state `Cancelled`. Exact consequences for current team assignments, notification cascades, and operational cancellation UX require stakeholder decision before specification.
- **Persisted Entity States**: `events.Events` (`Status = 'Cancelled'`).
- **Audit & Notification**: Audit logged (`EventCancelled`, recording justification).

### SLICE-14: Event Scheduled Completion & Administrative Closeout
- **Owning Module**: `Events`
- **Primary Persona**: System / Platform Administrator
- **Business Scope**: An Event in `Published` status transitions to terminal state `Completed` when the scheduled window concludes (`Now > EndTime`) via a lightweight native ASP.NET Core `BackgroundService` per ADR-007, or via explicit Admin manual closeout. Decoupled from attendance submission.
- **Persisted Entity States**: `events.Events` (`Status = 'Completed'`).
- **Audit & Notification**: Audit logged (`EventCompleted`).

### SLICE-15: Chief Leader On-Site Attendance Recording
- **Owning Module**: `Attendance`
- **Primary Persona**: Chief Leader
- **Business Scope**: Chief Leader takes roll call during or post-event, marking each actively staffed volunteer as `Attended` or `NoShow`. Absence of record represents `Unrecorded`. Submission takes immediate effect without an Admin approval gate. Independent of Event completion state.
- **Persisted Entity States**: `attendance.AttendanceOutcomes` (`Outcome = 'Attended'` or `'NoShow'`).
- **Audit & Notification**: Audit logged (`AttendanceFinalized`). Raw counts updated on volunteer participation profiles.

### SLICE-16: Admin Attendance Correction & Audit Logging
- **Owning Module**: `Attendance`
- **Primary Persona**: Platform Administrator
- **Business Scope**: Admin corrects an attendance outcome post-finalization. Requires a mandatory correction justification reason. Audit trail immutably records `OldOutcome`, `NewOutcome`, acting Admin, timestamp, and justification.
- **Persisted Entity States**: `attendance.AttendanceOutcomes` (`Outcome` updated).
- **Audit & Notification**: Immutable audit log written (`AttendanceCorrected`).

### SLICE-17: In-Platform Notification Delivery & Inbox Management
- **Owning Module**: `Notifications`
- **Primary Persona**: Platform Administrator, Chief Leader, Leader
- **Business Scope**: Users view their in-platform notification inbox, see unread counters, and mark notifications as read (`ReadAt` timestamp updated). The Angular client updates notifications via page-load navigation, route changes, window focus, and periodic background polling per ADR-006.
- **Persisted Entity States**: `notifications.InPlatformNotifications` (`ReadAt` updated).
- **Architectural Mechanics**: Relational persistence; client lifecycle refresh & polling. Zero SignalR WebSocket dependencies in V1.

### SLICE-18: Leader Profile & Factual Participation History
- **Owning Module**: `Identity & Access`
- **Primary Persona**: Volunteer Leader, Chief Leader, Platform Administrator
- **Business Scope**: Volunteer Leaders view their own profile and factual participation metrics (`Events Attended`, `No-Shows`, `Self-Cancellations`). Authorized Chief Leaders and Admins view volunteer operational history during selection. Factual indicators derived strictly from `TeamAssignments`, `AttendanceOutcomes`, and `Events`. Zero synthetic reliability scores.
- **Readiness**: Factual history derived from core data; extended personal attributes require stakeholder decision.

### SLICE-19: Admin & Chief Leader Operational Dashboard
- **Owning Module**: Cross-Cutting / Web App
- **Primary Persona**: Platform Administrator, Chief Leader
- **Business Scope**: Real-time operational overview via derived/request-time operational evaluation of current platform state:
  - Event status lists (`Draft`, `Published`, `Completed`, `Cancelled`).
  - Derived operational condition `In Progress` (`Published` and `StartTime <= Now <= EndTime`).
  - Derived operational condition `Pending Attendance` (`Completed` event with unrecorded attendance).
  - Staffing fill indicators and unassigned counts.
- **Architectural Mechanics**: Evaluated dynamically on request in SQL/LINQ projections per ADR-007; refreshed on navigation and polling per ADR-006.

### SLICE-20: Authorized Account & Role Administration *(V1 — Needs Stakeholder Decision)*
- **Owning Module**: `Identity & Access`
- **Primary Persona**: Platform Administrator
- **Business Scope**: Administrative provisioning of user accounts, role grants (`Admin`, `Chief Leader`, `Leader`), and account activation/deactivation lifecycles. Public onboarding and self-registration remain unresolved.

### SLICE-21: Post-Event Feedback & Evaluations *(V1 — Needs Stakeholder Decision)*
- **Owning Module**: `Feedback`
- **Primary Persona**: Volunteer Leader, Chief Leader
- **Business Scope**: Collects volunteer experience feedback and leadership evaluations post-event. In-scope for V1, but detailed business rules (question rubric, rating scales, anonymity, visibility policies) require formal stakeholder decisions before feature specification.

### SLICE-22: Incident Logging & Management *(V1 — Needs Stakeholder Decision)*
- **Owning Module**: `Incidents`
- **Primary Persona**: Chief Leader, Platform Administrator
- **Business Scope**: Logs operational on-site incidents (safety, venue, injuries) and provides audit tracking for leadership review. In-scope for V1, but detailed business rules (incident schema, severity taxonomy, escalation workflows) require formal stakeholder decisions before feature specification.
