# Feature Specification: SLICE-06 — Event Publication & Publishing Prerequisite Invariant

**Feature Branch**: `codex/005-event-publication`

**Created**: 2026-10-01

**Status**: Draft — ready for review

**Input**: SLICE-06 in the locked V1 roadmap, grounded in the ratified Event lifecycle (`Draft` → `Published`), Chief Leader assignment prerequisite invariant, role permission matrix, ADR-002, ADR-003, ADR-005, and ADR-006.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Review Publishing Readiness of a Draft Event (Priority: P1) 🎯 MVP

An Operations Admin views an existing Draft Event to verify its readiness for publication. The Admin sees whether the mandatory publishing prerequisite is satisfied (specifically, exactly one accepted `Active` Chief Leader assignment and a valid future or current schedule window) and receives unambiguous guidance on what is missing if the Event is not yet eligible to be published.

**Why this priority**: The Admin must know whether an Event meets the strict invariant before attempting to publish, preventing user confusion, operational mistakes, and premature volunteer broadcasts.

**Independent Test**: Sign in as an active Operations Admin, view a Draft Event with:
1. No Chief Leader appointment,
2. A Pending Chief Leader appointment,
3. A Declined Chief Leader appointment, and
4. An Active Chief Leader assignment.
Verify that only case (4) presents an enabled, actionable publication pathway, while cases (1)–(3) explicitly explain that an accepted Chief Leader is required.

**Acceptance Scenarios**:

1. **Given** a Draft Event has an `Active` Chief Leader assignment and valid schedule fields (`StartTime < EndTime`), **when** an active Operations Admin reviews the Draft Event, **then** the publishing action is available with explicit confirmation context explaining that publication locks the draft and opens the event to volunteer Leaders.
2. **Given** a Draft Event has no Chief Leader, a `Pending` Chief Leader, or a `Declined` Chief Leader, **when** an Admin views the Draft Event, **then** the publishing action is disabled or withheld, and an informative explanation states that an accepted Chief Leader is required before publication.
3. **Given** an unauthenticated actor, ordinary Leader, or Chief Leader without Admin credentials, **when** they attempt to view or publish the Draft Event, **then** access is denied with proper HTTP 401/403 status without leaking private draft information.

---

### User Story 2 - Publish Event Enforcing Prerequisite Invariants (Priority: P1)

An active Operations Admin explicitly confirms and executes publication of an eligible Draft Event. The server strictly enforces the publishing prerequisite invariant: the Event must currently be in `Draft` status, possess exactly one accepted `Active` Chief Leader assignment, and have a valid schedule window. Upon successful execution in a single atomic transaction, the Event transitions to `Published`, an immutable audit log record (`EventPublished`) is committed, in-platform notification intents are created for eligible volunteer Leaders and the active Chief Leader, and the event becomes ready for volunteer availability declarations.

**Why this priority**: This is the core state transition that unlocks the volunteer recruitment phase (Milestone M1), connecting event planning with the volunteer community.

**Independent Test**: Submit a valid publish request for an eligible Draft Event with its opaque rowversion; verify the Event transitions to `Published`, an immutable audit record is appended, notification intents are staged, the response returns the published Event representation, and the Event is no longer editable via Draft endpoints.

**Acceptance Scenarios**:

1. **Given** an eligible Draft Event with an `Active` Chief Leader assignment and matching `If-Match` rowversion, **when** an authorized Admin publishes the Event, **then** the Event status transitions to `Published`, its updated rowversion and publication details are returned, and editing via draft routes is permanently closed.
2. **Given** a successful Event publication, **when** audit logs are inspected, **then** exactly one immutable audit record is stored with action `EventPublished`, resource type `Event`, the Event ID, the acting Admin ID, UTC timestamp, previous value `Draft`, and new value `Published`.
3. **Given** a successful Event publication, **when** notification storage is inspected, **then** in-platform notification records are created for eligible Leaders informing them of the newly published event.
4. **Given** an invalid or ineligible Draft Event (e.g. absent Chief Leader, Chief Leader still `Pending` or `Declined`), **when** an Admin attempts to publish, **then** the request is rejected with HTTP 422 Unprocessable Entity and an RFC 7807 problem detail identifying the missing active Chief Leader invariant violation, with zero state mutation.
5. **Given** an Event whose schedule window is invalid (`StartTime >= EndTime`), **when** publication is attempted, **then** the request is rejected with HTTP 422 Unprocessable Entity and no state changes.

---

### User Story 3 - Concurrency, Idempotency, and Stale State Handling (Priority: P2)

When multiple Admins manage the same Event, or when network instability causes uncertain outcomes or re-submissions, the system deterministically protects data integrity via rowversion checking and idempotent replays.

**Why this priority**: Network retries and concurrent admin tabs must never corrupt the Event lifecycle, create duplicate audit trails, or duplicate broadcast notifications.

**Independent Test**:
1. Submit a publish command with a stale rowversion and verify HTTP 409 / 412 Conflict/Precondition Failed.
2. Replay the identical publish command on an already-published Event and verify HTTP 200 OK with `Idempotent-Replay: true` header, returning the committed published state without extra audits or duplicate notifications.
3. Attempt publication on an already Cancelled or Completed Event and verify HTTP 409 Conflict.

**Acceptance Scenarios**:

1. **Given** an Event whose rowversion was incremented by another operation, **when** an Admin submits a publish request with the stale `If-Match` header, **then** the request fails with a concurrency conflict and leaves the Event untouched.
2. **Given** an Event that was already successfully published, **when** the same Admin retries the publish request, **then** the endpoint returns HTTP 200 with `Idempotent-Replay: true` and the published Event representation, creating no additional audit records or notifications.
3. **Given** an Event that has transitioned to `Cancelled` or `Completed`, **when** an Admin attempts publication, **then** the request is rejected with HTTP 409 Conflict explaining the invalid lifecycle state.

---

### User Story 4 - Mobile Confirmation and Accessible Interaction (Priority: P2)

The Admin on a mobile device (320px–430px viewport) can easily review publishing readiness, see the active Chief Leader confirmation status, trigger the publish action via a comfortable touch target (minimum 44x44px), receive a clear confirmation step to avoid accidental triggers, and observe polite screen-reader announcements upon success or failure without layout shifts.

**Why this priority**: Mobile operations must be resilient, accessible, and mistake-resistant, honoring the Carpediem Engineering Constitution (WCAG 2.2 AA, mobile-first).

**Independent Test**: In Playwright across mobile viewports (320px, 360px, 390px, 430px) and desktop:
1. Verify publish button accessibility, contrast, and minimum touch target size.
2. Open confirmation dialog/card, cancel it, and verify zero state mutation.
3. Confirm publication, verify polite status announcement, absence of horizontal overflow, and transition of the view to the published state.

**Acceptance Scenarios**:

1. **Given** a mobile viewport down to 320px width, **when** the Admin views the Draft Event, **then** all elements reflow vertically without horizontal scrolling, touch targets meet or exceed 44px, and typography scales up to 200% cleanly.
2. **Given** the Admin taps "Publish Event", **when** the confirmation prompt appears, **then** keyboard focus moves logically into the prompt, and dismissing or cancelling leaves the Event in Draft without network mutations.
3. **Given** the Admin confirms publication, **when** the response arrives, **then** an accessible status message announces the success, and the screen updates to reflect the `Published` state.

---

## Edge Cases

- **Race with Chief Leader Decline**: If a Chief Leader declines concurrently just before the Admin submits a publish request, the server detects that the assignment is no longer `Active` and rejects publication with an explicit conflict message.
- **Draft Fields Missing or Blank**: If title or location is somehow blank/whitespace-only, publication is rejected by invariant validation.
- **Start Time in the Past**: If the scheduled window's `StartTime` has already passed when publication is attempted, publication remains allowed if `StartTime < EndTime`, but if `EndTime <= Now`, the event cannot be published as an active upcoming event.
- **Repeated In-Flight Clicks**: The client disables the submit button immediately upon invocation and prevents duplicate requests while the first request is pending.
- **Transactional Rollback**: If notification generation or audit log writing fails, the entire database transaction rolls back, keeping the Event in `Draft`.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST restrict Event publication strictly to authenticated users holding the Operations Admin role (`RequireAdmin` policy).
- **FR-002**: The system MUST enforce the publishing prerequisite invariant: an Event MAY transition from `Draft` to `Published` IF AND ONLY IF:
  - The Event is currently in `Status == EventStatus.Draft`.
  - The Event has exactly one associated `ChiefLeaderAssignment` with `Status == ChiefLeaderAssignmentStatus.Active`.
  - The Event has a valid scheduled window where `StartDateTime < EndDateTime` and both are UTC.
  - Required fields (`Title`, `Location`) are non-empty and non-whitespace.
- **FR-003**: If the publishing prerequisite invariant is violated, the system MUST reject the transition with HTTP 422 Unprocessable Entity (or 409 Conflict if state-incompatible) and return a structured RFC 7807 `ProblemDetails` error specifying the exact unmet condition.
- **FR-004**: A successful publication MUST update the Event's persisted status to `EventStatus.Published` and generate a new rowversion.
- **FR-005**: A successful publication MUST atomically append exactly one immutable audit log record (`Action = 'EventPublished'`, `ResourceType = 'Event'`, `ResourceId = Event.Id`, `PreviousValue = 'Draft'`, `NewValue = 'Published'`) in the same database transaction.
- **FR-006**: A successful publication MUST atomically stage in-platform notification records (`NotificationType = 'EventPublished'`) for eligible Leaders in the organization within the same database transaction.
- **FR-007**: The publication endpoint MUST require an antiforgery token (`X-XSRF-TOKEN` header and cookie) for all mutation requests.
- **FR-008**: The publication endpoint MUST require an `If-Match` header containing the client's known rowversion and MUST reject requests with stale or mismatched rowversions with HTTP 409 Conflict.
- **FR-009**: The publication endpoint MUST be idempotent: retrying publication with the same rowversion/intent on an already-`Published` Event MUST return HTTP 200 OK with header `Idempotent-Replay: true` and the current published Event representation, without creating duplicate audit records or duplicate notifications.
- **FR-010**: Once an Event is `Published`, it MUST no longer be editable via the `PUT /api/events/drafts/{id}` endpoint (`NotDraftException` / HTTP 409 `not_draft`).
- **FR-011**: The client interface MUST display whether a Draft Event is eligible for publication, including Chief Leader acceptance status.
- **FR-012**: The client interface MUST require explicit confirmation before publishing to prevent accidental one-tap publication.
- **FR-013**: The client interface MUST support mobile viewports from 320px width upward with minimum 44x44px touch targets, zero horizontal scrolling, and accessible ARIA attributes.
- **FR-014**: The client interface MUST handle network errors and unexpected failures gracefully, preserving the user's view and providing clear recovery options.

---

### Non-Functional & Architecture Requirements

- **NFR-001**: API endpoints must respond with $P_{95} < 500\text{ms}$ under expected operational concurrency (~100–150 users).
- **NFR-002**: Database updates must execute under SQL Server 2025 Read Committed Snapshot Isolation (RCSI) with atomic transactions.
- **NFR-003**: The user interface must achieve WCAG 2.2 Level AA compliance, verified by automated axe-core accessibility checks.
- **NFR-004**: No external message broker, background queue (Hangfire/Quartz), or SignalR backplane may be introduced for publication notifications; standard relational persistence and polling per ADR-006 is the ratified pattern.

---

## Success Criteria *(mandatory)*

- **SC-001**: An Admin can publish an eligible Draft Event with an Active Chief Leader and verify the Event status becomes `Published`.
- **SC-002**: Attempting to publish an Event without an Active Chief Leader is strictly blocked with an informative invariant error, and the Event remains `Draft`.
- **SC-003**: Exactly one audit record (`EventPublished`) and leader notification intents are committed in a single atomic transaction.
- **SC-004**: Repeated publish submissions replay safely without duplicate state mutations, audit rows, or notifications.
- **SC-005**: 100% of domain tests, API integration tests with real SQL Server 2025 in container, Angular unit tests, and Playwright end-to-end accessibility tests pass with zero errors.
