# Carpediem Concurrency Model & Adversarial Race Analysis

> [!IMPORTANT]
> **Status: ARCHITECTURAL BASELINE / STEP 13 CONCURRENCY REVIEW**  
> **Database Engine**: Microsoft SQL Server 2025  
> **Precedence**: Establishes the authoritative concurrency control strategy, race condition defenses, deterministic lock hierarchies, and idempotency classifications for Carpediem.

---

## 1. Concurrency Control Architecture Overview

Carpediem manages concurrent operations using a multi-layered defense-in-depth architecture:
1. **Engine-Level Isolation**: Read Committed Snapshot Isolation (RCSI) materially reduces common reader-writer blocking by serving committed row versions from `tempdb` while preserving statement-level read freshness.
2. **Declarative Database Integrity**: Filtered unique indexes enforce single-active constraints at the SQL Server storage engine level, preventing illegal duplicate states regardless of application pre-check timing.
3. **Optimistic Concurrency (`rowversion`)**: Targeted rowversion tokens on mutable aggregates (`Events`, `ChiefLeaderAssignments`, `TeamAssignments`, `AttendanceOutcomes`) detect stale updates and abort conflicting mutations.
4. **Deterministic Coordination Points**: Cross-aggregate invariants (such as CHANGE-001 dual-role mutual exclusivity) serialize contending workflows by acquiring an update lock on the parent `events.Events` coordination row inside the transaction, followed by explicit invariant revalidation.

> [!NOTE]
> **Operational Context Definition**: An assignment row with `Status = 'Active'` in `staffing.TeamAssignments` represents an active assignment within its parent staffing cycle. However, a **Current Operational Team Assignment** strictly requires both conditions:
> $$\text{CurrentOperationalTeamAssignment}(E, U) \iff \text{TeamAssignment.Status} = \text{'Active'} \;\land\; \text{StaffingCycle.IsCurrent} = 1$$
> Historical assignments attached to archived cycles (`IsCurrent = 0`) are immutable historical records and do not restrict a volunteer from participating on a rescheduled date.

---

## 2. Adversarial Analysis: The 10 Critical Race Conditions

### Race 1: Two Chief Leader Activations
* **Scenario**: In normal operation, an event has at most one pending or active Chief Leader appointment. However, if concurrent administrative appointment or acceptance commands race (e.g., candidate direct acceptance races with an administrative action recording external acceptance), both requests could attempt to transition their respective assignment rows to `Status = 'Active'`.
* **Vulnerability Window**: Both requests query `events.ChiefLeaderAssignments`, observe zero `Active` rows, pass application pre-checks, and attempt to update/insert their rows with `Status = 'Active'`.
* **Defense Mechanism**:
  1. **Locked Integrity Index**:
     ```sql
     CREATE UNIQUE NONCLUSTERED INDEX UX_ChiefLeaderAssignments_ActivePerEvent
     ON events.ChiefLeaderAssignments (EventId)
     WHERE Status = 'Active';
     ```
  2. **RowVersion Guard**: Evaluates `ChiefLeaderAssignments.RowVersion` on each update.
* **Execution & Loser Behavior**:
  - Exactly one transaction commits first, transitioning its row to `Active`.
  - The contending transaction attempts to commit `Status = 'Active'` for the same `EventId`. SQL Server detects a unique index collision on `UX_ChiefLeaderAssignments_ActivePerEvent` and immediately aborts the transaction.
  - EF Core maps the violation to a `DbUpdateException`. The application translates this to the standard domain conflict error contract: *"This event already has an active Chief Leader."*
* **Retry Policy**: **NO RETRY**. The business operation has become invalid; the client/coordinator must be informed of the conflict.

---

### Race 2: Chief Leader Activation vs. Team Assignment (CHANGE-001 Dual-Role Contention)
* **Scenario**: Dual-role User $U$ holds both `ChiefLeader` and `Leader` roles.
  - Path A: Candidate $U$ is being activated as Chief Leader for Event $E$.
  - Path B: Candidate $U$ is being assigned to an active team slot on Event $E$'s current staffing cycle.
  - Both commands execute concurrently.
* **Vulnerability Window**:
  - Path A checks for active team assignments for User $U$ on Event $E$ (finds 0).
  - Path B checks if User $U$ is the active Chief Leader for Event $E$ (finds false).
  - Both commands pass application pre-checks and commit concurrently, violating the ratified invariant:
    $$\text{ActiveChiefLeaderAssignment}(E, U) \implies \neg\text{CurrentOperationalTeamAssignment}(E, U)$$
* **Applicable Command Paths**: This mutual exclusivity invariant applies across all command workflows capable of making either side operational:
  1. `AcceptChiefLeaderDirect`
  2. `RecordExternalAcceptance`
  3. `SelectCandidate`
  4. `DirectDraft`
  5. `DraftReplacement`
* **Evaluation of Defense Mechanisms**:
  - *Database Triggers (Rejected)*: Obscure, difficult to test, and risk cross-schema blocking.
  - *Shared Lease Table (Rejected)*: Adds unnecessary schema overhead and maintenance burden.
  - *Chosen Strategy: Deterministic Coordination via Event Root Lock & Invariant Revalidation*:
    - Any workflow that mutates operational leadership or team membership for Event $E$ acquires an intentional update lock on the parent `events.Events` coordination row inside the transaction:
      ```csharp
      // EF Core LINQ / T-SQL coordination lock inside the transaction:
      await dbContext.Events
          .FromSqlInterpolated($"SELECT * FROM events.Events WITH (UPDLOCK) WHERE Id = {eventId}")
          .SingleAsync();
      ```
    - **Locking Reality**: The coordination resource is the parent `events.Events` row. `UPDLOCK` ensures mutual exclusion between competing update paths. (The engine hint `ROWLOCK` is an optimizer suggestion that does not guarantee row granularity under lock escalation; correctness is guaranteed because all conflicting paths serialize against the same root entity within their transaction).
    - The first transaction acquires the update lock on the Event row.
    - The contending transaction blocks until the first transaction completes.
    - **Invariant Revalidation**: When the second transaction unblocks, it re-queries domain state under RCSI:
      - If activating Chief Leader: verifies User $U$ has no current operational team assignment on $E$ (`StaffingCycle.IsCurrent == 1 AND TeamAssignment.Status == 'Active'`).
      - If activating Team Assignment: verifies User $U$ is not the active Chief Leader on $E$ (`ChiefLeaderAssignment.Status == 'Active'`).
      - If the invariant is breached, the second transaction rolls back and returns the standard domain conflict contract.
* **Deadlock Mitigation**: Establishing `events.Events` as the consistent initial coordination resource across leadership and staffing workflows creates a predictable access ordering ($E \rightarrow \text{Assignments}$), materially reducing cyclic lock waits.

---

### Race 3: Concurrent Duplicate Team Assignments
* **Scenario**: Two coordinators (or Chief Leader + Admin) simultaneously attempt to select the same volunteer Leader $U$ for the same current `StaffingCycle`.
* **Vulnerability Window**: Both requests query `staffing.TeamAssignments`, observe zero active assignments for Leader $U$, and attempt concurrent inserts.
* **Defense Mechanism**:
  - **Locked Integrity Index**:
    ```sql
    CREATE UNIQUE NONCLUSTERED INDEX UX_TeamAssignments_ActiveLeaderPerCycle
    ON staffing.TeamAssignments (StaffingCycleId, LeaderUserId)
    WHERE Status = 'Active';
    ```
* **Execution & Loser Behavior**:
  - The first insert succeeds and commits.
  - The second insert attempts to write a duplicate `(StaffingCycleId, LeaderUserId)` where `Status = 'Active'`. SQL Server detects a unique key violation.
  - EF Core translates this to a `DbUpdateException`, which maps to the application standard domain conflict error contract: *"Volunteer is already actively assigned to this roster cycle."*
* **Retry Policy**: **NO RETRY**.

---

### Race 4: Event Date Reschedule vs. Volunteer Availability Submission
* **Scenario**:
  - Request A: Volunteer Leader submits availability against the current `StaffingCycle`.
  - Request B: Operations Admin executes a material date reschedule, archiving the current `StaffingCycle` (`IsCurrent = false`) and creating a new cycle (`IsCurrent = true`).
* **Vulnerability Window**: Request A evaluates `StaffingCycle.IsCurrent == true` before the reschedule, but commits after the new cycle is created.
* **Defense Mechanism**:
  1. **Target Cycle Resolution**: The availability command explicitly carries and resolves the target `StaffingCycleId`.
  2. **Cycle Currency Revalidation**: Before committing, the transaction verifies that the target `StaffingCycleId` belongs to Event $E$ and remains current (`IsCurrent == true`).
  3. **Stale Context Rejection**: If the reschedule committed first, the target cycle is now archived (`IsCurrent == false`). The transaction rejects the stale availability submission as a context conflict rather than silently writing to an archived cycle or copying responses to the new date.
  4. **Scoped Foreign Key**: `staffing.AvailabilityResponses` maintains a foreign key to `events.StaffingCycles(Id)`. Responses attached to archived cycles are naturally excluded from operational candidate queries for the new date:
     $$\text{CurrentOperationalAvailability} = \text{AvailabilityResponse} \bowtie \text{StaffingCycle}_{\text{IsCurrent}=1}$$
* **Outcome**:
  - The user interface receives a conflict notification indicating the event date has changed, prompting the volunteer to submit availability for the rescheduled date. Stale submissions never contaminate the new staffing context.

---

### Race 5: Event Date Reschedule vs. Team Member Selection
* **Scenario**: Chief Leader drafts Leader $U$ into the roster while an Admin simultaneously reschedules the event date.
* **Vulnerability Window**: Chief Leader assigns Leader $U$ to `StaffingCycle_Old` right as Admin transitions the event to `StaffingCycle_New`.
* **Defense Mechanism**:
  1. **Deterministic Coordination on Event Root**: Both `RescheduleEventCommand` and `SelectCandidateCommand` acquire an update lock on the parent `events.Events` coordination row.
  2. **Cycle Validation Guard**: If `RescheduleEvent` commits first:
     - When `SelectCandidate` unblocks, it re-verifies that the target `StaffingCycleId` has `IsCurrent == true`.
     - Detecting `IsCurrent == false`, it immediately aborts with the standard domain conflict contract: *"Event was rescheduled while editing roster. Please refresh."*
  3. **Timing Invariant**: If `SelectCandidate` commits first, its assignment attaches to `StaffingCycle_Old`. When `RescheduleEvent` executes immediately following, `StaffingCycle_Old` is archived (`IsCurrent = false`), and the assignment remains preserved as an immutable historical record without becoming active or operational on the new date.

---

### Race 6: Volunteer Self-Cancellation vs. Administrative Staffing Override
* **Scenario**: A volunteer Leader submits self-cancellation with mandatory reason at the exact instant an Admin or Chief Leader attempts to remove or reassign the volunteer.
* **Vulnerability Window**: Both requests attempt to update the same `staffing.TeamAssignments` row.
* **Defense Mechanism**:
  1. **Optimistic Concurrency (`rowversion`)**: `staffing.TeamAssignments` includes `RowVersion rowversion NOT NULL`.
  2. **Guarded State Transition**: Update evaluates `WHERE Id = @id AND Status = 'Active' AND RowVersion = @rowVersion`.
* **Execution & Loser Behavior**:
  - Winning transaction updates `Status = 'Cancelled'`, updates `RowVersion`, and writes the corresponding audit log.
  - Losing transaction detects that `RowVersion` has changed. EF Core throws `DbUpdateConcurrencyException`.
  - Application catches the exception, re-evaluates current row state, and returns the standard domain conflict contract: *"Assignment state has changed. Please refresh."*
  - The audit trail strictly records only the winning transition; no duplicate or orphan cancellation audits are logged.

---

### Race 7: Replacement Contention
* **Scenario**: A volunteer self-cancels, reopening an operational slot. Two coordinators simultaneously attempt to draft replacements.
* **Vulnerability Window**: Both coordinators attempt to assign candidates to fill the vacancy.
* **Defense Mechanism**:
  - **Same Candidate Contention**: Filtered unique index `UX_TeamAssignments_ActiveLeaderPerCycle` guarantees that the *same* Leader cannot hold two active assignments in the same cycle. The second attempt is rejected by the database uniqueness constraint.
  - **Different Candidates Contention**: The unique index guarantees single-active status per leader, but does *not* restrict different leaders from being drafted into separate assignment rows. Exact staffing capacity limits, slot quotas, and replacement slot allocation rules remain **feature-level product design** (deferred to feature specifications). At the architecture baseline, drafting different candidates produces distinct valid rows. If capacity limits are ratified in future feature specifications, they will be enforced within the Event root serialization boundary.

---

### Race 8: Attendance Finalization vs. Administrative Attendance Correction
* **Scenario**: The assigned Chief Leader submits roll-call attendance on-site at the exact instant an Operations Admin attempts to record an administrative attendance correction.
* **Vulnerability Window**: Simultaneous insert or update operations on `attendance.AttendanceOutcomes`.
* **Defense Mechanism**:
  1. **First Attendance Insertion** (`FinalizeAttendance`): Guarded by the database UNIQUE constraint on `TeamAssignmentId` (`UX_AttendanceOutcomes_Assignment`). If concurrent initial submissions occur, exactly one insert commits; the contending insert collides on the unique index, aborts, and returns a domain conflict error.
  2. **Concurrent Updates / Correction** (`CorrectAttendance` vs. concurrent edit): Guarded by `AttendanceOutcomes.RowVersion`. The losing update detects a concurrency token mismatch, throws `DbUpdateConcurrencyException`, and rejects the stale write with the standard domain conflict contract.

---

### Race 9: Background Completion Worker vs. Administrative Closeout
* **Scenario**: The automated `EventLifecycleBackgroundWorker` discovers an event whose `EndDateTime` has passed and attempts to transition `Published` $\rightarrow$ `Completed`. Simultaneously, an Operations Admin triggers administrative closeout in the web portal.
* **Vulnerability Window**: Both processes attempt to update `events.Events.Status = 'Completed'`.
* **Defense Mechanism**:
  1. **Guarded State Transition**: The update checks that `Status == 'Published'` and verifies `Events.RowVersion`.
  2. **RowVersion Guard**: `events.Events.RowVersion` increments on mutation.
* **Execution & Loser Behavior**:
  - Exactly one execution successfully updates the row from `Published` to `Completed` and commits its corresponding audit log (`Events.EventCompleted`).
  - The contending execution observes that 0 rows were updated (or catches a concurrency token collision).
  - The loser detects that `Status` is already `Completed` and **safely no-ops**. No error is raised, and no duplicate completion audit record is written.

---

### Race 10: Event Schedule Edit vs. Background Completion Worker
* **Scenario**: An Admin extends an event's schedule (e.g., postponing `EndDateTime` from 18:00 to 21:00). Meanwhile, the background completion worker evaluates the old `EndDateTime` (18:00) and attempts to mark the event `Completed`.
* **Vulnerability Window**: Worker loads event snapshot, detects `Now > OldEndDateTime`, and prepares completion while Admin's schedule edit is in-flight.
* **Defense Mechanism**:
  - Admin's schedule update modifies `events.Events`, which increments `Events.RowVersion`.
  - When the background worker attempts its guarded completion update using the old `RowVersion`, EF Core detects the concurrency token mismatch.
  - The worker aborts the update, catches `DbUpdateConcurrencyException`, and drops the transition.
  - On the worker's next periodic scan, it inspects the newly extended `EndDateTime` (21:00) and correctly observes that the event has not yet concluded.

---

## 3. Deadlock Review & Deterministic Lock Hierarchy

To minimize the likelihood of concurrent transactions entering cyclic lock waits (SQL Server Error 1205 Deadlocks), Carpediem establishes a **consistent resource-acquisition ordering**:

### 3.1 Global Acquisition Ordering
Consistent application acquisition order materially reduces deadlocks across transactional workflows. While it does not prove deadlocks can never occur in complex multi-object relational engines under unexpected query patterns, it establishes a predictable lock hierarchy across command pipelines.

Transactions are NOT required to touch every entity. However, any transaction that accesses multiple entities must acquire or coordinate access adhering to this relative hierarchy:

$$\mathbf{1.\; Events} \longrightarrow \mathbf{2.\; StaffingCycles} \longrightarrow \mathbf{3.\; ChiefLeaderAssignments} \longrightarrow \mathbf{4.\; TeamAssignments} \longrightarrow \mathbf{5.\; AttendanceOutcomes} \longrightarrow \mathbf{6.\; AuditLogs}$$

```
Level 1: events.Events (Parent Aggregate / Coordination Root)
   │
   ▼
Level 2: events.StaffingCycles (Revision Scope)
   │
   ▼
Level 3: events.ChiefLeaderAssignments & staffing.AvailabilityResponses
   │
   ▼
Level 4: staffing.TeamAssignments
   │
   ▼
Level 5: attendance.AttendanceOutcomes
   │
   ▼
Level 6: audit.AuditLogs (Append-Only Audit Trail)
```

> [!NOTE]
> **Notification Independence**: `notifications.InPlatformNotifications` records are append-only inserts targeted to individual recipient inboxes. Because they insert new rows without acquiring locks on shared domain aggregates, they are excluded from the core domain lock hierarchy unless explicitly persisted synchronously within a coordinated command boundary.

### 3.2 Deadlock Retry Policy
* **Transient Error Strategy**: The ASP.NET Core host configures a bounded retry policy for operations safe to replay:
  - Catches transient SQL Server Error 1205 (Deadlock Victim) and transient connection timeouts.
  - Executes a **bounded number of retries** with **jittered exponential backoff**.
  - **Clean Transaction Restart**: On retry, the transaction restarts completely from the beginning: reloading root entities, evaluating fresh `RowVersion` tokens under RCSI, and revalidating domain invariant guards.
  - **Non-Retryable Errors**: Business invariant violations (filtered unique index collisions, foreign key violations) and unrecoverable optimistic concurrency collisions (`DbUpdateConcurrencyException` on stale user edits) are **non-retryable** and bubble up immediately as domain conflict errors.
  - **No Duplicate Stacking**: Resilience policies must not stack duplicate retry mechanisms across both EF Core execution strategies and outer middleware pipelines.

---

## 4. Idempotency Review & Classification Matrix

Every business operation in Carpediem is classified into one of four explicit architectural idempotency categories:

| Business Operation | Idempotency Classification | Primary Enforcement Mechanism | Replay / Contention Behavior |
| :--- | :--- | :--- | :--- |
| **`CreateDraftEvent`** | **Idempotency-Key Candidate** | Unique identifier / Client request token | Network retry with identical idempotency token returns existing draft DTO. |
| **`AppointChiefLeader`** | **Duplicate-Rejected** | Filtered Unique Index / Status Guard | Concurrent duplicate appointment detects existing assignment; returns domain conflict error. |
| **`AcceptChiefLeaderDirect`** | **Duplicate-Rejected** | `ChiefLeaderAssignments.RowVersion` + Status Guard | First call activates; replay detects `Status != Pending` and returns domain conflict error. |
| **`RecordExternalAcceptance`** | **Duplicate-Rejected** | `ChiefLeaderAssignments.RowVersion` + Status Guard | First call activates & logs audit; replay safely rejected via status guard. |
| **`DeclineChiefLeader`** | **Duplicate-Rejected** | `ChiefLeaderAssignments.RowVersion` + Status Guard | First call declines; replay detects `Status != Pending` and returns domain conflict error. |
| **`PublishEvent`** | **Response-Idempotent** | Guarded `Status == 'Draft'` Precondition | If already `Published`, safely returns current event DTO without re-executing transitions. |
| **`RescheduleEvent`** (Date) | **Duplicate-Rejected** | `Events.RowVersion` + Cycle Creation Guard | Stale reschedule request aborts via concurrency token collision. |
| **`CancelEvent`** | **Duplicate-Rejected** | `Events.RowVersion` + Guarded Transition | First call cancels and audits; duplicate call rejected via `Status != Published` guard. |
| **`CloseoutEventAdmin`** | **State-Idempotent** | Guarded SQL `WHERE Status = 'Published'` | Multiple executions transition event to `Completed`; subsequent calls modify 0 rows and safely no-op. |
| **`EventLifecycleScan`** (Worker)| **State-Idempotent** | Guarded SQL Scan on expired events | Periodic scans discover expired events; completed events are skipped with zero side effects. |
| **`DeclareAvailability`** | **State-Idempotent** | Direct Upsert / Toggle on Cycle | Repeated submissions of same availability status result in identical persistent state. |
| **`SelectCandidate`** | **Duplicate-Rejected** | Filtered Unique Index `UX_TeamAssignments_ActiveLeaderPerCycle` | Duplicate draft request collides on unique index; returns domain conflict error. |
| **`DirectDraft`** | **Duplicate-Rejected** | Filtered Unique Index `UX_TeamAssignments_ActiveLeaderPerCycle` | Duplicate draft request collides on unique index; returns domain conflict error. |
| **`SelfCancelAssignment`** | **Duplicate-Rejected** | `TeamAssignments.RowVersion` + `Status == 'Active'` Guard | First call cancels; duplicate call detects `Status != Active` and returns domain conflict error. |
| **`AdminStaffingOverride`** | **Duplicate-Rejected** | `TeamAssignments.RowVersion` + Status Guard | Replay detects modified state; returns domain conflict error. |
| **`FinalizeAttendance`** | **Duplicate-Rejected** | Unique Constraint `UX_AttendanceOutcomes_Assignment` | First call records outcome; duplicate call collides on unique constraint and is rejected. |
| **`CorrectAttendance`** | **Duplicate-Rejected** | `AttendanceOutcomes.RowVersion` + Guard | Subsequent correction modifies outcome and appends mandatory audit entry. |
| **`MarkNotificationRead`** | **State-Idempotent** | SQL `WHERE ReadAt IS NULL` Update | Subsequent calls evaluate `ReadAt IS NOT NULL`, modify 0 rows, and succeed cleanly. |
