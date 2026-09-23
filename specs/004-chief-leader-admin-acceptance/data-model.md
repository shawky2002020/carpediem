# Data Model: SLICE-05 — Chief Leader Admin-Recorded External Acceptance

## Existing `events.ChiefLeaderAssignments`

| Field | Before | After valid external acceptance |
|---|---|---|
| `Status` | `Pending` | `Active` |
| `AcceptedAt` | Null | UTC acceptance-recording instant |
| `AcceptanceSource` | Null | `AdminExternal` |
| `AcceptedByUserId` | Null | Acting Admin ID |
| `DeclinedAt` | Null | Null |
| `RowVersion` | Submitted opaque version | Changes after commit |

Event, candidate, appointment actor, and appointment time do not change. Existing status/source constraints, foreign keys, filtered one-Active-per-Event index, and rowversion remain in force.

## Existing `events.Events`

The related Event must remain `Draft`. Read the Event and current appointment together to reject missing, non-Draft, or mismatched targets. External acceptance changes no Event field.

## Existing `audit.AuditLogs`

First successful external acceptance appends one record:

| Field | Value |
|---|---|
| `ActorUserId` | Acting Admin |
| `Action` | `ChiefLeaderExternalAcceptanceRecorded` |
| `ResourceType` / `ResourceId` | `ChiefLeaderAssignment` / assignment ID |
| `OccurredAt` | Same UTC instant as acceptance |
| `MandatoryReason` | Trimmed Admin justification, 1–1,000 characters |
| `Source` | `AdminExternal` |
| `PreviousValue` / `NewValue` | Bounded Pending-to-Active evidence including Event/candidate identity |
| `CorrelationId` | Current request correlation |

Audit records remain append-only. Justification is not copied into assignment data or notification payloads.

## State and consistency outcomes

| Input | Persisted result |
|---|---|
| Valid Pending external acceptance | Active `AdminExternal` assignment + one matching audit; Event Draft |
| Same Admin/reason replay after uncertain response | Existing Active result; no new audit |
| Blank/whitespace/>1,000 reason | Validation failure; Pending unchanged |
| Active/Declined/non-Draft/mismatched target | No transition |
| Candidate direct response wins race | Direct outcome remains; Admin operation conflicts |
| Admin external acceptance wins race | Active AdminExternal remains; candidate response conflicts |
| Same-Event active team conflict | Pending unchanged |
| Audit/save failure | Whole operation rolls back to Pending |

No database migration is required. The TeamAssignment conflict guard remains a domain seam until the later Staffing schema exists.
