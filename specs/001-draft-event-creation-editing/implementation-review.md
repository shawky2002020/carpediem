# SLICE-02 Implementation Review

**Reviewed**: 2026-09-21
**Branch**: `codex/001-draft-event-creation-editing`
**Result**: PASS

## Functional requirement traceability

| Requirement | Implementation evidence | Verification |
|---|---|---|
| FR-001 | Admin route guard, Draft create route, server `RequireAdmin` | authorization integration and browser create journey |
| FR-002 | Admin-only Draft list and detail query endpoints/pages | read integration and create/reopen browser journey |
| FR-003 | Complete five-field Draft update use case and edit form | domain, integration, Angular, and browser edit tests |
| FR-004 | Current database-backed Admin policy on every Draft endpoint | Admin/Chief Leader/Leader/anonymous authorization matrix |
| FR-005 | Draft-only Admin routes and queries; no Leader available-event surface | authorization and Draft filtering tests |
| FR-006 | Title, Start, End, Location required; Notes nullable or blank | domain, API, Angular, browser tests with/without Notes |
| FR-007 | Unicode-aware server/domain nonblank validation | domain and API whitespace parameter sets |
| FR-008 | Strict Cairo input parser and field-addressable errors | Cairo unit tests, malformed JSON, API and browser validation tests |
| FR-009 | Aggregate and SQL checks enforce Start before End; no future-only rule | domain/API equal, reversed, and past-schedule tests |
| FR-010 | `Africa/Cairo` resolver, explicit overlap offsets, UTC persistence, Cairo projections | Cairo unit tests, SQL round trip, real-stack smoke |
| FR-011 | Endpoint policy plus application/domain/SQL validation | manipulated-request integration coverage |
| FR-012 | Event, cycle 1, audit, and intent commit in one transaction | SQL create and atomicity assertions |
| FR-013 | Transaction rollback and zero-write validation assertions | forced persistence failure and invalid-create integration tests |
| FR-014 | Draft edit and optional cycle rotation commit together | edit integration tests and cycle history assertions |
| FR-015 | Invalid/stale/failed edits preserve committed values | integration and browser failure tests |
| FR-016 | Domain and store reject non-Draft updates; DTO cannot set status | domain lifecycle and API `not_draft` tests |
| FR-017 | Allow-listed create/update audit written only with successful transaction | audit integration assertions, including Notes exclusion |
| FR-018 | Per-actor durable intent ledger and payload fingerprint | replay, concurrent request, lost response, changed payload, distinct intent tests |
| FR-019 | SQL rowversion predicate and `stale_draft` response | stale integration and browser review-flow tests |
| FR-020 | Inline/summary field errors with retained reactive form values | Angular and browser correction tests |
| FR-021 | dirty-form route/unload guard and explicit discard decision | Angular and browser abandonment tests |
| FR-022 | stable safe 401/403/404 ProblemDetails responses | contract and authorization tests |
| FR-023 | loading, saving, success, error, and uncertain states | Angular and browser state tests |
| FR-024 | retry keeps the creation intent; edit/create forms retain values | service/component and browser recovery tests |
| FR-025 | mobile one-column form, visible save, 44px targets, no overflow | responsive checks at 320/360/390/430 CSS pixels and real-stack 320px smoke |
| FR-026 | associated visible labels, inline feedback, reserved status regions | component markup and accessibility/browser checks |
| FR-027 | semantic controls, keyboard flow, visible focus, logical order | axe plus keyboard/focus browser checks |
| FR-028 | live regions, contrast tokens, non-color errors, zoom/reduced-motion support | accessibility and responsive browser checks |

## Acceptance scenario traceability

| Scenario | Result and evidence |
|---|---|
| US1.1 valid creation with Notes | PASS: API/SQL creation plus browser create/reopen |
| US1.2 creation without Notes | PASS: API theory case and browser case |
| US1.3 abandon unfinished creation | PASS: route guard and browser confirmation |
| US1.4 repeat uncertain creation | PASS: idempotency integration and browser retry |
| US1.5 Cairo entry, UTC representation, Cairo reopen | PASS: resolver/domain/SQL checks and real-stack smoke |
| US2.1 missing required field | PASS: API zero-write matrix and browser feedback |
| US2.2 whitespace-only required text | PASS: Unicode whitespace domain/API/browser cases |
| US2.3 End before Start | PASS: domain/API/browser rejection |
| US2.4 End equal to Start | PASS: domain/API/browser rejection |
| US2.5 malformed or unusable date/time | PASS: malformed JSON, Cairo gap, inline correction |
| US2.6 valid past schedule | PASS: domain/API acceptance |
| US3.1 open existing Draft with all values | PASS: detail integration and browser edit load |
| US3.2 valid edit remains Draft and consistent | PASS: edit/cycle/audit integration and real-stack smoke |
| US3.3 invalid edit preserves prior Draft | PASS: integration rollback and browser value retention |
| US3.4 stale edit does not overwrite | PASS: rowversion integration and browser conflict state |
| US4.1 unauthorized creation/edit denied | PASS: role and anonymous matrix with unchanged data |
| US4.2 ordinary Leader cannot discover Draft | PASS: Draft endpoint access matrix; no Leader event view added |
| US4.3 unknown Event | PASS: 404 contract and browser safe state |
| US4.4 recoverable/uncertain save | PASS: same-intent resolution and retained-form browser cases |
| US4.5 non-Draft edit | PASS: domain guard and distinct 409 `not_draft` contract |

## Success criteria

| Criterion | Result |
|---|---|
| SC-001 | PASS: required-only and Notes-present creation both succeed. |
| SC-002 | PASS: all approved invalid classes reject without a usable persisted Draft. |
| SC-003 | PASS: successful edits remain Draft; invalid and stale edits preserve the last valid save. |
| SC-004 | PASS: every tested non-Admin or anonymous Draft operation is denied. |
| SC-005 | PASS: full journeys work at 320 CSS pixels without horizontal overflow and with keyboard/touch access. |
| SC-006 | PASS: error states give a next action and one creation intent produces at most one Event. |
| SC-007 | PASS: automated WCAG A/AA scans and focused form checks pass at every configured viewport. |

## Scope review

The implementation adds Draft create/list/detail/edit, its initial and replacement `StaffingCycle` bookkeeping, required audit/idempotency support, and the minimum session/host scaffolding. It adds no Chief Leader appointment or acceptance, publication, availability, staffing selection or assignment, cancellation/replacement/rescheduling workflow, completion, attendance, notification delivery, Leader history, dashboard, production account administration, feedback, or incident capability.

## Contract, schema, and operational review

- Generated OpenAPI matches the approved Draft and session contracts for security, request/response shapes, optional Notes/offset fields, and documented 400/401/403/404/409/422/500 outcomes.
- The idempotent migration artifact contains only Identity, Event, StaffingCycle, AuditLog, and DraftCreateRequest schema. Named constraints enforce UTC offsets, valid windows, valid status, nonblank common text, unique cycles, and one current cycle.
- RCSI is applied through a nontransactional database operation because SQL Server does not allow that statement inside the schema transaction. The generated deployment script preserves a migration boundary; deployment interruption at that operation requires the normal migration-status check before retry.
- Request logs and traces include identifiers and timing without request bodies, credentials, Title, Location, or Notes. Audit payloads omit Notes content.
- The SQL integration harness uses the Docker CLI instead of the Testcontainers package because Windows application control blocked the unsigned helper assembly. It still creates a fresh SQL Server 2025 container on an unused port and removes it after the suite; this changes test orchestration only.

## Deviations and clarifications

No product requirement, contract, or scope deviation remains. No blocking clarification remains.
