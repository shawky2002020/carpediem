# Feature Specification: SLICE-05 — Chief Leader Admin-Recorded External Acceptance

**Feature Branch**: `codex/004-chief-leader-admin-acceptance`

**Created**: 2026-09-23

**Status**: Clarified — ready for planning

**Input**: SLICE-05 in the locked V1 roadmap, grounded in the ratified Chief Leader assignment lifecycle, role matrix, CHANGE-001, ADR-005, and the approved external-confirmation exception.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Review a Pending External Confirmation (Priority: P1)

An Operations Admin opens a Draft Event with a Pending Chief Leader appointment and reviews the appointed candidate and Event context before recording an acceptance that the candidate already communicated outside Carpediem.

**Why this priority**: The Admin must identify the exact Pending appointment and understand that this action records an existing external confirmation rather than making a decision for the candidate.

**Independent Test**: Sign in as an active Admin, open a Draft Event with a Pending appointment, and verify the Event, candidate, Pending status, and explicit external-acceptance action; verify other actors cannot access the action.

**Acceptance Scenarios**:

1. **Given** a Draft Event has a Pending Chief Leader appointment, **when** an active Admin opens its appointment context, **then** the Event title, appointed candidate, Pending status, and an explicit “Record external acceptance” action are available.
2. **Given** an unauthenticated actor, ordinary Leader, or Chief Leader without Admin authority, **when** they attempt to open or invoke the external-acceptance action, **then** access is denied without exposing protected details or changing the assignment.
3. **Given** no Pending appointment exists for the Draft Event, **when** an Admin opens its appointment context, **then** no external-acceptance action is offered for a historical, missing, Active, or Declined assignment.

---

### User Story 2 - Record Externally Confirmed Acceptance (Priority: P1)

After the appointed Chief Leader has actually confirmed verbally or through an external channel, the Admin explicitly records that confirmation with a mandatory justification note. The existing Pending assignment becomes Active, its AdminExternal provenance is preserved, and the Event remains Draft.

**Why this priority**: This approved fallback activates operational leadership when the candidate cannot or did not respond directly in Carpediem while preserving clear accountability.

**Independent Test**: Record external acceptance for a Pending appointment with a valid justification and verify one Active assignment, AdminExternal provenance, immutable audit evidence, operational ownership, and an unchanged Draft Event.

**Acceptance Scenarios**:

1. **Given** a Pending appointment, actual external confirmation, no same-Event team-assignment conflict, and a meaningful justification of at most 1,000 characters, **when** an authorized Admin records external acceptance, **then** the existing assignment becomes Active and records the acceptance time, acting Admin, and AdminExternal source.
2. **Given** a successful external acceptance, **when** the business result is reviewed, **then** one immutable audit record identifies the acting Admin, timestamp, Chief Leader, Event, mandatory justification, and external-confirmation source.
3. **Given** a successful external acceptance, **when** the Event is reviewed, **then** the Chief Leader has Event-scoped operational ownership, the assignment prerequisite for later publication is satisfied, and the Event remains Draft.
4. **Given** a blank, whitespace-only, or longer-than-1,000-character justification, **when** the Admin submits, **then** the action is rejected with meaningful justification feedback and the assignment remains Pending.
5. **Given** a dual-role candidate has an Active team assignment on the same Event's current staffing cycle, **when** an Admin attempts external acceptance, **then** activation is rejected and the Pending appointment remains unchanged.

---

### User Story 3 - Leave an Unfinished Record (Priority: P2)

An Admin who has started entering a justification can leave or cancel before submission without recording acceptance or creating audit evidence.

**Why this priority**: External acceptance is an accountable state transition and must occur only after an explicit completed action.

**Independent Test**: Enter a justification, cancel or navigate away, and verify that the assignment stays Pending and no external-acceptance audit record exists.

**Acceptance Scenarios**:

1. **Given** an Admin has entered an unsaved justification, **when** they cancel the action, **then** no acceptance or audit record is created and the appointment remains Pending.
2. **Given** an Admin has entered an unsaved justification, **when** they attempt to leave the flow, **then** the interface protects against accidental loss and any confirmed discard leaves domain state unchanged.

---

### User Story 4 - Resolve Invalid or Uncertain Outcomes (Priority: P2)

The Admin receives a safe, clear outcome when the appointment is missing, no longer Pending, changed concurrently, or cannot be saved. The Admin can inspect the current state after a recoverable failure without creating duplicate acceptance or audit evidence.

**Why this priority**: Candidate direct response and Admin external acceptance can race, and the system must preserve one authoritative outcome.

**Independent Test**: Exercise unknown, stale, repeated, conflicting, and failed submissions and verify that only one valid transition and matching audit result can persist.

**Acceptance Scenarios**:

1. **Given** an unknown Event or assignment, **when** an Admin attempts to record external acceptance, **then** a safe not-found outcome is shown and no state changes.
2. **Given** the candidate directly accepted or declined before the Admin submission, **when** the Admin submits stale Pending information, **then** the external acceptance is rejected and the committed direct outcome remains authoritative.
3. **Given** direct candidate response and Admin external acceptance race, **when** both are submitted, **then** exactly one valid outcome commits and the other actor receives a clear current-state or conflict outcome.
4. **Given** an uncertain result after submission, **when** the same Admin safely retries the same intent or reloads the appointment, **then** a committed external acceptance is identifiable without a duplicate transition or audit record.
5. **Given** the required audit evidence cannot be committed, **when** external acceptance is attempted, **then** the assignment remains Pending and the Admin receives clear failure feedback.

### Edge Cases

- Entering only spaces does not satisfy the mandatory justification requirement.
- The 1,000-character approved audit-reason limit accepts exactly 1,000 characters and rejects a longer reason without truncation.
- Active and Declined assignments cannot be converted or overwritten through external acceptance.
- A Pending appointment grants no operational Event ownership before a valid acceptance commits.
- Repeated clicks while a save is unresolved do not create multiple transitions or audit records.
- A recoverable failure preserves the entered justification where practical so the Admin can review the current state before retrying.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let an authenticated, active Operations Admin review a Draft Event's current Pending Chief Leader appointment and the appointed candidate before recording external acceptance.
- **FR-002**: The system MUST restrict review and recording of external acceptance to authorized Admins and MUST enforce that authority independently of client controls.
- **FR-003**: External acceptance MUST be available only through an explicit action identified as “Record external acceptance”; viewing an appointment, entering a note, or navigating MUST NOT imply acceptance.
- **FR-004**: The action MUST communicate that it records acceptance already confirmed verbally or through an external channel and MUST NOT operate as a silent Admin decision on the candidate's behalf.
- **FR-005**: Only an existing Pending Chief Leader assignment for a Draft Event MAY transition through this action; unknown, Active, Declined, or otherwise ineligible targets MUST NOT transition.
- **FR-006**: The Admin MUST provide a non-empty, non-whitespace justification note of at most 1,000 characters before external acceptance can be recorded.
- **FR-007**: A valid external acceptance MUST transition the existing Pending assignment to Active and record the acceptance time, acting Admin identity, and AdminExternal acceptance source.
- **FR-008**: External acceptance MUST grant the appointed Chief Leader Event-scoped operational ownership and satisfy the assignment part of the later publication prerequisite while leaving the Event in Draft.
- **FR-009**: Before activation, the system MUST verify that the appointed candidate holds no Active team assignment on the same Event's current staffing cycle; a conflict MUST leave the appointment Pending.
- **FR-010**: A successful external acceptance MUST append exactly one immutable audit record containing the acting Admin, timestamp, Chief Leader, Event, mandatory justification, and external-confirmation source.
- **FR-011**: The assignment transition and its required audit evidence MUST form one consistent business result; failure of either part MUST leave the prior Pending state intact.
- **FR-012**: External acceptance MUST NOT overwrite or remove prior appointment history, and it MUST NOT create a replacement assignment.
- **FR-013**: Repeated, stale, or concurrent submissions MUST NOT create duplicate audit records, more than one Active Chief Leader for the Event, or contradictory assignment outcomes. A same-intent retry means the same appointment, acting Admin, and normalized justification; a retry with different evidence MUST NOT be treated as the original success.
- **FR-014**: When direct candidate response and Admin external acceptance contend, the first valid committed outcome MUST remain authoritative and the losing operation MUST expose a safe current-state or conflict result.
- **FR-015**: The system MUST provide safe, distinguishable validation, not-found, authorization, invalid-state, concurrency-conflict, and recoverable-failure outcomes without exposing sensitive implementation details.
- **FR-016**: Cancelling or discarding an unfinished external-acceptance action MUST leave the appointment Pending and MUST NOT create audit evidence.
- **FR-017**: The flow MUST show clear loading, saving, success, and failure states, keep the primary action visible, and prevent accidental overlapping submissions while an outcome is unresolved.
- **FR-018**: On a recoverable failure, the Admin MUST be able to inspect the current appointment and safely retry where appropriate without unnecessarily losing the entered justification.
- **FR-019**: The flow MUST be usable at the project's smallest supported mobile viewport without normal-workflow horizontal scrolling, with touch targets at least 44 by 44 CSS pixels, mobile-friendly text entry, clear labels, stable layout, and no hover-only critical action.
- **FR-020**: The flow MUST meet WCAG 2.2 AA expectations for semantic controls, associated labels, keyboard use, visible focus, logical order, accessible validation and status messaging, sufficient contrast, non-color-only meaning, text scaling, and reduced-motion preferences.
- **FR-021**: This feature MUST NOT provide candidate direct response, Event publication, Chief Leader replacement, team staffing, notification inbox management, notification delivery controls, or external communication integration.

### Key Entities

- **ChiefLeaderAssignment**: The existing appointment record whose only valid external-confirmation transition is Pending to Active; it preserves acceptance time, source, and acting Admin attribution.
- **Event**: The associated Draft Event; acceptance establishes leadership ownership but does not publish or otherwise change the Event lifecycle.
- **External Acceptance Audit Record**: Immutable evidence of the action with Admin actor, time, candidate, Event, mandatory justification, and AdminExternal source.

### Scope and Decision Classification

- **Already approved / known**: An Admin may record acceptance only after actual verbal or external confirmation; the action is explicit; justification and immutable audit evidence are mandatory; Pending transitions to Active with AdminExternal provenance; operational ownership begins; CHANGE-001 same-Event exclusivity applies at activation.
- **Irrelevant to SLICE-05**: Candidate direct response, publication, staffing, post-publication replacement, notification inbox behavior, and integrations with chat or other external channels.
- **Requires clarification before planning**: None.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In every tested valid Pending appointment, an authorized Admin can record externally confirmed acceptance with one meaningful justification and produce one Active assignment.
- **SC-002**: Every successful external acceptance produces exactly one immutable audit record with all six required facts: Admin, time, Chief Leader, Event, justification, and external-confirmation source.
- **SC-003**: Every tested missing, whitespace-only, or over-limit justification is rejected while the assignment remains Pending and no external-acceptance audit record is created.
- **SC-004**: Every tested unauthorized, invalid-state, same-Event team-conflict, stale, and concurrent attempt leaves no duplicate Active assignment, contradictory outcome, or partial audit result.
- **SC-005**: The complete external-acceptance journey works at the smallest supported mobile viewport with no horizontal scrolling and with keyboard and screen-reader usable labels, validation, and status feedback.
- **SC-006**: After an uncertain submission, an Admin can determine the committed assignment state without creating a duplicate transition or audit record.

## Assumptions

- SLICE-03 supplies the existing Pending appointment and SLICE-02 supplies the Draft Event.
- The Admin is attesting that external confirmation already occurred; Carpediem records the evidence but does not initiate or verify the external communication channel.
- The approved 1,000-character audit-reason bound applies to this mandatory justification and no smaller feature-specific limit is introduced.
- Publication remains a separate explicit Admin action in SLICE-06.
