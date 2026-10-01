# Feature Quality Checklist: SLICE-06 — Event Publication & Publishing Prerequisite Invariant

**Purpose**: Formal pre-task requirements-quality gate covering lifecycle, invariants, audit, notifications, concurrency, authorization, recovery, mobile UX, accessibility, and scope boundaries
**Created**: 2026-10-01
**Feature**: [spec.md](../spec.md)

## Business Scope and Lifecycle

- [x] CHK001 Is the actor strictly defined as an authenticated Operations Admin? [Security, Spec §FR-001]
- [x] CHK002 Is the lifecycle transition explicitly defined as `Draft` → `Published`? [Completeness, Spec §FR-002, FR-004]
- [x] CHK003 Is the invariant requiring exactly one `Active` Chief Leader assignment unambiguously stated? [Integrity, Spec §FR-002, SC-002]
- [x] CHK004 Is the scheduled time window validation (`StartTime < EndTime`, UTC) required before publication? [Consistency, Spec §FR-002]
- [x] CHK005 Is the post-publication behavior defined (event locked from draft editing, open for volunteer availability in SLICE-07)? [Scope, Spec §FR-010]

## Audit Evidence and Notifications

- [x] CHK006 Is the required audit evidence enumerated precisely (action `EventPublished`, resource Event ID, acting Admin, UTC time, previous value `Draft`, new value `Published`)? [Completeness, Spec §FR-005]
- [x] CHK007 Is atomic consistency between Event status change, audit record, and notification staging required? [Consistency, Spec §FR-005, FR-006]
- [x] CHK008 Are in-platform notification records for eligible volunteer Leaders explicitly required? [Coverage, Spec §FR-006]

## Authorization, Concurrency, and Idempotency

- [x] CHK009 Are server-side authorization enforcement and non-admin denial cases specified? [Security, Spec §FR-001, US1]
- [x] CHK010 Is antiforgery token verification required on the mutation endpoint? [Security, Spec §FR-007]
- [x] CHK011 Is optimistic concurrency control via `If-Match` rowversion enforced? [Integrity, Spec §FR-008]
- [x] CHK012 Is safe idempotent replay defined for repeat submissions of an already-published event? [Clarity, Spec §FR-009]
- [x] CHK013 Are conflicts with non-Draft states (`Cancelled`, `Completed`, or stale version) handled with proper RFC 7807 problem details? [Exception Flow, Spec §FR-003, FR-008, US3]

## Mobile and Accessibility

- [x] CHK014 Are minimum viewport (320px), no horizontal overflow, and responsive vertical reflow specified? [Non-Functional, Spec §FR-013]
- [x] CHK015 Are minimum 44x44px touch targets and visible focus indicators required? [Accessibility, Spec §FR-013]
- [x] CHK016 Is explicit confirmation required to prevent accidental mobile publication? [Usability, Spec §FR-012]
- [x] CHK017 Are polite ARIA status announcements and error announcements specified? [Accessibility, Spec §FR-013, US4]

## Boundaries and Dependencies

- [x] CHK018 Are volunteer availability declarations, team selection, and event rescheduling excluded from this slice? [Scope, Spec §FR-005, FR-010]
- [x] CHK019 Are dependencies on existing Draft Event and Active Chief Leader models clearly established? [Dependency, Spec §Assumptions]
- [x] CHK020 Do measurable success criteria cover publication, invariant blocking, atomic audit/notifications, idempotency, and automated test passes? [Acceptance Criteria, Spec §SC-001–SC-005]
