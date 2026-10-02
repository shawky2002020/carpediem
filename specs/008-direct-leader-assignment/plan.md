# Implementation Plan: SLICE-09 — Direct Leader Assignment

## 1. Architecture Overview
- **Domain**: `TeamAssignment` aggregate root created in SLICE-08 is reused.
- **Application**:
  - `IDirectAssignmentStore` interface in `src/Modules/Staffing/Application/DirectAssignment/`.
  - Notification writer and audit log helpers for `DirectLeaderAssigned`.
- **Infrastructure**:
  - `EfDirectAssignmentStore` implementing `IDirectAssignmentStore`.
  - Queries all users in role `Leader`, filters out active Chief Leader on event, filters out already assigned leaders in active cycle, maps availability status (`NotAvailable` or `NoResponse`).
  - Atomic transaction: insert `TeamAssignment`, insert `InPlatformNotification`, insert `AuditLog`.
- **Web API**:
  - `EventDirectAssignmentEndpoints.cs` mapping `GET /api/events/{eventId}/direct-assignment/candidates` and `POST /api/events/{eventId}/direct-assignment`.
  - Authorization: `RequireChiefLeaderOrAdmin`. Antiforgery enforcement. Structured telemetry.
- **Frontend**:
  - Models: `DirectAssignmentCandidateItem`, `DirectAssignmentCandidatesResponse`, `DirectAssignLeaderRequest`.
  - Service: `DirectAssignmentService` (or integrated into `TeamSelectionService`).
  - Component: Enhance `TeamSelectionComponent` to render direct draft pool tab/section, allowing Chief Leader or Admin to directly assign non-respondents and `NotAvailable` leaders.
  - Mobile-first, 44px touch targets, zero layout shift, ARIA live region.
- **Testing**:
  - Domain tests (if new domain methods added).
  - SQL Server 2025 container integration tests (`tests/Carpediem.Api.IntegrationTests/Staffing/EventDirectAssignmentTests.cs`).
  - Angular unit tests (`team-selection.service.spec.ts` and `team-selection.component.spec.ts`).
  - Playwright E2E and WCAG 2.2 AA accessibility tests (`tests/e2e/direct-assignment.spec.ts`).
  - k6 100-VU concurrent workload (`tests/performance/direct-assignment.k6.js` and `tests/performance/run-direct-assignment.ps1`).
