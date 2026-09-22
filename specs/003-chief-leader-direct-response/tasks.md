---
description: "Actionable task list for SLICE-04 Chief Leader direct response"
---

# Tasks: SLICE-04 — Chief Leader In-Platform Response

**Input**: `specs/003-chief-leader-direct-response/{spec,plan,research,data-model,quickstart}.md` and `contracts/chief-leader-direct-response.openapi.yaml`

**Tests**: Required by the constitution. Write focused tests before corresponding behavior.

**Organization**: Tasks follow the four user stories. Shared response contracts and role policy are foundational. US2 and US3 use US1's candidate context; each final transition is independently testable.

## Phase 1: Setup

- [ ] T001 Create the response application folder at `src/Modules/Events/Application/ChiefLeaderResponses/` and frontend feature folder at `frontend/src/app/features/chief-leader-response/`
- [ ] T002 Create SQL/API integration test folder at `tests/Carpediem.Api.IntegrationTests/ChiefLeaderResponses/` and domain test file at `tests/Carpediem.Domain.Tests/Events/ChiefLeaderResponseTests.cs`

## Phase 2: Foundational

- [ ] T003 [P] Define response context/result, action, store contract, and typed failure outcomes in `src/Modules/Events/Application/ChiefLeaderResponses/IChiefLeaderResponseStore.cs`
- [ ] T004 [P] Add current active Chief Leader and active Admin membership query contracts in `src/Modules/Identity/Application/IChiefLeaderResponseDirectory.cs`
- [ ] T005 [P] Add a `RequireChiefLeader` policy beside the existing Admin policy in `src/Modules/Identity/Authorization/AuthorizationPolicies.cs` and register it in `src/Carpediem.Web/Program.cs`
- [ ] T006 [P] Add `ChiefLeaderDeclined` notification intent factory/staging contract in `src/Modules/Notifications/Domain/InPlatformNotification.cs` and `src/Modules/Notifications/Application/IChiefLeaderDeclineNotificationWriter.cs`
- [ ] T007 Register response store, Identity directory, and notification writer in `src/Infrastructure/Persistence/ServiceCollectionExtensions.cs`

## Phase 3: User Story 1 — Review My Pending Appointment (P1)

**Independent test**: An active appointed candidate can find and read their own Pending Draft Event context; other candidates, Leaders, inactive users, and anonymous actors cannot.

- [ ] T008 [P] [US1] Add failing SQL/API tests for own Pending list/detail, other-candidate denial, inactive/revoked role, unknown ID, and no Draft public exposure in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderResponses/ChiefLeaderResponseReadTests.cs`
- [ ] T009 [P] [US1] Add failing Angular service and page tests for context, explicit actions, loading, unknown, and authorization states in `frontend/src/app/features/chief-leader-response/chief-leader-response.spec.ts`
- [ ] T010 [US1] Implement active Chief Leader, exact candidate, and active Admin recipient queries in `src/Modules/Identity/Infrastructure/EfChiefLeaderResponseDirectory.cs`
- [ ] T011 [US1] Implement candidate-only Pending list and assignment/Event detail reads in `src/Infrastructure/Persistence/EfChiefLeaderResponseStore.cs`
- [ ] T012 [US1] Add typed list/detail GET, Chief Leader policy, safe 400/401/403/404 results, and OpenAPI metadata in `src/Carpediem.Web/Endpoints/ChiefLeaderResponseEndpoints.cs`
- [ ] T013 [US1] Register the list/detail GET endpoints in `src/Carpediem.Web/Program.cs`
- [ ] T014 [US1] Implement typed Pending list and detail GET in `frontend/src/app/features/chief-leader-response/chief-leader-response.service.ts`
- [ ] T015 [US1] Build mobile list and review routes with Event title, Cairo-local schedule, location, Pending status, and explicit choices in `frontend/src/app/features/chief-leader-response/chief-leader-response.component.ts`, `.html`, and `.scss`
- [ ] T016 [US1] Add Chief Leader role UX state and route guard in `frontend/src/app/core/auth/session.service.ts` and `frontend/src/app/core/auth/chief-leader.guard.ts`; register candidate routes in `frontend/src/app/app.routes.ts` and link the Pending list in `frontend/src/app/layout/app-shell.component.html`

## Phase 4: User Story 2 — Accept My Appointment (P1)

**Independent test**: Valid direct accept produces one Active assignment and direct audit, leaves the Event Draft, and rejects an Active same-Event team conflict.

- [ ] T017 [P] [US2] Add failing domain tests for Pending → Active, UTC/candidate attribution, non-Pending rejection, and same-Event team conflict in `tests/Carpediem.Domain.Tests/Events/ChiefLeaderResponseTests.cs`
- [ ] T018 [P] [US2] Add failing SQL/API tests for Active persistence, audit, role/ownership/CSRF guards, Draft preservation, and repeat accept in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderResponses/ChiefLeaderAcceptTests.cs`
- [ ] T019 [US2] Add guarded direct accept transition (`AcceptedAt` UTC, `AcceptedByUserId` candidate, `AcceptanceSource=Direct`) in `src/Modules/Events/Domain/ChiefLeaderAssignments/ChiefLeaderAssignment.cs`
- [ ] T020 [US2] Add `ChiefLeaderAccepted` immutable direct audit construction in `src/Infrastructure/Audit/ChiefLeaderResponseAuditWriter.cs`
- [ ] T021 [US2] Implement atomic accept, expected rowversion, same-action replay, and CHANGE-001 guard in `src/Infrastructure/Persistence/EfChiefLeaderResponseStore.cs`
- [ ] T022 [US2] Add antiforgery-protected Accept POST and safe 400/401/403/404/409 results in `src/Carpediem.Web/Endpoints/ChiefLeaderResponseEndpoints.cs`
- [ ] T023 [US2] Add typed accept request, one in-flight action, and Active feedback in `frontend/src/app/features/chief-leader-response/chief-leader-response.service.ts` and `chief-leader-response.component.ts`

## Phase 5: User Story 3 — Decline My Appointment (P1)

**Independent test**: Valid direct decline preserves the Declined row, records one direct audit, stages one alert per active Admin, and leaves the Event Draft without an Active Chief Leader.

- [ ] T024 [P] [US3] Add failing domain tests for Pending → Declined, UTC timestamp, no mandatory reason, and non-Pending rejection in `tests/Carpediem.Domain.Tests/Events/ChiefLeaderResponseTests.cs`
- [ ] T025 [P] [US3] Add failing SQL/API tests for historical row, active Admin alert fanout, atomic rollback, replay, and unchanged Event in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderResponses/ChiefLeaderDeclineTests.cs`
- [ ] T026 [US3] Add guarded direct decline transition (`DeclinedAt` UTC; acceptance fields remain null) in `src/Modules/Events/Domain/ChiefLeaderAssignments/ChiefLeaderAssignment.cs`
- [ ] T027 [US3] Implement `ChiefLeaderDeclined` notification staging with bounded context in `src/Infrastructure/Notifications/EfChiefLeaderDeclineNotificationWriter.cs`
- [ ] T028 [US3] Add `ChiefLeaderDeclined` direct audit construction in `src/Infrastructure/Audit/ChiefLeaderResponseAuditWriter.cs`
- [ ] T029 [US3] Implement atomic decline, Admin fanout, rowversion, and same-action replay in `src/Infrastructure/Persistence/EfChiefLeaderResponseStore.cs`
- [ ] T030 [US3] Add antiforgery-protected Decline POST and safe outcomes in `src/Carpediem.Web/Endpoints/ChiefLeaderResponseEndpoints.cs`
- [ ] T031 [US3] Add typed decline request and final-state feedback in `frontend/src/app/features/chief-leader-response/chief-leader-response.service.ts` and `chief-leader-response.component.ts`

## Phase 6: User Story 4 — Resolve Invalid or Uncertain Responses (P2)

**Independent test**: Missing, stale, opposite, concurrent, and failed responses produce one authoritative state with no duplicate audit/alert; candidate can reload.

- [ ] T032 [P] [US4] Add failing SQL/API tests for stale `If-Match`, racing opposite decisions, safe authorization/not-found errors, and no duplicate side effects in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderResponses/ChiefLeaderResponseConcurrencyTests.cs`
- [ ] T033 [P] [US4] Add failing Angular tests for uncertain save, reload, retained context, disabled overlapping choices, and focus/status feedback in `frontend/src/app/features/chief-leader-response/chief-leader-response.spec.ts`
- [ ] T034 [US4] Resolve SQL rowversion races and same-action retry outcomes in `src/Infrastructure/Persistence/EfChiefLeaderResponseStore.cs`
- [ ] T035 [US4] Complete recoverable error, reload, focus, accessible feedback, and stable mobile layout in `frontend/src/app/features/chief-leader-response/chief-leader-response.component.ts`, `.html`, and `.scss`

## Phase 7: Polish and Cross-Cutting Verification

- [ ] T036 Run Release solution build, domain tests, real SQL integration, Angular tests/build, and generated OpenAPI comparison; record evidence in `specs/003-chief-leader-direct-response/implementation-review.md`
- [ ] T037 Add and run Playwright response journey at 320/360/390/430 CSS px and desktop with keyboard, zoom, axe, and reduced-motion checks in `tests/e2e/chief-leader-response.spec.ts`
- [ ] T038 Add and run focused SQL-backed k6 response workload and record correctness/error results in `tests/performance/chief-leader-response.k6.js` and `specs/003-chief-leader-direct-response/implementation-review.md`
- [ ] T039 Review FR-001–021 and all 13 acceptance scenarios for full traceability, scope boundaries, and no schema change in `specs/003-chief-leader-direct-response/implementation-review.md`
- [ ] T040 Check `git diff --check`, changed/untracked files, and preserve pre-existing `logo.pdf` untracked in the feature branch

## Dependencies and Execution Order

- Setup and foundational contracts precede story work.
- US1 establishes candidate context. US2 and US3 independently add each final transition using that context.
- US4 exercises cross-story concurrency and recovery after both transitions exist.
- Tests precede corresponding implementation. Cross-cutting verification follows all stories.
- `[P]` denotes separate files or independent test surfaces where parallel work is safe.

## Parallel Examples

- After foundation, write the US1 SQL read tests in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderResponses/ChiefLeaderResponseReadTests.cs` while writing Angular page tests in `frontend/src/app/features/chief-leader-response/chief-leader-response.spec.ts`.
- Within US2, domain transition tests in `tests/Carpediem.Domain.Tests/Events/ChiefLeaderResponseTests.cs` and SQL/API tests in `tests/Carpediem.Api.IntegrationTests/ChiefLeaderResponses/ChiefLeaderAcceptTests.cs` use separate files.
- Within US3, domain decline tests and SQL/API alert tests likewise use separate files.

## Incremental Delivery

1. Complete setup and foundation.
2. Deliver the candidate-only Pending list/detail (US1) and verify it independently.
3. Add direct acceptance (US2) and verify Active state and audit.
4. Add direct decline (US3) and verify history, audit, and Admin alert.
5. Add concurrency/recovery coverage (US4), then run the cross-cutting gate.

## Requirement Coverage

| Requirements | Tasks |
|---|---|
| FR-001–005 | T008–T016, T032 |
| FR-006–008 | T017–T023 |
| FR-009–013 | T024–T031 |
| FR-014–018 | T032–T035 |
| FR-019–020 | T009, T015, T033, T035, T037 |
| FR-021 | T039 |
