# Data Model: SLICE-03 — Chief Leader Appointment & Notification

## `events.ChiefLeaderAssignments`

| Attribute | Shape | Rule |
|---|---|---|
| `Id` | GUID, primary key | Stable assignment identity |
| `EventId` | required Event FK | `NO ACTION`; target must exist and be Draft at appointment |
| `ChiefLeaderUserId` | required User FK | Active user with current Chief Leader role |
| `Status` | required string | `Pending`, `Active`, or `Declined`; this slice creates only `Pending` |
| `AppointedByUserId` | required User FK | Active authorized Admin |
| `AppointedAt` | required UTC instant | Appointment time |
| `AcceptedAt` | nullable UTC instant | Remains null in this slice |
| `AcceptanceSource` | nullable string | Remains null while Pending; later `Direct` or `AdminExternal` |
| `AcceptedByUserId` | nullable User FK | Remains null while Pending |
| `DeclinedAt` | nullable UTC instant | Remains null while Pending |
| `RowVersion` | concurrency token | Opaque assignment version |

Constraints:

- Status check permits only `Pending`, `Active`, `Declined`.
- Acceptance source check permits null, `Direct`, or `AdminExternal`.
- Unique filtered index on EventId for `Pending`.
- Unique filtered index on EventId for `Active`.
- Domain transaction forbids Pending and Active coexisting for one Event.
- Declined rows are never overwritten or deleted by normal appointment workflows.

State transition implemented here: absence of unresolved assignment, including declined-only history, to a new Pending record.

## `notifications.InPlatformNotifications`

| Attribute | Shape | Rule |
|---|---|---|
| `Id` | GUID, primary key | Stable notification identity |
| `RecipientUserId` | required User FK | Appointed candidate |
| `EventId` | nullable Event FK | Required for this notification type |
| `NotificationType` | required string, max 50 | Exactly `ChiefLeaderAppointment` for this slice |
| `CreatedAt` | required UTC instant | Same business operation time |
| `ReadAt` | nullable UTC instant | Null on creation; if set later, cannot precede creation |
| `ContextPayload` | nullable bounded string, max 500 | Allow-listed Event title/context used for later localized rendering |

There is no dismissal field and no external-channel state. A successful appointment creates exactly one unread notification intent.

## `events.ChiefLeaderAppointmentRequests`

| Attribute | Shape | Rule |
|---|---|---|
| `ActorUserId` | required User FK | Initiating Admin; part of unique key |
| `RequestToken` | required GUID | One appointment intent; part of unique key |
| `PayloadFingerprint` | required fixed hash | Deterministic Event/candidate fingerprint |
| `AssignmentId` | required assignment FK | Result returned for identical replay |
| `CreatedAt` | required UTC instant | Diagnostic/audit support |

Unique `(ActorUserId, RequestToken)` prevents duplicate processing. Same token and fingerprint returns the same assignment. Same token with a different Event or candidate returns a conflict. It is written atomically with assignment, notification, and audit.

## Reused `audit.AuditLogs`

Appointment entry:

- `Action`: `ChiefLeaderAppointed`
- `ResourceType`: `ChiefLeaderAssignment`
- `ResourceId`: assignment ID
- `ActorUserId`: appointing Admin
- `OccurredAt`: UTC appointment time
- `CorrelationId`: current request correlation
- `Source`: `Direct`
- `MandatoryReason`: null
- `NewValue`: bounded allow-list containing Event ID, candidate ID, and `Pending`; no credentials or arbitrary identity profile

Audit remains append-only and commits or rolls back with the business result.

## Identity dependency

Candidate queries read existing `identity.Users`, `identity.Roles`, and `identity.UserRoles`. Eligibility requires `Users.IsActive=true` and current role name `ChiefLeader`. Candidate output contains User ID and user name. The appointment transaction repeats this check.

## Relationships

```text
events.Events 1 ──* events.ChiefLeaderAssignments
identity.Users 1 ──* events.ChiefLeaderAssignments (candidate)
identity.Users 1 ──* events.ChiefLeaderAssignments (appointing Admin)
events.ChiefLeaderAssignments 1 ──* events.ChiefLeaderAppointmentRequests
identity.Users 1 ──* events.ChiefLeaderAppointmentRequests
identity.Users 1 ──* notifications.InPlatformNotifications
events.Events 1 ──* notifications.InPlatformNotifications
events.ChiefLeaderAssignments 1 ──* audit.AuditLogs (logical ResourceId)
```

All physical historical relationships use `ON DELETE NO ACTION`.

## Transaction outcomes

| Outcome | Persisted result |
|---|---|
| First valid appointment | One Pending assignment, one request record, one audit, one unread notification |
| Identical replay | Existing assignment returned; no new row |
| Changed payload with same token | Conflict; no change |
| Ineligible candidate, missing/non-Draft/already-assigned Event | Rejected; no change |
| Concurrent same intent | One winner; both resolve to one assignment |
| Concurrent different candidates | One winner; loser conflicts; one unresolved assignment |
| Persistence failure | Entire unit rolls back |
