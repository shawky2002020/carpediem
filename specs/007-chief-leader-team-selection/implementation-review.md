# SLICE-08 Implementation & Verification Review

**Feature**: SLICE-08 — Chief Leader Team Selection from Available Pool (Immediate active roster placement per Decision Group 5)  
**Branch**: `codex/007-chief-leader-team-selection`  
**Date**: 2026-10-01  
**Status**: Verified & Complete  

---

## 1. Executive Summary

SLICE-08 delivers end-to-end functionality for active Chief Leaders (and authorized Operations Administrators) to view the pool of volunteers who declared `Available` for the current active `StaffingCycle` of a `Published` event and immediately select them onto the confirmed active team roster (`staffing.TeamAssignments`) per **Decision Group 5**.

All architectural invariants and constraints have been strictly enforced and validated:
- **Immediate Active Roster Placement**: Volunteer selection immediately creates an `Active` `TeamAssignment` without redundant confirmation gates (Decision Group 5).
- **CHANGE-001 Same-Event Exclusivity Invariant**: The active Chief Leader for Event $E$ cannot be selected as a volunteer team member on Event $E$ (HTTP 409 `chief_leader_conflict`), while retaining eligibility on other events where they hold the `Leader` role.
- **Availability Precondition**: Candidate must have declared `Response = Available` in the active `StaffingCycle` (`IsCurrent == true`). Non-respondents and `NotAvailable` volunteers are rejected with HTTP 409 `candidate_not_available`.
- **Temporal Cutoff**: Team selection closes once `Event.StartDateTime <= UtcNow` (HTTP 409 `window_closed`).
- **Filtered Unique Index**: `UX_TeamAssignments_ActiveLeaderPerCycle` on `(StaffingCycleId, UserId)` WHERE `[Status] = 'Active'` guarantees no duplicate active assignments per leader per cycle.
- **Idempotent Replay**: Re-submitting an identical selection returns HTTP 200 with `Idempotent-Replay: true` header without creating duplicate notifications or extra audit records.
- **Single Atomic SQL Transaction**: EF Core execution strategy atomic transaction inserts `staffing.TeamAssignments`, inserts `notifications.InPlatformNotifications` (`VolunteerSelected`), and records `audit.AuditLogs` (`Action = 'VolunteerSelected'`).
- **Mobile-First & Accessible UI**: Responsive from 320px to desktop, $\ge 44\text{px}$ touch targets, zero layout shift, 200% font zoom reflow, polite ARIA live announcements, and 0 axe violations.

---

## 2. Requirement-to-Evidence Matrix

| Requirement | Description | Implementation File(s) | Verification Evidence |
| :--- | :--- | :--- | :--- |
| **FR-001** | Authorization: Active Chief Leader on Event $E$ or Operations Admin (`RequireChiefLeaderOrAdmin`) | `src/Modules/Identity/Authorization/AuthorizationPolicies.cs`<br>`src/Carpediem.Web/Endpoints/EventTeamSelectionEndpoints.cs` | `EventTeamSelectionTests.NonChiefLeaderOrAdminReceives403Forbidden`<br>`EventTeamSelectionTests.AdminCanQueryAndSelectVolunteers` |
| **FR-002** | Status guard: permitted exclusively on `Published` events; 409 `invalid_event_status` on others | `src/Modules/Staffing/Domain/TeamSelection/TeamAssignment.cs`<br>`src/Infrastructure/Persistence/EfTeamSelectionStore.cs` | `EventTeamSelectionTests.QueryOrSelectOnNonPublishedEventFailsWith409InvalidEventStatus`<br>`TeamAssignmentTests.CreateWhenEventIsNotPublishedThrowsInvalidEventStatusException` |
| **FR-003** | Temporal window: permitted only while `Event.StartDateTime > UtcNow`; 409 `window_closed` once passed | `src/Modules/Staffing/Domain/TeamSelection/TeamAssignment.cs`<br>`src/Infrastructure/Persistence/EfTeamSelectionStore.cs` | `EventTeamSelectionTests.SelectVolunteerAfterEventStartFailsWith409WindowClosed`<br>`TeamAssignmentTests.CreateWhenEventStartHasPassedThrowsTeamSelectionWindowClosedException`<br>Playwright: `Displays warning banner and disables selection buttons when selection window is closed` |
| **FR-004** | CHANGE-001 Same-Event Exclusivity: active Chief Leader on Event $E$ cannot be selected on $E$ (409 `chief_leader_conflict`) | `src/Infrastructure/Persistence/EfTeamSelectionStore.cs`<br>`src/Carpediem.Web/Endpoints/EventTeamSelectionEndpoints.cs` | `EventTeamSelectionTests.SelectingActiveChiefLeaderFailsWith409ChiefLeaderConflict`<br>`TeamAssignmentTests.CreateWhenCandidateIsChiefLeaderOnEventThrowsChiefLeaderExclusivityException`<br>Playwright: `Displays error alert when selection returns 409 conflict` |
| **FR-005** | Available pool constraint: candidate must have `Available` response in active `StaffingCycle` (`IsCurrent == true`) | `src/Infrastructure/Persistence/EfTeamSelectionStore.cs` | `EventTeamSelectionTests.SelectingCandidateWhoMarkedNotAvailableFailsWith409CandidateNotAvailable`<br>`EventTeamSelectionTests.SelectingCandidateWithNoAvailabilityFailsWith409CandidateNotAvailable`<br>`TeamAssignmentTests.CreateWhenCandidateNotAvailableThrowsCandidateNotAvailableException` |
| **FR-006** | Immediate `Active` status placement without intermediate confirmation gates (Decision Group 5) | `src/Modules/Staffing/Domain/TeamSelection/TeamAssignment.cs`<br>`src/Infrastructure/Persistence/EfTeamSelectionStore.cs` | `EventTeamSelectionTests.ChiefLeaderSelectsAvailableVolunteerReturns201CreatedAndPersistsActiveAssignmentAndNotificationAndAudit`<br>`TeamAssignmentTests.CreateActiveAssignmentInitializesWithActiveStatusAndUtcTimestamp` |
| **FR-007** | Filtered unique index `UX_TeamAssignments_ActiveLeaderPerCycle` on `(StaffingCycleId, UserId)` WHERE `[Status] = 'Active'` | `src/Modules/Staffing/Infrastructure/Persistence/Configurations/TeamAssignmentConfiguration.cs`<br>`src/Infrastructure/Persistence/Migrations/20261001195134_AddTeamAssignments.cs` | Validated by schema migration and concurrency constraint in SQL Server 2025 integration tests |
| **FR-008** | Idempotent replay: identical re-submission returns 200 with `Idempotent-Replay: true` header | `src/Infrastructure/Persistence/EfTeamSelectionStore.cs`<br>`src/Carpediem.Web/Endpoints/EventTeamSelectionEndpoints.cs` | `EventTeamSelectionTests.IdempotentReplayReturns200WithHeaderAndNoExtraAuditOrNotification`<br>`tests/performance/team-selection.k6.js` (100 VUs replay verified) |
| **FR-009** | Atomic SQL transaction creating `TeamAssignment`, `InPlatformNotification` (`VolunteerSelected`), and `AuditLog` (`Action = 'VolunteerSelected'`) | `src/Infrastructure/Persistence/EfTeamSelectionStore.cs`<br>`src/Infrastructure/Notifications/EfVolunteerSelectedNotificationWriter.cs` | `EventTeamSelectionTests.ChiefLeaderSelectsAvailableVolunteerReturns201CreatedAndPersistsActiveAssignmentAndNotificationAndAudit`<br>Asserted single row in each table |
| **FR-010** | Antiforgery token verification on mutating POST endpoint (`X-XSRF-TOKEN`) | `src/Carpediem.Web/Endpoints/EventTeamSelectionEndpoints.cs` | Enforced via `[RequireAntiforgeryToken]` and verified in integration/k6 workloads |
| **FR-011** | Query endpoint `GET /api/events/{eventId}/team-selection` returning event metadata, available candidates, active team roster | `src/Carpediem.Web/Endpoints/EventTeamSelectionEndpoints.cs`<br>`src/Infrastructure/Persistence/EfTeamSelectionStore.cs` | `EventTeamSelectionTests.ChiefLeaderCanQueryAvailablePoolAndActiveRosterReturns200Ok`<br>`team-selection.service.spec.ts` |
| **FR-012** | Mutation endpoint `POST /api/events/{eventId}/team-selection` accepting candidate user ID | `src/Carpediem.Web/Endpoints/EventTeamSelectionEndpoints.cs`<br>`src/Infrastructure/Persistence/EfTeamSelectionStore.cs` | `EventTeamSelectionTests.ChiefLeaderSelectsAvailableVolunteerReturns201CreatedAndPersistsActiveAssignmentAndNotificationAndAudit` |
| **FR-013** | UI displays Available Volunteers pool and Active Team Roster with count badges | `frontend/src/app/features/team-selection/components/team-selection.component.html`<br>`team-selection.component.ts` | `team-selection.component.spec.ts`<br>Playwright: `Chief Leader views available pool and selects volunteer into immediate active roster` |
| **FR-014** | Accessible "Select for Team" buttons with in-flight submission disablement | `frontend/src/app/features/team-selection/components/` | `team-selection.component.spec.ts`<br>Playwright tests on all viewports |
| **FR-015** | Mobile-first 320px–desktop, $\ge 44\text{px}$ touch targets, 200% font zoom reflow, `aria-live="polite"` announcements, 0 axe violations | `frontend/src/app/features/team-selection/components/team-selection.component.scss`<br>`frontend/src/app/features/team-selection/components/team-selection.component.html` | Playwright: `Team selection interface meets WCAG 2.2 AA, 44px touch targets, and 200% zoom reflow` (Pass on mobile-320, 360, 390, 430, desktop) |

---

## 3. Non-Functional & Architecture Verification

| Metric / Standard | Target / Requirement | Observed Result | Status |
| :--- | :--- | :--- | :--- |
| **Concurrent Load (k6)** | 100 concurrent leaders selected onto roster | 2,012 / 2,012 checks succeeded (100%), 0 HTTP failures, 0 errors | **PASS** |
| **k6 Latency** | Avg < 300ms under 100 VUs | `avg=112.34ms`, `med=11.04ms`, `p(90)=388.81ms`, journey `avg=1.09s` | **PASS** |
| **Domain Unit Tests** | 100% passing in container | 56 passed, 0 failed | **PASS** |
| **SQL Integration Tests** | Real SQL Server 2025 in container | 113 passed, 0 failed | **PASS** |
| **Frontend Unit Tests** | Vitest 100% passing | 54 passed, 0 failed | **PASS** |
| **Frontend Bundle Budget** | Component SCSS < 4.00 kB | Transfer size 3.59 kB (< 4.00 kB budget, 0 warnings) | **PASS** |
| **Playwright E2E & A11y** | 5 viewports, 0 axe violations | 20 passed across mobile-320, 360, 390, 430, and desktop | **PASS** |
| **Git Working Tree** | No unintended changes, no whitespace issues | `git diff --check` clean, `logo.pdf` untouched | **PASS** |

---

## 4. Scope Discipline & Boundary Audit (T032)

- **Direct Drafting of Non-Respondents (`SLICE-09`)**: Strictly omitted. The selection store queries exclusively volunteers who submitted `Response = Available` in the active cycle. Non-respondents and `NotAvailable` leaders cannot be selected via this slice.
- **Volunteer Self-Cancellation (`SLICE-10`)**: Strictly omitted. No self-service cancellation actions, endpoints, or states were introduced.
- **Rescheduling / Re-opening (`SLICE-12`)**: Strictly omitted. The implementation scopes all queries and assignments strictly to `StaffingCycle.IsCurrent == true`.
- **Conclusion**: Scope discipline is 100% preserved.
