# ADR-009: SQL Server Persistence & Schema Organization

* **Status**: ACCEPTED
* **Deciders**: Solution Architect, Engineering Team
* **Date**: 2026-09-17
* **Technical Domain**: Persistence, Database Architecture, Data Modeling, EF Core

---

## Context and Problem Statement

Carpediem uses Microsoft SQL Server 2025 as its primary relational data store. The backend is structured as a Modular Monolith in C# 14 / .NET 10 LTS using Entity Framework Core 10 (EF Core 10). The system coordinates several interrelated domains: Identity, Events (including Chief Leader Assignments and StaffingCycles), Staffing (Availability Responses and Team Assignments), Attendance, Leader Profiles, Feedback, Incidents, and Notifications.

While all data resides within a single SQL Server database to support transactional atomicity and operational simplicity for a ~150-user organization, the architecture requires clear boundaries:
1. **Explicit Write Encapsulation**: A module must never issue arbitrary `INSERT`, `UPDATE`, or `DELETE` statements against tables owned by another module.
2. **Read Efficiency & Projections**: Operations dashboards and Leader views need to perform efficient cross-entity queries and read projections without incurring severe N+1 overhead or artificial microservice HTTP hops.
3. **Migration Manageability**: Database schema evolution must be version-controlled, deterministic, and safe for automated CI/CD deployment.
4. **Referential Integrity vs. Code Decoupling**: We must balance database-enforced integrity with decoupled EF Core object graphs.

---

## Decision Drivers

1. **Single Database Simplicity**: Maintain one authoritative SQL Server 2025 database with ACID transaction support across domain workflows.
2. **Explicit Module Write Boundaries**: Mutations must execute exclusively through the owning module's commands and domain aggregates.
3. **Database Namespace Visibility**: Developers and DBAs must immediately recognize which table belongs to which domain module via SQL Server schemas.
4. **Pragmatic Relational Power**: This is a modular monolith, not distributed microservices; do not let artificial architectural purity prevent useful SQL Server capabilities (e.g., cross-schema read joins, physical referential integrity, database views).
5. **Deterministic Migration Pipeline**: A single host DbContext avoids the need to coordinate multiple EF migration histories and DbContext-level transaction boundaries, reducing operational and migration-order complexity for this modular monolith.
6. **Optimistic Concurrency**: Prevent concurrent edit collisions on high-contention operational entities (events, staffing assignments, attendance records).
7. **Performance & Query Optimization**: Explicit projection patterns (`.Select()`) and eager loading to prevent N+1 queries, paired with `.AsSplitQuery()` to eliminate Cartesian product explosions across multi-collection includes.

---

## Considered Options

* **Option A: Default `dbo` Flat Schema with Table Name Prefixes** (`dbo.Events_Event`, `dbo.Staffing_TeamAssignment`, etc.).
* **Option B: Schema-per-Module with Unified Host DbContext & Modular Configurations** *(Recommended & Chosen)*.
* **Option C: Independent DbContext per Module with Disjoint Migration Pipelines**.

---

## Decision Outcome

**Chosen Option**: **Option B: Schema-per-Module with Unified Host DbContext & Modular Configurations**.

### Architecture & Persistence Rules

#### 1. SQL Server Schema Namespaces
Database tables are organized into dedicated SQL Server schemas representing logical module boundaries. Domain schemas correspond to domain modules, while cross-cutting infrastructure (such as audit persistence) is maintained in its own dedicated schema:

| Schema Namespace | Boundary Classification | Owning Module / Boundary | Representative Tables (Illustrative) |
| :--- | :--- | :--- | :--- |
| **`identity`** | Domain Schema | `Identity` | `identity.Users`, `identity.Roles`, `identity.UserRoles`, `identity.AccountTokens` |
| **`events`** | Domain Schema | `Events` | `events.Events`, `events.StaffingCycles`, `events.ChiefLeaderAssignments` |
| **`staffing`** | Domain Schema | `Staffing` | `staffing.AvailabilityResponses`, `staffing.TeamAssignments`, `staffing.AssignmentCancellations` |
| **`attendance`**| Domain Schema | `Attendance` | `attendance.AttendanceOutcomes`, `attendance.AttendanceRollSheets` |
| **`leader`** | Domain Schema | `LeaderProfiles` | `leader.LeaderProfiles` |
| **`feedback`** | Domain Schema | `Feedback` | `feedback.LeaderEvaluations` |
| **`incidents`** | Domain Schema | `Incidents` | `incidents.IncidentReports` |
| **`notifications`** | Domain Schema | `Notifications` | `notifications.InPlatformNotifications` |
| **`audit`** | Infrastructure Schema | Cross-cutting / Audit Persistence | `audit.AuditLogs` |

*Note on Data Modeling*: The table listings above illustrate conceptual schema ownership. Comprehensive entity definitions, column data types, foreign key cascades, and physical index strategies will be formally specified during the forthcoming logical/physical ERD design phase.

#### 2. DbContext Strategy: Host DbContext with Scoped Module Interfaces
- **Single Host `CarpediemDbContext`**: Inherits from EF Core's `DbContext` and serves as the single transactional persistence coordinator.
- **Internal Module Interfaces**: Each module exposes an internal interface declaring its own entity sets (e.g., `IEventsDbContext`, `IStaffingDbContext`). Command handlers inject only their module-specific interface.
- **Assembly-Based Entity Configurations**: Inside `OnModelCreating`, the host `DbContext` discovers and applies `IEntityTypeConfiguration<T>` implementations from each module assembly:
  ```csharp
  builder.Entity<Event>().ToTable("Events", schema: "events");
  builder.Entity<TeamAssignment>().ToTable("TeamAssignments", schema: "staffing");
  ```
- **Write Ownership Rule**: Only the owning module's command handlers may modify entities within its schema. Direct cross-module writes are strictly prohibited.

#### 3. Foreign Keys & Referential Integrity Model
We adopt a pragmatic hybrid model balancing relational integrity with object decoupling:
- **Intra-Aggregate / Intra-Module**: Full EF Core navigation properties and cascading/restricted physical SQL foreign keys (e.g., `events.StaffingCycles.EventId` $\rightarrow$ `events.Events.Id`).
- **Cross-Module Boundaries**:
  - **In EF Core Entities**: Entities reference foreign modules via **scalar GUIDs** only (e.g., `staffing.TeamAssignment.LeaderUserId` is a `Guid`, not a navigation property to `identity.User`). This prevents accidental loading of massive cross-module object graphs and avoids circular EF model dependencies.
  - **In SQL Server**: Physical foreign key constraints with `ON DELETE NO ACTION` / `RESTRICT` are configured in EF model builders across schemas (e.g., `FK_TeamAssignments_Users` from `staffing.TeamAssignments(LeaderUserId)` to `identity.Users(Id)`). This leverages SQL Server's relational engine to guarantee referential integrity (preventing orphaned records or deleting users who hold event assignments) without coupling C# domain aggregates.

#### 4. Controlled Cross-Module Reads, Read Models & Projections
- Read-only queries (e.g., `OperationsDashboardQuery`, `EventRosterQuery`) are encouraged to join across schemas using EF Core `.AsNoTracking()` projections or database views (`vw_AdminOperationalSummary`).
- **Query Efficiency & N+1 Prevention**:
  - Eager loading (`.Include()`) or explicit `.Select()` projections avoid N+1 query execution by querying data in planned batches or single relational joins rather than iterative deferred loads.
  - Projections map directly to lightweight DTOs, fetching only required scalar columns and bypassing the EF Core change tracker for maximum query throughput.
- **Preventing Cartesian Explosions (`.AsSplitQuery()`)**:
  - When queries eager-load multiple related collections (1-to-many relationships, such as an Event with multiple Sessions and multiple Roster Slots), `.AsSplitQuery()` is applied to avoid Cartesian product explosion across SQL joins.
  - `.AsSplitQuery()` does not resolve N+1 queries by itself; it specifically mitigates join duplication and memory bloat on multi-collection queries.
- **Raw SQL / T-SQL Use**: High-performance read operations, specialized aggregation reports, or complex dashboard summaries may utilize raw T-SQL (`FromSqlRaw` / `FromSqlInterpolated`) or database views where EF Core LINQ generates suboptimal execution plans.

#### 5. Concurrency Control (Optimistic)
- Rather than imposing concurrency tokens indiscriminately across all tables, optimistic concurrency (`rowversion` / `byte[]`) is targeted specifically at high-collision transactional entities where concurrent administrative or user edits may clash:
  - `events.Events` (concurrent administrative status or schedule edits)
  - `events.ChiefLeaderAssignments` (simultaneous acceptance/decline and admin reassignment)
  - `staffing.TeamAssignments` (concurrent slot assignment, cancellation, or replacement)
  - `attendance.AttendanceOutcomes` (concurrent roll call submissions or post-event corrections)
- Append-mostly, low-contention, or immutable tables (e.g., `audit.AuditLogs`, `notifications.InPlatformNotifications`, `feedback.LeaderEvaluations`) do not require `rowversion` columns.
- EF Core automatically evaluates the concurrency token on `UPDATE` statements (`WHERE ConcurrencyToken = @token`), throwing a `DbUpdateConcurrencyException` upon collision (e.g., two coordinators modifying the same roster slot or simultaneous status transitions).

#### 6. Transaction Boundaries & Atomicity
- **Single Unit of Work**: Because all modules reside in one database under `CarpediemDbContext`, multi-aggregate updates (e.g., updating a `TeamAssignment` and writing an `audit.AuditLog`) execute inside a single atomic SQL Server transaction via `SaveChangesAsync()`.
- **No Distributed Transactions**: Eliminates the need for two-phase commit (`TransactionScope`), distributed transaction managers, or outbox pollers for core business mutations.

#### 7. Naming Conventions & Constraints
- **Table Names**: Pluralized PascalCase matching domain aggregates (e.g., `events.Events`, `staffing.TeamAssignments`).
- **Column Names**: PascalCase matching C# properties (e.g., `StartTime`, `MandatoryReason`).
- **Primary Keys**: `PK_<Schema>_<Table>` (clustered unique identifier).
- **Foreign Keys**: `FK_<SourceTable>_<TargetTable>_<PropertyName>`.
- **CHECK & UNIQUE Constraints**: Declared explicitly via fluent configuration (e.g., ensuring `StartTime < EndTime`, or single `Active` Chief Leader per event).

#### 8. Production-Safe Migrations Pipeline
- **Single Sequential Migration History**: Maintained under `src/Infrastructure/Persistence/Migrations` against `dbo.__EFMigrationsHistory`. A single host context ensures migration dependencies across schemas are sequenced linearly.
- **Idempotent CI/CD Deployment**: In production, migrations are never applied blindly via `context.Database.Migrate()` on application boot (which can induce deployment-time locking or multi-instance race conditions). Instead, CI/CD builds generate an idempotent SQL script via `dotnet ef migrations script --idempotent`, allowing DBAs to review and execute scripts with zero runtime downtime.
- **Non-Breaking Schema Changes**: Table renames, column deletions, or type modifications follow the expand/contract pattern across release milestones.

---

## Pros and Cons of the Options

### Option B: Schema-per-Module + Unified Host DbContext (Chosen)
* **Good**: Clean database organization: tables are logically grouped into module schemas in SQL Server Object Explorer.
* **Good**: Single ACID transaction boundary: enables atomic multi-entity operations coordinated by application services.
* **Good**: Single migration pipeline: A single host DbContext avoids the need to coordinate multiple EF migration histories and DbContext-level transaction boundaries, reducing operational and migration-order complexity for this modular monolith.
* **Good**: High-performance cross-schema read projections for dashboards without distributed networking overhead.
* **Good**: Enforces write encapsulation while avoiding premature multi-database microservice complexity.
* **Good**: Leverages SQL Server referential integrity constraints without coupling EF Core entity graphs.
* **Bad**: Requires discipline to prevent developers from adding direct navigation properties across schemas in EF Core models (enforced via scalar GUID rules).

### Option A: Single `dbo` Schema with Table Prefixes
* **Good**: Lowest initial setup overhead.
* **Bad**: Flat, cluttered database schema with 20+ tables prefixed with strings; poor visibility for DBAs.
* **Bad**: Fails to establish natural schema boundaries.

### Option C: Separate DbContext & Migration History per Module
* **Good**: Total physical isolation between modules in code.
* **Bad**: Extremely complex EF Core migration ordering: if module A creates a table that module B references, migrations fail if run out of order.
* **Bad**: Cross-module transactions require distributed transactions (`TransactionScope` / two-phase commit), which is fragile and completely unnecessary for a ~150-user application.

---

## Security & Reliability Impact

1. **Database-Level Least Privilege**: Production connection strings use a dedicated application login with read/write permissions on application schemas, but `DELETE`/`UPDATE` revoked on `audit.AuditLogs`.
2. **Connection Resiliency**: EF Core `EnableRetryOnFailure()` configured to handle transient network drops on cloud SQL instances.
3. **Deadlock Prevention**: Accessing multiple tables within a transaction follows a strict ordering convention: `identity` $\rightarrow$ `events` $\rightarrow$ `staffing` $\rightarrow$ `attendance` $\rightarrow$ `audit`.

---

## Testing Impact

- **Integration Testing**: Local integration tests run against real SQL Server 2025 instances hosted in Docker testcontainers (`Testcontainers.MsSql`), ensuring 100% fidelity with production T-SQL dialect, indexes, and constraints.

---

## Revisit Triggers

1. If a module's transaction volume grows to a scale requiring physical database sharding or extraction into an independent microservice (highly improbable at ~2–20 events/month).
2. If multi-region data sovereignty mandates storing specific tables in different geographical database instances.
