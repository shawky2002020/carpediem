# ADR-003: Authorization & Resource Policies

* **Status**: ACCEPTED
* **Deciders**: Solution Architect, Engineering Team
* **Date**: 2026-09-17
* **Technical Domain**: Security, Authorization, Domain Governance

---

## Context and Problem Statement

Carpediem defines three primary operational roles: `Admin`, `Chief Leader`, and `Leader`. Under [CHANGE-001](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md), role membership is non-exclusive: a user belonging to the predefined Chief Leader pool may also be granted the `Leader` role by an Administrator and participate as a regular volunteer Leader on events they are not leading.

However, business permissions in Carpediem cannot be determined by static role claims alone:
1. **Event-Scoped Chief Leader Ownership**: A user holding the `ChiefLeader` role does not possess blanket operational rights across all events. They possess authority to select team members, directly draft leaders, select replacements, and finalize attendance **only on events where they hold an `Active` `ChiefLeaderAssignment`**.
2. **Event-Scoped Leader Participation**: A dual-role user holding both `ChiefLeader` and `Leader` roles may only declare availability, enter candidate pools, and receive team assignments on events where they are **not** the active Chief Leader.
3. **Same-Event Mutual Exclusivity**: On any single event, holding an `Active` `ChiefLeaderAssignment` and an `Active` `TeamAssignment` is strictly mutually exclusive.
4. **Pending Assignment Scope**: No additional restriction has been defined for a `Pending` `ChiefLeaderAssignment`. Behavior remains governed by existing Leader participation rules until activation; however, an active `TeamAssignment` on the same event must be resolved before the pending appointment may transition to `Active`.
5. **Audited Administrative Overrides**: An Admin possesses global oversight, but high-impact operations (such as overriding team rosters, recording external Chief Leader acceptances, or executing post-event attendance corrections) strictly require mandatory recorded justifications for the audit trail.
6. **Separation of Authorization vs. Domain Lifecycle**: An operation may be authorized from a permission standpoint (the user has the right to act) but invalid from a domain lifecycle perspective (e.g., attempting to select a team on an event that is in `Draft` or `Cancelled` status). Domain validation must not be conflated with access control.

The system requires an authorization architecture that decouples global role checks from contextual resource relationships and domain lifecycle rules, preventing security logic from bleeding into ad-hoc controller conditionals or polluting domain aggregates.

---

## Decision Drivers

1. **Authoritative Server-Side Enforcement**: All authorization decisions are strictly enforced on the server. Client-side Angular route guards and UI button toggles are convenience/UX features only, never security boundaries.
2. **Contextual Resource Enforcement**: Operations must verify dynamic entity relationships (e.g., "Is this user the active Chief Leader for *this specific event*?").
3. **Three-Tier Separation of Concerns**:
   - **Tier 1: Global Role/Capability Gate** (Is the actor authenticated and assigned the required system role?)
   - **Tier 2: Event-Scoped Resource Relationship Gate** (Does the actor have operational ownership or resource-level authorization for this specific entity?)
   - **Tier 3: Domain Lifecycle Invariant Gate** (Is the requested operation valid under the target entity's current state machine?)
4. **Non-Exclusive Multi-Role Support**: Accurately model users holding both `ChiefLeader` and `Leader` without role inheritance (Chief Leader does not automatically imply Leader).
5. **Audited Administrative Overrides**: The authorization system must identify when an action is executed as an Admin override and enforce the presence of an audit justification.
6. **Testability & Maintainability**: Handlers must be easily unit-testable in isolation using mock `ClaimsPrincipal` and resource contexts.

---

## Considered Options

* **Option 1: Procedural Authorization in Controllers / Command Handlers** (Ad-hoc manual `if` statements checking user IDs and database queries).
* **Option 2: ASP.NET Core Policy-Based & Resource-Based Authorization** (`IAuthorizationService` with custom `AuthorizationHandler<TRequirement, TResource>`) *(Recommended & Chosen)*.
* **Option 3: External Policy Engine** (e.g., Open Policy Agent / OPA or Oso sidecar).

---

## Decision Outcome

**Chosen Option**: **Option 2: ASP.NET Core Policy-Based & Resource-Based Authorization**.

### Three-Tier Authorization Architecture

```
[ Incoming HTTP Request ]
          │
          ▼
┌─────────────────────────────────────────────────────────────┐
│ TIER 1: Global Policy & Role Gate                           │
│ - Enforced at Controller/Endpoint via [Authorize(Policy)]   │
│ - Evaluates: User authenticated? Possesses required role?   │
└──────────────────────────────┬──────────────────────────────┘
                               │ Passed
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ TIER 2: Event-Scoped Resource Relationship Gate             │
│ - Enforced in Application Service via IAuthorizationService │
│ - Evaluates: Is user the Active Chief Leader for Event X?   │
│              Does user own TeamAssignment Y?                │
│              Is user an authorized Leader on Event X?       │
│              Is Admin executing an audited override?        │
└──────────────────────────────┬──────────────────────────────┘
                               │ Passed
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ TIER 3: Domain Lifecycle Invariant Gate                     │
│ - Enforced inside Domain Aggregate / State Machine          │
│ - Evaluates: Is Event Published? Is Assignment Active?      │
│              Is transition permissible in state machine?    │
└──────────────────────────────┬──────────────────────────────┘
                               │ Valid
                               ▼
                    [ Execute Mutation ]
```

---

### Concrete Policies, Handlers, and Operational Rules

#### 1. Tier 1: Declarative Role Policies (Endpoint Level)
Enforced via `[Authorize(Policy = "...")]` at endpoint entry points:
- **`PolicyNames.RequireAdmin`**: `RequireRole("Admin")`
- **`PolicyNames.RequireChiefLeader`**: `RequireRole("ChiefLeader", "Admin")`
- **`PolicyNames.RequireLeader`**: `RequireRole("Leader")`
  - *Critical Rule per CHANGE-001*: `ChiefLeader` does **not** automatically imply `Leader`. A user with only `ChiefLeader` cannot access Leader-only endpoints. Only users explicitly assigned the `Leader` role (including dual-role Chief Leaders) satisfy this policy.

#### 2. Tier 2: Contextual Resource Handlers (`AuthorizationHandler<TRequirement, TResource>`)
Enforced inside application services using `IAuthorizationService.AuthorizeAsync(User, resource, requirement)`:

##### A. Chief Leader Event Operation (`EventChiefLeaderRequirement`)
- **Target Resource**: `EventId` or `EventContext`
- **Logic**: Evaluated when performing Chief Leader actions (team selection, replacement, roll call):
  ```
  User has ChiefLeader role
  AND
  User has Active ChiefLeaderAssignment for Event E
  OR
  (User has Admin role AND provides non-empty mandatory justification)
  ```
- **Governs**: Team candidate selection, direct drafting, replacement selection, and on-site attendance finalization.

##### B. Leader Event Participation (`EventLeaderParticipationRequirement`)
- **Target Resource**: `EventId`
- **Logic**: Evaluated when a user attempts to declare availability or be considered for staffing on Event $E$:
  ```
  User has Leader role
  AND
  User is NOT the Active Chief Leader for Event E (!EventsService.IsActiveChiefLeader(eventId, userId))
  ```
- **Governs**: Setting availability (`Available` / `NotAvailable`), entering candidate selection pools, receiving team assignments.
- **Enforces Same-Event Exclusivity**: Ensures an active Chief Leader cannot simultaneously be an active team member on the same event.
- **Pending Assignment Rule**: No additional restriction is defined for a `Pending` `ChiefLeaderAssignment`. Behavior remains governed by existing Leader participation rules until activation. However, an active `TeamAssignment` must be resolved before a pending assignment can transition to `Active`.

##### C. Own Resource Ownership (`OwnResourceRequirement`)
- **Target Resource**: `TeamAssignment` or `LeaderProfile`
- **Logic**: Verifies `resource.LeaderUserId == currentUserId`.
- **Governs**: Submitting own availability, controlled self-cancellation with reason, and editing personal profile fields.

##### D. Audited Administrative Override (`AdminOverrideRequirement`)
- **Target Resource**: Audited command payload
- **Logic**: Verifies user has `Admin` role AND command payload contains a non-empty `MandatoryReason`.
- **Governs**: Force-cancelling team assignments, manual post-event attendance corrections, and recording externally confirmed Chief Leader acceptances.

#### 3. Tier 3: Domain Lifecycle Invariant Gate (Domain Aggregate Level)
- Domain aggregates (e.g., `Event`, `ChiefLeaderAssignment`, `TeamAssignment`) strictly own their internal state machines.
- **Distinct from Authorization**: If a user is fully authorized under Tier 1 and Tier 2, but the event is in `Cancelled` status or the assignment is already `Cancelled`, the domain aggregate rejects the operation with a `DomainRuleViolationException` (yielding HTTP `409 Conflict` or `422 Unprocessable Entity`).
- Business rules are not converted into authorization handlers; authorization verifies *who can act on what*, while the aggregate verifies *whether the state transition is lawful*.

#### 4. Client-Side Angular Route & UI Guards (Convenience Layer)
- Angular 22 route guards (`canActivate`) and Signal-driven UI directives (`*ngIf="canSelectTeam()"`) provide responsive, immediate feedback by hiding unauthorized controls or redirecting unauthorized navigation.
- **Explicit Principle**: Client-side guards are strictly convenience and usability optimizations. Server-side Tier 1 and Tier 2 policies are the sole authoritative security perimeter.

---

## Pros and Cons of the Options

### Option 2: ASP.NET Core Policy-Based & Resource-Based Authorization (Chosen)
* **Good**: Standard idiomatic .NET architecture natively integrated with dependency injection.
* **Good**: Clean separation between global role evaluation (Tier 1), fine-grained resource logic (Tier 2), and aggregate invariants (Tier 3).
* **Good**: Highly testable: `AuthorizationHandler` implementations can be unit-tested in complete isolation with mock `ClaimsPrincipal` and resource objects.
* **Good**: Eliminates security logic duplication across controllers and application services.
* **Bad**: Requires explicit invocation of `IAuthorizationService.AuthorizeAsync()` inside application service handlers for resource-level operations.

### Option 1: Procedural Checks in Controllers / Services
* **Good**: Simple to write initially for basic applications.
* **Bad**: Severe risk of authorization bypass if a developer forgets an `if` check in a new command handler.
* **Bad**: Mixes security queries with business logic; impossible to audit centrally.

### Option 3: External Policy Engine (OPA / Oso)
* **Good**: Centralized policy DSL across multiple programming languages.
* **Bad**: Enormous operational overhead, external network latency, and zero justification for a single-deployable modular monolith.

---

## Security Impact

1. **Horizontal Privilege Escalation Prevention**: Chief Leaders cannot manipulate rosters or mark attendance for events led by other Chief Leaders. Leaders cannot cancel other leaders' assignments or view sensitive admin notes.
2. **Audited Override Guarantee**: Admins cannot silently bypass operational workflows without passing through handlers that enforce mandatory justification capture.
3. **Defense-in-Depth**: Even if an endpoint authorization filter were misconfigured, Tier 2 resource handlers and Tier 3 domain aggregate invariants prevent unauthorized state manipulation.

---

## Testing Impact

- **Unit Tests**: Handlers (`EventChiefLeaderHandler`, `EventLeaderParticipationHandler`, `OwnResourceHandler`, `AdminOverrideHandler`) are unit-tested with mock `ClaimsPrincipal` and resource objects.
- **Integration Tests**: `WebApplicationFactory` tests verify that a Chief Leader receives `403 Forbidden` when attempting to select a team for an event they do not actively lead, and that a dual-role Chief Leader can successfully submit availability for an event they are not leading.

---

## Rejected Alternatives

- **Encoding Event Ownership into JWT / Role Claims**: Rejected because Chief Leader event assignments change on a per-event basis and are dynamic. Injecting event IDs into user claims causes token bloat and stale permission states.

---

## Revisit Triggers

1. If Carpediem introduces multi-organization tenant boundaries or co-lead Chief Leader delegation.
2. When stakeholder discovery resolves Decision 8 (Sensitive Feedback & Evaluation Visibility Policy) or Decision 10 (Incident Reporting Access).
