# Feature Specification: SLICE-11 — Replacement Leader Assignment

**Feature Branch**: `codex/010-replacement-leader-assignment`  
**Created**: 2026-10-02  
**Status**: Draft  
**Input**: User request: "Replacement Leader Assignment per Milestone M2 (Volunteer Churn & Replacement) and Decision Group 4"  

---

## 1. Executive Summary & Business Intent

In event staffing operations, when an active volunteer leader cancels their assignment (via `SLICE-10`), a staffing gap is created. To preserve operational readiness for published events, the assigned Chief Leader (or Platform Administrator) must be able to draft a replacement volunteer leader into the active roster.

Per **Decision Group 4** and **Milestone M2** (ratified in `docs/requirements/LIFECYCLE_SPECIFICATION.md` Section 3, `docs/requirements/ROLE_PERMISSION_MATRIX.md` Item 10, and `docs/roadmap/V1_FEATURE_SLICES.md` Item SLICE-11):
- When a replacement leader is drafted, the original `Cancelled` assignment remains **preserved immutably** for historical tracking and attendance audit. It is **never overwritten, mutated, or deleted**.
- A **brand new distinct** `TeamAssignment` record is created in `Status = 'Active'` for the replacement leader with `AssignedAt = UtcNow` and `AssignedByUserId` set to the acting Chief Leader or Administrator.
- The Chief Leader assigned to the event has direct autonomy to draft replacement leaders from the pool of eligible candidates.
- Platform Administrators maintain operational oversight and can also execute replacement assignments directly when needed.
- Replacement assignment is permitted strictly on `Published` events before the event start time (`Event.StartDateTime > UtcNow`).
- Eligible replacement candidates include leaders who declared `Available` as well as other unassigned leaders in the organization who do not currently hold an active assignment for the event's current staffing cycle.
- The active Chief Leader cannot be selected as a volunteer team member (`chief_leader_conflict`).
- The system alerts the newly drafted volunteer leader via an in-platform notification (`ReplacementLeaderAssigned`) and writes an immutable audit log entry (`Action = 'ReplacementLeaderAssigned'`).

---

## 2. User Scenarios & Testing

### User Story 1 - View Replacement Context & Candidate Pool (Priority: P1) 🎯 MVP

As an active Chief Leader or Operations Administrator,  
I want to view the event's staffing status, including active roster members, cancelled assignments (staffing gaps), and eligible replacement candidates,  
So that I can identify vacant slots and select qualified replacement volunteers.

**Independent Test**:
1. Log in as the assigned Chief Leader (or Admin) for a published event.
2. Query the replacement context endpoint (`GET /api/events/{eventId}/replacement-assignment`).
3. Verify HTTP 200 OK returning event details, `isWindowOpen: true`, the active team list, the list of cancelled assignments (with cancellation reasons and timestamps), and the pool of eligible candidates.
4. Verify non-Chief Leader / non-Admin users receive 403 Forbidden (`unauthorized_chief_leader`).

**Acceptance Scenarios**:
1. **Given** a `Published` event with 1 active member and 1 cancelled member, **when** Chief Leader views replacement context, **then** the response returns both active and cancelled assignments, plus available candidates.
2. **Given** an event whose start time is in the past, **when** replacement context is viewed, **then** `isWindowOpen` is `false`.
3. **Given** an event in `Draft` status, **when** replacement context is queried, **then** returns 409 Conflict (`invalid_event_status`).

---

### User Story 2 - Chief Leader Drafts Replacement Leader (Priority: P1) 🎯 MVP

As an assigned Chief Leader,  
I want to select an eligible replacement leader for my event, optionally linking the replacement to the cancelled assignment being filled,  
So that the staffing gap is closed and the replacement volunteer is immediately added to the active roster.

**Independent Test**:
1. Log in as the active Chief Leader of a published event.
2. Submit a replacement assignment (`POST /api/events/{eventId}/replacement-assignment`) specifying `candidateUserId` and optional `replacedAssignmentId`.
3. Verify HTTP 201 Created returning a new `TeamAssignmentResponse` with `Status = 'Active'` and new unique `Id`.
4. Verify the prior cancelled assignment remains untouched in `Cancelled` status.
5. Verify an in-platform notification is created for the replacement leader (`NotificationType = 'ReplacementLeaderAssigned'`).
6. Verify an audit log entry is recorded (`Action = 'ReplacementLeaderAssigned'`).

**Acceptance Scenarios**:
1. **Given** a published event with an open slot from a cancelled assignment, **when** Chief Leader drafts an available leader, **then** a new active `TeamAssignment` is created immediately.
2. **Given** an optional `replacedAssignmentId` provided in the request, **when** valid, **then** the audit log links the replaced assignment ID.
3. **Given** a non-existent or active `replacedAssignmentId`, **when** submitted, **then** returns 400 Bad Request or 409 Conflict.

---

### User Story 3 - Administrator Override Replacement Selection (Priority: P1) 🎯 MVP

As an Operations Administrator,  
I want to draft a replacement volunteer leader on behalf of an event when necessary,  
So that staffing gaps can be resolved operationally even if the Chief Leader is unavailable.

**Independent Test**:
1. Log in as an Operations Administrator.
2. Submit a replacement assignment for an eligible candidate on a published event.
3. Verify HTTP 201 Created.
4. Verify audit log captures actor as Admin's user ID and records source as `AdminOverrideReplacement`.

**Acceptance Scenarios**:
1. **Given** an Administrator acting on any published event, **when** drafting a replacement, **then** the request succeeds with 201 Created.

---

### User Story 4 - Concurrency, Eligibility Guards & Idempotency (Priority: P2)

As the system,  
I want to reject ineligible candidates, enforce concurrency, and support safe idempotent replays,  
So that roster integrity and same-event exclusivity invariants are strictly maintained without duplicate assignments.

**Independent Test**:
1. Attempt to assign the event's active Chief Leader as a replacement -> verify 409 Conflict (`chief_leader_conflict`).
2. Attempt to assign a volunteer who is already active on the team -> verify safe idempotent replay (200 OK + `Idempotent-Replay: true`).
3. Attempt to draft replacement on an event that has already started -> verify 409 Conflict (`window_closed`).
4. Attempt to submit with a non-Chief Leader user -> verify 403 Forbidden (`unauthorized_chief_leader`).

**Acceptance Scenarios**:
1. **Given** candidate is the active Chief Leader, **when** replacement drafted, **then** returns 409 Conflict.
2. **Given** candidate already holds an active `TeamAssignment` in the current cycle, **when** submitted again, **then** returns 200 OK with `Idempotent-Replay: true`.
3. **Given** event start time has passed, **when** replacement drafted, **then** returns 409 Conflict (`window_closed`).

---

### User Story 5 - Mobile-First, Accessible Replacement Roster Interface (Priority: P2)

As a Chief Leader using a mobile device or desktop browser,  
I want a responsive, accessible interface showing staffing gaps and available replacement candidates with clear touch controls,  
So that I can easily draft replacements in the field under all accessibility conditions.

**Independent Test**:
1. Open `/events/:eventId/replacement-assignment` across 320px, 360px, 390px, 430px, and desktop viewports.
2. Run axe-core accessibility audit -> verify 0 WCAG 2.2 AA violations.
3. Verify touch targets on all buttons meet the 44px minimum requirement.
4. Verify 200% font zoom reflows without horizontal scroll overflow.
5. Verify screen reader polite live region announces selection outcomes.

---

## 3. Functional Requirements (FR-001 – FR-016)

- **FR-001**: The system MUST provide `GET /api/events/{eventId}/replacement-assignment` returning event details, staffing cycle ID, window open status, active roster, cancelled assignments, and eligible replacement candidates.
- **FR-002**: Only the assigned active Chief Leader or an Operations Administrator is authorized to query replacement context and draft replacements. Other users MUST receive 403 Forbidden (`unauthorized_chief_leader`).
- **FR-003**: The replacement window is open if and only if `Event.StartDateTime > UtcNow` AND `Event.Status == Published`. If the event has started or is not published, mutations MUST be rejected with 409 Conflict (`window_closed` or `invalid_event_status`).
- **FR-004**: The system MUST provide `POST /api/events/{eventId}/replacement-assignment` accepting `candidateUserId`, optional `replacedAssignmentId`, and optional `justification`.
- **FR-005**: Drafting a replacement MUST create a brand new `TeamAssignment` record with `Status = 'Active'`, `AssignedAt = UtcNow`, `AssignedByUserId = actorUserId`, `EventId = eventId`, and `StaffingCycleId = currentCycleId`.
- **FR-006**: Prior `Cancelled` assignment records MUST remain immutably preserved in the database and MUST NOT be updated, deleted, or reactivated.
- **FR-007**: If `replacedAssignmentId` is supplied, the system MUST verify that the assignment belongs to the event and has `Status == 'Cancelled'`. If invalid, the system MUST reject with 400 Bad Request or 409 Conflict.
- **FR-008**: The candidate cannot be the currently active Chief Leader of the event. If the candidate is Chief Leader, reject with 409 Conflict (`chief_leader_conflict`).
- **FR-009**: The candidate cannot already hold an active `TeamAssignment` for the current staffing cycle. If the candidate is already active, return 200 OK with `Idempotent-Replay: true` header.
- **FR-010**: All mutations MUST execute within a single atomic SQL Server transaction committing assignment persistence, notification dispatch, and audit logging.
- **FR-011**: The system MUST insert an `InPlatformNotification` (`NotificationType = 'ReplacementLeaderAssigned'`) for the drafted candidate.
- **FR-012**: The system MUST append an `AuditLog` entry (`Action = 'ReplacementLeaderAssigned'`) capturing actor, candidate ID, event ID, and optional replaced assignment ID.
- **FR-013**: The API MUST enforce antiforgery token verification (`X-XSRF-TOKEN`) on POST endpoints.
- **FR-014**: The frontend MUST provide a mobile-first responsive UI adhering to WCAG 2.2 AA (0 axe violations), 44px minimum touch targets, zero layout shift, and polite ARIA live regions.
- **FR-015**: Component stylesheets MUST strictly respect Angular's 4.00 kB maximum budget limit.
- **FR-016**: Reschedule and cycle rotation (`SLICE-12`) is strictly deferred. No synthetic scoring, attendance recording, or penalties may be introduced.

---

## 4. Success Criteria (SC-001 – SC-005)

- **SC-001**: 100% of domain invariants verified via unit tests with 0 regressions.
- **SC-002**: SQL Server 2025 container integration tests passing for all query, assignment, eligibility, concurrency, notification, and audit invariants.
- **SC-003**: Frontend unit tests and production build passing with 0 warnings, 0 errors, and budgets respected.
- **SC-004**: Playwright E2E and axe-core accessibility suite passing across mobile (320px–430px) and desktop viewports with 0 violations.
- **SC-005**: k6 performance benchmark demonstrating 100 concurrent VUs executing replacement assignments with 0 failed requests and sub-second p95 response time.
