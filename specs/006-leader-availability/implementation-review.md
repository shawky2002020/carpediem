# SLICE-07 Implementation & Verification Review

**Feature**: SLICE-07 — Leader Availability Declaration (Available / NotAvailable)  
**Branch**: `codex/006-leader-availability`  
**Date**: 2026-10-01  
**Status**: Verified & Complete  

---

## 1. Executive Summary

SLICE-07 delivers end-to-end functionality for volunteer Leaders to declare and update their schedule availability (`Available` / `NotAvailable`) for published events in the active `StaffingCycle`. All architectural invariants, including CHANGE-001 Same-Event Exclusivity, optimistic concurrency, idempotent replay, atomic SQL transactions, and WCAG 2.2 AA mobile-first UI, have been implemented and rigorously validated across the full testing pyramid.

---

## 2. Requirement-to-Evidence Matrix

| Requirement | Description | Implementation File(s) | Verification Evidence |
| :--- | :--- | :--- | :--- |
| **FR-001** | Role restriction: only authenticated users with `Leader` role | `src/Modules/Identity/Authorization/AuthorizationPolicies.cs`<br>`src/Carpediem.Web/Endpoints/EventAvailabilityEndpoints.cs` | `EventAvailabilityTests.AnonymousOrUnauthorizedUserIsRejected` (401/403) |
| **FR-002** | Only permitted on `Published` events; 409 `invalid_event_status` otherwise | `src/Modules/Staffing/Domain/Availability/AvailabilityResponse.cs`<br>`src/Infrastructure/Persistence/EfAvailabilityStore.cs` | `EventAvailabilityTests.DeclareAvailabilityOnNonPublishedEventFailsWith409InvalidEventStatus`<br>`AvailabilityResponseTests.DeclareWhenEventIsNotPublishedThrowsInvalidEventStatusException` |
| **FR-003** | Temporal window: rejected once `Event.StartDateTime <= UtcNow` with 409 `window_closed` | `src/Modules/Staffing/Domain/Availability/AvailabilityResponse.cs`<br>`src/Infrastructure/Persistence/EfAvailabilityStore.cs` | `EventAvailabilityTests.DeclareAvailabilityAfterEventStartFailsWith409WindowClosed`<br>`AvailabilityResponseTests.DeclareWhenEventStartHasPassedThrowsAvailabilityWindowClosedException` |
| **FR-004** | CHANGE-001 Same-Event Exclusivity: active Chief Leader on Event $E$ cannot declare availability on $E$, but can on others | `src/Infrastructure/Persistence/EfAvailabilityStore.cs`<br>`src/Modules/Staffing/Domain/Availability/ChiefLeaderExclusivityException.cs` | `EventAvailabilityTests.ActiveChiefLeaderOnEventCannotDeclareAvailabilityReturns409ChiefLeaderConflict`<br>`EventAvailabilityTests.DualRoleLeaderCanDeclareAvailabilityOnEventWhereTheyAreNotChiefLeader`<br>Playwright: `Dual-role Chief Leader on led event sees notice banner and disabled buttons` |
| **FR-005** | Associate record with event's current active `StaffingCycle` (`IsCurrent == true`) | `src/Infrastructure/Persistence/EfAvailabilityStore.cs` | `EventAvailabilityTests.LeaderCanDeclareAvailableOnPublishedEventReturns201CreatedAndPersistsRecordAndAudit` |
| **FR-006** | Valid states: `Available`, `NotAvailable`. Absence = `No AvailabilityResponse` ("Response Needed") | `src/Modules/Staffing/Domain/Availability/AvailabilityResponseType.cs`<br>`frontend/src/app/features/availability/data/availability.model.ts` | `AvailabilityResponseTests`<br>`availability-declaration.component.spec.ts` |
| **FR-007** | Mutability: Leaders can update declaration freely before event start | `src/Infrastructure/Persistence/EfAvailabilityStore.cs`<br>`frontend/src/app/features/availability/components/` | `EventAvailabilityTests.LeaderCanUpdateAvailabilityWithMatchingRowVersionReturns200AndUpdatesAudit`<br>Playwright: `Leader can toggle declared availability from Available to Not Available` |
| **FR-008** | Optimistic concurrency via `If-Match` rowversion; 409 `stale_version` on mismatch | `src/Infrastructure/Persistence/EfAvailabilityStore.cs`<br>`src/Carpediem.Web/Endpoints/EventAvailabilityEndpoints.cs` | `EventAvailabilityTests.StaleRowVersionUpdateFailsWith409Conflict`<br>`AvailabilityResponseTests.UpdateWithStaleVersionThrowsStaleAvailabilityVersionException` |
| **FR-009** | Idempotent replay: identical re-submission returns 200 with `Idempotent-Replay: true` without duplicate audit log | `src/Infrastructure/Persistence/EfAvailabilityStore.cs`<br>`src/Carpediem.Web/Endpoints/EventAvailabilityEndpoints.cs` | `EventAvailabilityTests.IdempotentReplayReturns200WithHeaderAndNoExtraAuditEntry`<br>`tests/performance/event-availability.k6.js` (100 VUs replay verified) |
| **FR-010** | Single atomic SQL transaction with audit log record (`Action = 'AvailabilityDeclared'`) | `src/Infrastructure/Persistence/EfAvailabilityStore.cs`<br>`src/Infrastructure/Audit/AuditLog.cs` | Verified in integration tests (`db.AuditLogs.SingleAsync`) across all mutations |
| **FR-011** | Antiforgery token validation on all mutations (`X-XSRF-TOKEN`) | `src/Carpediem.Web/Endpoints/EventAvailabilityEndpoints.cs` | Tested with `DraftApiTestHarness` and k6 test runs |
| **FR-012** | Query endpoint `GET /api/events/{eventId}/availability/me` | `src/Carpediem.Web/Endpoints/EventAvailabilityEndpoints.cs` | Tested in `EventAvailabilityTests` and `availability.service.spec.ts` |
| **FR-013** | Status badge rendering ("Available", "Not Available", "Response Needed") | `frontend/src/app/features/availability/components/availability-declaration/` | `availability-declaration.component.spec.ts`<br>Playwright test suite across all 5 viewports |
| **FR-014** | Distinct accessible actions with in-flight disablement | `frontend/src/app/features/availability/components/availability-declaration/` | `availability-declaration.component.spec.ts` |
| **FR-015** | Mobile-first 320px–desktop, $\ge 44\text{px}$ touch targets, zero overflow, `aria-live` region | `frontend/src/app/features/availability/components/availability-declaration/` | Playwright: `Availability declaration interface meets WCAG 2.2 AA, 44px targets, and 200% zoom reflow` (Pass on 320, 360, 390, 430, desktop) |

---

## 3. Non-Functional & Architecture Verification

| Metric / Standard | Target / Requirement | Observed Result | Status |
| :--- | :--- | :--- | :--- |
| **Concurrent Load (k6)** | 100 concurrent leaders declaring availability | 1,106 / 1,106 checks succeeded (100%), 0 HTTP failures, 0 errors | **PASS** |
| **k6 Latency** | Avg < 200ms, P90 < 1.0s under 100 VUs | `avg=152.28ms`, `p(90)=772.49ms`, journey `med=1.01s` | **PASS** |
| **Domain Tests** | 100% passing | 48 passed, 0 failed | **PASS** |
| **SQL Integration Tests** | Real SQL Server 2025 in container | 104 passed, 0 failed | **PASS** |
| **Frontend Unit Tests** | Vitest 100% passing | 47 passed, 0 failed | **PASS** |
| **Frontend Bundle Budget** | Component SCSS < 4.00 kB | Transfer size 3.20 kB (< 4.00 kB budget) | **PASS** |
| **Playwright E2E & A11y** | 5 viewports, 0 axe violations | 25 passed across mobile-320, 360, 390, 430, and desktop | **PASS** |
| **Git Working Tree** | No unintended changes, no whitespace issues | `git diff --check` clean, `logo.pdf` untouched | **PASS** |

---

## 4. Scope Discipline & Boundary Audit (T032)

- **Chief Leader Team Selection (`SLICE-08`)**: Strictly omitted. No candidate listing, drafting pool, or team assignment endpoints or components have been created.
- **Direct Drafting (`SLICE-09`)**: Strictly omitted.
- **Rescheduling / Re-opening (`SLICE-12`)**: Strictly omitted. The implementation cleanly queries the current active cycle (`IsCurrent == true`) and respects event immutability.
- **Conclusion**: Scope discipline is completely preserved.
