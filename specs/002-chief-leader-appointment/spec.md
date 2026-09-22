# Feature Specification: SLICE-03 — Chief Leader Appointment & Notification

**Feature Branch**: `codex/002-chief-leader-appointment`

**Created**: 2026-09-22

**Status**: Clarified — ready for planning

**Input**: SLICE-03 from the locked V1 roadmap, grounded in the Carpediem constitution, lifecycle specification, role and permission matrix, CHANGE-001, ADR-003, ADR-005, ADR-006, and the approved relational constraints.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Appoint a Chief Leader to a Draft Event (Priority: P1)

An authenticated Operations Administrator opens an existing Draft Event, reviews the predefined Chief Leader pool, and appoints one candidate. The appointment becomes Pending while the Event remains Draft.

**Why this priority**: A pending appointment is the required starting point for the later acceptance workflow and eventual publication.

**Independent Test**: Appoint an eligible Chief Leader to a Draft Event with no unresolved assignment and verify the Pending assignment, unchanged Event state, audit history, and notification intent as one consistent result.

**Acceptance Scenarios**:

1. **Given** an authorized Admin is viewing a Draft Event with no current Pending or Active Chief Leader assignment, **when** the Admin appoints an active user from the predefined Chief Leader pool, **then** exactly one new assignment exists in `Pending`, the Event remains `Draft`, and the appointment identifies the candidate, appointing Admin, and appointment time.
2. **Given** a successful appointment, **when** the business result is committed, **then** an immutable `ChiefLeaderAppointed` audit record and one in-platform `ChiefLeaderAppointment` notification intent for the appointed candidate are committed consistently with the Pending assignment.
3. **Given** a dual-role user belongs to both the Chief Leader and Leader roles, **when** the Admin appoints that user, **then** the appointment is permitted because a Pending Chief Leader assignment adds no same-Event participation restriction.
4. **Given** the Admin starts selecting a candidate but leaves without confirming, **when** the flow is abandoned, **then** no assignment, audit record, or notification intent is created.

---

### User Story 2 - Find an Eligible Chief Leader (Priority: P1)

An Admin can identify and select from the active users who are members of the predefined Chief Leader pool while appointing a Chief Leader to a Draft Event.

**Why this priority**: The authoritative pool constraint must be visible in the Admin workflow and enforced when the appointment is saved.

**Independent Test**: Open the appointment flow with users across active, inactive, Chief Leader, Leader-only, and dual-role conditions and verify that the selectable choices and save-time validation match the predefined pool rule.

**Acceptance Scenarios**:

1. **Given** active users in the predefined Chief Leader pool, **when** an authorized Admin opens appointment for an eligible Draft Event, **then** those users are available for selection with enough identity information to distinguish them.
2. **Given** an inactive user or a user without Chief Leader role membership, **when** the Admin reviews the candidate choices, **then** that user is not offered as an eligible candidate.
3. **Given** a candidate loses eligibility after the Admin opens the flow, **when** the Admin submits the appointment, **then** the save is rejected, no appointment result is committed, and the Admin receives an actionable outcome.

---

### User Story 3 - Review the Pending Appointment (Priority: P2)

An Admin can reopen the Draft Event and see the current Pending Chief Leader appointment without progressing or changing the assignment lifecycle.

**Why this priority**: Admins need a reliable view of the Event's appointment state before later response workflows occur.

**Independent Test**: Reopen an appointed Draft and verify that the candidate and Pending status are shown while acceptance, decline, external confirmation, and publication remain unavailable in this feature.

**Acceptance Scenarios**:

1. **Given** a Draft Event has a Pending Chief Leader assignment, **when** an authorized Admin opens the Event, **then** the Admin can identify the appointed candidate and the `Pending` status.
2. **Given** a Draft Event has a Pending assignment, **when** an Admin attempts another appointment through normal or manipulated input, **then** the request is rejected and the existing Pending assignment remains unchanged.
3. **Given** a Draft Event has only declined historical assignments and no Pending or Active assignment, **when** an Admin opens appointment, **then** a new eligible candidate may be appointed as a new Pending record without overwriting the declined history.

---

### User Story 4 - Handle Access and Appointment Failures (Priority: P2)

The Admin receives safe, clear feedback when an Event or candidate cannot be used or an appointment cannot be saved. Actors without Admin authority cannot appoint Chief Leaders or inspect the Admin candidate pool.

**Why this priority**: Appointment authority and identity information require consistent protection, and failed commands must not leave partial business results.

**Independent Test**: Exercise unauthorized, unknown Event, non-Draft Event, ineligible candidate, repeated submission, stale state, and recoverable save failure outcomes and verify no partial or duplicate appointment is committed.

**Acceptance Scenarios**:

1. **Given** an actor without Admin authority, **when** they request the Chief Leader candidate pool or attempt an appointment, **then** access is denied and no business data changes.
2. **Given** an unknown Event identifier, **when** an Admin opens or submits appointment, **then** a safe not-found outcome is returned and nothing is created.
3. **Given** an Event is no longer `Draft`, **when** an Admin attempts an appointment, **then** the action is rejected and no assignment, completed audit record, or notification intent is created.
4. **Given** an appointment was already committed but the response was unclear, **when** the same submission is repeated, **then** no duplicate assignment, audit record, or notification intent is created and the Admin can determine the committed result.
5. **Given** appointment fails because of a recoverable network or system problem, **when** the Admin receives the outcome, **then** the failure is clearly explained, the selected candidate remains available where practical, and retry does not produce duplicates.
6. **Given** another actor changes the relevant appointment state after the Admin opens the flow, **when** the Admin submits stale information, **then** the stale action does not overwrite or contradict the newer state and the Admin receives a conflict outcome.

### Edge Cases

- A session or Admin permission that expires before submission results in a denied save and no partial appointment result.
- Candidate eligibility is checked again at the authoritative save boundary; a stale candidate list cannot authorize an ineligible user.
- Pending appointment does not activate operational ownership, satisfy the publication prerequisite, or restrict a dual-role candidate's Leader participation.
- A declined assignment remains historical. A later appointment creates a new record rather than modifying or deleting the declined record.
- A repeated or concurrent appointment attempt cannot create multiple unresolved Pending assignments for one Event.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow an authenticated, authorized Operations Administrator to initiate Chief Leader appointment for an existing Event that remains `Draft`.
- **FR-002**: The system MUST allow an authorized Admin to view active users in the predefined Chief Leader pool as appointment candidates, with enough non-sensitive identity information to distinguish candidates.
- **FR-003**: The candidate set MUST include an active user with Chief Leader role membership even when that user also holds the Leader role.
- **FR-004**: The candidate set MUST exclude inactive users and users without Chief Leader role membership.
- **FR-005**: The system MUST revalidate the selected candidate's active status and Chief Leader role membership at the authoritative appointment boundary, regardless of displayed choices or manipulated client input.
- **FR-006**: The system MUST deny candidate-pool access and appointment commands to actors without Admin authority; Chief Leader or Leader role membership alone MUST NOT grant appointment authority.
- **FR-007**: Appointment MUST be allowed only while the target Event is `Draft`; the system MUST reject an appointment for an unknown Event or an Event in any other lifecycle state.
- **FR-008**: Appointment MUST be allowed only when the Event has no unresolved Pending or Active Chief Leader assignment. A declined historical assignment MUST NOT prevent a later new appointment.
- **FR-009**: A successful appointment MUST create one new `ChiefLeaderAssignment` in `Pending` and identify the Event, appointed candidate, appointing Admin, and appointment time.
- **FR-010**: A successful appointment MUST leave the Event in `Draft` and MUST NOT activate the assignment, satisfy the publication prerequisite, or initiate publication.
- **FR-011**: A Pending appointment MUST NOT impose a new restriction on the candidate's Leader participation. Same-Event Chief Leader and team assignment exclusivity MUST be evaluated only by the later activation workflow.
- **FR-012**: A successful appointment MUST record one immutable `ChiefLeaderAppointed` business audit entry identifying the actor, assignment, Event context, time, source, and correlation with the committed action.
- **FR-013**: A successful appointment MUST create one persistent in-platform notification intent of type `ChiefLeaderAppointment` for the appointed candidate, linked to the Event and containing only the approved bounded context required to render the appointment alert.
- **FR-014**: The Pending assignment, audit record, and notification intent MUST be committed as one consistent business result; failure of any required part MUST leave none of them presented as completed.
- **FR-015**: A rejected or unsuccessful appointment MUST NOT create a completed appointment audit record or notification intent and MUST NOT change any existing assignment.
- **FR-016**: Repeating the same appointment submission after an uncertain outcome MUST NOT create duplicate assignments, audit entries, or notification intents, and the Admin MUST be able to determine the resulting appointment.
- **FR-017**: Concurrent or stale appointment attempts MUST NOT create contradictory unresolved assignments or silently overwrite newer appointment state; the Admin MUST receive a clear conflict outcome where applicable.
- **FR-018**: The system MUST preserve declined assignment history. A later appointment after decline MUST create a new Pending assignment record and MUST NOT overwrite or delete the declined record.
- **FR-019**: An authorized Admin reopening a Draft Event MUST be able to see the current candidate and Pending appointment status.
- **FR-020**: The system MUST present safe not-found, authorization, validation, conflict, and recoverable system-failure outcomes without exposing sensitive implementation details.
- **FR-021**: Appointment loading, saving, success, and failure states MUST be communicated clearly; an unsuccessful appointment MUST NOT be shown as successful.
- **FR-022**: On a recoverable failure, the selected candidate and page context MUST remain available where practical, with a safe way to retry or inspect the outcome.
- **FR-023**: Abandoning an unconfirmed selection MUST NOT create an assignment, audit record, or notification intent.
- **FR-024**: The appointment workflow MUST remain usable at the project's smallest supported mobile viewport without normal-workflow horizontal scrolling, with touch targets at least 44 by 44 CSS pixels, a visible primary action, and no hover-only critical action.
- **FR-025**: The workflow MUST use clear labels, maintain stable layout as status and validation appear, and prevent accidental duplicate submission while an appointment outcome is pending.
- **FR-026**: The workflow MUST meet WCAG 2.2 AA for semantic controls, associated labels, keyboard operation, visible focus, logical reading and tab order, accessible error and status announcements, sufficient contrast, non-color-only meaning, text scaling, and reduced-motion preferences.
- **FR-027**: This feature MUST NOT provide acceptance, decline, external acceptance recording, publication, post-publication replacement, team assignment, notification inbox management, notification read/dismiss actions, or external notification channels.

### Key Entities

- **ChiefLeaderAssignment**: The appointment record connecting a Draft Event, an eligible Chief Leader candidate, the appointing Admin, the appointment time, and `Pending` status. Declined records are retained as history.
- **Event**: The existing aggregate that must remain in `Draft` throughout this feature.
- **Chief Leader Candidate**: An active user with membership in the predefined Chief Leader role pool. Dual-role membership is permitted.
- **In-Platform Notification Intent**: The persistent, unread semantic alert for the appointed candidate, linked to the Event and typed `ChiefLeaderAppointment`.
- **Audit Record**: The immutable business evidence of the appointment action and actor, committed consistently with the appointment.

### Scope and Decision Classification

- **Already approved / known**: Admin appointment authority; predefined Chief Leader pool; Pending assignment creation; dual-role membership under CHANGE-001; no participation restriction while Pending; appointment audit; persistent in-platform appointment notification; Event remains Draft.
- **Irrelevant to SLICE-03**: Direct acceptance or decline (SLICE-04), Admin-recorded external acceptance (SLICE-05), publication (SLICE-06), Leader availability and staffing (SLICE-07–11), post-publication replacement and rescheduling (later lifecycle slices), and full notification retrieval, read, dismiss, and delivery experience (SLICE-17).
- **Requires clarification before planning**: None. The locked lifecycle defines appointment from no assignment or declined history to a new Pending record and provides no Pending replacement transition.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In every acceptance scenario, an authorized Admin can appoint an active predefined Chief Leader to an eligible Draft Event and the resulting assignment is `Pending` while the Event remains `Draft`.
- **SC-002**: Every tested appointment of an inactive user, a non-Chief-Leader user, an unknown Event, a non-Draft Event, or an Event with an unresolved Pending or Active assignment is rejected without partial business results.
- **SC-003**: Every successful appointment produces exactly one correlated assignment, appointment audit record, and appointment notification intent; every failed appointment produces none of those as a completed result.
- **SC-004**: Repeated, concurrent, and stale submissions in the acceptance scenarios produce no duplicate or contradictory unresolved appointments.
- **SC-005**: All tested non-Admin attempts to inspect the candidate pool or appoint a Chief Leader are denied without disclosing protected candidate data.
- **SC-006**: The complete appointment and review journeys can be completed at the project's smallest supported mobile viewport without horizontal scrolling, hover-only actions, or inaccessible controls.
- **SC-007**: Appointment success, validation, authorization, conflict, not-found, and recoverable failure outcomes are actionable and available to keyboard and screen-reader users without relying on color alone.

## Assumptions

- SLICE-02 supplies an existing valid Draft Event and the authenticated Admin foundation.
- “Notification” in this slice means creation of the approved persistent in-platform notification intent. Full inbox retrieval, polling, read state, dismissal, and notification center UX belong to SLICE-17.
- A Pending assignment is unresolved. Because the ratified lifecycle has no Pending-to-replaced or Pending-to-cancelled transition, another appointment is rejected until a later approved response closes that assignment.
- No arbitrary search, pagination, or display-field rules are introduced for the small predefined Chief Leader pool; implementation planning may use existing identity data while avoiding sensitive attributes.
