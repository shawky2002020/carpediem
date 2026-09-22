# Implementation Plan: SLICE-04 — Chief Leader In-Platform Response

**Branch**: `codex/003-chief-leader-direct-response` | **Date**: 2026-09-22 | **Spec**: [spec.md](spec.md) | **Frozen requirements commit**: `6f054ba`

## Summary

Give the appointed, active Chief Leader a focused view of their Pending appointment and explicit Accept and Decline actions. Reuse the SLICE-03 assignment, audit, notification, authentication, and SQL transaction foundation. Accept activates the existing record; decline preserves it as Declined and stages an alert for each active Admin. Neither action changes the Draft Event.

## Technical Context

| Area | Decision |
|---|---|
| Language/runtime | Existing C# 14/.NET 10 and TypeScript/Angular 22 |
| Backend | ASP.NET Core REST, EF Core 10, SQL Server 2025, Identity cookies and policy authorization |
| Frontend | Standalone Angular routes, signals, HTTP service, shared design tokens |
| Data | Existing assignment, notification, and audit tables; no schema addition expected |
| Tests | Domain xUnit; real SQL API integration; Angular; mobile Playwright; focused k6 |
| Scale | Approximately 100–150 users, 10–15 Chief Leaders, 2–20 Events monthly |
| Constraints | 320 CSS px viewport, 44×44 touch targets, WCAG 2.2 AA, RFC 7807 errors; no invented latency gate |

No technical clarification remains.

## Constitution Check

| Gate | Before design | After design | Evidence |
|---|---|---|---|
| Spec and testable rules | PASS | PASS | Clarified spec and acceptance traceability |
| Modular monolith | PASS | PASS | Events owns response; Identity verifies role; Notifications stages alert |
| Server authorization | PASS | PASS | Policy plus active-candidate check on every read/write |
| Relational integrity | PASS | PASS | Existing FK/checks/indexes/rowversion and one transaction |
| Audit and history | PASS | PASS | Immutable direct audit and preserved Declined row |
| API and security | PASS | PASS | Typed OpenAPI, antiforgery, safe ProblemDetails |
| Mobile and accessibility | PASS | PASS | Focused mobile flow and explicit focus/status checks |
| Simplicity | PASS | PASS | No new infrastructure or speculative staffing table |
| Layered verification | PASS | PASS | Unit, SQL, contract, UI, E2E, and load |

No constitutional exception is requested.

## Research Decisions

See [research.md](research.md). The existing rowversion and Pending/Active/Declined state suffice. Repeated same action observes the committed state and returns it without a new audit or alert. A competing or opposite decision conflicts. The server never trusts a client-supplied candidate ID.

CHANGE-001 requires an Active team-assignment guard at activation. No such record can exist in the current schema. The domain transition accepts the conflict fact and rejects `true`; the future Staffing module must supply the authoritative lookup before it creates Active team assignments. No staffing workflow is implemented here.

## Architecture and Ownership

```text
Chief Leader response route
  → same-origin GET / response POST + antiforgery
  → ChiefLeader policy + candidate/active-user check
  → Events response use case and assignment transition
  → one SQL transaction: assignment + audit + Admin alerts
```

- **Events** owns assignment transitions, response orchestration, Event Draft guard, and candidate relationship.
- **Identity** supplies active role membership and active Admin recipients through narrow contracts.
- **Notifications** owns semantic `ChiefLeaderDeclined` notification intents; no inbox behavior.
- **Infrastructure** coordinates one database transaction and rowversion concurrency.
- **Web** exposes typed candidate-only endpoints and maps safe errors.

## Response Flow

1. Candidate opens a Pending assignment link. Server checks active Chief Leader membership and matching candidate ID, then returns Event title, Cairo-local schedule, location, status, and opaque version.
2. Candidate chooses Accept or Decline explicitly; the client permits one in-flight submission.
3. Server rechecks role, assignment, Event Draft state, expected version, and Pending state. Accept applies CHANGE-001 same-Event exclusivity.
4. Accept sets Active, `AcceptedAt`, `AcceptedByUserId`, `AcceptanceSource=Direct`; decline sets Declined and `DeclinedAt`. Event stays Draft.
5. State, audit, and on decline an alert for each active Admin commit together.
6. Repeated same action returns the already committed result without new side effects. Opposite/stale action conflicts. Failed transaction leaves Pending.

## API Contract

See [contracts/chief-leader-direct-response.openapi.yaml](contracts/chief-leader-direct-response.openapi.yaml).

| Operation | Success | Failures |
|---|---|---|
| `GET /api/chief-leader/appointments/{assignmentId}` | 200 own context | 400, 401, 403, 404, 5xx |
| `POST /api/chief-leader/appointments/{assignmentId}/accept` | 200 Active, including same-action replay | 400/CSRF, 401, 403, 404, 409, 5xx |
| `POST /api/chief-leader/appointments/{assignmentId}/decline` | 200 Declined, including same-action replay | 400/CSRF, 401, 403, 404, 409, 5xx |

Mutations send the opaque assignment version in `If-Match`. The candidate can GET after an uncertain outcome. Error bodies use safe RFC 7807 shape and correlation ID.

## Data and Consistency

See [data-model.md](data-model.md). Reuse the existing schema. `AcceptanceSource=Direct` and `AcceptedByUserId` describe direct acceptance; decline does not populate acceptance fields. The decline notification type fits the existing column. Audit `Source=Direct` describes both transitions. No migration is planned. Verify actual SQL concurrency behavior in integration tests. Staffing must connect the team-assignment lookup before that module creates Active assignments.

## Security, UX, and Observability

- Enforce current role membership, active user, and exact appointment ownership for every operation. Do not expose Admin recipients.
- Keep cookie/antiforgery controls, correlation IDs, structured outcome logs, and safe errors. Do not log raw notification context.
- Show Event context and Pending status before large actions. Verify 320/360/390/430 CSS px, stable loading/saving states, keyboard focus, 200% text, reduced motion, and accessible feedback.
- Preserve context on failure; allow reload and safe retry; prevent simultaneous opposite submissions.

## Verification and Traceability

| Spec scope | Evidence |
|---|---|
| FR-001–005; US1 | Candidate-only read, Draft eligibility, explicit action, non-public visibility |
| FR-006–008; US2 | Domain accept/team guard, SQL Active/direct attribution, Draft unchanged |
| FR-009–013; US3 | Domain decline, SQL historical row, one audit/Admin alerts, rollback |
| FR-014–018; US4 | Replay, opposite response, rowversion race, errors, uncertain reload |
| FR-019–020 | Angular semantics, mobile Playwright/keyboard/zoom/axe/reduced motion |
| FR-021 | Surface review excludes external acceptance, publication, inbox, staffing |

## Project Structure

```text
src/Modules/Events/Domain/ChiefLeaderAssignments/
src/Modules/Events/Application/ChiefLeaderResponses/
src/Modules/Notifications/
src/Infrastructure/Persistence/
src/Carpediem.Web/Endpoints/
frontend/src/app/features/chief-leader-response/
tests/
```

## Complexity Tracking

No new module, migration, broker, or request ledger is required. State-based same-action replay prevents duplicate transitions and side effects.
