# Tasks: SLICE-08 — Chief Leader Team Selection from Available Pool

**Input**: Design documents from `specs/007-chief-leader-team-selection/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contract](contracts/team-selection.openapi.yaml), [quickstart.md](quickstart.md)

**Tests**: Required by the feature specification and constitution. Write each listed test first and observe the intended failure before implementation.

**Organization**: Tasks are grouped by user story and ordered so each increment has independently reviewable evidence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on another incomplete task.
- **[Story]**: Maps the task to a user story in `spec.md`.
- Every task names the exact target file or directory.

---

## Phase 1: Setup, Entity Mapping, and Contract Alignment

**Purpose**: Establish OpenAPI configuration, telemetry, notification contracts, and audit helpers.

- [x] T001 Record the pre-task requirements quality gate in `specs/007-chief-leader-team-selection/checklists/feature-quality.md`
- [x] T002 [P] Add team selection operation metadata and required headers to generated OpenAPI configuration in `src/Carpediem.Web/Program.cs`
- [x] T003 [P] Create structured outcome telemetry for team selection in `src/Carpediem.Web/Observability/TeamSelectionTelemetry.cs`
- [x] T004 [P] Add `VolunteerSelected` notification domain method and writer contract in `src/Modules/Notifications/Domain/InPlatformNotification.cs` and `src/Modules/Notifications/Application/IVolunteerSelectedNotificationWriter.cs`

---

## Phase 2: Foundational Domain, Persistence, and Store Contracts

**Purpose**: Establish `TeamAssignment` aggregate, EF Core mapping, migration, and store abstraction.

**⚠️ CRITICAL**: User story implementation begins only after this phase is green.

- [x] T005 [P] Add failing domain unit tests for `TeamAssignment` creation, status transitions, and guard exceptions in `tests/Carpediem.Domain.Tests/Staffing/TeamAssignmentTests.cs`
- [x] T006 Implement `TeamAssignment` aggregate root, `TeamAssignmentStatus` enum, and guard exceptions (`CandidateNotAvailableException`, `TeamSelectionWindowClosedException`, `AlreadyAssignedException`) in `src/Modules/Staffing/Domain/TeamSelection/`
- [x] T007 [P] Define team selection store contracts (`ITeamSelectionStore.cs`) in `src/Modules/Staffing/Application/TeamSelection/ITeamSelectionStore.cs`
- [x] T008 [P] Add audit log helper for `VolunteerSelected` in `src/Infrastructure/Audit/AuditLog.cs`
- [x] T009 Configure `TeamAssignment` entity mapping (`staffing.TeamAssignments`, filtered unique index on `(StaffingCycleId, UserId)` WHERE `Status = 'Active'`) in `src/Infrastructure/Persistence/CarpediemDbContext.cs` and create EF Core migration `AddTeamAssignments` in `src/Infrastructure/Persistence/Migrations/`

**Checkpoint**: Staffing domain models, schema migration, and store contracts are ready for story implementation.

---

## Phase 3: User Story 1 — View Available Pool and Active Roster (Priority: P1) 🎯 MVP

**Goal**: The active Chief Leader (or Admin) can query team selection context to view available candidates and the active team roster.

**Independent Test**: Call `GET /api/events/{id}/team-selection` as active Chief Leader; verify 200 OK with event details, available candidates array, and active roster array. Verify non-chief leader receives 403 Forbidden.

### Tests for User Story 1

- [x] T010 [P] [US1] Add failing SQL/API integration tests for Chief Leader querying available candidates and active roster on Published event, non-chief leader receiving 403 Forbidden, and non-published event receiving 409 Conflict in `tests/Carpediem.Api.IntegrationTests/Staffing/EventTeamSelectionTests.cs`
- [x] T011 [P] [US1] Add failing Angular service tests for `getTeamSelectionContext` in `frontend/src/app/features/team-selection/data/team-selection.service.spec.ts`
- [x] T012 [P] [US1] Add failing Angular component tests for rendering available candidates pool and active roster in `frontend/src/app/features/team-selection/components/team-selection.component.spec.ts`

### Implementation for User Story 1

- [x] T013 [US1] Implement `EfTeamSelectionStore.GetTeamSelectionContextAsync` querying event, current cycle, available candidates, and active roster in `src/Infrastructure/Persistence/EfTeamSelectionStore.cs`
- [x] T014 [US1] Register `ITeamSelectionStore` in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs` and `src/Modules/Staffing/ServiceCollectionExtensions.cs`
- [x] T015 [US1] Implement `GET /api/events/{eventId}/team-selection` endpoint in `src/Carpediem.Web/Endpoints/EventTeamSelectionEndpoints.cs`
- [x] T016 [US1] Implement Angular `TeamSelectionService` in `frontend/src/app/features/team-selection/data/team-selection.service.ts`
- [x] T017 [US1] Implement `TeamSelectionComponent` displaying available pool and active roster in `frontend/src/app/features/team-selection/components/`

**Checkpoint**: A Chief Leader can view their available volunteer candidate pool and current roster.

---

## Phase 4: User Story 2 — Select Volunteer into Immediate Active Roster (Priority: P1) 🎯 MVP

**Goal**: The active Chief Leader can select an available volunteer, immediately placing them into `Active` status on the roster (Decision Group 5), creating an in-platform notification and audit entry in an atomic SQL transaction.

**Independent Test**: Call `POST /api/events/{id}/team-selection` with candidate ID; verify 201 Created, `Active` `TeamAssignment` row, `NotificationIntent` row, and `AuditLog` row.

### Tests for User Story 2

- [x] T018 [P] [US2] Add failing SQL/API integration tests for Chief Leader selecting an available volunteer, verifying 201 Created, `staffing.TeamAssignments` row in `Active` status, `notifications.InPlatformNotifications` row, and `audit.AuditLogs` entry in `tests/Carpediem.Api.IntegrationTests/Staffing/EventTeamSelectionTests.cs`
- [x] T019 [P] [US2] Add failing Angular service and component tests for selecting a volunteer and updating roster state in `frontend/src/app/features/team-selection/components/team-selection.component.spec.ts`

### Implementation for User Story 2

- [x] T020 [US2] Implement `EfTeamSelectionStore.SelectVolunteerAsync` with atomic transaction inserting `TeamAssignment`, `InPlatformNotification`, and `AuditLog` in `src/Infrastructure/Persistence/EfTeamSelectionStore.cs`
- [x] T021 [US2] Implement `POST /api/events/{eventId}/team-selection` endpoint with antiforgery and RFC 7807 error handling in `src/Carpediem.Web/Endpoints/EventTeamSelectionEndpoints.cs`
- [x] T022 [US2] Implement selection action trigger and optimistic list update in `frontend/src/app/features/team-selection/components/team-selection.component.ts`
- [x] T023 [US2] Implement `chief-leader.guard.ts` and register `/events/:eventId/team-selection` route in `frontend/src/app/app.routes.ts`

**Checkpoint**: A Chief Leader can select volunteers end-to-end with immediate active team placement.

---

## Phase 5: User Story 3 & 4 — Concurrency, Idempotency, and Exclusivity Invariants (Priority: P2)

**Goal**: Enforce CHANGE-001 same-event exclusivity, candidate availability check, window closed check, idempotent replay, and Admin selection oversight.

**Independent Test**: Attempt selecting active Chief Leader (assert 409 `chief_leader_conflict`); replay identical selection (assert 200 with `Idempotent-Replay: true`); select candidate who marked `NotAvailable` (assert 409 `candidate_not_available`).

### Tests for User Story 3 & 4

- [x] T024 [P] [US3/US4] Add failing SQL/API integration tests for CHANGE-001 exclusivity conflict (409), candidate not available (409), window closed (409), idempotent replay (200 + header), and Admin selection in `tests/Carpediem.Api.IntegrationTests/Staffing/EventTeamSelectionTests.cs`
- [x] T025 [P] [US3] Add failing Playwright E2E tests for Chief Leader team selection flow in `tests/e2e/team-selection.spec.ts`

### Implementation for User Story 3 & 4

- [x] T026 [US3/US4] Implement exclusivity checks, candidate availability validation, and idempotent replay handling in `src/Infrastructure/Persistence/EfTeamSelectionStore.cs` and `src/Carpediem.Web/Endpoints/EventTeamSelectionEndpoints.cs`
- [x] T027 [US3/US4] Implement error message display and disabled button states in `frontend/src/app/features/team-selection/components/`

---

## Phase 6: User Story 5 — Mobile-First, Accessible Team Selection Interaction (Priority: P2)

**Goal**: Ensure mobile-first responsive layout down to 320px, minimum 44px touch targets, zero layout shift, polite live announcements, and WCAG 2.2 AA compliance.

### Tests for User Story 5

- [x] T028 [P] [US5] Add Playwright axe accessibility, 44px minimum touch targets, 200% text zoom reflow, and ARIA live status tests across 320px, 360px, 390px, 430px, and desktop in `tests/e2e/team-selection.spec.ts`

### Implementation for User Story 5

- [x] T029 [US5] Polish mobile-first SCSS styles, 44px touch targets, and ARIA live region in `frontend/src/app/features/team-selection/components/team-selection.component.scss` (budget < 4.00 kB)

**Checkpoint**: Concurrency, idempotency, and WCAG 2.2 AA accessibility are verified.

---

## Phase 7: Polish, Verification, and Convergence

**Purpose**: Full regression suite execution, k6 performance benchmarking, scope audit, and convergence documentation.

- [x] T030 [P] Add a SQL-backed 100-user concurrent team selection workload test in `tests/performance/team-selection.k6.js` and `tests/performance/run-team-selection.ps1`
- [x] T031 Run Release build, domain tests, SQL Server 2025 integration in container, Angular unit tests/build, Playwright e2e/a11y suite, generated OpenAPI verification, k6 performance, and `git diff --check`
- [x] T032 Audit routes, OpenAPI, UI, and schema to verify SLICE-08 scope discipline (no direct drafting of non-respondents or self-cancellation introduced)
- [x] T033 Perform requirement-to-evidence convergence review for FR-001–FR-015 and all acceptance scenarios; record evidence in `specs/007-chief-leader-team-selection/implementation-review.md` and mark tasks complete
