# Data Model: SLICE-02 Draft Event Authoring

**Scope**: Only data used by Draft creation/editing and minimum Identity/Audit support. [CHANGE-002](../../docs/decisions/CHANGE-002-draft-event-minimum-fields.md), the [locked relational model](../../docs/data/RELATIONAL_SCHEMA.md), and the [feature plan](plan.md) govern this design. No future roster, publication, or attendance model is added.

## `events.Events` — aggregate root

| Attribute | Physical shape | Rule |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Stable Event identity |
| `Title` | `nvarchar(200) NOT NULL` | Required; Unicode whitespace-only rejected by domain/server; 200 is the locked schema cap, not a new product limit |
| `StartDateTime` | `datetimeoffset NOT NULL` | UTC instant, offset `+00:00`; input interpreted in `Africa/Cairo` |
| `EndDateTime` | `datetimeoffset NOT NULL` | UTC instant; must be after StartDateTime |
| `Location` | `nvarchar(max) NOT NULL` | Required descriptive text; no unratified length cap or venue taxonomy |
| `Notes` | `nvarchar(max) NULL` | Optional, may be empty or null |
| `Status` | `varchar(20) NOT NULL` | New Event is `Draft`; only Draft edits in this slice; locked status check retains future values without implementing transitions |
| `CreatedByUserId` | `uniqueidentifier NOT NULL` FK to `identity.Users` | Creating Admin; `ON DELETE NO ACTION` |
| `CreatedAt` | `datetimeoffset NOT NULL` | UTC creation time |
| `RowVersion` | SQL Server `rowversion NOT NULL` | Opaque edit concurrency token |

**Constraints**: `CK_Events_Status`, `CK_Events_TimeWindow (EndDateTime > StartDateTime)`, checks requiring a zero offset for both canonical Event timestamps, not-null required fields, and basic SQL nonblank checks for Title/Location where safely expressible. Full Unicode whitespace validation remains server/domain-owned. Date ordering compares UTC instants. Server rejects malformed/unusable local time, equal/reversed windows, and disallowed lifecycle mutation; a past window alone is allowed. Validation does not silently trim or otherwise rewrite accepted nonblank Title, Location, or Notes values.

**Lifecycle available here**:

```text
No Event ──CreateDraft──▶ Draft ──EditDraft──▶ Draft
```

No publish, cancel, Chief Leader appointment, staffing, or deletion operation is included.

## `events.StaffingCycles` — immutable schedule context

| Attribute | Physical shape | Rule |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Stable context identity |
| `EventId` | `uniqueidentifier NOT NULL` FK to `events.Events` | `ON DELETE NO ACTION` |
| `CycleNumber` | `int NOT NULL` | Starts at 1, increases on a Draft date change; positive check |
| `IsCurrent` | `bit NOT NULL` | Exactly one current context after a successful Draft create/edit |
| `EffectiveEventDate` | `date NOT NULL` | Cairo calendar date of the saved Event start |
| `CreatedAt` | `datetimeoffset NOT NULL` | UTC creation time |

**Constraints**: Unique `(EventId,CycleNumber)`; filtered unique `(EventId) WHERE IsCurrent=1` ensures *at most* one current context. The aggregate/application transaction ensures *at least* one for a persisted Draft. Creation inserts cycle 1 with the Event. A Draft edit that changes the Cairo start date marks the old cycle noncurrent and creates the next current cycle in the same transaction, preserving history. An edit within the same date leaves the cycle as is. This has no volunteer/notification side effects and is distinct from SLICE-12 published rescheduling.

## `audit.AuditLogs` — append-only evidence

Use the locked [audit schema](../../docs/data/RELATIONAL_SCHEMA.md) and [ADR-005](../../docs/decisions/ADR-005-audit-trail-strategy.md): `Id`, UTC `OccurredAt`, `ActorUserId`, `Action`, `ResourceType`, `ResourceId`, `CorrelationId`, optional `MandatoryReason`, `Source`, and optional structured `PreviousValue`/`NewValue`.

- `Events.EventCreated` and `Events.DraftEventUpdated` are the two actions required by the [SLICE-02 definition](../../docs/roadmap/V1_FEATURE_SLICES.md). `ResourceType=Event`; `Source=Direct`; actor is the authenticated Admin. Ordinary Draft authoring requires no reason.
- Creation records initial `Draft` status and authored field names. An edit records changed field names and old/new Title, Location, and UTC schedule values. Notes content is excluded; the audit records only whether Notes changed. Never serialize credentials, raw request bodies, or arbitrary entity snapshots.
- Append in the exact business transaction. No audit success record survives a rejected or rolled-back mutation. No normal workflow updates/deletes an audit row.

## `events.DraftCreateRequests` — idempotent creation support

| Attribute | Physical shape | Rule |
|---|---|---|
| `ActorUserId` | `uniqueidentifier NOT NULL` FK to `identity.Users` | Key scoped to initiating Admin |
| `RequestToken` | `uniqueidentifier NOT NULL` | Client-supplied creation intent token |
| `PayloadFingerprint` | fixed-length hash bytes, `NOT NULL` | Hash of canonical validated request fields; no sensitive text stored |
| `EventId` | `uniqueidentifier NOT NULL` FK to `events.Events` | Result to return on replay |
| `CreatedAt` | `datetimeoffset NOT NULL` | UTC diagnostic time |

**Key**: unique/primary `(ActorUserId,RequestToken)`. The same token and fingerprint identifies one Event, including concurrent and later retries; changed content with the same token is a conflict. Creation commits ledger, Event, cycle, and audit together. No retention/deletion period is invented here.

## Identity dependency

`identity.Users` and role membership come from the minimal ASP.NET Core Identity baseline. All Draft operations require an authenticated, active user with `Admin` authority. Event creator and audit actor reference that user. Development fixtures supply one Admin and non-Admin roles for authorization verification; onboarding workflow and profile fields are deferred.

## Relationship and consistency view

```text
identity.Users 1 ──* events.Events (CreatedByUserId)
events.Events 1 ──* events.StaffingCycles (one current)
identity.Users 1 ──* events.DraftCreateRequests (ActorUserId)
events.Events 1 ──* events.DraftCreateRequests (EventId; repeated intents may reference same result only through same key)
identity.Users 1 ──* audit.AuditLogs (ActorUserId)
events.Events 1 ──* audit.AuditLogs (ResourceId with ResourceType=Event)
```

Audit `ResourceId` is a cross-resource identifier rather than a physical Event FK in the locked schema. The use case supplies the relationship and commits atomically. All explicit historical FKs use `NO ACTION` delete behavior.

## Transaction and concurrency outcomes

| Operation | Reads/guard | Atomic writes | Rejection outcome |
|---|---|---|---|
| Create | Admin, request token/fingerprint, Cairo validation | Event + cycle 1 + audit + request record | No partial Draft; replay returns existing result; changed-payload token conflict |
| Read/list | Admin, `Status=Draft` | None | 404 missing, 409 existing non-Draft in edit resource |
| Edit | Admin, `Status=Draft`, original RowVersion, Cairo validation | Event + optional cycle archival/new cycle + audit | Previous Event/cycle unchanged on validation, state, concurrency, or persistence failure |

`Events.RowVersion` changes on a successful edit; the client gets the new opaque version. A stale update affects zero Event rows and is rejected. RCSI is the database's read baseline. A single host DbContext transaction preserves cross-table integrity.

## Schema-reconciliation note

The locked table listing predates CHANGE-002's `Location`/`Notes` field decision and the later SLICE-02 initial-cycle timing. Implementing this first feature requires an additive migration and a design-review note recording the discrepancy. No locked architecture choice, existing column, or later lifecycle rule is silently changed.
