# Feature Specification: SLICE-04 — Chief Leader In-Platform Response

**Feature Branch**: `codex/003-chief-leader-direct-response`

**Created**: 2026-09-22

**Status**: Clarified — ready for planning

**Input**: SLICE-04 in the locked V1 roadmap, grounded in the ratified Chief Leader assignment lifecycle, role matrix, CHANGE-001, and the accepted audit and notification decisions.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Review My Pending Appointment (Priority: P1)

An appointed Chief Leader signs in and sees enough information about their own Pending Event appointment to decide whether to accept or decline. A pending appointment is visible to its candidate even though the Event is still Draft and unavailable as a published Event to ordinary Leaders.

**Why this priority**: The candidate must understand the appointment before making an operational commitment.

**Independent Test**: Sign in as the appointed candidate, open the pending appointment, and verify the Event context and two response choices; sign in as another user and verify the appointment is inaccessible.

**Acceptance Scenarios**:

1. **Given** a Chief Leader has a Pending appointment, **when** that person opens their own appointment, **then** the Event title, schedule, location, Pending status, and Accept and Decline choices are available.
2. **Given** another Chief Leader, an ordinary Leader, or an unauthenticated actor, **when** they attempt to view or respond to the appointment, **then** access is denied without revealing protected appointment details or changing state.
3. **Given** an Event is still Draft, **when** its appointed candidate reviews the appointment, **then** the candidate can see this assigned context while ordinary Leaders still cannot see the Event as published or available.

---

### User Story 2 - Accept My Appointment (Priority: P1)

The appointed Chief Leader explicitly accepts a Pending appointment in Carpediem. The assignment becomes Active, the Chief Leader gains operational ownership of that Event, and the Event remains Draft until a later Admin publication action.

**Why this priority**: An accepted Active assignment is the approved prerequisite for later publication.

**Independent Test**: Accept a Pending appointment and verify its Active state, direct actor attribution, unchanged Draft Event, and the absence of a second response action.

**Acceptance Scenarios**:

1. **Given** the appointed Chief Leader has a Pending assignment and no conflicting active team assignment on that Event, **when** they explicitly accept it, **then** that assignment becomes Active, its acceptance time and direct source are recorded, and the Event remains Draft.
2. **Given** a dual-role candidate has an Active team assignment on the same Event's current staffing cycle, **when** they try to accept, **then** activation is rejected until that team assignment is resolved; the Chief Leader assignment remains Pending.
3. **Given** an accepted assignment, **when** the same response is repeated after an uncertain outcome, **then** no second transition or audit entry is created and the candidate can determine that the assignment is Active.

---

### User Story 3 - Decline My Appointment (Priority: P1)

The appointed Chief Leader explicitly declines a Pending appointment. The assignment becomes Declined and remains in history; the Event remains Draft without an Active Chief Leader. Operations Admins receive an in-platform alert so a later Admin appointment can proceed.

**Why this priority**: Decline closes the candidate's pending decision while preserving the business record and notifying the people who can act next.

**Independent Test**: Decline a Pending appointment and verify historical preservation, direct audit attribution, a persistent Admin alert, unchanged Draft Event, and no further candidate response action.

**Acceptance Scenarios**:

1. **Given** the appointed Chief Leader has a Pending assignment, **when** they explicitly decline it, **then** that same record becomes Declined with a decline time, the Event remains Draft, and no Active Chief Leader is created.
2. **Given** a successful decline, **when** the result is committed, **then** a direct `ChiefLeaderDeclined` audit entry and a persistent in-platform alert for active Operations Admins are recorded consistently with the state change.
3. **Given** a declined assignment, **when** the candidate or an Admin later reviews it, **then** the historical record remains identifiable; this feature does not appoint a replacement.

---

### User Story 4 - Resolve Invalid or Uncertain Responses (Priority: P2)

The candidate receives a safe, clear outcome when the appointment is missing, no longer Pending, was changed concurrently, or cannot be saved. They can inspect the current state after a recoverable failure without accidentally making a contradictory response.

**Why this priority**: A leadership response changes operational authority and must never be inferred from an uncertain screen state.

**Independent Test**: Exercise unknown, unauthorized, stale, repeated, conflicting, and failed responses and verify that only one valid decision persists with a matching audit result.

**Acceptance Scenarios**:

1. **Given** an unknown appointment, **when** the candidate opens or responds to it, **then** a safe not-found outcome is shown and no state changes.
2. **Given** an appointment that is already Active or Declined, **when** a new or opposite response is attempted, **then** no contradictory transition occurs and the current state is shown or can be reloaded.
3. **Given** two concurrent responses to the same Pending appointment, **when** they race, **then** only one outcome is committed and the other receives a clear current-state or conflict outcome.
4. **Given** a recoverable network or system failure after submission, **when** the candidate resumes, **then** the current assignment can be checked and a retry cannot create a second transition or audit entry.

### Edge Cases

- An expired or deactivated candidate account cannot respond, even if the appointment was previously visible.
- Acceptance and decline are mutually exclusive decisions on one Pending record. A response does not create a new assignment.
- A Pending appointment alone grants no operational Event ownership.
- If same-Event active team participation conflicts with acceptance, the candidate can still decline.
- A response that fails to commit its required audit or Admin alert is not presented as completed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let an authenticated, active Chief Leader view their own Pending appointment and the Event title, scheduled time, and location needed to respond while the Event remains Draft.
- **FR-002**: The system MUST restrict appointment review and direct response to the appointed candidate; Chief Leader role membership alone MUST NOT grant access to another person's appointment.
- **FR-003**: An appointed Chief Leader's Draft Event context MUST NOT make that Event available as a published Event to ordinary Leaders.
- **FR-004**: The system MUST present explicit Accept and Decline actions for a Pending appointment and MUST NOT infer acceptance from viewing, opening a notification, or navigation.
- **FR-005**: Only a Pending assignment for a Draft Event MAY be directly accepted or declined; unknown, Active, Declined, or otherwise ineligible targets MUST NOT transition.
- **FR-006**: A valid direct acceptance MUST transition the existing Pending assignment to Active and record the accepting candidate, acceptance time, and direct acceptance source.
- **FR-007**: Direct acceptance MUST grant Event-scoped operational Chief Leader ownership and satisfy the assignment part of the later publication prerequisite while leaving the Event in Draft.
- **FR-008**: Before activation, the system MUST verify that the candidate holds no Active team assignment on the same Event's current staffing cycle; a conflict MUST leave the Chief Leader assignment Pending.
- **FR-009**: A valid direct decline MUST transition the existing Pending assignment to Declined, record decline time, preserve the record as history, and leave the Event in Draft without an Active Chief Leader.
- **FR-010**: A successful direct acceptance MUST create one immutable `ChiefLeaderAccepted` business audit entry attributed to the candidate and distinguished as a direct response.
- **FR-011**: A successful direct decline MUST create one immutable `ChiefLeaderDeclined` business audit entry attributed to the candidate and distinguished as a direct response; no reason is mandatory.
- **FR-012**: A successful decline MUST create a persistent in-platform alert for active Operations Admins so they can see that the Event needs another Chief Leader appointment.
- **FR-013**: Each response state change and its required audit entry, plus the required Admin alert on decline, MUST form one consistent business result; a failed required part MUST leave the prior Pending state intact.
- **FR-014**: Repeated or concurrent responses MUST NOT create duplicate audit entries, duplicate decline alerts, or contradictory final states; the candidate MUST be able to determine the committed outcome after uncertainty.
- **FR-015**: The system MUST reject direct response by other users, inactive users, and unauthenticated users, including through manipulated client requests.
- **FR-016**: The system MUST give safe, distinguishable not-found, authorization, invalid-state, conflict, and recoverable failure outcomes without exposing sensitive implementation details.
- **FR-017**: The response flow MUST show clear loading, decision, saving, success, and failure states and MUST prevent accidental overlapping submissions while a decision is unresolved.
- **FR-018**: On a recoverable failure, the candidate MUST be able to inspect the current assignment and retry safely where appropriate without losing the relevant Event context.
- **FR-019**: The response flow MUST be usable at the project's smallest supported mobile viewport without normal-workflow horizontal scrolling, with visible primary actions, touch targets at least 44 by 44 CSS pixels, and no hover-only critical action.
- **FR-020**: The response flow MUST meet WCAG 2.2 AA expectations for semantic controls, associated labels, keyboard use, visible focus, logical order, accessible status and error messaging, sufficient contrast, non-color-only meaning, text scaling, and reduced-motion preferences.
- **FR-021**: This feature MUST NOT provide Admin-recorded external acceptance, Event publication, Chief Leader replacement, team staffing, notification inbox management, read/dismiss controls, or external notification channels.

### Key Entities

- **ChiefLeaderAssignment**: The existing appointment record with Pending, Active, and Declined states; direct response changes this record once and preserves a decline as history.
- **Event**: The scheduled Draft Event associated with the appointment; response does not change its lifecycle state.
- **Audit Record**: Immutable evidence of direct acceptance or decline with candidate actor, assignment, Event, time, and source.
- **In-Platform Admin Alert**: Persistent notification of a decline for active Operations Admins; inbox interaction belongs to a later feature.

### Scope and Decision Classification

- **Already approved / known**: The appointed Chief Leader responds directly; Pending transitions to Active or Declined; acceptance grants Event-scoped ownership; decline preserves history and alerts Admins; direct responses are audited; CHANGE-001 same-Event exclusivity applies at activation.
- **Irrelevant to SLICE-04**: Admin external confirmation, publication, later team placement and attendance, post-publication replacement, full inbox behavior, and external delivery.
- **Requires clarification before planning**: None. The later roadmap expressly includes the decline alert, resolving its older candidate classification in ADR-006 for this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In every tested valid Pending appointment, the appointed Chief Leader can review the Event context and choose exactly one explicit response.
- **SC-002**: Every tested direct acceptance produces one Active assignment and one direct audit entry while the Event remains Draft.
- **SC-003**: Every tested direct decline preserves one Declined historical assignment, creates one direct audit entry and the approved Admin alert, and leaves no Active Chief Leader for that Event.
- **SC-004**: Every tested unauthorized, inactive, invalid-state, same-Event team-conflict, stale, and concurrent response leaves no contradictory leadership state or partial business result.
- **SC-005**: The full review and response journey works at the smallest supported mobile viewport with no horizontal scrolling and with keyboard and screen-reader usable status feedback.
- **SC-006**: A candidate can determine the current assignment state after an uncertain submission without causing a duplicate transition or audit entry.

## Assumptions

- SLICE-03 supplies the existing Pending assignment, candidate identity, and authenticated session foundation.
- The Event remains Draft throughout this feature; publication is a separate Admin action.
- Decline alerts are persistent notification intents for active Admin recipients. Their inbox presentation and delivery timing belong to SLICE-17.
- A same-Event active team assignment can arise only after a later staffing capability exists, but the approved exclusivity rule remains a required activation guard.
