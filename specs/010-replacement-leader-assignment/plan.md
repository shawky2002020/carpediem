# Implementation Plan: SLICE-11 — Replacement Leader Assignment

**Feature Branch**: `codex/010-replacement-leader-assignment`  
**Date**: 2026-10-02  
**Status**: Ready for Execution  

---

## 1. Architectural Strategy & Modules

### Backend
- **Staffing Domain**: Ensure `TeamAssignment` creation invariants support replacement assignments while preserving past cancellations.
- **Application Layer (`Modules/Staffing/Application/ReplacementAssignment/`)**:
  - `IReplacementAssignmentStore.cs` interface.
  - Data transfer records: `ReplacementAssignmentContextResult`, `AssignReplacementResult`.
- **Infrastructure Persistence (`Infrastructure/Persistence/`)**:
  - `EfReplacementAssignmentStore.cs` implementing `IReplacementAssignmentStore`.
  - Transactional replacement assignment:
    1. Verify Event is `Published`.
    2. Verify Event start time is in future (`Event.StartDateTime > UtcNow`).
    3. Verify Chief Leader assignment: actor must be active Chief Leader or Admin.
    4. Verify candidate is not active Chief Leader (`chief_leader_conflict`).
    5. Check if candidate already has an active assignment in current cycle (`IsIdempotentReplay = true`).
    6. If `replacedAssignmentId` is provided, verify it belongs to this event and has `Status == 'Cancelled'`.
    7. Insert new `TeamAssignment` (`Status = 'Active'`).
    8. Insert `InPlatformNotification` (`ReplacementLeaderAssigned`).
    9. Append `AuditLog` (`ReplacementLeaderAssigned`).
    10. Commit transaction.
- **Endpoints (`Carpediem.Web/Endpoints/EventReplacementAssignmentEndpoints.cs`)**:
  - `GET /api/events/{eventId}/replacement-assignment`
  - `POST /api/events/{eventId}/replacement-assignment`
  - OpenAPI operation metadata and headers (`X-XSRF-TOKEN`, `Idempotent-Replay`).
- **Telemetry (`Carpediem.Web/Observability/ReplacementAssignmentTelemetry.cs`)**:
  - ActivitySource and Meter tracking operations (`context_queried`, `assigned`, `replay`, `conflict`, `forbidden`).

### Frontend
- **Feature Module (`frontend/src/app/features/replacement-assignment/`)**:
  - `data/replacement-assignment.model.ts`
  - `data/replacement-assignment.service.ts` and `.spec.ts`
  - `components/replacement-assignment.component.ts`, `.html`, `.scss`, `.spec.ts`
  - Route: `/events/:eventId/replacement-assignment` (guarded by `leaderGuard`).

---

## 2. Test Plan
- **Integration Tests**: `tests/Carpediem.Api.IntegrationTests/Staffing/EventReplacementAssignmentTests.cs` (covering queries, assignment, admin override, eligibility denials, 409 past-start, and idempotent replays).
- **Frontend Unit Tests**: Vitest covering service queries, mutation calls, and component interactions.
- **Playwright E2E**: `tests/e2e/replacement-leader-assignment.spec.ts` (covering roster rendering, candidate selection, live announcements, and WCAG 2.2 AA).
- **k6 Performance**: `tests/performance/replacement-leader-assignment.k6.js` (100 VUs concurrent replacement).
