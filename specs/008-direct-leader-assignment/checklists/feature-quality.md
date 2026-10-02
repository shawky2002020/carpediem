# Feature Quality Checklist: SLICE-09 — Direct Leader Assignment

**Purpose**: Formal pre-task requirements-quality gate covering lifecycle, invariants, audit, notifications, concurrency, authorization, recovery, mobile UX, accessibility, and scope boundaries  
**Created**: 2026-10-01  
**Feature**: [spec.md](../spec.md)  

## Business Scope and Lifecycle

- [x] CHK001 Is the actor strictly defined as an authenticated user holding the active Chief Leader role on the event or an Operations Administrator? [Security, Spec §FR-001]
- [x] CHK002 Is direct assignment permitted exclusively on `Published` events? [Completeness, Spec §FR-002]
- [x] CHK003 Is the temporal window strictly enforced (`Event.StartDateTime > DateTimeOffset.UtcNow`)? [Integrity, Spec §FR-003]
- [x] CHK004 Is the CHANGE-001 same-event exclusivity invariant unambiguously enforced (the active Chief Leader cannot be directly assigned as a volunteer team member on their led event)? [Integrity, Spec §FR-004]
- [x] CHK005 Is immediate active roster placement without redundant confirmation gates enforced per Approved Blocker 1 and Decision Group 5? [Business Rule, Spec §FR-005]

## Audit Evidence and Data Integrity

- [x] CHK006 Is the required audit evidence enumerated precisely (action `DirectLeaderAssignmentExecuted`, resource `TeamAssignment`, acting Chief Leader/Admin, selected volunteer ID, prior availability state `NotAvailable`/`NoResponse`)? [Completeness, Spec §FR-009]
- [x] CHK007 Is atomic consistency between `TeamAssignment` persistence, `NotificationIntent` creation, and `AuditLog` insertion enforced in a single transaction? [Consistency, Spec §FR-009]
- [x] CHK008 Are candidate availability checks and team assignments properly scoped to the Event's current active `StaffingCycle` (`IsCurrent == true`)? [Coverage, Spec §FR-007, FR-009]
- [x] CHK009 Does a filtered unique index prevent duplicate active assignments for the same volunteer on the same cycle? [Integrity, Spec §FR-008]

## Authorization, Concurrency, and Idempotency

- [x] CHK010 Are server-side authorization enforcement and unauthorized denial cases specified (403 Forbidden)? [Security, Spec §FR-001]
- [x] CHK011 Is antiforgery token verification required on mutating endpoints? [Security, Spec §FR-010]
- [x] CHK012 Is safe idempotent replay defined for repeat direct assignment submissions returning 200 with `Idempotent-Replay: true`? [Clarity, Spec §FR-008]
- [x] CHK013 Are conflicts (window closed, invalid event status, chief leader conflict, duplicate active assignment) handled with standard RFC 7807 problem details? [Exception Flow, Spec §FR-002–FR-004]

## Mobile and Accessibility

- [x] CHK014 Are minimum viewport (320px), no horizontal overflow, and responsive vertical reflow specified? [Non-Functional, Spec §FR-015]
- [x] CHK015 Are minimum 44x44px touch targets and visible focus indicators required? [Accessibility, Spec §FR-015]
- [x] CHK016 Are distinct, clear actions for directly drafting candidates provided with in-flight disablement? [Usability, Spec §FR-013, FR-014]
- [x] CHK017 Are polite ARIA status announcements and clear availability badges specified? [Accessibility, Spec §FR-015]

## Boundaries and Dependencies

- [x] CHK018 Is volunteer self-cancellation (`SLICE-10`) and replacement drafting (`SLICE-11`) excluded from this slice? [Scope, Spec §1]
- [x] CHK019 Are dependencies on `Events` (`Published` status, `StaffingCycle`, active Chief Leader assignment) and `Staffing` clearly established? [Dependency, Spec §Assumptions]
- [x] CHK020 Do measurable success criteria cover candidate querying, immediate placement, CHANGE-001 enforcement, window constraints, and 100% automated test passes? [Acceptance Criteria, Spec §SC-001–SC-005]
