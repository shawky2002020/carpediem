# Carpediem SQL Server Data Integrity & Isolation Review

> [!IMPORTANT]
> **Status: ARCHITECTURAL BASELINE / STEP 13 INTEGRITY REVIEW**  
> **Database Engine**: Microsoft SQL Server 2025  
> **Precedence**: Authoritative evaluation of SQL Server engine behaviors, isolation levels, index filtered predicates, query execution sanity, and audit transactional guarantees.

---

## 1. SQL Server Isolation Level Evaluation

### 1.1 Default Read Committed vs. Read Committed Snapshot Isolation (RCSI)
Microsoft SQL Server defaults to standard **`READ COMMITTED`** using shared read locks (`S` locks) that are acquired and released as statements read data. We evaluated two primary isolation models:

1. **Option A: Default Pessimistic `READ COMMITTED`**:
   - *Behavior*: Readers acquire `S` locks; writers acquire `X` locks. Readers block writers on the same resource; writers block readers.
   - *Vulnerabilities*: High risk of reader-writer blocking and transient deadlocks (Error 1205) when coordinators view dashboard summaries while roster edits or roll calls are being submitted.
2. **Option B: Read Committed Snapshot Isolation (`READ_COMMITTED_SNAPSHOT ON`) (Chosen)**:
   - *Behavior*: Readers do not acquire shared read locks; instead, they read row-versioned snapshots of committed data from `tempdb`. Writers continue to acquire exclusive locks (`X` locks) and intent-exclusive locks (`IX` locks).
   - *Benefits*:
     - **Materially Reduced Reader/Writer Blocking**: Readers access committed row versions without holding shared locks, eliminating common reader-writer lock contention during operational dashboard rendering, candidate pool views, and mobile schedule queries.
     - **Pessimistic Write Integrity Preserved**: Writers continue to block concurrent writers on the same resource, and explicit locking hints (`UPDLOCK`, `XLOCK`) retain standard pessimistic serialization behaviors.
     - Statement-level consistency: Each statement sees the latest committed data as of the statement's execution time, preventing dirty reads without requiring lock escalation.
     - Low `tempdb` overhead for Carpediem's modest scale (~150 users, 2–20 events/month).

### 1.2 Rejection of Global `SERIALIZABLE` Isolation
* **Analysis**: Enforcing `SERIALIZABLE` globally across all transactions introduces heavy range locks (`Key-Range S-S` and `Key-Range X-X`), severely degrading concurrency, increasing deadlock frequency by orders of magnitude, and providing no architectural benefit over targeted optimistic concurrency (`rowversion`) and filtered unique indexes.
* **Targeted Serialization**: Where strict serial execution is required (such as the CHANGE-001 dual-role exclusivity guard in Race 2), Carpediem applies **targeted update locks** (`UPDLOCK`) on the parent `events.Events` coordination row rather than escalating database-wide isolation. The coordination resource is the parent Event row inside the transaction; optimizer hints like `ROWLOCK` do not substitute for transactional serialization, which relies on all contending paths coordinating against the same root entity and revalidating invariants upon unblocking.

### 1.3 Read-Skew & Phantom Protection
* **Candidate Pool & Roster Queries**: Under RCSI, non-repeatable reads and phantoms are non-critical for read-only queries (a coordinator refreshing a roster expects to see newly committed candidates).
* **Write Invariant Integrity**: For write operations that depend on set-level constraints (e.g., verifying that no active assignment exists before drafting), application command guards execute within an explicit transaction that coordinates against the parent Event row, revalidating domain invariants prior to commit and preventing phantom-induced invariant breaches.

---

## 2. Filtered Unique Index Semantic Validation

SQL Server 2025 provides robust filtered index support. We validated each locked data integrity index against SQL Server storage engine rules:

### 2.1 `UX_StaffingCycles_CurrentPerEvent`
```sql
CREATE UNIQUE NONCLUSTERED INDEX UX_StaffingCycles_CurrentPerEvent
ON events.StaffingCycles (EventId)
WHERE IsCurrent = 1;
```
* **Validation**:
  - `IsCurrent` is a `bit NOT NULL`.
  - When an event is rescheduled, setting `IsCurrent = 0` removes the row from the filtered index, allowing the newly inserted cycle (`IsCurrent = 1`) to occupy the unique slot.
  - Historical cycles (`IsCurrent = 0`) are completely unconstrained, permitting an unlimited history of archived staffing cycles for any event.
  - *Engine Invariant*: Guarantees **at most one** row per `EventId` satisfies `IsCurrent = 1`.

### 2.2 `UX_ChiefLeaderAssignments_ActivePerEvent`
```sql
CREATE UNIQUE NONCLUSTERED INDEX UX_ChiefLeaderAssignments_ActivePerEvent
ON events.ChiefLeaderAssignments (EventId)
WHERE Status = 'Active';
```
* **Validation**:
  - `Status` is `varchar(20) NOT NULL`.
  - Multiple historical `Declined` assignments or unresponded `Pending` rows can coexist for the same event without violating the index.
  - Only when a row transitions to `Status = 'Active'` does the index evaluate uniqueness.
  - *Engine Invariant*: Guarantees **at most one** active Chief Leader assignment per event.

### 2.3 `UX_TeamAssignments_ActiveLeaderPerCycle`
```sql
CREATE UNIQUE NONCLUSTERED INDEX UX_TeamAssignments_ActiveLeaderPerCycle
ON staffing.TeamAssignments (StaffingCycleId, LeaderUserId)
WHERE Status = 'Active';
```
* **Validation**:
  - Allows a volunteer to hold multiple historical `Cancelled` assignments across time or within the same cycle, while strictly prohibiting more than one simultaneous `Active` slot on that cycle's roster.
  - *Engine Invariant*: Guarantees that a volunteer Leader cannot be double-staffed on an active team roster within any single staffing cycle.
  - *Current Operational Context*: While the unique index enforces uniqueness per cycle, an assignment is only *currently operational* if its parent staffing cycle satisfies `IsCurrent = 1`. Historical active assignments in archived cycles (`IsCurrent = 0`) remain immutable historical records and do not restrict participation on rescheduled dates.

### 2.4 SQL Server `SET` Options Compliance
* **Requirement**: Creating and querying tables with filtered indexes requires specific connection-level `SET` options:
  - `QUOTED_IDENTIFIER ON`
  - `ANSI_NULLS ON`
  - `ANSI_PADDING ON`
  - `ANSI_WARNINGS ON`
  - `CONCAT_NULL_YIELDS_NULL ON`
  - `NUMERIC_ROUNDABORT OFF`
* **Verification**: Modern Microsoft.Data.SqlClient and EF Core 10 configure these required `SET` options by default upon establishing connections to SQL Server.

---

## 3. Query & Index Sanity Review

We cross-referenced each proposed index against the query access patterns defined in sequence specifications ([SEQ-001](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-001-event-creation-chief-leader-publication.md) to [SEQ-008](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-008-attendance-finalization-and-correction.md)):

| Query Pattern / Sequence | Table & Key Columns | Target Index | Evaluation & Plan Sanity |
| :--- | :--- | :--- | :--- |
| **Current Staffing Lookup** ([SEQ-001](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-001-event-creation-chief-leader-publication.md), [SEQ-007](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-007-event-reschedule-staffing-cycle.md)) | `events.StaffingCycles (EventId) WHERE IsCurrent = 1` | `UX_StaffingCycles_CurrentPerEvent` | Clustered Index Seek / Unique Index Seek. Zero table scans. |
| **Candidate Pool Rendering** ([SEQ-002](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-002-leader-availability-and-selection.md)) | `staffing.AvailabilityResponses (StaffingCycleId, Status)` | `IX_AvailabilityResponses_CandidatePool` | Index Seek covering `LeaderUserId`, `SubmittedAt`. Immediate response. |
| **Active Team Roster** ([SEQ-002](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-002-leader-availability-and-selection.md), [SEQ-008](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-008-attendance-finalization-and-correction.md)) | `staffing.TeamAssignments (StaffingCycleId) WHERE Status = 'Active'` | `UX_TeamAssignments_ActiveLeaderPerCycle` | Filtered Index Seek covering active roster members. |
| **Chief Leader Event Lookup** ([SEQ-001](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-001-event-creation-chief-leader-publication.md), [SEQ-005](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-005-dual-role-chief-leader-as-leader.md)) | `events.ChiefLeaderAssignments (EventId) WHERE Status = 'Active'` | `UX_ChiefLeaderAssignments_ActivePerEvent` | Unique Filtered Seek. Instantaneous ownership verification. |
| **Unread Notification Polling** (ADR-006) | `notifications.InPlatformNotifications (RecipientUserId) WHERE ReadAt IS NULL` | `IX_Notifications_UnreadPolling` | Filtered Seek on unread alerts. Ignores archived history completely. |
| **Background Completion Scan** (ADR-007) | `events.Events (EndDateTime) WHERE Status = 'Published'` | `IX_Events_Published_EndDateTime` | Filtered Range Scan on active events only. Touches ~2–20 rows max. |
| **Audit Timeline Lookup** (ADR-005) | `audit.AuditLogs (ResourceType, ResourceId, OccurredAt DESC)` | `IX_AuditLogs_Resource_OccurredAt` | Backward Range Scan. Supports paginated administrative audit views. |

* **Redundancy Audit**: Zero redundant or overlapping indexes exist. All non-clustered indexes serve distinct query filters or enforce database-level invariants.

---

## 4. Audit Atomicity & Transactional Integrity

### 4.1 Invariant Formalization
ADR-005 mandates that operational audit logs are written in the exact same transactional boundary as domain state changes. In SQL Server 2025:
- When a domain aggregate executes a state transition (e.g., `SelfCancelAssignment`, `RecordExternalAcceptance`, `CorrectAttendance`), the command handler instantiates an `audit.AuditLog` entity within the same EF Core change tracker context.
- Calling `await dbContext.SaveChangesAsync()` issues both the domain update statement and the audit insert statement inside a single `BEGIN TRANSACTION` block.
- **Rollback Safety**:
  - If a foreign key check, check constraint, or concurrency collision causes the domain update to fail, the entire transaction rolls back; the audit record is not written.
  - If the audit insert fails (e.g., missing mandatory reason intercepted by EF Save Guard), the domain update rolls back.
  - Because domain mutations and mandatory audit records execute within a single atomic database transaction via the shared Unit of Work, partial failure triggers a complete transaction rollback, preventing orphaned audit logs or un-audited state mutations.

---

## 5. Schema Evaluation Post-Concurrency Review

* **Conclusion of Concurrency Review**:
  - The relational schema, table structures, and constraint definitions established in Step 12 **fully satisfy all concurrency and integrity requirements**.
  - No new tables, columns, or structural alterations are required.
  - The combination of targeted `rowversion` concurrency tokens, filtered unique indexes, and the `events.Events` coordination root provides rigorous architectural defense across all evaluated race conditions.
