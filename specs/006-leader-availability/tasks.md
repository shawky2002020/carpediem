# Tasks: SLICE-07 — Leader Availability Declaration (Available / NotAvailable)

**Input**: Design documents from `specs/006-leader-availability/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contract](contracts/leader-availability.openapi.yaml), [quickstart.md](quickstart.md)

**Tests**: Required by the feature specification and constitution. Write each listed test first and observe the intended failure before implementation.

**Organization**: Tasks are grouped by user story and ordered so each increment has independently reviewable evidence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on another incomplete task.
- **[Story]**: Maps the task to a user story in `spec.md`.
- Every task names the exact target file or directory.

---

## Phase 1: Setup, Module Creation, and Contract Alignment

**Purpose**: Establish new `Carpediem.Modules.Staffing` project, EF Core migration, OpenAPI contracts, and telemetry.

- [ ] T001 Record the 20/20 pre-task requirements quality gate and frozen spec/plan references in `specs/006-leader-availability/checklists/feature-quality.md` and `specs/006-leader-availability/plan.md`
- [ ] T002 [P] Create `Carpediem.Modules.Staffing` project in `src/Modules/Staffing/Carpediem.Modules.Staffing.csproj` and register in `Carpediem.slnx`
- [ ] T003 [P] Add availability operation metadata and required `If-Match`/`X-XSRF-TOKEN` headers to the generated OpenAPI configuration in `src/Carpediem.Web/Program.cs`
- [ ] T004 [P] Create structured outcome telemetry for leader availability declaration in `src/Carpediem.Web/Observability/EventAvailabilityTelemetry.cs`

---

## Phase 2: Foundational Domain, Persistence, and Application Contracts

**Purpose**: Establish `AvailabilityResponse` aggregate, EF Core entity configuration, migration, and store abstraction.

**⚠️ CRITICAL**: User story implementation begins only after this phase is green.

- [ ] T005 [P] Add failing domain tests for `AvailabilityResponse` creation, mutability, temporal window guards, and CHANGE-001 exclusivity checks in `tests/Carpediem.Domain.Tests/Staffing/AvailabilityResponseTests.cs`
- [ ] T006 Implement `AvailabilityResponse` entity, `AvailabilityResponseType` enum, and guard exceptions (`AvailabilityWindowClosedException`, `ChiefLeaderExclusivityException`, `InvalidEventStatusException`) in `src/Modules/Staffing/Domain/Availability/`
- [ ] T007 [P] Define availability store, command, query, and result contracts in `src/Modules/Staffing/Application/Availability/IAvailabilityStore.cs`
- [ ] T008 [P] Add audit log helper for availability declaration in `src/Infrastructure/Audit/AuditLog.cs`
- [ ] T009 Configure `AvailabilityResponse` entity mapping (`staffing.AvailabilityResponses`, unique index on `StaffingCycleId, UserId`) in `src/Infrastructure/Persistence/CarpediemDbContext.cs` and create EF Core migration `AddAvailabilityResponses` in `src/Infrastructure/Migrations/`

**Checkpoint**: Staffing domain models, schema migration, and store contracts are ready for story implementation.

---

## Phase 3: User Story 1 — Declare Availability for a Published Event (Priority: P1) 🎯 MVP

**Goal**: An authenticated Leader can view an upcoming Published Event, see their status as "Response Needed", and submit "Mark Available" or "Mark Not Available", persisting an `AvailabilityResponse` record and an audit entry.

**Independent Test**: As an authenticated Leader, call `GET /api/events/{id}/availability/me` (returns null response); submit `POST /api/events/{id}/availability` with `"Available"`; verify 201 Created and persisted record.

### Tests for User Story 1

- [ ] T010 [P] [US1] Add failing SQL/API integration tests for Leader declaring `Available` and `NotAvailable` on a Published Event, verifying 201 Created, `staffing.AvailabilityResponses` row, and `audit.AuditLogs` entry in `tests/Carpediem.Api.IntegrationTests/Staffing/EventAvailabilityTests.cs`
- [ ] T011 [P] [US1] Add failing Angular service tests for `getMyAvailability` and `declareAvailability` in `frontend/src/app/features/availability/data/availability.service.spec.ts`
- [ ] T012 [P] [US1] Add failing Angular component tests for initial "Response Needed" status badge and submission buttons in `frontend/src/app/features/availability/components/availability-declaration/availability-declaration.component.spec.ts`

### Implementation for User Story 1

- [ ] T013 [US1] Implement `EfAvailabilityStore.GetMyAvailabilityAsync` and `EfAvailabilityStore.DeclareAvailabilityAsync` in `src/Infrastructure/Persistence/EfAvailabilityStore.cs`
- [ ] T014 [US1] Register `IAvailabilityStore` in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs` and `src/Modules/Staffing/ServiceCollectionExtensions.cs`
- [ ] T015 [US1] Implement `GET /api/events/{eventId}/availability/me` and `POST /api/events/{eventId}/availability` endpoints with `RequireLeader` policy, antiforgery, and RFC 7807 error handling in `src/Carpediem.Web/Endpoints/EventAvailabilityEndpoints.cs`
- [ ] T016 [US1] Implement Angular `AvailabilityService` in `frontend/src/app/features/availability/data/availability.service.ts`
- [ ] T017 [US1] Implement `AvailabilityDeclarationComponent` displaying event schedule, "Response Needed" badge, and submission buttons in `frontend/src/app/features/availability/components/availability-declaration/`

**Checkpoint**: A Leader can submit their initial availability declaration end-to-end.

---

## Phase 4: User Story 2 — Self-Service Mutability of Declared Availability (Priority: P1)

**Goal**: A Volunteer Leader who already submitted an availability response can toggle freely between `Available` and `NotAvailable` prior to event start, enforced via optimistic concurrency (`If-Match`).

**Independent Test**: Submit `Available`, update to `NotAvailable` with rowversion; verify 200 OK, rowversion incremented, and updated audit log. Verify window cutoff rejects submissions after `StartDateTime` with 409 Conflict (`window_closed`).

### Tests for User Story 2

- [ ] T018 [P] [US2] Add failing SQL/API integration tests for updating existing declaration with matching rowversion, rejection on stale rowversion (409), and rejection when event `StartDateTime <= UtcNow` (409) in `tests/Carpediem.Api.IntegrationTests/Staffing/EventAvailabilityTests.cs`
- [ ] T019 [P] [US2] Add failing Angular component tests for toggling declared availability with `If-Match` token and handling closed window notices in `frontend/src/app/features/availability/components/availability-declaration/availability-declaration.component.spec.ts`

### Implementation for User Story 2

- [ ] T020 [US2] Implement optimistic rowversion update logic, window closing check, and audit diffing in `src/Infrastructure/Persistence/EfAvailabilityStore.cs` and `src/Carpediem.Web/Endpoints/EventAvailabilityEndpoints.cs`
- [ ] T021 [US2] Implement reactive status toggling, rowversion passing, and closed window banner in `frontend/src/app/features/availability/components/availability-declaration/`

**Checkpoint**: Volunteer Leaders can freely update their schedules before the event starts.

---

## Phase 5: User Story 3 — Enforce CHANGE-001 Dual-Role Participation and Exclusivity (Priority: P2)

**Goal**: Enforce that a user who is the active Chief Leader for an event cannot declare availability for that same event, while retaining full availability declaration eligibility on other events where they are not active Chief Leader.

**Independent Test**: Authenticate as a dual-role user (ChiefLeader + Leader); attempt availability declaration on their led event (assert 409 `chief_leader_conflict`); submit on another event (assert success).

### Tests for User Story 3

- [ ] T022 [P] [US3] Add failing SQL/API integration tests for rejecting availability declaration when acting user is the active Chief Leader on the event (409), and allowing it on other events in `tests/Carpediem.Api.IntegrationTests/Staffing/EventAvailabilityTests.cs`
- [ ] T023 [P] [US3] Add failing mobile Playwright E2E coverage for dual-role Chief Leader banner and disabled controls on led events vs. enabled on other events in `tests/e2e/event-availability.spec.ts`

### Implementation for User Story 3

- [ ] T024 [US3] Implement active Chief Leader check query and rejection in `src/Infrastructure/Persistence/EfAvailabilityStore.cs`
- [ ] T025 [US3] Implement dual-role Chief Leader notice card ("You are the Chief Leader for this event") and disabled state in `frontend/src/app/features/availability/components/availability-declaration/`

**Checkpoint**: CHANGE-001 dual-role mutual exclusivity invariant is verified across API and UI.

---

## Phase 6: User Story 4 & 5 — Concurrency, Idempotency, and Mobile Accessible Interaction (Priority: P2)

**Goal**: Ensure idempotent replays return 200 with `Idempotent-Replay: true`, touch targets meet 44px, viewports reflow from 320px cleanly, and ARIA live regions announce updates.

**Independent Test**: Replay identical declaration; verify header and no extra audit; test with axe-core across 320px–430px viewports with zero violations.

### Tests for User Story 4 & 5

- [ ] T026 [P] [US4/US5] Add failing SQL/API integration tests for idempotent replay on identical availability declaration returning 200 with `Idempotent-Replay: true` without duplicate audit log in `tests/Carpediem.Api.IntegrationTests/Staffing/EventAvailabilityTests.cs`
- [ ] T027 [P] [US5] Add Playwright axe accessibility, 44px minimum touch targets, 200% text zoom reflow, and ARIA live status tests across 320px, 360px, 390px, 430px, and desktop in `tests/e2e/event-availability.spec.ts`

### Implementation for User Story 4 & 5

- [ ] T028 [US4/US5] Implement idempotent replay matching in `src/Infrastructure/Persistence/EfAvailabilityStore.cs` and `src/Carpediem.Web/Endpoints/EventAvailabilityEndpoints.cs`
- [ ] T029 [US5] Polish mobile-first SCSS styles, 44px touch targets, and ARIA live region in `frontend/src/app/features/availability/components/availability-declaration/availability-declaration.component.scss`

**Checkpoint**: Concurrency, idempotency, and WCAG 2.2 AA accessibility are verified.

---

## Phase 7: Polish, Verification, and Convergence

**Purpose**: Full regression suite execution, k6 performance benchmarking, scope audit, and convergence documentation.

- [ ] T030 [P] Add a SQL-backed 100-user concurrent availability workload test in `tests/performance/event-availability.k6.js` and `tests/performance/run-event-availability.ps1`
- [ ] T031 Run Release build, domain tests, SQL Server 2025 integration in container, Angular unit tests/build, Playwright e2e/a11y suite, generated OpenAPI verification, k6 performance, and `git diff --check`
- [ ] T032 Audit routes, OpenAPI, UI, and schema to verify SLICE-07 scope discipline (no Chief Leader team selection or direct drafting introduced)
- [ ] T033 Perform requirement-to-evidence convergence review for FR-001–FR-015 and all acceptance scenarios; record evidence in `specs/006-leader-availability/implementation-review.md` and mark tasks complete
