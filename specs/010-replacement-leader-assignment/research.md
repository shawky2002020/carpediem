# Research & Architecture Analysis: SLICE-11 — Replacement Leader Assignment

**Feature Branch**: `codex/010-replacement-leader-assignment`  
**Date**: 2026-10-02  
**Status**: Completed  

---

## 1. Domain Modeling & Historical Immutability (ADR-005 & Decision Group 4)

In event volunteer staffing, replacing a dropped team member must never erase or mutate the dropped member's cancellation history:
- If we updated the old `TeamAssignment` with the new leader's `UserId`, the previous volunteer's cancellation record, cancellation timestamp, and cancellation justification would be lost or corrupted.
- Therefore, per **Decision Group 4** and `docs/requirements/LIFECYCLE_SPECIFICATION.md`:
  - The previous assignment remains immutably in `Status = 'Cancelled'` with its `CancelledAt` and `CancellationReason`.
  - A **brand-new** `TeamAssignment` entity is instantiated with:
    - `Id`: New Guid
    - `EventId`: Target event ID
    - `StaffingCycleId`: Current active staffing cycle ID
    - `UserId`: Replacement candidate user ID
    - `Status`: `'Active'`
    - `AssignedAt`: `UtcNow`
    - `AssignedByUserId`: Acting Chief Leader or Administrator user ID
    - `CancellationReason`: `null`
    - `CancelledAt`: `null`

---

## 2. Concurrency & Filtered Unique Index Alignment

SQL Server table `staffing.TeamAssignments` possesses a unique filtered index:
```sql
CREATE UNIQUE INDEX [UX_TeamAssignments_ActiveLeaderPerCycle]
ON [staffing].[TeamAssignments] ([StaffingCycleId], [UserId])
WHERE [Status] = 'Active';
```

- When an assignment was cancelled in SLICE-10, its status changed to `'Cancelled'`. Because of the `WHERE [Status] = 'Active'` predicate, the cancelled row dropped out of the index.
- When drafting a replacement:
  - If candidate $C$ is already active in the cycle, the application layer detects it and returns a safe **idempotent replay** (`200 OK` with `Idempotent-Replay: true`).
  - If a concurrent race condition occurs where two requests attempt to draft candidate $C$ simultaneously, the unique filtered index guarantees that exactly one transaction succeeds, while the other receives a unique constraint violation that maps to a concurrency error.

---

## 3. Authorization & Resource Policy (ADR-003)

- **Requirement**: Chief Leader for the assigned event, or Operations Administrator.
- **Route Policy**: `AuthorizationPolicies.RequireChiefLeaderOrAdmin` applies at endpoint group level.
- **Resource Verification**:
  ```csharp
  var chiefAssignment = await dbContext.ChiefLeaderAssignments
      .AsNoTracking()
      .FirstOrDefaultAsync(c => c.EventId == eventId && c.Status == "Active", cancellationToken);

  if (!isAdmin && (chiefAssignment == null || chiefAssignment.ChiefLeaderUserId != actorUserId))
  {
      return 403 Forbidden ("unauthorized_chief_leader");
  }
  ```

---

## 4. Notification & Audit Log Specifications

1. **In-Platform Notification**:
   - `NotificationType`: `ReplacementLeaderAssigned`
   - `RecipientUserId`: Candidate user ID
   - `EventId`: Event ID
   - `ContextPayload`: JSON containing `{ eventId, eventTitle, assignedAt, assignedByUserName }`
2. **Audit Trail**:
   - `Action`: `ReplacementLeaderAssigned`
   - `ActorUserId`: Acting Chief Leader or Admin
   - `ResourceId`: Newly created `TeamAssignment.Id`
   - `ResourceType`: `"TeamAssignment"`
   - `MandatoryReason`: `"Drafted as replacement leader"` or user-provided justification
   - `NewValue`: JSON snapshot of new assignment
   - `PreviousValue`: JSON snapshot of replaced cancelled assignment (if provided) or `null`
   - `Source`: `"ChiefLeaderReplacement"` or `"AdminOverrideReplacement"`

---

## 5. UI/UX Accessibility Strategy (WCAG 2.2 AA)

- **Mobile First**: Single-column vertical layout down to 320px with zero horizontal scroll at 200% zoom.
- **Visual Clarity**: Dedicated "Staffing Gaps" section highlighting cancelled slots with cancellation reasons, paired with an "Available Replacements" selection table.
- **Touch Target Minimums**: 44px min height and width on all action buttons.
- **Polite Live Regions**: `aria-live="polite"` announcing successful replacement placement immediately to assistive technology.
