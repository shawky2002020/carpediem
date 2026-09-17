# ADR-007: Background Processing Strategy

* **Status**: ACCEPTED
* **Deciders**: Solution Architect, Engineering Team
* **Date**: 2026-09-17
* **Technical Domain**: Asynchronous Processing, Lifecycle Transitions, Scheduled Tasks, Monolith Architecture

---

## Context and Problem Statement

Carpediem manages volunteer staffing and execution for 2–20 events per month. The ratified domain lifecycle specification establishes clear operational boundaries and states:
1. **Event Completion Rule**: An Event in `Published` status transitions to the terminal persisted state **`Completed`** via either of two ratified mechanisms:
   - **System-Time Condition**: The scheduled window concludes (`CurrentTime > Event.EndTime`).
   - **Explicit Admin Closeout**: An Operations Administrator manually executes an administrative closeout action.
2. **Derived Operational Conditions (Non-Persisted)**:
   - **`In Progress`**: Dynamically derived when `Event.Status == Published` and `Event.StartTime <= CurrentTime <= Event.EndTime`.
   - **`Pending Attendance`**: Dynamically derived when `Event.Status == Completed` and one or more active team assignments remain with `AttendanceOutcome == Unrecorded`.
3. **Low Operational Scale & Inactivity Windows**: Events occur infrequently. An event concluding on a weekend might receive zero user web traffic for several days.

We must decide the architectural mechanism for executing time-driven lifecycle transitions and evaluating operational conditions:
- Should time-driven completion rely solely on query-time evaluation, opportunistic request-time database mutations, or a scheduled hosted background worker?
- How do we guarantee safe, idempotent execution across multiple backend container replicas (AWS ECS/Fargate) without introducing heavy external schedulers, message brokers, or distributed locks (no Quartz, Hangfire, RabbitMQ, or Redis)?
- Are there any other approved V1 workflows requiring background processing?

---

## Decision Drivers

1. **Persisted Lifecycle Integrity**: `Completed` is a ratified persisted state. Historical reporting, operational audits, and attendance roll-call tracking require unambiguous, persisted status values rather than virtual calculations that drift if queries differ.
2. **Traffic-Independent Accuracy**: Events must transition to `Completed` even if no user logs in or issues an HTTP request immediately following the event's scheduled conclusion.
3. **Zero Superfluous Infrastructure**: For an application running ~2–20 events/month, external queue infrastructure (RabbitMQ, SQS) and dedicated job orchestrators (Hangfire with storage schemas, Quartz) are unwarranted operational overhead.
4. **Multi-Replica Safety & Idempotency**: Multiple ASP.NET Core container replicas running in ECS/Fargate must be able to execute scheduled background tasks concurrently without racing, deadlocking, or creating duplicate lifecycle transitions.
5. **Clear Boundary: Derived Conditions vs. Persisted States**: Derived conditions (`In Progress`, `Pending Attendance`) must remain read-model projections that incur zero background mutation overhead.

---

## Considered Options

* **Option A: Query-Time-Only Evaluation (Virtual State Calculation)**
  The database status remains `Published` indefinitely until queried; read queries evaluate `Now > EndTime` on-the-fly and present the event as completed in DTO projections without updating the database record.
* **Option B: Opportunistic Request-Time Mutation**
  When any user happens to issue an HTTP request querying or updating an event, middleware or handlers inspect `Now > EndTime` and opportunistically attempt a state update.
* **Option C: Guarded ASP.NET Core `BackgroundService` Hosted Worker (Chosen)**
  A lightweight, native .NET `IHostedService` (`BackgroundService`) runs inside the existing monolithic process on a low-frequency configurable periodic scan, executing a guarded, idempotent lifecycle transition.
* **Option D: External Enterprise Job Scheduler (Hangfire / Quartz.NET / AWS EventBridge)**
  Adopts a dedicated job scheduling framework backed by dedicated database tables or AWS serverless cron triggers invoking webhook endpoints.

---

## Decision Outcome

**Chosen Option**: **Option C: Guarded ASP.NET Core `BackgroundService` Hosted Worker (Combined with Query-Time Projections for Derived Conditions)**.

### Architecture & Technical Mechanics

#### 1. Derived Operational Conditions (Query-Time Projections)
The two non-persisted operational conditions are evaluated strictly on-the-fly in LINQ/SQL read queries without background jobs or scheduled writes:
- **`In Progress`**: Evaluated dynamically in read projections when `Event.Status == Published` and `StartTime <= CurrentTime <= EndTime`.
- **`Pending Attendance`**: Evaluated dynamically in dashboard summaries when `Event.Status == Completed` and any active roster assignment has unrecorded attendance.
Neither condition requires scheduled tasks, database writes, or background mutations.

#### 2. Persisted Event Completion via Guarded `BackgroundService`
To satisfy the ratified requirement that `Completed` is a persisted state—and to guarantee transitions occur even when the system receives zero user traffic—the ASP.NET Core backend includes a hosted service (`EventLifecycleBackgroundWorker`):
- **Execution Model**: Implements `Microsoft.Extensions.Hosting.BackgroundService`.
- **Cadence**: Executes as a **low-frequency configurable periodic scan** (an initial operational tuning default can be set during implementation; aggressive scheduling is unnecessary at ~2–20 events/month).
- **Guarded Domain Transition**: On each scan, the background worker discovers `Published` Events where `CurrentTime > Event.EndTime`. For each eligible Event, it invokes the **exact same guarded business lifecycle transition** used by the application architecture:
  $$\text{Published} \longrightarrow \text{Completed}$$
- **Domain Rules Preservation**: The worker does not bypass domain rules or execute un-audited ad-hoc updates for convenience; transitions remain history- and audit-aware in accordance with ADR-005.
- **Administrative Closeout (Alternative Ratified Path)**: An Operations Administrator may also explicitly trigger closeout at any time via an administrative command, executing the exact same state transition path on demand.

#### 3. Multi-Replica Concurrency & Safety (AWS ECS/Fargate)
Every ECS container task replica may host the `BackgroundService`. The architecture does not rely on a single application replica, in-memory locks, leader election, or external distributed locks (no Redis / Redlock):
- **Database-Backed Guarded Concurrency**: Transitions utilize compare-and-transition semantics.
- **Concurrent Execution Scenario**:
  - Worker on Replica A discovers an event satisfying `Now > EndTime` and attempts the `Published` $\rightarrow$ `Completed` transition.
  - Worker on Replica B (or an Admin closeout action) simultaneously discovers the same event and attempts transition.
  - Exactly one execution successfully acquires the state transition lock/concurrency token and commits the update.
  - The other execution observes a concurrency conflict or detects that `Status` is no longer `Published`, and **safely no-ops**.
- *Data-Model Decoupling*: Exact SQL Server locking, rowversion concurrency, or atomic update syntax belongs to the implementation and ERD design phase.

#### 4. Scope Audit: Do Any Other V1 Workflows Require Background Processing?
A rigorous review of all ratified requirements and sequence diagrams ([SEQ-001](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-001-event-creation-chief-leader-publication.md) through [SEQ-008](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-008-attendance-finalization-and-correction.md)) confirms:
- **Chief Leader Appointments**: Triggered directly by Admin command.
- **Team Selections & Direct Drafting**: Triggered directly by Chief Leader / Admin commands.
- **Volunteer Self-Cancellations**: Triggered directly by volunteer action.
- **True Date Rescheduling**: Triggered directly by Admin command.
- **Attendance Finalization & Corrections**: Triggered directly by Chief Leader roll call and Admin overrides.
- **In-Platform Notifications**: Staged as transactional records during command execution; fetched via client polling (ADR-006).
- **Conclusion**: **Automatic Event completion is the only currently justified V1 background lifecycle mutation.**

---

## Pros and Cons of the Options

### Option C: Guarded ASP.NET Core `BackgroundService` (Chosen)
* **Good**: Guarantees timely transition to `Completed` without requiring active HTTP user traffic.
* **Good**: Zero external infrastructure: native to .NET runtime, requiring no additional NuGet packages, message queues, or Redis.
* **Good**: Inherently safe under horizontal scaling across multiple ECS/Fargate replicas via database compare-and-transition semantics.
* **Good**: Respects domain boundaries: derived conditions remain pure query projections; only the ratified persisted state is updated.
* **Bad**: Periodic timer ticks consume a negligible amount of idle database polling capacity.

### Option A: Query-Time-Only Evaluation
* **Good**: Zero background worker execution.
* **Bad**: Fails to persist `Completed` as an authoritative state in the database, violating the ratified lifecycle specification.
* **Bad**: Makes direct SQL reporting, DBA audits, and cross-module relational queries difficult and prone to inconsistency.

### Option B: Opportunistic Request-Time Mutation
* **Good**: Avoids scheduled timer threads.
* **Bad**: If no one accesses the platform for days following an event, the event remains officially `Published` in database records, causing stale dashboard data and overdue alerts to stall.
* **Bad**: Mixes write mutations into read-only HTTP GET requests, violating HTTP idempotency semantics.

### Option D: External Job Scheduler (Hangfire / Quartz / EventBridge)
* **Good**: Rich visual management dashboards and cron expression flexibility.
* **Bad**: Massive architectural overkill: Hangfire creates 10+ internal database tables to manage state for an application running only 2–20 events per month.
* **Bad**: Introduces unwarranted maintenance, schema complexity, and potential failure points.

---

## Security & Reliability Impact

1. **Fault Tolerance**: If an ECS container crashes or restarts during a scan, any surviving container or the restarted task will safely execute the transition on its next periodic interval.
2. **Error Isolation**: The hosted service wraps execution in robust `try/catch` logging blocks, ensuring that transient database network interruptions never crash the application host.
3. **Clean Shutdown**: Implements cooperative cancellation via `CancellationToken`, ensuring in-flight tasks drain gracefully during ECS task deployments and container terminations.

---

## Revisit Triggers

Re-evaluate introducing an advanced job scheduler (such as Quartz.NET or AWS EventBridge) if:
1. Future product specifications mandate automated volunteer notification reminders at precise relative offsets (e.g., *"Dispatch reminder exactly 24 hours prior to event start"* across hundreds of events).
2. Complex multi-step batch workflows (e.g., automated data archiving or scheduled export generation) are approved for post-V1 milestones.
