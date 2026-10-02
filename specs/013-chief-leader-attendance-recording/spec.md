# Feature Specification: SLICE-15 — Chief Leader On-Site Attendance Recording

## 1. Context & Business Problem
During and following the execution of a volunteer-staffed event, operational leadership requires factual, on-the-ground roll call verification of volunteer attendance.
In accordance with **ADR-003**, **ADR-005**, **ADR-009**, and **SEQ-008**, the assigned Chief Leader conducts on-site roll call, recording physical service outcomes (`Attended` or `NoShow`) for all actively staffed volunteer leaders on the current event roster.

### Core Architectural Invariants
1. **Decoupled from Event Completion (ADR-007, SEQ-008)**:
   - Attendance submission operates strictly independently from event completion (`SLICE-14`).
   - Roll call may be recorded while the event is `Published` (e.g., at venue check-in) or after the event is `Completed`.
   - Event completion is never stalled by missing attendance; missing attendance post-completion dynamically produces the non-persisted operational condition `Pending Attendance` for operational dashboards (`SLICE-19`).
2. **Absence Modeling (Pure Absence Semantics)**:
   - There is NO persisted `Unrecorded` enum value in the database. Unrecorded attendance is represented strictly by the **absence of an `attendance.AttendanceOutcomes` row** for an active `TeamAssignment`.
   - The UI presents active roster members without an outcome record as `Unrecorded`.
3. **Immediate Finalization & Zero Approval Gate**:
   - Submissions take immediate effect upon submission with NO routine administrative approval queue.
4. **Authorized Persona Boundary**:
   - Only the assigned `Active` Chief Leader for the event or Platform Administrators (`CarpediemRoles.Admin`) can inspect the roll sheet or submit attendance.
   - Other users receive `403 Forbidden` (`unauthorized_chief_leader`).
5. **Auditing & Zero Notifications**:
   - Submitting attendance records an immutable audit trail (`AuditLog.AttendanceFinalized`).
   - Zero notifications are dispatched per ADR-006.

---

## 2. User Stories & Acceptance Criteria

### User Story 1: Chief Leader Views On-Site Attendance Roll Sheet
**As an** active Chief Leader assigned to a published or completed event,  
**I want to** view a roll sheet containing all actively staffed volunteer leaders on the current event roster,  
**So that** I can conduct roll call and identify which volunteers are present or absent.

#### Acceptance Criteria
- **AC-1.1**: The roll sheet endpoint `GET /api/events/{eventId}/attendance` returns the event title, scheduled window, location, active staffing cycle ID, and list of active roster members.
- **AC-1.2**: Cancelled team assignments and past staffing cycles are excluded from the roll sheet.
- **AC-1.3**: For each active volunteer, the response indicates their current recorded outcome (`Attended`, `NoShow`, or `null` for unrecorded) and recorded timestamp.
- **AC-1.4**: If the caller is not the active Chief Leader for this event (and not an Admin), the endpoint returns `403 Forbidden` (`unauthorized_chief_leader`).
- **AC-1.5**: If the event is in `Draft` or `Cancelled` status, the endpoint returns `409 Conflict` (`invalid_event_status`).

---

### User Story 2: Chief Leader Submits Roll Call Outcomes
**As an** active Chief Leader conducting on-site roll call,  
**I want to** mark volunteer leaders as either `Attended` or `NoShow` and submit the roll sheet,  
**So that** participation records are immediately and factually updated.

#### Acceptance Criteria
- **AC-2.1**: The Chief Leader can submit an array of outcome records via `POST /api/events/{eventId}/attendance`.
- **AC-2.2**: Each entry must contain `teamAssignmentId` (referencing an active assignment in the current cycle) and `outcome` (`Attended` or `NoShow`).
- **AC-2.3**: Submission creates or updates `attendance.AttendanceOutcomes` records in an atomic database transaction.
- **AC-2.4**: Submission takes immediate effect; no approval gate is required.
- **AC-2.5**: Submitting attendance records an audit log entry (`Action = 'AttendanceFinalized'`, `Source = 'ChiefLeaderRollCall'`).
- **AC-2.6**: Zero notifications are dispatched to volunteers or administrators upon attendance recording.
- **AC-2.7**: Submitting outcomes for all active assignments clears the derived `Pending Attendance` condition on the event.

---

### User Story 3: Mobile-First Responsive On-Site Experience & Accessibility
**As an** on-site Chief Leader using a mobile smartphone at a venue check-in desk,  
**I want to** quickly toggle attendance status with touch-friendly controls in any light condition,  
**So that** I can take roll call efficiently without mis-taps or UI friction.

#### Acceptance Criteria
- **AC-3.1**: The UI is fully responsive across viewports from 320px up to desktop.
- **AC-3.2**: All interactive controls (toggles, status buttons, submit button) have a minimum touch target size of 44px × 44px.
- **AC-3.3**: The interface complies with WCAG 2.2 AA (contrast, keyboard navigation, focus indicators, polite live announcements).
- **AC-3.4**: When the viewport text is zoomed to 200%, the interface reflows cleanly without horizontal scrolling.

---

## 3. Functional Requirements

| ID | Requirement Statement |
| :--- | :--- |
| **FR-001** | **Persona Authorization**: Only the active Chief Leader on the event or an Admin can view or submit attendance. Non-authorized actors receive `403 Forbidden` (`unauthorized_chief_leader`). |
| **FR-002** | **Event Lifecycle Guard**: Attendance operations are valid only for events in `Published` or `Completed` status. Draft and Cancelled events return `409 Conflict` (`invalid_event_status`). |
| **FR-003** | **Active Assignment Boundary**: Roll call items are restricted to team assignments on the event's current staffing cycle with `Status = Active`. |
| **FR-004** | **Allowed Outcomes**: Permitted outcome values are strictly `Attended` and `NoShow`. |
| **FR-005** | **Pure Absence Semantics**: Absence of an `AttendanceOutcome` row represents unrecorded attendance. |
| **FR-006** | **Unique Constraint**: Each `TeamAssignment` can have at most one `AttendanceOutcome` row (`UX_AttendanceOutcomes_Assignment`). |
| **FR-007** | **Atomic Persistence**: Roll call submissions are committed in a single database transaction. |
| **FR-008** | **Optimistic Concurrency**: `AttendanceOutcome` entity includes a `RowVersion` concurrency token. |
| **FR-009** | **Audit Trail**: Appends `AuditLog.AttendanceFinalized` capturing the event ID, actor ID, correlation ID, and outcome summary counts. |
| **FR-010** | **Zero Notifications**: No notifications are dispatched upon roll call recording. |
| **FR-011** | **Decoupled Completion**: Attendance recording is fully decoupled from event completion. |
| **FR-012** | **Scope Exclusions**: Post-finalization administrative corrections with mandatory justification are reserved for `SLICE-16`. |
