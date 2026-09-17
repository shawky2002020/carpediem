# Feature Specification: SLICE-02 — Draft Event Creation & Draft Editing

**Feature Branch**: `codex/001-draft-event-creation-editing`

**Created**: 2026-09-18

**Status**: Draft — clarified, ready for `/plan`

**Input**: SLICE-02 feature request, grounded in the [Carpediem constitution](../../.specify/memory/constitution.md), [source-of-truth rules](../../docs/SOURCE_OF_TRUTH.md), [CHANGE-002](../../docs/decisions/CHANGE-002-draft-event-minimum-fields.md), [lifecycle](../../docs/requirements/LIFECYCLE_SPECIFICATION.md), [role permissions](../../docs/requirements/ROLE_PERMISSION_MATRIX.md), and [V1 feature slices](../../docs/roadmap/V1_FEATURE_SLICES.md).

## Clarifications

### Session 2026-09-18

- Q: Which time zone should Admins use to enter and review Event times? → A: One fixed organization time zone.
- Q: Which organization time zone? → A: Cairo (`Africa/Cairo`).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a Draft Event (Priority: P1)

An authenticated Operations Administrator enters the minimum Event details and saves a Draft that can be opened later for further authoring. Creating a Draft does not publish it or expose it to ordinary Leaders.

**Why this priority**: A valid Draft Event is the starting point for later Event workflows.

**Independent Test**: Create a Draft using only the four required fields, reopen it as an Admin, and verify its values, Draft status, and initial current staffing context.

**Acceptance Scenarios**:

1. **Given** an authorized Admin has entered valid Title, StartDateTime, EndDateTime, Location, and Notes, **when** they create the Event, **then** exactly one Event exists in `Draft`, its supplied values are preserved, and its initial current `StaffingCycle` exists.
2. **Given** an authorized Admin has entered valid required fields and left Notes blank, **when** they create the Event, **then** creation succeeds with Notes absent or blank and the Event remains `Draft`.
3. **Given** an Admin has started a new Event but has not saved it, **when** they leave or cancel creation, **then** no Event is created and the Admin receives an appropriate warning before losing entered data.
4. **Given** a create request is repeated because its result was unclear, **when** it represents the same submission, **then** the Admin can determine whether creation succeeded without accidentally creating a duplicate Event.
5. **Given** an Admin enters a valid Event schedule as Cairo local date and time, **when** the Draft is saved and reopened, **then** the same intended Cairo schedule is shown and the Event's scheduled instants are represented in UTC.

---

### User Story 2 - Correct Invalid Draft Input (Priority: P1)

An Admin can identify and correct invalid fields during creation or editing. A rejected submission does not produce a partially valid Event or change a previously valid Draft.

**Why this priority**: Event schedule and identity data must be valid before later workflows use them.

**Independent Test**: Submit each invalid input class and verify field-specific feedback and unchanged persisted state.

**Acceptance Scenarios**:

1. **Given** one of Title, StartDateTime, EndDateTime, or Location is missing, **when** the Admin saves, **then** the save is rejected, the affected field is identified, and no invalid Event or edit is persisted.
2. **Given** Title or Location contains only whitespace, **when** the Admin saves, **then** the save is rejected with feedback on that field.
3. **Given** EndDateTime is earlier than StartDateTime, **when** the Admin saves, **then** the save is rejected and the date order problem is identified.
4. **Given** EndDateTime equals StartDateTime, **when** the Admin saves, **then** the save is rejected and the date order problem is identified.
5. **Given** a supplied date/time cannot be interpreted as a usable date/time, **when** the Admin saves, **then** the save is rejected, the affected value is identified, and entered values remain available for correction where practical.
6. **Given** all required values are valid and StartDateTime is before EndDateTime, but the scheduled window is in the past, **when** the Admin creates or edits a Draft, **then** past timing alone does not cause rejection.

---

### User Story 3 - Edit an Existing Draft (Priority: P2)

An Admin opens an existing Draft, changes any of the Draft authoring fields, and saves it without changing its lifecycle state.

**Why this priority**: Draft details may need correction before the later appointment and publication workflows.

**Independent Test**: Open a Draft, change its fields, save, reopen, and verify the revised values and unchanged Draft state.

**Acceptance Scenarios**:

1. **Given** an existing Draft, **when** an authorized Admin opens it, **then** the current Title, StartDateTime, EndDateTime, Location, and Notes values are shown for editing.
2. **Given** an existing Draft and valid changes to one or more authoring fields, **when** the Admin saves, **then** the new values are persisted, the Event remains `Draft`, and its current staffing context remains consistent with the saved Draft schedule.
3. **Given** an existing valid Draft and invalid proposed changes, **when** the Admin saves, **then** the update is rejected and all previously persisted valid values remain intact.
4. **Given** a Draft was changed after the Admin opened it, **when** that Admin saves a stale version, **then** the stale save does not silently overwrite the newer version and the Admin receives a clear conflict outcome.

---

### User Story 4 - Handle Access and Save Failures (Priority: P2)

The Admin receives safe, clear feedback when an Event cannot be opened or saved. Actors without Admin authority cannot use Draft authoring capabilities.

**Why this priority**: Drafts must remain private and recoverable through ordinary error conditions.

**Independent Test**: Attempt Draft access and mutation with unauthorized roles, an unknown Event, and a recoverable save failure.

**Acceptance Scenarios**:

1. **Given** a user without Admin authority, **when** they attempt to create or edit a Draft, **then** the action is denied and no Event data changes.
2. **Given** an ordinary Leader, **when** they browse available events or directly request a Draft, **then** the Draft is not exposed as an available or published Event.
3. **Given** an Admin opens or edits an Event identifier that does not exist, **then** a safe not-found outcome is shown and no Event is created or changed.
4. **Given** a save fails because of a recoverable network or system problem, **when** the Admin receives the outcome, **then** the failure is clearly explained, entered data remains available where practical, and retrying does not accidentally create a duplicate Event.
5. **Given** an Event is no longer `Draft` when an Admin attempts a Draft edit, **then** the edit is rejected without changing the Event or advancing its lifecycle.

### Edge Cases

- A session or Admin permission that expires between opening and saving a form must result in a denied save and clear next action; entered data should remain available where practical.
- An uncertain create response must not be presented as a confirmed failure if the Event may already exist; the Admin needs a way to resolve the outcome before trying again.
- Changing a Draft's schedule must leave the saved Event and its required current staffing context consistent. This does not define the later rescheduling workflow for published Events.
- Notes may be empty. No capacity, quota, category, tag, equipment, virtual meeting, Chief Leader, or recruitment value is required for Draft creation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow an authenticated, authorized Operations Administrator (Admin) to start Draft Event creation.
- **FR-002**: The system MUST allow Admins to view and open existing Draft Events for authoring.
- **FR-003**: The system MUST allow Admins to edit Title, StartDateTime, EndDateTime, Location, and Notes while an Event remains `Draft`.
- **FR-004**: The system MUST deny Draft creation and editing to actors without Admin authority, including Chief Leaders and Leaders acting without that authority; a request made outside the normal interface MUST receive the same protection.
- **FR-005**: The system MUST make Draft authoring views available to authorized Admins and keep Draft Events out of ordinary Leader available or published Event views. An unassigned Chief Leader MUST NOT gain Draft authoring access through their Chief Leader role.
- **FR-006**: The system MUST require Title, StartDateTime, EndDateTime, and Location to create or save a Draft Event; Notes MUST be optional.
- **FR-007**: The system MUST reject a Title or Location that is empty or consists only of whitespace.
- **FR-008**: The system MUST reject missing, malformed, or unusable StartDateTime and EndDateTime values.
- **FR-009**: The system MUST accept a Draft schedule only when `StartDateTime < EndDateTime`; equal or reversed values MUST be rejected. Past timing alone MUST NOT cause rejection because no future-only Draft rule is approved.
- **FR-010**: The system MUST let Admins enter and review Draft Event date/times in the fixed organization time zone `Africa/Cairo`, identify that zone in the authoring experience, and represent the corresponding scheduled instants as UTC Event timestamps. Reopening a Draft MUST show the intended Cairo local schedule. If an entered local time does not identify one usable instant, the system MUST request correction or explicit disambiguation rather than silently changing the intended time.
- **FR-011**: The system MUST apply critical validation and authorization at the authoritative save boundary, regardless of any earlier form feedback or manipulated client input.
- **FR-012**: A successful creation MUST establish one valid Event in `Draft` and its initial current `StaffingCycle` as one consistent business result.
- **FR-013**: A failed creation MUST leave no partially valid Event or orphaned initial staffing context presented as a usable Draft.
- **FR-014**: A successful Draft edit MUST persist the accepted field values, keep the Event in `Draft`, and keep its required current staffing context consistent with the saved Draft schedule.
- **FR-015**: An invalid or unsuccessful Draft edit MUST leave the previously persisted valid Event and its current staffing context intact.
- **FR-016**: The system MUST reject Draft authoring changes if the Event is no longer `Draft`; client manipulation MUST NOT bypass lifecycle guards or set another Event status through this feature. Draft creation or editing MUST NOT implicitly appoint a Chief Leader, publish or cancel an Event, or initiate Leader staffing or notifications.
- **FR-017**: A successful Draft create or edit MUST record the action and acting Admin in the approved operational audit history, consistently with the committed business change; a rejected or failed change MUST NOT be recorded as a completed change.
- **FR-018**: The system MUST prevent an accidental duplicate Event when the same creation submission is repeated or retried after an uncertain outcome, and MUST let the Admin determine the resulting Event.
- **FR-019**: A stale Draft edit MUST NOT silently overwrite a newer saved version; the Admin MUST receive a conflict outcome that permits review before a further save attempt.
- **FR-020**: The system MUST identify invalid fields with actionable feedback and MUST retain entered values for correction where practical.
- **FR-021**: An Admin who abandons an unsaved creation flow MUST not create an Event and MUST be warned before entered data is discarded.
- **FR-022**: The system MUST present a safe not-found outcome for an unknown Event and a safe authorization outcome for denied access, without exposing sensitive implementation details.
- **FR-023**: The system MUST communicate loading, saving, success, validation failure, authorization failure, and recoverable system failure states clearly; an unsuccessful save MUST NOT be shown as successful.
- **FR-024**: On a recoverable save failure, the system MUST preserve entered form data where practical and offer a safe path to retry or check the save outcome.
- **FR-025**: The creation and editing workflows MUST remain usable at the project's smallest supported mobile viewport without normal-workflow horizontal scrolling, with touch targets at least 44 by 44 CSS pixels, a visible and accessible primary save action, and mobile-friendly date/time entry.
- **FR-026**: The workflows MUST use clear field labels, place validation near the affected field where practical, avoid hover-only critical actions, and minimize layout shifts as feedback or loading states appear.
- **FR-027**: The workflows MUST meet WCAG 2.2 AA for semantic form controls, associated labels, keyboard operation, visible focus, logical reading and tab order, and accessible validation messages.
- **FR-028**: Status and error feedback MUST be available to screen readers, meet sufficient contrast, avoid color-only meaning, and remain usable when text is enlarged or reduced motion is requested.

### Key Entities

- **Event**: The authored gathering, with Title, scheduled start and end, Location, optional Notes, and lifecycle status. This feature creates and edits only `Draft` Events.
- **StaffingCycle**: The Event's initial current staffing context. It is established with creation and must remain consistent with the saved Draft schedule; staffing responses and assignments belong to later slices.
- **Operations Administrator**: An authenticated user with Admin authority who creates and edits Draft Events. The approved audit history identifies the acting Admin.

### Scope and Decision Classification

- **Already approved / known**: Admin creates, views, and edits Draft Events; CHANGE-002 supplies exactly four required fields and optional Notes; a newly created Event is `Draft`; the initial current `StaffingCycle` is established at creation; critical rules are checked at save; ordinary Leaders do not see Drafts as available events. The [V1 feature slice](../../docs/roadmap/V1_FEATURE_SLICES.md) expressly requires creation and update audit evidence and no Draft authoring notification. [ADR-003](../../docs/decisions/ADR-003-authorization-resource-policies.md) separates Admin authority from the Event's lifecycle guard; [ADR-002](../../docs/decisions/ADR-002-authentication-session-strategy.md) governs loss of authentication while saving.
- **Irrelevant to SLICE-02**: `FOUNDATION-01` is accompanying engineering scaffolding. Chief Leader appointment and response (`SLICE-03`–`05`), publication (`SLICE-06`), Leader availability and staffing (`SLICE-07`–`11`), published Event rescheduling, cancellation, completion, and attendance (`SLICE-12`–`16`), notifications (`SLICE-17`), Leader history and dashboards (`SLICE-18`–`19`), and account administration, feedback, and incidents (`SLICE-20`–`22`) are outside this feature. Their unresolved questions do not change Draft authoring requirements.
- **Requires clarification before planning**: None. The stakeholder selected `Africa/Cairo` for Admin entry and review; CHANGE-002 governs UTC Event timestamps. The later ratified CHANGE-002 resolves the older open minimum-field question. The later approved SLICE-02 handoff requires the initial current `StaffingCycle` at creation, even though older lifecycle sequence text describes its initialization at publication; this specification follows the later SLICE-02 contract without defining publication behavior.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In the acceptance scenarios, an authorized Admin can create a valid `Draft` using only the four required CHANGE-002 fields; adding or omitting Notes both succeed.
- **SC-002**: Every tested submission with a missing required field, whitespace-only required text, unusable date/time, or a non-increasing time window is rejected without a valid Draft being persisted.
- **SC-003**: Every tested successful Draft edit preserves the `Draft` status, and every tested failed or stale edit leaves the last valid saved values intact.
- **SC-004**: All tested unauthorized creation and editing attempts are denied, and ordinary Leaders cannot discover Drafts as available or published Events.
- **SC-005**: The full create, correct, and edit journeys can be completed at the project's smallest supported mobile viewport without horizontal scrolling or a hover-only action, using touch and keyboard input.
- **SC-006**: In tested validation, access, and recoverable failure cases, the Admin can identify the outcome and next action; repeated submission of one creation intent does not yield duplicate Events.
- **SC-007**: The create and edit journeys meet the applicable WCAG 2.2 AA checks for labels, focus, order, error announcements, contrast, text scaling, and non-color-only feedback.

## Assumptions

- Existing authenticated Admin access is supplied by the accompanying `FOUNDATION-01` bootstrap. Production account administration belongs to `SLICE-20`.
- “View Draft” in this feature means the Admin authoring view. The authoritative matrix permits Chief Leaders to view assigned Events, but assignment is introduced only in `SLICE-03`; this feature grants no unassigned Chief Leader Draft access.
- The project does not state a numeric minimum supported mobile viewport, so verification uses the smallest viewport adopted by the project at test time rather than inventing a width here.
- No additional mandatory fields, arbitrary text-length limits, or post-publication edit policies are introduced by this specification.
