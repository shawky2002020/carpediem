# Feature Specification: SLICE-14 — Event Scheduled Completion & Administrative Closeout

**Feature Branch**: `codex/012-event-completion-closeout`  
**Created**: 2026-10-02  
**Status**: Draft  
**Input**: User request: "Event Scheduled Completion & Administrative Closeout per Milestone M3 (Completion, Attendance, Profiling & Dashboards), EPIC-02"  

---

## 1. Executive Summary & Business Intent

In event operations, once an event's scheduled window has elapsed (`CurrentTime > Event.EndTime`), or when an event has physically wrapped up, the gathering transitions to its terminal lifecycle state: **`Completed`**. Historical reporting, attendance tracking, volunteer participation profiling (`SLICE-18`), and operational dashboards (`SLICE-19`) require an unambiguous, persisted status rather than calculated virtual states that drift.

Per **Milestone M3** and **ADR-007** (ratified in `docs/decisions/ADR-007-background-processing-strategy.md`, `docs/requirements/LIFECYCLE_SPECIFICATION.md` Section 2.B, `docs/data/CONCURRENCY_MODEL.md` Race 9, and `docs/roadmap/V1_FEATURE_SLICES.md` Item SLICE-14):

1. **Dual Completion Paths**:
   - **System Background Worker**: A native ASP.NET Core `BackgroundService` (`EventLifecycleBackgroundWorker`) periodically scans the database for `Published` events where `UtcNow >= EndDateTime` and transitions them to `Completed`.
   - **Administrative Manual Closeout**: Operations Administrators (`CarpediemRoles.Admin`) can explicitly trigger immediate closeout on a published event via `POST /api/events/{eventId}/complete`.
2. **Authorized Persona Boundary**:
   - Manual closeout is strictly restricted to Platform Administrators (`CarpediemRoles.Admin`). Non-administrators (Chief Leaders, volunteer leaders) receive 403 Forbidden (`unauthorized_administrator`).
3. **Event Lifecycle Preconditions & Terminal State**:
   - The event must be in `Status = Published`.
   - Events in `Draft` cannot be completed; returns 409 Conflict (`invalid_event_status`).
   - Events in `Cancelled` cannot be completed; returns 409 Conflict (`invalid_event_status`).
   - `Completed` is a **terminal state**: once completed, an event cannot be rescheduled, republished, cancelled, or returned to draft.
4. **Decoupled Attendance Invariant (Core Architecture)**:
   - Event completion is strictly decoupled from on-site attendance recording (`SLICE-15`).
   - Completing an event does NOT check or require attendance records to exist.
   - If attendance is unrecorded at completion, the event dynamically produces the derived operational condition `Pending Attendance` for dashboards (`SLICE-19`).
5. **Multi-Replica Safe Concurrency (ADR-007 & Race 9)**:
   - Multiple background worker replicas or concurrent Admin closeout requests race safely using database compare-and-transition semantics and `Events.RowVersion`.
   - Exactly one execution successfully commits the transition to `Completed` and writes the audit log. The losing execution detects that `Status` is already `Completed` and **safely no-ops**.
6. **Optimistic Concurrency & Idempotent Replay**:
   - Admin closeout endpoint inspects the `If-Match` request header. Mismatched rowversions return 412 Precondition Failed (`concurrency_conflict`).
   - Replaying a closeout request against an event already in `Completed` with the matching version returns 200 OK with `Idempotent-Replay: true`.
7. **Auditing & Notifications**:
   - An immutable audit log entry (`Action = 'EventCompleted'`) is committed atomically, recording the actor (Admin User ID or `System.EventLifecycleWorker`), previous status (`Published`), and new status (`Completed`).
   - Zero notifications are dispatched upon event completion per ADR-006 and V1_FEATURE_SLICES.md.

---

## 2. User Scenarios & Acceptance Criteria

### User Story 1 - Automated Background Completion Worker (Priority: P1) 🎯 MVP

As the system,  
I want a periodic background worker to automatically transition published events whose end time has elapsed to `Completed`,  
So that events reach their authoritative completion state even if no user accesses the platform immediately after the event ends.

**Independent Test**:
1. Seed a `Published` event whose `EndDateTime` is in the past (`EndDateTime < UtcNow`).
2. Trigger the `EventLifecycleBackgroundWorker` scan.
3. Verify that `Event.Status` transitions to `Completed`.
4. Verify an audit record is created with `Action = 'EventCompleted'`, `ActorUserId = 'System.EventLifecycleWorker'`, and `NewStatus = 'Completed'`.
5. Run the worker scan a second time: verify zero rows are modified and zero duplicate audit entries are written.

**Acceptance Scenarios**:
1. **Given** a `Published` event where `UtcNow >= EndDateTime`, **when** the background worker executes a scan, **then** the event transitions to `Completed` and an audit entry is recorded.
2. **Given** a `Published` event whose `EndDateTime` is in the future (`UtcNow < EndDateTime`), **when** the background worker scans, **then** the event remains in `Published` status.
3. **Given** multiple worker instances scanning concurrently, **when** attempting completion on the same event, **then** exactly one transition commits and the other safely no-ops.

---

### User Story 2 - Platform Administrator Executes Manual Event Closeout (Priority: P1) 🎯 MVP

As a Platform Administrator,  
I want to explicitly close out a published event through the web application,  
So that I can formally mark an event as completed on demand.

**Independent Test**:
1. Log in as an Administrator.
2. Select a `Published` event.
3. Submit `POST /api/events/{eventId}/complete` with a matching `If-Match` header.
4. Verify HTTP 200 OK returning the updated event details with `Status = 'Completed'`.
5. Verify non-Admin users receive 403 Forbidden (`unauthorized_administrator`).
6. Verify stale rowversion returns 412 Precondition Failed (`concurrency_conflict`).

**Acceptance Scenarios**:
1. **Given** a `Published` event, **when** Admin submits `POST /api/events/{eventId}/complete`, **then** the event status transitions to `Completed` and HTTP 200 OK is returned.
2. **Given** a non-Admin user (Chief Leader or volunteer leader), **when** attempting closeout, **then** returns 403 Forbidden (`unauthorized_administrator`).
3. **Given** an event in `Draft` or `Cancelled` status, **when** Admin attempts closeout, **then** returns 409 Conflict (`invalid_event_status`).
4. **Given** an event already in `Completed` status, **when** Admin resubmits closeout with matching `If-Match`, **then** returns 200 OK with `Idempotent-Replay: true`.

---

### User Story 3 - Decoupled Attendance & Derived Dashboard State (Priority: P1) 🎯 MVP

As an operations stakeholder,  
I want event completion to proceed regardless of whether attendance roll call has been finalized,  
So that event closure is not blocked by on-site attendance workflows while flagging unrecorded attendance.

**Independent Test**:
1. Create and publish an event with active team assignments but no recorded attendance.
2. Complete the event via background worker or Admin closeout.
3. Verify that the event successfully completes (`Status = Completed`).
4. Verify that existing team assignments remain intact in `Active` status.
5. Verify that the event is ready for post-event attendance recording (`SLICE-15`).

**Acceptance Scenarios**:
1. **Given** an event with unrecorded attendance, **when** completed, **then** completion succeeds without requiring attendance submission.
2. **Given** an event with already finalized attendance, **when** completed, **then** completion succeeds and preserves all attendance outcome records.

---

### User Story 4 - Accessible Event Closeout UI & Status Display (Priority: P2)

As an Administrator using the web client,  
I want a responsive, accessible interface to close out published events and view their completion status,  
So that I can manage event lifecycle transitions smoothly across mobile and desktop devices.

**Independent Test**:
1. Navigate to the event view as an Admin.
2. View status badge reflecting `Published` or `Completed`.
3. Click "Closeout Event" to open the confirmation modal.
4. Confirm closeout; observe immediate UI update to `Completed` with screen-reader announcement.
5. Run automated WCAG 2.2 AA audit: verify 0 violations, 44px min touch targets, and clean 200% zoom reflow.

---

## 3. Functional Requirements (FR)

| Req ID | Description |
| :--- | :--- |
| **FR-001** | Platform Administrator Persona: Only `CarpediemRoles.Admin` may invoke manual closeout. Non-admins receive 403 Forbidden (`unauthorized_administrator`). |
| **FR-002** | Target Event Lifecycle Guard: Target event must be `Published`. Draft/Cancelled events return 409 Conflict (`invalid_event_status`). |
| **FR-003** | Terminal State Invariant: Completed events cannot be edited via draft endpoints, rescheduled, cancelled, or transitioned out of `Completed`. |
| **FR-004** | System Background Worker: `EventLifecycleBackgroundWorker` periodically scans for `Published` events with `UtcNow >= EndDateTime` and completes them. |
| **FR-005** | Multi-Replica Idempotency: Concurrency conflicts in the background worker safely catch collisions and no-op without crashing or duplicate audits. |
| **FR-006** | Optimistic Concurrency Control: Admin closeout endpoint enforces `If-Match` against `Event.RowVersion`. Stale token triggers 412 Precondition Failed (`concurrency_conflict`). |
| **FR-007** | Safe Idempotent Replay: Repeated closeout requests with matching `If-Match` return 200 OK with `Idempotent-Replay: true`. |
| **FR-008** | Decoupled Attendance Invariant: Completion does NOT check or require attendance records; preserves existing team assignments. |
| **FR-009** | Immutable Audit Trail: Appends an audit entry (`Action = 'EventCompleted'`), capturing actor, `EventId`, `PreviousStatus`, and `NewStatus`. |
| **FR-010** | Zero Notifications: No in-platform notifications are dispatched on completion per ADR-006. |
| **FR-011** | Mobile-First UI & WCAG 2.2 AA: Closeout modal and status badges responsive from 320px to desktop, 44px touch targets, 200% font zoom reflow. |
| **FR-012** | Strict Scope Boundary: On-site attendance recording (`SLICE-15`) and attendance correction (`SLICE-16`) are strictly deferred. |
