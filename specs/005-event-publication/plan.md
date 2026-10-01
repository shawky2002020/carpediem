# Implementation Plan: SLICE-06 — Event Publication & Publishing Prerequisite Invariant

**Branch**: `codex/005-event-publication` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

## Summary

Enable Operations Admins to publish a `Draft` Event after strictly verifying the publishing prerequisite invariant: the Event must currently be in `Draft` status, possess exactly one accepted `Active` Chief Leader assignment, and have a valid schedule window. The server atomically transitions the Event to `Published`, appends an immutable `EventPublished` audit record, and stages in-platform notification records for eligible volunteer Leaders in a single SQL transaction. The client interface in `draft-edit` provides readiness visibility, accidental-click confirmation, and accessible feedback across mobile and desktop viewports.

## Technical Context

| Area | Decision |
|---|---|
| Language/runtime | C# 14 / .NET 10 and TypeScript / Angular 22 |
| Backend | ASP.NET Core Minimal APIs, EF Core 10, SQL Server 2025 (RCSI), Identity cookie auth with `RequireAdmin` policy |
| Frontend | Angular 22 standalone components, signals, reactive forms, shared SCSS tokens, mobile-first responsive layout |
| Data | Existing `Events`, `ChiefLeaderAssignments`, `AuditLogs`, and `InPlatformNotifications` tables; no schema migration required |
| Tests | Unit tests (xUnit), API integration tests with real SQL Server 2025 in container, Angular vitest unit/component tests, Playwright e2e/a11y suite, k6 performance load test |
| Scale | ~100–150 volunteer Leaders, 10–15 Chief Leaders, 2–20 Events monthly |
| Constraints | 320 CSS px minimum width, 44×44px touch targets, WCAG 2.2 AA compliance, RFC 7807 problem details, optimistic concurrency (`If-Match`), idempotent replays |

## Constitution Check

| Gate | Status | Evidence |
|---|---|---|
| I. Spec-Driven Development | PASS | Formal spec, checklists, data model, and contracts authored prior to code |
| II. Explicit & Testable Rules | PASS | Strict server-side invariant checking for Active Chief Leader |
| III/IV. Modular Monolith | PASS | Events owns transition, Identity provides roles, Notifications stages records, Infrastructure coordinates persistence |
| V. Server Authorization | PASS | `RequireAdmin` policy strictly enforced on the publication endpoint |
| VI/VII. Relational Integrity | PASS | Single atomic transaction; checks and FKs in SQL Server 2025 |
| VIII. API & OpenAPI | PASS | REST conventions, RFC 7807, OpenAPI schema, `If-Match` rowversion |
| X. Auditability | PASS | Immutable `EventPublished` audit record committed with transition |
| XI/XII. Mobile & A11y | PASS | 320px responsive reflow, 44px targets, ARIA live region, confirmation guard |
| XV. Testing Pyramid | PASS | Domain unit tests, SQL container integration tests, Angular unit tests, Playwright e2e tests |

## Architecture and Execution Flow

```text
Admin draft-edit page
  → user clicks "Publish Event" → confirms via dialog
  → POST /api/events/drafts/{id}/publish + If-Match + X-XSRF-TOKEN
  → RequireAdmin policy check
  → Load Event aggregate with ChiefLeaderAssignments & StaffingCycles
  → Invariant verification: Status == Draft, Active Chief Leader == 1, Start < End
  → Domain transition: event.Publish(...)
  → Transaction commits atomically:
      1. events.Events (Status = 'Published', RowVersion incremented)
      2. audit.AuditLogs (Action = 'EventPublished')
      3. notifications.InPlatformNotifications (for all eligible Leaders)
  → Response 200 OK with PublishedEventResponse (or Idempotent-Replay: true on retry)
```

## Module Ownership & Seams

1. **`Carpediem.Modules.Events` (Domain & Application)**:
   - `Event.Publish(Guid publishedByUserId, DateTimeOffset publishedAt)` domain method and invariants.
   - `ActiveChiefLeaderRequiredException`, `InvalidTimeWindowException`.
   - `IEventPublicationStore`, `PublishEventCommand`, and typed result contracts.
2. **`Carpediem.Modules.Notifications` (Domain & Application)**:
   - `InPlatformNotification.EventPublished(...)` factory method.
   - Stage publication notification for Leader recipients.
3. **`Carpediem.Infrastructure` (Persistence & Audit)**:
   - `EfEventPublicationStore` implementing atomic execution strategy.
   - Audit log integration using `AuditLog.Create(...)`.
4. **`Carpediem.Web` (Host & HTTP)**:
   - Endpoint in `DraftEventsEndpoints.cs` or `EventPublicationEndpoints.cs`.
   - Structured telemetry in `EventPublicationTelemetry.cs`.
   - OpenAPI metadata and problem details mapping.
5. **`frontend` (Angular Client)**:
   - `draft-events.service.ts` method `publishDraftEvent`.
   - `draft-edit.component.ts` and `html`: publication readiness check, confirmation card, in-flight protection, success announcements.
