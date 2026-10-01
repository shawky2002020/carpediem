# Implementation Plan: SLICE-07 — Leader Availability Declaration

**Branch**: `codex/006-leader-availability` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

## Summary

Enable authenticated volunteer Leaders to view a `Published` Event and declare their schedule availability as `Available` or `NotAvailable`. The server associates declarations with the Event's current active `StaffingCycle`, enforces optimistic concurrency via rowversioning, supports full self-service mutability prior to event start, rejects submissions once `StartDateTime` has arrived (`window_closed`), and strictly enforces the **CHANGE-001 Same-Event Exclusivity Invariant** (an active Chief Leader on an event cannot declare availability for that same event). All mutations execute in a single atomic SQL Server transaction that persists `staffing.AvailabilityResponses` and appends an immutable `AvailabilityDeclared` audit record.

---

## Technical Context

| Area | Decision |
|---|---|
| Language/runtime | C# 14 / .NET 10 and TypeScript / Angular 22 |
| Backend | ASP.NET Core Minimal APIs, EF Core 10, SQL Server 2025 (RCSI), Identity cookie auth with `RequireLeader` policy |
| Frontend | Angular 22 standalone components, signals, reactive HTTP services, shared SCSS tokens, mobile-first responsive layout |
| Data | New table `staffing.AvailabilityResponses` in schema `staffing` with unique index `(StaffingCycleId, UserId)` and FKs to `StaffingCycles`, `Events`, `Users` |
| Tests | Unit tests (xUnit), API integration tests with real SQL Server 2025 in container, Angular vitest unit/component tests, Playwright e2e/a11y suite across 5 viewports, k6 load/concurrency benchmark |
| Scale | ~100–150 volunteer Leaders, 10–15 Chief Leaders, 2–20 Events monthly |
| Constraints | 320 CSS px minimum width, 44×44px touch targets, WCAG 2.2 AA compliance, RFC 7807 problem details, optimistic concurrency (`If-Match`), idempotent replays |

---

## Constitution Check

| Gate | Status | Evidence |
|---|---|---|
| I. Spec-Driven Development | PASS | Formal spec, checklists, data model, and contracts authored prior to code |
| II. Explicit & Testable Rules | PASS | Strict server-side invariant checking for Published status, future start time, and CHANGE-001 exclusivity |
| III/IV. Modular Monolith | PASS | Staffing module owns `AvailabilityResponse`, Events provides cycle & Chief Leader status, Identity validates Leader role |
| V. Server Authorization | PASS | `RequireLeader` policy strictly enforced on all availability endpoints |
| VI/VII. Relational Integrity | PASS | Single atomic transaction; unique index on `(StaffingCycleId, UserId)` and foreign keys in SQL Server 2025 |
| VIII. API & OpenAPI | PASS | REST conventions, RFC 7807, OpenAPI schema, `If-Match` rowversion |
| X. Auditability | PASS | Immutable `AvailabilityDeclared` audit record committed with every creation or update |
| XI/XII. Mobile & A11y | PASS | 320px responsive reflow, 44px targets, ARIA live region, clear status badges |
| XV. Testing Pyramid | PASS | Domain unit tests, SQL container integration tests, Angular unit tests, Playwright e2e tests |

---

## Architecture and Execution Flow

```text
Leader Event View / Availability Component
  → user clicks "Mark Available" or "Mark Not Available"
  → POST /api/events/{eventId}/availability + If-Match + X-XSRF-TOKEN
  → RequireLeader policy check (403 if not Leader)
  → Load Event & active StaffingCycle & ChiefLeaderAssignment
  → Invariant checks:
      1. Event.Status == Published (409 invalid_event_status)
      2. Event.StartDateTime > UtcNow (409 window_closed)
      3. User is NOT active Chief Leader on Event (409 chief_leader_conflict)
  → Load existing AvailabilityResponse for (StaffingCycleId, UserId)
  → If existing:
      - If same response: 200 OK with Idempotent-Replay: true
      - If different: verify If-Match rowversion (409 stale_version); update Response & DeclaredAt
  → If new:
      - Create AvailabilityResponse
  → Single atomic SQL transaction commits:
      1. staffing.AvailabilityResponses (inserted or updated with new rowversion)
      2. audit.AuditLogs (Action = 'AvailabilityDeclared')
  → Response 201 Created (if new) or 200 OK (if updated/replayed)
```

---

## Module Ownership & Project Structure

1. **`Carpediem.Modules.Staffing` (New Module Project)**:
   - Project: `src/Modules/Staffing/Carpediem.Modules.Staffing.csproj`
   - Domain: `Carpediem.Modules.Staffing.Domain.Availability`:
     - `AvailabilityResponse` aggregate entity.
     - `AvailabilityResponseType` enum (`Available`, `NotAvailable`).
     - Invariant guard exceptions (`AvailabilityWindowClosedException`, `ChiefLeaderExclusivityException`, `InvalidEventStatusException`).
   - Application: `Carpediem.Modules.Staffing.Application.Availability`:
     - `IAvailabilityStore` abstraction.
     - `DeclareAvailabilityCommand`, `GetMyAvailabilityQuery`, result records.
2. **`Carpediem.Infrastructure`**:
   - `CarpediemDbContext`: Register `DbSet<AvailabilityResponse> AvailabilityResponses`.
   - Entity configuration: `AvailabilityResponseConfiguration` (table `staffing.AvailabilityResponses`, unique index on `(StaffingCycleId, UserId)`).
   - EF Core Migration: `AddAvailabilityResponses`.
   - Store implementation: `EfAvailabilityStore`.
   - Service registration in `ServiceCollectionExtensions.cs`.
3. **`Carpediem.Web`**:
   - Endpoints: `src/Carpediem.Web/Endpoints/EventAvailabilityEndpoints.cs`.
   - Telemetry: `src/Carpediem.Web/Observability/EventAvailabilityTelemetry.cs`.
   - OpenAPI transformer configuration for `/api/events/{eventId}/availability`.
4. **`frontend`**:
   - Service: `src/app/features/availability/data/availability.service.ts`.
   - Component: `src/app/features/availability/components/availability-declaration/` or integrated event participation card.
5. **Tests**:
   - Domain tests: `tests/Carpediem.Domain.Tests/Staffing/AvailabilityResponseTests.cs`.
   - API integration tests: `tests/Carpediem.Api.IntegrationTests/Staffing/EventAvailabilityTests.cs`.
   - Angular unit tests: `availability.service.spec.ts`, `availability-declaration.component.spec.ts`.
   - Playwright E2E: `tests/e2e/event-availability.spec.ts`.
   - Performance: `tests/performance/event-availability.k6.js`, `tests/performance/run-event-availability.ps1`.
