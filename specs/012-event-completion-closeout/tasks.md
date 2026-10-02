# Tasks: SLICE-14 — Event Scheduled Completion & Administrative Closeout

**Input**: Design documents from `specs/012-event-completion-closeout/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contract](contracts/event-completion.openapi.yaml), [quickstart.md](quickstart.md)

**Organization**: Tasks are grouped by phase and ordered so each increment has independently reviewable evidence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on another incomplete task.
- **[Story]**: Maps the task to a user story in `spec.md`.
- Every task names the exact target file or directory.

---

## Phase 1: Setup, Contracts, Telemetry & Worker Options

**Purpose**: Establish OpenAPI configuration, telemetry, and worker options.

- [x] T001 Record the pre-task requirements quality gate in `specs/012-event-completion-closeout/checklists/feature-quality.md`
- [x] T002 [P] Add event completion operation metadata and required headers to generated OpenAPI configuration in `src/Carpediem.Web/Program.cs`
- [x] T003 [P] Create structured outcome telemetry for event completion in `src/Carpediem.Web/Observability/EventCompletionTelemetry.cs`
- [x] T004 [P] Create worker options class in `src/Carpediem.Web/Workers/EventLifecycleWorkerOptions.cs`

---

## Phase 2: Domain Layer Invariants & Unit Tests

**Purpose**: Encapsulate domain rules for event completion and status guards.

- [x] T005 [P] Add domain exception `InvalidEventStatusForCompletionException` in `src/Modules/Events/Domain/Events/InvalidEventStatusForCompletionException.cs`
- [x] T006 Implement `Complete` aggregate method on `Event` in `src/Modules/Events/Domain/Events/Event.cs`
- [x] T007 Author domain unit tests for event completion in `tests/Carpediem.Domain.Tests/Events/EventCompletionTests.cs`
- [x] T008 Execute domain unit tests in container to verify domain invariants

---

## Phase 3: Application Store & Persistence Layer

**Purpose**: Establish `IEventCompletionStore`, audit log helpers, and transactional persistence.

- [x] T009 [P] Add audit log helper for `EventCompleted` in `src/Infrastructure/Audit/AuditLog.cs`
- [x] T010 [P] Define application store contract (`IEventCompletionStore.cs`) and DTOs in `src/Modules/Events/Application/Completion/`
- [x] T011 Implement `EfEventCompletionStore` in `src/Infrastructure/Persistence/EfEventCompletionStore.cs` handling scheduled batch completion and manual closeout with idempotent replay
- [x] T012 Register `IEventCompletionStore` in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs`

---

## Phase 4: Hosted Background Worker & Web API Endpoints

**Purpose**: Implement native `BackgroundService` and expose minimal API endpoint with Admin authorization, antiforgery, and `If-Match` parsing.

- [x] T013 Implement `EventLifecycleBackgroundWorker` in `src/Carpediem.Web/Workers/EventLifecycleBackgroundWorker.cs`
- [x] T014 Implement `POST /api/events/{eventId}/complete` in `src/Carpediem.Web/Endpoints/EventCompletionEndpoints.cs`
- [x] T015 Register hosted service, worker options, and map endpoints in `src/Carpediem.Web/Program.cs`

---

## Phase 5: Container Integration Tests (SQL Server 2025)

**Purpose**: Verify end-to-end database transactions, background worker scanning, manual closeout, authorization, concurrency tokens, idempotent replay, and audit logging.

- [x] T016 Author comprehensive integration tests in `tests/Carpediem.Api.IntegrationTests/Events/EventCompletionTests.cs`
- [x] T017 Execute SQL integration suite via `powershell -ExecutionPolicy Bypass -File tests/run-sql-integration.ps1 -ContainerTestHost`

---

## Phase 6: Frontend Models, Service, Dialog Component & Vitest Tests

**Purpose**: Build accessible, responsive Angular closeout UI under the 4.00 kB style budget.

- [x] T018 Define TypeScript models in `frontend/src/app/features/event-completion/data/event-completion.model.ts`
- [x] T019 Implement and test `EventCompletionService` in `frontend/src/app/features/event-completion/data/event-completion.service.ts` and `.spec.ts`
- [x] T020 Implement closeout confirmation dialog component in `frontend/src/app/features/event-completion/components/complete-event-dialog/`
- [x] T021 Author component unit tests in `frontend/src/app/features/event-completion/components/complete-event-dialog/complete-event-dialog.component.spec.ts`
- [x] T022 Register closeout route in `frontend/src/app/app.routes.ts`
- [x] T023 Execute `npm test -- --no-watch` and `npm run build` in `frontend/`

---

## Phase 7: Playwright E2E & WCAG 2.2 AA Tests

**Purpose**: Test user experience, confirmation dialog focus trap, responsive reflow, 44px touch targets, and accessibility across 5 viewports.

- [x] T024 Author Playwright E2E test in `tests/e2e/event-completion.spec.ts`
- [x] T025 Run Playwright test suite across all 5 viewports with 0 axe violations

---

## Phase 8: k6 Performance Workload Benchmark (100 VUs)

**Purpose**: Benchmark manual closeout and scheduled completion under high concurrency against SQL Server 2025.

- [x] T026 Author k6 benchmark script `tests/performance/event-completion.k6.js` and launcher `tests/performance/run-event-completion.ps1`
- [x] T027 Execute benchmark and export results to `artifacts/performance/012-event-completion.json`

---

## Phase 9: Review, Verification & Merge

**Purpose**: Complete implementation review, verify clean git status, and merge into base branch.

- [x] T028 Complete `specs/012-event-completion-closeout/implementation-review.md`
- [x] T029 Update `specs/012-event-completion-closeout/checklists/feature-quality.md` to 100% complete
- [x] T030 Commit all changes to `codex/012-event-completion-closeout` and fast-forward merge into `codex/005-event-publication`
