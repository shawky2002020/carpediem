# Carpediem Relational Database Schema Specification

> [!IMPORTANT]
> **Status: ARCHITECTURAL BASELINE / PENDING DATA DESIGN REVIEW**  
> **Database Engine**: Microsoft SQL Server 2025 (Amazon RDS for SQL Server in production; Docker container locally).  
> **Architecture Pattern**: Schema-per-Module Organization under a Unified Host `CarpediemDbContext` (ADR-009).  
> **Referential Integrity**: Physical Foreign Keys across schemas with `ON DELETE NO ACTION` to preserve historical integrity.

---

## 1. Schema Namespaces & Module Write Ownership

Database tables are partitioned into dedicated SQL Server schemas representing domain module boundaries. Direct cross-schema write mutations are strictly prohibited; all modifications execute via the owning module's command handlers.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                CARPEDIEM SQL SERVER 2025                               │
├───────────────────┬───────────────────┬────────────────────────┬───────────────────────┤
│  identity schema  │   events schema   │    staffing schema     │   attendance schema   │
│  - Users          │   - Events        │  - AvailabilityResp    │  - AttendanceOutcomes │
│  - Roles          │   - ChiefLeader   │  - TeamAssignments    │    (Attended / NoShow)│
│  - UserRoles      │     Assignments   ├────────────────────────┼───────────────────────┤
│  - Framework Auth │   - StaffingCycles│   notifications schema │     audit schema      │
├───────────────────┴───────────────────┤  - InPlatformNotifs    │  - AuditLogs          │
│             leader schema             ├────────────────────────┴───────────────────────┤
│  - LeaderProfiles                     │  feedback & incidents: DEFERRED TO SPEC        │
└───────────────────────────────────────┴────────────────────────────────────────────────┘
```

| Schema Namespace | Boundary Classification | Owning Module | Allowed Writers | Read Permissions |
| :--- | :--- | :--- | :--- | :--- |
| **`identity`** | Domain Schema | `Identity & Access` | Identity Module Commands | All modules (via projections/contracts) |
| **`events`** | Domain Schema | `Events` | Events Module Commands & BackgroundService | All modules & Dashboard Views |
| **`staffing`** | Domain Schema | `Staffing` | Staffing Module Commands | Staffing, Attendance, Dashboard Views |
| **`attendance`** | Domain Schema | `Attendance` | Attendance Module Commands | Attendance, Profiles, Dashboard Views |
| **`leader`** | Domain Schema | `LeaderProfiles` | LeaderProfiles Module Commands | LeaderProfiles, Staffing, Dashboard |
| **`notifications`**| Domain Schema | `Notifications` | Notifications Module Commands | Notifications Module |
| **`audit`** | Infrastructure Schema | Cross-Cutting / Audit | Audited Domain Transactions (EF Guard) | Operations Admin Reports & DBAs |
| **`engagement`** | Domain Schema | `Engagement` (Feedback & Incidents) | Feedback & Incident Endpoints (`CHANGE-003`) | Admin Review & Event-Scoped Chief/Leader Views |

---

## 2. Table Definitions by Schema

### 2.1 Schema: `identity`

#### Table: `identity.Users`
Represents an authenticated human account in the system. Configured via EF Core `IdentityUser<Guid>` to use `uniqueidentifier` keys.

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | `PK_identity_Users` (Clustered) | Authoritative account GUID. |
| `UserName` | `nvarchar(256)` | NO | | | Framework login username. |
| `NormalizedUserName`| `nvarchar(256)` | NO | | `UX_Users_NormalizedUserName` | Uppercase normalized username. |
| `Email` | `nvarchar(256)` | NO | | | Contact/login email address. |
| `NormalizedEmail` | `nvarchar(256)` | NO | | `UX_Users_NormalizedEmail` | Uppercase normalized email. |
| `EmailConfirmed` | `bit` | NO | `0` | | Account email confirmation state. |
| `PasswordHash` | `nvarchar(max)` | YES | | | PBKDF2 / ASP.NET Core password hash. |
| `SecurityStamp` | `nvarchar(max)` | YES | | | Revocation & session stamp (ADR-002). |
| `ConcurrencyStamp` | `nvarchar(max)` | YES | | | Framework optimistic concurrency stamp. |
| `PhoneNumber` | `nvarchar(50)` | YES | | | Optional phone number. |
| `PhoneNumberConfirmed`| `bit` | NO | `0` | | Phone confirmation flag. |
| `TwoFactorEnabled` | `bit` | NO | `0` | | Two-factor authentication toggle. |
| `LockoutEnd` | `datetimeoffset` | YES | | | Lockout expiration window. |
| `LockoutEnabled` | `bit` | NO | `1` | | Lockout policy enforcement flag. |
| `AccessFailedCount`| `int` | NO | `0` | | Failed login attempt counter. |
| `IsActive` | `bit` | NO | `1` | | Domain activation flag. Deactivated users cannot log in. |
| `CreatedAt` | `datetimeoffset`| NO | `SYSDATETIMEOFFSET()`| | Account creation timestamp. |

#### Table: `identity.Roles`
System authorization roles (`Admin`, `ChiefLeader`, `Leader`). Configured via `IdentityRole<Guid>`.

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | `PK_identity_Roles` (Clustered) | Role GUID. |
| `Name` | `nvarchar(256)` | NO | | | Role display name. |
| `NormalizedName` | `nvarchar(256)` | NO | | `UX_Roles_NormalizedName` | Normalized role name (`ADMIN`, `CHIEFLEADER`, `LEADER`). |
| `ConcurrencyStamp` | `nvarchar(max)` | YES | | | Framework concurrency stamp. |

#### Table: `identity.UserRoles`
Associative join mapping users to roles. Supports multi-role personas per CHANGE-001.

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `UserId` | `uniqueidentifier` | NO | | `PK_identity_UserRoles`, `FK_UserRoles_Users` | Target user account. `ON DELETE CASCADE`. |
| `RoleId` | `uniqueidentifier` | NO | | `PK_identity_UserRoles`, `FK_UserRoles_Roles` | Target system role. `ON DELETE CASCADE`. |

*Composite Primary Key*: `PK_identity_UserRoles (UserId, RoleId)`.

#### ASP.NET Core Identity Framework Infrastructure Tables
Managed directly by ASP.NET Core Identity:
* `identity.UserClaims` (`Id` int IDENTITY, `UserId` uniqueidentifier, `ClaimType` nvarchar(max), `ClaimValue` nvarchar(max)).
* `identity.UserLogins` (`LoginProvider` nvarchar(450), `ProviderKey` nvarchar(450), `ProviderDisplayName` nvarchar(max), `UserId` uniqueidentifier).
* `identity.UserTokens` (`UserId` uniqueidentifier, `LoginProvider` nvarchar(450), `Name` nvarchar(450), `Value` nvarchar(max)).
* `identity.RoleClaims` (`Id` int IDENTITY, `RoleId` uniqueidentifier, `ClaimType` nvarchar(max), `ClaimValue` nvarchar(max)).

---

### 2.2 Schema: `leader`

#### Table: `leader.LeaderProfiles`
Volunteer domain profile tracking biographical and contact details.

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | `PK_leader_LeaderProfiles` (Clustered) | Profile GUID. |
| `UserId` | `uniqueidentifier` | NO | | `UX_LeaderProfiles_UserId`, `FK_LeaderProfiles_Users` | 1:1 bound to `identity.Users.Id`. `ON DELETE NO ACTION`. |
| `CreatedAt` | `datetimeoffset`| NO | `SYSDATETIMEOFFSET()`| | Profile creation timestamp. |
| `UpdatedAt` | `datetimeoffset`| YES | | | Last profile update timestamp. |

*Design Rule*: Historical participation counts (events attended, cancellations, no-shows) are **not stored as denormalized columns**. They are derived from operational records (`attendance.AttendanceOutcomes`, `staffing.TeamAssignments`) or dedicated read models. Detailed profile attributes (phone, emergency contact, shirt size, dietary needs, qualifications) are **DEFERRED TO FEATURE SPEC**.

---

### 2.3 Schema: `events`

#### Table: `events.Events`
Master aggregate root representing an organized gathering.

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | `PK_events_Events` (Clustered) | Authoritative event GUID. |
| `Title` | `nvarchar(200)` | NO | | | Descriptive event name. |
| `Status` | `varchar(20)` | NO | `'Draft'` | `CK_Events_Status` | Persisted state: `Draft`, `Published`, `Completed`, `Cancelled`. |
| `StartDateTime` | `datetimeoffset`| NO | | | Scheduled start timestamp with timezone offset. |
| `EndDateTime` | `datetimeoffset`| NO | | `CK_Events_TimeWindow` | Scheduled conclusion window (`EndDateTime > StartDateTime`). |
| `CreatedByUserId` | `uniqueidentifier` | NO | | `FK_Events_Users_CreatedBy` | Creating Admin GUID. `ON DELETE NO ACTION`. |
| `CreatedAt` | `datetimeoffset`| NO | `SYSDATETIMEOFFSET()`| | Creation audit timestamp. |
| `RowVersion` | `rowversion` | NO | | Concurrency Token | Optimistic concurrency token (ADR-009). |

*Table Constraints*:
* `CK_Events_Status`: `Status IN ('Draft', 'Published', 'Completed', 'Cancelled')`.
* `CK_Events_TimeWindow`: `EndDateTime > StartDateTime`.

#### Table: `events.StaffingCycles`
Represents an immutable staffing revision boundary. Scopes availability responses and team assignments to a specific event calendar date.

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | `PK_events_StaffingCycles` (Clustered)| Cycle GUID. |
| `EventId` | `uniqueidentifier` | NO | | `FK_StaffingCycles_Events`, Index | Associated event. `ON DELETE NO ACTION`. |
| `CycleNumber` | `int` | NO | `1` | `CK_StaffingCycles_Number` (`>= 1`)| Monotonically increasing revision sequence. |
| `IsCurrent` | `bit` | NO | `1` | Filtered Unique Index | `1` if currently active staffing window; `0` if sealed. |
| `EffectiveEventDate`| `date` | NO | | | Calendar date this staffing context applies to. |
| `CreatedAt` | `datetimeoffset`| NO | `SYSDATETIMEOFFSET()`| | Creation timestamp. |

*Table Constraints & Filtered Indexes*:
* `UX_StaffingCycles_Event_CycleNumber`: Unique `(EventId, CycleNumber)`.
* `UX_StaffingCycles_CurrentPerEvent`: Filtered Unique Index:
  ```sql
  CREATE UNIQUE NONCLUSTERED INDEX UX_StaffingCycles_CurrentPerEvent
  ON events.StaffingCycles (EventId)
  WHERE IsCurrent = 1;
  ```
  *Guarantee*: Enforces at the database engine level that an Event has **at most one current staffing cycle**. Domain transaction rules ensure **at least one** current cycle exists once an Event is Published.

#### Table: `events.ChiefLeaderAssignments`
Manages appointment, acceptance/decline workflow, and operational ownership of an event Chief Leader.

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | `PK_events_ChiefLeaderAssignments` | Assignment GUID. |
| `EventId` | `uniqueidentifier` | NO | | `FK_ChiefLeaderAssignments_Events` | Target event. `ON DELETE NO ACTION`. |
| `ChiefLeaderUserId`| `uniqueidentifier` | NO | | `FK_ChiefLeaderAssignments_Users` | Candidate Chief Leader account. `ON DELETE NO ACTION`. |
| `Status` | `varchar(20)` | NO | `'Pending'` | `CK_ChiefLeaderAssignments_Status` | `Pending`, `Active`, `Declined`. |
| `AppointedByUserId`| `uniqueidentifier` | NO | | `FK_ChiefLeaderAssignments_AppointedBy`| Appointing Admin. `ON DELETE NO ACTION`. |
| `AppointedAt` | `datetimeoffset`| NO | `SYSDATETIMEOFFSET()`| | Appointment timestamp. |
| `AcceptedAt` | `datetimeoffset`| YES | | | Acceptance timestamp. |
| `AcceptanceSource` | `varchar(20)` | YES | | `CK_ChiefLeaderAssignments_Source`| Domain acceptance provenance: `Direct` or `AdminExternal`. |
| `AcceptedByUserId` | `uniqueidentifier` | YES | | `FK_ChiefLeaderAssignments_AcceptedBy` | Acting user GUID (`NULL` if Direct; Admin ID if AdminExternal). `ON DELETE NO ACTION`. |
| `DeclinedAt` | `datetimeoffset`| YES | | | Timestamp if declined. |
| `RowVersion` | `rowversion` | NO | | Concurrency Token | Optimistic concurrency token. |

*Table Constraints & Filtered Indexes*:
* `CK_ChiefLeaderAssignments_Status`: `Status IN ('Pending', 'Active', 'Declined')`.
* `CK_ChiefLeaderAssignments_Source`: `AcceptanceSource IS NULL OR AcceptanceSource IN ('Direct', 'AdminExternal')`.
* `UX_ChiefLeaderAssignments_ActivePerEvent`: Filtered Unique Index:
  ```sql
  CREATE UNIQUE NONCLUSTERED INDEX UX_ChiefLeaderAssignments_ActivePerEvent
  ON events.ChiefLeaderAssignments (EventId)
  WHERE Status = 'Active';
  ```
  *Guarantee*: Enforces at the database engine level that an Event can have **at most one Active Chief Leader**.

---

### 2.4 Schema: `staffing`

#### Table: `staffing.AvailabilityResponses` (Dimension A)
Captures a volunteer Leader's declared availability for an event staffing cycle.

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | `PK_staffing_AvailabilityResponses` | Response GUID. |
| `StaffingCycleId` | `uniqueidentifier` | NO | | `FK_AvailabilityResponses_StaffingCycles`| Scoped staffing cycle. `ON DELETE NO ACTION`. |
| `LeaderUserId` | `uniqueidentifier` | NO | | `FK_AvailabilityResponses_Users` | Responding Leader. `ON DELETE NO ACTION`. |
| `Status` | `varchar(20)` | NO | | `CK_AvailabilityResponses_Status` | `Available` or `NotAvailable`. |
| `SubmittedAt` | `datetimeoffset`| NO | `SYSDATETIMEOFFSET()`| | Initial submission timestamp. |
| `UpdatedAt` | `datetimeoffset`| YES | | | Last toggle/modification timestamp. |

*Table Constraints & Indexes*:
* `CK_AvailabilityResponses_Status`: `Status IN ('Available', 'NotAvailable')`.
* `UX_AvailabilityResponses_Cycle_Leader`: Unique `(StaffingCycleId, LeaderUserId)`.
  *Guarantee*: Enforces at most one availability response per Leader per staffing cycle.

#### Table: `staffing.TeamAssignments` (Dimension B)
Records leadership's operational placement of a Leader on an event team roster slot.

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | `PK_staffing_TeamAssignments` | Assignment GUID. |
| `StaffingCycleId` | `uniqueidentifier` | NO | | `FK_TeamAssignments_StaffingCycles`| Scoped staffing cycle. `ON DELETE NO ACTION`. |
| `LeaderUserId` | `uniqueidentifier` | NO | | `FK_TeamAssignments_Users_Leader` | Staffed Leader. `ON DELETE NO ACTION`. |
| `Status` | `varchar(20)` | NO | `'Active'` | `CK_TeamAssignments_Status` | `Active` or `Cancelled`. |
| `AssignedByUserId` | `uniqueidentifier` | NO | | `FK_TeamAssignments_Users_AssignedBy`| Chief Leader or Admin. `ON DELETE NO ACTION`.|
| `AssignedAt` | `datetimeoffset`| NO | `SYSDATETIMEOFFSET()`| | Placement timestamp. |
| `CancelledByUserId`| `uniqueidentifier` | YES | | `FK_TeamAssignments_Users_CancelledBy`| Cancelling actor. `ON DELETE NO ACTION`. |
| `CancelledAt` | `datetimeoffset`| YES | | | Cancellation timestamp. |
| `CancellationReason`| `nvarchar(500)`| YES | | | Conditional cancellation reason (mandatory for self-cancellation). |
| `RowVersion` | `rowversion` | NO | | Concurrency Token | Optimistic concurrency token. |

*Table Constraints & Filtered Indexes*:
* `CK_TeamAssignments_Status`: `Status IN ('Active', 'Cancelled')`.
* `UX_TeamAssignments_ActiveLeaderPerCycle`: Filtered Unique Index:
  ```sql
  CREATE UNIQUE NONCLUSTERED INDEX UX_TeamAssignments_ActiveLeaderPerCycle
  ON staffing.TeamAssignments (StaffingCycleId, LeaderUserId)
  WHERE Status = 'Active';
  ```
  *Guarantee*: Enforces that a Leader can hold **at most one Active assignment** within a given staffing cycle.

---

### 2.5 Schema: `attendance`

#### Table: `attendance.AttendanceOutcomes` (Dimension C)
Factual on-the-ground record of physical event service for an assigned volunteer. Follows pure absence semantics: unrecorded attendance is the absence of a row.

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | `PK_attendance_AttendanceOutcomes` | Outcome GUID. |
| `TeamAssignmentId` | `uniqueidentifier` | NO | | `UX_AttendanceOutcomes_Assignment`, FK | Bound 1:1 to `staffing.TeamAssignments.Id`. `ON DELETE NO ACTION`. |
| `Outcome` | `varchar(20)` | NO | | `CK_AttendanceOutcomes_Outcome` | Factual outcome: `Attended` or `NoShow`. |
| `FinalizedByUserId`| `uniqueidentifier` | NO | | `FK_AttendanceOutcomes_FinalizedBy` | Chief Leader who recorded roll call. `ON DELETE NO ACTION`. |
| `FinalizedAt` | `datetimeoffset`| NO | `SYSDATETIMEOFFSET()`| | Finalization timestamp. |
| `CorrectedByUserId`| `uniqueidentifier` | YES | | `FK_AttendanceOutcomes_CorrectedBy` | Admin who performed correction. `ON DELETE NO ACTION`. |
| `CorrectedAt` | `datetimeoffset`| YES | | | Administrative correction timestamp. |
| `RowVersion` | `rowversion` | NO | | Concurrency Token | Optimistic concurrency token. |

*Table Constraints & Indexes*:
* `CK_AttendanceOutcomes_Outcome`: `Outcome IN ('Attended', 'NoShow')`.
* `UX_AttendanceOutcomes_Assignment`: Unique `(TeamAssignmentId)`.
  *Guarantee*: Enforces at most one attendance record per team assignment.

---

### 2.6 Schema: `notifications`

#### Table: `notifications.InPlatformNotifications`
Persisted semantic in-platform notification records (ADR-006).

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | `PK_notifications_Notifications` | Notification GUID. |
| `RecipientUserId` | `uniqueidentifier` | NO | | `FK_Notifications_Users`, Index | Target user. `ON DELETE NO ACTION`. |
| `EventId` | `uniqueidentifier` | YES | | `FK_Notifications_Events` | Contextual event link. `ON DELETE NO ACTION`. |
| `NotificationType` | `varchar(50)` | NO | | | Semantic notification code. |
| `CreatedAt` | `datetimeoffset`| NO | `SYSDATETIMEOFFSET()`| Index | Notification creation timestamp. |
| `ReadAt` | `datetimeoffset`| YES | | `CK_Notifications_ReadAt` | Read confirmation timestamp (`NULL` = unread). |
| `ContextPayload` | `nvarchar(500)` | YES | | | Constrained key-value context for client rendering. |

*Table Constraints*:
* `CK_Notifications_ReadAt`: `ReadAt IS NULL OR ReadAt >= CreatedAt`.

---

### 2.7 Schema: `audit`

#### Table: `audit.AuditLogs`
Immutable, append-only operational audit trail capturing critical actions and mandatory justification notes (ADR-005).

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | `PK_audit_AuditLogs` (Clustered) | Audit record GUID. |
| `OccurredAt` | `datetimeoffset`| NO | `SYSDATETIMEOFFSET()`| Index | UTC occurrence timestamp. |
| `ActorUserId` | `uniqueidentifier` | YES | | `FK_AuditLogs_Users`, Index | Acting user GUID (`NULL` for automated system worker). `ON DELETE NO ACTION`. |
| `Action` | `varchar(100)` | NO | | Index | Action name (e.g., `Events.EventRescheduled`). |
| `ResourceType` | `varchar(50)` | NO | | Index | Affected entity type (`Event`, `TeamAssignment`, etc.). |
| `ResourceId` | `uniqueidentifier` | NO | | Index | Primary key of affected entity. |
| `CorrelationId` | `uniqueidentifier` | NO | | Index | Request correlation identifier. |
| `MandatoryReason` | `nvarchar(1000)`| YES | | | Mandatory justification note where required. |
| `Source` | `varchar(50)` | NO | | | `Direct`, `AdminExternal`, `AdminOverride`, `SystemWorker`. |
| `PreviousValue` | `nvarchar(max)` | YES | | | Structured JSON of modified previous fields. |
| `NewValue` | `nvarchar(max)` | YES | | | Structured JSON of modified new fields. |

*Design Rule*: Normal application workflows never edit or delete historical audit records. Revoking `UPDATE` and `DELETE` permissions at the database login level is documented as a deployment/security hardening candidate consistent with ADR-005.

---

### 2.8 Schema: `engagement` (Ratified per [CHANGE-003](../decisions/CHANGE-003-v1-remaining-slice-policy.md))

#### Table: `engagement.EventFeedback` (`SLICE-21`)
Post-completion 1–5 ratings submitted by the active Chief Leader for assigned Leaders (`Kind = 'Leader'`) or by assigned Leaders for the Event (`Kind = 'Event'`).

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | | `PK_EventFeedback` (Clustered) | Feedback record GUID. |
| `EventId` | `uniqueidentifier` | NO | | `FK_EventFeedback_Events_EventId` | Target Completed Event. `ON DELETE NO ACTION`. |
| `RaterUserId` | `uniqueidentifier` | NO | | `FK_EventFeedback_Users_RaterUserId` | Submitting Chief Leader or Leader. `ON DELETE NO ACTION`. |
| `LeaderUserId` | `uniqueidentifier` | YES | | `FK_EventFeedback_Users_LeaderUserId` | Rated Leader when `Kind = 'Leader'`; `NULL` when `Kind = 'Event'`. `ON DELETE NO ACTION`. |
| `Kind` | `nvarchar(16)` | NO | | `CK_EventFeedback_Kind`, `CK_EventFeedback_Subject` | `'Event'` or `'Leader'`. |
| `Rating` | `int` | NO | | `CK_EventFeedback_Rating` (`BETWEEN 1 AND 5`) | Numeric 1–5 rating. |
| `Comment` | `nvarchar(500)` | YES | | | Optional post-event comment (max 500 chars). |
| `SubmittedAt` | `datetimeoffset` | NO | | | UTC submission timestamp. |

*Table Constraints & Indexes*:
* `CK_EventFeedback_Rating`: `[Rating] BETWEEN 1 AND 5`.
* `CK_EventFeedback_Kind`: `[Kind] IN ('Event','Leader')`.
* `CK_EventFeedback_Subject`: `([Kind] = 'Event' AND [LeaderUserId] IS NULL) OR ([Kind] = 'Leader' AND [LeaderUserId] IS NOT NULL)`.
* `IX_EventFeedback_EventId_RaterUserId_Kind_LeaderUserId`: Unfiltered unique index preventing duplicate ratings per `(EventId, RaterUserId, Kind, LeaderUserId)`.

#### Table: `engagement.IncidentReports` (`SLICE-22`)
Event-scoped incident log with Admin review tracking.

| Column Name | SQL Server Type | Nullable | Default | Constraints & Indexes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | NO | | `PK_IncidentReports` (Clustered) | Incident report GUID. |
| `EventId` | `uniqueidentifier` | NO | | `FK_IncidentReports_Events_EventId`, Index | Associated Event. `ON DELETE NO ACTION`. |
| `ReporterUserId` | `uniqueidentifier` | NO | | `FK_IncidentReports_Users_ReporterUserId` | Submitting participant/Admin. `ON DELETE NO ACTION`. |
| `Title` | `nvarchar(200)` | NO | | | Short incident title (1–200 chars). |
| `Description` | `nvarchar(4000)` | NO | | | Factual incident narrative (1–4000 chars). |
| `OccurredAt` | `datetimeoffset` | NO | | | UTC occurrence timestamp (`<= ReportedAt`). |
| `ReportedAt` | `datetimeoffset` | NO | | Index `(EventId, ReportedAt)` | UTC submission timestamp. |
| `ReviewedAt` | `datetimeoffset` | YES | | Index `(ReviewedAt)` | UTC Admin review timestamp (`NULL` = open). |
| `ReviewedByUserId` | `uniqueidentifier` | YES | | `FK_IncidentReports_Users_ReviewedByUserId` | Reviewing Admin GUID. `ON DELETE NO ACTION`. |
| `ReviewNote` | `nvarchar(1000)` | YES | | | Optional Admin review note (max 1000 chars). |
| `RowVersion` | `rowversion` | NO | | Concurrency Token | Optimistic concurrency token. |

---

## 3. Relational Key & Foreign Key Strategy

### 3.1 Primary Keys & Sequential Clustering
* **Surrogate Keys**: Tables use a scalar `uniqueidentifier` surrogate primary key.
* **Clustering Performance**: Ordered or sequential key generation can reduce page splits and B-tree fragmentation compared with fully random GUID insertion patterns, subject to SQL Server ordering and actual workload. The exact generation mechanism (`NEWSEQUENTIALID()` in SQL Server vs application-level `Guid.CreateVersion7()` in .NET 10) is marked for EF/SQL implementation review to verify SQL Server byte-ordering compatibility.
* **ASP.NET Core Identity Keys**: `identity.Users.Id` and `identity.Roles.Id` are explicitly configured as `uniqueidentifier` (`IdentityUser<Guid>`).
* **Associative Tables**: `identity.UserRoles` uses composite primary key `(UserId, RoleId)`.

### 3.2 Foreign Key Referential Integrity & Delete Rule
* **Default Rule**: All domain foreign keys are configured with **`ON DELETE NO ACTION`**.
* **Historical Preservation**: Carpediem preserves historical domain data across business workflows. User deactivation (`IsActive = 0`) preserves user records; assignments, attendance outcomes, and audit logs are never deleted or corrupted by cascading deletes.
* **Exception**: Framework-managed tokens, claims, and role associations within `identity` use `ON DELETE CASCADE` from `identity.Users`.
