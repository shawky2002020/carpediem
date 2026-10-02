# Technical Research: SLICE-10 — Leader Self-Cancellation with Mandatory Justification

## 1. Domain State Transition & Invariants

In `src/Modules/Staffing/Domain/TeamSelection/TeamAssignment.cs`:
- The entity already possesses:
  - `Status`: enum `TeamAssignmentStatus` (`Active = 1`, `Cancelled = 2`).
  - `CancellationReason`: string? nullable, configured with `HasMaxLength(500)`.
  - `CancelledAt`: DateTimeOffset? nullable, column type `datetimeoffset`.
  - `RowVersion`: byte[] concurrency token (`IsRowVersion()`).
- Add domain method `Cancel(string justification, DateTimeOffset cancelledAtUtc, DateTimeOffset eventStartUtc)`:
  - Enforce `justification` is not null or whitespace; trim and assert length $\le 500$.
  - Enforce current status is `TeamAssignmentStatus.Active`. (Cannot cancel a record that is already Cancelled).
  - Enforce `eventStartUtc > cancelledAtUtc`. (Cannot cancel after the event has already started).
  - Transition `Status = TeamAssignmentStatus.Cancelled`.
  - Set `CancellationReason = justification.Trim()`.
  - Set `CancelledAt = cancelledAtUtc`.

## 2. Resource-Based Authorization (ADR-003)

- Authorization policy: `RequireLeader` (at endpoint level) + Resource-level ownership check.
- The acting user must either:
  1. Own the assignment (`TeamAssignment.UserId == actorUserId`), OR
  2. Possess the `Admin` role (`context.User.IsInRole(CarpediemRoles.Admin)`).
- If neither condition is met, return HTTP 403 Forbidden with problem code `unauthorized_assignment_owner`.

## 3. Idempotency & Optimistic Concurrency

- Concurrency: Clients read the assignment version (base64 `RowVersion`) and submit it via the standard `If-Match` HTTP header.
- If `If-Match` is provided and does not match the current `RowVersion`:
  - If the assignment is already in status `Cancelled` AND its `CancellationReason` matches the submitted `justification.Trim()`, treat as an idempotent replay: return HTTP 200 OK with `Idempotent-Replay: true` header.
  - Otherwise, return HTTP 412 Precondition Failed / 409 Conflict with code `concurrency_conflict`.

## 4. Atomic Multi-Aggregate Side-Effects

Within a single SQL Server transaction (via EF Core Execution Strategy):
1. **Update `staffing.TeamAssignments`**: transition to `Cancelled`, set `CancelledAt`, `CancellationReason`.
2. **Alert Chief Leader & Admins**:
   - Query the active Chief Leader for the event: `db.ChiefLeaderAssignments.SingleOrDefaultAsync(a => a.EventId == eventId && a.Status == ChiefLeaderAssignmentStatus.Active)`.
   - Query active Admin user IDs: `userManager.GetUsersInRoleAsync(CarpediemRoles.Admin)`.
   - Stage in-platform notification: `InPlatformNotification.TeamMemberCancelled(recipientId, eventId, now, payload)` for the Chief Leader (if present) and each active Admin.
3. **Audit Log**:
   - Write `AuditLog.VolunteerSelfCancelled(assignmentId, actorUserId, correlationId, now, reason, isAdmin)`.
4. Commit transaction and emit structured telemetry.

## 5. Frontend Modal UX & Accessibility (WCAG 2.2 AA)

- Use a controlled modal component or embedded dialog state.
- Semantics: `<div role="dialog" aria-modal="true" aria-labelledby="cancel-dialog-title">`.
- Focus trap: Focus moves into modal on open; Tab cycles through modal interactive elements; Esc key dismisses.
- Character counter: Live counter showing current length (e.g. `45 / 500 characters`).
- Validation: Prevent submission if empty, whitespace, or exceeding 500 characters.
- Live announcement: Use `aria-live="polite"` to announce successful cancellation to assistive technologies.
- Styling: Mobile-first responsive, 44px minimum touch targets, zero layout shift, SCSS strictly under the 4.00 kB budget.
