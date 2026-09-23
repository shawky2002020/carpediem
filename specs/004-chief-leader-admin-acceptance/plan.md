# Implementation Plan: SLICE-05 — Chief Leader Admin-Recorded External Acceptance

**Branch**: `codex/004-chief-leader-admin-acceptance` | **Date**: 2026-09-23 | **Spec**: [spec.md](spec.md) | **Frozen requirements commit**: `d9ce950`

## Summary

Extend the existing Admin Chief Leader appointment view with an explicit external-acceptance form for a Pending appointment. A meaningful justification is required. The server atomically activates the existing assignment with `AdminExternal` provenance and appends immutable audit evidence. The Event remains Draft, and direct candidate response, publication, staffing, and notification delivery remain separate slices.

## Technical Context

| Area | Decision |
|---|---|
| Language/runtime | Existing C# 14/.NET 10 and TypeScript/Angular 22 |
| Backend | ASP.NET Core REST, EF Core 10, SQL Server 2025, Identity cookies and Admin policy authorization |
| Frontend | Existing standalone Admin appointment route, signals, HTTP service, reactive justification control, shared design tokens |
| Data | Existing assignment and audit tables; no schema addition expected |
| Tests | Domain xUnit; real SQL API integration; Angular; mobile Playwright; focused k6 |
| Scale | Approximately 100–150 users, 10–15 Chief Leaders, 2–20 Events monthly |
| Constraints | 320 CSS px viewport, 44×44 touch targets, WCAG 2.2 AA, RFC 7807 errors, 1,000-character justification maximum; no invented latency gate |

No technical clarification remains.

## Constitution Check

| Gate | Before design | After design | Evidence |
|---|---|---|---|
| Spec and testable rules | PASS | PASS | Clarified spec, bounded reason rule, acceptance traceability |
| Modular monolith | PASS | PASS | Events owns transition; Infrastructure coordinates persistence; Web maps contract |
| Server authorization | PASS | PASS | Admin policy plus active-Admin persistence check |
| Relational integrity | PASS | PASS | Existing FK/checks/filtered index/rowversion and one transaction |
| Audit and history | PASS | PASS | Mandatory immutable `AdminExternal` audit evidence and preserved assignment history |
| API and security | PASS | PASS | Typed OpenAPI, antiforgery, opaque version, safe ProblemDetails |
| Mobile and accessibility | PASS | PASS | Existing mobile Admin surface, labeled note, focus/status checks |
| Simplicity | PASS | PASS | No migration, external-channel integration, or speculative staffing table |
| Layered verification | PASS | PASS | Unit, SQL, contract, UI, E2E, and load |

No constitutional exception is requested.

## Research Decisions

See [research.md](research.md). Reuse the current assignment row and rowversion. A successful retry is recognized from the Active `AdminExternal` state and its matching immutable audit evidence; it does not append a second audit record. A direct candidate response or other competing transition wins if it commits first.

CHANGE-001 requires an Active team-assignment guard at activation. No TeamAssignment storage exists yet. The domain transition keeps the conflict input and rejects `true`; the future Staffing implementation must connect the authoritative current-cycle lookup before it creates Active team assignments.

## Architecture and Ownership

```text
Admin appointment page
  → explicit external-acceptance POST + antiforgery + opaque version
  → Admin authorization and active-account verification
  → Events external-acceptance use case and assignment transition
  → one SQL transaction: assignment + mandatory audit evidence
```

- **Events** owns the Pending-to-Active transition, Draft guard, assignment identity, and operational ownership result.
- **Identity** supplies current active Admin authority through existing role membership.
- **Infrastructure** validates the mandatory reason, coordinates rowversion concurrency, stages typed audit evidence, and commits atomically.
- **Web** extends the Admin appointment route with a typed mutation and safe error mapping.
- **Frontend** extends the existing Admin appointment view; it does not create a second management surface.

## External Acceptance Flow

1. Admin opens the existing Draft Event appointment context. The server returns the current Pending assignment, candidate, and opaque version.
2. Admin explicitly opens “Record external acceptance,” reads the attestation text, and enters a justification of 1–1,000 meaningful characters.
3. Client allows one in-flight submission and sends assignment identity, justification, and opaque version with antiforgery protection.
4. Server rechecks active Admin authority, Event Draft state, exact Pending assignment, expected version, reason, and CHANGE-001 activation guard.
5. The assignment becomes Active with UTC `AcceptedAt`, acting Admin `AcceptedByUserId`, and `AcceptanceSource=AdminExternal`.
6. One audit entry with source `AdminExternal`, mandatory reason, assignment/Event/candidate context, Admin actor, and correlation commits in the same transaction.
7. Same-intent replay returns the committed assignment without another audit. Direct response, opposite state, stale data, or a competing activation produces a safe conflict/current-state outcome.

## API Contract

See [contracts/chief-leader-admin-acceptance.openapi.yaml](contracts/chief-leader-admin-acceptance.openapi.yaml).

| Operation | Success | Failures |
|---|---|---|
| `POST /api/events/drafts/{eventId}/chief-leader-appointment/{assignmentId}/external-acceptance` | 200 Active, including same-intent replay | 400/CSRF, 401, 403, 404, 409, 422, 5xx |

The request carries `justification`; mutation headers carry `If-Match` and `X-XSRF-TOKEN`. The existing Admin context GET is the recovery read after an uncertain outcome. Error bodies use the existing safe RFC 7807 shape and correlation identifier.

## Data and Consistency

See [data-model.md](data-model.md). Reuse the existing schema. Assignment acceptance fields already support `AdminExternal`; audit reason length is already 1,000. No migration is planned.

The domain transition and audit record are staged before one save/transaction commit. Audit construction rejects blank or over-limit reasons. Rowversion and the filtered one-Active-per-Event index protect races. Same-intent replay verifies existing assignment provenance and audit evidence rather than relying on client UI state.

## Security, UX, and Observability

- Enforce current active Admin membership on the server. Do not trust client Event, candidate, source, timestamp, or actor values.
- Keep cookie, antiforgery, correlation ID, and structured outcome telemetry conventions. Do not log justification text in request logs or telemetry.
- Explain that the Admin is recording an external confirmation. Label the justification, associate errors, show remaining/maximum length without color-only meaning, and preserve text on recoverable failure.
- Verify 320/360/390/430 CSS px, 44×44 targets, keyboard/focus, 200% text, reduced motion, stable saving state, discard protection, and accessible success/error announcements.

## Verification and Traceability

| Spec scope | Evidence |
|---|---|
| FR-001–005; US1 | Admin context/action visibility, authorization, exact Pending/Draft guards |
| FR-006–012; US2 | Domain validation/transition/team guard, SQL provenance, ownership, atomic mandatory audit |
| FR-013–015; US4 | Replay, rowversion and direct-response races, typed safe errors |
| FR-016–018; US3/US4 | Cancel/discard, single submission, preserved reason and recovery read |
| FR-019–020 | Angular semantics, mobile Playwright, keyboard, scaling, axe, reduced motion |
| FR-021 | Surface review excludes direct response, publication, replacement, staffing, inbox, and external integration |

## Project Structure

```text
src/Modules/Events/Domain/ChiefLeaderAssignments/
src/Modules/Events/Application/ChiefLeaderExternalAcceptances/
src/Infrastructure/Audit/
src/Infrastructure/Persistence/
src/Carpediem.Web/Endpoints/
frontend/src/app/features/chief-leader-appointment/
tests/
```

## Complexity Tracking

No new module, database migration, external service, notification type, or request ledger is required. Existing state plus immutable audit evidence supports safe same-intent replay.
