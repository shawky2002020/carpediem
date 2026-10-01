# Implementation Plan: SLICE-08 — Chief Leader Team Selection from Available Pool

**Branch**: `codex/007-chief-leader-team-selection` | **Module**: `Carpediem.Modules.Staffing`

---

## 1. Architecture Overview & Component Responsibilities

SLICE-08 implements Chief Leader team selection from the available volunteer pool for a published event.

```
[ Angular UI: TeamSelectionComponent ]
          │
          │ GET /api/events/{id}/team-selection
          │ POST /api/events/{id}/team-selection
          ▼
[ ASP.NET Core: EventTeamSelectionEndpoints ]
          │ (RequireChiefLeaderOrAdmin policy + Antiforgery)
          ▼
[ EfTeamSelectionStore : ITeamSelectionStore ]
          │
          ├──> Verify Event Status == Published
          ├──> Verify Temporal Window (StartDateTime > UtcNow)
          ├──> Verify Acting User is Active Chief Leader or Admin
          ├──> Verify Candidate is Not the Active Chief Leader (CHANGE-001)
          ├──> Verify Candidate has Response == Available in current StaffingCycle
          │
          ├──> [Atomic SQL Server Transaction (RCSI)]
          │      ├── Insert staffing.TeamAssignments (Status = 'Active')
          │      ├── Insert notifications.InPlatformNotifications (VolunteerSelected)
          │      └── Insert audit.AuditLogs (VolunteerSelected)
          ▼
[ Microsoft SQL Server 2025 ]
```

---

## 2. Technical Decisions & Invariant Implementations

1. **Authorization**:
   - `RequireChiefLeaderOrAdmin` authorization policy. A resource authorization check or store-level verification ensures the acting user is the event's assigned active Chief Leader (`ChiefLeaderAssignment.Status == Active` and `ChiefLeaderAssignment.UserId == actorUserId`) or holds the `Admin` role.
2. **Domain Aggregate**:
   - `TeamAssignment` in `src/Modules/Staffing/Domain/TeamSelection/`.
   - Enum `TeamAssignmentStatus`: `Active`, `Cancelled`.
   - Domain guards: `CandidateNotAvailableException`, `ChiefLeaderExclusivityException`, `InvalidEventStatusException`, `TeamSelectionWindowClosedException`.
3. **Database Migration**:
   - Migration `AddTeamAssignments` creating `staffing.TeamAssignments`.
   - Filtered unique index `UX_TeamAssignments_ActiveLeaderPerCycle` on `(StaffingCycleId, UserId)` WHERE `[Status] = 'Active'`.
   - Non-clustered index `IX_TeamAssignments_Event_Status` on `(EventId, Status)`.
4. **Idempotency & Replay**:
   - Replaying selection of an already active team member returns HTTP 200 OK with `Idempotent-Replay: true` header.
5. **Observability**:
   - Structured telemetry: `TeamSelectionTelemetry` tracking selection counts, rejection reasons, and durations.

---

## 3. Test Pyramid Strategy

1. **Domain Tests** (`tests/Carpediem.Domain.Tests/Staffing/`):
   - `TeamAssignmentTests`: entity creation, UTC invariant, status assertions, guard exceptions.
2. **Integration Tests** (`tests/Carpediem.Api.IntegrationTests/Staffing/`):
   - `EventTeamSelectionTests`: testing Chief Leader query, successful selection, CHANGE-001 exclusivity rejection, candidate not available rejection, non-chief leader 403 rejection, idempotent replay, atomic audit & notification insertion.
3. **Frontend Unit Tests** (`frontend/src/app/features/team-selection/`):
   - Service and component tests for data retrieval, selection triggers, optimistic UI updates, error banners.
4. **Playwright E2E & A11y Tests** (`tests/e2e/team-selection.spec.ts`):
   - Tests across 5 viewports (320px, 360px, 390px, 430px, desktop) with axe-core audit, 44px touch targets, 200% font zoom reflow.
5. **Performance Benchmark** (`tests/performance/team-selection.k6.js`):
   - 100 concurrent selection / roster query operations, verifying zero errors and $P_{95} < 300\text{ms}$.
