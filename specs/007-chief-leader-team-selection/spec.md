# Feature Specification: SLICE-08 — Chief Leader Team Selection from Available Pool

**Feature Branch**: `codex/007-chief-leader-team-selection`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Chief Leader Team Selection from Available Pool (Immediate active roster placement per Decision Group 5)"

---

## 1. Executive Summary & Business Intent

Once an Event is published (`SLICE-06`) and volunteer Leaders have submitted their schedule availability (`SLICE-07`), the assigned Chief Leader must construct their on-site volunteer event team roster.

`SLICE-08` empowers the active Chief Leader (and authorized Operations Administrators) to view the pool of volunteers who declared `Available` for the event's current active `StaffingCycle`, inspect the current team roster, and select volunteers onto the active team.

Per **Decision Group 5** (ratified in `docs/product/PRODUCT_DISCOVERY.md` Item 8 and `docs/requirements/LIFECYCLE_SPECIFICATION.md` Section 5 & 7), selection **immediately** places the volunteer onto the confirmed team roster in `Active` status without redundant confirmation gates. Furthermore, `SLICE-08` enforces the **CHANGE-001 Same-Event Exclusivity Invariant**: the active Chief Leader for an event cannot be selected as a volunteer team member on that same event, while retaining full selection eligibility on other events where they hold the `Leader` role.

---

## 2. User Scenarios & Testing *(mandatory)*

### User Story 1 - View Available Pool and Active Roster (Priority: P1) 🎯 MVP

As the active Chief Leader for a published event,
I want to view the list of volunteer Leaders who marked themselves `Available` alongside the current active team roster,
So that I can see my candidate options and know how many team slots remain.

**Why this priority**: Visibility of the candidate pool is the fundamental prerequisite for team selection.

**Independent Test**:
1. Log in as the assigned active Chief Leader for a `Published` Event.
2. Navigate to `/events/{eventId}/team-selection`.
3. Verify the view displays event metadata (title, Cairo schedule, location), the list of candidates who marked `Available` in the current cycle, and the list of currently active team members with total count.
4. Verify non-leaders or unauthenticated users are rejected with HTTP 401/403.

**Acceptance Scenarios**:
1. **Given** a `Published` Event where three Leaders marked `Available` and none are yet selected, **when** the active Chief Leader views the team selection screen, **then** all three Leaders appear in the "Available Volunteers" pool with declaration timestamps, and the active roster shows zero members.
2. **Given** a user who is NOT the active Chief Leader or an Admin on Event $E$, **when** they attempt to access the team selection endpoint, **then** the server rejects the request with HTTP 403 Forbidden (`unauthorized_chief_leader`).
3. **Given** an Event that is in `Draft`, `Completed`, or `Cancelled` status, **when** team selection query is accessed, **then** the request is rejected with HTTP 409 Conflict (`invalid_event_status`).

---

### User Story 2 - Select Volunteer into Immediate Active Roster (Priority: P1) 🎯 MVP

As the active Chief Leader for a published event,
I want to select an available volunteer Leader from the pool,
So that they are immediately placed onto the active event team roster and notified.

**Why this priority**: Direct, immediate placement delivers on Decision Group 5 by eliminating volunteer confirmation friction and finalizing team rosters efficiently.

**Independent Test**:
1. View an available volunteer in the pool.
2. Click "Select for Team".
3. Verify the server returns HTTP 201 Created and persists an `Active` `TeamAssignment` in `staffing.TeamAssignments`.
4. Verify an audit record is logged (`Action = 'VolunteerSelected'`) and an in-platform notification intent is created in `notifications.NotificationIntents`.
5. Verify the candidate moves from the "Available Volunteers" list into the "Active Team Roster".

**Acceptance Scenarios**:
1. **Given** an available volunteer candidate $U$ in the current cycle of Event $E$, **when** the Chief Leader selects $U$, **then** a new `TeamAssignment` with `Status = 'Active'` is created, and the candidate is moved into the active roster.
2. **Given** a successful team selection, **when** persisted, **then** an atomic SQL transaction creates the `TeamAssignment`, creates a `NotificationIntent` for the selected volunteer, and writes an audit log with actor attribution.
3. **Given** an active team roster, **when** new volunteers are selected, **then** the active roster count dynamically increments and the list updates without full page reloads.

---

### User Story 3 - Enforce CHANGE-001 Exclusivity and Lifecycle Invariants (Priority: P2)

As the platform system and operational leadership,
I want selection to enforce same-event exclusivity, availability eligibility, and duplicate assignment prevention,
So that the roster is strictly consistent and free of operational conflicts.

**Why this priority**: Same-event dual-role conflicts and duplicate assignments corrupt operational command on site.

**Independent Test**:
1. Attempt to select a user who is the active Chief Leader on Event $E$; verify server rejects with HTTP 409 Conflict (`chief_leader_conflict`).
2. Attempt to select a volunteer who has withdrawn their availability (now `NotAvailable`) or who never responded; verify server rejects with HTTP 409 Conflict (`candidate_not_available`).
3. Attempt to select a volunteer who is already `Active` on the team roster for this event and cycle; verify filtered index / concurrency rejects duplicate creation.
4. Attempt selection after `Event.StartDateTime <= UtcNow`; verify server rejects with HTTP 409 Conflict (`window_closed`).

**Acceptance Scenarios**:
1. **Given** User $U$ is the active Chief Leader on Event $E$, **when** any selection attempt targets $U$ on Event $E$, **then** the request is rejected with HTTP 409 Conflict and error code `chief_leader_conflict`.
2. **Given** Candidate $C$ marked `NotAvailable` or has no availability record in the current cycle, **when** the Chief Leader attempts selection via the available pool endpoint, **then** the request is rejected with HTTP 409 Conflict (`candidate_not_available`).
3. **Given** Candidate $C$ is already actively assigned on Event $E$ in the current cycle, **when** a duplicate selection request is submitted, **then** the system enforces uniqueness and rejects or safely replays.

---

### User Story 4 - Concurrency, Idempotency, and Admin Oversight (Priority: P2)

As a Chief Leader or Operations Administrator,
I want selection requests to be resilient against network retries and concurrent drafting actions,
So that duplicate assignments and spurious audit records are never created.

**Why this priority**: Mobile network unpredictability during staffing coordination requires strict idempotency.

**Independent Test**:
1. Submit a selection request with candidate $C$.
2. Replay the identical selection request; verify HTTP 200 OK with `Idempotent-Replay: true` header without creating duplicate assignments, extra audit records, or duplicate notification intents.
3. As an Operations Admin, execute selection on behalf of the event; verify successful `Active` placement with audit attribution.

**Acceptance Scenarios**:
1. **Given** Candidate $C$ is already an `Active` team member on Event $E$ from an earlier selection, **when** the Chief Leader re-submits the identical selection command, **then** the server returns HTTP 200 OK with `Idempotent-Replay: true` and the existing assignment details.
2. **Given** an Operations Admin authenticated on the platform, **when** the Admin executes team selection on a published event, **then** the selection succeeds and logs `AuditSource = 'AdminSelection'`.

---

### User Story 5 - Mobile-First, Accessible Team Selection UI (Priority: P2)

As a Chief Leader managing team selection from a mobile device (320px–430px viewport),
I want a responsive, touch-friendly interface with clear roster counters, polite screen reader updates, and zero layout shift,
So that I can select my team efficiently on the go.

**Why this priority**: Chief Leaders frequently manage volunteer rosters from mobile devices between shifts.

**Independent Test**:
1. In Playwright across 320px, 360px, 390px, 430px viewports and desktop:
   - Run axe-core accessibility audit (0 violations).
   - Verify touch targets meet or exceed 44x44px.
   - Verify 200% font scaling reflow without horizontal scrolling.
   - Verify ARIA polite announcements when a volunteer is selected.

**Acceptance Scenarios**:
1. **Given** any viewport width from 320px upward, **when** viewing the team selection screen, **then** candidate cards and selection buttons fit without horizontal scrolling, and touch target heights are $\ge 44\text{px}$.
2. **Given** assistive technology (screen reader), **when** a volunteer is selected, **then** the update is announced via an `aria-live="polite"` region.
3. **Given** reduced-motion preferences, **when** interacting with candidate lists, **then** animations are disabled or instantaneous.

---

## 3. Edge Cases & Boundary Conditions

- **Concurrent Availability Revocation**: If Candidate $C$ toggles their response from `Available` to `NotAvailable` right before the Chief Leader clicks select, the database transaction checks current availability and aborts with HTTP 409 Conflict (`candidate_not_available`).
- **Rescheduled Event Cycle**: If an event was rescheduled (`SLICE-12`), only responses and assignments in the active `StaffingCycle` (`IsCurrent == true`) are displayed and selectable. Historical assignments remain preserved under older cycles.
- **Immediate Start Window Cutoff**: Once `Event.StartDateTime <= DateTimeOffset.UtcNow`, team selection closes and attempts are rejected with HTTP 409 Conflict (`window_closed`).
- **Direct Drafting of Non-Respondents**: Direct drafting of non-respondents and `NotAvailable` volunteers is strictly governed by `SLICE-09` and is out of scope for `SLICE-08`.

---

## 4. Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST restrict team selection actions to the authenticated active Chief Leader for Event $E$ or an Operations Administrator (`RequireChiefLeaderOrAdmin` authorization).
- **FR-002**: The system MUST permit team selection ONLY on Events with `Status == EventStatus.Published`. Requests on `Draft`, `Completed`, or `Cancelled` events MUST be rejected with HTTP 409 Conflict (`invalid_event_status`).
- **FR-003**: The system MUST permit team selection ONLY while `Event.StartDateTime > DateTimeOffset.UtcNow`. Requests submitted when `Event.StartDateTime <= DateTimeOffset.UtcNow` MUST be rejected with HTTP 409 Conflict (`window_closed`).
- **FR-004**: The system MUST strictly enforce the CHANGE-001 Same-Event Exclusivity Invariant: if Candidate $U$ is the active Chief Leader on Event $E$, the system MUST reject any attempt to select $U$ as a team member on Event $E$ with HTTP 409 Conflict (`chief_leader_conflict`).
- **FR-005**: Team selection MUST target exclusively candidates who have declared `Response = Available` in the Event's current active `StaffingCycle` (`IsCurrent == true`). If a candidate does not have an `Available` response in the active cycle, the selection MUST be rejected with HTTP 409 Conflict (`candidate_not_available`).
- **FR-006**: Team selection MUST immediately create a `TeamAssignment` record in `staffing.TeamAssignments` with `Status = TeamAssignmentStatus.Active` (Decision Group 5). No intermediate invited or pending confirmation states may be introduced.
- **FR-007**: The system MUST enforce a filtered unique index `UX_TeamAssignments_ActiveLeaderPerCycle` on `(StaffingCycleId, UserId)` where `Status = 'Active'` ensuring a Leader cannot hold duplicate active assignments on the same event cycle.
- **FR-008**: Team selection MUST be idempotent: submitting an identical selection request for a candidate who is already active on the team roster MUST return HTTP 200 OK with `Idempotent-Replay: true` header without creating duplicate audit records or duplicate notifications.
- **FR-009**: Every team selection MUST execute within a single atomic SQL Server transaction that inserts `staffing.TeamAssignments`, inserts `notifications.NotificationIntents` for the volunteer, and appends an immutable audit log in `audit.AuditLogs` (`Action = 'VolunteerSelected'`, `ResourceType = 'TeamAssignment'`).
- **FR-010**: All mutation requests MUST require an antiforgery token (`X-XSRF-TOKEN` header and cookie).
- **FR-011**: The system MUST expose a query endpoint `GET /api/events/{eventId}/team-selection` returning the event metadata, list of available candidates (with user ID, name/username, declared timestamp), and list of active team members (with assignment ID, user ID, name/username, assigned timestamp).
- **FR-012**: The system MUST expose a mutation endpoint `POST /api/events/{eventId}/team-selection` accepting candidate user ID to select them onto the team.
- **FR-013**: The client UI MUST display the Available Volunteers pool and the Active Team Roster with current count badge.
- **FR-014**: The client UI MUST provide accessible "Select for Team" actions per candidate, disabling controls while a submission is in flight.
- **FR-015**: The client UI MUST support mobile viewports down to 320px width with minimum 44x44px touch targets, zero horizontal overflow, and `aria-live="polite"` announcements for roster additions.

---

### Non-Functional & Architecture Requirements

- **NFR-001**: API endpoints for team selection pool and selection mutations must respond with $P_{95} < 300\text{ms}$ under 100 concurrent requests.
- **NFR-002**: Database operations must execute against Microsoft SQL Server 2025 under Read Committed Snapshot Isolation (RCSI) with atomic transactions.
- **NFR-003**: The user interface must achieve WCAG 2.2 Level AA compliance, verified by automated axe-core audits.
- **NFR-004**: No external message brokers, distributed caches, or background queues may be introduced. Native SQL Server relational storage per ADR-006 and ADR-009 is the ratified architecture.

---

## 5. Success Criteria *(mandatory)*

- **SC-001**: An active Chief Leader can view the list of available volunteer candidates and current team roster on their published event.
- **SC-002**: Selecting an available volunteer immediately places them into `Active` status on the team roster, records an audit log, and queues a notification intent.
- **SC-003**: The CHANGE-001 exclusivity invariant prevents the active Chief Leader from being selected onto the team for their led event.
- **SC-004**: Candidates without `Available` declarations or past-deadline selections are strictly rejected with HTTP 409 Conflict.
- **SC-005**: 100% of domain unit tests, SQL Server 2025 container integration tests, Angular unit tests, Playwright E2E/a11y tests, and k6 performance benchmarks pass with zero errors.
