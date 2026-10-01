# Data Model: SLICE-06 — Event Publication & Publishing Prerequisite Invariant

## Overview

SLICE-06 transitions an existing `Draft` Event to `Published` upon verifying the invariant of exactly one `Active` Chief Leader assignment. All necessary database structures already exist in the SQL Server 2025 schema. **No database migration is required.**

## Entity State Changes

### `events.Events`

- **Status Transition**: `Draft` → `Published`
- **Concurrency**: `RowVersion` is incremented by SQL Server upon UPDATE.
- **Constraints Verified**:
  - `CK_Events_Status`: `[Status] IN ('Draft', 'Published', 'Completed', 'Cancelled')`
  - `CK_Events_TimeWindow`: `[EndDateTime] > [StartDateTime]`
  - `CK_Events_StartUtc`: `DATEPART(TZOFFSET, [StartDateTime]) = 0`
  - `CK_Events_EndUtc`: `DATEPART(TZOFFSET, [EndDateTime]) = 0`
  - `CK_Events_TitleNonblank`: `LEN(LTRIM(RTRIM([Title]))) > 0`
  - `CK_Events_LocationNonblank`: `LEN(LTRIM(RTRIM([Location]))) > 0`

### `events.ChiefLeaderAssignments`

- Read-only check during publication:
  - Must find exactly one assignment where `EventId == targetEventId` and `Status == 'Active'`.
  - If zero, or status is `Pending` or `Declined`, publication is rejected.

### `events.StaffingCycles`

- Read-only check during publication:
  - Verifies that the initial staffing cycle (`IsCurrent == 1`) exists for the Event.

### `audit.AuditLogs`

- One row inserted atomically in the same transaction:
  - `Id`: New GUID
  - `OccurredAt`: UTC publication timestamp
  - `ActorUserId`: Acting Operations Admin GUID
  - `Action`: `'EventPublished'`
  - `ResourceType`: `'Event'`
  - `ResourceId`: Target Event GUID
  - `CorrelationId`: Request Correlation GUID
  - `Source`: `'Direct'`
  - `PreviousValue`: `'Draft'`
  - `NewValue`: `'Published'`
  - `MandatoryReason`: NULL

### `notifications.InPlatformNotifications`

- One notification per eligible active Leader:
  - `Id`: New GUID
  - `RecipientUserId`: Leader User GUID
  - `EventId`: Target Event GUID
  - `NotificationType`: `'EventPublished'`
  - `CreatedAt`: UTC publication timestamp
  - `ContextPayload`: JSON containing `{ "eventTitle": "...", "startDateTime": "...", "location": "..." }`

## Transaction Boundary

All database operations execute in a single EF Core execution strategy transaction:
1. Load Event aggregate with `ChiefLeaderAssignments` and `StaffingCycles` under rowversion lock.
2. Invariant guard: verify `Draft` status, valid schedule, exactly one `Active` Chief Leader assignment.
3. Domain method: `event.Publish(adminUserId, publishedAt)`.
4. Stage audit record via `AuditLog`.
5. Stage leader notifications via `InPlatformNotification`.
6. SaveChangesAsync commits Event status update, rowversion increment, audit record, and notification records atomically.
