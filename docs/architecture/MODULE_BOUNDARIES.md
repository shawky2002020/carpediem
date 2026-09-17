# Modular Monolith Architecture & Module Boundaries

> [!IMPORTANT]
> **Status: PROPOSED ARCHITECTURAL BASELINE**
> **Design Paradigm**: Domain-Driven **Modular Monolith** in ASP.NET Core 10.
> **Enforcement Rule**: Logical modules maintain strict encapsulation, explicit public API contracts, and dedicated transaction boundaries within a single deployable unit. Direct cross-module internal coupling is prohibited. Authoritative mutations must pass through the owning module; cross-module read models may combine data in a controlled manner.

---

## 1. Architectural Principles & Boundary Rules

1. **Single Deployable, Strict Logical Encapsulation [LOCKED]**:
   - The entire backend platform runs as a single deployable ASP.NET Core 10 application (`Carpediem.Web`), with business logic organized into cohesive, bounded modules under `src/Modules/`.
2. **Contract-Based Cross-Module Communication [PROPOSED]**:
   - Modules interact synchronously strictly via explicit application service interfaces (public contracts) or asynchronously via in-process domain events.
   - A module NEVER directly mutates the internal entities or bypasses the domain logic of another module.
3. **Transactional Integrity & Aggregates [LOCKED]**:
   - Each module controls its own aggregate boundaries and write-side consistency.
   - Cross-module transactions are minimized. When a workflow spans boundaries, the owning module validates prerequisites through public contract queries or in-process coordination.
4. **Database Access & Schema Strategy [PROPOSED / PENDING ADR-009]**:
   - Modules must not bypass another module's business rules for writes.
   - Authoritative mutations go exclusively through the owning module's commands.
   - Cross-module read models and operational projections may query and combine data in a controlled, read-only manner.
   - Exact SQL Server schema organization (e.g., schema-per-module vs. shared schema with naming conventions) will be evaluated and finalized in ADR-009.

---

## 2. High-Level Module Structure & Dependency Graph

```
                               ┌───────────────────────────┐
                               │   OperationsDashboard     │
                               │      (Read Projections)   │
                               └─────────────┬─────────────┘
                                             │ Reads
                   ┌─────────────────────────┼─────────────────────────┐
                   ▼                         ▼                         ▼
         ┌───────────────────┐               │               ┌───────────────────┐
         │      Events       │               │               │     Staffing      │
         │  (StaffingCycle + │◄──────────────┼───────────────┤ (Avail + Assign)  │
         │ ChiefLeaderAssign)│               │               └─────────┬─────────┘
         └─────────┬─────────┘               │                         │
                   │                         │                         ▼
                   │                         │               ┌───────────────────┐
                   │                         │               │    Attendance     │
                   │                         │               │ (Outcome & Audit) │
                   │                         │               └─────────┬─────────┘
                   │                         │                         │
                   │                         ▼                         │
                   │               ┌───────────────────┐               │
                   │               │  LeaderProfiles   │◄──────────────┤
                   │               │ (Profile Maint.)  │ (Read Events) │
                   │               └─────────┬─────────┘               │
                   │                         │                         │
                   ▼                         ▼                         ▼
         ┌───────────────────┐     ┌───────────────────┐     ┌───────────────────┐
         │     Incidents     │     │ Identity & Access │     │   Feedback /      │
         │ (Disruption Logs) │────►│   (Users & RBAC)  │◄────┤   Evaluations     │
         └───────────────────┘     └─────────▲─────────┘     └───────────────────┘
                                             │
                                   ┌─────────┴─────────┐
                                   │   Notifications   │
                                   │ (In-Platform Msg) │
                                   └───────────────────┘
```

### Dependency Cycle Elimination Confirmation
- **Previous Cycle Removed**: In the prior draft, `Events` depended on `ChiefLeaderAssignment`, while `ChiefLeaderAssignment` depended on `Events`. By consolidating `ChiefLeaderAssignment` into the `Events` module, the circular dependency is completely eliminated.
- **Strict Acyclic Graph (DAG)**:
  - `Identity & Access` is the independent root foundation (zero upstream dependencies).
  - `Events` depends only on `Identity & Access`.
  - `Staffing` depends on `Events` and `Identity & Access`.
  - `Attendance` depends on `Staffing`, `Events`, and `Identity & Access`.
  - `LeaderProfiles`, `Feedback`, `Incidents`, and `Notifications` depend only downward.
  - `OperationsDashboard` acts as a pure consumer of read models.

---

## 3. Logical Module Specifications

### Module 1: Identity & Access (`Carpediem.Modules.Identity`)
- **Status**: **LOCKED** (Core concepts) / **UNRESOLVED** (Invitation & account onboarding details)
- **Responsibility**:
  - Manages authenticated human identities, credentials, role assignments (`Admin`, `Chief Leader`, `Leader`), and account activation tokens.
  - Enforces server-side authentication and role-based claim evaluation.
  - Maintains the locked business invariant: Chief Leaders belong to a predefined known pool; there is no system qualification workflow.
- **Owns**:
  - `User`, `Role`, `UserRole`, `AccountActivationToken`, `AuditCredentialRecord`.
- **Does NOT Own**:
  - Volunteer profile details (owned by `LeaderProfiles`).
  - Event assignments or operational leadership ownership (owned by `Events`).
- **Dependencies**:
  - None (Foundation module).
- **Commands Exposed**:
  - `ProvisionUserAccount(email, role, initialDetails)`
  - `ActivateAccount(token, password)`
  - `UpdateUserRole(userId, newRole)`
- **Read / Query Contracts**:
  - `GetUserById(userId): UserDto?`
  - `GetChiefLeaderPool(): IReadOnlyList<UserDto>`
  - `IsUserInChiefLeaderPool(userId): bool`
  - `HasRole(userId, roleName): bool`
- **Emitted Events**:
  - `UserAccountProvisioned`, `UserAccountActivated`, `UserRoleChanged`.
- **Important Invariants [LOCKED]**:
  - Exactly three distinct roles in V1 (`Admin`, `Chief Leader`, `Leader`).
  - Predefined Chief Leader pool is managed strictly by Admins; no self-service promotion or system qualification.
  - **Non-Exclusive Roles ([CHANGE-001](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md))**: Role membership is non-exclusive; a user belonging to the predefined Chief Leader pool may also hold the `Leader` role if granted by an Admin. Dual-role users can participate as normal Leaders on non-led events.

---

### Module 2: Events & Coordination (`Carpediem.Modules.Events`)
- **Status**: **LOCKED** (Lifecycle, Invariants, Chief Leader flow) / **UNRESOLVED** (Mandatory vs. optional fields, post-publish Chief Leader replacement)
- **Responsibility**:
  - Manages event creation, location metadata, schedule windows (`StartTime`, `EndTime`), and the formal **Event Lifecycle** (`Draft`, `Published`, `Completed`, `Cancelled`).
  - Owns the **`StaffingCycle` (Schedule Revision)** aggregate: handles material date changes, archives historical cycles immutably, and triggers cycle resets.
  - Owns the **`ChiefLeaderAssignment`** lifecycle (`Pending` $\rightarrow$ `Active` / `Declined`): manages Chief Leader appointment, notification, acceptance, decline, and external confirmation audit logging.
- **Owns**:
  - `Event`, `EventSchedule`, `StaffingCycle`, `EventCancellationAudit`.
  - `ChiefLeaderAssignment`, `ExternalAcceptanceAuditRecord`.
- **Does NOT Own**:
  - Volunteer availability responses or team roster slots (owned by `Staffing`).
  - Attendance recording or roll call (owned by `Attendance`).
- **Dependencies**:
  - `Carpediem.Modules.Identity` (validates acting Admin; validates Chief Leader is in predefined pool).
- **Commands Exposed**:
  - `CreateEvent(title, startTime, endTime, location, adminId)`
  - `AppointChiefLeader(eventId, chiefLeaderUserId, adminId)`
  - `AcceptChiefLeaderAssignment(assignmentId, chiefLeaderUserId)`
  - `DeclineChiefLeaderAssignment(assignmentId, chiefLeaderUserId)`
  - `RecordExternalChiefLeaderAcceptance(assignmentId, adminId, mandatoryReason)`
  - `PublishEvent(eventId, adminId)`
  - `RescheduleEvent(eventId, newStartTime, newEndTime, adminId, reason)`
  - `CancelEvent(eventId, adminId, mandatoryReason)`
- **Read / Query Contracts**:
  - `GetEvent(eventId): EventDto?`
  - `GetCurrentStaffingCycle(eventId): StaffingCycleDto`
  - `GetActiveChiefLeaderAssignment(eventId): ChiefLeaderAssignmentDto?`
  - `HasActiveChiefLeaderAssignment(eventId): bool`
  - `IsActiveChiefLeader(eventId, userId): bool`
- **Emitted Events**:
  - `EventCreated`, `ChiefLeaderAppointed`, `ChiefLeaderAccepted`, `ChiefLeaderDeclined`, `EventPublished`, `EventRescheduled`, `EventCancelled`.
- **Important Invariants [LOCKED]**:
  - **Publishing Prerequisite**: An Event cannot transition from `Draft` to `Published` unless there is an accepted, `Active` Chief Leader assignment (`Event.Status == Published => Event has exactly one active accepted ChiefLeaderAssignment`).
  - **Single Active Chief Leader**: An event may have at most one `Active` Chief Leader assignment at any time.
  - **Same-Event Exclusivity Invariant ([CHANGE-001](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md))**: If User $U$ has an `Active` `ChiefLeaderAssignment` for Event $E$, $U$ cannot hold an `Active` `TeamAssignment` on $E$. Any active team assignment for $U$ on $E$ must be resolved before a `Pending` Chief Leader assignment can transition to `Active`.
  - **External Acceptance Audit**: Recording external acceptance strictly logs acting Admin, timestamp, Chief Leader, Event, and mandatory justification; audit history distinguishes direct from external acceptance.
  - **Material Reschedule Rule**: Date changes seal the active `StaffingCycle`, invalidate old availability, open a fresh cycle, and preserve history immutably.
  - **Decoupled Completion**: Event completion is driven by time/closeout, decoupled from attendance.

---

### Module 3: Staffing & Participation (`Carpediem.Modules.Staffing`)
- **Status**: **LOCKED** (Lifecycles, selection authority, direct drafting, self-cancellation) / **UNRESOLVED** (Late cancellation timing thresholds)
- **Responsibility**:
  - Encapsulates volunteer availability pooling and event team rosters:
    - **Dimension A (`AvailabilityResponse`)**: Volunteer declared schedule availability (`Available` $\leftrightarrow$ `NotAvailable`, absence modeled as `No AvailabilityResponse`).
    - **Dimension B (`TeamAssignment`)**: Official team placements (`Active` $\rightarrow$ `Cancelled`, absence modeled as `No TeamAssignment`).
  - Governs Chief Leader team selection, direct drafting of unassigned Leaders, controlled self-cancellations with mandatory reasons, and direct replacement selection.
- **Owns**:
  - `AvailabilityResponse`, `TeamAssignment`, `AssignmentCancellationAudit`, `ReplacementLink`.
- **Does NOT Own**:
  - Event scheduling or Chief Leader appointment (owned by `Events`).
  - Physical attendance outcomes (`Attended` / `NoShow`) (owned by `Attendance`).
  - Factual aggregate metric storage (metrics are derived from this module's records).
- **Dependencies**:
  - `Carpediem.Modules.Events` (binds staffing to active `StaffingCycle`; queries active Chief Leader for selection authorization).
  - `Carpediem.Modules.Identity` (validates Leader identities).
- **Commands Exposed**:
  - `SetAvailability(eventId, leaderUserId, isAvailable)`
  - `SelectCandidate(eventId, leaderUserId, chiefLeaderUserId)`
  - `DirectDraftLeader(eventId, leaderUserId, actingUserId)`
  - `SelfCancelAssignment(assignmentId, leaderUserId, mandatoryReason)`
  - `SelectReplacement(cancelledAssignmentId, replacementLeaderUserId, chiefLeaderUserId)`
  - `AdminCancelAssignment(assignmentId, adminUserId, mandatoryReason)`
- **Read / Query Contracts**:
  - `GetActiveTeamRoster(eventId): IReadOnlyList<TeamAssignmentDto>`
  - `GetAvailableCandidatePool(eventId): IReadOnlyList<CandidateDto>`
  - `GetLeaderParticipationHistory(leaderUserId): IReadOnlyList<ParticipationRecordDto>`
- **Emitted Events**:
  - `AvailabilityDeclared`, `TeamMemberSelected`, `LeaderDirectlyDrafted`, `AssignmentSelfCancelled`, `ReplacementSelected`.
- **Important Invariants [LOCKED]**:
  - **Immediate Confirmation**: Selection and direct drafting create an immediate `Active` assignment (no separate confirmation step in V1).
  - **Self-Cancellation Guard**: Requires explicit confirmation and mandatory reason text; notifies Chief Leader and Admin.
  - **Immutable Replacements**: A cancelled assignment is never overwritten or deleted. Selecting a replacement creates a *new* `TeamAssignment` in `Active` status.
  - **Direct Replacement Authority**: Chief Leader directly selects replacements from available candidates when permitted.
  - **Same-Event Exclusivity Guard ([CHANGE-001](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md))**: If User $U$ has an active Chief Leader assignment on Event $E$, $U$ cannot hold an `Active` `TeamAssignment` on $E$, and is excluded from candidate selection. Dual-role users holding the `Leader` role may participate as normal Leaders on any event where they are not the active Chief Leader. No additional restriction has been defined for a Pending ChiefLeaderAssignment. Behavior remains governed by existing Leader participation rules until activation. However, if the user currently has an Active TeamAssignment for the same Event, that TeamAssignment must be resolved before the Pending ChiefLeaderAssignment may transition to Active.

---

### Module 4: Attendance Recording (`Carpediem.Modules.Attendance`)
- **Status**: **LOCKED** (Immediate finalization, Admin audited corrections, decoupled completion)
- **Responsibility**:
  - Manages **Dimension C (`AttendanceOutcome`)**: factual on-site roll call and service recording (`Unrecorded` $\rightarrow$ `Attended` / `NoShow`).
  - Supports Chief Leader immediate finalization on-site without an administrative approval queue.
  - Supports Admin post-event audited corrections with mandatory recorded justification.
  - Evaluates the derived operational condition: **`Pending Attendance`**.
- **Owns**:
  - `AttendanceOutcome`, `AttendanceRollSheet`, `AttendanceCorrectionAudit`.
- **Does NOT Own**:
  - Roster management or volunteer assignment (owned by `Staffing`).
  - Event status or scheduling (owned by `Events`).
- **Dependencies**:
  - `Carpediem.Modules.Staffing` (retrieves active roster; attendance exists strictly for `Active` assignments).
  - `Carpediem.Modules.Events` (verifies event window and active Chief Leader).
  - `Carpediem.Modules.Identity`.
- **Commands Exposed**:
  - `FinalizeAttendance(eventId, IReadOnlyList<AttendanceEntryDto> entries, chiefLeaderUserId)`
  - `RecordAdminAttendanceCorrection(attendanceId, newStatus, adminUserId, mandatoryReason)`
- **Read / Query Contracts**:
  - `GetEventAttendance(eventId): IReadOnlyList<AttendanceRecordDto>`
  - `HasUnfinalizedAttendance(eventId): bool`
  - `GetLeaderAttendanceSummary(leaderUserId): LeaderAttendanceSummaryDto`
- **Emitted Events**:
  - `AttendanceFinalized`, `AttendanceCorrectedByAdmin`.
- **Important Invariants [LOCKED]**:
  - **Active Roster Binding**: Attendance records exist strictly for `Active` `TeamAssignments`.
  - **Immediate Finalization**: Chief Leader submission takes immediate effect in factual participation history without an Admin approval gate.
  - **Audited Correction**: Admin corrections require an immutable audit trail (`OldOutcome`, `NewOutcome`, `AdminId`, `MandatoryReason`).
  - **Operational Decoupling**: Missing attendance after event end produces the operational condition `Pending Attendance`, but does not delay Event completion.

---

### Module 5: Leader Profiles (`Carpediem.Modules.LeaderProfiles`)
- **Status**: **PROPOSED** (Core boundary) / **UNRESOLVED** (Exact profile field list, privacy boundaries)
- **Responsibility**:
  - Manages approved personal profile information and user profile maintenance behaviors.
  - Facilitates profile editing by Leaders according to approved field permissions.
- **Owns**:
  - `LeaderProfile` (approved personal and contact fields; exact field inventory remains UNRESOLVED).
  - Profile update audit records.
- **Does NOT Own**:
  - Attended counts, cancellation counts, or no-show counts. These facts originate authoritatively from `Staffing` and `Attendance` and are projected into read models.
- **Dependencies**:
  - `Carpediem.Modules.Identity` (maps 1-to-1 with User).
- **Commands Exposed**:
  - `UpdateProfile(userId, UpdateProfileDto dto)`
- **Read / Query Contracts**:
  - `GetProfileByUserId(userId): LeaderProfileDto?`
- **Emitted Events**:
  - `LeaderProfileUpdated`.
- **Important Invariants [LOCKED]**:
  - Profile data access must respect privacy boundaries (specific PII visibility rules remain UNRESOLVED).

---

### Module 6: Feedback & Evaluations (`Carpediem.Modules.Feedback`)
- **Status**: **PROPOSED** (Responsibility) / **UNRESOLVED** (Visibility policy, evaluation rubric, authoring permissions)
- **Responsibility**:
  - Captures post-event performance evaluations, feedback, and operational commentary submitted by Chief Leaders regarding served Leaders.
  - Visibility policy and access control remain UNRESOLVED pending formal product decision. Authorization will be enforced according to the future approved policy.
- **Owns**:
  - `LeaderEvaluation`, `EvaluationCommentary`.
- **Does NOT Own**:
  - Factual attendance or no-show tracking (owned by `Attendance`).
  - Quantitative reliability scoring (synthetic formulas are strictly prohibited).
- **Dependencies**:
  - `Carpediem.Modules.Attendance` (verifies participation).
  - `Carpediem.Modules.Events` (verifies event context).
  - `Carpediem.Modules.Identity`.
- **Commands Exposed**:
  - `SubmitLeaderEvaluation(eventId, leaderUserId, chiefLeaderUserId, evaluationDto)`
- **Read / Query Contracts**:
  - `GetEvaluationsForEvent(eventId): IReadOnlyList<LeaderEvaluationDto>`
  - `GetEvaluationsForLeader(leaderUserId, requestingUserId): IReadOnlyList<LeaderEvaluationDto>`
- **Emitted Events**:
  - `LeaderEvaluationSubmitted`.
- **Important Invariants [LOCKED]**:
  - Strictly non-algorithmic: Evaluations are qualitative commentary; they must NEVER be calculated into a synthetic score.

---

### Module 7: Incidents (`Carpediem.Modules.Incidents`)
- **Status**: **PROPOSED** (Responsibility) / **UNRESOLVED** (Correction/editing behavior, reporting permissions)
- **Responsibility**:
  - Logs critical operational disruptions, safety concerns, or behavioral incidents occurring during an event.
  - Preserves historical integrity and auditability. Exact incident editing/correction rules remain UNRESOLVED pending product specification.
- **Owns**:
  - `IncidentReport`, `IncidentAuditRecord`.
- **Does NOT Own**:
  - Routine event notes or post-event evaluations.
- **Dependencies**:
  - `Carpediem.Modules.Events`, `Carpediem.Modules.Identity`.
- **Commands Exposed**:
  - `LogIncident(eventId, reporterUserId, incidentDto)`
  - `UpdateIncident(incidentId, updaterUserId, updateDto, mandatoryReason)`
- **Read / Query Contracts**:
  - `GetIncidentsForEvent(eventId): IReadOnlyList<IncidentDto>`
- **Emitted Events**:
  - `IncidentReported`, `IncidentUpdated`.

---

### Module 8: In-Platform Notifications (`Carpediem.Modules.Notifications`)
- **Status**: **LOCKED** (In-platform delivery only) / **UNRESOLVED** (Specific triggers and expiration rules)
- **Responsibility**:
  - Manages in-platform operational alerts, unread counters, and message dismissals.
  - Subscribes to domain events (Chief Leader appointed, team selected, assignment cancelled, event rescheduled, pending attendance) to create notifications.
- **Owns**:
  - `InPlatformNotification`, `NotificationState`.
- **Does NOT Own**:
  - External communication channels (SMS, WhatsApp, and OS push are strictly out of scope for V1).
- **Dependencies**:
  - `Carpediem.Modules.Identity` (resolves recipient user IDs).
- **Commands Exposed**:
  - `CreateNotification(recipientUserId, type, title, message, referenceUri)`
  - `MarkAsRead(notificationId, userId)`
  - `DismissNotification(notificationId, userId)`
- **Read / Query Contracts**:
  - `GetUnreadNotifications(userId): IReadOnlyList<NotificationDto>`
  - `GetUnreadCount(userId): int`
- **Emitted Events**:
  - `NotificationCreated`, `NotificationRead`.
- **Important Invariants [LOCKED]**:
  - Notifications are restricted strictly to in-platform display in V1.

---

### Module 9: Operations Dashboard & Read Models (`Carpediem.Modules.Dashboard`)
- **Status**: **PROPOSED** (Module boundary) / **UNRESOLVED** (Exact dashboard widget layout and metric thresholds)
- **Responsibility**:
  - Pre-computed and composable read models powering Admin and Chief Leader home dashboards, as well as Leader commitment views.
  - Surfaces actionable operational conditions:
    - Events lacking accepted Chief Leaders
    - Staffing gaps against target leader quotas
    - Events currently `In Progress`
    - Completed events flagged as `Pending Attendance`
    - Factual leader commitment indicators (attended count, cancellation count, no-show count compiled from Staffing and Attendance).
- **Owns**:
  - Query projections, materialized operational view models.
- **Does NOT Own**:
  - Authoritative write-side state or domain invariants.
- **Dependencies**:
  - Consumes public read contracts and events from `Events`, `Staffing`, `Attendance`, and `LeaderProfiles`.
- **Read / Query Contracts**:
  - `GetAdminOperationalDashboard(): AdminDashboardDto`
  - `GetChiefLeaderDashboard(chiefLeaderUserId): ChiefLeaderDashboardDto`
  - `GetLeaderCommitmentIndicators(leaderUserId): LeaderCommitmentSummaryDto`
