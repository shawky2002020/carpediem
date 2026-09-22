# Data Model: SLICE-04 — Chief Leader In-Platform Response

## Existing `events.ChiefLeaderAssignments`

| Field | Direct accept | Direct decline |
|---|---|---|
| `Status` | Pending → Active | Pending → Declined |
| `AcceptedAt` | UTC response instant | Null |
| `AcceptanceSource` | `Direct` | Null |
| `AcceptedByUserId` | Candidate ID | Null |
| `DeclinedAt` | Null | UTC response instant |
| `RowVersion` | Changes | Changes |

Existing appointment/candidate/Event/Admin IDs do not change. Declined records remain history. Existing Pending/Active uniqueness, status/source checks, FKs, and rowversion remain in force.

## Existing `events.Events`

Related Event must be Draft. Read title, UTC schedule, and location for candidate context. Response changes no Event data.

## Existing `audit.AuditLogs`

First successful accept writes `ChiefLeaderAccepted`; first decline writes `ChiefLeaderDeclined`. Both use `Source=Direct`, candidate actor, assignment/Event identity, UTC time, and correlation. No mandatory reason.

## Existing `notifications.InPlatformNotifications`

Decline creates one `ChiefLeaderDeclined` intent for each active Admin, with Event ID, UTC time, unread status, and bounded context. No read/dismiss behavior.

## Outcomes

| Input | Persisted result |
|---|---|
| Valid Pending accept, no team conflict | Active + one audit; Event Draft |
| Valid Pending decline | Declined + one audit + Admin alerts; Event Draft |
| Same-action replay | Existing state; no new side effect |
| Opposite/stale/ineligible/non-Draft | No transition |
| Concurrent responses | One winner; loser gets conflict |
| Audit/alert/save failure | Roll back to Pending |

The active team-assignment guard is a domain input. No TeamAssignment is creatable in the current schema; the Staffing slice must wire its lookup before it creates Active team assignments.
