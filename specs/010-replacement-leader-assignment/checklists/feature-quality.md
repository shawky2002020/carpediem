# Quality Gate Checklist: SLICE-11 — Replacement Leader Assignment

**Feature Branch**: `codex/010-replacement-leader-assignment`  
**Created**: 2026-10-02  
**Status**: Passed & Complete  

---

## 1. Requirements Completeness Gate

- [x] **Clear Persona Boundaries**: Active Chief Leader (for assigned event) or Operations Administrator (audited oversight/override). Non-assigned leaders receive 403 Forbidden (`unauthorized_chief_leader`).
- [x] **Explicit Event Lifecycle Invariant**: Event MUST be `Published`. Draft, Completed, or Cancelled events return 409 Conflict (`invalid_event_status`).
- [x] **Temporal Window Invariant**: Replacement selection is strictly permitted while `Event.StartDateTime > UtcNow`. If `Event.StartDateTime <= UtcNow`, returns 409 Conflict (`window_closed`).
- [x] **Immutable History Invariant**: Prior `Cancelled` assignment records are NEVER overwritten, modified, or deleted. Selecting a replacement creates a **brand new** `TeamAssignment` with `Status = 'Active'`.
- [x] **Candidate Eligibility & Exclusivity**:
  - Candidate must possess the `Leader` role.
  - Candidate cannot be the event's active Chief Leader (`chief_leader_conflict`).
  - Candidate cannot already hold an active `TeamAssignment` for the current staffing cycle (`already_assigned`).
- [x] **Optional Replaced Assignment Linkage**: When a replacement is selected to fill a specific cancelled slot, the replaced assignment ID is validated (must be `Cancelled` and on the same event) and recorded in the audit trail.
- [x] **Optimistic Concurrency & Idempotency**: Concurrency evaluated via `RowVersion` and filtered unique index. If candidate is already active on the team, return safe idempotent replay (200 OK with `Idempotent-Replay: true`).
- [x] **Atomic Side-Effects**: Single SQL Server transaction commits new `staffing.TeamAssignments` insertion, `notifications.InPlatformNotifications` alert for the replacement volunteer, and immutable `audit.AuditLogs` entry (`ReplacementLeaderAssigned`).
- [x] **Strict Scope Discipline**: Reschedule & cycle rotation (`SLICE-12`) is strictly deferred. No attendance recording, penalties, or reliability metrics computed.

---

## 2. Technical Quality & Architecture Gate

- [x] **Clean Architecture Alignment**: Domain invariants encapsulated in `TeamAssignment` factories/methods. Store contract in `Modules/Staffing/Application/ReplacementAssignment/`. EF Core implementation in `Infrastructure/Persistence/`.
- [x] **Structured Telemetry**: Dedicated activity source and metric counters (`ReplacementAssignmentTelemetry`).
- [x] **Security & Antiforgery**: Antiforgery token validation enforced on mutation endpoints (`X-XSRF-TOKEN`). Resource-level authorization enforced (`RequireChiefLeaderOrAdmin`).
- [x] **SQL Server 2025 Test Compatibility**: All integration tests target containerized SQL Server 2025 with snapshots and real constraints.
- [x] **Accessibility & Mobile-First**: Mobile-first UI down to 320px, minimum 44px touch targets, zero layout shift, trapped focus/modals if used, and `aria-live="polite"` announcements.
- [x] **Performance Budget**: Angular component SCSS strictly within 4.00 kB budget. k6 concurrent load test passes with 100 concurrent VUs.

---

## 3. Verification & Evidence Sign-Off

- [x] Unit Tests: `Carpediem.Domain.Tests` covering replacement assignment domain invariants passing (74/74 passed).
- [x] Integration Tests: `Carpediem.Api.IntegrationTests` covering SQL Server 2025 persistence, candidate filtering, concurrency, notifications, audit log, authorization denials, and idempotent replay passing (141/141 passed).
- [x] Frontend Tests: Angular service and component specs passing with 100% assertions (71/71 passed).
- [x] Production Build: `npm run build` passing with 0 warnings and budgets respected.
- [x] End-to-End & a11y: Playwright test suite passing across 320px–desktop viewports with 0 axe violations (25/25 passed).
- [x] k6 Load Workload: 100 concurrent VU benchmark passing with 0 failed requests and sub-second p95 latency (1924/1924 checks passed).
- [x] Git Tree Hygiene: Untracked `logo.pdf` untouched; `git diff --check` passes cleanly.
