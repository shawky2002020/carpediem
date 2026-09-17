# Carpediem Index Candidates & Query Performance Strategy

> [!IMPORTANT]
> **Status: ARCHITECTURAL BASELINE / PENDING PHYSICAL IMPLEMENTATION BENCHMARKING**  
> **Database Engine**: Microsoft SQL Server 2025  
> **Operational Scale**: ~100–150 volunteer Leaders, ~10–15 Chief Leaders, ~3 Operations Administrators, 2–20 Events per month.  
> **Classification Framework**:
> - **LOCKED DATA INTEGRITY INDEXES**: Required to enforce database-level invariants (e.g., filtered unique indexes for current cycle and active leadership).
> - **QUERY-PERFORMANCE CANDIDATES**: Recommended access path optimizations evaluated against execution plans during implementation.

---

## 1. Indexing Strategy & Principles

At Carpediem's operational scale, the entire active database working set can reside comfortably in SQL Server memory (buffer pool). Index design is governed by three specific rules:
1. **Integrity-First**: Use SQL Server **filtered unique indexes** to guarantee business rules that declarative constraints cannot enforce alone (e.g., at most one current staffing cycle per event, at most one active Chief Leader per event, at most one active roster placement per volunteer per cycle).
2. **Query Pattern Alignment**: Support the core query paths identified across verified sequence diagrams ([SEQ-001](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-001-event-creation-chief-leader-publication.md) to [SEQ-008](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-008-attendance-finalization-and-correction.md)).
3. **Write-Cost Tradeoff**: Prevent write amplification by avoiding redundant indexes on low-volume operational tables.

---

## 2. Locked Data Integrity Indexes

The following indexes are **mandatory architectural integrity mechanisms** required to enforce domain lifecycle invariants at the database engine level:

### 2.1 `events.StaffingCycles`: Single Current Cycle per Event
* **Index Name**: `UX_StaffingCycles_CurrentPerEvent`
* **Purpose**: Enforces that an Event has **at most one** currently active staffing window (`IsCurrent = 1`).
* **Definition**:
  ```sql
  CREATE UNIQUE NONCLUSTERED INDEX UX_StaffingCycles_CurrentPerEvent
  ON events.StaffingCycles (EventId)
  WHERE IsCurrent = 1;
  ```
* **Status**: **LOCKED DATA INTEGRITY INDEX**.

### 2.2 `events.ChiefLeaderAssignments`: Single Active Chief Leader per Event
* **Index Name**: `UX_ChiefLeaderAssignments_ActivePerEvent`
* **Purpose**: Enforces that an Event can have **at most one** `Active` Chief Leader at any given time.
* **Definition**:
  ```sql
  CREATE UNIQUE NONCLUSTERED INDEX UX_ChiefLeaderAssignments_ActivePerEvent
  ON events.ChiefLeaderAssignments (EventId)
  INCLUDE (ChiefLeaderUserId, AppointedAt, AcceptanceSource)
  WHERE Status = 'Active';
  ```
* **Status**: **LOCKED DATA INTEGRITY INDEX**.

### 2.3 `staffing.TeamAssignments`: Single Active Placement per Volunteer per Cycle
* **Index Name**: `UX_TeamAssignments_ActiveLeaderPerCycle`
* **Purpose**: Enforces that a volunteer Leader can hold **at most one** `Active` team slot within a given staffing cycle.
* **Definition**:
  ```sql
  CREATE UNIQUE NONCLUSTERED INDEX UX_TeamAssignments_ActiveLeaderPerCycle
  ON staffing.TeamAssignments (StaffingCycleId, LeaderUserId)
  INCLUDE (AssignedAt, AssignedByUserId)
  WHERE Status = 'Active';
  ```
* **Status**: **LOCKED DATA INTEGRITY INDEX**.

### 2.4 `staffing.AvailabilityResponses`: Unique Declaration per Volunteer per Cycle
* **Index Name**: `UX_AvailabilityResponses_Cycle_Leader`
* **Purpose**: Enforces that a volunteer Leader has at most one availability record per staffing cycle.
* **Definition**: Unique Constraint on `(StaffingCycleId, LeaderUserId)`.
* **Status**: **LOCKED DATA INTEGRITY INDEX**.

### 2.5 `attendance.AttendanceOutcomes`: 1:1 Outcome per Team Assignment
* **Index Name**: `UX_AttendanceOutcomes_Assignment`
* **Purpose**: Enforces that a team assignment receives at most one finalized attendance record.
* **Definition**: Unique Constraint on `(TeamAssignmentId)`.
* **Status**: **LOCKED DATA INTEGRITY INDEX**.

---

## 3. Query-Performance Candidates

The following indexes are **performance candidates** designed to accelerate specific application workflows. They will be validated against SQL Server execution plans during physical development:

### 3.1 `events.Events`: Background Completion Worker Scan
* **Candidate Name**: `IX_Events_Published_EndDateTime`
* **Target Query**: `EventLifecycleBackgroundWorker` discovering events where `Status = 'Published'` and `CurrentTime > EndDateTime` (ADR-007).
* **Definition**:
  ```sql
  CREATE NONCLUSTERED INDEX IX_Events_Published_EndDateTime
  ON events.Events (EndDateTime)
  INCLUDE (Title, StartDateTime)
  WHERE Status = 'Published';
  ```
* **Tradeoff Analysis**: The filtered predicate indexes only the ~2–20 active events, completely ignoring historical `Completed` or `Cancelled` records. Write cost on finished events is zero.
* **Status**: **QUERY-PERFORMANCE CANDIDATE**.

### 3.2 `staffing.AvailabilityResponses`: Candidate Pool Query
* **Candidate Name**: `IX_AvailabilityResponses_CandidatePool`
* **Target Query**: Candidate pool rendering for Chief Leader selection: querying available volunteers for the current cycle (`WHERE StaffingCycleId = @CycleId AND Status = 'Available'`) ([SEQ-002](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-002-leader-availability-and-selection.md)).
* **Key Columns**: `StaffingCycleId` ASC, `Status` ASC
* **Included Columns**: `LeaderUserId`, `SubmittedAt`, `UpdatedAt`
* **Status**: **QUERY-PERFORMANCE CANDIDATE**.

### 3.3 `events.ChiefLeaderAssignments`: User Assignment Lookup
* **Candidate Name**: `IX_ChiefLeaderAssignments_User_Status`
* **Target Query**: Chief Leader dashboard fetching active/pending invitations for a user (`WHERE ChiefLeaderUserId = @UserId AND Status IN ('Pending', 'Active')`).
* **Key Columns**: `ChiefLeaderUserId` ASC, `Status` ASC
* **Included Columns**: `EventId`, `AppointedAt`, `AcceptanceSource`
* **Status**: **QUERY-PERFORMANCE CANDIDATE**.

### 3.4 `staffing.TeamAssignments`: Volunteer Schedule Query
* **Candidate Name**: `IX_TeamAssignments_Leader_Status`
* **Target Query**: Leader mobile home screen displaying upcoming assigned shifts (`WHERE LeaderUserId = @UserId AND Status = 'Active'`).
* **Key Columns**: `LeaderUserId` ASC, `Status` ASC
* **Included Columns**: `StaffingCycleId`, `AssignedAt`
* **Status**: **QUERY-PERFORMANCE CANDIDATE**.

### 3.5 `notifications.InPlatformNotifications`: Unread Polling Query
* **Candidate Name**: `IX_Notifications_UnreadPolling`
* **Target Query**: Angular client polling for unread notifications (`WHERE RecipientUserId = @UserId AND ReadAt IS NULL`) (ADR-006).
* **Definition**:
  ```sql
  CREATE NONCLUSTERED INDEX IX_Notifications_UnreadPolling
  ON notifications.InPlatformNotifications (RecipientUserId, CreatedAt DESC)
  INCLUDE (NotificationType, ContextPayload, EventId)
  WHERE ReadAt IS NULL;
  ```
* **Tradeoff Analysis**: Touches only active unread alerts. Costless background polling that never scans historical read notifications.
* **Status**: **QUERY-PERFORMANCE CANDIDATE**.

### 3.6 `audit.AuditLogs`: Timeline & Resource History
* **Candidate Name**: `IX_AuditLogs_Resource_OccurredAt`
* **Target Query**: Administrative history modals (`WHERE ResourceType = @Type AND ResourceId = @Id ORDER BY OccurredAt DESC`) (ADR-005).
* **Key Columns**: `ResourceType` ASC, `ResourceId` ASC, `OccurredAt` DESC
* **Included Columns**: `ActorUserId`, `Action`, `MandatoryReason`, `Source`
* **Status**: **QUERY-PERFORMANCE CANDIDATE**.

* **Candidate Name**: `IX_AuditLogs_Actor_OccurredAt`
* **Target Query**: Tracking actions by a specific Administrator (`WHERE ActorUserId = @UserId ORDER BY OccurredAt DESC`).
* **Key Columns**: `ActorUserId` ASC, `OccurredAt` DESC
* **Included Columns**: `Action`, `ResourceType`, `ResourceId`
* **Status**: **QUERY-PERFORMANCE CANDIDATE**.

---

## 4. Summary Matrix

| Schema | Table | Index Name | Classification | Key Columns |
| :--- | :--- | :--- | :--- | :--- |
| `events` | `StaffingCycles` | `UX_StaffingCycles_CurrentPerEvent` | **LOCKED INTEGRITY** | `EventId` (Filtered `WHERE IsCurrent = 1`) |
| `events` | `ChiefLeaderAssignments` | `UX_ChiefLeaderAssignments_ActivePerEvent` | **LOCKED INTEGRITY** | `EventId` (Filtered `WHERE Status = 'Active'`) |
| `staffing` | `TeamAssignments` | `UX_TeamAssignments_ActiveLeaderPerCycle` | **LOCKED INTEGRITY** | `StaffingCycleId, LeaderUserId` (Filtered `WHERE Status = 'Active'`) |
| `staffing` | `AvailabilityResponses` | `UX_AvailabilityResponses_Cycle_Leader` | **LOCKED INTEGRITY** | `StaffingCycleId, LeaderUserId` (Unique Constraint) |
| `attendance` | `AttendanceOutcomes` | `UX_AttendanceOutcomes_Assignment` | **LOCKED INTEGRITY** | `TeamAssignmentId` (Unique Constraint) |
| `events` | `Events` | `IX_Events_Published_EndDateTime` | **QUERY CANDIDATE** | `EndDateTime` (Filtered `WHERE Status = 'Published'`) |
| `staffing` | `AvailabilityResponses` | `IX_AvailabilityResponses_CandidatePool` | **QUERY CANDIDATE** | `StaffingCycleId, Status` |
| `events` | `ChiefLeaderAssignments` | `IX_ChiefLeaderAssignments_User_Status` | **QUERY CANDIDATE** | `ChiefLeaderUserId, Status` |
| `staffing` | `TeamAssignments` | `IX_TeamAssignments_Leader_Status` | **QUERY CANDIDATE** | `LeaderUserId, Status` |
| `notifications`| `InPlatformNotifications`| `IX_Notifications_UnreadPolling` | **QUERY CANDIDATE** | `RecipientUserId, CreatedAt DESC` (Filtered `WHERE ReadAt IS NULL`) |
| `audit` | `AuditLogs` | `IX_AuditLogs_Resource_OccurredAt` | **QUERY CANDIDATE** | `ResourceType, ResourceId, OccurredAt DESC` |
| `audit` | `AuditLogs` | `IX_AuditLogs_Actor_OccurredAt` | **QUERY CANDIDATE** | `ActorUserId, OccurredAt DESC` |
