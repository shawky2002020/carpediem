# Quality Gate Checklist: SLICE-12 — Event Reschedule & Staffing Cycle Rotation

**Feature Branch**: `codex/011-event-reschedule-cycle-rotation`  
**Created**: 2026-10-02  
**Status**: Complete (100% Passed)  

---

## 1. Requirements Completeness Gate

- [x] **Authorized Persona Boundary**: Restricted exclusively to Platform Administrators (`CarpediemRoles.Admin`). Non-administrators, including Chief Leaders and volunteer leaders, receive 403 Forbidden (`unauthorized_administrator`).
- [x] **Event Lifecycle Invariant**: Reschedule operations are strictly permitted on `Published` events. Events in `Draft` return 409 Conflict (`invalid_event_status` — must use draft edit endpoints). Events in `Completed` or `Cancelled` return 409 Conflict (`invalid_event_status`).
- [x] **Temporal Window Invariant**: The event's current start time must be in the future (`Event.StartDateTime > UtcNow`). If the event scheduled window has already started or elapsed, returns 409 Conflict (`window_closed`).
- [x] **New Schedule Invariants**:
  - `NewStartDateTime > UtcNow` (cannot reschedule into the past).
  - `NewEndDateTime > NewStartDateTime` (end time must be strictly after start time).
  - Both timestamps must resolve to valid UTC instants (`Offset == TimeSpan.Zero`).
- [x] **Mandatory Administrative Justification**: Admin must provide an administrative rationale between 10 and 500 characters explaining the reschedule reason.
- [x] **Staffing Cycle Rotation Invariant**:
  - The current `StaffingCycle` record is sealed and marked historical (`IsCurrent = false`).
  - A new `StaffingCycle` record is created with `CycleNumber = old.CycleNumber + 1`, `IsCurrent = true`, and `EffectiveEventDate = DateOnly.FromDateTime(NewStartDateTime.Date)`.
  - Historical `staffing.AvailabilityResponses` and `staffing.TeamAssignments` remain tied immutably to the sealed `StaffingCycleId`.
  - The newly active cycle starts completely clean: zero availability responses and zero team assignments.
- [x] **Event Entity Update & Concurrency**:
  - `Event.StartDateTime` and `Event.EndDateTime` are updated to the new schedule.
  - `Event.Status` remains `Published`.
  - Optimistic concurrency is enforced via the `If-Match` header against `Event.RowVersion`. Stale tokens trigger 412 Concurrency Conflict (`concurrency_conflict`).
- [x] **Atomic Side-Effects & Notifications**:
  - Executed inside a single SQL Server atomic transaction.
  - High-priority in-platform notifications (`EventRescheduled`) dispatched to previous active roster members, the active Chief Leader, and candidates who declared availability in the sealed cycle.
  - Immutable audit log entry recorded (`Action = 'EventRescheduled'`) capturing previous schedule, new schedule, actor Admin ID, and administrative justification.
- [x] **Strict Scope Boundary**:
  - Event cancellation (`SLICE-13`) and completion closeout (`SLICE-14`) are strictly deferred.
  - Untracked `logo.pdf` is never touched.

---

## 2. Technical Quality & Architecture Gate

- [x] **Clean Architecture & Domain Encapsulation**: Domain transition encapsulated in `Event.Reschedule(...)` and `StaffingCycle.CreateNext(...)`. Store contract in `Modules/Events/Application/Reschedule/`. EF Core implementation in `Infrastructure/Persistence/`.
- [x] **Filtered Index Concurrency Protection**: SQL Server filtered unique index `UX_StaffingCycles_CurrentPerEvent` (`[IsCurrent] = 1`) protected by updating the historical cycle before inserting the new active cycle within the same transaction.
- [x] **Structured Observability**: Dedicated OpenTelemetry ActivitySource and Meter (`EventRescheduleTelemetry`) tracking requests, status conflicts, and concurrency collisions.
- [x] **Security & Antiforgery**: Antiforgery validation enforced via `RequireAntiforgeryTokenAttribute`.
- [x] **SQL Server 2025 Test Compatibility**: All integration tests target containerized SQL Server 2025 (`run-sql-integration.ps1`).
- [x] **Accessibility & Mobile-First**: Responsive layout down to 320px, 44px min touch targets, 200% font zoom reflow without clipping, trapped focus in reschedule dialog, and `aria-live="polite"` feedback.
- [x] **Performance Budget**: Component SCSS strictly under the 4.00 kB budget. k6 concurrent load test passes with 100 concurrent VUs.

---

## 3. Verification & Evidence Sign-Off

- [x] Domain Unit Tests: `Carpediem.Domain.Tests` covering reschedule invariants, validation, and cycle rotation (82/82 passed).
- [x] Integration Tests: `Carpediem.Api.IntegrationTests` covering SQL Server transaction, cycle rotation, authorization, conflict codes, concurrency, notifications, and audit logging (149/149 passed).
- [x] Frontend Unit Tests: Vitest specs covering Angular reschedule service and dialog component (79/79 passed).
- [x] Production Build: `npm run build` succeeds with 0 errors and 0 warnings.
- [x] Playwright E2E & WCAG 2.2 AA: Playwright tests passing across all 5 viewports with 0 axe violations (20/20 passed).
- [x] k6 Load Workload: 100-VU concurrent benchmark meeting performance thresholds (`artifacts/performance/011-event-reschedule.json`).
- [x] Git Tree Hygiene: `git status` clean (except untracked `logo.pdf`), no leftover debug files.
