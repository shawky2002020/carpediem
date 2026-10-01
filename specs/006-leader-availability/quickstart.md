# Quickstart: Validate SLICE-07 — Leader Availability Declaration

## Prerequisites

- Implemented SLICE-01 through SLICE-06 with SQL Server 2025 container running.
- At least one `Published` Event in the future (`StartDateTime > UtcNow`).
- Seed accounts: at least 1 Leader (`leader1`), 1 Chief Leader (`chiefleader1`), and 1 dual-role Chief Leader with Leader capability.
- .NET 10 SDK, Node 24, Playwright browsers, and Docker.

---

## Validation Steps

1. **Domain Unit Tests**:
   - Run `dotnet test tests/Carpediem.Domain.Tests`.
   - Verify `AvailabilityResponse` entity invariants: response must be `Available` or `NotAvailable`, window must be open, cycle and user must be specified.
   - Verify CHANGE-001 domain guard: active Chief Leader on the event cannot declare availability.

2. **API & SQL Server 2025 Integration Tests**:
   - Run `powershell -File tests/run-sql-integration.ps1 -ContainerTestHost`.
   - Verify:
     - Authentication & authorization: Leader role required (403 for unprivileged users).
     - Published event constraint: 409 Conflict if Event is in `Draft`, `Completed`, or `Cancelled`.
     - Window constraint: 409 Conflict if `Event.StartDateTime <= UtcNow`.
     - CHANGE-001 exclusivity: 409 Conflict if user is active Chief Leader on this Event.
     - Successful creation (201 Created) and update (200 OK) with rowversion tracking.
     - Unique constraint: at most 1 response per user per cycle in `staffing.AvailabilityResponses`.
     - Idempotent replay: 200 OK with `Idempotent-Replay: true` header on identical re-submission without extra audit log.
     - Atomic audit log: `Action = 'AvailabilityDeclared'`.

3. **Angular Unit & Component Tests**:
   - Run `npm test -- --watch=false` in `frontend`.
   - Verify:
     - Availability card renders "Response Needed", "Available", or "Not Available" badge accurately.
     - "Mark Available" and "Mark Not Available" trigger HTTP requests with antiforgery and `If-Match`.
     - Single in-flight submission guard disables actions while pending.
     - Active Chief Leader view displays "You are the Chief Leader for this event" banner and disables actions.

4. **Playwright E2E & WCAG 2.2 AA Accessibility**:
   - Run Playwright suite across 320px, 360px, 390px, 430px mobile viewports and desktop.
   - Verify:
     - Minimum 44x44px touch targets for availability actions.
     - Zero horizontal overflow at 320px width and at 200% font scaling.
     - ARIA `role="status"` polite announcements on status change.
     - Zero axe-core accessibility violations.

5. **Performance Benchmark**:
   - Run k6 load test simulating 100 concurrent Leaders submitting availability declarations.
   - Verify 0 errors, 100% threshold success, and $P_{95} < 300\text{ms}$.
