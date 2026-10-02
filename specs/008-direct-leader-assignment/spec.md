# Feature Specification: SLICE-09 — Direct Leader Assignment

**Feature Branch**: `codex/008-direct-leader-assignment`  
**Created**: 2026-10-01  
**Status**: Draft  
**Input**: User request: "Direct Leader Assignment (Direct assignment of non-respondents and NotAvailable volunteers per Approved Blocker 1 and Decision Group 5)"  

---

## 1. Executive Summary & Business Intent

While volunteer leaders typically submit schedule availability before team selection (`SLICE-07` and `SLICE-08`), operational staffing demands often necessitate drafting qualified leaders who have either not yet submitted a response ("Response Needed" / No Response) or who declared themselves `NotAvailable`.

Per **Approved Blocker 1** and **Decision Group 5** (ratified in `docs/requirements/LIFECYCLE_SPECIFICATION.md` Sections 5 & 7 and `docs/roadmap/V1_FEATURE_SLICES.md` Item SLICE-09):
- The assigned Chief Leader and Operations Administrators possess authoritative direct placement rights to assign any eligible volunteer Leader directly onto the event team roster.
- Direct assignment **immediately** places the leader into `Active` status on `staffing.TeamAssignments` without redundant intermediate confirmation gates. If a drafted leader cannot serve, they must explicitly self-cancel via `SLICE-10`.
- The system strictly enforces the **CHANGE-001 Same-Event Exclusivity Invariant**: the active Chief Leader for Event $E$ cannot be directly assigned as a volunteer team member on Event $E$.
- The system records comprehensive audit logs (`Action = 'DirectLeaderAssignmentExecuted'`) documenting the actor attribution and the candidate's prior availability state (`NotAvailable` or `NoResponse`), creates an in-platform notification intent, and guarantees idempotent replays.

---

## 2. User Scenarios & Testing

### User Story 1 - View Direct Draft Candidate Pool (Priority: P1) 🎯 MVP

As the active Chief Leader or Operations Administrator for a published event,  
I want to view qualified leaders who have not responded or who marked `NotAvailable` in the active staffing cycle,  
So that I can identify suitable leaders to directly draft when the available volunteer pool is insufficient.

**Independent Test**:
1. Log in as the assigned active Chief Leader or Admin for a `Published` Event.
2. Query the direct assignment candidate pool endpoint (`GET /api/events/{eventId}/direct-assignment/candidates`).
3. Verify 200 OK returning qualified leaders with their current availability status (`NotAvailable` or `NoResponse`), excluding the Chief Leader and already-active team members.
4. Verify non-chief leaders / unauthorized users receive 403 Forbidden.

**Acceptance Scenarios**:
1. **Given** a `Published` Event with active cycle $C$, **when** the Chief Leader views direct draft candidates, **then** the list includes all eligible leaders who either marked `NotAvailable` or have no availability response in cycle $C$.
2. **Given** User $U$ is the active Chief Leader on Event $E$, **when** the candidate pool is retrieved, **then** $U$ is excluded from the candidate pool per CHANGE-001 exclusivity.
3. **Given** Leader $L$ is already an `Active` member on the event roster for cycle $C$, **when** the candidate pool is retrieved, **then** $L$ is excluded from the selectable direct draft pool.
4. **Given** an Event in `Draft`, `Completed`, or `Cancelled` status, **when** the pool is accessed, **then** the server returns HTTP 409 Conflict (`invalid_event_status`).

---

### User Story 2 - Directly Draft a Non-Respondent Leader into Active Roster (Priority: P1) 🎯 MVP

As the active Chief Leader or Operations Administrator,  
I want to directly assign a leader who has not submitted an availability response,  
So that they are immediately added to the active roster and notified of their assignment.

**Independent Test**:
1. Select a leader with `NoResponse` ("Response Needed").
2. Submit direct assignment (`POST /api/events/{eventId}/direct-assignment`).
3. Verify HTTP 201 Created returning `TeamAssignmentResponse` with `Status = 'Active'`.
4. Verify atomic SQL transaction persists `staffing.TeamAssignments`, `notifications.InPlatformNotifications` (`DirectLeaderAssigned`), and `audit.AuditLogs` (`Action = 'DirectLeaderAssignmentExecuted'`, `AvailabilityState = 'NoResponse'`).
5. Verify the leader appears immediately in the active team roster.

**Acceptance Scenarios**:
1. **Given** a qualified Leader $L$ with no availability response for the current cycle of `Published` Event $E$, **when** the Chief Leader directly assigns $L$, **then** an `Active` `TeamAssignment` is created immediately without confirmation prompts.
2. **Given** a successful direct assignment, **when** committed, **then** an in-platform notification intent is created and an audit log records `Source = 'ChiefLeaderDirectDraft'` (or `'AdminDirectDraft'`) with prior availability state recorded.

---

### User Story 3 - Directly Draft a `NotAvailable` Leader into Active Roster (Priority: P1) 🎯 MVP

As the active Chief Leader or Operations Administrator,  
I want to directly draft a leader who indicated `NotAvailable` when operational requirements demand it,  
So that staffing coverage is achieved and the assignment is formally tracked.

**Independent Test**:
1. Select a leader whose current response is `NotAvailable`.
2. Submit direct assignment (`POST /api/events/{eventId}/direct-assignment`).
3. Verify HTTP 201 Created and `Active` status assignment.
4. Verify audit record notes `AvailabilityState = 'NotAvailable'`.

**Acceptance Scenarios**:
1. **Given** Leader $L$ who previously submitted `NotAvailable` in the active cycle, **when** directly assigned by Chief Leader or Admin, **then** the assignment succeeds with `Status = 'Active'` and logs prior status `NotAvailable`.

---

### User Story 4 - Concurrency, Idempotency, Exclusivity, and Boundary Invariants (Priority: P2)

As the system and platform leadership,  
I want direct assignment to enforce CHANGE-001 exclusivity, prevent duplicate assignments, safely replay duplicate submissions, and reject assignments after event start,  
So that roster integrity and operational discipline are preserved.

**Independent Test**:
1. Attempt direct assignment of the event's active Chief Leader; verify 409 Conflict (`chief_leader_conflict`).
2. Replay identical direct assignment for an already assigned leader; verify 200 OK with `Idempotent-Replay: true` header without duplicate audit logs or notifications.
3. Attempt direct assignment after `Event.StartDateTime <= UtcNow`; verify 409 Conflict (`window_closed`).

**Acceptance Scenarios**:
1. **Given** User $U$ is active Chief Leader on Event $E$, **when** direct assignment targets $U$, **then** server rejects with 409 Conflict (`chief_leader_conflict`).
2. **Given** Candidate $C$ is already actively assigned on Event $E$, **when** direct assignment is submitted again for $C$, **then** server returns 200 OK with `Idempotent-Replay: true`.
3. **Given** `Event.StartDateTime <= UtcNow`, **when** direct assignment is attempted, **then** server rejects with 409 Conflict (`window_closed`).

---

### User Story 5 - Mobile-First, Accessible Direct Assignment UI (Priority: P2)

As a Chief Leader managing assignments on a mobile device,  
I want an accessible, responsive direct draft view with clear availability badges, 44px minimum touch targets, and polite screen reader announcements,  
So that I can staff my team on mobile without layout shift or accessibility barriers.

**Independent Test**:
1. In Playwright across 320px–desktop viewports:
   - Run axe-core accessibility audit (0 violations).
   - Verify minimum 44px touch targets on buttons.
   - Verify 200% font zoom reflow without horizontal scroll overflow.
   - Verify `aria-live="polite"` announcements when direct draft is executed.

---

## 3. Requirements

### Functional Requirements

- **FR-001**: Restrict direct leader assignment actions and candidate queries to authenticated active Chief Leader on Event $E$ or Operations Administrators (`RequireChiefLeaderOrAdmin`).
- **FR-002**: Direct assignment MUST be permitted exclusively on Events with `Status == EventStatus.Published`. Requests on `Draft`, `Completed`, or `Cancelled` events MUST be rejected with HTTP 409 Conflict (`invalid_event_status`).
- **FR-003**: Direct assignment MUST be permitted exclusively while `Event.StartDateTime > DateTimeOffset.UtcNow`. Requests submitted when `Event.StartDateTime <= DateTimeOffset.UtcNow` MUST be rejected with HTTP 409 Conflict (`window_closed`).
- **FR-004**: Enforce the CHANGE-001 Same-Event Exclusivity Invariant: if Candidate $U$ is the active Chief Leader on Event $E$, any direct assignment attempt targeting $U$ on Event $E$ MUST be rejected with HTTP 409 Conflict (`chief_leader_conflict`).
- **FR-005**: Direct assignment MUST immediately create a `TeamAssignment` in `staffing.TeamAssignments` with `Status = TeamAssignmentStatus.Active` (Approved Blocker 1 & Decision Group 5). No intermediate invited or pending confirmation states may be introduced.
- **FR-006**: Candidates eligible for direct assignment MUST hold the `Leader` role in `identity.UserRoles`.
- **FR-007**: Direct assignment candidate query MUST return candidates who either marked `NotAvailable` or have no availability record (`NoResponse`) in the current active `StaffingCycle` (`IsCurrent == true`), excluding the Chief Leader and already-active team members.
- **FR-008**: Submitting an identical direct assignment request for a candidate who is already active on the team roster MUST return HTTP 200 OK with `Idempotent-Replay: true` header without creating duplicate audit records or duplicate notifications.
- **FR-009**: Every direct assignment MUST execute within a single atomic SQL Server transaction that inserts `staffing.TeamAssignments`, inserts `notifications.InPlatformNotifications` (`DirectLeaderAssigned`), and appends an immutable audit log in `audit.AuditLogs` (`Action = 'DirectLeaderAssignmentExecuted'`, `ResourceType = 'TeamAssignment'`).
- **FR-010**: All mutating requests MUST require an antiforgery token (`X-XSRF-TOKEN` header and cookie).
- **FR-011**: The system MUST expose a query endpoint `GET /api/events/{eventId}/direct-assignment/candidates` returning eligible candidates with their user ID, username, and availability state (`NotAvailable` with timestamp, or `NoResponse`).
- **FR-012**: The system MUST expose a mutation endpoint `POST /api/events/{eventId}/direct-assignment` accepting candidate user ID.
- **FR-013**: The client UI MUST display the Direct Draft candidate list with availability status badges (`Not Available` / `Response Needed`).
- **FR-014**: The client UI MUST provide accessible "Direct Assign" actions per candidate, disabling controls while a submission is in flight.
- **FR-015**: The client UI MUST support mobile viewports down to 320px width with minimum 44x44px touch targets, zero horizontal overflow, and `aria-live="polite"` announcements for roster additions.

---

## 4. Success Criteria

- **SC-001**: An active Chief Leader or Admin can view eligible leaders who have not responded or marked `NotAvailable` on their published event.
- **SC-002**: Directly assigning a candidate immediately places them into `Active` status on the team roster, records an audit log noting their prior availability state, and creates an in-platform notification.
- **SC-003**: The CHANGE-001 exclusivity invariant prevents the active Chief Leader from being directly assigned on their led event.
- **SC-004**: Idempotent replays safely return 200 OK with `Idempotent-Replay: true`. Past-deadline requests return 409 `window_closed`.
- **SC-005**: 100% of domain unit tests, SQL Server 2025 container integration tests, Angular unit tests, Playwright E2E/a11y tests, and k6 performance benchmarks pass with zero errors.
