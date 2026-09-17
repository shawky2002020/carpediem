# Carpediem Architecture Baseline Lock

> [!IMPORTANT]
> **Architecture Status**: **LOCKED FOR FEATURE SPECIFICATION & IMPLEMENTATION PLANNING**  
> **Effective Date**: 2026-09-17  
> **Engine Baseline**: Microsoft SQL Server 2025 | ASP.NET Core 10 | Angular 20 SPA  
> **Governance Authority**: Solution Architecture & Data Architecture Pair

---

## 1. Executive Summary & Purpose of the Lock

This document establishes the **formal architecture lock** for Carpediem. The architecture baseline is frozen to serve as the stable foundation for **Spec Kit feature specifications and implementation planning**.

This lock certifies that:
1. **Core Architecture Boundaries Are Fixed**: Modular Monolith module boundaries, C1/C2 container design, session authentication, server-side authorization policies, transactional unit-of-work boundaries, append-oriented audit evidence, and decoupled in-platform notifications are formally ratified.
2. **Relational Model & Concurrency Invariants Are Fixed**: SQL Server 2025 table schemas, primary and foreign key relationships, filtered unique integrity indexes, optimistic concurrency tokens (`RowVersion`), RCSI baseline, and the parent `events.Events` coordination lock for cross-table invariants are formally ratified.
3. **Traceability Is Verified**: Every ratified lifecycle rule and invariant maps directly to verified domain, authorization, persistence, and concurrency controls without orphan rules.

> [!CAUTION]
> **Lock Scope Clarification**:  
> Locking the architecture does **NOT** claim that unresolved feature product decisions are solved. Unratified feature rules remain explicitly deferred.  
> Locking the architecture does **NOT** authorize generating project-wide API DTO catalogs, entity code classes, database migrations, or UI components. All implementation artifacts must be derived incrementally from locked feature specifications.

---

## 2. Authoritative Document & Commit Lineage

The Carpediem architecture baseline is defined strictly by the following committed artifacts:

| Layer / Milestone | Authoritative Committed Artifacts | Canonical Commit Hash | Scope & Precedence |
| :--- | :--- | :--- | :--- |
| **Ratified Requirements** | `docs/requirements/LIFECYCLE_SPECIFICATION.md`<br>`docs/requirements/CONCEPTUAL_DOMAIN_MODEL.md`<br>`docs/requirements/ROLE_PERMISSION_MATRIX.md`<br>`docs/product/PRODUCT_DISCOVERY.md` | `3d6417d` | Establishes the authoritative domain terms, roles, and state machines. |
| **Requirements Amendment** | `docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md` | `1ecf437` | Ratifies dual-role Chief Leader participation and event-level mutual exclusivity. |
| **Architecture Foundation** | `docs/architecture/c4/C1_SYSTEM_CONTEXT.md`<br>`docs/architecture/c4/C2_CONTAINER.md`<br>`docs/architecture/MODULE_BOUNDARIES.md` | `093b229` | Defines system context, deployment containers, and modular monolith module boundaries. |
| **Core Technical ADRs** | `docs/decisions/ADR-002-authentication-session-strategy.md`<br>`docs/decisions/ADR-003-authorization-resource-policies.md`<br>`docs/decisions/ADR-005-audit-trail-strategy.md`<br>`docs/decisions/ADR-009-sql-server-persistence-and-schema-organization.md` | `b15a730` | Accepts session auth, resource authz, transactional audit, and single-host DbContext. |
| **Sequence Specifications** | `docs/architecture/sequences/SEQ-001-event-creation-chief-leader-publication.md`<br>through<br>`docs/architecture/sequences/SEQ-008-attendance-finalization-and-correction.md` | `91c87ab` | Proves domain rules, authorization checks, and transaction boundaries across 8 key workflows. |
| **Secondary Technical ADRs**| `docs/decisions/ADR-006-in-platform-notification-strategy.md`<br>`docs/decisions/ADR-007-background-processing-strategy.md`<br>`docs/decisions/ADR-008-deployment-containerization.md` | `4512b81` | Accepts in-platform polling notifications, hosted lifecycle workers, and ECS Fargate deployment. |
| **Logical Relational Model** | `docs/data/LOGICAL_DATA_MODEL.md`<br>`docs/data/RELATIONAL_SCHEMA.md`<br>`docs/architecture/erd/ERD_CORE.md`<br>`docs/data/CONSTRAINT_CATALOG.md`<br>`docs/data/INDEX_CANDIDATES.md` | `e66ce52` | Defines SQL Server 2025 relational schema, types, nullability, constraints, and indexes. |
| **Data Integrity & Concurrency** | `docs/data/TRANSACTION_BOUNDARIES.md`<br>`docs/data/CONCURRENCY_MODEL.md`<br>`docs/data/SQL_SERVER_INTEGRITY_REVIEW.md` | `011b110` | Establishes Unit of Work atomicity, RCSI baseline, race condition defenses, and lock ordering. |
| **Consistency Review** | `docs/architecture/ARCHITECTURE_CONSISTENCY_REVIEW.md` | Uncommitted / Current | Comprehensive pre-lock audit across Dimensions A through I. |

---

## 3. What Architecture Is Formally Locked

The following architectural decisions are frozen and govern all subsequent design and development:

### 3.1 Architecture Pattern & Deployment Topology
- **Pattern**: Modular Monolith inside a single ASP.NET Core host application (ADR-001).
- **Module Boundaries & Logical Dependency DAG**:
  - Notation: `A --> B` denotes Module A depends on Module B.
  - `Events` depends on `Identity` (reads user existence and role claims).
  - `Staffing` depends on `Events` (references `events.StaffingCycles(Id)`) and `Identity` (reads user identity).
  - `Attendance` depends on `Staffing` (references `staffing.TeamAssignments(Id)`), `Events` (evaluates event completion context), and `Identity` (reads user identity).
  - `Audit` and `Notifications` are cross-cutting owned modules invoked through controlled contracts.
  - Zero circular write dependencies exist; each module strictly controls write mutations to its owned schema tables.
- **Hosting Topology**: AWS ECS Fargate hosting the ASP.NET Core container behind an AWS Application Load Balancer (ALB) and CloudFront CDN. CloudFront reverse-proxies `/api/*` and `/hub/*` to the backend on the same apex/subdomain as the Angular SPA (ADR-008).

### 3.2 Security, Authentication & Authorization
- **Authentication**: ASP.NET Core Identity cookie authentication with `HttpOnly`, `SameSite=Lax` (or `Strict`), `Secure` cookies. Zero bearer tokens in browser storage (ADR-002).
- **CSRF Defense**: ASP.NET Core Antiforgery tokens validated on unsafe HTTP methods (ADR-002).
- **Authorization**: Solely enforced on the server. Angular route guards are UX affordances only. Handlers enforce role policies (`Admin`, `ChiefLeader`, `Leader`) and imperative resource-based policies (`IAuthorizationHandler`) verifying entity ownership and operational state (ADR-003).

### 3.3 Relational Persistence & Integrity
- **Database Engine**: Microsoft SQL Server 2025 exclusively (ADR-009).
- **Schema Separation**: Dedicated schemas (`identity`, `events`, `staffing`, `attendance`, `notifications`, `audit`) within a single database managed by a single host `CarpediemDbContext`.
- **Isolation Baseline**: `READ_COMMITTED_SNAPSHOT ON` (RCSI). Standard reads use row-versioned snapshots from `tempdb`, materially reducing reader-writer blocking while preserving statement-level freshness.
- **Declarative Filtered Unique Indexes**:
  - `UX_StaffingCycles_CurrentPerEvent` on `events.StaffingCycles(EventId) WHERE IsCurrent = 1`
  - `UX_ChiefLeaderAssignments_ActivePerEvent` on `events.ChiefLeaderAssignments(EventId) WHERE Status = 'Active'`
  - `UX_TeamAssignments_ActiveLeaderPerCycle` on `staffing.TeamAssignments(StaffingCycleId, LeaderUserId) WHERE Status = 'Active'`
- **Unique Constraints**:
  - `UX_AttendanceOutcomes_Assignment` on `attendance.AttendanceOutcomes(TeamAssignmentId)`
- **Optimistic Concurrency**: `RowVersion rowversion NOT NULL` on selected concurrency-sensitive operational entities: `events.Events`, `events.ChiefLeaderAssignments`, `staffing.TeamAssignments`, and `attendance.AttendanceOutcomes`.
- **Historical Safety**: All foreign keys to historical child entities configure `ON DELETE NO ACTION`. Cascading deletes are prohibited across operational tables.

### 3.4 Concurrency, Coordination & Module Persistence Semantics
- **Event Coordination Lock**: Cross-table Event-scoped invariants (including CHANGE-001 dual-role exclusivity and reschedule vs. assignment races) serialize using an update lock (`UPDLOCK`) on the parent `events.Events` row inside the transaction, followed by explicit invariant revalidation.
- **Operational Context Rule**:
  $$\text{CurrentOperationalTeamAssignment}(E, U) \iff \text{TeamAssignment.Status} = \text{'Active'} \;\land\; \text{StaffingCycle.IsCurrent} = 1$$
  Historical assignments in archived cycles (`IsCurrent = 0`) do not restrict participation on rescheduled dates.
- **Audit Persistence**: Append-oriented, transactionally consistent audit evidence with restricted privileged access and deployment-hardening controls. Normal business workflows do not mutate or delete historical audit evidence (ADR-005).
- **Notifications Persistence**: Owned by the `Notifications` module. Notification records are persisted transactionally within domain commands; presentation/polling is decoupled. Because read state is mutable via `ReadAt`, Notifications are **not** append-only (ADR-006).

---

## 4. Explicitly Deferred Feature Decisions

The architecture baseline deliberately leaves the following areas open for **feature-level product specification**:

1. **Complete Event Field Set**: Custom fields, meeting links, equipment notes, and tag taxonomy.
2. **LeaderProfile Extended Attributes**: Skills taxonomy, shirt sizes, emergency contact schema, biography, and profile imagery.
3. **Feedback Rubric & Review Details**: Specific review questions, rating scales, peer feedback, and role-based feedback visibility rules.
4. **Incident Management**: Incident data model, classification taxonomy, and escalation trees.
5. **Post-Publication Chief Leader Lifecycle**: Detailed workflows for active Chief Leader withdrawal, emergency reassignment, or delegation after an event is published.
6. **Volunteer Reliability Metrics**: Exact mathematical formulas for commitment ratios, reliability scores, and tier designations.
7. **Cancellation Penalty Policies**: Automated cooldown periods, strike counters, and penalty lockout enforcement.
8. **Data Retention & Archival Policies**: Retention, privacy, archival, and deletion/anonymization requirements (remain deferred product/legal decisions).
9. **Leader Onboarding & Invitation Workflows**: Self-registration flows, email invitation token schemas, and vetting pipelines.
10. **Notification Delivery & Trigger Rules**: Notification reminder timing, broadcast criteria, and any future external delivery channels (SMS, push, and external messaging channels remain outside approved V1).
11. **Roster Capacity & Quotas**: Exact maximum roster limits, slot reservations, and replacement slot allocation rules.

---

## 5. Change-Control & Governance Rules

1. **Architecture Status**:
   $$\mathbf{LOCKED\; FOR\; FEATURE\; SPECIFICATION\; \&\; IMPLEMENTATION\; PLANNING}$$
2. **Meaning of Lock**:
   - The architecture baseline is **consumable by feature specifications**.
   - Unresolved feature and business rules **remain unresolved** until addressed in feature specs.
   - Any modification to architecture requires an **explicit Architecture Decision Record (ADR) or Change Proposal** reviewed and approved by the Architecture pair.
   - Any modification to business rules requires **formal requirements governance** amending the requirements baseline.
   - Implementation workflows **MUST NOT invent missing feature requirements**.
3. **Prohibition on Premature Code Generation**:
   - Do NOT create a global, project-wide DTO or API catalog. REST contracts must be derived feature-by-feature from locked feature specifications.
   - Do NOT generate EF Core entity classes, database migrations, or executable SQL scripts until an individual feature specification enters implementation.

---

## 6. Post-Lock Governance Sequence

Locking the architecture establishes the foundation for feature development. The required governance sequence following Architecture Lock is:

1. **Epic / Feature Slicing**: Deconstruct requirements into cohesive vertical slices.
2. **Prioritize V1 Feature Slices**: Rank slices by dependency and core business value.
3. **Choose First Vertical Feature**: Select the initial slice based on prioritization.
4. **Spec Kit `/specify`**: Author the feature specification (`spec.md`).
5. **Spec Kit `/clarify`**: Resolve underspecified requirements and edge cases.
6. **Define Acceptance Criteria / Stories**: Break specification into verifiable stories.
7. **Lock Feature / Story**: Formal sign-off on the feature specification.
8. **Spec Kit `/plan`**: Create the technical implementation plan.
9. **Spec Kit `/tasks`**: Generate actionable, dependency-ordered tasks.
10. **Implementation**: Only then dispatch verified tasks for implementation execution.
