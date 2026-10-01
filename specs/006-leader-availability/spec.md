# Feature Specification: SLICE-07 — Leader Availability Declaration (Available / NotAvailable)

**Feature Branch**: `codex/006-leader-availability`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Leader Availability Declaration (Available / NotAvailable)"

---

## 1. Executive Summary & Business Intent

In the Carpediem volunteer staffing lifecycle, once an Event has met the publishing prerequisite invariant and transitioned to `Published` (`SLICE-06`), volunteer Leaders need to declare whether they can serve on the event.

`SLICE-07` enables authenticated users holding the `Leader` role to view a `Published` Event, inspect its scheduled Cairo window and venue, and submit their schedule availability as either `Available` or `NotAvailable`. Absence of a response represents `No AvailabilityResponse` (presented as "Response Needed" in the Leader UI). Leaders maintain self-service mutability to change their declaration freely at any point before the event start time.

Furthermore, `SLICE-07` enforces the **CHANGE-001 Same-Event Exclusivity Invariant**: a user who is the active Chief Leader for an event is strictly prohibited from declaring volunteer availability on that same event, while retaining full eligibility to declare availability on any other event where they are not the active Chief Leader.

---

## 2. User Scenarios & Testing *(mandatory)*

### User Story 1 - Declare Availability for a Published Event (Priority: P1) 🎯 MVP

As an authenticated Volunteer Leader,
I want to view a published event and declare whether I am `Available` or `Not Available`,
So that the Chief Leader knows I am available for selection onto the event team roster.

**Why this priority**: Without availability declarations, the Chief Leader cannot view an available candidate pool or select team members (`SLICE-08`).

**Independent Test**:
1. Log in as an authenticated Leader.
2. View a `Published` Event with no prior response; verify status is "Response Needed".
3. Submit "Mark Available" and verify status updates immediately to "Available" with confirmed timestamp.
4. Verify an `AvailabilityResponse` record is persisted in `staffing.AvailabilityResponses` and an audit entry is created.

**Acceptance Scenarios**:
1. **Given** a `Published` Event in the future and a Leader who has not yet responded, **when** the Leader navigates to the event availability view, **then** the interface clearly indicates "Response Needed" and presents "Mark Available" and "Mark Not Available" actions.
2. **Given** a Leader viewing an event in "Response Needed" state, **when** they click "Mark Available", **then** the server creates an `AvailabilityResponse` with `Response = 'Available'` and returns HTTP 201 Created with the saved record and rowversion.
3. **Given** a Leader viewing an event in "Response Needed" state, **when** they click "Mark Not Available", **then** the server creates an `AvailabilityResponse` with `Response = 'NotAvailable'` and returns HTTP 201 Created.

---

### User Story 2 - Self-Service Mutability of Declared Availability (Priority: P1)

As a Volunteer Leader who previously declared availability for an upcoming event,
I want to update my declaration between `Available` and `Not Available` if my schedule changes,
So that leadership always has accurate, up-to-date volunteer availability prior to the event.

**Why this priority**: Volunteer schedules are volatile; locking availability upon first submission would force volunteers into attendance churn or no-shows.

**Independent Test**:
1. Declare `Available` on an event.
2. Reload or return to the view; change declaration to `Not Available` with the valid `If-Match` rowversion.
3. Verify the existing `AvailabilityResponse` record is updated in place, a new rowversion is issued, and an updated audit record is appended.
4. Attempt to change availability after the event's `StartDateTime` has passed; verify the server strictly rejects the request with HTTP 409 Conflict (`window_closed`).

**Acceptance Scenarios**:
1. **Given** an existing `Available` response on an upcoming `Published` Event, **when** the Leader selects "Change to Not Available", **then** the response is updated to `NotAvailable`, the UI displays the updated status badge, and optimistic concurrency is enforced via `If-Match`.
2. **Given** an existing `NotAvailable` response on an upcoming `Published` Event, **when** the Leader selects "Change to Available", **then** the response is updated to `Available`.
3. **Given** an Event whose `StartDateTime` has already arrived or passed in UTC, **when** a Leader attempts to create or update their availability declaration, **then** the request is rejected with HTTP 409 Conflict (`window_closed`) and an explanatory message.

---

### User Story 3 - Enforce CHANGE-001 Dual-Role Participation and Exclusivity (Priority: P2)

As a Chief Leader who also holds the volunteer Leader role,
I want to participate as a volunteer Leader on events led by others while being restricted from volunteer declaration on events I lead,
So that operational authority and volunteer roles never conflict on the same event.

**Why this priority**: CHANGE-001 ratified non-exclusive organizational roles but strict event-scoped operational exclusivity ($\text{Active Chief Leader}(E, U) \implies \neg\text{Volunteer Leader}(E, U)$).

**Independent Test**:
1. Log in as a dual-role user (holding both `ChiefLeader` and `Leader` roles).
2. For Event A (where the user has an `Active` `ChiefLeaderAssignment`): view the event; verify availability controls are absent/disabled with a notice: "You are the Chief Leader for this event". If an API POST is attempted, verify HTTP 409 Conflict (`chief_leader_conflict`).
3. For Event B (where the user is NOT the active Chief Leader): view the event; verify availability controls ("Mark Available", "Mark Not Available") are fully enabled and submissions succeed.

**Acceptance Scenarios**:
1. **Given** User $U$ is the active Chief Leader on Event $E$, **when** User $U$ attempts to submit availability on Event $E$, **then** the server rejects the request with HTTP 409 Conflict and error code `chief_leader_conflict`.
2. **Given** User $U$ holds the `ChiefLeader` role but is NOT the active Chief Leader on Event $E$, and holds the `Leader` role, **when** User $U$ submits availability on Event $E$, **then** the submission succeeds normally.
3. **Given** a user who does not hold the `Leader` role (e.g. an Admin without Leader role), **when** they attempt to submit availability, **then** the server rejects the request with HTTP 403 Forbidden.

---

### User Story 4 - Concurrency, Idempotency, and Lifecycle Integrity (Priority: P2)

As a Volunteer Leader or Administrator,
I want availability declarations to be resilient against concurrent edits, network retries, and invalid event states,
So that data consistency and audit trails are never corrupted.

**Why this priority**: Mobile devices frequently experience network drops, retries, and concurrent window closes.

**Independent Test**:
1. Replay an identical availability declaration with the same response value; verify HTTP 200 OK with `Idempotent-Replay: true` header without creating duplicate audit rows.
2. Submit an update with a stale `If-Match` rowversion; verify HTTP 409 Conflict (`stale_version`) with reload guidance.
3. Attempt to declare availability on an Event in `Draft`, `Completed`, or `Cancelled` status; verify HTTP 409 Conflict (`invalid_event_status`).

**Acceptance Scenarios**:
1. **Given** an existing `Available` response, **when** the Leader submits another `Available` request with matching intent, **then** the server returns HTTP 200 OK with `Idempotent-Replay: true` and the existing state.
2. **Given** an update submitted with an outdated rowversion, **when** processed, **then** the server returns HTTP 409 Conflict and leaves the persisted record unchanged.
3. **Given** an Event in `Draft`, `Completed`, or `Cancelled` state, **when** a Leader attempts to declare availability, **then** the server returns HTTP 409 Conflict explaining that availability is only open for `Published` events.

---

### User Story 5 - Mobile-First, Accessible Availability Interaction (Priority: P2)

As a Volunteer Leader using a mobile smartphone (320px–430px viewport),
I want an intuitive, thumb-friendly interface with clear status badges, polite screen reader updates, and zero layout shift,
So that I can declare or change my availability on the go in under 5 seconds.

**Why this priority**: Over 80% of volunteer schedule responses occur on mobile devices. Compliance with WCAG 2.2 Level AA and Carpediem Mobile-First design rules is mandatory.

**Independent Test**:
1. In Playwright across 320px, 360px, 390px, 430px viewports and desktop:
   - Run axe-core accessibility audit (0 violations).
   - Verify touch targets meet or exceed 44x44px.
   - Verify 200% font scaling reflow without horizontal scrolling.
   - Verify ARIA polite announcements upon availability update.

**Acceptance Scenarios**:
1. **Given** any viewport width from 320px upward, **when** viewing the availability card, **then** all actions fit within the screen without horizontal scrolling, and touch target heights are $\ge 44\text{px}$.
2. **Given** assistive technology (screen reader), **when** availability is declared or toggled, **then** the result is announced via an `aria-live="polite"` region.
3. **Given** reduced-motion preferences, **when** interacting with buttons and banners, **then** animations are disabled or instantaneous.

---

## 3. Edge Cases & Boundary Conditions

- **Event Material Reschedule**: When an event's date is rescheduled in `SLICE-12`, its active `StaffingCycle` is marked historical (`IsCurrent = false`) and a new cycle is created. In `SLICE-07`, all queries and mutations strictly target the event's current active cycle (`IsCurrent == true`). Responses in older cycles remain preserved as historical records.
- **Immediate Start Window Cutoff**: If a Leader opens the page 30 seconds before `StartDateTime` and submits 1 second after `StartDateTime`, the server strictly evaluates the UTC timestamp against `Event.StartDateTime` and returns HTTP 409 Conflict (`window_closed`).
- **Rapid Double-Tap / In-Flight Submission**: The client disables submit actions immediately upon click to prevent double dispatch. The database unique constraint `UX_AvailabilityResponses_StaffingCycle_User` guarantees database-level uniqueness.
- **Multiple Events Concurrent Availability**: A Leader MAY declare `Available` for multiple overlapping events in V1. Final placement exclusivity is enforced during team selection (`SLICE-08`) and direct assignment (`SLICE-09`), not during initial availability declaration.

---

## 4. Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST permit only authenticated users holding the `Leader` role to submit or update availability declarations on events (`RequireLeader` policy).
- **FR-002**: The system MUST permit availability declaration ONLY on Events with `Status == EventStatus.Published`. Requests on `Draft`, `Completed`, or `Cancelled` events MUST be rejected with HTTP 409 Conflict (`invalid_event_status`).
- **FR-003**: The system MUST permit availability declaration ONLY while `Event.StartDateTime > DateTimeOffset.UtcNow`. Requests submitted when `Event.StartDateTime <= DateTimeOffset.UtcNow` MUST be rejected with HTTP 409 Conflict (`window_closed`).
- **FR-004**: The system MUST strictly enforce the CHANGE-001 Same-Event Exclusivity Invariant: if the authenticated user has an `Active` `ChiefLeaderAssignment` on Event $E$, the system MUST reject any availability declaration on Event $E$ with HTTP 409 Conflict (`chief_leader_conflict`).
- **FR-005**: An availability declaration MUST associate the record with the Event's current active `StaffingCycle` (`IsCurrent == true`).
- **FR-006**: The system MUST support two valid declared states: `Available` and `NotAvailable`. The absence of an `AvailabilityResponse` row in the current `StaffingCycle` represents `No AvailabilityResponse` ("Response Needed").
- **FR-007**: A Leader MUST be able to change their declaration freely between `Available` and `NotAvailable` prior to the event start time.
- **FR-008**: The system MUST enforce optimistic concurrency:
  - When creating a new declaration where none existed, the client sends `If-None-Match: *` or omits `If-Match`.
  - When updating an existing declaration, the client MUST provide the known rowversion via the `If-Match` header. Mismatches MUST be rejected with HTTP 409 Conflict (`stale_version`).
- **FR-009**: The availability endpoint MUST be idempotent: submitting an identical response value with matching intent MUST return HTTP 200 OK with the `Idempotent-Replay: true` header without creating duplicate audit records.
- **FR-010**: Every availability creation or update MUST execute within a single atomic SQL transaction that updates `staffing.AvailabilityResponses` and appends an immutable audit log record in `audit.AuditLogs` (`Action = 'AvailabilityDeclared'`, `ResourceType = 'AvailabilityResponse'`).
- **FR-011**: All mutation requests MUST require an antiforgery token (`X-XSRF-TOKEN` header and cookie).
- **FR-012**: The system MUST expose a query endpoint `GET /api/events/{eventId}/availability/me` allowing the authenticated Leader to retrieve their current declared availability, rowversion, and event availability window status.
- **FR-013**: The client UI MUST render the current availability status badge ("Available", "Not Available", or "Response Needed") with high visual clarity.
- **FR-014**: The client UI MUST provide distinct, accessible actions to "Mark Available" and "Mark Not Available", disabling inputs while a submission is in flight.
- **FR-015**: The client UI MUST support viewports down to 320px width with minimum 44x44px touch targets, zero horizontal scrolling, and accessible live region announcements for state changes.

---

### Non-Functional & Architecture Requirements

- **NFR-001**: API endpoints for availability declaration and retrieval must respond with $P_{95} < 300\text{ms}$ under 100 concurrent requests.
- **NFR-002**: Database operations must execute against Microsoft SQL Server 2025 under Read Committed Snapshot Isolation (RCSI) with atomic transactions.
- **NFR-003**: The user interface must achieve WCAG 2.2 Level AA compliance, verified by automated axe-core audits.
- **NFR-004**: No external message brokers, distributed caches, or background queues may be introduced. Native SQL Server relational storage per ADR-006 and ADR-009 is the ratified architecture.

---

## 5. Success Criteria *(mandatory)*

- **SC-001**: An authenticated Leader can view an upcoming published event, declare `Available` or `Not Available`, and see their status updated in the UI and persisted in the database.
- **SC-002**: A Leader can toggle their availability between `Available` and `Not Available` prior to event start, with optimistic concurrency protecting against lost updates.
- **SC-003**: An active Chief Leader on an event is blocked from declaring availability on that event with a clear error, while being permitted on other events (CHANGE-001 verified).
- **SC-004**: Submissions on non-published or past events are strictly rejected with HTTP 409 Conflict.
- **SC-005**: 100% of domain unit tests, SQL Server 2025 container integration tests, Angular unit tests, Playwright E2E/a11y tests, and k6 performance benchmarks pass with zero errors.
