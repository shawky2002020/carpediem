# Tasks: SLICE-10 — Leader Self-Cancellation with Mandatory Justification

**Input**: Design documents from `specs/009-leader-self-cancellation/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contract](contracts/leader-self-cancellation.openapi.yaml), [quickstart.md](quickstart.md)

**Tests**: Required by the feature specification and constitution. Write each listed test first and observe the intended failure before implementation.

**Organization**: Tasks are grouped by user story and ordered so each increment has independently reviewable evidence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on another incomplete task.
- **[Story]**: Maps the task to a user story in `spec.md`.
- Every task names the exact target file or directory.

---

## Phase 1: Setup, Contracts, Telemetry, and Notification Alignment

**Purpose**: Establish OpenAPI configuration, telemetry, notification contracts, and audit helpers.

- [x] T001 Record the pre-task requirements quality gate in `specs/009-leader-self-cancellation/checklists/feature-quality.md`
- [x] T002 [P] Add cancellation operation metadata and required headers to generated OpenAPI configuration in `src/Carpediem.Web/Program.cs`
- [x] T003 [P] Create structured outcome telemetry for leader self-cancellation in `src/Carpediem.Web/Observability/LeaderSelfCancellationTelemetry.cs`
- [x] T004 [P] Add `TeamMemberCancelled` notification factory in `src/Modules/Notifications/Domain/InPlatformNotification.cs` and writer contract in `src/Modules/Notifications/Application/`

---

## Phase 2: Foundational Store Contracts & Domain Invariants

**Purpose**: Establish `TeamAssignment.Cancel(...)`, `ISelfCancellationStore.cs`, audit log helpers, and domain guards.

- [x] T005 [P] Add domain method `TeamAssignment.Cancel(...)` in `src/Modules/Staffing/Domain/TeamSelection/TeamAssignment.cs`
- [x] T006 [P] Add domain unit tests in `tests/Carpediem.Domain.Tests/Staffing/TeamAssignmentCancelTests.cs`
- [x] T007 [P] Add audit log helper for `VolunteerSelfCancelled` in `src/Infrastructure/Audit/AuditLog.cs`
- [x] T008 [P] Define store contracts (`ISelfCancellationStore.cs`) in `src/Modules/Staffing/Application/SelfCancellation/ISelfCancellationStore.cs`
- [x] T009 [P] Implement notification writer `EfTeamMemberCancelledNotificationWriter` in `src/Infrastructure/Notifications/`
- [x] T010 Implement `EfSelfCancellationStore` in `src/Infrastructure/Persistence/EfSelfCancellationStore.cs`
- [x] T011 Register `ISelfCancellationStore` and notification writer in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs`

---

## Phase 3: User Story 1 — Query Assignment & Cancellation Context (Priority: P1) 🎯 MVP

**Goal**: Authenticated volunteer leader or Admin can query assignment details and cancellation window eligibility for a published event.

### Tests for User Story 1

- [x] T012 [P] [US1] Add failing SQL/API integration tests for querying assignment context on Published event and non-published 409 Conflict in `tests/Carpediem.Api.IntegrationTests/Staffing/EventSelfCancellationTests.cs`
- [x] T013 [P] [US1] Add failing Angular service tests for `getMyAssignment` in `frontend/src/app/features/volunteer-assignment/data/volunteer-assignment.service.spec.ts`

### Implementation for User Story 1

- [x] T014 [US1] Implement `EfSelfCancellationStore.GetMyAssignmentAsync` in `src/Infrastructure/Persistence/EfSelfCancellationStore.cs`
- [x] T015 [US1] Implement `GET /api/events/{eventId}/my-assignment` endpoint in `src/Carpediem.Web/Endpoints/EventSelfCancellationEndpoints.cs`
- [x] T016 [US1] Implement `getMyAssignment` in `frontend/src/app/features/volunteer-assignment/data/volunteer-assignment.service.ts`

---

## Phase 4: User Story 2 & 3 — Self-Cancel & Admin Override with Mandatory Justification (Priority: P1) 🎯 MVP

**Goal**: Volunteer leader self-cancels or Admin overrides cancellation with a mandatory non-empty justification, transitioning status to `Cancelled` and dispatching notifications and audit logs.

### Tests for User Story 2 & 3

- [x] T017 [P] [US2/US3] Add failing SQL/API integration tests for volunteer self-cancellation and admin override cancellation, verifying 200 OK, `Cancelled` status, notification intents, and audit log entries in `tests/Carpediem.Api.IntegrationTests/Staffing/EventSelfCancellationTests.cs`
- [x] T018 [P] [US2/US3] Add failing Angular component tests for self-cancellation confirmation modal in `frontend/src/app/features/volunteer-assignment/components/volunteer-assignment.component.spec.ts`

### Implementation for User Story 2 & 3

- [x] T019 [US2/US3] Implement `EfSelfCancellationStore.CancelAssignmentAsync` in `src/Infrastructure/Persistence/EfSelfCancellationStore.cs`
- [x] T020 [US2/US3] Implement `POST /api/events/{eventId}/assignments/{assignmentId}/cancel` endpoint in `src/Carpediem.Web/Endpoints/EventSelfCancellationEndpoints.cs`
- [x] T021 [US2/US3] Implement `cancelAssignment` in `frontend/src/app/features/volunteer-assignment/data/volunteer-assignment.service.ts`
- [x] T022 [US2/US3] Implement controlled cancellation confirmation modal with mandatory justification textarea and character counter in `frontend/src/app/features/volunteer-assignment/components/volunteer-assignment.component.ts`

---

## Phase 5: User Story 4 — Concurrency, Idempotency, and Authorization Denials (Priority: P2)

**Goal**: Enforce resource-level authorization (non-owner non-admin receives 403), window closed check (409), invalid justification check (400), concurrency token check (412/409), and safe idempotent replay (200 + header).

### Tests for User Story 4

- [x] T023 [P] [US4] Add failing SQL/API integration tests for 403 non-owner denial, 409 window closed, 400 empty reason, 412 concurrency mismatch, and 200 idempotent replay in `tests/Carpediem.Api.IntegrationTests/Staffing/EventSelfCancellationTests.cs`
- [x] T024 [P] [US4] Add failing Playwright E2E tests for leader self-cancellation flow in `tests/e2e/leader-self-cancellation.spec.ts`

### Implementation for User Story 4

- [x] T025 [US4] Implement concurrency checking, authorization guards, and idempotent replay handling in `src/Infrastructure/Persistence/EfSelfCancellationStore.cs` and `src/Carpediem.Web/Endpoints/EventSelfCancellationEndpoints.cs`
- [x] T026 [US4] Implement cancellation error handling, live region status updates, and disabled buttons in frontend component

---

## Phase 6: User Story 5 — Mobile-First, Accessible Confirmation Modal UI (Priority: P2)

**Goal**: Ensure mobile-first responsive layout down to 320px, minimum 44px touch targets, zero layout shift, polite live announcements, trapped focus, Esc key dismiss, and WCAG 2.2 AA compliance.

### Tests for User Story 5

- [x] T027 [P] [US5] Add Playwright axe accessibility, 44px minimum touch targets, 200% text zoom reflow, and ARIA live status tests in `tests/e2e/leader-self-cancellation.spec.ts`

### Implementation for User Story 5

- [x] T028 [US5] Polish mobile-first SCSS styles, modal accessibility, and polite live region in `frontend/src/app/features/volunteer-assignment/components/volunteer-assignment.component.scss` (budget < 4.00 kB)

---

## Phase 7: Polish, Verification, and Convergence

**Purpose**: Full regression suite execution, k6 performance benchmarking, scope audit, and convergence documentation.

- [x] T029 [P] Add a SQL-backed 100-user concurrent self-cancellation workload test in `tests/performance/leader-self-cancellation.k6.js` and `tests/performance/run-leader-self-cancellation.ps1`
- [x] T030 Run Release build, domain tests, SQL Server 2025 integration in container, Angular unit tests/build, Playwright e2e/a11y suite, generated OpenAPI verification, k6 performance, and `git diff --check`
- [x] T031 Audit routes, OpenAPI, UI, and schema to verify SLICE-10 scope discipline (no replacement drafting introduced)
- [x] T032 Perform requirement-to-evidence convergence review for FR-001–FR-016 and all acceptance scenarios; record evidence in `specs/009-leader-self-cancellation/implementation-review.md` and mark tasks complete
