# Carpediem V1 Delivery Sequence & Milestone Strategy

> [!IMPORTANT]
> **Governance Status**: Authoritative Delivery & Milestone Strategy  
> **Precedence**: Aligned strictly with [ARCHITECTURE_LOCK.md](file:///c:/dev/web/Carpediem/docs/architecture/ARCHITECTURE_LOCK.md), [LIFECYCLE_SPECIFICATION.md](file:///c:/dev/web/Carpediem/docs/requirements/LIFECYCLE_SPECIFICATION.md), [CHANGE-001](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md), and [CHANGE-002](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-002-draft-event-minimum-fields.md).  
> **Rule**: Technical implementation foundations accompany vertical slices; no artificial technical epics or unapproved lifecycle states exist.

---

## 1. Ten Foundational Delivery Principles

1. **Vertical Slice Discipline**: Every deliverable produces a functional vertical slice from Angular UI down to SQL Server persistence and append-oriented audit logging.
2. **Zero Invented Lifecycles**: Do not create recruitment window deadlines, automated window closures, or team confirmation gates. Follow the ratified lifecycle: Published Event $\rightarrow$ Leader declares Available $\rightarrow$ Chief Leader selects $\rightarrow$ Active TeamAssignment.
3. **Persisted State Strictness**: Adhere strictly to ratified persisted statuses:
   - Event: `Draft`, `Published`, `Completed`, `Cancelled`.
   - ChiefLeaderAssignment: `Pending`, `Active`, `Declined`.
   - AvailabilityResponse: `Available`, `NotAvailable`.
   - TeamAssignment: `Active`, `Cancelled`.
   - AttendanceOutcome: `Attended`, `NoShow` (Absence of row = `Unrecorded`).
4. **Immediate Team Placement**: Chief Leader selection immediately produces an `Active` `TeamAssignment`. Redundant "invited" or "confirmation" steps are forbidden.
5. **Direct Leader Assignment Authority**: Support immediate `Active` assignment for Leaders who did not respond or marked `NotAvailable` without treating Chief Leader assignment as an override.
6. **Native Asynchronous Lifecycle Execution**: Event completion post-`EndTime` is executed by a lightweight native ASP.NET Core `BackgroundService` per ADR-007. Zero external schedulers (no Hangfire, Quartz).
7. **Relational Notification Strategy**: Notifications are persisted in SQL Server and retrieved via Angular lifecycle refreshes and polling per ADR-006. Zero SignalR dependencies in V1.
8. **Decoupled Identity Bootstrap**: Early feature slices utilize deterministic seed fixtures in `FOUNDATION-01` (1 Admin, 2 Chief Leaders, 5 Leaders) to bypass unresolved production self-registration.
9. **Factual Integrity**: Profile history and attendance records track raw counts (`Attended`, `NoShow`, `Cancelled`). No synthetic scoring, algorithms, or automated penalties.
10. **Stakeholder Honesty**: Features with unresolved business definitions (`Feedback`, `Incidents`, `Account Administration`, `Event Cancellation` consequences) are explicitly gated by stakeholder decisions before entering Spec Kit `/specify`.

---

## 2. Technical Bootstrap Accompanying First Slice (`FOUNDATION-01`)

The first business implementation slice (`SLICE-02`) carries the foundational application scaffolding:
- **Host Infrastructure**: ASP.NET Core 10 Web API host with Modular Monolith folder/assembly boundaries.
- **Database Baseline**: Microsoft SQL Server 2025 connection, RCSI enabled (`READ_COMMITTED_SNAPSHOT ON`), and EF Core single-host `CarpediemDbContext`.
- **Security & Session**: HTTP-only session cookie authentication handler, sliding expiration, and Antiforgery validation.
- **Client Foundation**: Angular 20 SPA application shell, navigation frame, and authentication session state interceptor.
- **Developer Fixtures**: Deterministic development accounts (1 Operations Admin, 2 Chief Leaders, 5 Leaders).

---

## 3. Prioritized Delivery Ordering (P0 / P1 / P2)

### P0: First Usable Volunteer Staffing Workflow (Milestone M1)
*Objective: Prove end-to-end event creation, Chief Leader appointment, event publication, volunteer availability, and team staffing.*

1. **`FOUNDATION-01`**: First Slice Technical Bootstrap *(Platform scaffolding)*
2. **`SLICE-02`**: Draft Event Creation & Draft Editing *(Event aggregate root; unblocked by CHANGE-002)*
3. **`SLICE-03`**: Chief Leader Appointment & Notification *(Appointing leadership candidate)*
4. **`SLICE-04`**: Chief Leader In-Platform Response *(Direct accept/decline workflow)*
5. **`SLICE-05`**: Chief Leader Admin-Recorded External Acceptance *(Administrative fallback)*
6. **`SLICE-06`**: Event Publication & Publishing Prerequisite Invariant *(Publishing gate)*
7. **`SLICE-07`**: Leader Availability Declaration *(Volunteer schedule submission)*
8. **`SLICE-08`**: Chief Leader Team Selection from Available Pool *(Immediate active roster placement)*
9. **`SLICE-09`**: Direct Leader Assignment *(Direct assignment of non-respondents)*

### P1: Operational Execution, Attendance & Churn (Milestones M2 & M3)
*Objective: Deliver attendance roll call, operational dashboards, churn management, and in-platform notifications.*

**Milestone M2: Completion, Attendance & Operational Visibility**
10. **`SLICE-14`**: Event Scheduled Completion & Closeout *(Native BackgroundService / Admin closeout)*
11. **`SLICE-15`**: Chief Leader On-Site Attendance Recording *(Roll call finalization; independent of completion)*
12. **`SLICE-16`**: Admin Attendance Correction & Audit Logging *(Audited corrections)*
13. **`SLICE-18`**: Leader Profile & Factual Participation History *(Factual participation counts)*
14. **`SLICE-19`**: Admin & Chief Leader Operational Dashboard *(Derived/request-time operational evaluation)*

**Milestone M3: Volunteer Churn, Rescheduling & Notification Center**
15. **`SLICE-10`**: Leader Self-Cancellation with Mandatory Justification *(Controlled self-cancellation)*
16. **`SLICE-11`**: Replacement Leader Assignment *(Selecting replacement for cancelled slot)*
17. **`SLICE-12`**: Event Reschedule & Staffing Cycle Rotation *(Material date change governance)*
18. **`SLICE-17`**: In-Platform Notification Delivery & Inbox Management *(Inbox & polling refresh)*

### P2: Stakeholder-Gated V1 Capabilities (Milestone M4)
*Objective: Implement V1 capabilities following resolution of stakeholder decisions.*

19. **`SLICE-13`**: Event Cancellation *(Gated by decision on assignment/notification effects)*
20. **`SLICE-20`**: Authorized Account & Role Administration *(Gated by production onboarding decision)*
21. **`SLICE-21`**: Post-Event Feedback & Evaluations *(Gated by survey rubric decision)*
22. **`SLICE-22`**: Incident Logging & Management *(Gated by incident taxonomy decision)*

---

## 4. First Usable Product Milestone (Milestone M1)

**Milestone M1: "Volunteer Recruitment & Team Placement Baseline"**

- **Included Deliverables**:
  $$\text{M1} = \text{FOUNDATION-01} + \text{SLICE-02} + \text{SLICE-03} + \text{SLICE-04} + \text{SLICE-06} + \text{SLICE-07} + \text{SLICE-08} + \text{SLICE-09}$$
- **End-to-End Workflow Proven**:
  1. Operations Admin authenticates using seed credentials (`FOUNDATION-01`).
  2. Admin creates a Draft Event with Title, Start/End times, Location, and optional Notes per CHANGE-002 (`SLICE-02`).
  3. Admin appoints a candidate from the predefined Chief Leader pool (`SLICE-03`).
  4. Appointed Chief Leader logs in and accepts the assignment directly (`SLICE-04`).
  5. Admin publishes the Event; server verifies the accepted Chief Leader prerequisite (`SLICE-06`).
  6. Volunteer Leaders view the published Event and submit `Available` or `NotAvailable` (`SLICE-07`).
  7. Chief Leader views the available pool and selects team members (`SLICE-08`).
  8. Chief Leader or Admin directly assigns any required non-respondents (`SLICE-09`).
  9. Selected Leaders are **immediately placed on the active team roster** in `Active` status without redundant confirmation gates.

---

## 5. First Feature Handoff Profile

### `SLICE-02: Draft Event Creation & Draft Editing`
- **Readiness Classification**: **`READY FOR /SPECIFY`** (Zero blocking decisions).
- **Actor**: Platform Administrator.
- **Business Objective**: Allow an Admin to establish a valid `Draft` Event record that later Chief Leader appointment and publication workflows can build on, and edit draft details prior to publication.
- **Included Fields (Ratified per [CHANGE-002](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-002-draft-event-minimum-fields.md))**:
  - `Title` (Required string)
  - `StartDateTime` (Required UTC datetime)
  - `EndDateTime` (Required UTC datetime)
  - `Location` (Required string venue/location text)
  - `Notes` (Optional string)
- **Known Required Invariants**:
  - `StartDateTime < EndDateTime`.
  - Required text fields must not be empty or whitespace.
  - Event status is strictly initialized to `Draft`.
  - Initial `StaffingCycle` is created with `IsCurrent = 1`.
- **Explicit Exclusions (Handled by Later Slices)**:
  - Chief Leader appointment (`SLICE-03`).
  - Event publication (`SLICE-06`).
  - Event cancellation (`SLICE-13`).
  - Event rescheduling (`SLICE-12`).
  - Volunteer availability and staffing (`SLICE-07`, `SLICE-08`).
  - Attendance (`SLICE-15`).
  - Notifications dispatch (draft events are invisible to Leaders).
