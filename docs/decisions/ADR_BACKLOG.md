# Architecture Decision Record (ADR) Backlog

> [!IMPORTANT]
> **Status: PROPOSED ARCHITECTURAL BASELINE**
> **Purpose**: Tracks core technical decisions required to transition Carpediem from requirements into physical implementation.
> **Classification Framework**:
> - **`ALREADY LOCKED`**: Explicitly mandated by canonical source of truth, constitution, or locked lifecycle specification. Requires no further trade-off debate.
> - **`NEEDS ADR`**: Technical design decision requiring formal MADR authoring, options evaluation, and stakeholder sign-off during Phase 2.
> - **`DEFERRED`**: Implementation detail or post-V1 concern that can safely be postponed without blocking module boundaries or core development.

---

## 1. ADR Inventory & Classification Matrix

| ADR ID | Decision Title | Status | Primary Driver / Scope | Proposed Options & Evaluation Scope |
| :--- | :--- | :--- | :--- | :--- |
| **ADR-001** | **Modular Monolith Architecture** | **ALREADY LOCKED** | Eliminates distributed network overhead and operational complexity for ~150 users while enforcing strict domain module boundaries. | Single deployable ASP.NET Core 10 host containing encapsulated domain modules communicating via typed contracts and in-process events. |
| **[ADR-002](file:///c:/dev/web/Carpediem/docs/decisions/ADR-002-authentication-session-strategy.md)** | **Authentication & Session Strategy** | **ACCEPTED** | Secure, mobile-first browser authentication for volunteer Leaders, Chief Leaders, and Admins across smartphone devices. | ASP.NET Core Identity with secure HttpOnly SameSite cookies and unified-origin routing (CloudFront/ALB path routing). Instant revocation via `SecurityStamp`. |
| **[ADR-003](file:///c:/dev/web/Carpediem/docs/decisions/ADR-003-authorization-resource-policies.md)** | **Authorization & Resource Policies** | **ACCEPTED** | Server-side enforcement of global RBAC roles + dynamic event ownership rules (updated per [CHANGE-001](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md)). | ASP.NET Core Policy-based & Resource-based authorization (`IAuthorizationHandler`) verifying non-exclusive roles, active `ChiefLeaderAssignment` records, event-scoped leader participation, and own-resource ownership. |
| **ADR-004** | **Domain Lifecycle & Historical Integrity** | **ALREADY LOCKED** | Codified and ratified in `LIFECYCLE_SPECIFICATION.md`. Decouples 5 lifecycles and guarantees immutability. | Five independent state machines; absence-based state modeling; `StaffingCycle` rotation; immutable preservation of cancellations and declined assignments. |
| **[ADR-005](file:///c:/dev/web/Carpediem/docs/decisions/ADR-005-audit-trail-strategy.md)** | **Audit Trail Strategy** | **ACCEPTED** | Reliable, tamper-resistant capture of mandatory justification notes, administrative overrides, and external Chief Leader acceptances. | Hybrid strategy: Explicit typed business audit records in `audit.AuditLogs` committed in the same transaction as domain state mutations, backed by an EF Core validation guard. |
| **[ADR-006](file:///c:/dev/web/Carpediem/docs/decisions/ADR-006-in-platform-notification-strategy.md)** | **In-Platform Notification Strategy** | **ACCEPTED** | Mobile-first operational alerting without external messaging services (SMS/WhatsApp/Push). | Persisted relational notifications in SQL Server combined with client-initiated lifecycle polling (Page Visibility API + configurable background timer) and unread badge counters. |
| **[ADR-007](file:///c:/dev/web/Carpediem/docs/decisions/ADR-007-background-processing-strategy.md)** | **Background Processing Strategy** | **ACCEPTED** | Management of time-based conditions (`In Progress`, `Pending Attendance`, event completion). | Query-time LINQ projections for non-persisted derived conditions (`In Progress`, `Pending Attendance`) paired with an idempotent ASP.NET Core `BackgroundService` executing guarded database state transitions for persisted `Completed` status. |
| **[ADR-008](file:///c:/dev/web/Carpediem/docs/decisions/ADR-008-deployment-containerization.md)** | **Deployment & Containerization Architecture** | **ACCEPTED** | Production AWS cloud topology and local developer experience. | CloudFront unified-origin reverse proxy (S3 for static Angular SPA, ALB for ASP.NET Core API on ECS Fargate) + private RDS for SQL Server 2025; local Docker Compose service topology. |
| **[ADR-009](file:///c:/dev/web/Carpediem/docs/decisions/ADR-009-sql-server-persistence-and-schema-organization.md)** | **SQL Server Persistence & Schema Organization** | **ACCEPTED** | Relational data integrity, schema namespaces, and EF Core migration management. | Schema-per-module (`events`, `staffing`, `attendance`, `identity`, `audit`, etc.) over a single SQL Server database via a unified host `CarpediemDbContext` applying modular configurations. |

---

## 2. Deferred Decisions Inventory (Non-Blocking / Post-V1)

| Item | Description | Revisit Trigger | Status |
| :--- | :--- | :--- | :--- |
| **ADR-010 (Deferred)** | **Chief Leader Emergency Reassignment Workflow** | Procedure and permissions for replacing an already active Chief Leader after an Event has been Published. | Event Management feature specification | **DEFERRED** |
| **ADR-011 (Deferred)** | **Account Onboarding & Invitation Flow** | Whether new accounts are pre-created by Admins with emailed activation tokens vs. self-registration with Admin approval. | Account Onboarding feature specification | **DEFERRED** |
| **ADR-012 (Deferred)** | **Feedback & Evaluation Visibility Policy** | Whether Leaders can view qualitative ratings/notes authored by Chief Leaders, and whether Leaders can evaluate events. | Evaluation feature specification | **DEFERRED** |
| **ADR-013 (Deferred)** | **Incident Correction & Addendum Policy** | Rules regarding whether incident reports are strictly append-only addenda or permit audited corrections. | Incidents feature specification | **DEFERRED** |
| **ADR-014 (Deferred)** | **Data Retention & Historical Archiving Policy** | Multi-year cold-storage archiving rules for events, attendance records, and audit logs. | Operational hardening phase | **DEFERRED** |
| **ADR-015 (Deferred)** | **AWS Cloud Infrastructure Provisioning (IaC)** | Terraform / AWS CDK provisioning scripts for ECS Fargate, ALB, and RDS SQL Server. | Staging deployment phase | **DEFERRED** |
