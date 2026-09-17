# Carpediem V1 Epic Map

> [!IMPORTANT]
> **Governance Status**: Authoritative Product Roadmap Specification  
> **Precedence**: Aligned strictly with [ARCHITECTURE_LOCK.md](file:///c:/dev/web/Carpediem/docs/architecture/ARCHITECTURE_LOCK.md), [LIFECYCLE_SPECIFICATION.md](file:///c:/dev/web/Carpediem/docs/requirements/LIFECYCLE_SPECIFICATION.md), [CHANGE-001](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md), and [CHANGE-002](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-002-draft-event-minimum-fields.md).  
> **Scope**: Decomposes approved Carpediem V1 capabilities into business-oriented epics mapped directly to modular monolith module boundaries.

---

## 1. Executive Summary

This document establishes the **V1 Epic Map** for Carpediem. The epic architecture is structured strictly around **approved business capabilities** rather than technical implementation details or candidate workflows.

Key governance alignments:
1. **Zero Invented Recruitment Windows**: Volunteer availability declaration begins upon Event publication and continues until Chief Leader selection. No artificial recruitment open/close states, deadlines, or automated window closure background workers exist.
2. **Zero Redundant Team Confirmation**: Chief Leader selection immediately creates an `Active` `TeamAssignment`. No intermediate "invited", "pending confirmation", or "roster confirmation" state machines exist.
3. **Ratified Persisted States Only**:
   - **Event**: `Draft`, `Published`, `Completed`, `Cancelled`.
   - **ChiefLeaderAssignment**: `Pending`, `Active`, `Declined` (Initial: Absence of record).
   - **AvailabilityResponse**: `Available`, `NotAvailable` (Initial: Absence of record).
   - **TeamAssignment**: `Active`, `Cancelled` (Initial: Absence of record).
   - **AttendanceOutcome**: `Attended`, `NoShow` (Initial: Absence of record / `Unrecorded`).
4. **CHANGE-001 Dual-Role Exclusivity**: Cross-cutting acceptance rule ensuring that on the same event, holding an `Active` `ChiefLeaderAssignment` and an `Active` `TeamAssignment` is strictly mutually exclusive.
5. **CHANGE-002 Minimum Event Fields**: Ratifies the mandatory Draft Event creation fields (`Title`, `StartDateTime`, `EndDateTime`, `Location`, optional `Notes`).
6. **Canonical V1 Scope**: Post-Event Feedback/Evaluations and Incident Logging are **in-scope for V1**, but classified as requiring stakeholder decisions before detailed feature specification.

---

## 2. Master Epic Catalog

Carpediem V1 comprises **13 Business Epics** mapped to modular monolith boundaries:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             CARPEDIEM V1 EPICS                              │
│                                                                             │
│  [Identity & Access]                                                        │
│  ├── EPIC-01: Platform Access & Authorized Account Administration           │
│  └── EPIC-10: Leader Profile & Factual Participation History                │
│                                                                             │
│  [Events Module]                                                            │
│  ├── EPIC-02: Event Planning & Lifecycle Management                         │
│  └── EPIC-07: Event Rescheduling & Staffing Cycle Governance                │
│                                                                             │
│  [Staffing Module]                                                          │
│  ├── EPIC-03: Chief Leader Appointment & Acceptance                         │
│  ├── EPIC-04: Leader Availability Declaration                               │
│  ├── EPIC-05: Team Staffing & Direct Leader Assignment                      │
│  └── EPIC-06: Volunteer Cancellation & Replacement                          │
│                                                                             │
│  [Attendance Module]                                                        │
│  └── EPIC-08: On-Site Attendance & Administrative Correction                │
│                                                                             │
│  [Notifications Module]                                                     │
│  └── EPIC-09: In-Platform Notifications & Delivery                          │
│                                                                             │
│  [Operational Oversight]                                                    │
│  └── EPIC-11: Operational Dashboards & Condition Monitoring                 │
│                                                                             │
│  [Stakeholder-Gated V1 Capabilities]                                        │
│  ├── EPIC-12: Post-Event Feedback & Evaluations                             │
│  └── EPIC-13: Incident Logging & Management                                 │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Epic Specifications

### EPIC-01: Platform Access & Authorized Account Administration
- **Owning Module**: `Identity & Access`
- **Primary Persona**: Platform Administrator, Chief Leader, Leader
- **Business Capability**: Enables secure user access to the Carpediem web application via secure, HTTP-only session cookies with sliding expiration. Governs Admin management of authorized accounts and roles. Early delivery utilizes deterministic seed fixtures (`FOUNDATION-01`) to bootstrap development without blocking on unresolved production onboarding workflows.
- **Architectural Reference**: ADR-002 (Session Auth, sliding expiration, Antiforgery protection).

### EPIC-02: Event Planning & Lifecycle Management
- **Owning Module**: `Events`
- **Primary Persona**: Platform Administrator
- **Business Capability**: Enables Admins to author new Events in `Draft` state supplying ratified minimum fields per CHANGE-002, edit draft details, publish Events (enforcing the active Chief Leader invariant), transition Events to `Completed` post-schedule, or cancel Events with mandatory justification.
- **Persisted States**: `Draft`, `Published`, `Completed`, `Cancelled`.
- **Architectural Reference**: `events.Events`, `events.StaffingCycles` (initial cycle).

### EPIC-03: Chief Leader Appointment & Acceptance
- **Owning Module**: `Staffing` (coordinating with `Events`)
- **Primary Persona**: Platform Administrator, Chief Leader
- **Business Capability**: Governs the appointment of a Chief Leader from a predefined organization pool to an Event. The appointed Chief Leader accepts or declines directly in-platform. If confirmed verbally or via external channels, an Admin may record external acceptance with mandatory justification and explicit audit logging. Enforces the strict publishing prerequisite: an Event cannot transition to `Published` without an `Active` Chief Leader. Incorporates CHANGE-001 cross-cutting rules.
- **Persisted States**: `Pending`, `Active`, `Declined`.

### EPIC-04: Leader Availability Declaration
- **Owning Module**: `Staffing`
- **Primary Persona**: Volunteer Leader, Chief Leader (acting as Leader)
- **Business Capability**: Enables volunteer Leaders to view `Published` Events and submit their schedule availability as `Available` or `NotAvailable`. Leaders maintain self-service mutability to update their declaration while the Event remains open prior to event execution. Absence of declaration represents `No AvailabilityResponse` ("Response Needed" in UI).
- **Persisted States**: `Available`, `NotAvailable` (Absence of row = No response).

### EPIC-05: Team Staffing & Direct Leader Assignment
- **Owning Module**: `Staffing`
- **Primary Persona**: Chief Leader, Platform Administrator
- **Business Capability**: Grants the assigned Chief Leader primary authority to select team members from the `Available` volunteer pool. Selection immediately creates an `Active` `TeamAssignment` and notifies the Leader. Also grants Chief Leaders and Admins the authority to directly assign any active Leader who has not responded or declared `NotAvailable` (Direct Leader Assignment). Zero redundant confirmation steps exist.
- **Persisted States**: `Active` (Absence of row = Unassigned).

### EPIC-06: Volunteer Cancellation & Replacement
- **Owning Module**: `Staffing`
- **Primary Persona**: Volunteer Leader, Chief Leader, Platform Administrator
- **Business Capability**: Enables an actively staffed Leader to self-cancel their assignment via a controlled UI modal requiring explicit confirmation and a mandatory justification reason. Self-cancellation immediately transitions the assignment to `Cancelled` and notifies leadership. Chief Leaders can then select an immediate replacement, creating a brand new `Active` assignment record while preserving the historical cancelled record.
- **Persisted States**: `Cancelled`.

### EPIC-07: Event Rescheduling & Staffing Cycle Governance
- **Owning Module**: `Events` (coordinating with `Staffing`)
- **Primary Persona**: Platform Administrator
- **Business Capability**: Manages material date changes for scheduled Events. When an Event date changes, the Event remains `Published`, the active `StaffingCycle` is sealed and archived immutably (`IsCurrent = 0`), prior availability responses are invalidated, and a new `StaffingCycle` (`IsCurrent = 1`) is opened, requiring fresh availability declarations and team selection for the new date.
- **Architectural Reference**: `events.StaffingCycles`, `UX_StaffingCycles_CurrentPerEvent`.

### EPIC-08: On-Site Attendance & Administrative Correction
- **Owning Module**: `Attendance`
- **Primary Persona**: Chief Leader, Platform Administrator
- **Business Capability**: Enables the assigned Chief Leader to capture and finalize on-the-ground attendance outcomes for actively staffed Leaders post-event (`Attended` or `NoShow`). Absence of record represents `Unrecorded`. Grants Admins post-finalization override authority to correct attendance outcomes with mandatory justification and immutable audit trail logging. Feeds factual attendance counts into Leader profiles without synthetic scoring.
- **Persisted States**: `Attended`, `NoShow` (Absence of row = `Unrecorded`).

### EPIC-09: In-Platform Notifications & Delivery
- **Owning Module**: `Notifications`
- **Primary Persona**: Platform Administrator, Chief Leader, Leader
- **Business Capability**: Provides a centralized in-platform notification inbox. Business workflows create notification intents transactionally according to the accepted notification architecture. The client consumes notifications via navigation lifecycle refreshes and low-overhead background polling per ADR-006. Users view notifications, observe unread counters, and mark items as read.
- **Architectural Reference**: ADR-006 (Relational persistence, HTTP polling/lifecycle refresh; no SignalR, no external brokers).

### EPIC-10: Leader Profile & Factual Participation History
- **Owning Module**: `Identity & Access`
- **Primary Persona**: Volunteer Leader, Chief Leader, Platform Administrator
- **Business Capability**: Presents volunteer profile information and raw, factual participation metrics (`Events Attended`, `No-Shows`, `Self-Cancellations`) derived from `TeamAssignments`, `AttendanceOutcomes`, and `Events`. Strict adherence to factual reporting: zero synthetic reliability scores, weighting formulas, or automated penalties.

### EPIC-11: Operational Dashboards & Condition Monitoring
- **Owning Module**: Cross-Cutting / Web App
- **Primary Persona**: Platform Administrator, Chief Leader
- **Business Capability**: Provides operational visibility into event execution via derived/request-time operational evaluation of non-persisted conditions:
  - **`In Progress`**: Dynamically evaluated when `Status == Published` and `StartTime <= Now <= EndTime`.
  - **`Pending Attendance`**: Dynamically evaluated when `Status == Completed` and any active team assignment remains with unrecorded attendance.
  - Staffing counts and volunteer response indicators.

### EPIC-12: Post-Event Feedback & Evaluations *(V1 — Needs Stakeholder Decision)*
- **Owning Module**: `Feedback` *(Deferred Specification)*
- **Primary Persona**: Volunteer Leader, Chief Leader, Platform Administrator
- **Business Capability**: Captures post-event volunteer feedback and leadership evaluations. Remains an approved V1 capability, but detailed business rules (question rubric, rating scales, anonymity, visibility policies) require formal stakeholder decisions before feature specification.

### EPIC-13: Incident Logging & Management *(V1 — Needs Stakeholder Decision)*
- **Owning Module**: `Incidents` *(Deferred Specification)*
- **Primary Persona**: Chief Leader, Platform Administrator
- **Business Capability**: Enables logging, reviewing, and tracking operational on-site incidents (injuries, venue disruptions, safety concerns). Remains an approved V1 capability, but detailed business rules (incident schema, severity taxonomy, escalation pathways) require formal stakeholder decisions before feature specification.

---

## 4. Summary of Traceability to Architecture & Implementation Slices

| Epic | Owning Module | Key Feature Slices | Key Architecture Reference |
| :--- | :--- | :--- | :--- |
| **EPIC-01** | `Identity & Access` | `FOUNDATION-01`, `SLICE-20` | ADR-002, ADR-003 |
| **EPIC-02** | `Events` | `SLICE-02`, `SLICE-06`, `SLICE-13`, `SLICE-14` | CHANGE-002, ADR-005, ADR-007, ADR-009 |
| **EPIC-03** | `Staffing` / `Events` | `SLICE-03`, `SLICE-04`, `SLICE-05` (CHANGE-001 cross-cutting) | CHANGE-001, ADR-003, ADR-005 |
| **EPIC-04** | `Staffing` | `SLICE-07` | ADR-003, ADR-009 |
| **EPIC-05** | `Staffing` | `SLICE-08`, `SLICE-09` (CHANGE-001 cross-cutting) | ADR-003, ADR-005, ADR-009 |
| **EPIC-06** | `Staffing` | `SLICE-10`, `SLICE-11` | ADR-003, ADR-005 |
| **EPIC-07** | `Events` / `Staffing` | `SLICE-12` | ADR-005, ADR-009 |
| **EPIC-08** | `Attendance` | `SLICE-15`, `SLICE-16` | ADR-003, ADR-005, ADR-009 |
| **EPIC-09** | `Notifications` | `SLICE-17` | ADR-006 |
| **EPIC-10** | `Identity & Access` | `SLICE-18` | ADR-003 |
| **EPIC-11** | Cross-Cutting | `SLICE-19` | ADR-007 |
| **EPIC-12** | `Feedback` | `SLICE-21` | ADR-001 |
| **EPIC-13** | `Incidents` | `SLICE-22` | ADR-001 |
