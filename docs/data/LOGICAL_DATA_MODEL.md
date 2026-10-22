# Carpediem Logical Data Model Specification

> [!IMPORTANT]
> **Status: ARCHITECTURAL BASELINE / PENDING DATA DESIGN REVIEW**  
> **Governance Authority**: Aligned with Ratified Lifecycle Specification, CHANGE-001, Module Boundaries, C1/C2 Architecture, and Accepted ADRs (ADR-001 through ADR-009).  
> **Precedence**: This document defines the authoritative logical relational model. It establishes logical entities, ownership boundaries, lifecycle purposes, and invariants prior to physical database migration authoring.

---

## 1. Executive Summary & Modeling Principles

Carpediem is an internal event-staffing platform serving volunteer Leaders, Chief Leaders, and Operations Administrators. The logical data model reflects the following foundational architectural decisions:

1. **Decoupled Lifecycles (ADR-004 & Lifecycle Spec)**: Five distinct, orthogonal state machines (`Event`, `ChiefLeaderAssignment`, `AvailabilityResponse`, `TeamAssignment`, `AttendanceOutcome`) are modeled with explicit state representations and absence-based initial conditions.
2. **Dual-Role Non-Exclusive Personas (CHANGE-001)**: Users may hold both `ChiefLeader` and `Leader` roles simultaneously. Mutual exclusivity is enforced against **current operational participation** rather than through global role locks or historical records.
3. **Staffing Context & Reschedule Integrity**: An explicit `StaffingCycle` entity encapsulates the staffing revision boundary. Material date reschedules archive prior cycles and reopen availability cleanly without destructive in-place mutations.
4. **Schema-per-Module Namespace Organization (ADR-009)**: Entities are logically partitioned into SQL Server schemas representing module write boundaries (`identity`, `events`, `staffing`, `attendance`, `leader`, `notifications`, `audit`).
5. **Single Unit of Work / Cross-Schema Relational Integrity**: A single host database coordinates all schemas. Cross-module relationships are maintained via physical foreign keys with `ON DELETE NO ACTION` to guarantee historical data preservation.
6. **Strict Scope Control**: Only ratified requirements are fully modeled. Unresolved feature details (detailed profile fields, feedback rubrics, incident categories, invitation workflows) are explicitly marked as **DEFERRED TO FEATURE SPEC**.

---

## 2. Logical Entity Inventory by Schema

| Schema | Logical Entity | Boundary Classification | Owning Module | Lifecycle / Purpose |
| :--- | :--- | :--- | :--- | :--- |
| **`identity`** | **`User`** | Domain Entity | Identity & Access | Authenticated human identity, credentials, active status (`IdentityUser<Guid>`) |
| **`identity`** | **`Role`** | Domain Entity | Identity & Access | RBAC role classification (`Admin`, `ChiefLeader`, `Leader`) |
| **`identity`** | **`UserRole`** | Domain Join Entity | Identity & Access | Multi-role assignment mapping per CHANGE-001 |
| **`identity`** | *Identity Framework Records* | Framework Infrastructure | Identity & Access | ASP.NET Core Identity claims, logins, tokens |
| **`leader`** | **`LeaderProfile`** | Domain Entity | LeaderProfiles | Volunteer operational profile details |
| **`events`** | **`Event`** | Domain Aggregate Root | Events | Master event gathering, scheduled window, execution state |
| **`events`** | **`ChiefLeaderAssignment`** | Domain Entity | Events | Appointment, acceptance/decline workflow, operational ownership |
| **`events`** | **`StaffingCycle`** | Domain Entity | Events | Scoped staffing revision boundary; rotated on date reschedule |
| **`staffing`** | **`AvailabilityResponse`** | Domain Entity | Staffing | Dimension A: Volunteer declared schedule availability |
| **`staffing`** | **`TeamAssignment`** | Domain Entity | Staffing | Dimension B: Active roster placement or cancelled assignment |
| **`attendance`**| **`AttendanceOutcome`** | Domain Entity | Attendance | Dimension C: Factual physical service outcome (`Attended`, `NoShow`) |
| **`notifications`**| **`InPlatformNotification`**| Domain Entity | Notifications | In-platform alerting, read state, contextual reference (ADR-006) |
| **`audit`** | **`AuditLog`** | Cross-Cutting Entity | Audit Persistence | Append-only immutable audit trail and mandatory justification notes (ADR-005) |
| **`engagement`** | **`EventFeedback`** | Domain Entity | Engagement (`SLICE-21`) | Post-event 1–5 ratings (`Chief Leader -> Leader`, `Leader -> Event`) with optional comment |
| **`engagement`** | **`IncidentReport`** | Domain Entity | Engagement (`SLICE-22`) | Event-scoped incident log with Admin review and optional review note |

---

## 3. Logical Entity Specifications

### 3.1 Identity Schema

#### `identity.User`
* **Purpose**: Authoritative record of an authenticated user.
* **Ownership**: `Identity & Access` module.
* **Key Attributes**:
  * `Id` (GUID, PK): Unique identifier. Configured as `uniqueidentifier` (`IdentityUser<Guid>`) overriding framework default string keys.
  * `UserName` / `NormalizedUserName` (String, Unique): System username.
  * `Email` / `NormalizedEmail` (String, Unique): Verified contact email.
  * `EmailConfirmed` (Boolean): Verification marker.
  * `PasswordHash` / `SecurityStamp` / `ConcurrencyStamp` (String): Identity infrastructure security tokens.
  * `IsActive` (Boolean): Administrative account activation flag. Deactivation revokes login privileges without destroying historical references.
  * `CreatedAt` (DateTimeOffset): Account creation audit timestamp.
* **Invariants**:
  * Email must be unique across all active and inactive users.
  * Historical records pointing to a `UserId` are preserved as historical records and not destructively overwritten during normal business workflows. Eventual retention/archive/deletion policy remains unresolved.

#### `identity.Role`
* **Purpose**: System security roles defining coarse-grained authorization boundaries.
* **Ownership**: `Identity & Access` module.
* **Key Attributes**:
  * `Id` (GUID, PK): Unique role identifier.
  * `Name` / `NormalizedName` (String, Unique): Fixed system roles: `Admin`, `ChiefLeader`, `Leader`.
* **Invariants**:
  * V1 supports exactly three system roles.
  * Chief Leaders are a predefined organizational pool; role membership is managed strictly by Admins. There is no automated qualification workflow (`IsChiefLeaderQualified` does not exist).

#### `identity.UserRole`
* **Purpose**: Associative join entity linking users to roles.
* **Ownership**: `Identity & Access` module.
* **Key Attributes**:
  * `UserId` (GUID, Composite PK, FK to `identity.User`).
  * `RoleId` (GUID, Composite PK, FK to `identity.Role`).
* **Invariants (CHANGE-001 Compliance)**:
  * A user may hold both `ChiefLeader` and `Leader` roles simultaneously.
  * Holding `ChiefLeader` does NOT automatically imply or grant the `Leader` role.

#### *Identity Framework Infrastructure Entities*
* `identity.UserClaims`, `identity.UserLogins`, `identity.UserTokens`, `identity.RoleClaims`.
* Managed directly by ASP.NET Core Identity for cookie authentication, token issuance, and password resets. Classified strictly as framework infrastructure.

---

### 3.2 Leader Profile Schema

#### `leader.LeaderProfile`
* **Purpose**: Volunteer domain profile containing operational contact and biographical details.
* **Ownership**: `LeaderProfiles` module.
* **Key Attributes**:
  * `Id` (GUID, PK): Profile identifier.
  * `UserId` (GUID, Unique, FK to `identity.User`): 1:1 relationship with User account.
  * `CreatedAt` (DateTimeOffset): Creation timestamp.
  * `UpdatedAt` (DateTimeOffset, Nullable): Last update timestamp.
  * *Unresolved Profile Attributes*: Phone numbers, emergency contacts, shirt sizes, dietary constraints, qualifications/skills $\rightarrow$ **DEFERRED TO FEATURE SPEC**.
* **Design Rule on Participation Metrics**:
  * `LeaderProfiles` owns profile attributes only.
  * Factual historical participation metrics (events attended, cancellations, no-shows) are **NOT stored as denormalized counters** on `LeaderProfile`. They are dynamically derived from authoritative operational records (`attendance.AttendanceOutcomes`, `staffing.TeamAssignments`) or dedicated read-model projections when required.

---

### 3.3 Events Schema

#### `events.Event`
* **Purpose**: Aggregate root representing a scheduled gathering requiring volunteer staffing.
* **Ownership**: `Events` module.
* **Key Attributes**:
  * `Id` (GUID, PK): Unique event identifier.
  * `Title` (String, Max 200): Operational title.
  * `Status` (String / Enum): Persisted lifecycle status: `Draft`, `Published`, `Completed`, `Cancelled`.
  * `StartDateTime` (DateTimeOffset): Scheduled event start window with timezone offset.
  * `EndDateTime` (DateTimeOffset): Scheduled event conclusion window with timezone offset.
  * `RowVersion` (RowVersion / Byte Array): Optimistic concurrency token.
  * `CreatedAt` (DateTimeOffset): Audit creation timestamp.
  * `CreatedByUserId` (GUID, FK to `identity.User`): Creating Administrator.
  * *Unresolved Event Attributes*: Detailed descriptions, location coordinates, meeting URLs, dress codes, staffing capacity targets $\rightarrow$ **DEFERRED TO EVENT MANAGEMENT FEATURE SPEC**.
* **Invariants & State Semantics**:
  * `EndDateTime > StartDateTime`.
  * Valid persisted states: `Draft`, `Published`, `Completed`, `Cancelled`.
  * **Non-Persisted Derived Conditions**:
    * `In Progress`: Dynamically evaluated when `Status == Published` and `StartDateTime <= CurrentTime <= EndDateTime`.
    * `Pending Attendance`: Dynamically evaluated when `Status == Completed` and one or more CURRENT operational team assignments lack a recorded `AttendanceOutcome`.
    * `Fully Staffed`: Dynamically evaluated against staffing targets.
    * `Rescheduled`: Operational event, not a persistent status.
  * **Publishing Prerequisite**: An event cannot transition from `Draft` to `Published` unless it possesses exactly one `Active` accepted `ChiefLeaderAssignment`.
  * **Completion Mechanics (ADR-007)**: Transitions from `Published` to `Completed` via guarded `BackgroundService` execution when `CurrentTime > EndDateTime` OR via explicit Admin closeout. Guarded optimistic concurrency prevents duplicate processing.

#### `events.ChiefLeaderAssignment`
* **Purpose**: Manages the formal appointment, acceptance/decline workflow, and operational ownership of an event Chief Leader.
* **Ownership**: `Events` module.
* **Key Attributes**:
  * `Id` (GUID, PK): Assignment identifier.
  * `EventId` (GUID, FK to `events.Event`): Associated event.
  * `ChiefLeaderUserId` (GUID, FK to `identity.User`): Target Chief Leader candidate.
  * `Status` (String / Enum): Persisted status: `Pending`, `Active`, `Declined`.
  * `AppointedAt` (DateTimeOffset): Timestamp of Admin appointment.
  * `AppointedByUserId` (GUID, FK to `identity.User`): Acting Administrator.
  * `AcceptedAt` (DateTimeOffset, Nullable): Timestamp of acceptance.
  * `AcceptanceSource` (String, Nullable): `Direct` (in-platform acceptance) or `AdminExternal` (Admin recorded externally confirmed acceptance).
  * `AcceptedByUserId` (GUID, FK to `identity.User`, Nullable): Acting user (`NULL` if Direct acceptance by Chief Leader; Admin User ID if AdminExternal).
  * `DeclinedAt` (DateTimeOffset, Nullable): Timestamp if declined.
  * `RowVersion` (RowVersion / Byte Array): Optimistic concurrency token.
* **Invariants**:
  * Candidate must belong to the predefined Chief Leader pool.
  * At most one `Active` assignment may exist per event at any time (enforced via SQL Server filtered unique index).
  * **Domain Acceptance Provenance**: `AcceptanceSource`, `AcceptedAt`, and `AcceptedByUserId` preserve whether the Chief Leader accepted directly or via Admin external confirmation as authoritative domain data on the assignment record.
  * If `AcceptanceSource == AdminExternal`, an audit record in `audit.AuditLogs` with a mandatory justification note is strictly required.
  * Declined assignments are preserved as historical records and not destructively overwritten during normal business workflows. Re-appointment creates a new assignment record in `Pending` status.
  * Emergency reassignment/replacement of an already active Chief Leader post-publication is **DEFERRED TO EVENT MANAGEMENT FEATURE SPEC**.

#### `events.StaffingCycle` (Staffing Context Architecture)
* **Purpose**: Represents an immutable staffing revision boundary. Scopes volunteer availability declarations and team roster assignments to a specific scheduled event date.
* **Ownership**: `Events` module.
* **Key Attributes**:
  * `Id` (GUID, PK): Staffing cycle identifier.
  * `EventId` (GUID, FK to `events.Event`): Associated event.
  * `CycleNumber` (Integer): Monotonically increasing revision sequence (1, 2, ...).
  * `IsCurrent` (Boolean): Flag indicating whether this is the currently active staffing window for the event.
  * `EffectiveEventDate` (Date): The specific calendar date to which this staffing context applies.
  * `CreatedAt` (DateTimeOffset): Timestamp of cycle creation.
* **Cardinality & Current Applicability Rules**:
  * **Database Invariant**: SQL Server filtered unique index `(EventId) WHERE IsCurrent = 1` guarantees **at most one** current staffing cycle per event.
  * **Domain Transaction Rule**: The domain ensures **at least one** current staffing cycle exists once an Event is Published. (Draft events do not strictly require a staffing cycle until publication opens cycle 1).
  * **Current Operational Scope**:
    * `CurrentOperationalAssignment = Active TeamAssignment + Current StaffingCycle (IsCurrent = true)`.
    * `CurrentOperationalAvailability = AvailabilityResponse + Current StaffingCycle (IsCurrent = true)`.
  * **Date Reschedule Mechanics**:
    1. An Admin changes the calendar date of a `Published` event.
    2. The domain transaction marks the existing cycle `IsCurrent = false`.
    3. A new `StaffingCycle` is inserted with `CycleNumber = @old + 1`, `EffectiveEventDate = @newDate`, and `IsCurrent = true`.
    4. Prior assignments and availability remain historically intact linked to the sealed cycle. They are no longer part of the current operational roster, avoiding destructive status updates.
    5. Availability for the new cycle automatically begins with `No AvailabilityResponse` for all Leaders.

---

### 3.4 Staffing Schema

#### `staffing.AvailabilityResponse` (Dimension A)
* **Purpose**: Captures a volunteer Leader's declared schedule availability for an event's staffing cycle.
* **Ownership**: `Staffing` module.
* **Key Attributes**:
  * `Id` (GUID, PK): Response identifier.
  * `StaffingCycleId` (GUID, FK to `events.StaffingCycle`): Scoped staffing cycle.
  * `LeaderUserId` (GUID, FK to `identity.User`): Responding volunteer Leader.
  * `Status` (String / Enum): `Available`, `NotAvailable`.
  * `SubmittedAt` (DateTimeOffset): Timestamp of declaration.
  * `UpdatedAt` (DateTimeOffset, Nullable): Timestamp of last change.
* **Invariants**:
  * "No Record" represents the absence of a row. When an event is published or a new staffing cycle opens, Leaders have no row (presented in UI as "Response Needed").
  * Unique constraint on `(StaffingCycleId, LeaderUserId)` ensures at most one response per Leader per staffing cycle.
  * Volunteers may freely update their response between `Available` and `NotAvailable` while the event availability window remains open.
  * Direct team assignment by leadership does NOT mutate or delete the Leader's `AvailabilityResponse`.

#### `staffing.TeamAssignment` (Dimension B)
* **Purpose**: Records leadership's operational appointment of a specific Leader to an event team roster slot.
* **Ownership**: `Staffing` module.
* **Key Attributes**:
  * `Id` (GUID, PK): Assignment identifier.
  * `StaffingCycleId` (GUID, FK to `events.StaffingCycle`): Scoped staffing cycle.
  * `LeaderUserId` (GUID, FK to `identity.User`): Assigned volunteer Leader.
  * `Status` (String / Enum): `Active`, `Cancelled`.
  * `AssignedAt` (DateTimeOffset): Timestamp of placement.
  * `AssignedByUserId` (GUID, FK to `identity.User`): Chief Leader or Admin who performed selection/drafting.
  * `CancelledAt` (DateTimeOffset, Nullable): Timestamp of cancellation.
  * `CancelledByUserId` (GUID, FK to `identity.User`, Nullable): Actor who cancelled assignment.
  * `CancellationReason` (String, Max 500, Nullable): Recorded reason (mandatory for Leader self-cancellation).
  * `RowVersion` (RowVersion / Byte Array): Optimistic concurrency token.
* **Invariants**:
  * "No Record" represents an unstaffed Leader. Selection or direct drafting immediately creates an `Active` record.
  * A Leader may hold at most one `Active` assignment within a given staffing cycle (enforced via SQL Server filtered unique index).
  * A `Cancelled` assignment is never deleted, reactivated, or overwritten in place.
  * Replacement flow creates a **new distinct `TeamAssignment`** in `Active` status for the replacement Leader.
  * *Conditional Cancellation Reason*: `CancellationReason` is nullable. It is strictly enforced by domain guards for Leader self-cancellation, but is not globally mandatory for all cancellation pathways (such as event cancellation).
  * *Lineage Evaluation*: Tracking explicit pairwise replacement lineage (`ReplacesTeamAssignmentId`) is omitted because ratified requirements require slot-based replacement and historical preservation, but do not define causal replacement tracking.

---

### 3.5 Attendance Schema

#### `attendance.AttendanceOutcome` (Dimension C)
* **Purpose**: Factual ground-truth record of physical event service for an assigned volunteer.
* **Ownership**: `Attendance` module.
* **Key Attributes**:
  * `Id` (GUID, PK): Outcome identifier.
  * `TeamAssignmentId` (GUID, Unique, FK to `staffing.TeamAssignment`): Strictly bound 1:1 to a team assignment.
  * `Outcome` (String / Enum): `Attended`, `NoShow`.
  * `FinalizedAt` (DateTimeOffset, Nullable): Timestamp of initial finalization by Chief Leader.
  * `FinalizedByUserId` (GUID, FK to `identity.User`, Nullable): Chief Leader who recorded roll call.
  * `CorrectedAt` (DateTimeOffset, Nullable): Timestamp of administrative correction.
  * `CorrectedByUserId` (GUID, FK to `identity.User`, Nullable): Acting Administrator.
  * `RowVersion` (RowVersion / Byte Array): Optimistic concurrency token.
* **Absence Modeling & Invariants**:
  * **Pure Absence Semantics**: There is **no persisted `Unrecorded` enum value**. An unrecorded attendance outcome is represented strictly by the **absence of an `AttendanceOutcome` row** for a team assignment.
  * A row exists only once attendance is finalized (`Attended` or `NoShow`).
  * At most one `AttendanceOutcome` per `TeamAssignment` (enforced via unique constraint on `TeamAssignmentId`).
  * **Derived Condition (`Pending Attendance`)**: Evaluated when `Event.Status == Completed` and one or more CURRENT operational team assignments (`TeamAssignment.Status == Active` where `StaffingCycle.IsCurrent == true`) have no associated `AttendanceOutcome` row.
  * Chief Leader has primary finalization authority; submission takes immediate effect without an administrative review gate.
  * Admin corrections record an immutable entry in `audit.AuditLogs` capturing old outcome, new outcome, and mandatory justification note.
  * Decoupled from Event completion: Missing attendance does not stall event completion.

---

### 3.6 Notifications Schema

#### `notifications.InPlatformNotification`
* **Purpose**: Persisted semantic in-platform notification records (ADR-006).
* **Ownership**: `Notifications` module.
* **Key Attributes**:
  * `Id` (GUID, PK): Notification identifier.
  * `RecipientUserId` (GUID, FK to `identity.User`): Target user.
  * `EventId` (GUID, FK to `events.Event`, Nullable): Contextual event reference.
  * `NotificationType` (String, Max 50): Semantic notification code (e.g., `ChiefLeaderAppointment`, `RosterPlacement`, `StaffingCancellation`, `ScheduleAdjustment`).
  * `CreatedAt` (DateTimeOffset): Dispatch timestamp.
  * `ReadAt` (DateTimeOffset, Nullable): Timestamp when read. `ReadAt IS NULL` signifies an **unread** notification.
  * `ContextPayload` (String, Max 500 / Bounded JSON, Nullable): Constrained key-value parameters supporting client-side localized rendering (e.g., `{"EventTitle":"Annual Gala"}`). Not an arbitrary document store.
* **Invariants**:
  * `ReadAt IS NULL OR ReadAt >= CreatedAt`.
  * Redundant `IsRead` boolean is omitted (`ReadAt IS NULL` is authoritative).
  * `IsDismissed` is omitted (notification dismissal has not been approved in product requirements).
  * Hardcoded English display titles and bodies are omitted from the data model; client applications render localized UI text based on `NotificationType` and `ContextPayload`.
  * External channels (SMS, WhatsApp, native OS push) are strictly excluded from V1.

---

### 3.7 Audit Schema

#### `audit.AuditLog`
* **Purpose**: Immutable, tamper-resistant record of critical business operations, administrative overrides, and mandatory justifications (ADR-005).
* **Ownership**: Cross-cutting / Audit Persistence.
* **Key Attributes**:
  * `Id` (GUID, PK): Audit record identifier.
  * `OccurredAt` (DateTimeOffset): Accurate UTC timestamp.
  * `ActorUserId` (GUID, FK to `identity.User`, Nullable): Acting user ID (`NULL` indicates automated system action, e.g., `BackgroundService`).
  * `Action` (String, Max 100): Operation identifier (e.g., `Events.EventPublished`, `Events.EventRescheduled`, `ChiefLeader.ExternalAcceptanceRecorded`, `Staffing.AssignmentCancelled`, `Attendance.OutcomeCorrected`).
  * `ResourceType` (String, Max 50): Target entity classification (`Event`, `ChiefLeaderAssignment`, `TeamAssignment`, `AttendanceOutcome`).
  * `ResourceId` (GUID): Primary key of the affected entity.
  * `CorrelationId` (GUID): Request correlation identifier tracing the HTTP command or background execution context.
  * `MandatoryReason` (String, Max 1000, Nullable): Recorded text justification. Strictly required for reason-mandated actions (self-cancellation, admin external acceptance, admin attendance correction, event cancellation).
  * `Source` (String, Max 50): Origin mechanism (`Direct`, `AdminExternal`, `AdminOverride`, `SystemWorker`).
  * `PreviousValue` (String, Max / JSON, Nullable): Bounded representation of previous state for altered fields.
  * `NewValue` (String, Max / JSON, Nullable): Bounded representation of new state for altered fields.
* **Invariants**:
  * Append-only: Normal business workflows never edit or delete historical audit records. (Database-level login permissions restricting `UPDATE`/`DELETE` represent a deployment/security hardening candidate consistent with ADR-005).
  * Security guard: Audit logs must never store passwords, password hashes, security stamps, tokens, cookies, or unnecessary PII.
  * Commits atomically within the same database transaction as the underlying domain mutation.

---

### 3.8 Engagement Schema (Ratified per [CHANGE-003](../decisions/CHANGE-003-v1-remaining-slice-policy.md))

#### `engagement.EventFeedback` (`SLICE-21`)
* **Purpose**: Captures post-event 1–5 ratings submitted after an Event reaches `Completed` status.
* **Key Attributes**: `Id`, `EventId`, `RaterUserId`, `LeaderUserId` (nullable when `Kind = 'Event'`), `Kind` (`'Event'` | `'Leader'`), `Rating` (`1..5`), `Comment` (optional, max 500 chars), `SubmittedAt`.
* **Invariants**: Active Chief Leader rates active assigned Leaders on the current staffing cycle (`Kind = 'Leader'`); active assigned Leaders rate the Event (`Kind = 'Event'`). Unique per `(EventId, RaterUserId, Kind, LeaderUserId)`. Visible to Admins.

#### `engagement.IncidentReport` (`SLICE-22`)
* **Purpose**: Captures basic event incident reports and Admin review state.
* **Key Attributes**: `Id`, `EventId`, `ReporterUserId`, `Title` (1–200 chars), `Description` (1–4000 chars), `OccurredAt` (UTC `<= ReportedAt`), `ReportedAt`, `ReviewedAt` (nullable), `ReviewedByUserId` (nullable), `ReviewNote` (optional, max 1000 chars), `RowVersion`.
* **Invariants**: Reporters see their own reports; active Chief Leaders see reports for Events they lead; Admins see all reports and mark them reviewed once with an optional note.

---

## 4. Cross-Table Invariants & CHANGE-001 Dual-Role Modeling

### 4.1 Same-Event Exclusivity Invariant
Per ratified [CHANGE-001](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md), a user may hold both `ChiefLeader` and `Leader` roles in `identity.UserRole`. However, operational responsibility on any single event is mutually exclusive:
$$\text{ActiveChiefLeaderAssignment}(E, U) \implies \neg\text{CurrentOperationalTeamAssignment}(E, U)$$

### 4.2 Current Staffing Context Boundary
* The invariant applies strictly to **CURRENT operational participation**:
  * An `Active` `ChiefLeaderAssignment` on Event $E$ prohibits an `Active` `TeamAssignment` attached to $E$'s **current** `StaffingCycle` (`IsCurrent = true`).
  * It does **not** conflict with historical assignments attached to older, sealed `StaffingCycles` (`IsCurrent = false`) from prior dates.
* **Enforcement Strategy**:
  1. **Candidate Pool Exclusion**: Read queries for the candidate pool on Event $E$ automatically filter out the user holding the active `ChiefLeaderAssignment` on Event $E$.
  2. **Assignment Command Guard**:
     - `SelectCandidate` and `DirectDraft` verify that the target user does not hold an `Active` `ChiefLeaderAssignment` for Event $E$.
  3. **Activation Command Guard**:
     - `AcceptChiefLeaderAssignment` and `RecordExternalAcceptance` verify:
       `SELECT COUNT(1) FROM staffing.TeamAssignments ta INNER JOIN events.StaffingCycles sc ON ta.StaffingCycleId = sc.Id WHERE sc.EventId = @EventId AND sc.IsCurrent = 1 AND ta.LeaderUserId = @ChiefLeaderUserId AND ta.Status = 'Active' == 0`.
     - If an active team assignment exists on the current cycle, activation is rejected until that assignment is resolved.
  4. Enforced via Application/Domain Transaction Guards inside the single host database transaction.
