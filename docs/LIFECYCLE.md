# Development Lifecycle & Story Governance

## End-to-End Lifecycle Pipeline

```
DISCOVERY
  │
  ▼
REQUIREMENTS
  │
  ▼
ARCHITECTURE
  │
  ▼
EPIC
  │
  ▼
FEATURE SPEC ──► CLARIFICATION (Optional)
  │
  ▼
STORY
  │
  ▼
REVIEW ──► READY ──► LOCKED
                      │
                      ▼
                     PLAN
                      │
                      ▼
                    TASKS
                      │
                      ▼
               IMPLEMENTATION
                      │
                      ▼
                 CODE REVIEW
                      │
                      ▼
                     QA
                      │
                      ▼
                CONVERGENCE ──► DONE
```

---

## Story State Machine Definitions

| State | Responsible Role | Description | Entry Criteria | Exit Criteria |
| :--- | :--- | :--- | :--- | :--- |
| **DRAFT** | BA / PM | Initial story or feature specification draft created via BMAD / Spec Kit. | Discovery complete | Feature spec & acceptance criteria drafted |
| **REVIEW** | SA / SEC / UX / QA | Multi-persona review of spec clarity, security policies, UX fit, and testability. | Spec drafted | All review comments addressed |
| **READY** | PM | Story approved by team and ready for locking. | Reviews passed | Product Manager approval |
| **LOCKED** | PM / SA | Specification frozen. No implementation agent may alter requirements. | Story ready | Formal lock action |
| **IMPLEMENTING**| DEV | Implementation Developer executing tasks strictly against LOCKED plan. | Story locked & plan created | Code written & unit tests passing |
| **VERIFYING** | CR / QA | Code review, static analysis, boundary QA verification, and spec convergence audit. | Dev work complete | Code review & QA tests pass |
| **DONE** | PM | Story verified, converged with specification, and merged to main. | Convergence verified | Merged to main |

---

## Governance & Locking Rules

1. **Strict Immutability of `LOCKED` State**:
   - Once a specification reaches `LOCKED`, implementation agents MUST treat it as read-only.
   - Developers are prohibited from modifying specs, adding unexpected scope, or altering business invariants.

2. **Formal Change Request Procedure**:
   - If an implementation obstacle requires modifying a `LOCKED` requirement:
     1. Implementation halts immediately.
     2. Developer logs an **Architecture / Requirement Clarification Note**.
     3. The Product Manager and Solution Architect must review the proposed change.
     4. If approved, the story moves back to `REVIEW`, updated, re-approved, and re-`LOCKED`.

3. **Convergence Rule (`/speckit-converge`)**:
   - A story cannot transition from `VERIFYING` to `DONE` unless `/speckit-converge` confirms that 100% of tasks in the technical plan have been verified against the code and unit tests.
