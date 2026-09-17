# Carpediem Critical Domain & Architecture Sequence Index

> [!IMPORTANT]
> **Status: PROPOSED ARCHITECTURAL BASELINE**
> **Governance Precedence**: Proves domain rules, lifecycle transitions, authorization tiers, transaction boundaries, and audit mandates across the modular monolith before designing the physical ERD or database tables.
> **Scope**: Logical and architectural interaction flows proving system coherence across actors, Angular SPA, ASP.NET Core host, and module boundaries.

---

## 1. Purpose & Objectives

The sequence diagrams in this directory validate that all ratified business requirements, lifecycle state machines, and technical decisions function together cohesively prior to data modeling:
1. **Prove Lifecycle Orthogonality**: Demonstrate that the 5 independent lifecycles (`Event`, `ChiefLeaderAssignment`, `AvailabilityResponse`, `TeamAssignment`, `AttendanceOutcome`) and the staffing context schedule boundary operate without cross-lifecycle conflation.
2. **Validate Three-Tier Authorization (ADR-003)**: Confirm that Tier 1 (Global Role Policies), Tier 2 (Event-Scoped Resource Handlers), and Tier 3 (Domain Lifecycle Invariants) execute in their proper architectural boundaries.
3. **Verify Audit Trail Mandates (ADR-005)**: Guarantee that explicit business justifications, administrative overrides, and external confirmations record mandatory audit evidence within the same atomic unit of work as the domain state change.
4. **Enforce Dual-Role & Exclusivity Rules (CHANGE-001)**: Prove that a user holding both `ChiefLeader` and `Leader` roles can participate as a volunteer on non-led events while strictly enforcing mutual exclusivity on events they lead.
5. **Establish Transaction & Concurrency Boundaries (ADR-009)**: Identify exact units of work, delineate atomic multi-aggregate updates, and highlight concurrency-sensitive points where optimistic concurrency controls prevent race conditions.

---

## 2. Authoritative Sources

The sequence specifications are derived directly from the ratified foundation:
* [Formal Lifecycle Specification](file:///c:/dev/web/Carpediem/docs/requirements/LIFECYCLE_SPECIFICATION.md)
* [CHANGE-001: Chief Leader Dual-Role Participation](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md)
* [Modular Monolith Boundaries](file:///c:/dev/web/Carpediem/docs/architecture/MODULE_BOUNDARIES.md)
* [C1 System Context](file:///c:/dev/web/Carpediem/docs/architecture/c4/C1_SYSTEM_CONTEXT.md)
* [C2 Container Architecture](file:///c:/dev/web/Carpediem/docs/architecture/c4/C2_CONTAINER.md)
* [ADR-002: Authentication & Session Strategy](file:///c:/dev/web/Carpediem/docs/decisions/ADR-002-authentication-session-strategy.md)
* [ADR-003: Authorization & Resource Policies](file:///c:/dev/web/Carpediem/docs/decisions/ADR-003-authorization-resource-policies.md)
* [ADR-005: Audit Trail Strategy](file:///c:/dev/web/Carpediem/docs/decisions/ADR-005-audit-trail-strategy.md)
* [ADR-009: SQL Server Persistence & Schema Organization](file:///c:/dev/web/Carpediem/docs/decisions/ADR-009-sql-server-persistence-and-schema-organization.md)

---

## 3. Sequence Inventory

| Sequence ID | Title & Focus | Primary Actor(s) | Modules Involved | Key Invariants / Guards Proved |
| :--- | :--- | :--- | :--- | :--- |
| **[SEQ-001](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-001-event-creation-chief-leader-publication.md)** | **Event Creation, Chief Leader Appointment, Acceptance & Publication** | Admin, Candidate Chief Leader | `Events`, `Identity`, `Notifications`, `Audit` | Mandatory publishing prerequisite: Event has exactly one `Active` accepted Chief Leader; decline branch historical retention. |
| **[SEQ-002](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-002-leader-availability-and-selection.md)** | **Leader Availability Declaration, Candidate Pooling & Team Selection** | Volunteer Leader, Active Chief Leader | `Staffing`, `Events`, `Notifications`, `Audit` | Decoupled dimensions (Dimension A vs Dimension B); candidate pooling from active staffing context; immediate active team assignment. |
| **[SEQ-003](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-003-direct-leader-assignment.md)** | **Direct Assignment of Unanswered / Not Available Leader** | Chief Leader, Admin, Volunteer Leader | `Staffing`, `Events`, `Notifications`, `Audit` | Approved direct draft exception; availability truth preserved (does NOT mutate to Available); admin override audit record. |
| **[SEQ-004](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-004-cancellation-and-replacement.md)** | **Leader Self-Cancellation, Staffing Gap & Replacement Selection** | Assigned Leader, Chief Leader, Admin | `Staffing`, `Events`, `Notifications`, `Audit` | Mandatory cancellation reason; immutable history (never delete/reactivate); replacement creates new `TeamAssignment`. |
| **[SEQ-005](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-005-dual-role-chief-leader-as-leader.md)** | **Dual-Role Chief Leader Participating as Normal Leader** | Dual-Role User, External Chief Leader | `Staffing`, `Events`, `Identity` | Proves CHANGE-001: Global capability $\neq$ contextual responsibility; strict same-event exclusivity ($\text{Active CL} \implies \neg\text{Active TeamMember}$). |
| **[SEQ-006](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-006-pending-chief-leader-conflict-resolution.md)** | **Pending Chief Leader Appointment with Existing Team Assignment & Conflict Resolution** | Dual-Role User, Admin | `Events`, `Staffing`, `Audit` | Pending appointment does not restrict; transition to `Active` is rejected until active team assignment is resolved; NO silent auto-cancellation. |
| **[SEQ-007](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-007-event-reschedule-staffing-cycle.md)** | **True Date Reschedule & Staffing Context Rotation** | Admin, Chief Leader, Volunteer Leaders | `Events`, `Staffing`, `Notifications`, `Audit` | Material date change rotates staffing context; prior context preserved historically; new context begins with No AvailabilityResponse. |
| **[SEQ-008](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-008-attendance-finalization-and-correction.md)** | **Attendance Finalization, Pending Attendance & Administrative Correction** | Active Chief Leader, Admin | `Attendance`, `Staffing`, `Events`, `Audit` | Immediate roll call finalization (no routine admin approval gate); decoupled event completion; non-persisted `Pending Attendance` operational condition; audited admin correction. |

---

## 4. Cross-Sequence Architectural Invariants

Across all eight sequence specifications, the following universal principles are strictly maintained:
1. **Server-Side Authoritative Enforcement**: Angular route guards and UI button toggles serve strictly as user convenience; every mutation passes through ASP.NET Core Tier 1 (Role Policy), Tier 2 (Resource Handler), and Tier 3 (Domain Aggregate Lifecycle Invariant) gates.
2. **Immediate Placement & Decoupled Dimensions**: Carpediem eliminates multi-stage invitation or re-confirmation queues for standard selection. An assignment is immediately `Active`. Volunteer availability (Dimension A) and roster assignment (Dimension B) are strictly decoupled and tracked independently.
3. **Absence-Based State Normalization**: The system never stores synthetic placeholder values (`Unanswered`, `None`) to signify absence. Unanswered availability is represented by the absence of an `AvailabilityResponse` record; an unassigned volunteer is represented by the absence of a `TeamAssignment` record.
4. **Immutable History & Record Lineage**: Assignments, declinations, cancellations, and old staffing contexts are never physically deleted or overwritten. Replacements always create new entity records.
5. **Decoupled Event Closeout**: An event transitions to `Completed` based on the conclusion of its scheduled time window (`Now > EndTime`) or via explicit Admin closeout, completely independent of attendance submission. Unsubmitted roll calls surface as a derived operational flag (`Pending Attendance`), never as a blocking lifecycle state.

---

## 5. Notification Boundary: Approved vs. Candidate Triggers

In accordance with architectural boundaries, notification requirements are distinguished between explicitly approved business rules and candidate triggers pending the resolution of ADR-006:

### A. Approved Notification Requirements
1. **Chief Leader Appointment Notification**: The appointed candidate receives an in-platform notification prompting acceptance or decline (SEQ-001).
2. **Volunteer Placement Notification**: A volunteer selected or directly drafted onto the team roster receives an in-platform notification confirming their assignment (SEQ-002, SEQ-003, SEQ-005).
3. **Volunteer Self-Cancellation Notification**: The assigned Chief Leader and Operations Admins receive an in-platform notification alerting them to a volunteer cancellation (SEQ-004).
4. **Material Same-Day Schedule Modification**: Volunteers and Chief Leaders receive notification of same-day time/location adjustments according to approved rules (SEQ-007).

### B. Candidate Notification Triggers (Pending Notification Specification / ADR-006)
1. **Event Publication Broadcast**: General broadcast alerting all volunteer Leaders that an event is published and open for availability (SEQ-001).
2. **Chief Leader Declination Alert**: Alerting Admins that an appointed Chief Leader declined the assignment (SEQ-001).
3. **True Date Reschedule Broadcast**: Alerting previously engaged volunteers to submit availability for a new rescheduled date (SEQ-007).

*Delivery & Persistence Boundary*: The reliable delivery and persistence transport (e.g., relational persistence with request-time polling vs. SignalR real-time hubs) remains to be formally established under ADR-006.

---

## 6. Concurrency-Sensitive Operations (Candidates for ERD Review)

The sequence flows identified key race-condition scenarios requiring database-level constraints or optimistic concurrency control, to be formally specified during the ERD and data-modeling phase:
1. **Chief Leader Acceptance / Reassignment Race**: Protects against concurrent candidate acceptance and administrative reassignment (SEQ-001).
2. **Duplicate Team Assignment Race**: Invariant: at most one `Active` `TeamAssignment` per volunteer per event context (SEQ-002, SEQ-003). Potential enforcement mechanisms (unique filtered index or concurrency tokens) will be evaluated in the ERD.
3. **Volunteer Self-Cancellation vs. Admin Removal Collision**: Ensures simultaneous volunteer cancellation and administrative removal on the same roster slot resolve deterministically without stale overwrites (SEQ-004).
4. **Attendance Record Concurrency**: Prevents simultaneous administrative corrections from clashing with late Chief Leader roll call updates (SEQ-008).
5. **Event Schedule Rescheduling Race**: Ensures concurrent administrative updates do not desynchronize the event schedule or staffing context (SEQ-007).

---

## 7. Evidence for Remaining Architecture Decisions

The sequence flows provide grounded architectural evidence to guide the resolution of the three remaining Phase 2 ADRs:

### A. Evidence for ADR-006: In-Platform Notification Strategy
- No approved sequence currently proves that push-style real-time delivery is mandatory.
- Persisted notifications with request-time refresh/polling remain viable for ~150 users; SignalR remains an option if UX requirements justify immediate delivery.
- Notification intent is recorded as part of the successful application workflow; transport and polling cadence remain to be resolved in ADR-006.

### B. Evidence for ADR-007: Background Processing Strategy
- `In Progress` status can be derived dynamically from current time (`StartTime <= Now <= EndTime`).
- `Pending Attendance` is a derived operational condition evaluated upon query read.
- Whether `Event.Completed` requires time-driven background mutation depends on the verified Event-completion rule (automatic background transition vs. request-time evaluation vs. explicit Admin closeout).
- No other approved sequence currently proves a background worker is required.

### C. Evidence for ADR-008: Deployment Architecture
- All sequence interactions consist of standard HTTPS REST requests between the Angular SPA and the ASP.NET Core modular monolith backend, supporting unified-origin reverse proxy routing (`/api/*` $\rightarrow$ API, `/*` $\rightarrow$ SPA).
- Local Docker Compose approximates the application/API/database service topology. CloudFront, ALB, ECS/Fargate, IAM, TLS, networking, and managed-service behavior remain production-specific.

---

## 8. Unresolved Items & Governance Boundaries Discovered

The sequence design strictly respected governance boundaries and deliberately avoided inventing business rules for non-blocking concerns:
1. **Post-Publish Chief Leader Replacement (ADR-010 / Deferred)**:
   - SEQ-001 covers the initial appointment and acceptance prior to publication.
   - If an active Chief Leader withdraws *after* an event is published, the exact reassignment workflow remains formally deferred to the Event Management feature specification.
2. **Replacement Lineage Storage (Deferred to ERD)**:
   - SEQ-004 establishes that a replacement creates a new `TeamAssignment`. The ERD review must determine whether explicit replacement lineage (e.g., a reference to the cancelled assignment) is useful/required.
3. **Volunteer Cancellation Penalties / Cut-offs (Unresolved)**:
   - No cancellation penalty, late-cancellation threshold, or commitment effect is currently approved. These remain unresolved and must not be invented.
4. **Data Retention Purge Policies (ADR-014 / Deferred)**:
   - Historical records are preserved indefinitely without inventing an arbitrary purge period.
