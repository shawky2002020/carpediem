---

description: "Dependency-ordered implementation tasks for SLICE-02 Draft Event Creation & Draft Editing"
---

# Tasks: SLICE-02 — Draft Event Creation & Draft Editing

**Input**: Design documents from `specs/001-draft-event-creation-editing/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, and `quickstart.md`

**Tests**: Required by the approved plan and Carpediem constitution. In each user-story phase, create the listed tests before that story's implementation; new behavior must fail for the expected reason before it is added.

**Organization**: Tasks are grouped by user story so each approved workflow can be implemented and validated as a coherent increment. FOUNDATION-01 work is limited to the shared setup and foundational tasks required by SLICE-02.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with other marked tasks after its stated prerequisites because it changes different files
- **[Story]**: Maps the task to a user story from `spec.md`
- Every task names its target file or directory

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the minimum executable Angular → ASP.NET Core → Events module → EF Core → SQL Server path and its test projects.

- [ ] T001 Create the .NET 10 solution and the Web, Identity, Events, Infrastructure, Domain Tests, and API Integration Tests projects with the planned references in `Carpediem.slnx`, `src/Carpediem.Web/Carpediem.Web.csproj`, `src/Modules/Identity/Carpediem.Modules.Identity.csproj`, `src/Modules/Events/Carpediem.Modules.Events.csproj`, `src/Infrastructure/Carpediem.Infrastructure.csproj`, `tests/Carpediem.Domain.Tests/Carpediem.Domain.Tests.csproj`, and `tests/Carpediem.Api.IntegrationTests/Carpediem.Api.IntegrationTests.csproj`
- [ ] T002 [P] Pin the .NET SDK, nullable/analysis settings, and shared build defaults in `global.json` and `Directory.Build.props`
- [ ] T003 [P] Create the Angular 22 standalone workspace with routing, SCSS, strict TypeScript, and package scripts in `frontend/package.json`, `frontend/angular.json`, and `frontend/tsconfig.json`
- [ ] T004 [P] Define the SQL Server 2025 local service, health check, persistent volume, and configuration placeholders without committed secrets in `compose.yaml` and `.env.example`
- [ ] T005 Add only the approved backend packages for ASP.NET Core Identity, EF Core SQL Server/design tooling, Serilog, OpenTelemetry, OpenAPI, xUnit, WebApplicationFactory, and SQL Server Testcontainers to `Directory.Packages.props`
- [ ] T006 [P] Configure repository-wide ignore rules for .NET, Angular, Playwright, local secrets, test output, and container artifacts without adding, moving, modifying, or staging the existing untracked `logo.pdf` in `.gitignore`

**Checkpoint**: The planned projects restore and the empty backend, frontend, and test workspaces build without feature behavior.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement the minimum hosting, session, authorization, persistence, error, observability, frontend shell, and test infrastructure required by every story.

**⚠️ CRITICAL**: Complete this phase before user-story implementation.

- [ ] T007 Configure the ASP.NET Core composition root, module registration points, JSON conventions, OpenAPI, HTTPS/cookie defaults, and environment configuration in `src/Carpediem.Web/Program.cs` and `src/Carpediem.Web/appsettings.json`
- [ ] T008 [P] Implement the host EF Core context with one migration history, schema-aware module configuration discovery, UTC-safe conventions, and SQL Server retry/RCSI setup in `src/Infrastructure/Persistence/CarpediemDbContext.cs` and `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs`
- [ ] T009 [P] Configure minimal ASP.NET Core Identity user/role persistence, secure `HttpOnly`/`Secure`/`SameSite=Lax` cookie sessions, and the `Admin`, `ChiefLeader`, and `Leader` role constants in `src/Modules/Identity/Infrastructure/IdentityConfiguration.cs` and `src/Modules/Identity/Domain/CarpediemUser.cs`
- [ ] T010 Implement the server-enforced `RequireAdmin` policy and authorization registration in `src/Modules/Identity/Authorization/AuthorizationPolicies.cs` and `src/Modules/Identity/ServiceCollectionExtensions.cs`
- [ ] T011 Implement antiforgery issuance/validation and the minimal login, current-session, and logout operations from the session bootstrap contract in `src/Carpediem.Web/Endpoints/SessionEndpoints.cs` and `src/Carpediem.Web/Security/AntiforgeryConfiguration.cs`
- [ ] T012 [P] Implement safe RFC 7807 responses with stable application codes, required trace IDs, field errors, correlation propagation, and no internal-detail leakage in `src/Carpediem.Web/Errors/ApiProblemDetailsService.cs` and `src/Carpediem.Web/Errors/ExceptionHandlingMiddleware.cs`
- [ ] T013 [P] Configure Serilog, lightweight OpenTelemetry request traces/metrics, safe logging filters, and `/healthz` without recording form values or credentials in `src/Carpediem.Web/Observability/ObservabilityConfiguration.cs`
- [ ] T014 Add deterministic development-only fixtures for one Admin, two Chief Leaders, and five Leaders without committed credentials in `src/Modules/Identity/Infrastructure/DevelopmentIdentitySeeder.cs`
- [ ] T015 [P] Create the Angular application shell, authenticated routes, and role-aware navigation in `frontend/src/app/app.config.ts`, `frontend/src/app/app.routes.ts`, and `frontend/src/app/layout/app-shell.component.ts`
- [ ] T016 Implement Angular session bootstrap, Admin route guard, credentials-enabled HTTP, antiforgery header handling, and correlation/error primitives in `frontend/src/app/core/auth/session.service.ts`, `frontend/src/app/core/auth/admin.guard.ts`, and `frontend/src/app/core/http/api.interceptor.ts`
- [ ] T017 [P] Establish only the mobile form, focus, status, contrast, spacing, and touch-target tokens needed by SLICE-02 from committed approved UX documentation, without depending on provisional untracked design assets, in `frontend/src/styles.scss` and `frontend/src/styles/_tokens.scss`
- [ ] T018 [P] Create the SQL Server Testcontainers/WebApplicationFactory harness with isolated database reset and configurable authenticated test principals in `tests/Carpediem.Api.IntegrationTests/Infrastructure/CarpediemWebApplicationFactory.cs` and `tests/Carpediem.Api.IntegrationTests/Infrastructure/SqlServerFixture.cs`
- [ ] T019 [P] Configure Angular unit testing and Playwright projects for 320, 360, 390, and 430 CSS-pixel mobile viewports plus desktop in `frontend/vitest.config.ts` and `tests/e2e/playwright.config.ts`

**Checkpoint**: The host starts, SQL Server is reachable, session bootstrap works, Admin policy and antiforgery are enforceable, the Angular shell can read session state, and all test harnesses start.

---

## Phase 3: User Story 1 — Create a Draft Event (Priority: P1) 🎯 MVP

**Goal**: An authorized Admin creates and reopens one valid Draft using the four required CHANGE-002 fields, with optional Notes, Cairo-local authoring, UTC persistence, one current StaffingCycle, audit evidence, and duplicate-submit protection.

**Independent Test**: Sign in as the seeded Admin, create with Title, StartDateTime, EndDateTime, and Location both with and without Notes, then reopen the result and prove preserved values, `Draft` status, canonical UTC instants, exactly one current cycle, one creation audit, and no duplicate after replaying the same intent.

### Tests for User Story 1

- [ ] T020 [P] [US1] Add failing domain tests for valid creation, required Title/Location, Unicode whitespace rejection, strict start-before-end ordering, accepted past timing, optional blank/null Notes, exact accepted-text preservation, `Draft` initialization, and initial cycle creation in `tests/Carpediem.Domain.Tests/Events/CreateDraftEventTests.cs` and `tests/Carpediem.Domain.Tests/Events/DraftEventValidationTests.cs`
- [ ] T021 [P] [US1] Add failing API/SQL integration tests for authorized create with and without Notes, Event/cycle/audit/ledger atomicity, UTC zero-offset storage, Cairo round trip, and rollback on persistence failure in `tests/Carpediem.Api.IntegrationTests/DraftEvents/CreateDraftEventTests.cs`
- [ ] T022 [P] [US1] Add failing concurrency/idempotency integration tests for double submission, concurrent same-token submission, lost-response replay, same-token changed payload conflict, and distinct tokens with identical business fields in `tests/Carpediem.Api.IntegrationTests/DraftEvents/CreateDraftIdempotencyTests.cs`
- [ ] T023 [P] [US1] Add failing API contract tests for Draft list, create, detail, `201` first success, `200` replay, Location/replay headers, DTO shapes, and required error fields in `tests/Carpediem.Api.IntegrationTests/Contracts/DraftEventsContractTests.cs`
- [ ] T024 [P] [US1] Add failing Angular tests for form initialization, Cairo labeling, optional Notes, one token per creation intent, saving-state duplicate suppression, success announcement, and list reopening in `frontend/src/app/features/draft-events/pages/draft-create/draft-create.component.spec.ts` and `frontend/src/app/features/draft-events/pages/draft-list/draft-list.component.spec.ts`
- [ ] T025 [P] [US1] Add a failing Playwright Admin create-and-reopen scenario at a mobile viewport in `tests/e2e/draft-events-create.spec.ts`

### Implementation for User Story 1

- [ ] T026 [P] [US1] Implement the Event aggregate and status with required Unicode-nonblank Title/Location, exact accepted-text preservation, strict `StartDateTime < EndDateTime`, allowed past timing, initial `Draft`, and `Title nvarchar(200) NOT NULL`, `Location nvarchar(max) NOT NULL`, `Notes nvarchar(max) NULL`, `StartDateTime datetimeoffset NOT NULL`, `EndDateTime datetimeoffset NOT NULL`, `Status varchar(30) NOT NULL`, `CreatedByUserId uniqueidentifier NOT NULL`, `CreatedAt datetimeoffset NOT NULL`, and `RowVersion rowversion NOT NULL` semantics in `src/Modules/Events/Domain/Events/Event.cs` and `src/Modules/Events/Domain/Events/EventStatus.cs`
- [ ] T027 [P] [US1] Implement StaffingCycle creation with `EventId uniqueidentifier NOT NULL`, `CycleNumber int NOT NULL`, `EffectiveEventDate date NOT NULL`, `IsCurrent bit NOT NULL`, and `CreatedAt datetimeoffset NOT NULL` in `src/Modules/Events/Domain/StaffingCycles/StaffingCycle.cs`
- [ ] T028 [P] [US1] Implement append-only Event audit records with actor, action, Event resource, UTC occurrence, direct source, correlation ID, initial `Draft` status/field names, and no Notes content in `src/Infrastructure/Audit/AuditLog.cs` and `src/Infrastructure/Audit/EventAuditWriter.cs`
- [ ] T029 [P] [US1] Implement durable create-intent records with `ActorUserId uniqueidentifier NOT NULL`, `RequestToken uniqueidentifier NOT NULL`, fixed-length `PayloadFingerprint NOT NULL`, `EventId uniqueidentifier NOT NULL`, and `CreatedAt datetimeoffset NOT NULL` in `src/Modules/Events/Infrastructure/Persistence/DraftCreateRequest.cs`
- [ ] T030 [US1] Configure Event persistence with GUID PK, creator FK using `NO ACTION`, required/null column shapes, `CHECK StartDateTime < EndDateTime`, zero-offset UTC checks, status check, rowversion concurrency, and safe common blank-text checks in `src/Modules/Events/Infrastructure/Persistence/Configurations/EventConfiguration.cs`
- [ ] T031 [P] [US1] Configure StaffingCycle persistence with Event FK using `NO ACTION`, unique `(EventId, CycleNumber)`, and a filtered unique current-cycle index in `src/Modules/Events/Infrastructure/Persistence/Configurations/StaffingCycleConfiguration.cs`
- [ ] T032 [P] [US1] Configure append-only audit and create-intent persistence, including unique `(ActorUserId, RequestToken)` and Event/User FKs using `NO ACTION`, in `src/Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs` and `src/Modules/Events/Infrastructure/Persistence/Configurations/DraftCreateRequestConfiguration.cs`
- [ ] T033 [US1] Generate and review the additive initial feature migration for only Identity, Events, StaffingCycles, AuditLogs, and DraftCreateRequests with RCSI and named constraints in `src/Infrastructure/Persistence/Migrations/`
- [ ] T034 [P] [US1] Implement server-owned `Africa/Cairo` resolution that rejects nonexistent local times, requires a valid offset for ambiguous times, converts to zero-offset UTC, and returns server-derived Cairo values in `src/Modules/Events/Application/Time/CairoEventTimeResolver.cs`
- [ ] T035 [US1] Implement deterministic creation fingerprinting with fixed field order, exact preserved text including Notes null versus blank, resolved UTC instants, and selected Cairo offsets in `src/Modules/Events/Application/Drafts/DraftCreateFingerprint.cs`
- [ ] T036 [US1] Implement the create-Draft use case with Admin actor context, server validation, Event + cycle 1 + audit + request-ledger atomic commit, same-intent replay, changed-payload conflict, and uncertain-commit resolution in `src/Modules/Events/Application/Drafts/CreateDraftEventHandler.cs`
- [ ] T037 [P] [US1] Implement Admin-only Draft list/detail queries with Cairo projections, canonical UTC values, `Draft` filtering, paging, safe missing/non-Draft outcomes, and opaque version output in `src/Modules/Events/Application/Drafts/GetDraftEventHandler.cs` and `src/Modules/Events/Application/Drafts/ListDraftEventsHandler.cs`
- [ ] T038 [US1] Define typed create/list/detail DTOs that match `contracts/draft-events.openapi.yaml` without exposing EF entities in `src/Modules/Events/Application/Drafts/DraftEventContracts.cs`
- [ ] T039 [US1] Map Admin-only `GET /api/events/drafts`, `POST /api/events/drafts`, and `GET /api/events/drafts/{id}` with antiforgery on create, stable ProblemDetails, Location header, and replay header behavior in `src/Carpediem.Web/Endpoints/DraftEventsEndpoints.cs`
- [ ] T040 [P] [US1] Implement the Angular Draft API models/service with Cairo-local request values, canonical UTC response values, opaque versions, idempotency keys, and ProblemDetails decoding in `frontend/src/app/features/draft-events/data/draft-event.models.ts` and `frontend/src/app/features/draft-events/data/draft-events.service.ts`
- [ ] T041 [P] [US1] Build the reusable semantic Draft form with the five CHANGE-002 fields, explicit labels, Cairo help, touch/keyboard-friendly date-time inputs, visible save action, status regions, and dirty-state contract in `frontend/src/app/features/draft-events/components/draft-event-form/draft-event-form.component.ts`, `frontend/src/app/features/draft-events/components/draft-event-form/draft-event-form.component.html`, and `frontend/src/app/features/draft-events/components/draft-event-form/draft-event-form.component.scss`
- [ ] T042 [US1] Implement the create page with fresh-intent token creation, retry token retention, saving/success/failure states, discard warning, and post-save navigation in `frontend/src/app/features/draft-events/pages/draft-create/draft-create.component.ts` and `frontend/src/app/features/draft-events/pages/draft-create/draft-create.component.html`
- [ ] T043 [US1] Implement the compact Draft list with loading, empty, recoverable-error, and reopen states and register Admin-only create/list routes in `frontend/src/app/features/draft-events/pages/draft-list/draft-list.component.ts`, `frontend/src/app/features/draft-events/pages/draft-list/draft-list.component.html`, and `frontend/src/app/features/draft-events/draft-events.routes.ts`
- [ ] T044 [US1] Wire Events application/infrastructure services, endpoint mapping, EF configurations, and generated OpenAPI into the host in `src/Modules/Events/ServiceCollectionExtensions.cs` and `src/Carpediem.Web/Program.cs`
- [ ] T045 [US1] Run the US1 domain, integration, contract, Angular, and Playwright tests and record the independent MVP result in `specs/001-draft-event-creation-editing/quickstart.md`

**Checkpoint**: User Story 1 is a complete independently testable MVP for valid Draft creation and reopening.

---

## Phase 4: User Story 2 — Correct Invalid Draft Input (Priority: P1)

**Goal**: Admins receive actionable field feedback for every approved invalid-input class, retain correctable input, and never persist a partial or invalid Draft; valid past schedules remain accepted.

**Independent Test**: Submit each missing, whitespace-only, malformed, nonexistent/ambiguous Cairo, equal-time, and reversed-time case during creation, plus a valid past schedule, and prove correct field/global feedback, preserved form values, and unchanged persistence.

### Tests for User Story 2

- [ ] T046 [P] [US2] Add failing domain validation tests for missing required values, Unicode whitespace-only Title/Location, equal/reversed UTC instants, accepted blank/null Notes, exact accepted text, and valid past schedules in `tests/Carpediem.Domain.Tests/Events/DraftEventValidationTests.cs`
- [ ] T047 [P] [US2] Add failing Cairo conversion tests for ordinary values, invalid shapes, DST gaps, DST overlaps, invalid selected offsets, UTC conversion, and Cairo/UTC calendar-boundary cases in `tests/Carpediem.Domain.Tests/Events/CairoEventTimeResolverTests.cs`
- [ ] T048 [P] [US2] Add failing API tests for 400 malformed input, 422 field-addressable missing/blank/order/time validation, allowed overlap offsets, no partial persistence, and accepted past schedules in `tests/Carpediem.Api.IntegrationTests/DraftEvents/DraftValidationTests.cs`
- [ ] T049 [P] [US2] Add failing Angular tests for inline and summary errors, invalid-control semantics, focus movement, screen-reader announcements, preserved values, past-date acceptance, and correction/resubmission in `frontend/src/app/features/draft-events/components/draft-event-form/draft-event-form.component.spec.ts`
- [ ] T050 [P] [US2] Add failing Playwright cases for required fields, whitespace, equal/reversed dates, unusable Cairo times, correction, keyboard use, and persisted-state protection in `tests/e2e/draft-events-validation.spec.ts`

### Implementation for User Story 2

- [ ] T051 [US2] Implement field-addressable application validation with stable keys for required values, Unicode whitespace, Cairo time, and strict start-before-end failures while reusing domain enforcement and preserving accepted nonblank text in `src/Modules/Events/Application/Drafts/DraftEventInputValidator.cs`
- [ ] T052 [US2] Map malformed input, validation errors, ambiguous-time offset choices, and safe global failures to the documented 400/422 ProblemDetails contract in `src/Carpediem.Web/Errors/DraftEventProblemMapper.cs`
- [ ] T053 [US2] Complete client validation/error mapping, inline association, error-summary focus, live status feedback, value preservation, and correction resubmission in `frontend/src/app/features/draft-events/components/draft-event-form/draft-event-form.component.ts` and `frontend/src/app/features/draft-events/components/draft-event-form/draft-event-form.component.html`
- [ ] T054 [US2] Run the US2 domain, API, Angular, and Playwright tests and record the correction-flow result in `specs/001-draft-event-creation-editing/quickstart.md`

**Checkpoint**: User Stories 1 and 2 support valid creation plus correction of every approved invalid-input class.

---

## Phase 5: User Story 3 — Edit an Existing Draft (Priority: P2)

**Goal**: An Admin opens a Draft, changes any of its five authoring fields, and saves without changing lifecycle state; invalid, stale, failed, and non-Draft edits preserve the last committed state.

**Independent Test**: Open the same Draft in two Admin sessions, save valid field and schedule changes in the first, then attempt invalid and stale saves from the second; prove the Event remains Draft, Cairo/UTC values and cycle history are correct, one update audit is committed, and rejected saves preserve the last valid state.

### Tests for User Story 3

- [ ] T055 [P] [US3] Add failing domain tests for editing all five fields, unchanged `Draft` status, exact text preservation, same-Cairo-date cycle retention, cross-Cairo-date cycle replacement, immutable historical cycles, and non-Draft rejection in `tests/Carpediem.Domain.Tests/Events/EditDraftEventTests.cs`
- [ ] T056 [P] [US3] Add failing API/SQL integration tests for complete updates, rowversion advancement, stale conflict, invalid rollback, lifecycle guard, cycle uniqueness/history, audit allow-list, and Notes-content exclusion in `tests/Carpediem.Api.IntegrationTests/DraftEvents/UpdateDraftEventTests.cs`
- [ ] T057 [P] [US3] Add failing update contract tests for required opaque version, 200 response/new version, 404 missing Event, 409 `stale_draft`, 409 `not_draft`, 422 field errors, and safe 500 shape in `tests/Carpediem.Api.IntegrationTests/Contracts/UpdateDraftEventContractTests.cs`
- [ ] T058 [P] [US3] Add failing Angular tests for loading an existing Draft, UTC-derived Cairo display, dirty tracking, discard choice, successful version refresh, invalid save preservation, and stale review/reload behavior in `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.spec.ts`
- [ ] T059 [P] [US3] Add failing Playwright create→open→edit→reopen and two-session stale-edit scenarios in `tests/e2e/draft-events-edit.spec.ts`

### Implementation for User Story 3

- [ ] T060 [US3] Implement the Event aggregate Draft-edit method and cycle-date-change result without permitting lifecycle mutation in `src/Modules/Events/Domain/Events/Event.cs`
- [ ] T061 [US3] Implement the complete-field update use case with Admin actor, Draft guard, original rowversion predicate, Cairo conversion, atomic optional cycle switch, audit allow-list, and rollback behavior in `src/Modules/Events/Application/Drafts/UpdateDraftEventHandler.cs`
- [ ] T062 [US3] Extend update DTOs and map `PUT /api/events/drafts/{id}` with required version, antiforgery, 404/409/422/500 outcomes, and the refreshed Draft representation in `src/Modules/Events/Application/Drafts/DraftEventContracts.cs` and `src/Carpediem.Web/Endpoints/DraftEventsEndpoints.cs`
- [ ] T063 [US3] Implement the edit page with load/not-found/non-Draft states, populated Cairo values, dirty-form discard protection, save state, refreshed version, preserved unsaved values, and stale refresh/review action in `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.ts` and `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.html`
- [ ] T064 [US3] Register the Admin-only edit route and connect list/detail navigation without exposing Drafts in any Leader-facing route in `frontend/src/app/features/draft-events/draft-events.routes.ts` and `frontend/src/app/features/draft-events/pages/draft-list/draft-list.component.html`
- [ ] T065 [US3] Run the US3 domain, integration, contract, Angular, and Playwright tests and record the independent edit-flow result in `specs/001-draft-event-creation-editing/quickstart.md`

**Checkpoint**: User Stories 1–3 support complete valid creation, correction, reopening, and concurrency-safe Draft editing.

---

## Phase 6: User Story 4 — Handle Access and Save Failures (Priority: P2)

**Goal**: Unauthorized actors cannot discover or mutate Drafts, and Admins receive safe, actionable outcomes for missing Events, expired/lost authority, non-Draft state, conflicts, and recoverable or uncertain saves without losing form data unnecessarily.

**Independent Test**: Exercise every Draft endpoint as Admin, Chief Leader, Leader, and anonymous; request missing/non-Draft Events; expire or change authority during a form; and interrupt create/edit responses to prove correct 401/403/404/409/500 behavior, no data changes, retained correctable values, and safe retry/outcome resolution.

### Tests for User Story 4

- [ ] T066 [P] [US4] Add failing authorization matrix tests for anonymous, Admin, Chief Leader, and Leader access to every Draft endpoint plus ordinary-Leader Draft query privacy in `tests/Carpediem.Api.IntegrationTests/DraftEvents/DraftAuthorizationTests.cs`
- [ ] T067 [P] [US4] Add failing failure-contract tests for expired session, lost Admin role, unknown Event, non-Draft Event, antiforgery failure, unexpected exception, trace ID, safe messages, and zero writes/audits in `tests/Carpediem.Api.IntegrationTests/DraftEvents/DraftFailureTests.cs`
- [ ] T068 [P] [US4] Add failing Angular tests for 401 reauthentication guidance, 403 denied state, 404 safe state, global 409/500 feedback, uncertain create resolution, retry token retention, and edit-form preservation in `frontend/src/app/features/draft-events/data/draft-events-failure.spec.ts`
- [ ] T069 [P] [US4] Add failing Playwright scenarios for non-Admin denial, unknown Draft, expired session during save, and interrupted create/edit recovery in `tests/e2e/draft-events-failures.spec.ts`

### Implementation for User Story 4

- [ ] T070 [US4] Apply `RequireAdmin`, Draft-only resource checks, antiforgery, and uniform 401/403/404/409/500 ProblemDetails behavior to all Draft endpoints in `src/Carpediem.Web/Endpoints/DraftEventsEndpoints.cs`
- [ ] T071 [US4] Complete query and mutation failure mapping so missing IDs, non-Draft resources, stale versions, idempotency-key reuse, and unexpected failures have distinct stable codes without data leakage in `src/Carpediem.Web/Errors/DraftEventProblemMapper.cs`
- [ ] T072 [US4] Implement frontend failure recovery with retained form state, explicit outcome checking for uncertain create, safe retry, reauthentication guidance, denied/not-found states, and no false success announcements in `frontend/src/app/features/draft-events/data/draft-events.service.ts`, `frontend/src/app/features/draft-events/pages/draft-create/draft-create.component.ts`, and `frontend/src/app/features/draft-events/pages/draft-edit/draft-edit.component.ts`
- [ ] T073 [US4] Run the US4 authorization, failure-contract, Angular, and Playwright tests and record the independent access/recovery result in `specs/001-draft-event-creation-editing/quickstart.md`

**Checkpoint**: All four user stories are independently testable and the feature handles approved access, state, and recovery failures safely.

---

## Phase 7: Polish & Cross-Cutting Verification

**Purpose**: Complete traceability, accessibility, responsive behavior, migration safety, observability review, and full feature validation without adding later slices.

- [ ] T074 [P] Add automated axe checks and manual-review fixtures for labels, focus, order, error/status announcements, contrast, non-color meaning, 200% text zoom, and reduced motion in `tests/e2e/draft-events-accessibility.spec.ts`
- [ ] T075 [P] Add mobile layout checks at 320/360/390/430 CSS pixels for one-column flow, no normal horizontal scrolling, at least 44×44px touch targets, visible save action, stable feedback space, and keyboard completion in `tests/e2e/draft-events-responsive.spec.ts`
- [ ] T076 Compare generated OpenAPI with both approved contracts, correct implementation drift to match them, and change a contract only through an approved artifact correction in `specs/001-draft-event-creation-editing/contracts/draft-events.openapi.yaml` and `specs/001-draft-event-creation-editing/contracts/session-bootstrap.openapi.yaml`
- [ ] T077 Generate and inspect the idempotent deployment SQL for FK/check/index names, zero-offset constraints, RCSI, reversibility, and absence of destructive or future-slice schema changes in `src/Infrastructure/Persistence/Migrations/` and `artifacts/migrations/001-draft-event-creation-editing.sql`
- [ ] T078 Audit structured logs, traces, metrics, health output, and audit history to ensure correlation is present while credentials, form contents, Notes text, and raw requests remain absent in `src/Carpediem.Web/Observability/ObservabilityConfiguration.cs` and `src/Infrastructure/Audit/EventAuditWriter.cs`
- [ ] T079 Run the full .NET, Angular, contract, SQL Server integration, Playwright, accessibility, and responsive suites and record command evidence in `specs/001-draft-event-creation-editing/quickstart.md`
- [ ] T080 Execute the complete quickstart manually from a clean local environment, reconcile any command/path drift, and record final results in `specs/001-draft-event-creation-editing/quickstart.md`
- [ ] T081 Review the implementation against FR-001–FR-028, all 20 acceptance scenarios, SC-001–SC-007, and the SLICE-02 scope exclusions; record traceability and escalate any deviation for formal clarification in `specs/001-draft-event-creation-editing/implementation-review.md`

**Checkpoint**: The implemented slice satisfies the approved requirements, contracts, constitution, and quickstart with no later-slice capability added.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 — Setup**: Starts immediately.
- **Phase 2 — Foundational**: Depends on T001–T006 and blocks all user stories.
- **Phase 3 — US1**: Depends on Phase 2 and creates the shared Event, cycle, persistence, API, and Draft form capabilities used by later stories.
- **Phase 4 — US2**: Depends on US1's base aggregate, create endpoint, and form; it can begin once T026, T034, T039, and T041 exist.
- **Phase 5 — US3**: Depends on US1's create/read path and shared form. It may run alongside US2 after those shared prerequisites are stable.
- **Phase 6 — US4**: Depends on the endpoints and screens whose failure behavior it completes. Authorization tests can begin after Phase 2; final recovery work follows US1 and US3.
- **Phase 7 — Polish**: Depends on all user stories selected for delivery.

### User Story Dependency Graph

```text
Setup → Foundation → US1 Create/Reopen ─┬→ US2 Correct Invalid Input ─┐
                                       ├→ US3 Edit Draft ────────────┼→ US4 Access/Failures → Polish
                                       └─────────────────────────────┘
```

### Within Each User Story

1. Create the story's tests and confirm they fail for the expected missing behavior.
2. Implement domain/data behavior before application handlers.
3. Implement handlers and DTOs before endpoint and client integration.
4. Complete frontend behavior before story E2E validation.
5. Run the story's focused suites and satisfy its checkpoint before treating the story as complete.

## Parallel Opportunities

- After T001, T002–T004 and T006 can proceed in parallel; T005 waits for the project files.
- In Phase 2, backend persistence, identity, error handling, observability, frontend shell/styles, and test harness tasks marked `[P]` can proceed in parallel, with integration deferred until their dependencies exist.
- Within US1, T020–T025 can be authored in parallel; T026–T029 and T034 can then proceed in parallel; persistence configuration and frontend components can proceed in separate workstreams.
- After US1 shared prerequisites exist, US2 validation and US3 edit work can proceed in parallel because their primary files differ.
- US4 authorization/failure tests can be authored while US2/US3 implementation progresses, then completed against the final endpoints and screens.
- T074 and T075 can run in parallel after all story screens exist.

## Parallel Example: User Story 1

```text
Task T020: Domain creation tests
Task T021: API/SQL creation tests
Task T022: Idempotency/concurrency tests
Task T023: API contract tests
Task T024: Angular create/list tests
Task T025: Playwright create/reopen test

Then, after expected failures are recorded:

Task T026: Event aggregate
Task T027: StaffingCycle entity
Task T028: Event audit record/writer
Task T029: Durable create-intent entity
Task T034: Cairo time resolver
```

## Implementation Strategy

### MVP First

1. Complete Phase 1.
2. Complete Phase 2.
3. Complete Phase 3 (US1).
4. Stop and validate the independent create/reopen workflow, including atomicity, UTC/Cairo handling, audit, authorization, and idempotent replay.
5. Continue only after the MVP checkpoint passes.

### Incremental Delivery

1. **US1** delivers valid Draft creation and reopening.
2. **US2** completes invalid-input correction and field feedback.
3. **US3** adds lifecycle-safe, concurrency-safe Draft editing.
4. **US4** completes access denial and recoverable failure behavior.
5. **Polish** closes accessibility, mobile, migration, observability, and traceability gates.

## Requirements Coverage Summary

| Coverage area | Primary tasks |
|---|---|
| Create/view authorization and Draft privacy (FR-001–005) | T009–T011, T037–T044, T066–T072 |
| CHANGE-002 fields, validation, and Cairo/UTC behavior (FR-006–011) | T020–T021, T026, T030, T034, T046–T053 |
| Atomic Event/current-cycle creation and safe edits (FR-012–015) | T021, T027–T033, T036, T055–T062 |
| Draft lifecycle and audit guards (FR-016–017) | T028, T036, T055–T062, T078 |
| Duplicate creation and stale edits (FR-018–019) | T022, T029, T032, T035–T036, T056–T063 |
| Feedback, abandonment, missing data, and recovery (FR-020–024) | T041–T043, T049–T053, T058–T073 |
| Mobile and WCAG 2.2 AA (FR-025–028) | T017, T019, T041, T049, T074–T075 |
| Success criteria and complete acceptance evidence (SC-001–007) | T045, T054, T065, T073, T074–T081 |

## Notes

- `[P]` means the task changes different files and has no unmet dependency on another task in the same parallel group.
- Story labels provide traceability; setup, foundational, and final cross-cutting tasks intentionally have no story label.
- Do not introduce Chief Leader appointment, publication, availability, staffing, cancellation, replacement, rescheduling, completion, attendance, notifications, dashboards, account administration, feedback, or incidents.
- Do not use Title, schedule, or Location as a synthetic uniqueness rule.
- Do not replace Cairo zone rules with a fixed UTC offset or the Admin device time zone.
- Do not run automatic production migrations on application startup.
- Commit after a tested task or coherent task group; do not commit unrelated untracked design assets or `logo.pdf` with this feature.
