# Technical Research & Architectural Findings: SLICE-06 — Event Publication

## 1. Context & Architectural Positioning

SLICE-06 completes Milestone M1's event authoring phase by transitioning an Event from `Draft` to `Published`. This is the essential publishing prerequisite invariant defined in `LIFECYCLE_SPECIFICATION.md` Section 2 D & 3 B.

### Key Preconditions:
1. **Event State**: Must be in `Draft`.
2. **Leadership Invariant**: Exactly one accepted `Active` `ChiefLeaderAssignment`.
3. **Temporal Invariant**: `StartDateTime < EndDateTime` (both in UTC).
4. **Optimistic Concurrency**: Matching `RowVersion` via `If-Match`.

## 2. Notification Mechanics (ADR-006)

ADR-006 explicitly chose **Persisted Notifications + Lifecycle Polling** over SignalR WebSocket streaming to preserve mobile resilience and zero distributed backplane requirements.

For event publication:
- Recipients: All active users holding the `Leader` role.
- Type: `InPlatformNotification.EventPublished` (`"EventPublished"`).
- Persistence: Staged and saved in the identical EF Core transaction alongside the Event status transition and the audit log record.
- Polling/Retrieval: Handled by existing notification infrastructure; Leaders will see unread counts upon next navigation, app launch, or polling interval.

## 3. Optimistic Concurrency & Idempotency

- The HTTP endpoint requires `If-Match: "<base64-rowversion>"`.
- If a stale version is supplied, SQL Server concurrency check or EF Core `DbUpdateConcurrencyException` raises a conflict, mapped to HTTP 409 Conflict with code `stale_event`.
- If an Admin retries the publish request and the Event is already `Published`:
  - If the actor matches or the replay is identical: returns HTTP 200 OK with `Idempotent-Replay: true` and the published representation.
  - If the Event is in an incompatible state (e.g. `Cancelled`), returns HTTP 409 Conflict (`invalid_event_state`).

## 4. Security & Server-Side Enforcement (Constitutional Principle V)

- Endpoint: `POST /api/events/drafts/{id}/publish`.
- Authorization: Policy `RequireAdmin`. Non-admin requests are rejected server-side with 403 Forbidden.
- Antiforgery: Enforced via `[RequireAntiforgeryToken]` attribute and `X-XSRF-TOKEN` header.

## 5. Mobile & Accessibility Constraints (Constitutional Principles XI & XII)

- Viewports: Seamless reflow from 320px up to 1280px+.
- Targets: All buttons and interactive links must have minimum dimensions of 44x44 CSS px.
- Accidental Publish Prevention: Explicit two-step confirmation (dialog or inline disclosure card) so an accidental single tap on a mobile device does not publish the event.
- Live announcements: ARIA live regions (`role="status"`, `aria-live="polite"`) for publishing progress and success.
