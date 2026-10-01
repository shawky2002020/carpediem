# Quickstart: Validate SLICE-06 — Event Publication & Publishing Prerequisite Invariant

## Prerequisites

Implemented SLICE-02 through SLICE-05 host and SQL Server 2025 schema; an active Operations Admin; a Draft Event with an Active Chief Leader assignment; .NET 10, Node 24, Playwright browsers, and Docker.

## Validation Steps

1. **Domain Tests**: Run `dotnet test tests/Carpediem.Domain.Tests`. Verify that `Event.Publish` enforces `Status == Draft`, exactly one `Active` Chief Leader assignment, valid UTC time window (`Start < End`), and sets `Status = Published`.
2. **API Integration Tests**: Run `powershell -File tests/run-sql-integration.ps1 -ContainerTestHost`. Verify:
   - Operations Admin authorization and non-admin denial (403).
   - Prerequisite invariant enforcement: 422 if no Chief Leader, or Chief Leader is Pending or Declined.
   - Successful transition to `Published` with updated rowversion.
   - Atomic insertion of exactly one `EventPublished` audit record.
   - Atomic creation of in-platform notifications for eligible Leaders.
   - Optimistic concurrency: 409 on stale `If-Match`.
   - Idempotent replay: 200 with `Idempotent-Replay: true` on duplicate submission without duplicate audit/notifications.
   - Rejection of draft editing once published (409).
3. **Angular Unit & Component Tests**: Run `npm test -- --watch=false` in `frontend`. Verify:
   - Publishing readiness indicator (enabled when Active Chief Leader is present, disabled/withheld when absent or non-active).
   - Confirmation dialog workflow and cancellation.
   - Single in-flight submission guard.
   - Status update to Published upon success.
4. **Playwright E2E & Accessibility**: Run Playwright suite across 320/360/390/430px viewports and desktop. Verify:
   - Minimum 44x44px touch targets.
   - Zero horizontal overflow on 320px viewport.
   - Screen reader announcements for progress and success.
   - Keyboard focus management inside the confirmation dialog.
5. **OpenAPI & Performance**: Verify OpenAPI generation matches `contracts/event-publication.openapi.yaml` and verify performance metrics under concurrency.
