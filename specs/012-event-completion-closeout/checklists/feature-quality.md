# Quality Gate Checklist: SLICE-14 — Event Scheduled Completion & Administrative Closeout

**Feature Branch**: `codex/012-event-completion-closeout`  
**Created**: 2026-10-02  
**Status**: Complete (100% Passed)  

---

## 1. Requirements Completeness Gate

- [x] **Dual Completion Paths**:
  - **Scheduled System Completion**: Native ASP.NET Core `BackgroundService` discovers `Published` events where `UtcNow >= EndDateTime` and transitions them to `Completed`.
  - **Administrative Closeout**: Operations Administrator (`CarpediemRoles.Admin`) executes manual closeout via `POST /api/events/{eventId}/complete`.
- [x] **Authorized Persona Boundary**:
  - Admin closeout endpoint is strictly restricted to Platform Administrators (`CarpediemRoles.Admin`). Non-administrators (Chief Leaders, volunteer leaders) receive 403 Forbidden (`unauthorized_administrator`).
- [x] **Event Lifecycle Invariant**:
  - Target event MUST be in `Status = Published`.
  - Draft events cannot be completed; returns 409 Conflict (`invalid_event_status`).
  - Cancelled events cannot be completed; returns 409 Conflict (`invalid_event_status`).
  - Terminal state: once `Completed`, an event cannot transition to any other status.
- [x] **Decoupled from Attendance (Core Architectural Invariant)**:
  - Event completion operates strictly independent of whether on-site attendance (`SLICE-15`) has been recorded.
  - Absence of attendance records post-completion dynamically produces the derived operational condition `Pending Attendance` for dashboards (`SLICE-19`).
- [x] **Multi-Replica Safe Background Worker (ADR-007)**:
  - Implements native `Microsoft.Extensions.Hosting.BackgroundService`.
  - Configurable scan interval with cooperative `CancellationToken` shutdown.
  - Safe compare-and-transition semantics: if another replica or an Admin already completed the event, the worker safely no-ops without error.
- [x] **Optimistic Concurrency & Idempotency**:
  - Admin closeout validates `If-Match` against `Event.RowVersion`. Stale token triggers 412 Precondition Failed (`concurrency_conflict`).
  - Repeating closeout on an already completed event with identical version returns 200 OK with `Idempotent-Replay: true`.
- [x] **Atomic Auditing & Zero Notifications**:
  - Appends an immutable audit log entry (`Action = 'EventCompleted'`), capturing actor (Admin User ID or `System.EventLifecycleWorker`), previous status (`Published`), and new status (`Completed`).
  - Zero notifications dispatched on completion per ADR-006 & V1_FEATURE_SLICES.md.
- [x] **Strict Scope Boundary**:
  - On-site attendance recording (`SLICE-15`) and attendance correction (`SLICE-16`) are deferred.
  - Untracked `logo.pdf` is never touched.

---

## 2. Technical Quality & Architecture Gate

- [x] **Clean Architecture & Domain Encapsulation**:
  - Domain logic encapsulated in `Event.Complete(...)` inside `Modules/Events/Domain/Events/Event.cs`.
  - Application store contract in `Modules/Events/Application/Completion/IEventCompletionStore.cs`.
  - EF Core implementation in `Infrastructure/Persistence/EfEventCompletionStore.cs`.
- [x] **Hosted Background Worker**:
  - `EventLifecycleBackgroundWorker` registered as `IHostedService` in `Carpediem.Web`.
  - Uses `IServiceScopeFactory` to manage scoped database dependencies cleanly.
- [x] **Structured Observability**:
  - Dedicated OpenTelemetry ActivitySource and Meter (`EventCompletionTelemetry`) tracking background scans, transitions, manual closeouts, and concurrency collisions.
- [x] **Security & Antiforgery**:
  - Antiforgery validation enforced via `RequireAntiforgeryTokenAttribute` on HTTP closeout endpoint.
- [x] **SQL Server 2025 Test Compatibility**:
  - Integration tests verified against containerized SQL Server 2025 (`run-sql-integration.ps1`).
- [x] **Accessibility & Mobile-First**:
  - Admin closeout dialog/button accessible down to 320px viewport, 44px min touch targets, 200% font zoom reflow, and polite live region status.
- [x] **Performance Budget**:
  - Component SCSS strictly under the 4.00 kB budget.
  - k6 load test executes with 100 concurrent VUs.

---

## 3. Verification & Evidence Sign-Off

- [x] Domain Unit Tests: `Carpediem.Domain.Tests` covering `Event.Complete(...)` invariants and status transitions (87/87 passed).
- [x] Integration Tests: `Carpediem.Api.IntegrationTests` covering SQL Server transactions, background worker scanning, manual closeout, authorization, concurrency tokens, idempotent replay, and audit logging (156/156 passed).
- [x] Frontend Unit Tests: Vitest specs covering Angular closeout service and component (86/86 passed).
- [x] Production Build: `npm run build` succeeds with 0 errors and 0 warnings.
- [x] Playwright E2E & WCAG 2.2 AA: Playwright tests passing across all 5 viewports with 0 axe violations (20/20 passed).
- [x] k6 Load Workload: 100-VU concurrent benchmark meeting performance thresholds (`artifacts/performance/012-event-completion.json`, 1500/1500 checks passed).
- [x] Git Tree Hygiene: `git status` clean (except untracked `logo.pdf`), no leftover debug files.
