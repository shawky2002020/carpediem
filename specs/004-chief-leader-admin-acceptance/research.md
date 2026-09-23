# Research: SLICE-05 — Chief Leader Admin-Recorded External Acceptance

## Existing assignment and provenance

- **Decision**: Transition the existing SLICE-03 Pending assignment and populate the existing acceptance fields with `AdminExternal`, acting Admin, and UTC time.
- **Rationale**: The locked lifecycle defines external acceptance as another Pending-to-Active path on the same record. Creating another assignment would damage history and race protection.
- **Alternatives considered**: A separate external-confirmation entity duplicates assignment state; replacing the Pending row violates preservation rules.

## Mandatory reason and audit atomicity

- **Decision**: Validate trimmed justification server-side at 1–1,000 characters and commit one typed external-acceptance audit record in the same transaction as activation.
- **Rationale**: ADR-005 and the logical data model require mandatory evidence and define the approved maximum. Atomic commit prevents unaudited authority changes.
- **Alternatives considered**: Optional reason, silent truncation, or asynchronous audit all violate locked rules.

## Concurrency and retry

- **Decision**: Use assignment rowversion for the submitted Pending view. Recognize a same-intent replay only when the assignment is already Active from `AdminExternal` and matching immutable audit evidence identifies the same Admin and justification. Other terminal or stale outcomes conflict.
- **Rationale**: This resolves uncertain network outcomes without another table and prevents a replay with different evidence from being treated as the original operation.
- **Alternatives considered**: Blindly treating every Active state as success hides a candidate direct-accept race; a request ledger adds schema without additional business value.

## Authorization and trusted values

- **Decision**: Require Admin policy and current active Admin membership. Derive actor, candidate, Event, source, and time on the server from authenticated and persisted state.
- **Rationale**: External acceptance is an audited administrative exception. Client-supplied provenance would weaken audit integrity.
- **Alternatives considered**: UI-only restriction or accepting client actor/source fields is unsafe.

## CHANGE-001 seam

- **Decision**: Retain the same domain activation guard used by direct acceptance. Until TeamAssignment exists, no conflicting record can be created; the later Staffing slice must wire its current-cycle lookup before creating active team assignments.
- **Rationale**: Preserves the ratified same-Event exclusivity rule without implementing later staffing scope.
- **Alternatives considered**: Omitting the guard loses a locked invariant; inventing Staffing persistence pulls a later slice forward.

## Admin user experience

- **Decision**: Extend the current Admin appointment page with a focused disclosure/form instead of adding another route. Keep the existing context visible, require explicit submission, guard dirty navigation, and use context reload for uncertain outcomes.
- **Rationale**: The Admin is already reviewing the exact appointment and candidate there; one surface reduces selection errors and preserves mobile context.
- **Alternatives considered**: A separate dashboard or notification-driven workflow belongs to later slices.

No research item remains unresolved.
