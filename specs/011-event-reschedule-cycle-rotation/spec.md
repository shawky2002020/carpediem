# Feature Specification: SLICE-12 — Event Reschedule & Staffing Cycle Rotation

**Feature Branch**: `codex/011-event-reschedule-cycle-rotation`  
**Created**: 2026-10-02  
**Status**: Draft  
**Input**: User request: "Event Reschedule & Staffing Cycle Rotation per Milestone M3 (Schedule Turbulence & Rotation), EPIC-07"  

---

## 1. Executive Summary & Business Intent

In event management operations, unexpected circumstances (venue changes, severe weather, organizational adjustments) may necessitate shifting the scheduled date or time of an already published event. When an event's schedule shifts materially, previously collected volunteer availability responses and active roster assignments can no longer be assumed valid for the new operational window.

Per **Milestone M3 ("Schedule Turbulence & Rotation", EPIC-07)** and **Decision Group 4** (ratified in `docs/requirements/LIFECYCLE_SPECIFICATION.md` Section 4, `docs/requirements/ROLE_PERMISSION_MATRIX.md` Item 11, and `docs/roadmap/V1_FEATURE_SLICES.md` Item SLICE-12):
1. **Administrative Authority**: Only Platform Administrators (`CarpediemRoles.Admin`) possess the authorization to execute a material reschedule on a published event. Non-administrators (Chief Leaders, volunteer leaders) receive 403 Forbidden (`unauthorized_administrator`).
2. **Event Lifecycle Preconditions**: The target event must be in `Status = 'Published'`. Draft events cannot be rescheduled via this endpoint and must be edited via draft endpoints (returning 409 Conflict: `invalid_event_status`). Completed or Cancelled events cannot be rescheduled (returning 409 Conflict: `invalid_event_status`).
3. **Temporal Invariant**: The event's current scheduled start time must be in the future (`Event.StartDateTime > UtcNow`). Events that have already started or concluded cannot be rescheduled (returning 409 Conflict: `window_closed`).
4. **New Schedule Validation**:
   - `NewStartDateTime > UtcNow` (cannot reschedule into the past).
   - `NewEndDateTime > NewStartDateTime` (end time must be after start time).
   - Both timestamps must resolve to valid UTC instants.
   - Mandatory administrative justification of 10 to 500 characters explaining the reason for the reschedule.
5. **Staffing Cycle Rotation (Core Architectural Invariant)**:
   - The active `events.StaffingCycles` record is sealed and archived immutably (`IsCurrent = false`).
   - A brand new `events.StaffingCycles` record is created with `CycleNumber = oldCycle.CycleNumber + 1`, `IsCurrent = true`, and `EffectiveEventDate = DateOnly.FromDateTime(NewStartDateTime.Date)`.
   - Historical availability declarations (`staffing.AvailabilityResponses`) and team assignments (`staffing.TeamAssignments`) remain untouched and tied to the archived `StaffingCycleId`.
   - The new cycle starts clean with zero active team assignments and zero availability responses.
6. **Optimistic Concurrency**:
   - Reschedule requests must provide an `If-Match` header containing the event's current base64 `RowVersion`. Concurrency mismatches return 412 Precondition Failed (`concurrency_conflict`).
7. **Atomic Side-Effects & Communications**:
   - Atomic SQL Server transaction commits the event schedule update, cycle archival, new cycle creation, notifications, and audit logging.
   - High-priority in-platform notifications (`NotificationType = 'EventRescheduled'`) are dispatched to:
     - Previous active roster members from the archived cycle.
     - The active Chief Leader for the event.
     - Candidate leaders who had declared availability in the archived cycle.
   - An immutable audit log entry (`Action = 'EventRescheduled'`) is created capturing prior schedule, new schedule, actor Admin ID, and administrative justification.

---

## 2. User Scenarios & Acceptance Criteria

### User Story 1 - Platform Administrator Executes Event Reschedule (Priority: P1) 🎯 MVP

As a Platform Administrator,  
I want to update the start and end date/time of a published event with a mandatory justification,  
So that the event reflects the new operational schedule in the platform.

**Independent Test**:
1. Log in as an Administrator.
2. Select a published event whose start date is in the future.
3. Submit `POST /api/events/{eventId}/reschedule` with valid `NewStartDateTime`, `NewEndDateTime`, and `Justification` (>= 10 chars), along with the matching `If-Match` header.
4. Verify HTTP 200 OK returning the updated event details, the new `StaffingCycleId`, `CycleNumber`, and the new schedule.
5. Verify `Event.Status` remains `Published`.
6. Verify non-Admin users receive 403 Forbidden (`unauthorized_administrator`).

**Acceptance Scenarios**:
1. **Given** a published future event, **when** Admin submits a valid reschedule request, **then** the event schedule is updated and HTTP 200 OK is returned.
2. **Given** a non-Admin user (e.g., Chief Leader or Volunteer Leader), **when** attempting to reschedule, **then** the request is rejected with 403 Forbidden (`unauthorized_administrator`).
3. **Given** a missing or empty justification or justification shorter than 10 characters, **when** submitted, **then** returns 400 Bad Request (`invalid_justification`).

---

### User Story 2 - Staffing Cycle Rotation & Roster Archival (Priority: P1) 🎯 MVP

As the system,  
I want to seal the current staffing cycle and instantiate a new clean staffing cycle upon reschedule,  
So that volunteer availability and team selections are reset for the new date while historical records remain intact.

**Independent Test**:
1. Prior to reschedule, verify event has `CycleNumber = 1` with 2 declared availability responses and 1 active team assignment.
2. Execute reschedule as Administrator.
3. Query the database: verify old cycle has `IsCurrent = 0`.
4. Verify new cycle exists with `CycleNumber = 2`, `IsCurrent = 1`, and `EffectiveEventDate` matching the new start date.
5. Query `/api/events/{eventId}/availability` or `/api/events/{eventId}/team-selection`:
   - Verify current cycle has 0 active team assignments.
   - Verify current cycle has 0 availability responses.
   - Prior responses and assignments remain in the database tied to cycle 1.

**Acceptance Scenarios**:
1. **Given** an event with existing availability and team assignments, **when** rescheduled, **then** previous assignments remain tied to the archived cycle and the new cycle begins completely unstaffed.
2. **Given** the SQL Server filtered unique index `UX_StaffingCycles_CurrentPerEvent`, **when** the transaction commits, **then** no index violation occurs.

---

### User Story 3 - Cascading In-Platform Notifications & Audit Logging (Priority: P1) 🎯 MVP

As a volunteer leader, Chief Leader, or candidate previously associated with the event,  
I want to receive an immediate in-platform notification that the event has been rescheduled,  
So that I know my prior schedule commitment is invalidated and I can declare availability for the new date.

**Independent Test**:
1. Seed an event with an active Chief Leader, 2 active volunteer team members, and 1 unavailable candidate in cycle 1.
2. Admin reschedules the event.
3. Verify in-platform notifications of type `EventRescheduled` are created for:
   - The active Chief Leader.
   - The 2 active volunteer team members.
   - The candidate who declared availability.
4. Verify the notifications contain the event title, prior schedule, new schedule, and administrative reason.
5. Verify an audit log entry of action `EventRescheduled` is persisted with the actor Admin ID, prior/new dates, and justification.

**Acceptance Scenarios**:
1. **Given** multiple distinct stakeholders across active roster, chief leadership, and candidate pool, **when** reschedule completes, **then** each distinct stakeholder receives exactly one `EventRescheduled` notification.
2. **Given** the acting Admin, **when** reschedule commits, **then** an immutable audit log entry is recorded with the Admin's user ID and justification.

---

### User Story 4 - Concurrency, Lifecycle & Temporal Guards (Priority: P2)

As the platform,  
I want to prevent rescheduling elapsed, draft, or terminal events, and guard against stale updates,  
So that system state remains consistent and resilient to race conditions.

**Independent Test**:
1. Attempt to reschedule an event in `Draft` status -> verify 409 Conflict (`invalid_event_status`).
2. Attempt to reschedule an event in `Completed` status -> verify 409 Conflict (`invalid_event_status`).
3. Attempt to reschedule an event in `Cancelled` status -> verify 409 Conflict (`invalid_event_status`).
4. Attempt to reschedule an event whose `StartDateTime <= UtcNow` -> verify 409 Conflict (`window_closed`).
5. Attempt to reschedule with `NewStartDateTime <= UtcNow` -> verify 400 Bad Request (`invalid_schedule`).
6. Attempt to reschedule with `NewEndDateTime <= NewStartDateTime` -> verify 400 Bad Request (`invalid_schedule`).
7. Attempt to reschedule with an outdated `If-Match` header -> verify 412 Precondition Failed (`concurrency_conflict`).

**Acceptance Scenarios**:
1. **Given** an event whose start date has already passed, **when** reschedule is requested, **then** returns 409 Conflict (`window_closed`).
2. **Given** an outdated `If-Match` token, **when** submitted, **then** returns 412 Precondition Failed (`concurrency_conflict`).

---

## 3. Functional Requirements (FR-001 – FR-016)

- **FR-001**: The system MUST restrict the reschedule endpoint (`POST /api/events/{eventId}/reschedule`) exclusively to users with the `Admin` role. Non-admins MUST receive 403 Forbidden (`unauthorized_administrator`).
- **FR-002**: The target event MUST exist in `events.Events`. Non-existent event IDs MUST return 404 Not Found (`not_found`).
- **FR-003**: The target event MUST be in `Status = 'Published'`. Events in `Draft`, `Completed`, or `Cancelled` MUST return 409 Conflict (`invalid_event_status`).
- **FR-004**: The target event's current `StartDateTime` MUST be strictly greater than `DateTimeOffset.UtcNow`. Events with elapsed or started schedules MUST return 409 Conflict (`window_closed`).
- **FR-005**: `NewStartDateTime` MUST be strictly greater than `DateTimeOffset.UtcNow`. Requests violating this MUST return 400 Bad Request (`invalid_schedule`).
- **FR-006**: `NewEndDateTime` MUST be strictly greater than `NewStartDateTime`. Requests violating this MUST return 400 Bad Request (`invalid_schedule`).
- **FR-007**: Both `NewStartDateTime` and `NewEndDateTime` MUST be valid UTC timestamps (`Offset == TimeSpan.Zero`).
- **FR-008**: The request MUST include a non-empty `Justification` string of at least 10 and at most 500 characters. Violations MUST return 400 Bad Request (`invalid_justification`).
- **FR-009**: The request MUST provide a valid base64 concurrency token in the `If-Match` header. Missing or invalid headers MUST return 400 Bad Request (`malformed_request` / `malformed_version`). Mismatched tokens MUST return 412 Precondition Failed (`concurrency_conflict`).
- **FR-010**: The active `events.StaffingCycles` record (`IsCurrent = 1`) for the event MUST be marked historical (`IsCurrent = 0`).
- **FR-011**: A new `events.StaffingCycles` record MUST be inserted with `CycleNumber = oldCycle.CycleNumber + 1`, `IsCurrent = 1`, and `EffectiveEventDate = DateOnly.FromDateTime(NewStartDateTime.Date)`.
- **FR-012**: The update of `IsCurrent = 0` on the old cycle and insertion of `IsCurrent = 1` on the new cycle MUST be sequenced within an atomic database transaction to satisfy the filtered unique index `UX_StaffingCycles_CurrentPerEvent`.
- **FR-013**: Historical records in `staffing.AvailabilityResponses` and `staffing.TeamAssignments` tied to the sealed cycle MUST NOT be deleted or mutated.
- **FR-014**: The new cycle MUST start completely empty: queries for current availability responses and active team members MUST return empty collections.
- **FR-015**: In-platform notifications of type `EventRescheduled` MUST be created for the active Chief Leader, previously active team members, and previously declared candidates from the sealed cycle. Duplicate recipients MUST be deduplicated.
- **FR-016**: An immutable audit log record (`Action = 'EventRescheduled'`) MUST be appended to `audit.AuditLogs`, capturing the previous start/end times, new start/end times, actor Admin ID, and mandatory justification.

---

## 4. Non-Functional Requirements

- **Security & Authorization**: Admin-only authorization enforced via ASP.NET Core authorization policies (`RequireAdmin`). Antiforgery validation enforced via `RequireAntiforgeryTokenAttribute`.
- **Database Concurrency & Integrity**: All operations wrapped in a SQL Server transaction. Concurrency checked via `RowVersion`.
- **Observability**: Telemetry events recorded via `EventRescheduleTelemetry` with distinct outcomes (`rescheduled`, `invalid_event_status`, `window_closed`, `concurrency_conflict`, `unauthorized_administrator`).
- **Accessibility & UX**: Reschedule modal dialog supports keyboard trap, Escape dismissal, WCAG 2.2 AA compliant contrast, 44px min touch targets, and `aria-live="polite"` state announcement.
- **Bundle & Performance**: Component SCSS strictly under 4.00 kB. k6 benchmark maintains sub-second p95 latency under 100 concurrent VUs.
