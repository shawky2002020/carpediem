# Implementation Plan: SLICE-03 — Chief Leader Appointment & Notification

**Branch**: `codex/002-chief-leader-appointment` | **Date**: 2026-09-22 | **Spec**: [spec.md](spec.md) | **Frozen requirements commit**: `71cad19`

**Input**: Clarified SLICE-03 specification with 27 functional requirements and 16 acceptance scenarios. This plan extends the completed SLICE-02 vertical host and does not implement response, publication, or the notification inbox.

## Summary

Add an Admin-only mobile appointment flow to an existing Draft Event. The flow reads active members of the predefined Chief Leader role, creates one Pending `ChiefLeaderAssignment`, records `ChiefLeaderAppointed` audit evidence, and stages one persistent `ChiefLeaderAppointment` notification intent in the same SQL Server transaction. The server rechecks Event state, candidate eligibility, and unresolved assignment state. Durable request identity and database constraints make uncertain, repeated, and concurrent submissions safe.

## Technical Context

| Area | Decision |
|---|---|
| Runtime | Existing C# 14 / .NET 10 LTS and TypeScript / Angular 22 |
| Backend | Existing ASP.NET Core REST/OpenAPI, EF Core 10, Identity cookie session, policy authorization, and RFC 7807 errors |
| Frontend | Existing standalone Angular routes, signals, Reactive Forms, RxJS HTTP service, and shared design tokens |
| Data | SQL Server 2025; additive migration in the single host context; `events`, `notifications`, and `audit` schemas |
| Testing | xUnit domain tests; real SQL Server integration and contract tests; Angular component/service tests; focused Playwright; k6 appointment workload |
| Scale | Existing V1 scale of roughly 100–150 users and 2–20 Events per month; candidate result is intentionally unpaged at this scale |
| Quality | Existing correlation, logs, health, 44×44 targets, 320 CSS px verification, WCAG 2.2 AA, and safe errors |

No technical clarification remains.

## Constitution Check

| Gate | Before design | After design | Evidence |
|---|---|---|---|
| Spec driven and testable | PASS | PASS | Frozen spec, acceptance traceability, and no unresolved marker |
| Modular monolith boundaries | PASS | PASS | Events owns assignment; Identity supplies role membership; Notifications owns notification entity; one host coordinates |
| Server authorization | PASS | PASS | Admin policy on read and write plus active-user/candidate checks at save |
| Relational integrity | PASS | PASS | FK, status check, filtered uniqueness, rowversion, request uniqueness, atomic transaction |
| Audit and history | PASS | PASS | Immutable appointment audit and preserved declined rows |
| Mobile and accessibility | PASS | PASS | Focused narrow-screen route, labeled selection, announcements, keyboard and zoom checks |
| Safe migration | PASS | PASS | Additive tables and indexes; generated SQL inspected; no destructive change |
| Simplicity | PASS | PASS | One database and in-process transaction; no broker, push channel, or speculative inbox |
| Layered verification | PASS | PASS | Domain, SQL integration, contract, frontend, E2E, accessibility, and realistic load checks |

No constitutional exception is requested.

## Source Reconciliation and Scope

1. The locked lifecycle permits `No Assignment → Pending` and `Declined → new Pending`. It does not permit replacement or cancellation of Pending, so an Event with Pending or Active assignment rejects appointment.
2. The locked database catalog guarantees at most one Active assignment. This slice adds an equivalent filtered unique index for Pending assignments and retains the application guard that forbids Pending beside Active. No declined history is overwritten.
3. CHANGE-001 applies operational exclusivity only when a Pending assignment later activates. Appointment therefore does not query or alter team participation.
4. ADR-006 requires a persistent semantic notification at appointment. This slice stages that record only; notification list, polling, read state, dismissal, and external channels remain SLICE-17.
5. Identity profile/display-name fields are not ratified. Candidate choices use the existing non-sensitive user name and stable user identifier; no new profile attributes are introduced.

## Architecture and Ownership

```text
Angular Draft Event → Chief Leader appointment route
  → same-origin Admin GET/POST + antiforgery + request token
  → Web endpoint and safe ProblemDetails
  → Events appointment application use case
      → Identity candidate directory contract
      → Event aggregate / ChiefLeaderAssignment rules
      → Notifications intent contract
      → Audit writer
  → single CarpediemDbContext transaction
      → identity.Users/UserRoles (read)
      → events.ChiefLeaderAssignments + AppointmentRequests
      → notifications.InPlatformNotifications
      → audit.AuditLogs
```

- **Events** owns `ChiefLeaderAssignment`, lifecycle rules, appointment command, result, and Event relationship.
- **Identity** exposes a narrow candidate directory that answers whether a user is active and currently holds `ChiefLeader`; no Events code accesses Identity internals directly.
- **Notifications** owns the semantic in-platform notification entity and public staging contract. It has no delivery worker or UI in this slice.
- **Infrastructure** maps all entities and implements the one-database unit of work. It may coordinate module contracts without moving business rules out of their owners.
- **Web** authenticates, enforces `RequireAdmin`, validates antiforgery/request controls, maps typed results/errors, and exposes no EF entity.

## Appointment Use Cases

### Open Appointment

1. Admin opens the appointment route from a Draft Event.
2. Server verifies an active Admin session, resolves the Event, and requires `Draft`.
3. It returns the Event context, current unresolved assignment if one exists, and active users in the predefined Chief Leader role. Inactive and non-Chief-Leader users are absent. User name is the distinguishing label.
4. If Pending or Active exists, the route shows that status and does not offer a second appointment. Declined history is retained but does not block candidate selection.

### Confirm Appointment

1. Client creates one random request token for the selected-candidate intent and reuses it across double taps or uncertain retries. Saving state suppresses overlapping interaction.
2. Server verifies active Admin authorization, antiforgery, request token, Event existence/state, current unresolved assignment, and current candidate eligibility.
3. `Event.AppointChiefLeader` creates a Pending assignment. Pending adds no team-participation guard.
4. The use case stages the assignment, a `ChiefLeaderAppointed` audit entry, one unread `ChiefLeaderAppointment` notification intent, and a request record, then commits once.
5. First success returns the assignment and Event link. Identical replay returns the same result. Reusing a token with different Event/candidate conflicts.
6. Concurrent requests are serialized by unique request and Pending-per-Event constraints. If another candidate wins, the loser gets a conflict and current state; if the same intent wins, replay resolves to the committed assignment.

### Failure Behavior

- Malformed identifiers or request control produce safe bad-request errors; expired authentication produces 401; non-Admin produces 403; missing Event produces 404.
- Non-Draft, already Pending/Active, stale competing selection, or changed-payload request-token reuse produce stable 409 outcomes.
- Inactive or non-Chief-Leader candidate produces field-addressable 422 validation without exposing role membership beyond the safe message.
- Transaction or network failure never presents partial assignment, audit, or notification as completed. The client retains selection and token until the result is resolved.

## API Contract

See [contracts/chief-leader-appointment.openapi.yaml](contracts/chief-leader-appointment.openapi.yaml).

| Operation | Success | Failure highlights |
|---|---|---|
| `GET /api/events/drafts/{eventId}/chief-leader-appointment` | 200 Event context, current unresolved assignment, eligible candidates | 400, 401, 403, 404, 409 non-Draft, safe 5xx |
| `POST /api/events/drafts/{eventId}/chief-leader-appointment` | 201 new Pending assignment; 200 identical replay | 400/CSRF, 401, 403, 404, 409 already assigned/concurrent/token conflict/non-Draft, 422 candidate ineligible, safe 5xx |

Candidate membership is authoritative only at save time. Every response remains typed and documented in generated OpenAPI. Error bodies use the existing stable ProblemDetails shape and correlation identifier.

## Data and Migration Design

See [data-model.md](data-model.md).

- Add `events.ChiefLeaderAssignments` with the locked fields and status/source checks. Preserve `Declined` rows and use `NO ACTION` foreign keys.
- Add a filtered unique index on `(EventId)` where `Status='Pending'`, alongside the locked Active filtered index. Domain/application logic prevents Pending and Active coexisting.
- Add `notifications.InPlatformNotifications` with recipient/Event FKs, semantic type, UTC creation time, nullable read time, and bounded context payload. Appointment initializes it unread. No dismissal column or external-delivery state is introduced.
- Add `events.ChiefLeaderAppointmentRequests`, unique by `(ActorUserId,RequestToken)`, with payload fingerprint and resulting assignment. It commits with the appointment and has no invented retention job.
- Reuse `audit.AuditLogs`; `ResourceType=ChiefLeaderAssignment`, `ResourceId=assignment ID`, `Action=ChiefLeaderAppointed`, `Source=Direct`, and bounded new-value context. No mandatory reason applies.
- Inspect the generated migration and idempotent script for schemas, FK delete behavior, checks, filtered indexes, unique request key, and UTC constraints. Production startup still does not auto-migrate.

## Security, Privacy, and Observability

- Apply `RequireAdmin` to candidate and appointment endpoints; validate the active user and candidate state server side. Angular route guards and hidden controls remain presentation only.
- Require antiforgery for POST. Use secure same-origin cookie settings already established. Do not return emails, claims, or role lists in candidate results.
- Avoid logging candidate lists, raw payloads, cookies, or notification context. Structured logs may include safe actor/Event/assignment IDs, outcome category, correlation ID, and elapsed time.
- Add trace spans and counters around appointment outcome categories using existing observability. `/healthz` remains the host readiness signal.

## Mobile-First and Accessibility Design

- Add a focused Admin route under the Draft Event flow. At 320/360/390/430 CSS px it uses one column, no data table, no horizontal scrolling, and a visible 44×44 or larger confirmation action.
- Candidate choices use semantic radio controls or an equivalent single-selection control with programmatic labels. The current Pending state is a textual status, not color alone.
- Loading, empty pool, selected, saving, success, validation, conflict, authorization, and recoverable error states have stable space and accessible status announcements.
- Keyboard order follows Event context → candidate selection → confirmation → cancel/back. Focus moves to the error summary or meaningful success destination. Text at 200% remains usable and reduced-motion preference is honored.
- Leaving before confirmation writes nothing. Recoverable failures preserve the candidate and request token. The primary action disables while the request is unresolved.

## Verification and Traceability

| Spec scope | Planned evidence |
|---|---|
| FR-001–008; US1/US2 | Domain and SQL integration tests for Draft/state/pool/active checks; API 401/403/404/409/422; candidate privacy |
| FR-009–015; US1.1–3 | Domain state test and SQL all-or-none assertions for assignment, audit, notification, unchanged Event |
| FR-016–018; US3/US4 | Same-token replay, changed payload, parallel same/different candidates, unique Pending/Active constraints, declined history |
| FR-019–023 | API/UI reopen, empty/error states, uncertain save retry, abandonment, safe ProblemDetails |
| FR-024–026 | Angular semantics; Playwright at mobile widths, keyboard, axe, 200% text, reduced motion, status announcements |
| FR-027 | Contract/surface review proving no response, publication, inbox, external channel, or staffing operation was added |

Load verification exercises concurrent candidate reads and appointment commands at V1 scale and asserts correctness/error rate. It does not invent a response-time gate absent from authoritative NFRs.

## Project Structure Changes

```text
src/Modules/Events/
  Domain/ChiefLeaderAssignments/
  Application/ChiefLeaderAppointments/
  Infrastructure/Persistence/Configurations/
src/Modules/Notifications/
  Domain/InPlatformNotification.cs
  Application/IAppointmentNotificationWriter.cs
  Infrastructure/Persistence/Configurations/
src/Modules/Identity/
  Application/IChiefLeaderCandidateDirectory.cs
src/Infrastructure/
  Persistence/ (DbSets, adapters, migration)
  Audit/ChiefLeaderAppointmentAuditWriter.cs
src/Carpediem.Web/Endpoints/ChiefLeaderAppointmentEndpoints.cs
frontend/src/app/features/chief-leader-appointment/
tests/ (domain, API integration, Angular, Playwright, k6 additions)
```

Creating a small Notifications module is required by the ratified ownership boundary and ADR-006. It contains only the entity and contract necessary for this business result.

## Complexity Tracking

No constitutional violation or speculative infrastructure is introduced. The durable request record is justified by the explicit uncertain-outcome and duplicate-prevention requirements; the Notifications module is justified by a ratified persisted notification side effect.
