# Feature Quality Checklist: SLICE-07 — Leader Availability Declaration

**Purpose**: Formal pre-task requirements-quality gate covering lifecycle, invariants, audit, notifications, concurrency, authorization, recovery, mobile UX, accessibility, and scope boundaries
**Created**: 2026-10-01
**Feature**: [spec.md](../spec.md)

## Business Scope and Lifecycle

- [x] CHK001 Is the actor strictly defined as an authenticated user holding the Leader role? [Security, Spec §FR-001]
- [x] CHK002 Is availability declaration permitted exclusively on `Published` events? [Completeness, Spec §FR-002]
- [x] CHK003 Is the temporal window strictly enforced (`Event.StartDateTime > DateTimeOffset.UtcNow`)? [Integrity, Spec §FR-003]
- [x] CHK004 Is CHANGE-001 same-event exclusivity invariant unambiguously enforced (active Chief Leader on an event cannot declare availability for that same event)? [Integrity, Spec §FR-004]
- [x] CHK005 Is self-service mutability between `Available` and `NotAvailable` supported prior to event start? [Usability, Spec §FR-007]

## Audit Evidence and Data Integrity

- [x] CHK006 Is the required audit evidence enumerated precisely (action `AvailabilityDeclared`, resource `AvailabilityResponse`, acting Leader, previous value, new value)? [Completeness, Spec §FR-010]
- [x] CHK007 Is atomic consistency between `AvailabilityResponse` persistence and `AuditLog` insertion enforced in a single transaction? [Consistency, Spec §FR-010]
- [x] CHK008 Are responses properly scoped to the Event's current active `StaffingCycle`? [Coverage, Spec §FR-005]

## Authorization, Concurrency, and Idempotency

- [x] CHK009 Are server-side authorization enforcement and non-leader denial cases specified (403 Forbidden)? [Security, Spec §FR-001]
- [x] CHK010 Is antiforgery token verification required on mutating endpoints? [Security, Spec §FR-011]
- [x] CHK011 Is optimistic concurrency control via `If-Match` rowversion enforced on updates? [Integrity, Spec §FR-008]
- [x] CHK012 Is safe idempotent replay defined for repeat submissions with identical intent? [Clarity, Spec §FR-009]
- [x] CHK013 Are conflicts (window closed, invalid event status, chief leader conflict, stale version) handled with standard RFC 7807 problem details? [Exception Flow, Spec §FR-002–FR-004, FR-008]

## Mobile and Accessibility

- [x] CHK014 Are minimum viewport (320px), no horizontal overflow, and responsive vertical reflow specified? [Non-Functional, Spec §FR-015]
- [x] CHK015 Are minimum 44x44px touch targets and visible focus indicators required? [Accessibility, Spec §FR-015]
- [x] CHK016 Are distinct, clear actions for "Mark Available" and "Mark Not Available" provided with in-flight disablement? [Usability, Spec §FR-014]
- [x] CHK017 Are polite ARIA status announcements and clear badges specified? [Accessibility, Spec §FR-013, FR-015]

## Boundaries and Dependencies

- [x] CHK018 Are Chief Leader team selection (`SLICE-08`) and direct drafting (`SLICE-09`) excluded from this slice? [Scope, Spec §1, Edge Cases]
- [x] CHK019 Are dependencies on `Events` (`Published` status, `StaffingCycle`, active Chief Leader assignment) clearly established? [Dependency, Spec §Assumptions]
- [x] CHK020 Do measurable success criteria cover availability submission, mutability, CHANGE-001 enforcement, window constraints, and 100% automated test passes? [Acceptance Criteria, Spec §SC-001–SC-005]
