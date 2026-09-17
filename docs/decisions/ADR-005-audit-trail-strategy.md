# ADR-005: Audit Trail Strategy

* **Status**: ACCEPTED
* **Deciders**: Solution Architect, Engineering Team
* **Date**: 2026-09-17
* **Technical Domain**: Data Governance, Security, Auditability, Compliance

---

## Context and Problem Statement

Carpediem coordinates volunteer staffing, operational event leadership, and attendance tracking. Because staffing decisions directly impact leader reliability records, and operational overrides can alter team compositions or attendance outcomes, several operations strictly mandate **tamper-resistant, business-meaningful auditability**:
1. Administrative staffing interventions and overrides.
2. Chief Leader assignment appointment, direct acceptance, declines, and externally confirmed acceptance recordings.
3. Volunteer Leader self-cancellations (requiring mandatory reason capture).
4. Direct drafting of Leaders not marked available.
5. Replacement selections following cancellations.
6. On-site attendance finalization and administrative attendance corrections.
7. Material event rescheduling (date changes rotating the `StaffingCycle`) and event cancellations.
8. User role assignments, privilege modifications, and account deactivations.

The system requires an audit trail architecture that captures rich business context (the *who*, *what*, *why*, and *context*), guarantees atomic transactional consistency with domain mutations, excludes sensitive credentials, and maintains an append-only, immutable history without degrading database performance or introducing unnecessary infrastructure.

---

## Decision Drivers

1. **Business-Meaningful Intent**: Must capture explicit human justifications and semantic business actions, not merely low-level database column diffs or generic table change noise.
2. **Transactional Atomicity**: The audit record MUST be persisted within the exact same database transaction as the domain state mutation. If the state change rolls back, the audit rolls back; if the state change commits, the audit record is guaranteed to exist (zero phantom audits, zero untracked mutations).
3. **Immutable, Append-Only History**: Audit records must never be updated, overwritten, or deleted by application workflows.
4. **Sensitive Data Protection & Redaction**: Passwords, authentication secrets, tokens, and unnecessary personal data must never be logged.
5. **Operational Simplicity**: V1 scale (~150 users, ~2–20 events/month) requires eliminating external logging clusters, Kafka pipelines, or separate document databases.
6. **Decoupled Retention Policy**: Data retention duration (Section 13 Decision 15 / ADR-014) remains **UNRESOLVED** at the product level; the audit architecture must not invent a retention period.
7. **Infrastructure Schema Role**: The `audit` persistence area is a cross-cutting infrastructure concern, distinct from authoritative business domain module schemas.

---

## Considered Options

* **Option A: Explicit Domain / Business Audit Events Only** (Command handlers explicitly construct and persist typed business audit records carrying user justification and domain metadata).
* **Option B: Generic Persistence-Level Interceptor Only** (EF Core `SaveChangesInterceptor` capturing entity change-tracker diffs automatically).
* **Option C: Hybrid Strategy (Explicit Business Audit Records + Defensive Persistence Guard)** *(Recommended & Chosen)*.

---

## Decision Outcome

**Chosen Option**: **Option C: Hybrid Strategy (Explicit Business Audit Records + Defensive Persistence Guard)**.

### Architecture & Mechanics

#### 1. Explicit Business Audit Records (Primary Audit Mechanism)
The application/domain workflow explicitly produces typed business audit records for operations requiring an audit trail. Rather than relying on generic ChangeTracker diff logging as the source of business meaning, the command handler explicitly stages an `AuditLog` entity carrying:
- **`AuditLogId`**: Unique identifier (UUID/Guid, Primary Key).
- **`Timestamp`**: High-precision UTC timestamp (`DateTimeOffset`).
- **`ActorUserId`**: Guid of the authenticated user triggering the operation.
- **`ActorRole`**: Role under which the actor operated (`Admin`, `Chief Leader`, `Leader`, or `System`).
- **`BusinessAction`**: Explicit typed domain action string (e.g., `ChiefLeaderAssignment.ExternalAcceptanceRecorded`, `Staffing.LeaderDirectlyDrafted`, `AttendanceOutcome.AdminCorrected`).
- **`AggregateType`**: Target aggregate type (`Event`, `ChiefLeaderAssignment`, `TeamAssignment`, `AttendanceOutcome`, `User`).
- **`AggregateId`**: Target entity primary key (Guid).
- **`MandatoryReason`**: Captured explanation text (mandatory for cancellations, overrides, external acceptances, and corrections; empty only when optional).
- **`AuditSource`**: Operational provenance (`DirectUserAction`, `AdminExternalConfirmation`, `AdminOverride`, `SystemLifecycle`).
- **`PriorValue` / `NewValue`**: Meaningful business status values when justified (e.g., prior outcome `Attended` $\rightarrow$ new outcome `NoShow`), not raw SQL column dumps.
- **`CorrelationId`**: HTTP request trace identifier for end-to-end diagnostics.
- **`ClientIp` & `UserAgent`**: Diagnostic network metadata.

#### 2. Defensive Persistence Guard (Defensive Safety Mechanism Only)
The EF Core `SaveChangesInterceptor` is **not** responsible for discovering business intent or inferring reasons by diffing arbitrary entity properties. Instead, it acts strictly as a defensive compile/runtime safety guard:
- When an application command executes an operation marked as requiring audit evidence (e.g., via a command interface or entity transition contract such as `IRequiresReason`), the interceptor verifies that a valid corresponding `AuditLog` record is present in the current `DbContext` change tracker.
- If mandatory audit evidence is absent or mandatory reason text is blank, the interceptor rejects the transaction, throwing an `AuditEvidenceMissingException` before any SQL statements are dispatched.
- This defensive guard ensures developers cannot accidentally bypass mandatory audit logging during future code extensions.

#### 3. Transactional Atomicity Guarantee
- Domain mutations and audit records are staged in the **same EF Core `DbContext` instance**.
- Both are committed within a **single atomic SQL Server transaction** via `SaveChangesAsync()`.
- Guarantees that business state changes and audit evidence cannot silently diverge.

#### 4. Strict Redaction & Data Protection Rules
The audit system strictly enforces what is NOT logged:
- **Forbidden from Audit Logs**:
  - Passwords, password hashes, security stamps, and authentication secrets.
  - Invitation tokens and session credentials.
  - Unnecessary personal identifiable information (PII) such as dietary requirements or emergency contacts.
  - Arbitrary full-entity JSON snapshots by default.
- Payload serialization uses an explicit allow-list of business properties.

#### 5. Append-Only Schema Enforcement & Amendments
- The `audit.AuditLogs` table is strictly append-only.
- In production, SQL Server table permissions will grant the application user `INSERT` and `SELECT` rights, explicitly denying `UPDATE` and `DELETE`.
- If an Admin issues a secondary correction (e.g., re-correcting an attendance record), a **new** audit record is appended, referencing the prior record ID in its payload.

#### 6. Persistence Classification & Retention Policy Boundary
- **Cross-Cutting Infrastructure**: The `audit` schema is a cross-cutting infrastructure persistence area, distinct from domain module schemas (`events`, `staffing`, `attendance`, `identity`). The exact physical table layout and index definitions belong to the logical/physical ERD phase.
- **Retention Unresolved**: Data retention duration remains formally **UNRESOLVED** (Section 13 Decision 15). The audit table uses standard indexing on `Timestamp` to allow future archiving or cold-storage partitioning without structural redesign.

---

## Operations Requiring Mandatory Audit Records

The following operations represent the comprehensive V1 audit baseline:

| Domain Area | Operation | Triggering Actor | Mandatory Reason Required? | Source Classification | Key Information Captured |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Events** | **Chief Leader Appointment** | Admin | No (Standard operational flow) | `DirectUserAction` | Chief Leader User ID, Event ID, Appointing Admin ID. |
| **Events** | **Chief Leader Accept** | Chief Leader | No | `DirectUserAction` | Chief Leader User ID, Event ID. |
| **Events** | **Chief Leader Decline** | Chief Leader | No (Optional comment) | `DirectUserAction` | Chief Leader User ID, Event ID, historical preservation flag. |
| **Events** | **External Chief Leader Acceptance** | Admin | **YES (Mandatory)** | `AdminExternalConfirmation` | Acting Admin ID, Chief Leader ID, Event ID, external communication justification note. |
| **Events** | **Event Rescheduled (Date Change)** | Admin | **YES (Mandatory)** | `AdminOverride` | Old Dates, New Dates, Event ID, StaffingCycle ID, reason. |
| **Events** | **Event Cancelled** | Admin | **YES (Mandatory)** | `AdminOverride` | Event ID, mandatory cancellation reason. |
| **Staffing** | **Direct Drafting (Non-Available Leader)** | Chief Leader / Admin | No (Chief Leader) / **YES** (Admin Override) | `DirectUserAction` / `AdminOverride` | Leader ID, Event ID, prior availability state, acting user. |
| **Staffing** | **Leader Self-Cancellation** | Leader | **YES (Mandatory)** | `DirectUserAction` | Assignment ID, Leader ID, Event ID, mandatory cancellation reason text. |
| **Staffing** | **Admin Staffing Removal / Override** | Admin | **YES (Mandatory)** | `AdminOverride` | Assignment ID, Target Leader ID, Event ID, operational justification. |
| **Staffing** | **Replacement Selection** | Chief Leader / Admin | No (Chief Leader) / **YES** (Admin Override) | `DirectUserAction` / `AdminOverride` | Cancelled Assignment ID, New Assignment ID, Replacement Leader ID. |
| **Attendance** | **Attendance Finalization** | Chief Leader | No (Standard roll call) | `DirectUserAction` | Event ID, Chief Leader ID, finalized counts (Attended / NoShow). |
| **Attendance** | **Attendance Outcome Correction** | Admin | **YES (Mandatory)** | `AdminOverride` | Attendance ID, Prior Outcome, Corrected Outcome, mandatory justification note. |
| **Identity** | **User Role Assignment / Change** | Admin | **YES (Mandatory)** | `AdminOverride` | Target User ID, Previous Roles, New Roles (including dual-role grants per CHANGE-001), reason. |
| **Identity** | **Account Deactivation / Suspension** | Admin | **YES (Mandatory)** | `AdminOverride` | Target User ID, Admin ID, administrative justification. |

---

## Pros and Cons of the Options

### Option C: Hybrid Strategy (Explicit Business Audits + Defensive Persistence Guard)
* **Good**: High business value: captures exact human reasons and semantic intent rather than raw column names.
* **Good**: Strict transactional guarantee: impossible for state to commit without its audit log.
* **Good**: Zero noise: avoids logging routine query reads or uninformative internal timestamps.
* **Good**: Sensitive data is protected via allow-list serialization.
* **Bad**: Developers must explicitly create audit entries in command handlers for new auditable business actions (mitigated by the defensive persistence guard).

### Option A: Pure Domain Events Only (Without Defensive Persistence Guard)
* **Good**: Clean domain modeling.
* **Bad**: Risk of developer oversight: an auditable state change might occur without triggering an audit log if not checked by a defensive persistence guard.

### Option B: Generic ChangeTracker Interception Only
* **Good**: Automatic capture of all table modifications.
* **Bad**: Completely blind to business intent (logs "column Status changed from 1 to 2", but cannot explain *why* or capture the user's mandatory operational justification).
* **Bad**: Generates noisy, bloated JSON diffs that are expensive to query and index.
* **Bad**: Severe risk of serializing PII or password hashes into JSON columns.

---

## Security & Compliance Impact

1. **Non-Repudiation**: Every critical operational decision and administrative override is permanently attributed to an authenticated user ID, role, and IP address.
2. **Tamper Resistance**: Append-only design prevents malicious or accidental deletion of historical actions.
3. **Privacy Compliance**: Strict allow-list serialization prevents accidental ingestion of sensitive PII into searchable audit logs.

---

## Testing Impact

- **Unit Tests**: Handlers verify that executing an auditable command without a mandatory reason throws a `ValidationException` before database submission.
- **Integration Tests**: Verify that `audit.AuditLogs` contains exactly one record with matching business action and reason following an audited operation.

---

## Revisit Triggers

1. When stakeholder discovery resolves Decision 15 (Data Retention & Historical Purge Policies).
2. If regulatory or compliance requirements mandate cryptographic hashing/chaining (e.g., ledger-style audit trails) for external certification.
