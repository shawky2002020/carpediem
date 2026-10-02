# Implementation Plan: SLICE-14 — Event Scheduled Completion & Administrative Closeout

**Branch**: `codex/012-event-completion-closeout`  
**Milestone**: M3: Completion, Attendance, Profiling & Dashboards (EPIC-02)  

---

## 1. Architectural Strategy

SLICE-14 implements the authoritative state transition from `Published` to terminal `Completed` for events whose scheduled window has concluded or whose physical gathering has wrapped up.

### Key Architectural Pillars:
1. **Domain Encapsulation**: Transition executed via `Event.Complete(...)` enforcing that only `Published` events can be completed, preserving all existing assignments and cycles.
2. **Native Hosted Service (ADR-007)**: `EventLifecycleBackgroundWorker` registered as `IHostedService` executing periodic, multi-replica safe scans without external job dependencies.
3. **Admin Closeout Endpoint**: `POST /api/events/{eventId}/complete` secured with Admin role authorization, antiforgery token, and optimistic concurrency (`If-Match`).
4. **Decoupled Attendance Invariant**: Zero dependency on attendance finalization; unrecorded attendance naturally projects as `Pending Attendance` for dashboards.
5. **Multi-Replica Concurrency**: Database compare-and-transition and EF Core rowversion concurrency detection ensures losing workers or concurrent closeout calls safely no-op.

---

## 2. Phased Implementation Roadmap

### Phase 1: Setup, Telemetry, Options & Contracts
- **Artifacts**:
  - `src/Carpediem.Web/Observability/EventCompletionTelemetry.cs`
  - `src/Carpediem.Web/Workers/EventLifecycleWorkerOptions.cs`
  - OpenAPI metadata in `src/Carpediem.Web/Program.cs`
- **Output**: Verified build with registered telemetry and options.

### Phase 2: Domain Layer Invariants & Unit Tests
- **Artifacts**:
  - `src/Modules/Events/Domain/Events/InvalidEventStatusForCompletionException.cs`
  - `src/Modules/Events/Domain/Events/Event.cs` (`Complete(...)` method)
  - `tests/Carpediem.Domain.Tests/Events/EventCompletionTests.cs`
- **Output**: 100% passing domain unit tests.

### Phase 3: Application Store & Persistence Layer
- **Artifacts**:
  - `src/Infrastructure/Audit/AuditLog.cs` (`EventCompleted` factory)
  - `src/Modules/Events/Application/Completion/IEventCompletionStore.cs`
  - `src/Infrastructure/Persistence/EfEventCompletionStore.cs`
  - Service registration in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs`
- **Output**: Atomic transactional store handling both background scans and manual closeouts with audit logging.

### Phase 4: Hosted Background Worker & Web API Endpoints
- **Artifacts**:
  - `src/Carpediem.Web/Workers/EventLifecycleBackgroundWorker.cs`
  - `src/Carpediem.Web/Endpoints/EventCompletionEndpoints.cs`
  - Registration and mapping in `src/Carpediem.Web/Program.cs`
- **Output**: Running background service and HTTP endpoint with OpenAPI and security bindings.

### Phase 5: Container Integration Tests (SQL Server 2025)
- **Artifacts**:
  - `tests/Carpediem.Api.IntegrationTests/Events/EventCompletionTests.cs`
- **Scenarios Tested**:
  1. Background worker completes past published events and ignores future events.
  2. Worker safely no-ops on repeated scans.
  3. Admin manual closeout succeeds and returns 200 OK with ETag.
  4. Non-Admin closeout rejected with 403 Forbidden.
  5. Draft / Cancelled events rejected with 409 Conflict.
  6. Stale `If-Match` rejected with 412 Precondition Failed.
  7. Idempotent replay on already completed event returns 200 OK + `Idempotent-Replay: true`.
  8. Concurrent execution collision handled safely without errors.
  9. Team assignments remain active and attendance records are decoupled.

### Phase 6: Frontend Models, Service, Modal Dialog & Vitest Tests
- **Artifacts**:
  - `frontend/src/app/features/event-completion/data/event-completion.model.ts`
  - `frontend/src/app/features/event-completion/data/event-completion.service.ts` & `.spec.ts`
  - `frontend/src/app/features/event-completion/components/complete-event-dialog/`
  - Route in `frontend/src/app/app.routes.ts`
- **Output**: 100% passing Vitest specs and clean production build (< 4.00 kB styles).

### Phase 7: Playwright E2E & WCAG 2.2 AA Tests
- **Artifacts**:
  - `tests/e2e/event-completion.spec.ts`
- **Scenarios**: Admin views and confirms closeout, validates accessibility across all 5 viewports with 0 axe violations.

### Phase 8: k6 High-Concurrency Workload Benchmark (100 VUs)
- **Artifacts**:
  - `tests/performance/event-completion.k6.js`
  - `tests/performance/run-event-completion.ps1`
- **Output**: `artifacts/performance/012-event-completion.json` meeting 0% error thresholds.

### Phase 9: Sign-Off, Quality Checklist & Fast-Forward Merge
- **Artifacts**:
  - `specs/012-event-completion-closeout/implementation-review.md`
  - Completed `checklists/feature-quality.md`
- **Output**: Fast-forward merge into `codex/005-event-publication`.
