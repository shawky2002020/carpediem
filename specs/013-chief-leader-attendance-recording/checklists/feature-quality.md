# Quality Gate Checklist: SLICE-15 — Chief Leader On-Site Attendance Recording

**Feature Branch**: `codex/013-chief-leader-attendance-recording`  
**Created**: 2026-10-02  
**Status**: Completed  

---

## 1. Requirements Completeness Gate

- [x] **Chief Leader Roll Call Finalization**:
  - The active Chief Leader (or Admin) can inspect the event attendance roll sheet via `GET /api/events/{eventId}/attendance`.
  - The active Chief Leader (or Admin) can submit roll call attendance outcomes via `POST /api/events/{eventId}/attendance`, marking active volunteer roster members as `Attended` or `NoShow`.
- [x] **Authorized Persona Boundary**:
  - Roll call access and submission are restricted to the assigned active Chief Leader for the event or Platform Administrators (`CarpediemRoles.Admin`).
  - Volunteer Leaders who are not the assigned active Chief Leader receive `403 Forbidden` (`unauthorized_chief_leader`).
- [x] **Target Event Lifecycle Preconditions**:
  - Target event MUST be in `Status = Published` or `Status = Completed`.
  - Draft events reject attendance with `409 Conflict` (`invalid_event_status`).
  - Cancelled events reject attendance with `409 Conflict` (`invalid_event_status`).
- [x] **Absence Semantics (No Persisted 'Unrecorded' State)**:
  - There is no persisted `Unrecorded` enum value in `attendance.AttendanceOutcomes`.
  - Unrecorded attendance is represented strictly by the absence of an `AttendanceOutcome` row for an active `TeamAssignment`.
  - The UI presents assignments lacking an outcome as `Unrecorded`.
- [x] **Decoupled Completion Invariant (SEQ-008)**:
  - Event completion operates strictly independent of attendance submission.
  - Roll call can be conducted during the event (`Published`) or post-conclusion (`Completed`).
  - Submitting attendance for all active roster members clears the derived `Pending Attendance` condition.
- [x] **Roster Scope & Unique Outcome Constraint**:
  - Only volunteer leaders with `TeamAssignment.Status == Active` in the event's current `StaffingCycle` appear on the roll sheet.
  - Unique constraint `UX_AttendanceOutcomes_Assignment` on `TeamAssignmentId` guarantees at most one outcome record per assignment.
  - Subsequent submissions for existing outcomes update the outcome value idempotently.
- [x] **Immediate Finalization & Zero Routine Approval**:
  - Attendance finalization takes immediate effect upon submission with NO routine Admin approval gate.
- [x] **Immutable Audit Logging**:
  - Appends an audit log entry (`Action = 'AttendanceFinalized'`, `Source = 'ChiefLeaderRollCall'`), recording the actor ID, event ID, correlation ID, and summary counts (`Attended`, `NoShow`).
- [x] **Zero Notifications Invariant**:
  - Zero notifications are dispatched upon roll call submission per ADR-006 & V1_FEATURE_SLICES.md.
- [x] **Strict Scope Boundary**:
  - Admin attendance correction with mandatory justification (`SLICE-16`) is strictly deferred.
  - Untracked `logo.pdf` is never modified or committed.

---

## 2. Technical Quality & Architecture Gate

- [x] **Clean Architecture & Domain Encapsulation**:
  - Domain aggregate `AttendanceOutcome` defined in `src/Modules/Attendance/Domain/AttendanceOutcome.cs`.
  - Application store contract defined in `src/Modules/Attendance/Application/IAttendanceStore.cs`.
  - EF Core implementation in `src/Infrastructure/Persistence/EfAttendanceStore.cs`.
  - Entity mapping configured in `src/Infrastructure/Persistence/Configurations/AttendanceOutcomeConfiguration.cs` under schema `attendance`.
- [x] **Database Migration (SQL Server 2025)**:
  - EF Core migration generated creating table `attendance.AttendanceOutcomes` with `UX_AttendanceOutcomes_Assignment`, foreign keys, and rowversion.
- [x] **Structured Observability**:
  - OpenTelemetry ActivitySource and Meter (`AttendanceTelemetry`) tracking roll sheet fetches, finalizations, and conflicts.
- [x] **Security & Antiforgery**:
  - Antiforgery validation enforced via `RequireAntiforgeryTokenAttribute` on HTTP submission endpoint.
- [x] **Accessibility & Mobile-First Touch Targets**:
  - Roll sheet responsive across viewports 320px–430px and desktop.
  - Minimum 44px touch targets on all interactive outcome selection toggles/buttons.
  - WCAG 2.2 AA compliant with zero axe violations, polite live region announcements, and 200% font zoom reflow without overflow.
- [x] **Performance Budget**:
  - Component SCSS strictly under the 4.00 kB budget.
  - k6 load test executes with 100 concurrent VUs.

---

## 3. Verification & Evidence Sign-Off

- [x] Domain Unit Tests: `Carpediem.Domain.Tests` covering `AttendanceOutcome` invariants and validations (93/93 passed).
- [x] Containerized Integration Tests: `Carpediem.Api.IntegrationTests` covering SQL Server transactions, roll sheet retrieval, roll call finalization, authorization, and audit logging (164/164 passed).
- [x] Frontend Unit Tests: Vitest specs covering Angular attendance service and roll sheet component (94/94 passed).
- [x] Production Build: `npm run build` succeeds with 0 errors and 0 warnings.
- [x] Playwright E2E & WCAG 2.2 AA: Playwright tests passing across all 5 viewports with 0 axe violations (20/20 passed).
- [x] k6 Load Workload: 100-VU concurrent benchmark meeting performance thresholds (`artifacts/performance/013-chief-leader-attendance.json`, 2100/2100 checks succeeded).
- [x] Git Tree Hygiene: `git status` clean (except untracked `logo.pdf`).
