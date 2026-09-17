# Carpediem Transaction Boundaries & Atomicity Specification

> [!IMPORTANT]
> **Status: ARCHITECTURAL BASELINE / STEP 13 REVIEW**  
> **Database Engine**: Microsoft SQL Server 2025  
> **Transaction Model**: Single Host `CarpediemDbContext` Unit of Work (ADR-009)  
> **Concurrency Engine**: Read Committed Snapshot Isolation (RCSI) + Targeted Optimistic Concurrency (`rowversion`)

---

## 1. Architectural Principles & Isolation Baseline

### 1.1 Single Unit of Work per Request
Carpediem operates as a Modular Monolith where all domain schemas (`identity`, `events`, `staffing`, `attendance`, `leader`, `notifications`, `audit`) reside within a single SQL Server 2025 database. In compliance with ADR-009:
- Every command workflow executes within a **single database transaction** managed by the host `CarpediemDbContext`.
- Distributed transactions (two-phase commit / MSDTC) and asynchronous outbox polling for core business mutations are eliminated.
- If any operation within a command fails (e.g., domain invariant violation, concurrency collision, or missing mandatory audit data), the entire transaction rolls back cleanly, leaving zero partial state.

### 1.2 SQL Server Isolation Level: Read Committed Snapshot Isolation (RCSI)
* **Baseline Configuration**: Carpediem mandates enabling `READ_COMMITTED_SNAPSHOT` on the SQL Server database:
  ```sql
  ALTER DATABASE CarpediemDb SET READ_COMMITTED_SNAPSHOT ON;
  ```
* **Architectural Rationale**:
  - **Materially Reduced Reader/Writer Blocking**: Under RCSI, read queries (e.g., volunteer schedule lookups, candidate pool rendering, operations dashboard summaries) access committed row-versioned snapshots from `tempdb` without holding shared read locks (`S` locks), materially reducing common reader-writer blocking.
  - **Writer-Writer and Explicit Lock Mechanics**: RCSI does not eliminate writer-writer contention. Writers continue to acquire exclusive locks (`X` locks) and intent-exclusive locks (`IX` locks). Schema locks (`Sch-S`, `Sch-M`) and explicit locking hints (`UPDLOCK`, `XLOCK`) continue to exhibit standard pessimistic locking behaviors.
  - **Statement-Level Freshness**: Unlike full Snapshot Isolation (which snapshots at transaction start), RCSI evaluates each statement against committed data as of that statement's start, preventing long-running reads from operating on excessively stale data while maintaining standard Read Committed semantics for writes.

### 1.3 Audit Atomicity Rule (ADR-005 Compliance)
Every audited business command follows a strict atomicity invariant:
$$\text{Business Mutation Committed} \iff \text{Mandatory Audit Log Committed}$$
- The EF Core change tracker enqueues both the domain entity state change and the `audit.AuditLog` record into the same `DbContext` change set.
- An EF Core `SaveChanges` interception guard verifies that reason-mandated actions (e.g., Leader self-cancellation, Admin external acceptance, Admin attendance correction, event cancellation) possess a non-empty `MandatoryReason` before dispatching the SQL transaction.
- If the domain mutation fails, the audit record is aborted and never persists. If the audit insertion fails, the domain mutation rolls back.

---

## 2. Command Workflow Transaction Matrix

The following matrix documents each core business operation, its participating schemas, aggregate boundaries, optimistic concurrency tokens, and audit requirements:

| Command / Workflow | Owning Module | Participating Schemas | Concurrency Token Evaluated | Audit Log Recorded | Mandatory Reason Required |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`CreateDraftEvent`** | `Events` | `events`, `audit` | None (New entity) | `Events.EventCreated` | NO |
| **`AppointChiefLeader`** | `Events` | `events`, `notifications` | None (New entity) | None | NO |
| **`AcceptChiefLeaderDirect`** | `Events` | `events`, `notifications` | `ChiefLeaderAssignments.RowVersion` | None | NO |
| **`RecordExternalAcceptance`**| `Events` | `events`, `audit`, `notifications` | `ChiefLeaderAssignments.RowVersion` | `ChiefLeader.ExternalAcceptanceRecorded`| **YES** |
| **`DeclineChiefLeader`** | `Events` | `events`, `notifications` | `ChiefLeaderAssignments.RowVersion` | None | NO |
| **`PublishEvent`** | `Events` | `events`, `audit`, `notifications` | `Events.RowVersion` | `Events.EventPublished` | NO |
| **`RescheduleEvent`** (Date) | `Events` | `events`, `audit`, `notifications` | `Events.RowVersion` | `Events.EventRescheduled` | NO (Audited) |
| **`CancelEvent`** | `Events` | `events`, `staffing`, `audit`, `notifications` | `Events.RowVersion` | `Events.EventCancelled` | **YES** |
| **`CloseoutEventAdmin`** | `Events` | `events`, `audit` | `Events.RowVersion` | `Events.EventCompleted` | NO |
| **`EventLifecycleScan`** (Worker)| `Events` | `events`, `audit` | `Events.RowVersion` | `Events.EventCompleted` (System) | NO |
| **`DeclareAvailability`** | `Staffing` | `staffing` | None (Single-user toggle) | None | NO |
| **`SelectCandidate`** | `Staffing` | `staffing`, `notifications` | None (New entity / filtered unique index) | None | NO |
| **`DirectDraft`** | `Staffing` | `staffing`, `notifications` | None (New entity / filtered unique index) | None | NO |
| **`SelfCancelAssignment`** | `Staffing` | `staffing`, `audit`, `notifications` | `TeamAssignments.RowVersion` | `Staffing.LeaderSelfCancelled` | **YES** |
| **`AdminStaffingOverride`** | `Staffing` | `staffing`, `audit`, `notifications` | `TeamAssignments.RowVersion` | `Staffing.AdminOverride` | **YES** |
| **`FinalizeAttendance`** | `Attendance` | `attendance`, `notifications` | None (New entity / unique constraint) | None | NO |
| **`CorrectAttendance`** | `Attendance` | `attendance`, `audit` | `AttendanceOutcomes.RowVersion` | `Attendance.OutcomeCorrected` | **YES** |
| **`MarkNotificationRead`** | `Notifications`| `notifications` | None (Idempotent update) | None | NO |

---

## 3. Transaction Execution Lifecycle

Every command pipeline in the ASP.NET Core host executes through a standard transactional Unit of Work behavior:

```
[ Incoming Command / Worker Request ]
                     │
                     ▼
       ┌───────────────────────────┐
       │   ADR-003 AuthZ Check     │ ──► AuthZ Failed: Terminate (Standard Auth Response)
       └─────────────┬─────────────┘
                     │
                     ▼
       ┌───────────────────────────┐
       │ Begin Transaction (RCSI)  │
       └─────────────┬─────────────┘
                     │
                     ▼
       ┌───────────────────────────┐
       │ Load Root Aggregates      │
       │ (Evaluate RowVersion)     │
       └─────────────┬─────────────┘
                     │
                     ▼
       ┌───────────────────────────┐
       │ Execute Domain Logic      │ ──► Invariant Failed: Rollback & Domain Conflict Error
       └─────────────┬─────────────┘
                     │
                     ▼
       ┌───────────────────────────┐
       │ EF Core Save Guard Check  │
       │ - Validate Mandatory Reason│ ──► Missing Reason: Rollback & Invariant Error
       │ - Stage Notification Intent│
       └─────────────┬─────────────┘
                     │
                     ▼
       ┌───────────────────────────┐
       │ SaveChangesAsync()        │
       │ - Insert/Update Domain Rows│
       │ - Insert AuditLog Rows    │ ──► Concurrency Collision / Constraint Violation: 
       │ - Insert Notifications    │     Rollback & Map to Domain Conflict Contract
       └─────────────┬─────────────┘
                     │
                     ▼
       ┌───────────────────────────┐
       │ Commit Transaction        │
       └─────────────┬─────────────┘
                     │
                     ▼
       [ Success Result / Response DTO ]
```

---

## 4. Notification Decoupling vs. Transaction Atomicity

In accordance with ADR-006:
- In-platform notification records (`notifications.InPlatformNotifications`) are staged as relational entities inside the `DbContext` change set during command execution.
- They **commit within the same atomic database transaction** as the underlying domain mutation.
- This guarantees that notification records are never created if a domain transaction fails, and domain mutations never succeed without persisting their corresponding notification intent.
- Delivery to client devices occurs independently via Angular client-side HTTP polling (Page Visibility API + visible-tab timer), ensuring that physical transport failures or sleeping mobile tabs never roll back business transactions.
