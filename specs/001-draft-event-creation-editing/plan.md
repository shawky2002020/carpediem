# Implementation Plan: SLICE-02 — Draft Event Creation & Draft Editing

**Branch**: `codex/001-draft-event-creation-editing` | **Date**: 2026-09-18 | **Spec**: [spec.md](spec.md) | **Frozen requirements commit**: `761527b`

**Input**: Clarified SLICE-02 specification: 28 functional requirements and 20 acceptance scenarios. This document designs the feature; it creates no application code or task list.

## Summary

Deliver the first executable Admin path from an Angular 22 mobile form through an ASP.NET Core 10 API and the `Events` module to SQL Server 2025. The feature creates and edits only `Draft` Events. The server owns validation, Cairo-to-UTC conversion, authorization, lifecycle guards, idempotency, and audit. Creation commits the Event, initial current `StaffingCycle`, audit, and deduplication record together. Editing uses the locked Event concurrency token and commits accepted changes with audit; stale or invalid edits change nothing. `FOUNDATION-01` contributes only the host, identity, database, client shell, tests, and diagnostics needed for this path.

## Technical Context

| Area | Decision |
|---|---|
| Runtime | C# 14 / .NET 10 LTS; TypeScript / Angular 22 |
| Backend | ASP.NET Core 10 REST/OpenAPI; EF Core 10; Identity cookie sessions; policy authorization; RFC 7807 `ProblemDetails` |
| Frontend | Standalone routes, Reactive Forms, Signals for local view state, RxJS HTTP service; custom SCSS tokens; no NgRx |
| Data | SQL Server 2025, one host `CarpediemDbContext`, module schemas, RCSI, feature migration |
| Testing | xUnit; `WebApplicationFactory` + SQL Server Testcontainers; API contract; Angular tests; focused Playwright |
| Platform | Same-origin SPA/API per ADR-002/008; Docker Compose SQL Server locally |
| Scale | V1 roughly 100–150 Leaders and 2–20 Events/month; no speculative scaling infrastructure |
| Quality | 44×44 CSS-pixel targets, no normal mobile horizontal scrolling, WCAG 2.2 AA, safe errors, correlation IDs; no invented performance target |

## Source Reconciliation and Boundaries

1. [CHANGE-002](../../docs/decisions/CHANGE-002-draft-event-minimum-fields.md) ratifies Title, StartDateTime, EndDateTime, Location, and optional Notes. The older [relational schema listing](../../docs/data/RELATIONAL_SCHEMA.md) omits Location and Notes despite CHANGE-002's statement that they are already supported. The first feature migration realizes these two ratified attributes additively; do not silently assume the columns exist or redesign the architecture.
2. Older lifecycle/constraint text initializes a cycle at publication and derives its date by casting the Event start timestamp. The later approved [SLICE-02 definition](../../docs/roadmap/V1_FEATURE_SLICES.md) and [delivery handoff](../../docs/roadmap/V1_DELIVERY_ORDER.md) require an initial current cycle at Draft creation, while clarification fixes Cairo as the authoring calendar zone and UTC as the timestamp representation. Create the cycle at Draft creation using the Cairo start calendar date; casting a UTC value would select the wrong displayed date near midnight. A future publication flow must reuse the current cycle. No publication behavior is implemented here.
3. The approved [concurrency model](../../docs/data/CONCURRENCY_MODEL.md) treats `CreateDraftEvent` as an idempotency-key candidate and `Events` as rowversion-protected. A durable request record fulfills the spec's duplicate-prevention requirement without a synthetic uniqueness rule on Event fields.
4. Admins enter and review `Africa/Cairo` local time; Event instants are UTC. The API makes local input explicit and the server owns conversion. Never assume a fixed UTC offset.
5. Chief Leader appointment, publication, published rescheduling, volunteer staffing, notifications, cancellation, completion, and attendance remain later slices.

## Constitution Check

| Gate | Before research | After design | Evidence |
|---|---|---|---|
| Spec-driven, testable rules | PASS | PASS | Frozen spec and traceability below |
| Modular monolith and ownership | PASS | PASS | `Events` owns Event/StaffingCycle writes; Identity and Audit retain their boundaries |
| Server authorization and security | PASS | PASS | Admin policy, state guard, cookie/CSRF baseline |
| Relational integrity and SQL depth | PASS | PASS | FK/check/unique indexes, rowversion, atomic writes, migration review |
| Audit/history | PASS | PASS | Typed records commit with changes; old cycles retained |
| Mobile-first and WCAG 2.2 AA | PASS | PASS | Narrow-screen form and accessible focus/error behavior |
| Safe migrations | PASS | PASS | Additive first migration and inspected SQL script; no runtime production migration |
| Simplicity | PASS | PASS | One host/database; no broker, cache cluster, NgRx, or future module implementation |
| Testing and observability | PASS | PASS | Layered tests, structured logging, correlation, health |

No constitutional exception is requested. Recheck these gates before approving later tasks.

## Architecture and Ownership

```text
Angular Admin Draft routes / Reactive Form
  → same-origin authenticated HTTP + antiforgery
  → Carpediem.Web REST / OpenAPI / ProblemDetails
  → Events application use cases (CreateDraft, ListDrafts, GetDraft, UpdateDraft)
  → Event domain invariants and StaffingCycle lifecycle
  → single CarpediemDbContext unit of work
  → identity + events + audit schemas in SQL Server
```

- **Interface/API** binds typed DTOs, enforces `RequireAdmin`, validates antiforgery on mutations, maps safe errors, and publishes OpenAPI. EF entities are never exposed.
- **Application** orchestrates load, Cairo conversion, idempotency, domain operations, audit staging, and one unit of work. Read projections return only Draft authoring fields.
- **Domain (`Events`)** has `Event.CreateDraft` and `Event.EditDraft` enforcing required values, strictly increasing UTC window, and Draft-only edits. Those methods cannot publish, cancel, or appoint a Chief Leader.
- **Infrastructure** implements SQL Server mappings/migrations, Identity storage, request ledger, audit persistence, and diagnostics. The host context coordinates one transaction. Module writes remain with their owner; Audit is cross-cutting.
- **Identity bootstrap** supplies minimal sign-in/out/current session and nonproduction fixtures (1 Admin, 2 Chief Leaders, 5 Leaders), not production onboarding.

## Time and Date Boundary

1. Angular form fields carry local date/time labeled `Africa/Cairo`; the browser device zone never interprets them. Use accessible mobile-capable entry and clear zone help text.
2. Request values are local timestamps without offset plus an optional chosen offset for a time that occurs twice under Cairo rules. Server resolves the IANA zone with current time-zone data. Nonexistent times produce field validation; ambiguous times require an explicit valid offset choice. Never silently shift or guess.
3. Server converts to UTC `DateTimeOffset` at zero offset and compares instants. Past dates pass when the ratified field/order rules pass.
4. Reads and responses include canonical UTC instants and server-derived Cairo local time/offset for editing. Reopening preserves the intended local schedule across Admin device zones.
5. Initial `StaffingCycle.EffectiveEventDate` follows the Cairo calendar date of the Event start. A Draft-only change of that calendar date seals the old current cycle and creates a new current cycle in the same edit transaction; a change within that date retains the cycle. No availability/team records exist for a Draft in this slice, and no notification or published-reschedule workflow runs. Historical cycles remain immutable.

## Use-Case Design

### Create Draft

1. Authenticated Admin opens an empty focused form. Client validates required input and obvious ordering for immediate feedback.
2. One creation intent receives one random request token, reused for double taps and retries of the same payload. Saving state prevents overlapping taps. An uncertain outcome retains values and token; the client resolves that intent before permitting a changed-payload submission with a new token.
3. Server verifies session, Admin policy, antiforgery, request shape, text/time invariants, and Cairo interpretation independently of the client. Required text is evaluated with Unicode-aware whitespace rules; valid nonblank Title, Location, and Notes values are preserved rather than silently trimmed or rewritten.
4. Server hashes a deterministic serialization of the validated request with fixed field order, exact preserved text (including Notes null versus blank), and resolved UTC instants/selected Cairo offsets, then checks the unique `(ActorUserId, RequestToken)` record. Same token/hash returns the existing Event; same token with different payload conflicts. Different keys represent deliberate separate creations even with identical business fields.
5. The `Events` use case creates a Draft Event, initial current cycle (`CycleNumber=1`), `Events.EventCreated` audit, and request record. All commit or roll back under one host unit of work. Failure produces no usable partial Draft or audit.
6. First success returns `201 Created` with Event URI, representation, and version. An identical replay returns that same Event identity with replay indication. Client opens the Draft and announces success once.

### Read/List Drafts

- Admin-only GET operations return a compact Draft list for reopening and one Draft detail. The list is a navigation aid, not a dashboard, and defines a clear empty state and a recoverable list-load failure state. Reads are no-tracking and never expose Drafts to Leader views. Missing ID returns 404; an existing non-Draft at the edit resource returns a safe state conflict.
- Detail includes five authoring fields in Cairo form, canonical UTC instants, status, and opaque version. No staffing or Chief Leader management data is exposed.

### Edit Draft

1. Admin opens current Draft and its opaque version, then edits any of the five CHANGE-002 fields.
2. Submit the complete field set and previously read version. Server verifies session/Admin/antiforgery, resolves Event, checks `Draft` and version, converts Cairo values, then invokes domain edit.
3. A schedule date change aligns the current cycle as above. Stage `Events.DraftEventUpdated` audit with actor, Event, UTC time, and allow-listed changed fields. Save Event, cycle change, and audit together.
4. Invalid input, unauthorized attempt, stale version, changed state, or failed persistence leaves the previous committed state intact. A `409` reports stale/not-Draft state with stable code. Client retains unsaved values and offers refresh/review; it never silently overwrites.

## API and Error Contract

See [contracts/draft-events.openapi.yaml](contracts/draft-events.openapi.yaml) and the minimal [session bootstrap contract](contracts/session-bootstrap.openapi.yaml). All Draft routes are same-origin `/api/...`, require an authenticated Admin session, and use REST, OpenAPI, and `ProblemDetails` conventions.

| Operation | Input/control | Success | Failure highlights |
|---|---|---|---|
| `GET /api/events/drafts` | Admin cookie; bounded paging | 200 Draft summaries | 401, 403, safe 5xx |
| `POST /api/events/drafts` | Cairo fields; `Idempotency-Key`; antiforgery | 201 + Location, Draft/version; same-intent replay returns same Event | 400 malformed/CSRF, 401, 403, 422 validation, 409 token conflict, safe 5xx |
| `GET /api/events/drafts/{id}` | Admin cookie | 200 Draft/version | 400 malformed ID, 401, 403, 404 missing, 409 not Draft |
| `PUT /api/events/drafts/{id}` | Complete Cairo fields + expected version; antiforgery | 200 updated Draft/new version | 400 malformed/CSRF, 401, 403, 404, 422 validation, 409 stale/not Draft, safe 5xx |

- `ValidationProblemDetails` identifies field keys. Ambiguous Cairo times may include valid offset choices. Authorization and unexpected failures use safe global messages. A correlation ID is returned for support.
- A network interruption has unknown outcome until verified. Client retains data/token; replay resolves the original Event. No uniqueness constraint is placed on Title, schedule, or Location.
- Minimal sign-in/out/current-session and antiforgery bootstrap endpoints are `FOUNDATION-01` support, not account management. They are defined narrowly in the session contract; antiforgery bootstrap is same-origin and non-cacheable.

## Data and Migration Design

See [data-model.md](data-model.md). The first feature migration establishes only the required `identity`, `events`, and `audit` schemas and tables with one host migration history. Do not precreate staffing, attendance, notification, feedback, or incident tables.

- **`events.Events`**: Locked GUID PK, `Title nvarchar(200) NOT NULL`, Status, UTC start/end `datetimeoffset`, creator FK, created time, `rowversion`; realize `Location nvarchar(max) NOT NULL` and `Notes nvarchar(max) NULL`. Domain enforces Unicode whitespace/date/lifecycle rules without rewriting accepted text. SQL reinforces nullability, status, increasing window, zero UTC offsets, and common blank text where safe.
- **`events.StaffingCycles`**: Event FK with `ON DELETE NO ACTION`, unique `(EventId,CycleNumber)`, filtered unique current-cycle index, effective Cairo start date, created time. Application ensures at least one current cycle with each Draft; SQL guarantees at most one. Draft date change preserves history and switches current cycle atomically.
- **`audit.AuditLogs`**: Locked append-only shape. Create/update records carry actor, Event resource, UTC occurrence, direct source, correlation, and an explicit payload allow-list. Creation records initial `Draft` status and the authored field names. Edits record changed field names and old/new Title, Location, and UTC schedule values; Notes record only that Notes changed, never their free text. No mandatory reason applies to ordinary Draft authoring. No audit survives a rejected or rolled-back change. Operational logs remain separate and never capture form contents.
- **`events.DraftCreateRequests`**: Minimal additive durable ledger with actor FK, request token, payload fingerprint, resulting Event FK, UTC created time; unique `(ActorUserId,RequestToken)`. Written with Event/cycle/audit. No guessed retention period.
- **SQL behavior**: Duplicate key collision resolves to committed record after the winning transaction; mismatched fingerprint conflicts. Event `rowversion` participates in update predicate; zero affected rows yields stale conflict. RCSI is enabled. No distributed transaction/cache.
- **Migration safety**: Inspect generated migration and idempotent deployment SQL. Verify FK/check/index names and UTC offsets. No destructive migration or automatic production migration on app startup. Record the Location/Notes documentation mismatch in migration review.

## Authentication, Security, and Observability

- Identity uses secure `HttpOnly`, `Secure`, `SameSite=Lax` cookies per ADR-002 and server `RequireAdmin` per ADR-003 on all Draft endpoints. Angular guards are convenience only.
- Validate antiforgery on unsafe methods, including sign-in/out as appropriate. Expired session yields 401; lost Admin authority yields 403, without writing data. UI preserves input where practical and never shows failed save as success.
- Typed DTOs, server validation, Angular output encoding, and safe `ProblemDetails` cover input/errors. Do not log form contents, credentials, or full request bodies by default.
- Use Serilog structured logs, lightweight OpenTelemetry request traces/metrics, `/healthz`, and propagated correlation ID. Log failure category, safe Event/actor IDs, and trace ID without sensitive text.

## Mobile-First Frontend and Accessibility

- Compact Draft list and create/edit routes use one column from 320 CSS px, following the current [mobile layout foundation](../../docs/ux/MOBILE_FIRST_RULES.md), with short labeled fields, Cairo zone help, no normal horizontal scrolling, at least 48px-high form controls and 44×44px targets, and a visible reachable primary action. The list specifies empty, loading, and recoverable-error presentations. Verify 320/360/390/430 px, text zoom, and tablet/desktop expansion. Import the committed [semantic token foundation](../../docs/ux/DESIGN_TOKENS.md) as the shared source for stable operational spacing, sizing, focus, status, and color roles. Keep the application on the system font stack until the proposed typography receives owner approval, and do not make review-only or rights-unclear imagery a runtime dependency.
- Reactive Forms own values/validation; a feature service owns HTTP. Signals represent local loading/saving/success/error; RxJS manages requests. NgRx is unnecessary.
- Keep one creation token with the active form intent. Disable duplicate submit during save; retain token/values on uncertain result and resolve that result before starting a new intent. Successful edit updates version; stale conflict preserves unsaved values and offers reload/review.
- Track form dirtiness for create and edit routes. A cancel action or navigation away from unsaved changes requires an accessible discard decision; choosing to remain preserves the form, while confirmed discard creates or updates nothing. After a successful save, clear the dirty state before navigation.
- Date/time entry works with touch and keyboard, independently of device zone. Reserve space for feedback where practical to limit layout shift.
- Bind labels and field errors programmatically, identify invalid controls, announce save states in status regions, focus error summary/first invalid field as appropriate, preserve logical order, visible focus, contrast, zoom, non-color-only meaning, and reduced motion. No hover-only action.

## Verification Strategy and Traceability

| Requirements / acceptance | Design locus | Contract/data impact | Planned verification |
|---|---|---|---|
| FR-001–005; US1.1, US3.1, US4.1–3 | Admin policy, Draft routes/projections | Session + Draft GET/POST/PUT; creator FK | API 401/403 for Admin/Chief Leader/Leader; Draft visibility; Playwright create/open |
| FR-006–011; US1.2, US1.5, US2.1–6 | Domain and Cairo converter | DraftWrite/ValidationProblem; UTC Event columns and SQL time check | Unit cases for every field and Cairo gap/overlap; SQL UTC round trip; form tests |
| FR-012–015; US1.1, US3.2–3 | Event/StaffingCycle/audit unit of work | Event + current-cycle FK/index; create/update responses | Testcontainers all-or-none create/failed edit; Draft date change/current uniqueness |
| FR-016–017; US3.2, US4.5 | Domain method and audit writer | Draft state guard; AuditLogs | Integration verifies no later workflow effect and only committed audit |
| FR-018; US1.4, US4.4 | Ledger and client token | Idempotency-Key + DraftCreateRequests | Concurrent POST, lost-response replay, changed-payload conflict, distinct keys with same Event values |
| FR-019; US3.4 | Event rowversion | DTO version; 409 stale_draft | Two Admin clients; second conflicts; first values and one update audit remain |
| FR-020–024; US1.3, US4.1–5 | ProblemDetails and form state | 400/401/403/404/409/422/500; no invalid write | Contract tests, frontend recovery tests, focused negative E2E |
| FR-025–028; all user journeys | Responsive semantic form | Field error mapping/status representation | Narrow viewport/keyboard Playwright; automated/manual WCAG 2.2 AA, zoom and screen-reader check |

Use `WebApplicationFactory` with SQL Server Testcontainers for persistence/authorization/atomicity/retries. Contract tests compare OpenAPI and error shapes. Critical E2E signs in as seeded Admin, enters Cairo-local values, creates a Draft, reopens, edits, and verifies displayed Cairo time and persisted UTC instant. Keep high-value negative E2E focused; prove most edge cases at unit/integration level. Add a k6 baseline workload for Draft list/read/create/update against an isolated test environment at the constitution's approximate 100–150-user project scale. Record response and error metrics without inventing a pass/fail latency threshold that no approved NFR defines.

### Success criteria traceability

| Success criterion | Objective evidence planned |
|---|---|
| SC-001 | Create contract and E2E cover four required fields with Notes present and absent |
| SC-002 | Unit/integration parameter sets prove every missing, whitespace, malformed, and non-increasing input is rejected without persistence |
| SC-003 | Successful edit, failed edit rollback, and two-client stale edit integration tests prove Draft state and last committed values |
| SC-004 | Admin/Chief Leader/Leader API matrix plus Leader-facing query checks prove authorization and Draft privacy |
| SC-005 | Playwright at 320 CSS px with touch-sized controls and keyboard-only completion proves the critical mobile path without horizontal scrolling |
| SC-006 | Contract/frontend tests prove actionable error states; concurrent and uncertain create tests prove one Event per intent |
| SC-007 | Automated accessibility checks plus manual focus, announcement, contrast, 200% zoom, and non-color review cover applicable WCAG 2.2 AA expectations |

## FOUNDATION-01 Scope

### REQUIRED NOW

| Item | Minimum for SLICE-02 |
|---|---|
| Solution | ASP.NET Core host, Events and minimal Identity modules, one Infrastructure/persistence area, Angular workspace, focused test projects |
| Database | Docker Compose SQL Server 2025, host DbContext, RCSI, first migration for only needed schemas/tables/constraints |
| Access | Cookie sign-in/out/session, Admin policy, deterministic development fixtures (1 Admin, 2 Chief Leaders, 5 Leaders), antiforgery |
| API | Same-origin proxy, typed REST DTOs, OpenAPI, ProblemDetails, request correlation |
| UI | Small shell, authenticated navigation, Draft list/create/edit routes, minimal reusable form styling using approved tokens when available |
| Quality/ops | xUnit, WebApplicationFactory + SQL Server Testcontainers, Angular tests, Playwright, k6 baseline workload, structured logging/tracing, `/healthz` |

### DEFERRED

- Production onboarding/invitations, account administration, and broad role management.
- Chief Leader assignment/acceptance, publication, staffing, attendance, notifications, dashboards, feedback, incidents, and their schemas/UI.
- Broad component library, NgRx, background workers, external messaging, distributed caches, brokers, search, orchestration, and production deployment automation beyond compatibility with the locked topology.

## Project Structure

### Documentation (this feature)

```text
specs/001-draft-event-creation-editing/
├── spec.md
├── checklists/requirements.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/draft-events.openapi.yaml
├── contracts/session-bootstrap.openapi.yaml
└── quickstart.md
```

### Planned source layout (implementation phase only)

```text
src/
├── Carpediem.Web/
│   └── Carpediem.Web.csproj          # HTTP composition, endpoints, policies, errors
├── Modules/
│   ├── Identity/
│   │   └── Carpediem.Modules.Identity.csproj  # Minimal session/Identity contracts
│   └── Events/
│       ├── Carpediem.Modules.Events.csproj
│       ├── Application/               # Draft commands/queries and DTO mapping
│       ├── Domain/                    # Event and StaffingCycle invariants
│       └── Infrastructure/            # Events EF configurations
└── Infrastructure/
    ├── Carpediem.Infrastructure.csproj
    ├── Persistence/                  # Host DbContext and one migration history
    └── Audit/                        # Cross-cutting audit persistence
frontend/src/app/
├── core/                              # Session, HTTP/antiforgery, correlation
└── features/draft-events/            # Routes, form, list, service, local state
tests/
├── Carpediem.Domain.Tests/
├── Carpediem.Api.IntegrationTests/
├── e2e/
└── performance/
```

**Structure decision**: One deployable host references the minimum Identity, Events, and Infrastructure projects; module EF configurations are discovered by the host DbContext as ADR-009 describes. This follows [module boundaries](../../docs/architecture/MODULE_BOUNDARIES.md) while keeping only one deployable unit and one migration history. These paths are planned, not created by `/plan`.

## Planning Exit Review

- Constitution gates: PASS; no unjustified platform addition.
- Contract, data model, and quickstart must agree on Cairo conversion, UTC storage, Admin policy, current-cycle creation, audit, version conflict, and idempotent create.
- No source code or tasks are created in this phase. Specific technical decisions are documented in [research.md](research.md); no product question remains before `/tasks`.
