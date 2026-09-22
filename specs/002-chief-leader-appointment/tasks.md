---
description: "Actionable task list for SLICE-03 Chief Leader appointment and notification"
---

# Tasks: SLICE-03 — Chief Leader Appointment & Notification

**Input**: Design documents from `specs/002-chief-leader-appointment/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/chief-leader-appointment.openapi.yaml`, `quickstart.md`

**Tests**: Required by the constitution and the feature acceptance criteria. Write each focused test before its implementation and observe the expected failure.

**Organization**: Tasks are grouped by user story. Shared schema and transaction support are foundational because every story depends on the same authoritative appointment state.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes separate files without depending on an incomplete task
- **[Story]**: Maps the task to the matching user story in `spec.md`
- Every task names its target file

## Phase 1: Setup

**Purpose**: Add the minimal module and test structure needed for this slice.

- [x] T001 Add `src/Modules/Notifications/Carpediem.Modules.Notifications.csproj` and register it in `Carpediem.slnx`
- [x] T002 Add Notifications project references to `src/Infrastructure/Carpediem.Infrastructure.csproj` and `src/Carpediem.Web/Carpediem.Web.csproj`
- [x] T003 [P] Create appointment application/domain folders under `src/Modules/Events/Application/ChiefLeaderAppointments/` and `src/Modules/Events/Domain/ChiefLeaderAssignments/`
- [x] T004 [P] Create frontend feature folders under `frontend/src/app/features/chief-leader-appointment/`
- [x] T005 [P] Create focused integration-test folders under `tests/Carpediem.Api.IntegrationTests/ChiefLeaderAppointments/`

---

## Phase 2: Foundational Data and Contracts

**Purpose**: Establish assignment, notification, retry, and cross-module contracts that block all stories.

**Critical**: Complete this phase before user-story implementation.

- [x] T006 [P] Add `ChiefLeaderAssignmentStatus` with exactly `Pending`, `Active`, and `Declined` in `src/Modules/Events/Domain/ChiefLeaderAssignments/ChiefLeaderAssignmentStatus.cs`
- [x] T007 [P] Add the locked assignment fields, UTC and identity guards, Pending factory, and rowversion to `src/Modules/Events/Domain/ChiefLeaderAssignments/ChiefLeaderAssignment.cs`
- [x] T008 [P] Add persistent unread `ChiefLeaderAppointment` intent fields and bounded context rules to `src/Modules/Notifications/Domain/InPlatformNotification.cs`
- [x] T009 [P] Add durable `(ActorUserId, RequestToken)` request identity, fingerprint, result assignment, and UTC creation fields to `src/Modules/Events/Infrastructure/Persistence/ChiefLeaderAppointmentRequest.cs`
- [x] T010 [P] Define candidate and eligibility query contracts in `src/Modules/Identity/Application/IChiefLeaderCandidateDirectory.cs`
- [x] T011 [P] Define appointment notification staging contract in `src/Modules/Notifications/Application/IAppointmentNotificationWriter.cs`
- [x] T012 Define appointment persistence/read/result abstractions and typed exceptions in `src/Modules/Events/Application/ChiefLeaderAppointments/IChiefLeaderAppointmentStore.cs`
- [x] T013 [P] Map assignment checks, `NO ACTION` FKs, rowversion, unique Pending Event index, and unique Active Event index in `src/Modules/Events/Infrastructure/Persistence/Configurations/ChiefLeaderAssignmentConfiguration.cs`
- [x] T014 [P] Map notification type length 50, context length 500, unread time check, and `NO ACTION` FKs in `src/Modules/Notifications/Infrastructure/Persistence/Configurations/InPlatformNotificationConfiguration.cs`
- [x] T015 [P] Map request key uniqueness, 32-byte fingerprint, result FK, and `NO ACTION` FKs in `src/Modules/Events/Infrastructure/Persistence/Configurations/ChiefLeaderAppointmentRequestConfiguration.cs`
- [x] T016 Register assignment, request, and notification sets plus Identity/Event relationships in `src/Infrastructure/Persistence/CarpediemDbContext.cs`
- [x] T017 Register candidate, appointment, and notification services in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs`
- [x] T018 Generate the additive SLICE-03 EF migration in `src/Infrastructure/Persistence/Migrations/` and verify it contains no destructive SLICE-02 changes
- [x] T019 Generate and inspect `artifacts/migrations/002-chief-leader-appointment.sql` for all FKs, checks, filtered indexes, unique request key, and rollback operations

**Checkpoint**: The database can represent one unresolved appointment, its notification, audit evidence, and retry identity without later-slice behavior.

---

## Phase 3: User Story 1 — Appoint a Chief Leader (Priority: P1) MVP

**Goal**: An Admin commits one Pending appointment, audit record, and notification intent while the Event remains Draft.

**Independent Test**: Appoint an eligible candidate to an unassigned Draft and verify all four persisted records and unchanged Draft state.

### Tests

- [x] T020 [P] [US1] Add failing domain tests for Pending creation, actor/time guards, and unchanged Event Draft state in `tests/Carpediem.Domain.Tests/Events/ChiefLeaderAppointmentTests.cs`
- [x] T021 [P] [US1] Add failing SQL integration tests for successful all-or-none assignment, audit, notification, and request persistence in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderAppointments/AppointChiefLeaderTests.cs`
- [x] T022 [P] [US1] Add failing API contract tests for POST success, headers, response shape, and OpenAPI coverage in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderAppointments/ChiefLeaderAppointmentContractTests.cs`
- [x] T023 [P] [US1] Add failing Angular service tests for POST token reuse and typed response in `frontend/src/app/features/chief-leader-appointment/data/chief-leader-appointment.service.spec.ts`
- [x] T024 [P] [US1] Add failing page tests for selection, confirmation, saving, success, and abandonment in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.spec.ts`

### Implementation

- [x] T025 [US1] Add `Event.AppointChiefLeader` lifecycle guard and collection relationship in `src/Modules/Events/Domain/Events/Event.cs`
- [x] T026 [P] [US1] Add bounded `ChiefLeaderAppointed` audit construction in `src/Infrastructure/Audit/ChiefLeaderAppointmentAuditWriter.cs`
- [x] T027 [P] [US1] Implement notification staging in `src/Infrastructure/Notifications/EfAppointmentNotificationWriter.cs`
- [x] T028 [US1] Implement idempotent atomic appointment persistence and replay handling in `src/Infrastructure/Persistence/EfChiefLeaderAppointmentStore.cs`
- [x] T029 [US1] Implement the save-time appointment orchestration and error outcomes in `src/Modules/Events/Application/ChiefLeaderAppointments/AppointChiefLeaderHandler.cs`
- [x] T030 [US1] Add Admin/antiforgery POST mapping and safe ProblemDetails codes in `src/Carpediem.Web/Endpoints/ChiefLeaderAppointmentEndpoints.cs`
- [x] T031 [US1] Register the new endpoint in `src/Carpediem.Web/Program.cs`
- [x] T032 [P] [US1] Implement typed POST and stable request-token state in `frontend/src/app/features/chief-leader-appointment/data/chief-leader-appointment.service.ts`
- [x] T033 [US1] Implement the semantic single-selection appointment page in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.ts`
- [x] T034 [US1] Implement labeled candidate choices, visible confirmation, status regions, and cancel behavior in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.html`
- [x] T035 [US1] Implement narrow-screen layout, 44×44 targets, stable status space, focus, contrast, and reduced-motion styling in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.scss`
- [x] T036 [US1] Add the guarded appointment route and link from Draft editing in `frontend/src/app/app.routes.ts` and `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.html`

**Checkpoint**: User Story 1 is independently operable and testable.

---

## Phase 4: User Story 2 — Find an Eligible Chief Leader (Priority: P1)

**Goal**: The Admin sees active Chief Leader members only, including dual-role users, and save-time validation handles stale eligibility.

**Independent Test**: Compare active/inactive, Chief-Leader/Leader-only, and dual-role users in candidate reads and appointment submissions.

### Tests

- [x] T037 [P] [US2] Add failing candidate-query and save-time eligibility tests in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderAppointments/ChiefLeaderCandidateTests.cs`
- [x] T038 [P] [US2] Add failing authorization and candidate-privacy tests for Admin, Chief Leader, Leader, inactive Admin, and anonymous sessions in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderAppointments/ChiefLeaderAppointmentAuthorizationTests.cs`
- [x] T039 [P] [US2] Add failing Angular tests for loading, empty-pool, distinguishing label, and ineligible-candidate recovery in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.spec.ts`

### Implementation

- [x] T040 [US2] Implement active Chief Leader and dual-role membership queries in `src/Modules/Identity/Infrastructure/EfChiefLeaderCandidateDirectory.cs`
- [x] T041 [US2] Implement appointment-context read and authoritative candidate revalidation in `src/Infrastructure/Persistence/EfChiefLeaderAppointmentStore.cs`
- [x] T042 [US2] Add Admin-only GET mapping and privacy-bounded candidate DTOs in `src/Carpediem.Web/Endpoints/ChiefLeaderAppointmentEndpoints.cs`
- [x] T043 [US2] Add typed GET handling and empty/error recovery states in `frontend/src/app/features/chief-leader-appointment/data/chief-leader-appointment.service.ts` and `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.ts`

**Checkpoint**: Candidate visibility and authoritative eligibility are independently verifiable.

---

## Phase 5: User Story 3 — Review the Pending Appointment (Priority: P2)

**Goal**: Reopening the Draft shows its current Pending candidate and blocks another appointment without changing lifecycle state.

**Independent Test**: Reopen an appointed Draft and verify Pending identity/status, duplicate rejection, and declined-history preservation.

### Tests

- [x] T044 [P] [US3] Add failing integration tests for reopened Pending state, second appointment conflict, and a new row after declined history in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderAppointments/ReviewChiefLeaderAppointmentTests.cs`
- [x] T045 [P] [US3] Add failing page tests for textual Pending status and disabled appointment controls in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.spec.ts`

### Implementation

- [x] T046 [US3] Complete current unresolved assignment projection with candidate and opaque version in `src/Infrastructure/Persistence/EfChiefLeaderAppointmentStore.cs`
- [x] T047 [US3] Render current Pending appointment and prevent second selection in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.html`
- [x] T048 [US3] Surface the current Chief Leader appointment entry point/state on Draft edit in `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.ts` and `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.html`

**Checkpoint**: User Story 3 is independently verifiable without response or publication actions.

---

## Phase 6: User Story 4 — Handle Access and Appointment Failures (Priority: P2)

**Goal**: Every denied, missing, invalid, stale, concurrent, repeated, or recoverable outcome is safe and preserves committed state.

**Independent Test**: Exercise each defined failure and verify stable feedback with no partial or duplicate rows.

### Tests

- [x] T049 [P] [US4] Add failing integration tests for malformed/missing/non-Draft Event, invalid token, candidate ineligibility, and forced rollback in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderAppointments/ChiefLeaderAppointmentFailureTests.cs`
- [x] T050 [P] [US4] Add failing replay, changed-payload, same-candidate concurrency, and competing-candidate tests in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderAppointments/ChiefLeaderAppointmentIdempotencyTests.cs`
- [x] T051 [P] [US4] Add failing Playwright appointment success, failure, retry, access, and duplicate-submit scenarios in `tests/e2e/chief-leader-appointment.spec.ts`

### Implementation

- [x] T052 [US4] Complete unique-conflict resolution, replay recovery, transaction rollback, and typed exceptions in `src/Infrastructure/Persistence/EfChiefLeaderAppointmentStore.cs`
- [x] T053 [US4] Complete stable 400/401/403/404/409/422/500 mappings without sensitive detail in `src/Carpediem.Web/Endpoints/ChiefLeaderAppointmentEndpoints.cs`
- [x] T054 [US4] Preserve selection/token and present actionable conflict, authorization, not-found, and recoverable errors in `frontend/src/app/features/chief-leader-appointment/pages/appointment/appointment.component.ts` and `.html`

**Checkpoint**: All four user stories and failure paths are independently testable.

---

## Phase 7: Polish and Cross-Cutting Verification

**Purpose**: Close contracts, accessibility, performance, migration, and traceability gates.

- [x] T055 [P] Add keyboard, axe, 200% text, reduced-motion, mobile-width, and no-horizontal-scroll coverage in `tests/e2e/chief-leader-appointment-accessibility.spec.ts`
- [x] T056 [P] Add V1-scale candidate-read and concurrent appointment integrity checks in `tests/performance/chief-leader-appointment.k6.js`
- [x] T057 [P] Export generated host OpenAPI to `artifacts/openapi/generated-chief-leader-appointment.json` and compare it with `specs/002-chief-leader-appointment/contracts/chief-leader-appointment.openapi.yaml`
- [x] T058 Review structured logs, traces, and counters for safe identifiers and outcome categories in `src/Carpediem.Web/Observability/ObservabilityConfiguration.cs` and appointment code
- [x] T059 Run the complete .NET build, domain tests, real SQL integration suite, Angular tests, Playwright matrix, k6 workload, migration SQL inspection, and `specs/002-chief-leader-appointment/quickstart.md`
- [x] T060 Validate FR-001–FR-027 and all 16 acceptance scenarios, record evidence and scope review in `specs/002-chief-leader-appointment/implementation-review.md`

---

## Dependencies and Execution Order

### Phase Dependencies

- Phase 1 starts immediately.
- Phase 2 depends on project registration from Phase 1 and blocks all stories.
- US1 and US2 are both P1, but US1 implementation requires candidate eligibility from the foundational contract; their tests can be written in parallel.
- US3 depends on the US1 appointment result.
- US4 depends on the same command/read surfaces and then hardens every failure path.
- Phase 7 depends on all selected stories.

### User Story Dependencies

- **US1**: Foundation → Pending business result.
- **US2**: Foundation → candidate directory; can be verified separately through GET and rejected save.
- **US3**: US1 → current Pending projection and review.
- **US4**: US1 + US2 read/write surfaces → complete failure, retry, and conflict behavior.

### Parallel Opportunities

- T003–T005, T006–T011, and T013–T015 change separate files.
- Each story's test files marked `[P]` can be authored together before implementation.
- T026/T027 and T032 can proceed after their contracts exist.
- T055–T057 are independent after core behavior stabilizes.

## Implementation Strategy

1. Complete the additive schema and contracts.
2. Deliver the US1 transaction as the MVP and prove all-or-none persistence.
3. Complete US2 candidate discovery and save-time revalidation.
4. Add US3 review without introducing response actions.
5. Harden US4 concurrency, retry, authorization, and failure behavior.
6. Run the full cross-cutting gate and record traceability.

## Notes

- Keep `logo.pdf` untracked and untouched.
- Do not add acceptance, decline, external acceptance, publication, team staffing, or notification inbox operations.
- Mark tasks complete only after their file change and specified evidence exist.
