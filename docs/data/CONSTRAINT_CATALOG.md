# Carpediem Relational Constraint Catalog

> [!IMPORTANT]
> **Status: ARCHITECTURAL BASELINE / PENDING DATA DESIGN REVIEW**  
> **Governance Authority**: Aligned with Ratified Lifecycle Specification, CHANGE-001, and Accepted ADRs (ADR-001 to ADR-009).  
> **Classification Framework**:
> - **`DB ENFORCEABLE`**: Enforced directly by SQL Server engine mechanisms (PRIMARY KEY, FOREIGN KEY, CHECK, UNIQUE, FILTERED UNIQUE INDEX).
> - **`APPLICATION / DOMAIN ENFORCED`**: Enforced in ASP.NET Core domain commands and transactional aggregates (cross-table, multi-aggregate, or complex temporal rules).
> - **`BOTH`**: Enforced in domain business logic and backed by declarative database constraints for defense-in-depth.
> - **`DEFERRED`**: Invariant depends on unresolved feature specifications (Feedback, Incidents, Profile fields).

---

## 1. Classification & Architecture Framework

Relational databases excel at set-based declarative guarantees, but complex multi-aggregate business rules (such as cross-table mutual exclusivity or multi-entity state prerequisites) must not be forced into fragile SQL triggers or procedural database stored procedures. 

Carpediem establishes a clear separation of invariant enforcement:
1. **Physical Declarative Constraints (`DB ENFORCEABLE` / `BOTH`)**: Used for entity-level invariants, state enum validation, temporal bounds within a single row, referential integrity, and filtered uniqueness.
2. **Domain Aggregate Consistency (`APPLICATION / DOMAIN ENFORCED` / `BOTH`)**: Used for workflow state transitions, candidate pool filtering, permission checks, and cross-aggregate invariants that span multiple tables.

---

## 2. Invariant Catalog by Table

### 2.1 Identity Schema

| Target Entity | Invariant Name | Classification | Mechanism | Description / Rule |
| :--- | :--- | :--- | :--- | :--- |
| `identity.Users` | `UX_Users_NormalizedEmail` | `DB ENFORCEABLE` | Unique Index | `NormalizedEmail` must be unique across all user records. |
| `identity.Users` | `UX_Users_NormalizedUserName` | `DB ENFORCEABLE` | Unique Index | `NormalizedUserName` must be unique across all user records. |
| `identity.Roles` | `UX_Roles_NormalizedName` | `DB ENFORCEABLE` | Unique Index | Exactly three fixed roles: `ADMIN`, `CHIEFLEADER`, `LEADER`. |
| `identity.UserRoles` | `PK_identity_UserRoles` | `DB ENFORCEABLE` | Composite PK | `(UserId, RoleId)` composite primary key prevents duplicate role assignments. |
| `identity.UserRoles` | Multi-Role Persona Support | `BOTH` | Domain + Schema | Per CHANGE-001, a user may hold both `ChiefLeader` and `Leader` roles simultaneously. Domain enforces that `ChiefLeader` role assignment is restricted to predefined pool members. |

---

### 2.2 Leader Profile Schema

| Target Entity | Invariant Name | Classification | Mechanism | Description / Rule |
| :--- | :--- | :--- | :--- | :--- |
| `leader.LeaderProfiles` | `UX_LeaderProfiles_UserId` | `DB ENFORCEABLE` | Unique Constraint | Exactly one `LeaderProfile` per `identity.Users` account. |
| `leader.LeaderProfiles` | Profile Eligibility Guard | `APPLICATION ENFORCED` | Domain Command | A `LeaderProfile` is only instantiated for users who hold `Leader` or `ChiefLeader` roles. |
| `leader.LeaderProfiles` | Metrics Derivation Rule | `APPLICATION ENFORCED` | Domain Architecture | Profile owns profile data only; factual participation metrics (events attended, cancellations, no-shows) are derived dynamically from operational records or read models, not denormalized columns. |

---

### 2.3 Events Schema

| Target Entity | Invariant Name | Classification | Mechanism | Description / Rule |
| :--- | :--- | :--- | :--- | :--- |
| `events.Events` | `CK_Events_Status` | `BOTH` | CHECK Constraint + Domain | `Status IN ('Draft', 'Published', 'Completed', 'Cancelled')`. State transitions governed by domain aggregate. |
| `events.Events` | `CK_Events_TimeWindow` | `BOTH` | CHECK Constraint + Domain | `EndDateTime > StartDateTime`. Event start must precede conclusion. |
| `events.Events` | Publishing Prerequisite | `APPLICATION ENFORCED` | Domain Command Guard | Event cannot transition to `Published` unless it possesses **exactly one active accepted `ChiefLeaderAssignment`**. Spans `events.Events` and `events.ChiefLeaderAssignments`. |
| `events.Events` | Event Completion Rule (ADR-007) | `BOTH` | Domain Worker + RowVersion | Event transitions `Published` $\rightarrow$ `Completed` via guarded BackgroundService (`Now > EndDateTime`) or Admin closeout. Concurrency token detects races. |
| `events.StaffingCycles` | `UX_StaffingCycles_Event_Cycle`| `DB ENFORCEABLE` | Unique Constraint | `(EventId, CycleNumber)` unique. Revision sequence is unique per event. |
| `events.StaffingCycles` | `UX_StaffingCycles_CurrentPerEvent`| `DB ENFORCEABLE` | Filtered Unique Index | `CREATE UNIQUE INDEX ... (EventId) WHERE IsCurrent = 1;` Enforces **at most one** current staffing cycle per event. |
| `events.StaffingCycles` | Staffing Cycle Existence Rule | `APPLICATION ENFORCED` | Domain Command | Domain ensures **at least one** current staffing cycle exists once an Event is Published and across date reschedules. |
| `events.StaffingCycles` | Date Reschedule Rotation | `APPLICATION ENFORCED` | Domain Transaction | When event date changes, prior cycle has `IsCurrent = 0` and a new cycle is created with `IsCurrent = 1`. Old availability/assignments remain tied to prior cycle historically. |
| `events.ChiefLeaderAssignments` | `CK_ChiefLeaderAssignments_Status` | `BOTH` | CHECK Constraint + Domain | `Status IN ('Pending', 'Active', 'Declined')`. |
| `events.ChiefLeaderAssignments` | `CK_ChiefLeaderAssignments_Source` | `BOTH` | CHECK Constraint + Domain | `AcceptanceSource IS NULL OR AcceptanceSource IN ('Direct', 'AdminExternal')`. |
| `events.ChiefLeaderAssignments` | `UX_ChiefLeaderAssignments_ActivePerEvent` | `DB ENFORCEABLE` | Filtered Unique Index | `CREATE UNIQUE INDEX ... (EventId) WHERE Status = 'Active';` Guarantees at most one active Chief Leader per event. |
| `events.ChiefLeaderAssignments` | Predefined Pool Invariant | `APPLICATION ENFORCED` | Domain Command Guard | Candidate must belong to the predefined Chief Leader organizational pool (`identity.UserRoles` has `ChiefLeader`). No qualification workflow exists. |
| `events.ChiefLeaderAssignments` | External Acceptance Audit | `APPLICATION ENFORCED` | ADR-005 Domain Guard | If `AcceptanceSource == 'AdminExternal'`, an immutable audit log with acting admin and mandatory justification is required. |
| `events.ChiefLeaderAssignments` | Historical Preservation | `BOTH` | ON DELETE NO ACTION + Domain | Declined assignments remain in `Declined` status. Re-appointment creates a new assignment row. Rows are not destructively overwritten. |

---

### 2.4 Staffing Schema

| Target Entity | Invariant Name | Classification | Mechanism | Description / Rule |
| :--- | :--- | :--- | :--- | :--- |
| `staffing.AvailabilityResponses` | `CK_AvailabilityResponses_Status` | `BOTH` | CHECK Constraint + Domain | `Status IN ('Available', 'NotAvailable')`. "No Record" is modeled as row absence. |
| `staffing.AvailabilityResponses` | `UX_AvailabilityResponses_Cycle_Leader` | `DB ENFORCEABLE` | Unique Constraint | `(StaffingCycleId, LeaderUserId)` unique. At most one response per volunteer per staffing cycle. |
| `staffing.AvailabilityResponses` | Assignment Independence | `APPLICATION ENFORCED` | Domain Architecture | Direct team assignment or selection does NOT delete or mutate the Leader's availability response. |
| `staffing.TeamAssignments` | `CK_TeamAssignments_Status` | `BOTH` | CHECK Constraint + Domain | `Status IN ('Active', 'Cancelled')`. "No Record" is modeled as row absence. |
| `staffing.TeamAssignments` | `UX_TeamAssignments_ActiveLeaderPerCycle` | `DB ENFORCEABLE` | Filtered Unique Index | `CREATE UNIQUE INDEX ... (StaffingCycleId, LeaderUserId) WHERE Status = 'Active';` A Leader can hold at most one active slot per cycle. |
| `staffing.TeamAssignments` | Mandatory Self-Cancel Reason | `APPLICATION ENFORCED` | Domain Command Guard | Leader self-cancellation strictly requires a non-empty `CancellationReason`. Other cancellation paths (e.g. event cancel) do not require text reasons in this column. |
| `staffing.TeamAssignments` | Immutable Replacement Flow | `BOTH` | Filtered Index + Domain | Cancelled assignment remains `Cancelled`. Replacement creates a brand-new `TeamAssignment` record in `Active` status. |
| `staffing.TeamAssignments` | Direct Draft Authority | `APPLICATION ENFORCED` | Domain Command | Leadership may assign a Leader regardless of availability response (`Available`, `NotAvailable`, or No Response). |

---

### 2.5 Attendance Schema

| Target Entity | Invariant Name | Classification | Mechanism | Description / Rule |
| :--- | :--- | :--- | :--- | :--- |
| `attendance.AttendanceOutcomes` | `CK_AttendanceOutcomes_Outcome` | `BOTH` | CHECK Constraint + Domain | `Outcome IN ('Attended', 'NoShow')`. Pure absence semantics: unrecorded attendance is the absence of a row. |
| `attendance.AttendanceOutcomes` | `UX_AttendanceOutcomes_Assignment` | `DB ENFORCEABLE` | Unique Constraint | `TeamAssignmentId` is unique. At most one attendance outcome record per team assignment. |
| `attendance.AttendanceOutcomes` | Bound to Active Assignment | `APPLICATION ENFORCED` | Domain Command Guard | Attendance outcome can only be initialized for `Active` team assignments, never for cancelled assignments. |
| `attendance.AttendanceOutcomes` | Immediate Finalization Authority | `APPLICATION ENFORCED` | Domain Architecture | Chief Leader roll-call submission takes immediate effect without an administrative approval gate. |
| `attendance.AttendanceOutcomes` | Audited Admin Correction | `APPLICATION ENFORCED` | ADR-005 Domain Guard | Administrative correction of finalized attendance requires an immutable audit log capturing old value, new value, and mandatory reason. |
| `attendance.AttendanceOutcomes` | Event Completion Decoupling | `APPLICATION ENFORCED` | Domain Architecture | Unrecorded attendance does not block event completion; it surfaces as the derived dashboard condition `Pending Attendance`. |

---

### 2.6 Notifications & Audit Schemas

| Target Entity | Invariant Name | Classification | Mechanism | Description / Rule |
| :--- | :--- | :--- | :--- | :--- |
| `notifications.InPlatformNotifications`| `CK_Notifications_ReadAt` | `DB ENFORCEABLE` | CHECK Constraint | `ReadAt IS NULL OR ReadAt >= CreatedAt`. `ReadAt IS NULL` represents unread state. |
| `notifications.InPlatformNotifications`| In-Platform Exclusivity | `APPLICATION ENFORCED` | Domain Architecture | Delivery is restricted strictly to in-platform relational records; external channels (SMS, push, WhatsApp) are excluded. |
| `audit.AuditLogs` | Append-Only Immutability | `APPLICATION ENFORCED` | Domain Guard | Normal business workflows never edit or delete historical audit records. (Database login permissions revoking `UPDATE`/`DELETE` are a deployment/security hardening candidate). |
| `audit.AuditLogs` | Sensitive Data Redaction | `APPLICATION ENFORCED` | EF Save Guard (ADR-005)| Passwords, password hashes, security stamps, tokens, cookies, and secrets are strictly blocked from audit payloads. |
| `audit.AuditLogs` | Atomic Transaction Commitment | `APPLICATION ENFORCED` | EF Unit of Work | Audit records commit inside the exact same database transaction as the underlying domain mutation. |

---

## 3. Deep-Dive: Critical Cross-Table Invariants

### 3.1 Invariant 1: Same-Event Dual-Role Mutual Exclusivity (CHANGE-001)
* **Rule**: For any Event $E$ and User $U$:
  $$\text{ActiveChiefLeaderAssignment}(E, U) \implies \neg\text{CurrentOperationalTeamAssignment}(E, U)$$
* **Current Operational Scope**:
  - The invariant checks CURRENT operational participation: an active `ChiefLeaderAssignment` on Event $E$ prohibits an active `TeamAssignment` attached to $E$'s **current** `StaffingCycle` (`IsCurrent = 1`).
  - It does **not** conflict with historical assignments attached to older, sealed `StaffingCycles` (`IsCurrent = false`) from prior dates.
* **Enforcement Strategy**:
  1. **Candidate Pool Exclusion**: Read queries for the candidate pool on Event $E$ automatically filter out the user holding the active `ChiefLeaderAssignment` on Event $E$.
  2. **Assignment Command Guard**:
     - `SelectCandidate` and `DirectDraft` verify that the target user does not hold an `Active` `ChiefLeaderAssignment` for Event $E$.
  3. **Activation Command Guard**:
     - `AcceptChiefLeaderAssignment` and `RecordExternalAcceptance` verify that no active team assignment exists for that user on Event $E$'s current staffing cycle (`IsCurrent = 1`).
     - If an active team assignment exists on the current cycle, activation is rejected until that assignment is resolved.
  4. Enforced via Application/Domain Transaction Guards inside the single host database transaction.

### 3.2 Invariant 2: Event Publishing Prerequisite
* **Rule**:
  $$\text{Event.Status} == \text{'Published'} \implies \text{Event has exactly one Active accepted ChiefLeaderAssignment}$$
* **Why Cross-Table**: Spans `events.Events` (`Status`) and `events.ChiefLeaderAssignments` (`Status = 'Active'`).
* **Enforcement Strategy**:
  - Command `PublishEventCommand(EventId)` executes inside a database transaction:
    1. Inspects `events.ChiefLeaderAssignments` for the target `EventId`.
    2. Validates that exactly one record exists with `Status = 'Active'`.
    3. Validates that `StartDateTime` and `EndDateTime` are defined.
    4. Updates `events.Events.Status = 'Published'`.
    5. Inserts initial `events.StaffingCycles` record (`CycleNumber = 1`, `EffectiveEventDate = CAST(StartDateTime AS date)`, `IsCurrent = 1`).
    6. Commits transaction atomically.

---

## 4. Ratified Engagement & Account Invariants ([CHANGE-003](../decisions/CHANGE-003-v1-remaining-slice-policy.md))

| Target Entity | Invariant Name | Classification | Mechanism | Description / Rule |
| :--- | :--- | :--- | :--- | :--- |
| `identity.Users` | Pending Registration Approval (`SLICE-20`) | `APPLICATION ENFORCED` | Domain Command + Login Check | Publicly registered Leaders start with `IsActive = 0` and `Leader` role; login is blocked until an Admin approves the account (`IsActive = 1`). |
| `engagement.EventFeedback` | `CK_EventFeedback_Rating` (`SLICE-21`) | `BOTH` | CHECK Constraint + Domain | `[Rating] BETWEEN 1 AND 5`. |
| `engagement.EventFeedback` | `CK_EventFeedback_Kind` & `Subject` | `BOTH` | CHECK Constraint + Domain | `Kind IN ('Event', 'Leader')`; `LeaderUserId IS NULL` when `Kind = 'Event'` and `NOT NULL` when `Kind = 'Leader'`. |
| `engagement.EventFeedback` | `IX_EventFeedback_EventId_RaterUserId_Kind_LeaderUserId` | `DB ENFORCEABLE` | Unfiltered Unique Index | At most one rating per rater, event, kind, and target subject (including `NULL` `LeaderUserId` for Event ratings). |
| `engagement.IncidentReports` | Temporal & Review Invariant (`SLICE-22`) | `APPLICATION ENFORCED` | Domain Aggregate + RowVersion | `OccurredAt <= ReportedAt` (UTC); incident reports can be marked reviewed once by an Admin (`ReviewedAt`, `ReviewedByUserId`, optional `ReviewNote`) and are audited in `audit.AuditLogs`. |

### Remaining Post-V1 Deferrals
1. **Extended Leader Profile Fields**: Personal contact details, emergency contacts, shirt sizes, dietary constraints, certifications.
2. **Data Retention & Archiving**: Minimum retention duration before cold storage or historical purging.
