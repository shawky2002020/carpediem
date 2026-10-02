# Implementation Plan: SLICE-12 — Event Reschedule & Staffing Cycle Rotation

**Feature Branch**: `codex/011-event-reschedule-cycle-rotation`  
**Date**: 2026-10-02  
**Status**: Ready for Execution  

---

## 1. Architectural Strategy & Modules

### Backend Domain Layer (`Modules/Events/Domain/`)
- Define domain exceptions in `Modules/Events/Domain/Events/`:
  - `EventRescheduleWindowClosedException.cs`
  - `InvalidEventStatusException.cs` (if not already in Events namespace)
- Enhance `Event` aggregate:
  - Add `Reschedule(DateTimeOffset newStartDateTime, DateTimeOffset newEndDateTime, Guid actorUserId, DateTimeOffset rescheduledAt)` method.
  - Enforce status invariant (`Status == Published`).
  - Enforce start time invariant (`StartDateTime > rescheduledAt`).
  - Enforce new schedule validation (`newStartDateTime > rescheduledAt`, `newEndDateTime > newStartDateTime`, UTC offset).
  - Execute cycle rotation: mark current cycle historical, create next cycle with incremented cycle number and effective event date.
- Add domain unit tests in `tests/Carpediem.Domain.Tests/Events/EventRescheduleTests.cs`.

### Application & Infrastructure Layer
- Notification domain & application:
  - Add `EventRescheduledType` and `EventRescheduled(...)` factory to `InPlatformNotification.cs`.
  - Create `IEventRescheduledNotificationWriter` interface and `EfEventRescheduledNotificationWriter` implementation.
- Audit logging:
  - Add `EventRescheduled(...)` factory to `AuditLog.cs`.
- Application contract (`Modules/Events/Application/Reschedule/`):
  - `IEventRescheduleStore.cs` interface.
  - DTOs: `RescheduleEventCommand`, `RescheduleEventResult`.
- Persistence implementation (`Infrastructure/Persistence/`):
  - `EfEventRescheduleStore.cs` implementing `IEventRescheduleStore`.
  - Atomically update old cycle (`IsCurrent = 0`), save, insert new cycle (`IsCurrent = 1`), update event schedule, query distinct stakeholders from old cycle, stage notifications, stage audit log, and commit transaction.
- Service registration:
  - Register `IEventRescheduleStore` and `IEventRescheduledNotificationWriter` in `ServiceCollectionExtensions.cs`.

### Web & Observability Layer (`Carpediem.Web/`)
- Telemetry:
  - `EventRescheduleTelemetry.cs` in `Observability/` with ActivitySource and metrics (`rescheduled`, `invalid_event_status`, `window_closed`, `concurrency_conflict`, `unauthorized_administrator`).
- Minimal API Endpoints:
  - `EventRescheduleEndpoints.cs` in `Endpoints/`:
    - `POST /api/events/{eventId}/reschedule`
    - Authorization: `RequireRole(CarpediemRoles.Admin)`.
    - Antiforgery: `RequireAntiforgeryTokenAttribute`.
    - Concurrency: `If-Match` header validation.
    - Status codes: 200, 400, 401, 403, 404, 409, 412, 500.
  - Wire endpoint mapping and OpenAPI metadata in `Program.cs`.

### Frontend Layer (`frontend/src/app/features/event-reschedule/`)
- Model & Service:
  - `data/event-reschedule.model.ts`
  - `data/event-reschedule.service.ts` and `.spec.ts`
- Reschedule Dialog Component:
  - `components/reschedule-dialog/reschedule-dialog.component.ts`
  - Template with accessible datetime-local inputs, justification textarea, validation errors, and live regions.
  - SCSS strictly under the 4.00 kB budget, responsive down to 320px, 44px min touch targets.
  - Unit tests in `reschedule-dialog.component.spec.ts`.

---

## 2. Testing & Verification Strategy

1. **Domain Unit Tests**: Verify reschedule logic, cycle incrementation, historical flag transition, and exception guards.
2. **Containerized SQL Server 2025 Integration Tests**: Test transactional cycle rotation, filtered index concurrency, notification generation, audit trail, role authorization, and concurrency conflict detection.
3. **Frontend Vitest Specs**: Validate service API interaction and component state management.
4. **Production Build**: Verify `npm run build` succeeds with zero errors and CSS within budgets.
5. **Playwright E2E & WCAG 2.2 AA**: Validate full user flow, 0 axe accessibility violations, responsive 320px layout, 44px touch targets, and 200% font zoom reflow across all 5 viewports.
6. **k6 Performance Workload**: Run 100-VU load test via `run-event-reschedule.ps1` ensuring sub-second p95 latency.
