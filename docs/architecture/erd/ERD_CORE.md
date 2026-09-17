# Carpediem Core Entity-Relationship Diagram (ERD)

> [!IMPORTANT]
> **Status: ARCHITECTURAL BASELINE / PENDING DATA DESIGN REVIEW**  
> **Database Engine**: Microsoft SQL Server 2025  
> **Precedence**: Visual and structural representation of the authoritative logical data model and relational schema specifications.

---

## 1. Overview & Diagram Decomposition

To maintain visual clarity, the relational model is decomposed into three focused domain ERDs followed by an end-to-end cross-module relationship map:

1. **Core Operational ERD**: `events.Events`, `events.StaffingCycles`, `events.ChiefLeaderAssignments`, `staffing.AvailabilityResponses`, `staffing.TeamAssignments`, and `attendance.AttendanceOutcomes`.
2. **Identity & Profile ERD**: `identity.Users`, `identity.Roles`, `identity.UserRoles`, and `leader.LeaderProfiles`.
3. **Cross-Cutting Audit & Notifications ERD**: `audit.AuditLogs` and `notifications.InPlatformNotifications`.
4. **Cross-Module Unified Relationship Map**: Complete high-level system entity relationships and cardinalities.

---

## 2. Diagram 1: Core Operational ERD (Events, Staffing & Attendance)

```mermaid
erDiagram
    Event ||--o{ StaffingCycle : "rotates on reschedule"
    Event ||--o{ ChiefLeaderAssignment : "appoints candidate"
    StaffingCycle ||--o{ AvailabilityResponse : "scopes declaration"
    StaffingCycle ||--o{ TeamAssignment : "scopes roster"
    TeamAssignment ||--o| AttendanceOutcome : "records service (absence = unrecorded)"

    Event {
        uuid Id PK
        nvarchar Title
        varchar Status "Draft | Published | Completed | Cancelled"
        datetimeoffset StartDateTime
        datetimeoffset EndDateTime
        uuid CreatedByUserId FK
        datetimeoffset CreatedAt
        rowversion RowVersion
    }

    StaffingCycle {
        uuid Id PK
        uuid EventId FK
        int CycleNumber "1, 2, ..."
        bit IsCurrent "Filtered Unique Index: at most 1 per event"
        date EffectiveEventDate "Calendar date"
        datetimeoffset CreatedAt
    }

    ChiefLeaderAssignment {
        uuid Id PK
        uuid EventId FK
        uuid ChiefLeaderUserId FK
        varchar Status "Pending | Active | Declined"
        uuid AppointedByUserId FK
        datetimeoffset AppointedAt
        datetimeoffset AcceptedAt "Nullable"
        varchar AcceptanceSource "Direct | AdminExternal"
        uuid AcceptedByUserId FK "Nullable: Admin ID if external"
        datetimeoffset DeclinedAt "Nullable"
        rowversion RowVersion
    }

    AvailabilityResponse {
        uuid Id PK
        uuid StaffingCycleId FK
        uuid LeaderUserId FK
        varchar Status "Available | NotAvailable"
        datetimeoffset SubmittedAt
        datetimeoffset UpdatedAt
    }

    TeamAssignment {
        uuid Id PK
        uuid StaffingCycleId FK
        uuid LeaderUserId FK
        varchar Status "Active | Cancelled"
        uuid AssignedByUserId FK
        datetimeoffset AssignedAt
        uuid CancelledByUserId FK "Nullable"
        datetimeoffset CancelledAt "Nullable"
        nvarchar CancellationReason "Nullable: mandatory for self-cancel"
        rowversion RowVersion
    }

    AttendanceOutcome {
        uuid Id PK
        uuid TeamAssignmentId FK "Unique: at most 1 per assignment"
        varchar Outcome "Attended | NoShow"
        uuid FinalizedByUserId FK
        datetimeoffset FinalizedAt
        uuid CorrectedByUserId FK "Nullable"
        datetimeoffset CorrectedAt "Nullable"
        rowversion RowVersion
    }
```

---

## 3. Diagram 2: Identity, Roles & Leader Profile ERD

```mermaid
erDiagram
    User ||--o{ UserRole : "holds"
    Role ||--o{ UserRole : "assigned_to"
    User ||--o| LeaderProfile : "extends 1:1"

    User {
        uuid Id PK "IdentityUser<Guid>"
        nvarchar UserName
        nvarchar NormalizedUserName "Unique"
        nvarchar Email
        nvarchar NormalizedEmail "Unique"
        bit EmailConfirmed
        nvarchar PasswordHash
        nvarchar SecurityStamp
        bit IsActive "Account activation flag"
        datetimeoffset CreatedAt
    }

    Role {
        uuid Id PK "IdentityRole<Guid>"
        nvarchar Name "Admin | ChiefLeader | Leader"
        nvarchar NormalizedName "Unique"
    }

    UserRole {
        uuid UserId PK_FK
        uuid RoleId PK_FK
    }

    LeaderProfile {
        uuid Id PK
        uuid UserId FK "Unique: 1 per User"
        datetimeoffset CreatedAt
        datetimeoffset UpdatedAt
    }
```

---

## 4. Diagram 3: Cross-Cutting Audit & Notifications ERD

```mermaid
erDiagram
    User ||--o{ InPlatformNotification : "receives"
    Event ||--o{ InPlatformNotification : "contextual link"
    User ||--o{ AuditLog : "initiates (ActorUserId)"

    InPlatformNotification {
        uuid Id PK
        uuid RecipientUserId FK
        uuid EventId FK "Nullable"
        varchar NotificationType
        datetimeoffset CreatedAt
        datetimeoffset ReadAt "Nullable: NULL = unread"
        nvarchar ContextPayload "Bounded JSON context"
    }

    AuditLog {
        uuid Id PK
        datetimeoffset OccurredAt
        uuid ActorUserId FK "Nullable: null for System"
        varchar Action "e.g. Events.EventRescheduled"
        varchar ResourceType "Event | ChiefLeaderAssignment | etc"
        uuid ResourceId
        uuid CorrelationId
        nvarchar MandatoryReason "Required for approved actions"
        varchar Source "Direct | AdminExternal | SystemWorker"
        nvarchar PreviousValue "JSON of modified fields"
        nvarchar NewValue "JSON of modified fields"
    }
```

---

## 5. Diagram 4: Cross-Module Relational Flow & Cardinality Map

```mermaid
erDiagram
    User ||--o{ ChiefLeaderAssignment : "appointed_as"
    User ||--o{ AvailabilityResponse : "declares"
    User ||--o{ TeamAssignment : "staffed_on"
    User ||--o{ Event : "created_by"
    
    Event ||--o{ StaffingCycle : "contains"
    Event ||--o{ ChiefLeaderAssignment : "governed_by"
    StaffingCycle ||--o{ AvailabilityResponse : "tracks"
    StaffingCycle ||--o{ TeamAssignment : "tracks"
    TeamAssignment ||--o| AttendanceOutcome : "evaluates (absence = unrecorded)"

    User ||--o| LeaderProfile : "owns"
    User ||--o{ InPlatformNotification : "receives"
    User ||--o{ AuditLog : "acts"
```

---

## 6. Key Relational Invariants Illustrated in the Diagrams

1. **At Most One Current Staffing Cycle**: An `Event` has zero or more historical `StaffingCycles`, but at most one with `IsCurrent = 1` (enforced via filtered unique index). Domain rules ensure at least one upon publication.
2. **Current Operational Assignment Scope**: An assignment is currently operational only if `TeamAssignment.Status = 'Active'` AND `StaffingCycle.IsCurrent = 1`.
3. **At Most One Active Chief Leader**: An `Event` can have multiple historical `ChiefLeaderAssignments` (`Declined`), but at most one with `Status = 'Active'`.
4. **No Double-Staffing on Roster**: A volunteer Leader can have at most one `TeamAssignment` with `Status = 'Active'` within the same `StaffingCycle`.
5. **Pure Absence Attendance**: Unrecorded attendance is represented by the absence of an `AttendanceOutcome` row. Persisted rows exist only for `Attended` or `NoShow`.
6. **Historical Integrity Preservation**: All cross-module foreign keys use `ON DELETE NO ACTION`. Cancelled team assignments and declined leadership appointments are preserved as historical records and not destructively overwritten during normal business workflows.
