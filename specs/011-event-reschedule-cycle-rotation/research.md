# Technical Research: SLICE-12 — Event Reschedule & Staffing Cycle Rotation

**Feature Branch**: `codex/011-event-reschedule-cycle-rotation`  
**Date**: 2026-10-02  

---

## 1. Cycle Rotation Mechanics & Historical Preservation

### Why Staffing Cycles Matter
In the Carpediem architecture, volunteer commitments are tightly bound to specific calendar dates and times. If an event scheduled for Saturday morning is rescheduled to Sunday afternoon, volunteer availability declarations and assignments for Saturday morning are materially invalid for Sunday afternoon.

Rather than deleting prior availability declarations (`staffing.AvailabilityResponses`) and team assignments (`staffing.TeamAssignments`), Carpediem binds these records to a specific `StaffingCycleId`.
- When an event is initially created, cycle 1 is instantiated (`CycleNumber = 1`, `IsCurrent = 1`).
- When an event is rescheduled, cycle 1 is sealed (`IsCurrent = 0`) and cycle 2 is created (`CycleNumber = 2`, `IsCurrent = 1`).
- Historical data remains completely untouched and immutably tied to cycle 1 for audit, historical reporting, and attendance investigation.
- All operational queries for availability and team selection filter by `c.EventId == eventId && c.IsCurrent`. Consequently, the new cycle immediately presents as an empty roster ready for fresh availability declarations and team selection.

---

## 2. SQL Server Filtered Unique Index Concurrency

### The Constraint
In `StaffingCycleConfiguration.cs`:
```csharp
builder.HasIndex(cycle => cycle.EventId)
    .IsUnique()
    .HasFilter("[IsCurrent] = 1")
    .HasDatabaseName("UX_StaffingCycles_CurrentPerEvent");
```
This filtered unique index guarantees that at most ONE staffing cycle per event has `IsCurrent = 1` in SQL Server at any given moment.

### Potential EF Core Ordering Hazard
If EF Core batches an `INSERT` of the new cycle (`IsCurrent = 1`) before the `UPDATE` of the old cycle (`IsCurrent = 0`), SQL Server will reject the batch with:
```
Cannot insert duplicate key row in object 'events.StaffingCycles' with unique index 'UX_StaffingCycles_CurrentPerEvent'.
```

### Engineered Solution
To eliminate any statement ordering ambiguity:
```csharp
await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

// 1. Seal old cycle first
currentCycle.MarkHistorical();
await db.SaveChangesAsync(cancellationToken); // Emits UPDATE StaffingCycles SET IsCurrent = 0 WHERE Id = ...

// 2. Add new cycle, update event, notifications, audit log
db.StaffingCycles.Add(newCycle);
eventItem.UpdateSchedule(newStartDateTime, newEndDateTime);
// stage notifications and audit log...
await db.SaveChangesAsync(cancellationToken); // Emits INSERT StaffingCycles (IsCurrent = 1) and other writes

await transaction.CommitAsync(cancellationToken);
```
Because both `SaveChangesAsync` calls execute inside the same SQL transaction, the operation is completely atomic and guarantees the filtered unique index is never violated.

---

## 3. In-Platform Notification Strategy & Deduplication

### Stakeholders Alerted
When an event is rescheduled, the following participants must be informed:
1. **The Active Chief Leader**: Needs to know the event date changed and that team selection must occur again for the new date.
2. **Previously Active Team Members**: Need to know their prior assignment has been archived due to the date change.
3. **Previously Declared Candidates**: Need to know the date changed so they can submit their availability for the new operational window.

### Deduplication
A user might be in multiple categories (e.g., both a declared candidate and an assigned leader). The store collects:
```csharp
var recipientIds = activeTeamUserIds
    .Concat(activeChiefLeaderUserIds)
    .Concat(candidateUserIds)
    .Distinct()
    .ToList();
```
Each unique user receives exactly one `EventRescheduled` notification with context containing the event title, old date/time, new date/time, and the administrative justification.

---

## 4. Optimistic Concurrency via `If-Match`

The API requires an `If-Match` header containing the event's current base64 `RowVersion`.
- If missing or malformed: returns 400 Bad Request (`malformed_request` / `malformed_version`).
- If mismatched: EF Core or rowversion check detects a concurrency violation, returning 412 Precondition Failed (`concurrency_conflict`).

---

## 5. UI/UX Accessibility & Reflow (WCAG 2.2 AA)

1. **Reflow & Responsive Design**:
   - The reschedule dialog and form fields must adapt cleanly from 320px width up to 4K desktops without horizontal scrollbars.
   - Flex containers must use `flex-wrap: wrap` and `min-width: 0` to maintain integrity under 200% font zoom.
2. **Touch Targets**:
   - All interactive controls (buttons, inputs) maintain at least a 44x44px target area.
3. **Focus & Keyboard Navigation**:
   - Focus is trapped within the reschedule modal when opened and restored on dismissal.
   - Escape key dismisses the dialog.
4. **Live Announcements**:
   - A dedicated `aria-live="polite"` region alerts screen readers when a reschedule completes or fails.
5. **SCSS Budget**:
   - All styles must be encapsulated within class selectors and keep compiled CSS under 4.00 kB.
