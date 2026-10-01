# Specification Quality Checklist: SLICE-07 — Leader Availability Declaration (Available / NotAvailable)

**Purpose**: Validate specification completeness before clarification and planning
**Created**: 2026-10-01
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details in functional user scenarios
- [x] Focused on user value, operational workflow, and volunteer experience
- [x] Written with clear domain terminology
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No `[NEEDS CLARIFICATION]` markers remain
- [x] Invariants and guard rules are unambiguous (Event must be Published, StartTime in future, CHANGE-001 exclusivity)
- [x] Success criteria are measurable
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (past start time, concurrent mutability, material reschedule cycle scoping)
- [x] Scope is clearly bounded (strictly covers availability declaration; excludes Chief Leader team selection, which belongs to SLICE-08)
- [x] Dependencies and assumptions identified (Published Event and active StaffingCycle)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover initial declaration, mutability, CHANGE-001 dual-role exclusivity, and mobile a11y
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] Clear authorization rules defined (Authenticated users holding Leader role; active Chief Leader excluded per CHANGE-001)

## Notes

- Reviewed against `LIFECYCLE_SPECIFICATION.md` Section 4 & 7, `CHANGE-001`, `V1_DELIVERY_ORDER.md`, `V1_FEATURE_SLICES.md`, `ROLE_PERMISSION_MATRIX.md`, ADR-002, ADR-003, ADR-005, and ADR-006.
- Enforces CHANGE-001 same-event exclusivity invariant: an active Chief Leader on an event cannot declare availability as a volunteer Leader on that same event.
