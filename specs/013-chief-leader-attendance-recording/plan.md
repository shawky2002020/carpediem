# Implementation Plan: SLICE-15 — Chief Leader On-Site Attendance Recording

## 1. Plan Overview
This phased plan delivers on-site attendance roll call recording for Chief Leaders across domain, persistence, API, frontend, testing, and performance layers in accordance with ADR-003, ADR-005, ADR-009, and SEQ-008.

---

## 2. Phases & Milestones

### Phase 1: Setup, Telemetry & OpenAPI Configuration
- Create OpenTelemetry instrumentation (`AttendanceTelemetry.cs`) in `src/Carpediem.Web/Observability/`.
- Register telemetry in `ObservabilityConfiguration.cs`.
- Register OpenAPI operation metadata in `Program.cs`.

### Phase 2: Domain Layer & Unit Tests
- Create `src/Modules/Attendance/Carpediem.Modules.Attendance.csproj` (or add Attendance aggregate inside appropriate module if lightweight, or dedicated module per ADR-009).
- Define `AttendanceOutcomeValue` and `AttendanceOutcome` aggregate root.
- Author domain unit tests in `tests/Carpediem.Domain.Tests/Attendance/AttendanceOutcomeTests.cs`.
- Verify in container (100% pass).

### Phase 3: Persistence, EF Core Mapping & Database Migration
- Add `AuditLog.AttendanceFinalized` in `src/Infrastructure/Audit/AuditLog.cs`.
- Create `AttendanceOutcomeConfiguration.cs` in `src/Infrastructure/Persistence/Configurations/` targeting schema `attendance`.
- Add `DbSet<AttendanceOutcome> AttendanceOutcomes` to `CarpediemDbContext`.
- Generate EF Core migration (`AddAttendanceOutcomes`) creating table `attendance.AttendanceOutcomes`.
- Define `IAttendanceStore` in application layer and implement `EfAttendanceStore` in `src/Infrastructure/Persistence/`.
- Register `IAttendanceStore` in `ServiceCollectionExtensions.cs`.

### Phase 4: Web API Endpoints
- Implement `AttendanceEndpoints.cs` in `src/Carpediem.Web/Endpoints/`:
  - `GET /api/events/{eventId}/attendance`: Get roll sheet.
  - `POST /api/events/{eventId}/attendance`: Finalize roll call.
- Map endpoints in `Program.cs`.

### Phase 5: Container Integration Tests (SQL Server 2025)
- Author integration tests in `tests/Carpediem.Api.IntegrationTests/Attendance/AttendanceRecordingTests.cs`:
  - Roll sheet retrieval (Published & Completed events).
  - 403 Forbidden for non-Chief Leaders.
  - 409 Conflict for Draft and Cancelled events.
  - Immediate finalization with `Attended` and `NoShow`.
  - Idempotent subsequent roll call submissions.
  - Decoupled completion: attendance recording post-completion.
  - Audit log verification.
- Execute integration tests via `run-sql-integration.ps1 -ContainerTestHost`.

### Phase 6: Frontend Models, Service, Roll Sheet Component & Vitest Tests
- TypeScript models in `frontend/src/app/features/attendance/data/attendance.model.ts`.
- `AttendanceService` in `frontend/src/app/features/attendance/data/attendance.service.ts` and `.spec.ts`.
- `AttendanceRollSheetComponent` in `frontend/src/app/features/attendance/components/attendance-roll-sheet/` (TS, HTML, SCSS). Keep SCSS strictly under the 4.00 kB budget.
- Register route `/events/:eventId/attendance` protected by `chiefLeaderGuard`.
- Execute `npm test -- --no-watch` and `npm run build` in `frontend/`.

### Phase 7: Playwright E2E & WCAG 2.2 AA Accessibility Suite
- Author `tests/e2e/attendance-recording.spec.ts`.
- Execute across all 5 viewports with 0 axe violations.

### Phase 8: k6 High-Concurrency Workload Benchmark (100 VUs)
- Author `tests/performance/chief-leader-attendance.k6.js` and `tests/performance/run-chief-leader-attendance.ps1`.
- Execute against containerized SQL Server 2025 and export `artifacts/performance/013-chief-leader-attendance.json`.

### Phase 9: Sign-Off, Commit & Merge
- Author `implementation-review.md`.
- Update `feature-quality.md` and `tasks.md` to 100% complete.
- Commit to `codex/013-chief-leader-attendance-recording` and fast-forward merge into `codex/005-event-publication`.
