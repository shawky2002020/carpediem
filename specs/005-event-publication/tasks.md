# Tasks: SLICE-06 — Event Publication & Publishing Prerequisite Invariant

**Input**: Design documents from `specs/005-event-publication/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contract](contracts/event-publication.openapi.yaml), [quickstart.md](quickstart.md)

**Tests**: Required by the feature specification and constitution. Write each listed test first and observe the intended failure before implementation.

**Organization**: Tasks are grouped by user story and ordered so each increment has independently reviewable evidence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and has no dependency on another incomplete task.
- **[Story]**: Maps the task to a user story in `spec.md`.
- Every task names the exact target file or directory.

## Phase 1: Setup and Contract Alignment

**Purpose**: Establish feature baseline, OpenAPI contracts, and telemetry.

- [ ] T001 Record the 20/20 pre-task requirements quality gate and frozen spec/plan references in `specs/005-event-publication/checklists/feature-quality.md` and `specs/005-event-publication/plan.md`
- [ ] T002 [P] Add event publication operation metadata and required `If-Match`/`X-XSRF-TOKEN` headers to the generated OpenAPI configuration in `src/Carpediem.Web/Program.cs`
- [ ] T003 [P] Create structured outcome telemetry for event publication in `src/Carpediem.Web/Observability/EventPublicationTelemetry.cs`

---

## Phase 2: Foundational Domain and Application Contracts

**Purpose**: Establish domain invariant guards, notification factory, and store abstractions.

**⚠️ CRITICAL**: User story implementation begins only after this phase is green.

- [ ] T004 [P] Add failing domain tests for `Event.Publish` enforcing `Draft` status, exactly one `Active` Chief Leader assignment, valid UTC schedule window, and setting `Status = Published` in `tests/Carpediem.Domain.Tests/Events/EventPublicationTests.cs`
- [ ] T005 Implement `Publish` method and invariant guard exceptions (`ActiveChiefLeaderRequiredException`, `InvalidTimeWindowException`) on `Event` in `src/Modules/Events/Domain/Events/Event.cs`
- [ ] T006 [P] Define event-publication command, result, and store contracts in `src/Modules/Events/Application/EventPublication/IEventPublicationStore.cs`
- [ ] T007 [P] Add event published notification factory method in `src/Modules/Notifications/Domain/InPlatformNotification.cs`
- [ ] T008 [P] Add audit log helper for event publication in `src/Infrastructure/Audit/AuditLog.cs`

**Checkpoint**: Domain invariants and interfaces are ready for story implementation.

---

## Phase 3: User Story 1 — Review Publishing Readiness of a Draft Event (Priority: P1) 🎯 MVP

**Goal**: An authorized Operations Admin viewing a Draft Event sees whether it meets the publishing prerequisite invariant (Active Chief Leader and valid schedule) with clear, actionable guidance.

**Independent Test**: View a Draft Event with no Chief Leader, Pending Chief Leader, Declined Chief Leader, and Active Chief Leader; only Active Chief Leader enables the publish action.

### Tests for User Story 1

- [ ] T009 [P] [US1] Add failing Angular component tests for publishing readiness indicator (enabled when Chief Leader is Active, disabled/explaining when absent or not active) in `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.spec.ts`
- [ ] T010 [P] [US1] Add failing mobile Playwright coverage for Admin publishing action visibility and non-admin denial in `tests/e2e/event-publication.spec.ts`

### Implementation for User Story 1

- [ ] T011 [US1] Add publishing readiness card, status badge, and prerequisite guidance in `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.html` and `draft-edit.component.scss`

**Checkpoint**: The Admin can visually inspect whether an Event is ready to be published without triggering state changes.

---

## Phase 4: User Story 2 — Publish Event Enforcing Prerequisite Invariants (Priority: P1)

**Goal**: A valid Admin publish action transitions the Event to `Published`, appends an immutable `EventPublished` audit record, and stages Leader notifications in one atomic transaction.

**Independent Test**: Publish an eligible Draft Event with matching rowversion; verify the Event transitions to `Published`, audit and notification rows are created, and draft editing is locked.

### Tests for User Story 2

- [ ] T012 [P] [US2] Add failing SQL/API integration tests for Admin publishing with Active Chief Leader, rejection without Active Chief Leader (422), atomic `EventPublished` audit record, atomic leader notifications, and draft-edit locking in `tests/Carpediem.Api.IntegrationTests/EventPublication/EventPublicationTests.cs`
- [ ] T013 [P] [US2] Add failing Angular service tests for `publishDraftEvent` with `If-Match` rowversion in `frontend/src/app/features/draft-events/data/draft-events.service.spec.ts`

### Implementation for User Story 2

- [ ] T014 [US2] Implement `publishDraftEvent` HTTP method in `frontend/src/app/features/draft-events/data/draft-events.service.ts`
- [ ] T015 [US2] Implement atomic publication, invariant validation, audit record creation, leader notification queries, and transaction commit in `src/Infrastructure/Persistence/EfEventPublicationStore.cs`
- [ ] T016 [US2] Register `IEventPublicationStore` in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs` and `src/Modules/Events/ServiceCollectionExtensions.cs`
- [ ] T017 [US2] Add `POST /api/events/drafts/{id}/publish` endpoint with antiforgery, `If-Match`, authorization, and RFC 7807 error mapping in `src/Carpediem.Web/Endpoints/DraftEventsEndpoints.cs`
- [ ] T018 [US2] Add confirmation dialog, reactive publish submission, single in-flight lock, and published state transition in `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.ts` and `draft-edit.component.html`

**Checkpoint**: The primary publication business workflow is completely implemented and independently testable.

---

## Phase 5: User Story 3 — Concurrency, Idempotency, and Stale State Handling (Priority: P2)

**Goal**: Ensure optimistic concurrency, idempotent replay for duplicate publish requests, and clean rejection of invalid lifecycle transitions.

**Independent Test**: Replay an identical publish command on an already-published Event; submit with a stale rowversion; attempt publication on a cancelled or completed event.

### Tests for User Story 3

- [ ] T019 [P] [US3] Add failing SQL/API integration tests for stale rowversion rejection (409), idempotent replay returning 200 with `Idempotent-Replay: true` without duplicate audit/notifications, and rejection on non-draft states in `tests/Carpediem.Api.IntegrationTests/EventPublication/EventPublicationTests.cs`
- [ ] T020 [P] [US3] Add failing Angular component tests for handling 409 stale draft conflict and uncertain result recovery in `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.spec.ts`

### Implementation for User Story 3

- [ ] T021 [US3] Implement rowversion verification, matching-audit idempotent replay, and conflict mapping in `src/Infrastructure/Persistence/EfEventPublicationStore.cs` and `src/Carpediem.Web/Endpoints/DraftEventsEndpoints.cs`
- [ ] T022 [US3] Implement client conflict recovery, error announcements, and reload options in `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.ts` and `draft-edit.component.html`

**Checkpoint**: Every stale, concurrent, repeated, or conflicting submission produces a deterministic, safe outcome.

---

## Phase 6: User Story 4 — Mobile Confirmation and Accessible Interaction (Priority: P2)

**Goal**: Ensure the mobile UI reflows cleanly, touch targets meet or exceed 44px, confirmation prevents accidental clicks, and screen readers receive status updates.

**Independent Test**: Test with axe-core across 320px, 360px, 390px, 430px viewports and desktop in Playwright.

### Tests for User Story 4

- [ ] T023 [P] [US4] Add axe accessibility, keyboard focus trap/restoration, visible focus, 44px minimum target, 200% text scaling, reduced motion, and 320px viewport tests in `tests/e2e/event-publication.spec.ts`

### Implementation for User Story 4

- [ ] T024 [US4] Polish mobile-first SCSS styles for confirmation dialog, status live region, responsive spacing, and touch target compliance in `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.scss`

**Checkpoint**: Mobile usability and WCAG 2.2 AA accessibility are verified.

---

## Phase 7: Polish, Verification, and Convergence

**Purpose**: Full regression suite execution, k6 performance benchmarking, scope audit, and convergence documentation.

- [ ] T025 [P] Add a SQL-backed 100-user publication and replay workload test in `tests/performance/event-publication.k6.js` and `tests/performance/run-event-publication.ps1`
- [ ] T026 Run Release build, domain tests, SQL Server 2025 integration in container, Angular unit tests/build, Playwright e2e/a11y suite, generated OpenAPI verification, k6 performance, and `git diff --check`
- [ ] T027 Audit routes, OpenAPI, UI, notifications, and schema to verify SLICE-06 scope discipline (no volunteer availability submission, team selection, or rescheduling introduced)
- [ ] T028 Perform requirement-to-evidence convergence review for FR-001–FR-014 and all acceptance scenarios; record evidence in `specs/005-event-publication/implementation-review.md` and mark tasks complete
