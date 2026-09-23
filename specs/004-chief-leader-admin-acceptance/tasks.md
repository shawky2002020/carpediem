# Tasks: SLICE-05 — Chief Leader Admin-Recorded External Acceptance

**Input**: Design documents from `specs/004-chief-leader-admin-acceptance/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contract](contracts/chief-leader-admin-acceptance.openapi.yaml), [quickstart.md](quickstart.md)

**Tests**: Required by the feature specification and constitution. Write each listed test first and observe the intended failure before implementation.

**Organization**: Tasks are grouped by user story and ordered so each increment has independently reviewable evidence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on another incomplete task.
- **[Story]**: Maps the task to a user story in `spec.md`.
- Every task names the exact target file or directory.

## Phase 1: Setup and Contract Alignment

**Purpose**: Confirm the frozen requirements gate and establish the feature's existing-surface seams.

- [ ] T001 Record the 24/24 pre-task requirements quality gate and frozen spec/plan references in `specs/004-chief-leader-admin-acceptance/checklists/feature-quality.md` and `specs/004-chief-leader-admin-acceptance/plan.md`
- [ ] T002 [P] Add external-acceptance operation metadata and required `If-Match`/`X-XSRF-TOKEN` headers to the generated OpenAPI configuration in `src/Carpediem.Web/Program.cs`
- [ ] T003 [P] Create structured outcome telemetry for external acceptance without justification text in `src/Carpediem.Web/Observability/ChiefLeaderExternalAcceptanceTelemetry.cs`

---

## Phase 2: Foundational Domain and Application Contracts

**Purpose**: Establish shared transition, audit, and persistence contracts before UI stories.

**⚠️ CRITICAL**: User story implementation begins only after this phase is green.

- [ ] T004 [P] Add failing domain tests for Pending-to-Active AdminExternal transition, UTC time, Admin attribution, non-Pending rejection, and active same-Event team conflict in `tests/Carpediem.Domain.Tests/Events/ChiefLeaderExternalAcceptanceTests.cs`
- [ ] T005 Implement `AcceptExternal` and focused state/team-conflict guards on the existing assignment in `src/Modules/Events/Domain/ChiefLeaderAssignments/ChiefLeaderAssignment.cs`
- [ ] T006 [P] Define external-acceptance command/result, validation, not-found, conflict, and store contracts in `src/Modules/Events/Application/ChiefLeaderExternalAcceptances/IChiefLeaderExternalAcceptanceStore.cs`
- [ ] T007 Implement the external-acceptance handler with trimmed 1–1,000-character justification validation in `src/Modules/Events/Application/ChiefLeaderExternalAcceptances/RecordChiefLeaderExternalAcceptanceHandler.cs`
- [ ] T008 [P] Extend typed assignment views with nullable accepted time, source, and actor needed for Active recovery in `src/Modules/Events/Application/ChiefLeaderAppointments/IChiefLeaderAppointmentStore.cs`
- [ ] T009 [P] Add a typed immutable audit factory requiring a trimmed 1–1,000-character reason and `AdminExternal` source in `src/Infrastructure/Audit/ChiefLeaderExternalAcceptanceAuditWriter.cs` and `src/Infrastructure/Audit/AuditLog.cs`

**Checkpoint**: Domain transition and server contracts are ready for story implementation.

---

## Phase 3: User Story 1 — Review a Pending External Confirmation (Priority: P1) 🎯 MVP

**Goal**: An authorized Admin sees the exact Pending candidate and an explicit action that explains it records an already received external confirmation.

**Independent Test**: Open the Admin appointment view for Pending, Active, Declined, absent, unauthorized, and unknown contexts; only Pending for an authorized Admin offers the explicit action.

### Tests for User Story 1

- [ ] T010 [P] [US1] Add failing Angular component tests for Pending-only action visibility, candidate/Event context, attestation wording, and absent/terminal suppression in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.spec.ts`
- [ ] T011 [P] [US1] Add failing mobile Playwright coverage for Admin action visibility and ordinary-role denial in `tests/e2e/chief-leader-external-acceptance.spec.ts`

### Implementation for User Story 1

- [ ] T012 [P] [US1] Extend Admin appointment client types with accepted provenance while keeping the current context contract in `frontend/src/app/features/chief-leader-appointment/data/chief-leader-appointment.service.ts`
- [ ] T013 [US1] Add the Pending-only explicit “Record external acceptance” disclosure and explanatory copy in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.html`
- [ ] T014 [US1] Add mobile-first disclosure/form shell styles, stable status space, 44px targets, focus visibility, and no horizontal overflow in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.scss`

**Checkpoint**: The Admin can identify the exact eligible appointment and understand the action without changing state.

---

## Phase 4: User Story 2 — Record Externally Confirmed Acceptance (Priority: P1)

**Goal**: A valid Admin attestation activates the existing assignment with complete AdminExternal provenance and one atomic mandatory audit record.

**Independent Test**: Submit 1, 1,000, blank, whitespace-only, and 1,001-character reasons; verify only valid inputs produce one Active AdminExternal assignment, one complete audit record, operational ownership, and an unchanged Draft Event.

### Tests for User Story 2

- [ ] T015 [P] [US2] Add failing SQL/API tests for active Admin authorization, exact Event/assignment matching, valid 1- and 1,000-character reasons, blank/whitespace/1,001 rejection without truncation, AdminExternal fields, unchanged Draft, ownership, and one audit in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderExternalAcceptances/ChiefLeaderExternalAcceptanceTests.cs`
- [ ] T016 [P] [US2] Add failing Angular service tests for request body plus opaque version header in `frontend/src/app/features/chief-leader-appointment/data/chief-leader-appointment.service.spec.ts`
- [ ] T017 [P] [US2] Add failing Angular component tests for justification association, whitespace/1,001 validation, 1/1,000 acceptance, single in-flight submission, and success state in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.spec.ts`

### Implementation for User Story 2

- [ ] T018 [US2] Implement active-Admin, exact Draft/Event/assignment, rowversion, Pending, team-conflict, provenance, audit, and atomic save behavior in `src/Infrastructure/Persistence/EfChiefLeaderExternalAcceptanceStore.cs`
- [ ] T019 [US2] Register the store and handler dependencies in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs` and `src/Modules/Events/ServiceCollectionExtensions.cs`
- [ ] T020 [US2] Add the typed Admin POST with antiforgery, `If-Match`, validation, authorization, safe RFC 7807 mapping, and telemetry in `src/Carpediem.Web/Endpoints/ChiefLeaderAppointmentEndpoints.cs`
- [ ] T021 [US2] Map accepted provenance consistently in Admin appointment GET/POST responses in `src/Infrastructure/Persistence/EfChiefLeaderAppointmentStore.cs` and `src/Carpediem.Web/Endpoints/ChiefLeaderAppointmentEndpoints.cs`
- [ ] T022 [US2] Implement the external-acceptance HTTP method and typed result in `frontend/src/app/features/chief-leader-appointment/data/chief-leader-appointment.service.ts`
- [ ] T023 [US2] Implement reactive justification state, server/client validation mapping, one in-flight submission, and Active success context in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.ts`
- [ ] T024 [US2] Complete the labeled justification, inline errors, length feedback, and explicit submit controls in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.html`
- [ ] T025 [US2] Add successful AdminExternal browser flow and boundary validation to `tests/e2e/chief-leader-external-acceptance.spec.ts`

**Checkpoint**: The primary audited external-acceptance business result is complete and independently testable.

---

## Phase 5: User Story 3 — Leave an Unfinished Record (Priority: P2)

**Goal**: Cancelling or discarding an unfinished justification never changes domain state or creates audit evidence.

**Independent Test**: Enter justification, cancel or attempt navigation, and verify discard protection plus zero mutation.

### Tests for User Story 3

- [ ] T026 [P] [US3] Add failing Angular tests for cancel, dirty-navigation confirmation, retained text after staying, and zero service call in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.spec.ts`
- [ ] T027 [P] [US3] Add failing Playwright cancellation and leave/stay coverage with zero mutation in `tests/e2e/chief-leader-external-acceptance.spec.ts`

### Implementation for User Story 3

- [ ] T028 [US3] Extend the appointment component's route-deactivation contract to include unsaved external-acceptance justification in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.ts`
- [ ] T029 [US3] Wire explicit cancel/discard behavior without state mutation in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.html`

**Checkpoint**: Unfinished evidence can be safely retained or discarded without a business operation.

---

## Phase 6: User Story 4 — Resolve Invalid or Uncertain Outcomes (Priority: P2)

**Goal**: Direct-response races, stale versions, same-intent replays, and recoverable failures produce one authoritative outcome and preserve actionable Admin context.

**Independent Test**: Race candidate direct accept/decline against Admin external acceptance, replay the same and different evidence, force audit/save failure, and simulate a lost response; verify one state, one matching audit, and safe reload/retry behavior.

### Tests for User Story 4

- [ ] T030 [P] [US4] Add failing SQL/API tests for same Admin/normalized-reason replay, different-reason conflict, stale version, direct accept/decline races, one-Active index conflict, audit rollback, inactive Admin, unknown targets, and safe errors in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderExternalAcceptances/ChiefLeaderExternalAcceptanceTests.cs`
- [ ] T031 [P] [US4] Add failing Angular tests for conflict/current-state reload, uncertain outcome, preserved reason, safe same-intent retry, and expired session in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.spec.ts`
- [ ] T032 [P] [US4] Add failing Playwright tests for uncertain submit recovery, direct-response winner display, and no duplicate acceptance in `tests/e2e/chief-leader-external-acceptance.spec.ts`

### Implementation for User Story 4

- [ ] T033 [US4] Implement matching-audit same-intent replay and distinct stale/direct/opposite/team/index conflict outcomes in `src/Infrastructure/Persistence/EfChiefLeaderExternalAcceptanceStore.cs`
- [ ] T034 [US4] Complete endpoint mapping for replay, validation, not-found, forbidden, stale, invalid-state, team-conflict, and unexpected outcomes in `src/Carpediem.Web/Endpoints/ChiefLeaderAppointmentEndpoints.cs`
- [ ] T035 [US4] Implement preserved-input uncertain outcome, context reload, same-intent retry, and session-expiry handling in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.ts` and `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.html`

**Checkpoint**: Every invalid, stale, failed, replayed, or concurrent attempt has one safe and testable outcome.

---

## Phase 7: Polish, Accessibility, Performance, and Convergence

**Purpose**: Close cross-cutting requirements and prove spec-to-implementation convergence.

- [ ] T036 [P] Add axe, keyboard, visible-focus, 200% text, reduced-motion, 44px target, stable-layout, and 320/360/390/430 no-overflow checks in `tests/e2e/chief-leader-external-acceptance.spec.ts`
- [ ] T037 [P] Add a SQL-backed 100-user external-acceptance/replay workload and generated OpenAPI assertions in `tests/performance/chief-leader-external-acceptance.k6.js` and `tests/performance/run-chief-leader-external-acceptance.ps1`
- [ ] T038 Run Release build, domain tests, SQL Server 2025 integration, Angular tests/build, full Playwright, generated OpenAPI, k6, and `git diff --check`; record fresh evidence in `specs/004-chief-leader-admin-acceptance/implementation-review.md`
- [ ] T039 Audit routes, OpenAPI, UI, migrations, notification types, and changed files to verify SLICE-05 adds no direct response, publication, replacement, staffing, inbox, delivery control, or external-channel integration; record the result in `specs/004-chief-leader-admin-acceptance/implementation-review.md`
- [ ] T040 Perform requirement-to-evidence convergence for FR-001–FR-021 and all 15 acceptance scenarios, resolve any gaps, and mark completed tasks in `specs/004-chief-leader-admin-acceptance/tasks.md`

---

## Dependencies and Execution Order

### Phase Dependencies

- **Phase 1** starts immediately.
- **Phase 2** depends on Phase 1 and blocks all user story mutations.
- **US1** depends on the existing SLICE-03 Admin context plus Phase 2 types.
- **US2** depends on Phase 2 and supplies the only state-changing operation.
- **US3** can begin after the US1 form shell exists; it does not depend on a successful mutation.
- **US4** depends on the US2 mutation and recovery read.
- **Phase 7** depends on all stories.

### User Story Dependencies

```text
Foundation ──┬── US1 Review ──┬── US3 Discard
             └── US2 Record ──┴── US4 Recovery/Races
                                      ↓
                                 Convergence
```

### Within Each User Story

1. Add the listed failing test and observe the intended failure.
2. Implement domain/application behavior before persistence and endpoint mapping.
3. Implement client service before component mutation behavior.
4. Pass focused tests before broader regression gates.

## Parallel Opportunities

- T002 and T003 can run in parallel.
- T004, T006, T008, and T009 touch independent foundational files.
- T010 and T011 can be written in parallel; T012 can proceed alongside them.
- T015–T017 are independent failing-test tasks.
- T026 and T027 are independent; T030–T032 are independent.
- T036 and T037 can be prepared in parallel before the final sequential gate.

## Parallel Example: User Story 2

```text
Task T015: SQL/API acceptance, authorization, boundary, provenance, and audit tests
Task T016: Angular service contract tests
Task T017: Angular form and validation tests
```

## Implementation Strategy

### MVP First

1. Complete setup and foundation.
2. Complete US1 so the Admin understands the exact Pending appointment and action.
3. Complete US2 to deliver the auditable business outcome.
4. Validate US1 and US2 independently before recovery refinements.

### Incremental Completion

1. Add US3 discard protection.
2. Add US4 replay, concurrency, and uncertain-outcome recovery.
3. Run all accessibility, load, contract, scope, and convergence gates.

## Notes

- `[P]` means the task can run concurrently because it owns different files or only adds an independent test.
- Every external-acceptance write must keep assignment and mandatory audit evidence atomic.
- Do not log, emit in notification context, or expose the justification beyond authorized audit use.
- Do not add a migration unless implementation disproves the plan's existing-schema finding; such a discovery requires plan correction before proceeding.
- Keep `logo.pdf` and the unrelated DOCX untracked and untouched.
