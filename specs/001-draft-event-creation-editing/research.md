# Phase 0 Research: SLICE-02 Draft Event Authoring

**Date**: 2026-09-18 | **Baseline**: [spec.md](spec.md), [architecture lock](../../docs/architecture/ARCHITECTURE_LOCK.md), and accepted ADRs. This research answers implementation choices within the locked stack; it does not reopen product or architecture decisions.

## 1. Cairo local time at the API boundary

**Decision**: Accept an offset-free local date/time for each Event endpoint plus an optional disambiguating offset. Label the form `Africa/Cairo`. Convert on the server with `TimeZoneInfo` using the IANA zone and available ICU/time-zone data. Reject invalid local times; for an ambiguous local time, return valid offset choices and require an explicit selection. Store Event instants as UTC `DateTimeOffset` with zero offset. Return server-derived Cairo local values and offsets for editing.

**Rationale**: An HTML `datetime-local` value has no time zone, and Angular's DatePipe documents offset-oriented time-zone input rather than a guarantee that an IANA ID resolves historical/future Cairo rules. Server ownership keeps validation and persistence consistent and independent of the Admin device zone. A local-to-UTC boundary needs both a zone and, at DST overlap, an offset choice to identify exactly one instant. Tests must include ordinary, nonexistent, and ambiguous times plus a Cairo/UTC calendar-date boundary. The runtime/container must include ICU/tzdata and avoid invariant globalization mode. See [.NET `TimeZoneInfo`](https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.findsystemtimezonebyid?view=net-10.0), [ambiguous time handling](https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.isambiguoustime?view=net-10.0), [ICU globalization](https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu), and [Angular DatePipe](https://angular.dev/api/common/DatePipe).

**Alternatives considered**: Convert using a hard-coded UTC+02/+03 offset (reject: daylight-saving changes); interpret input in the device zone (reject: conflicts with `Africa/Cairo`); let client and server each convert (reject: divergent rules); add a client time-zone library now (reject: no need when server provides canonical edit values).

## 2. Duplicate Draft creation after double tap or uncertain result

**Decision**: Require a client-generated `Idempotency-Key` for create and persist an `events.DraftCreateRequests` record keyed by `(ActorUserId, RequestToken)` with canonical payload fingerprint and resulting Event ID. Commit it in the same transaction as Event, initial cycle, and audit. Same token/fingerprint returns the original Event; changed payload conflicts. The form retains the key across retries of the same intent and generates a new key only for a deliberate new creation after any uncertain previous outcome has been resolved. Unique-key contention serializes concurrent requests. Verify uncertain commit by reading the ledger on a fresh context before deciding to retry.

**Rationale**: The [constitution](../../.specify/memory/constitution.md) requires idempotency where duplicate submissions risk corruption, and the [concurrency model](../../docs/data/CONCURRENCY_MODEL.md) identifies creation as a request-token candidate. A durable record works across process restarts and concurrent server instances without making Title/date unique. Run the entire unit of work under one EF execution strategy; do not stack a second retry loop. See [EF Core connection resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency) and [SQL Server unique constraints](https://learn.microsoft.com/en-us/sql/relational-databases/tables/unique-constraints-and-check-constraints?view=sql-server-ver17).

**Alternatives considered**: Disable the button only (reject: cannot protect network retry or another tab); use business-field uniqueness (reject: legitimate Events can match); use an in-memory cache (reject: not durable); use client-assigned Event ID only (possible, but weaker than a ledger for payload mismatch and uncertain-result lookup).

## 3. Stale Draft editing

**Decision**: Return an opaque Base64 version derived from locked `Events.RowVersion` on reads and saves. Require it with each complete Draft update. The application checks the supplied version against the loaded aggregate and EF's original rowversion; a mismatched/zero-row update returns `409` with stable `stale_draft` code. Client retains unsaved edits and offers refresh/review. No automatic last-write-wins or blind retry.

**Rationale**: The [architecture lock](../../docs/architecture/ARCHITECTURE_LOCK.md), [ADR-009](../../docs/decisions/ADR-009-sql-server-persistence-and-schema-organization.md), and [concurrency model](../../docs/data/CONCURRENCY_MODEL.md) already select Event rowversion. EF Core issues an update constrained by the original token and raises a concurrency exception when no row matches, as documented in [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).

**Alternatives considered**: Timestamp comparison (reject: weaker/ambiguous precision); server-side merge (reject: unapproved conflict behavior); ETag/If-Match (valid HTTP alternative, but the opaque body version and stable domain conflict shape align simply with the established 409 contract).

## 4. First-slice schema reconciliation and Draft cycle updates

**Decision**: Treat the later ratified CHANGE-002 fields and SLICE-02 initial-cycle requirement as the feature contract. The first migration realizes Location/Notes and a current initial cycle even though older schema/constraint prose omits or delays them. On a Draft start-date change, archive the old cycle and create a new current one atomically; this preserves the locked cycle's historical immutability without implementing published rescheduling.

**Rationale**: The [relational schema](../../docs/data/RELATIONAL_SCHEMA.md) defines Title as `nvarchar(200)` but omits Location/Notes. [CHANGE-002](../../docs/decisions/CHANGE-002-draft-event-minimum-fields.md) later ratifies both fields. [V1 feature slices](../../docs/roadmap/V1_FEATURE_SLICES.md) later require a current cycle on Draft creation. A SQL filtered unique index enforces at most one current cycle, and the application transaction ensures at least one. The record's effective date is the Cairo calendar date of the Event start, matching clarified Admin authoring. Review any migration against those explicit discrepancies; do not rewrite locked historical documents during `/plan`.

**Alternatives considered**: Pretend columns/cycle already exist (reject: schema listing contradicts it); make capacity or venue taxonomy mandatory (reject: outside CHANGE-002); mutate a historical cycle's effective date (reject: cycle is an immutable revision boundary); implement published reschedule now (reject: SLICE-12).

## 5. API/security and frontend state

**Decision**: Same-origin REST endpoints use Admin policy, secure Identity cookies, antiforgery on unsafe methods, typed DTOs, generated OpenAPI, and safe `ProblemDetails`. The Angular feature uses Reactive Forms, a small HTTP service, and local Signals with RxJS requests. A compact Draft list permits reopening; no NgRx or dashboard state is needed.

**Rationale**: This follows [ADR-002](../../docs/decisions/ADR-002-authentication-session-strategy.md), [ADR-003](../../docs/decisions/ADR-003-authorization-resource-policies.md), and constitution principles. Official [.NET 10 antiforgery guidance](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0), [API authentication response behavior](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/cookie-authentication-api-endpoints?view=aspnetcore-10.0), [ProblemDetails guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0), and [OpenAPI support](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0) support the chosen integration pattern.

**Alternatives considered**: Client-only role guards (reject: no security boundary); token storage in browser storage (reject: ADR-002); NgRx (reject: local form state); extra service/deployment infrastructure (reject: first-slice complexity without a requirement).

## Research outcome

No unresolved technical question blocks `/tasks`. Field limits beyond the locked `Title nvarchar(200)` are not invented. Audit/event data retention remains a later governance decision; this plan does not impose a deletion period.
