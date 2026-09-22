# Research: SLICE-04 — Chief Leader In-Platform Response

## Existing state and rowversion

- **Decision**: Transition the SLICE-03 assignment and use its SQL rowversion as the concurrency token.
- **Rationale**: Pending, Active, Declined, direct acceptance fields, and rowversion already exist. A response is a single irreversible decision.
- **Alternatives considered**: New response table or idempotency ledger would duplicate committed state.

## Atomic audit and Admin alert

- **Decision**: In one SQL transaction, append one direct audit entry and, on decline, stage `ChiefLeaderDeclined` notification intents for active Admins.
- **Rationale**: ADR-005 and the roadmap require traceable responses; the lifecycle requires Admin alert. Atomic commit avoids a response without evidence or alert.
- **Alternatives considered**: Asynchronous staging permits a partial result; external delivery and inbox belong later.

## Candidate authorization

- **Decision**: ChiefLeader role policy plus database check for active user and exact assignment candidate on each read/write.
- **Rationale**: Role alone does not grant ownership; revocation must take effect.
- **Alternatives considered**: Client candidate IDs allow horizontal access.

## CHANGE-001 seam

- **Decision**: Keep a domain activation guard for active same-Event team assignment. No such record can exist before Staffing schema/capability. Staffing must supply the authoritative lookup before adding team assignments.
- **Rationale**: Preserve ratified invariant without implementing later staffing.
- **Alternatives considered**: Speculative TeamAssignment table violates slice scope.

## API and UX

- **Decision**: Candidate-only Pending list/detail GET and explicit Accept/Decline POST with antiforgery, `If-Match`, typed results, RFC 7807 errors, and focused Angular routes.
- **Rationale**: Matches host conventions and makes stale/uncertain outcomes observable.
- **Alternatives considered**: Automatic acceptance and full inbox violate scope.

No research item remains unresolved.
