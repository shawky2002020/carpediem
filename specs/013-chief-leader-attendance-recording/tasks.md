# Tasks: SLICE-15 — Chief Leader On-Site Attendance Recording

**Input**: Design documents from `specs/013-chief-leader-attendance-recording/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/attendance.openapi.yaml](contracts/attendance.openapi.yaml), [quickstart.md](quickstart.md)

**Organization**: Tasks are grouped by phase and ordered so each increment has independently reviewable evidence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on another incomplete task.
- **[Story]**: Maps the task to a user story in `spec.md`.
- Every task names the exact target file or directory.

---

## Phase 1: Setup, Telemetry & OpenAPI Configuration

**Purpose**: Establish OpenAPI configuration and telemetry for attendance recording.

- [x] T001 Record the pre-task requirements quality gate in `specs/013-chief-leader-attendance-recording/checklists/feature-quality.md`
- [x] T002 [P] Create structured outcome telemetry in `src/Carpediem.Web/Observability/AttendanceTelemetry.cs`
- [x] T003 [P] Register telemetry in `src/Carpediem.Web/Observability/ObservabilityConfiguration.cs`
- [x] T004 Add OpenAPI documentation metadata in `src/Carpediem.Web/Program.cs`

---

## Phase 2: Domain Layer & Unit Tests

**Purpose**: Encapsulate domain rules for attendance outcomes and validation.

- [x] T005 Create `Carpediem.Modules.Attendance.csproj` under `src/Modules/Attendance/` and add to `Carpediem.slnx`
- [x] T006 Implement `AttendanceOutcome` aggregate and `AttendanceOutcomeValue` in `src/Modules/Attendance/Domain/AttendanceOutcome.cs`
- [x] T007 Author domain unit tests in `tests/Carpediem.Domain.Tests/Attendance/AttendanceOutcomeTests.cs`
- [x] T008 Execute domain unit tests in container to verify domain invariants

---

## Phase 3: Application Store, Persistence & Database Migration

**Purpose**: Establish `IAttendanceStore`, EF Core configurations, migration, and persistence.

- [x] T009 [P] Add audit log helper for `AttendanceFinalized` in `src/Infrastructure/Audit/AuditLog.cs`
- [x] T010 [P] Define application store contract `IAttendanceStore.cs` and DTOs in `src/Modules/Attendance/Application/`
- [x] T011 Create EF Core entity configuration `AttendanceOutcomeConfiguration.cs` in `src/Infrastructure/Persistence/Configurations/`
- [x] T012 Register `DbSet<AttendanceOutcome>` and module configuration in `CarpediemDbContext.cs`
- [x] T013 Generate EF Core migration `AddAttendanceOutcomes` for table `attendance.AttendanceOutcomes`
- [x] T014 Implement `EfAttendanceStore.cs` in `src/Infrastructure/Persistence/EfAttendanceStore.cs`
- [x] T015 Register `IAttendanceStore` in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs`

---

## Phase 4: Web API Endpoints

**Purpose**: Expose minimal API endpoints with Chief Leader and Admin authorization, antiforgery validation, and Problem Details.

- [x] T016 Implement `AttendanceEndpoints.cs` in `src/Carpediem.Web/Endpoints/AttendanceEndpoints.cs`
- [x] T017 Map attendance endpoints in `src/Carpediem.Web/Program.cs`

---

## Phase 5: Container Integration Tests (SQL Server 2025)

**Purpose**: Verify end-to-end database transactions, roll sheet retrieval, roll call finalization, authorization, and audit logging against SQL Server 2025.

- [x] T018 Author comprehensive integration tests in `tests/Carpediem.Api.IntegrationTests/Attendance/AttendanceRecordingTests.cs`
- [x] T019 Execute SQL integration suite via `powershell -ExecutionPolicy Bypass -File tests/run-sql-integration.ps1 -ContainerTestHost`

---

## Phase 6: Frontend Models, Service, Roll Sheet Component & Vitest Tests

**Purpose**: Build accessible, mobile-first Angular roll sheet UI under the 4.00 kB style budget.

- [x] T020 Define TypeScript models in `frontend/src/app/features/attendance/data/attendance.model.ts`
- [x] T021 Implement and test `AttendanceService` in `frontend/src/app/features/attendance/data/attendance.service.ts` and `.spec.ts`
- [x] T022 Implement `AttendanceRollSheetComponent` in `frontend/src/app/features/attendance/components/attendance-roll-sheet/`
- [x] T023 Author component unit tests in `frontend/src/app/features/attendance/components/attendance-roll-sheet/attendance-roll-sheet.component.spec.ts`
- [x] T024 Register attendance route in `frontend/src/app/app.routes.ts`
- [x] T025 Execute `npm test -- --no-watch` and `npm run build` in `frontend/`

---

## Phase 7: Playwright E2E & WCAG 2.2 AA Tests

**Purpose**: Test user experience, touch toggles, responsive reflow, 44px touch targets, and accessibility across 5 viewports.

- [x] T026 Author Playwright E2E test in `tests/e2e/attendance-recording.spec.ts`
- [x] T027 Run Playwright test suite across all 5 viewports with 0 axe violations

---

## Phase 8: k6 Performance Workload Benchmark (100 VUs)

**Purpose**: Benchmark attendance roll call finalization under high concurrency against SQL Server 2025.

- [x] T028 Author k6 benchmark script `tests/performance/chief-leader-attendance.k6.js` and launcher `tests/performance/run-chief-leader-attendance.ps1`
- [x] T029 Execute benchmark and export results to `artifacts/performance/013-chief-leader-attendance.json`

---

## Phase 9: Review, Verification & Merge

**Purpose**: Complete implementation review, verify clean git status, and merge into base branch.

- [x] T030 Complete `specs/013-chief-leader-attendance-recording/implementation-review.md`
- [x] T031 Update `specs/013-chief-leader-attendance-recording/checklists/feature-quality.md` to 100% complete
- [x] T032 Commit all changes to `codex/013-chief-leader-attendance-recording` and fast-forward merge into `codex/005-event-publication`
