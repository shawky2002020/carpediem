# Tasks: SLICE-11 — Replacement Leader Assignment

**Input**: Design documents from `specs/010-replacement-leader-assignment/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contract](contracts/replacement-leader-assignment.openapi.yaml), [quickstart.md](quickstart.md)

**Tests**: Required by the feature specification and constitution. Write each listed test first and observe the intended failure before implementation.

**Organization**: Tasks are grouped by user story and ordered so each increment has independently reviewable evidence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on another incomplete task.
- **[Story]**: Maps the task to a user story in `spec.md`.
- Every task names the exact target file or directory.

---

## Phase 1: Setup, Contracts, Telemetry, and Notification Alignment

**Purpose**: Establish OpenAPI configuration, telemetry, notification contracts, and audit helpers.

- [x] T001 Record the pre-task requirements quality gate in `specs/010-replacement-leader-assignment/checklists/feature-quality.md`
- [x] T002 [P] Add replacement operation metadata and required headers to generated OpenAPI configuration in `src/Carpediem.Web/Program.cs`
- [x] T003 [P] Create structured outcome telemetry for replacement assignment in `src/Carpediem.Web/Observability/ReplacementAssignmentTelemetry.cs`
- [x] T004 [P] Add `ReplacementLeaderAssigned` notification factory in `src/Modules/Notifications/Domain/InPlatformNotification.cs` and writer contract in `src/Modules/Notifications/Application/`

---

## Phase 2: Foundational Store Contracts & Domain Invariants

**Purpose**: Establish `IReplacementAssignmentStore.cs`, audit log helpers, and domain guards.

- [x] T005 [P] Add audit log helper for `ReplacementLeaderAssigned` in `src/Infrastructure/Audit/AuditLog.cs`
- [x] T006 [P] Define store contracts (`IReplacementAssignmentStore.cs`) in `src/Modules/Staffing/Application/ReplacementAssignment/IReplacementAssignmentStore.cs`
- [x] T007 [P] Implement notification writer `EfReplacementLeaderAssignedNotificationWriter` in `src/Infrastructure/Notifications/`
- [x] T008 Implement `EfReplacementAssignmentStore` in `src/Infrastructure/Persistence/EfReplacementAssignmentStore.cs`
- [x] T009 Register `IReplacementAssignmentStore` and notification writer in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs`

---

## Phase 3: User Story 1 — Query Replacement Context & Candidate Pool (Priority: P1) 🎯 MVP

**Goal**: Authenticated Chief Leader or Admin can query event staffing state, active members, cancelled members, and eligible replacement candidates.

### Tests for User Story 1

- [x] T010 [P] [US1] Add failing SQL/API integration tests for querying replacement context on Published event and non-published 409 Conflict in `tests/Carpediem.Api.IntegrationTests/Staffing/EventReplacementAssignmentTests.cs`
- [x] T011 [P] [US1] Add failing Angular service tests for `getReplacementContext` in `frontend/src/app/features/replacement-assignment/data/replacement-assignment.service.spec.ts`

### Implementation for User Story 1

- [x] T012 [US1] Implement `EfReplacementAssignmentStore.GetReplacementContextAsync` in `src/Infrastructure/Persistence/EfReplacementAssignmentStore.cs`
- [x] T013 [US1] Implement `GET /api/events/{eventId}/replacement-assignment` endpoint in `src/Carpediem.Web/Endpoints/EventReplacementAssignmentEndpoints.cs`
- [x] T014 [US1] Implement `getReplacementContext` in `frontend/src/app/features/replacement-assignment/data/replacement-assignment.service.ts`

---

## Phase 4: User Story 2 & 3 — Chief Leader & Admin Draft Replacement (Priority: P1) 🎯 MVP

**Goal**: Chief Leader or Admin drafts an eligible replacement leader, creating a brand new active `TeamAssignment` while preserving the historical cancelled assignment.

### Tests for User Story 2 & 3

- [x] T015 [P] [US2/US3] Add failing SQL/API integration tests for Chief Leader replacement selection and Admin override, verifying 201 Created, new active assignment, untouched cancelled record, notification, and audit log entries in `tests/Carpediem.Api.IntegrationTests/Staffing/EventReplacementAssignmentTests.cs`
- [x] T016 [P] [US2/US3] Add failing Angular component tests for replacement assignment interface in `frontend/src/app/features/replacement-assignment/components/replacement-assignment.component.spec.ts`

### Implementation for User Story 2 & 3

- [x] T017 [US2/US3] Implement `EfReplacementAssignmentStore.AssignReplacementLeaderAsync` in `src/Infrastructure/Persistence/EfReplacementAssignmentStore.cs`
- [x] T018 [US2/US3] Implement `POST /api/events/{eventId}/replacement-assignment` endpoint in `src/Carpediem.Web/Endpoints/EventReplacementAssignmentEndpoints.cs`
- [x] T019 [US2/US3] Implement `assignReplacementLeader` in `frontend/src/app/features/replacement-assignment/data/replacement-assignment.service.ts`
- [x] T020 [US2/US3] Implement replacement candidate selection table and action in `frontend/src/app/features/replacement-assignment/components/replacement-assignment.component.ts`

---

## Phase 5: User Story 4 — Concurrency, Eligibility Guards & Idempotency (Priority: P2)

**Goal**: Enforce resource-level authorization (non-assigned leader receives 403), window closed check (409), Chief Leader conflict (409), and safe idempotent replay (200 + header).

### Tests for User Story 4

- [x] T021 [P] [US4] Add failing SQL/API integration tests for 403 non-Chief-Leader denial, 409 Chief Leader candidate conflict, 409 past-start window closed, and 200 idempotent replay in `tests/Carpediem.Api.IntegrationTests/Staffing/EventReplacementAssignmentTests.cs`
- [x] T022 [P] [US4] Add failing Playwright E2E tests for replacement assignment flow in `tests/e2e/replacement-leader-assignment.spec.ts`

### Implementation for User Story 4

- [x] T023 [US4] Implement eligibility guards, authorization checks, and idempotent replay handling in `src/Infrastructure/Persistence/EfReplacementAssignmentStore.cs` and `src/Carpediem.Web/Endpoints/EventReplacementAssignmentEndpoints.cs`
- [x] T024 [US4] Implement error alerts, live region status updates, and disabled buttons in frontend component

---

## Phase 6: User Story 5 — Mobile-First, Accessible Replacement Interface (Priority: P2)

**Goal**: Ensure mobile-first responsive layout down to 320px, minimum 44px touch targets, zero layout shift, polite live announcements, and WCAG 2.2 AA compliance.

### Tests for User Story 5

- [x] T025 [P] [US5] Add Playwright axe accessibility, 44px minimum touch targets, 200% text zoom reflow, and ARIA live status tests in `tests/e2e/replacement-leader-assignment.spec.ts`

### Implementation for User Story 5

- [x] T026 [US5] Implement mobile-first SCSS styles and polite live regions in `frontend/src/app/features/replacement-assignment/components/replacement-assignment.component.scss` (budget < 4.00 kB)
- [x] T027 [US5] Register replacement route `/events/:eventId/replacement-assignment` in `frontend/src/app/app.routes.ts`

---

## Phase 7: Polish, Verification, and Convergence

**Purpose**: Full regression suite execution, k6 performance benchmarking, scope audit, and convergence documentation.

- [x] T028 [P] Add a SQL-backed 100-user concurrent replacement assignment workload test in `tests/performance/replacement-leader-assignment.k6.js` and `tests/performance/run-replacement-leader-assignment.ps1`
- [x] T029 Run Release build, domain tests, SQL Server 2025 integration in container, Angular unit tests/build, Playwright e2e/a11y suite, generated OpenAPI verification, k6 performance, and `git diff --check`
- [x] T030 Audit routes, OpenAPI, UI, and schema to verify SLICE-11 scope discipline (no reschedule/cycle rotation logic introduced)
- [x] T031 Perform requirement-to-evidence convergence review for FR-001–FR-016 and all acceptance scenarios; record evidence in `specs/010-replacement-leader-assignment/implementation-review.md` and mark tasks complete
