# Tasks: SLICE-09 — Direct Leader Assignment

**Input**: Design documents from `specs/008-direct-leader-assignment/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contract](contracts/direct-assignment.openapi.yaml), [quickstart.md](quickstart.md)

**Tests**: Required by the feature specification and constitution. Write each listed test first and observe the intended failure before implementation.

**Organization**: Tasks are grouped by user story and ordered so each increment has independently reviewable evidence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on another incomplete task.
- **[Story]**: Maps the task to a user story in `spec.md`.
- Every task names the exact target file or directory.

---

## Phase 1: Setup, Contracts, Telemetry, and Notification Alignment

**Purpose**: Establish OpenAPI configuration, telemetry, notification contracts, and audit helpers.

- [x] T001 Record the pre-task requirements quality gate in `specs/008-direct-leader-assignment/checklists/feature-quality.md`
- [x] T002 [P] Add direct assignment operation metadata and required headers to generated OpenAPI configuration in `src/Carpediem.Web/Program.cs`
- [x] T003 [P] Create structured outcome telemetry for direct leader assignment in `src/Carpediem.Web/Observability/DirectAssignmentTelemetry.cs`
- [x] T004 [P] Add `DirectLeaderAssigned` notification factory in `src/Modules/Notifications/Domain/InPlatformNotification.cs` and writer contract in `src/Modules/Notifications/Application/`

---

## Phase 2: Foundational Store Contracts & Domain Invariants

**Purpose**: Establish `IDirectAssignmentStore.cs`, audit log helpers, and domain guards.

- [x] T005 [P] Define direct assignment store contracts (`IDirectAssignmentStore.cs`) in `src/Modules/Staffing/Application/DirectAssignment/IDirectAssignmentStore.cs`
- [x] T006 [P] Add audit log helper for `DirectLeaderAssignmentExecuted` in `src/Infrastructure/Audit/AuditLog.cs`
- [x] T007 Implement `EfDirectAssignmentStore` in `src/Infrastructure/Persistence/EfDirectAssignmentStore.cs`
- [x] T008 Register `IDirectAssignmentStore` in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs`

---

## Phase 3: User Story 1 — Query Direct Draft Candidate Pool (Priority: P1) 🎯 MVP

**Goal**: Active Chief Leader or Admin can query candidates who have not responded or who marked `NotAvailable`, excluding Chief Leader and already active members.

### Tests for User Story 1

- [x] T009 [P] [US1] Add failing SQL/API integration tests for querying direct draft candidate pool on Published event, non-chief leader receiving 403 Forbidden, and non-published event receiving 409 Conflict in `tests/Carpediem.Api.IntegrationTests/Staffing/EventDirectAssignmentTests.cs`
- [x] T010 [P] [US1] Add failing Angular service tests for `getDirectAssignmentCandidates` in `frontend/src/app/features/team-selection/data/team-selection.service.spec.ts`

### Implementation for User Story 1

- [x] T011 [US1] Implement `EfDirectAssignmentStore.GetCandidatesAsync` in `src/Infrastructure/Persistence/EfDirectAssignmentStore.cs`
- [x] T012 [US1] Implement `GET /api/events/{eventId}/direct-assignment/candidates` endpoint in `src/Carpediem.Web/Endpoints/EventDirectAssignmentEndpoints.cs`
- [x] T013 [US1] Implement `getDirectAssignmentCandidates` in `frontend/src/app/features/team-selection/data/team-selection.service.ts`
- [x] T014 [US1] Add direct draft candidate pool list to `TeamSelectionComponent` in `frontend/src/app/features/team-selection/components/`

---

## Phase 4: User Story 2 & 3 — Direct Draft Non-Respondent & `NotAvailable` Leader (Priority: P1) 🎯 MVP

**Goal**: Active Chief Leader or Admin directly drafts a leader, immediately placing them into `Active` status on the roster (Decision Group 5 & Approved Blocker 1).

### Tests for User Story 2 & 3

- [x] T015 [P] [US2/US3] Add failing SQL/API integration tests for directly drafting a non-respondent and drafting a `NotAvailable` leader, verifying 201 Created, `Active` assignment, notification intent, and audit log noting prior status in `tests/Carpediem.Api.IntegrationTests/Staffing/EventDirectAssignmentTests.cs`
- [x] T016 [P] [US2/US3] Add failing Angular component tests for directly drafting a candidate and updating roster state in `frontend/src/app/features/team-selection/components/team-selection.component.spec.ts`

### Implementation for User Story 2 & 3

- [x] T017 [US2/US3] Implement `EfDirectAssignmentStore.DirectAssignLeaderAsync` in `src/Infrastructure/Persistence/EfDirectAssignmentStore.cs`
- [x] T018 [US2/US3] Implement `POST /api/events/{eventId}/direct-assignment` endpoint in `src/Carpediem.Web/Endpoints/EventDirectAssignmentEndpoints.cs`
- [x] T019 [US2/US3] Implement `directAssignLeader` in `frontend/src/app/features/team-selection/data/team-selection.service.ts`
- [x] T020 [US2/US3] Implement direct draft action trigger and optimistic list update in `frontend/src/app/features/team-selection/components/team-selection.component.ts`

---

## Phase 5: User Story 4 — Concurrency, Idempotency, and Exclusivity Invariants (Priority: P2)

**Goal**: Enforce CHANGE-001 same-event exclusivity, window closed check, idempotent replay, and Admin oversight.

### Tests for User Story 4

- [x] T021 [P] [US4] Add failing SQL/API integration tests for CHANGE-001 exclusivity conflict (409), window closed (409), idempotent replay (200 + header), and Admin direct draft in `tests/Carpediem.Api.IntegrationTests/Staffing/EventDirectAssignmentTests.cs`
- [x] T022 [P] [US4] Add failing Playwright E2E tests for direct leader assignment flow in `tests/e2e/direct-assignment.spec.ts`

### Implementation for User Story 4

- [x] T023 [US4] Implement exclusivity checks, window checks, and idempotent replay handling in `src/Infrastructure/Persistence/EfDirectAssignmentStore.cs` and `src/Carpediem.Web/Endpoints/EventDirectAssignmentEndpoints.cs`
- [x] T024 [US4] Implement error message display and disabled button states in `frontend/src/app/features/team-selection/components/`

---

## Phase 6: User Story 5 — Mobile-First, Accessible Direct Assignment UI (Priority: P2)

**Goal**: Ensure mobile-first responsive layout down to 320px, minimum 44px touch targets, zero layout shift, polite live announcements, and WCAG 2.2 AA compliance.

### Tests for User Story 5

- [x] T025 [P] [US5] Add Playwright axe accessibility, 44px minimum touch targets, 200% text zoom reflow, and ARIA live status tests in `tests/e2e/direct-assignment.spec.ts`

### Implementation for User Story 5

- [x] T026 [US5] Polish mobile-first SCSS styles, 44px touch targets, and ARIA live region in `frontend/src/app/features/team-selection/components/team-selection.component.scss` (budget < 4.00 kB)
- [x] T027 [US5] Ensure seamless switching between "Available Volunteers" and "Direct Draft Pool" with clear counters and live announcements

---

## Phase 7: Polish, Verification, and Convergence

**Purpose**: Full regression suite execution, k6 performance benchmarking, scope audit, and convergence documentation.

- [x] T028 [P] Add a SQL-backed 100-user concurrent direct assignment workload test in `tests/performance/direct-assignment.k6.js` and `tests/performance/run-direct-assignment.ps1`
- [x] T029 Run Release build, domain tests, SQL Server 2025 integration in container, Angular unit tests/build, Playwright e2e/a11y suite, generated OpenAPI verification, k6 performance, and `git diff --check`
- [x] T030 Audit routes, OpenAPI, UI, and schema to verify SLICE-09 scope discipline (no volunteer self-cancellation or replacement drafting introduced)
- [x] T031 Perform requirement-to-evidence convergence review for FR-001–FR-015 and all acceptance scenarios; record evidence in `specs/008-direct-leader-assignment/implementation-review.md` and mark tasks complete
