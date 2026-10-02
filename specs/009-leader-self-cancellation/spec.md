# Feature Specification: SLICE-10 — Leader Self-Cancellation with Mandatory Justification

**Feature Branch**: `codex/009-leader-self-cancellation`  
**Created**: 2026-10-02  
**Status**: Draft  
**Input**: User request: "Leader Self-Cancellation with Mandatory Justification per Decision Group 4 and Milestone M2 (Volunteer Churn & Replacement)"  

---

## 1. Executive Summary & Business Intent

In event staffing operations, unforeseen personal, health, or schedule conflicts inevitably arise after a volunteer leader has been assigned to a team roster. When an actively staffed leader discovers they cannot fulfill their service commitment, they must have a controlled, self-service mechanism to cancel their placement in advance of the event.

Per **Decision Group 4** and **Milestone M2** (ratified in `docs/requirements/LIFECYCLE_SPECIFICATION.md` Sections 5 & 7, `docs/roadmap/V1_FEATURE_SLICES.md` Item SLICE-10, and `docs/architecture/decisions/ADR-003-authorization-resource-policies.md`):
- An actively staffed volunteer leader possesses the authority to self-cancel their active `TeamAssignment` via a controlled UI modal requiring explicit confirmation and a mandatory free-text justification reason.
- An Operations Administrator possesses administrative override authority to cancel an active `TeamAssignment` with a mandatory administrative justification reason.
- The assignment immediately transitions from `Active` to `Cancelled`. The record is **never deleted** and **never reactivated**; it remains immutably preserved to protect factual participation history per `SLICE-18` (raw counts of assignments, attendance, and cancellations without synthetic scoring).
- The cancellation vacates the team position, reopening the slot for replacement leader assignment in `SLICE-11`.
- The system alerts the event's active Chief Leader and Operations Administrators via in-platform notifications (`TeamMemberCancelled`) so they can initiate replacement drafting.
- The system logs an immutable audit trail entry (`Action = 'VolunteerSelfCancelled'`) recording the actor, resource ID, event ID, justification reason, and previous status (`Active`).
- Cancellation is permitted strictly while the event remains `Published` and before the event commences (`Event.StartDateTime > UtcNow`). Once an event begins, self-cancellation is closed.
- Replacement leader drafting is strictly deferred to `SLICE-11`.

---

## 2. User Scenarios & Testing

### User Story 1 - View Assignment & Cancellation Context (Priority: P1) 🎯 MVP

As an actively staffed Volunteer Leader (or Operations Administrator),  
I want to view my assignment details for a published event, including event schedule, location, assignment status, and cancellation eligibility,  
So that I understand my current commitment and whether I am eligible to self-cancel.

**Independent Test**:
1. Log in as a volunteer Leader assigned to a `Published` event whose start time is in the future.
2. Navigate to the assignment view (`GET /api/events/{eventId}/my-assignment` or assignment context).
3. Verify HTTP 200 OK returning event title, schedule, location, assignment status `Active`, `isWindowOpen: true`, and opaque version token.
4. Verify non-assigned users receive 404 Not Found or 403 Forbidden.

**Acceptance Scenarios**:
1. **Given** an actively assigned Leader $L$ on `Published` Event $E$, **when** $L$ views their assignment, **then** the response displays status `Active` with the option to cancel.
2. **Given** Event $E$ whose `StartDateTime <= UtcNow`, **when** the assignment is viewed, **then** `isWindowOpen` is `false` and cancellation is indicated as closed.
3. **Given** an assignment already `Cancelled`, **when** viewed, **then** the response displays status `Cancelled`, the cancellation timestamp, and the cancellation reason.

---

### User Story 2 - Self-Cancel Assignment with Mandatory Justification (Priority: P1) 🎯 MVP

As an actively assigned Volunteer Leader,  
I want to self-cancel my team assignment by submitting an explicit confirmation modal with a mandatory justification reason,  
So that I am formally released from the roster and the leadership team is immediately notified to draft a replacement.

**Independent Test**:
1. Open the cancellation confirmation modal on an active assignment.
2. Enter a valid non-empty justification (e.g. "Family emergency conflict").
3. Submit cancellation request (`POST /api/events/{eventId}/assignments/{assignmentId}/cancel`).
4. Verify HTTP 200 OK returning `TeamAssignmentResponse` with `Status = 'Cancelled'`, `CancellationReason`, and `CancelledAt`.
5. Verify in-platform notifications dispatched to active Chief Leader and active Administrators.
6. Verify an audit record is appended with `Action = 'VolunteerSelfCancelled'`, `MandatoryReason`, and `Source = 'VolunteerSelfCancellation'`.
7. Verify the leader no longer appears in the active roster count.

**Acceptance Scenarios**:
1. **Given** an `Active` assignment for Leader $L$ on `Published` Event $E$, **when** $L$ submits cancellation with a valid non-empty reason, **then** the status transitions immediately to `Cancelled`.
2. **Given** a cancellation submission with empty, null, or whitespace-only justification, **when** submitted, **then** the server rejects the request with HTTP 400 Bad Request (`invalid_justification`).
3. **Given** a cancellation submission with justification exceeding 500 characters, **when** submitted, **then** the server rejects with HTTP 400 Bad Request (`invalid_justification`).

---

### User Story 3 - Administrator Override Cancellation with Mandatory Justification (Priority: P1) 🎯 MVP

As an Operations Administrator,  
I want to cancel an active team member's assignment on their behalf when necessary, providing a mandatory administrative reason,  
So that the roster reflects operational realities when a volunteer cannot access the platform.

**Independent Test**:
1. Log in as an Operations Administrator.
2. Submit cancellation for an active volunteer assignment providing an administrative reason (`POST /api/events/{eventId}/assignments/{assignmentId}/cancel`).
3. Verify HTTP 200 OK with `Status = 'Cancelled'`.
4. Verify audit log captures `Action = 'VolunteerSelfCancelled'`, `Source = 'AdminOverrideCancellation'`, and the actor is the Admin's user ID.
5. Verify notifications dispatched to the Chief Leader and Admins.

**Acceptance Scenarios**:
1. **Given** an `Active` assignment on `Published` Event $E$, **when** an Administrator submits cancellation with a valid reason, **then** the assignment transitions to `Cancelled` with admin audit attribution.

---

### User Story 4 - Concurrency, Idempotency, Window-Closed & Authorization Denials (Priority: P2)

As the platform and operations team,  
I want the cancellation endpoint to enforce strict temporal boundaries, reject unauthorized callers, safely handle concurrent modifications, and replay idempotent requests cleanly,  
So that race conditions and unauthorized tampering are prevented.

**Independent Test**:
1. Attempt cancellation by a user who is neither the assignment owner nor an Administrator; verify HTTP 403 Forbidden (`unauthorized_assignment_owner`).
2. Attempt cancellation after `Event.StartDateTime <= UtcNow`; verify HTTP 409 Conflict (`window_closed`).
3. Attempt cancellation on an Event whose status is `Draft`, `Completed`, or `Cancelled`; verify HTTP 409 Conflict (`invalid_event_status`).
4. Replay an identical cancellation request with the same justification for an already cancelled assignment; verify HTTP 200 OK with `Idempotent-Replay: true` header without creating duplicate notifications or audit logs.
5. Attempt cancellation with a mismatched `If-Match` rowversion against an active assignment; verify HTTP 412 Precondition Failed / 409 Conflict (`concurrency_conflict`).

**Acceptance Scenarios**:
1. **Given** User $U$ is not the assigned leader and lacks Admin role, **when** $U$ attempts cancellation, **then** server returns 403 Forbidden (`unauthorized_assignment_owner`).
2. **Given** `Event.StartDateTime <= UtcNow`, **when** cancellation is attempted, **then** server returns 409 Conflict (`window_closed`).
3. **Given** an assignment already cancelled with identical justification, **when** re-submitted, **then** server returns 200 OK with `Idempotent-Replay: true`.

---

### User Story 5 - Mobile-First, Accessible Confirmation Modal UI (Priority: P2)

As a Volunteer Leader using a mobile device,  
I want a responsive, accessible confirmation dialog that requires explicit confirmation, validates my justification text, traps focus, supports keyboard dismissal, and provides live screen reader updates,  
So that I do not accidentally cancel and can easily complete the process on any screen.

**Independent Test**:
1. In Playwright across 320px–desktop viewports:
   - Run axe-core accessibility audit (0 violations).
   - Verify modal dialog semantics (`role="dialog"`, `aria-modal="true"`, `aria-labelledby`).
   - Verify keyboard interaction: focus trapped within modal, Escape key dismisses without cancelling.
   - Verify minimum 44x44px touch targets on buttons.
   - Verify character counter for justification textarea (max 500 characters).
   - Verify 200% font zoom reflow without horizontal overflow.
   - Verify polite screen reader announcement upon successful cancellation.

---

## 3. Functional Requirements

- **FR-001**: Restrict assignment cancellation mutations (`POST /api/events/{eventId}/assignments/{assignmentId}/cancel`) to the authenticated volunteer leader owning the assignment (`UserId == actorUserId`) OR users holding the `Admin` role (`OwnResourceRequirement` or `Admin`).
- **FR-002**: Unauthorized users attempting cancellation MUST receive HTTP 403 Forbidden (`unauthorized_assignment_owner`).
- **FR-003**: Cancellation MUST be permitted exclusively on Events with `Status == EventStatus.Published`. Requests on `Draft`, `Completed`, or `Cancelled` events MUST be rejected with HTTP 409 Conflict (`invalid_event_status`).
- **FR-004**: Cancellation MUST be permitted exclusively while `Event.StartDateTime > DateTimeOffset.UtcNow`. Requests submitted when `Event.StartDateTime <= DateTimeOffset.UtcNow` MUST be rejected with HTTP 409 Conflict (`window_closed`).
- **FR-005**: Self-cancellation MUST require a non-empty, non-whitespace free-text justification reason between 1 and 500 characters. Submissions with null, empty, or whitespace reasons, or reasons exceeding 500 characters, MUST be rejected with HTTP 400 Bad Request (`invalid_justification`).
- **FR-006**: Upon successful cancellation, `TeamAssignment` status MUST transition permanently from `TeamAssignmentStatus.Active` to `TeamAssignmentStatus.Cancelled`, recording `CancellationReason = justification.Trim()` and `CancelledAt = DateTimeOffset.UtcNow`.
- **FR-007**: Assignment records MUST NEVER be deleted or reactivated; historical records remain immutably preserved for participation tracking (SLICE-18).
- **FR-008**: Submitting an identical cancellation request for an assignment that is already `Cancelled` with the exact same trimmed justification MUST return HTTP 200 OK with `Idempotent-Replay: true` header without creating duplicate notifications or duplicate audit logs.
- **FR-009**: If an `If-Match` header is supplied and does not match the assignment's current `RowVersion` (and the assignment is not already cancelled with identical justification), the request MUST be rejected with HTTP 412 Precondition Failed or HTTP 409 Conflict (`concurrency_conflict`).
- **FR-010**: Every successful cancellation MUST execute within a single atomic SQL Server transaction that updates `staffing.TeamAssignments`, inserts `notifications.InPlatformNotifications` (`TeamMemberCancelled`) for the active Chief Leader and all active Admins, and appends an immutable audit log in `audit.AuditLogs` (`Action = 'VolunteerSelfCancelled'`, `ResourceType = 'TeamAssignment'`).
- **FR-011**: All mutating requests MUST require an antiforgery token (`X-XSRF-TOKEN` header and cookie).
- **FR-012**: The system MUST expose a query endpoint `GET /api/events/{eventId}/my-assignment` returning the caller's assignment on Event $E$, including event metadata, assignment status, timestamps, cancellation details, `isWindowOpen`, and opaque version token.
- **FR-013**: The client UI MUST present a controlled cancellation confirmation modal requiring explicit confirmation and mandatory justification before triggering the cancellation API.
- **FR-014**: The cancellation modal MUST comply with accessibility guidelines: `role="dialog"`, `aria-modal="true"`, focus trapping, Escape key dismissal, and visible character counter.
- **FR-015**: The client UI MUST support mobile viewports down to 320px width with minimum 44x44px touch targets, zero horizontal overflow, and `aria-live="polite"` announcements upon cancellation.
- **FR-016**: Replacement drafting (`SLICE-11`) is strictly deferred. No penalties, reliability scores, or synthetic metrics may be generated.

---

## 4. Success Criteria

- **SC-001**: An actively assigned Leader can successfully self-cancel their assignment with a mandatory reason on a published event before start time.
- **SC-002**: The assignment status permanently updates to `Cancelled`, an immutable audit log is appended with actor attribution and justification reason, and notification intents are created for the Chief Leader and Admins.
- **SC-003**: Non-owners and non-admins receive 403 Forbidden; past-start requests receive 409 `window_closed`; empty reasons receive 400 `invalid_justification`.
- **SC-004**: Idempotent replays safely return 200 OK with `Idempotent-Replay: true` header.
- **SC-005**: 100% of domain unit tests, SQL Server 2025 container integration tests, Angular unit tests, Playwright E2E/a11y tests, and k6 performance benchmarks pass with zero errors.
