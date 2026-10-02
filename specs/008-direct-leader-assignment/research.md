# Research & Decisions: SLICE-09 — Direct Leader Assignment

## 1. Decision Provenance & Alignment

- **Approved Blocker 1** (`docs/requirements/LIFECYCLE_SPECIFICATION.md` Section 5 & 7):
  - Chief Leaders and Admins may directly draft a Leader who is `NotAvailable` or has No Record (*Response Needed*); this also creates an immediate `Active` assignment.
  - Chief Leader assignment is a standard direct staffing action; Admin assignment is an audited administrative action.
- **Decision Group 5** (`docs/product/PRODUCT_DISCOVERY.md` Item 8):
  - Team selection and direct drafting immediately create confirmed `Active` team placements without redundant confirmation steps.
  - Leaders who cannot serve must explicitly self-cancel (`SLICE-10`).
- **CHANGE-001 Invariant**:
  - The active Chief Leader for Event $E$ cannot be drafted as a volunteer team member on Event $E$ (HTTP 409 `chief_leader_conflict`).
- **Idempotency & Concurrency**:
  - Filtered unique index `UX_TeamAssignments_ActiveLeaderPerCycle` prevents duplicate active assignments.
  - Duplicate submissions return HTTP 200 with `Idempotent-Replay: true` without creating duplicate notifications or audit records.

## 2. API Design & Store Abstraction

- We can extend `ITeamSelectionStore` or create `IDirectAssignmentStore` in `src/Modules/Staffing/Application/DirectAssignment/`.
  - Let's create `IDirectAssignmentStore` with:
    - `GetDirectAssignmentCandidatesAsync(Guid eventId, Guid actorUserId, CancellationToken cancellationToken)`
    - `DirectAssignLeaderAsync(Guid eventId, Guid candidateUserId, Guid actorUserId, bool isAdmin, CancellationToken cancellationToken)`
- In the frontend:
  - We can integrate direct draft capabilities into the team selection experience (`TeamSelectionComponent`), offering a clean segmented control or tab: "Available Volunteers (X)" vs. "Direct Draft Pool (Y)", or an integrated section.
  - Having the direct draft pool right in the Team Selection view ensures the Chief Leader can view their whole staffing situation in one responsive, accessible screen!
