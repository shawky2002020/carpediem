# Technical Research & Architectural Findings: SLICE-07 — Leader Availability Declaration

## 1. Context & Architectural Positioning

SLICE-07 begins the volunteer staffing phase of Milestone M1. Following Event publication (`SLICE-06`), volunteer Leaders declare schedule availability (`Available` / `NotAvailable`). This data forms the candidate pool from which Chief Leaders select team members (`SLICE-08`).

### Architecture Boundaries (`MODULE_BOUNDARIES.md` Section 3)
- **Owning Module**: `Carpediem.Modules.Staffing`.
- **Entities**:
  - `AvailabilityResponse` in schema `staffing`.
- **Collaborating Modules**:
  - `Carpediem.Modules.Events`: Provides `Event` status verification (`Status == Published`) and active `StaffingCycle` reference (`IsCurrent == true`). Checks whether user has an `Active` `ChiefLeaderAssignment`.
  - `Carpediem.Modules.Identity`: Validates authenticated user holds the `Leader` role.

---

## 2. Domain Invariants & Guard Rules

1. **Event Status Invariant**:
   - Availability declaration is permitted ONLY when `Event.Status == EventStatus.Published`.
   - If Event is `Draft`, `Completed`, or `Cancelled`, mutation is rejected with HTTP 409 Conflict (`invalid_event_status`).
2. **Temporal Window Invariant**:
   - Availability declaration is open strictly while `Event.StartDateTime > DateTimeOffset.UtcNow`.
   - Once the event's start time arrives or passes, the window is closed; requests are rejected with HTTP 409 Conflict (`window_closed`).
3. **CHANGE-001 Same-Event Exclusivity Invariant**:
   - A user who is the active Chief Leader for Event $E$ (`ChiefLeaderAssignment.Status == Active`) is strictly prohibited from declaring availability or participating as a volunteer Leader on Event $E$.
   - Any attempt by the active Chief Leader to submit availability on their assigned event is rejected with HTTP 409 Conflict (`chief_leader_conflict`).
   - A user who is Chief Leader on Event $A$ can freely declare availability on Event $B$ if they also hold the `Leader` role.
4. **Staffing Cycle Scoping**:
   - All declarations belong to the event's current active cycle: `StaffingCycle.IsCurrent == true`.
   - If an event is later rescheduled in `SLICE-12`, prior availability responses remain archived under the historical cycle, and the new cycle begins with `No AvailabilityResponse`.

---

## 3. Concurrency, Indexing & Storage Strategy (ADR-009)

### Persistence Schema: `staffing.AvailabilityResponses`
- `Id`: `uniqueidentifier` NOT NULL PRIMARY KEY
- `StaffingCycleId`: `uniqueidentifier` NOT NULL FK to `events.StaffingCycles(Id)`
- `EventId`: `uniqueidentifier` NOT NULL FK to `events.Events(Id)`
- `UserId`: `uniqueidentifier` NOT NULL FK to `identity.Users(Id)`
- `Response`: `nvarchar(32)` NOT NULL (`'Available'` or `'NotAvailable'`)
- `DeclaredAt`: `datetimeoffset(7)` NOT NULL
- `RowVersion`: `rowversion` NOT NULL

### Database Indexes:
- **`UX_AvailabilityResponses_StaffingCycle_User`**: Unique clustered/non-clustered index on `(StaffingCycleId, UserId)` enforcing at most one response per user per cycle.
- **`IX_AvailabilityResponses_Event_Response`**: Non-clustered index on `(EventId, Response)` for fast candidate pool queries in `SLICE-08`.

### Optimistic Concurrency:
- `GET /api/events/{eventId}/availability/me` returns the current response, `version` (Base64 rowversion), and window status.
- `POST /api/events/{eventId}/availability` accepts `If-Match: "<rowversion>"`.
- If creating for the first time, `If-Match` may be omitted or `If-None-Match: *`.
- If updating, `If-Match` must match the current `RowVersion`. Mismatches yield HTTP 409 Conflict (`stale_version`).

### Idempotency:
- If a client replays an identical declaration (same user, cycle, and response value), the server detects matching state and returns HTTP 200 OK with `Idempotent-Replay: true` without appending duplicate audit logs.

---

## 4. Security & Antiforgery (ADR-002, ADR-003, Principle V)

- **Authentication**: ASP.NET Core Identity cookies with `HttpOnly`, `SameSite=Lax`, and `Secure`.
- **Authorization**: Policy `RequireLeader` (user has role `Leader`). Non-leaders receive 403 Forbidden.
- **Antiforgery**: `[RequireAntiforgeryToken]` on all mutating endpoints (`POST`); verified via `X-XSRF-TOKEN` header.

---

## 5. Mobile & Accessibility Constraints (Principles XI & XII)

- **Viewports**: 320px, 360px, 390px, 430px, and desktop reflow without horizontal scrolling.
- **Touch Targets**: Minimum 44x44 CSS px for "Mark Available", "Mark Not Available", and reload buttons.
- **Typography Reflow**: Clean layout under 200% font zoom.
- **ARIA Live Regions**: `role="status"` and `aria-live="polite"` announcing declaration outcomes to screen readers.
- **Dual-Role Visual Clarity**: When viewing an event where the current user is the Active Chief Leader, the availability card clearly displays: "You are the Chief Leader for this event" and disables/hides availability submission.
