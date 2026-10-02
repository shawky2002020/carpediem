# Tasks: SLICE-12 — Event Reschedule & Staffing Cycle Rotation

**Input**: Design documents from `specs/011-event-reschedule-cycle-rotation/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contract](contracts/event-reschedule.openapi.yaml), [quickstart.md](quickstart.md)

**Tests**: Required by the feature specification and constitution. Write each listed test first and observe the intended failure before implementation.

**Organization**: Tasks are grouped by user story and ordered so each increment has independently reviewable evidence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on another incomplete task.
- **[Story]**: Maps the task to a user story in `spec.md`.
- Every task names the exact target file or directory.

---

## Phase 1: Setup, Contracts, Telemetry, and Notification Alignment

**Purpose**: Establish OpenAPI configuration, telemetry, notification contracts, and audit helpers.

- [x] T001 Record the pre-task requirements quality gate in `specs/011-event-reschedule-cycle-rotation/checklists/feature-quality.md`
- [x] T002 [P] Add event reschedule operation metadata and required headers to generated OpenAPI configuration in `src/Carpediem.Web/Program.cs`
- [x] T003 [P] Create structured outcome telemetry for event reschedule in `src/Carpediem.Web/Observability/EventRescheduleTelemetry.cs`
- [x] T004 [P] Add `EventRescheduled` notification factory in `src/Modules/Notifications/Domain/InPlatformNotification.cs` and writer contract in `src/Modules/Notifications/Application/IEventRescheduledNotificationWriter.cs`

---

## Phase 2: Domain Layer Invariants & Unit Tests

**Purpose**: Encapsulate domain rules for event reschedule, temporal validation, and staffing cycle rotation.

- [x] T005 [P] Add domain exceptions (`EventRescheduleWindowClosedException`, `InvalidEventStatusForRescheduleException`) in `src/Modules/Events/Domain/Events/`
- [x] T006 Implement `Reschedule` aggregate method on `Event` in `src/Modules/Events/Domain/Events/Event.cs`
- [x] T007 Author domain unit tests for reschedule invariants and cycle rotation in `tests/Carpediem.Domain.Tests/Events/EventRescheduleTests.cs`
- [x] T008 Execute domain unit tests via `dotnet test tests/Carpediem.Domain.Tests/` to verify domain behavior

---

## Phase 3: Application Store & Persistence Layer

**Purpose**: Establish `IEventRescheduleStore.cs`, audit log helpers, notification writers, and transactional persistence.

- [x] T009 [P] Add audit log helper for `EventRescheduled` in `src/Infrastructure/Audit/AuditLog.cs`
- [x] T010 [P] Define application store contract (`IEventRescheduleStore.cs`) and DTOs in `src/Modules/Events/Application/Reschedule/`
- [x] T011 [P] Implement notification writer `EfEventRescheduledNotificationWriter` in `src/Infrastructure/Notifications/EfEventRescheduledNotificationWriter.cs`
- [x] T012 Implement `EfEventRescheduleStore` in `src/Infrastructure/Persistence/EfEventRescheduleStore.cs` handling atomic cycle rotation, stakeholder notification staging, and audit logging
- [x] T013 Register `IEventRescheduleStore` and `IEventRescheduledNotificationWriter` in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs`

---

## Phase 4: Web API Endpoints & Observability

**Purpose**: Expose minimal API endpoint with Admin authorization, antiforgery, `If-Match` parsing, and problem responses.

- [x] T014 Implement `POST /api/events/{eventId}/reschedule` in `src/Carpediem.Web/Endpoints/EventRescheduleEndpoints.cs`
- [x] T015 Map endpoints in `src/Carpediem.Web/Program.cs` and wire up middleware

---

## Phase 5: Container Integration Tests (SQL Server 2025)

**Purpose**: Verify end-to-end database transactions, filtered unique index safety, cycle rotation, authorization, notifications, and concurrency.

- [x] T016 Author comprehensive integration tests in `tests/Carpediem.Api.IntegrationTests/Events/EventRescheduleTests.cs`
- [x] T017 Execute SQL integration suite via `powershell -ExecutionPolicy Bypass -File tests/run-sql-integration.ps1 -ContainerTestHost`

---

## Phase 6: Frontend Models, Service, Dialog Component & Vitest Tests

**Purpose**: Build accessible, responsive Angular reschedule UI under the 4.00 kB style budget.

- [x] T018 Define TypeScript models in `frontend/src/app/features/event-reschedule/data/event-reschedule.model.ts`
- [x] T019 Implement and test `EventRescheduleService` in `frontend/src/app/features/event-reschedule/data/event-reschedule.service.ts` and `.spec.ts`
- [x] T020 Implement reschedule modal dialog component in `frontend/src/app/features/event-reschedule/components/reschedule-dialog/`
- [x] T021 Author component unit tests in `frontend/src/app/features/event-reschedule/components/reschedule-dialog/reschedule-dialog.component.spec.ts`
- [x] T022 Execute `npm test -- --no-watch` and `npm run build` in `frontend/`

---

## Phase 7: Playwright E2E & WCAG 2.2 AA Tests

**Purpose**: Test user experience, modal focus trap, responsive reflow, 44px touch targets, and accessibility across 5 viewports.

- [x] T023 Author Playwright E2E test in `tests/e2e/event-reschedule.spec.ts`
- [x] T024 Run Playwright test suite across all 5 viewports with 0 axe violations

---

## Phase 8: k6 Performance Workload Benchmark (100 VUs)

**Purpose**: Benchmark reschedule under high concurrency against SQL Server 2025.

- [x] T025 Author k6 benchmark script `tests/performance/event-reschedule.k6.js` and launcher `tests/performance/run-event-reschedule.ps1`
- [x] T026 Execute benchmark and export results to `artifacts/performance/011-event-reschedule.json`

---

## Phase 9: Review, Verification & Merge

**Purpose**: Complete implementation review, verify clean git status, and merge into base branch.

- [x] T027 Complete `specs/011-event-reschedule-cycle-rotation/implementation-review.md`
- [x] T028 Update `specs/011-event-reschedule-cycle-rotation/checklists/feature-quality.md` to 100% complete
- [x] T029 Commit all changes to `codex/011-event-reschedule-cycle-rotation` and fast-forward merge into `codex/005-event-publication`
