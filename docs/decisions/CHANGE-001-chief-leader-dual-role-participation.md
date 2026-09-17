# Change Record: CHANGE-001 — Chief Leader Dual-Role Participation

> [!IMPORTANT]
> **Change Record Identifier**: `CHANGE-001`  
> **Date**: 2026-09-17  
> **Status**: **APPROVED & RATIFIED**  
> **Classification**: Formal Requirements & Architecture Change Request  
> **Supersedes**: Decision Blocker 2 ("Exclusive Leadership Role / Strict Role Separation") across all discovery, requirements, and architecture baselines.

---

## 1. Executive Summary

This change record formally modifies the Carpediem role model and event participation rules. Previously, Carpediem locked a strict role separation rule: individuals in the predefined Chief Leader pool exclusively led events and were strictly prohibited from applying, marking availability, or participating as normal volunteer Leaders on any event.

Stakeholders have approved **CHANGE-001**, allowing a person who belongs to the predefined Chief Leader group to also hold the `Leader` role and participate as a normal volunteer Leader on events where they are **not** actively assigned as the Chief Leader.

Role membership is non-exclusive, but operational responsibility on a given event is mutually exclusive: **for the same event, a user cannot simultaneously be the Active Chief Leader and an Active Team Member**.

---

## 2. Historical Context & Original Rule (Superseded)

### Original Approved Rule (Decision Blocker 2)
In the initial product discovery and requirements specification:
- *"Chief Leaders exclusively lead events; they cannot mark availability, apply, or participate as normal Leaders on any event."*
- Chief Leader and Leader roles were defined as mutually exclusive system personas.
- Rationale: Stakeholders initially wanted complete operational separation between field supervisors and volunteer personnel to avoid confusion of authority and simplify V1 permissions.

### Grounding Locations of Original Rule (Now Marked Superseded)
- `docs/product/PRODUCT_DISCOVERY.md` (Section 2, Blocker 2)
- `docs/requirements/CONCEPTUAL_DOMAIN_MODEL.md` (Section 2 `Role`, lines 32–33)
- `docs/requirements/ROLE_PERMISSION_MATRIX.md` (Capability 7, line 50; Section F Item 8, line 184)
- `docs/requirements/LIFECYCLE_SPECIFICATION.md` (Section 7 Mapping Table, line 283)
- `docs/architecture/c4/C1_SYSTEM_CONTEXT.md` (Section 3 Persona table)
- `docs/architecture/MODULE_BOUNDARIES.md` (Module 1 Identity & Access, line 106)

---

## 3. New Approved Requirement

1. **Non-Exclusive Organizational Roles**:
   - A user in Carpediem may hold multiple organizational roles/capabilities simultaneously (specifically: `ChiefLeader` and `Leader`).
   - Holding the `ChiefLeader` capability does **not** automatically grant or mandate the `Leader` capability. Not every Chief Leader must participate as a volunteer Leader.
   - A selected subset of Chief Leaders who wish to volunteer on events may be granted the `Leader` role by an Administrator.

2. **Predefined Pool Invariant Preserved**:
   - Chief Leaders remain a predefined organizational group managed strictly by Admins.
   - **Do NOT model `IsChiefLeaderQualified`** or any qualification workflow. There is no automated promotion, testing, or application process to become a Chief Leader.
   - The change strictly means that `ChiefLeader` role membership and `Leader` role membership are no longer mutually exclusive.

3. **Event-Scoped Context & Rights**:
   - Authorization and participation depend on the specific **Event context**:
     - **For Event A** (where User $U$ has an `Active` `ChiefLeaderAssignment`):
       - $U$ acts as the operational Chief Leader for Event A.
       - $U$ possesses full operational ownership (team selection, replacement, attendance finalization).
       - $U$ **must not** mark availability or hold a `TeamAssignment` on Event A.
     - **For Event B** (where User $U$ is **not** the Active Chief Leader, and $U$ holds the `Leader` role):
       - $U$ may declare schedule availability (`Available` $\leftrightarrow$ `NotAvailable`).
       - $U$ becomes part of the candidate pool for Event B.
       - $U$ may be selected by Event B's Chief Leader or directly drafted by Admin.
       - $U$ may receive an `Active` `TeamAssignment`, execute controlled self-cancellation with reason, and receive an `AttendanceOutcome` (`Attended` / `NoShow`), exactly like any other volunteer Leader.

---

## 4. Reason for Change

1. **Organizational Reality**: In an organization of ~100–150 Leaders and ~10–15 Chief Leaders, field leaders are experienced volunteers who frequently wish to serve as regular team members on events led by their peers.
2. **Staffing Flexibility**: A strict prohibition against Chief Leaders volunteering artificially restricted the available volunteer pool for community events.
3. **Low Complexity Overhead**: Decoupling global role assignment from event-scoped operational ownership achieves full operational flexibility without altering core state machines or introducing complex workflows.

---

## 5. Affected Lifecycle Invariants & Mutual Exclusivity

### The Same-Event Exclusivity Invariant
For any Event $E$ and User $U$:
$$\text{Active ChiefLeaderAssignment}(E, U) \implies \neg\text{Active TeamAssignment}(E, U)$$

Specifically, within the domain:
1. **Assignment Conflict Guard**: If User $U$ has an `Active` `ChiefLeaderAssignment` for Event $E$, User $U$ cannot hold an `Active` `TeamAssignment` on Event $E$.
2. **Transition Resolution Requirement**: If User $U$ has an `Active` `TeamAssignment` for Event $E$, that active team assignment must be resolved (e.g., cancelled) before a `Pending` `ChiefLeaderAssignment` can transition to `Active` for Event $E$.
3. **Pending Assignment Boundary**: No additional restriction has been defined for a Pending ChiefLeaderAssignment. Behavior remains governed by existing Leader participation rules until activation. However, if the user currently has an Active TeamAssignment for the same Event, that TeamAssignment must be resolved before the Pending ChiefLeaderAssignment may transition to Active.
4. **Candidate Pool Filtering**: When Event $E$'s candidate pool is rendered for team selection, the active Chief Leader of Event $E$ is automatically excluded from the candidate pool.

---

## 6. Lifecycle Impact Assessment (Explicit Confirmation)

> [!NOTE]
> **No New State Machines or States**:
> - **Event Lifecycle**: Unchanged (`Draft` $\rightarrow$ `Published` $\rightarrow$ `Completed` / `Cancelled`).
> - **ChiefLeaderAssignment Lifecycle**: Unchanged (`Pending` $\rightarrow$ `Active` / `Declined`).
> - **AvailabilityResponse Lifecycle**: Unchanged (`No AvailabilityResponse` $\rightarrow$ `Available` $\leftrightarrow$ `NotAvailable`).
> - **TeamAssignment Lifecycle**: Unchanged (`No TeamAssignment` $\rightarrow$ `Active` $\rightarrow$ `Cancelled`).
> - **AttendanceOutcome Lifecycle**: Unchanged (`Unrecorded` $\rightarrow$ `Attended` / `NoShow`).
>
> The change is strictly about **role eligibility**, **multi-role identity modeling**, and **event-scoped contextual authorization**. Zero state transitions are added or modified.

---

## 7. Authorization Impact (Global Role vs. Event Context)

Authorization must formally distinguish between a **Global Role/Capability** and an **Event-Scoped Relationship**:

```
[ Request on Event E by User U ]
               │
               ▼
┌─────────────────────────────────────────┐
│ TIER 1: Global Role Membership          │
│ - Does U have "ChiefLeader" role?       │
│ - Does U have "Leader" role?            │
└────────────────────┬────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────┐
│ TIER 2: Event-Scoped Relationship Check │
│ Case 1: Chief Leader Action on Event E  │
│   Requires: HasRole("ChiefLeader")      │
│         AND IsActiveChiefLeader(E, U)   │
│                                         │
│ Case 2: Leader Action on Event E        │
│   Requires: HasRole("Leader")           │
│         AND NOT IsActiveChiefLeader(E, U│
│         AND Standard Staffing Rules     │
└─────────────────────────────────────────┘
```

This will be codified in **ADR-003** (Authorization & Resource Policies).

---

## 8. Architecture Impact

1. **Identity & Access Module (`Carpediem.Modules.Identity`)**:
   - `User` entity supports a many-to-many relationship with `Role` (`UserRoles`), allowing a single user to hold both `ChiefLeader` and `Leader`.
   - Invariant: Exactly three role definitions exist in V1 (`Admin`, `ChiefLeader`, `Leader`).
   - Query contract `HasRole(userId, roleName)` evaluates role membership independently.
2. **Events Module (`Carpediem.Modules.Events`)**:
   - Continues to own `ChiefLeaderAssignment`.
   - Exposes query: `IsActiveChiefLeader(eventId, userId): bool`.
3. **Staffing Module (`Carpediem.Modules.Staffing`)**:
   - Allows dual-role users holding `Leader` to declare availability and receive team assignments provided `Events.IsActiveChiefLeader(eventId, userId)` returns `false`.
   - Excludes the active Chief Leader of the event from candidate selection lists.
4. **No New Modules or Synthetic Toggles**:
   - No new module is introduced.
   - No per-event "ActAsLeader" toggle is introduced. The system infers context automatically from assignments.

---

## 9. Superseded Statements & Authoritative Language

| Grounding Document | Superseded Statement | New Authoritative Rule |
| :--- | :--- | :--- |
| `docs/product/PRODUCT_DISCOVERY.md` | *"Chief Leaders exclusively lead events; they cannot mark availability, apply, or participate as normal Leaders on any event."* | Users in the predefined Chief Leader pool may also be granted the `Leader` role by an Admin. Dual-role users can participate as normal Leaders on events where they are not the active Chief Leader. For the same event, holding active Chief Leader and active Team Assignment roles is strictly mutually exclusive. |
| `docs/requirements/CONCEPTUAL_DOMAIN_MODEL.md` | *"Exclusive Leadership Role (APPROVED - Blocker 2): Chief Leaders exclusively lead events. They cannot mark availability, apply, or participate as normal Leaders on any event."* | Role membership is non-exclusive: a User may hold both `ChiefLeader` and `Leader` roles. However, on any single event, active Chief Leader assignment and active Team Assignment are mutually exclusive. |
| `docs/requirements/ROLE_PERMISSION_MATRIX.md` | Capability 7: *"Chief Leader: None (on all events) ... Chief Leaders exclusively lead events; cannot participate as normal Leaders."* | Capability 7: Chief Leaders who also hold the `Leader` role may mark availability and participate on events where they are not the active Chief Leader. |
| `docs/requirements/LIFECYCLE_SPECIFICATION.md` | Section 7 Mapping: *"Predefined pool; assigned by Admin; exclusive leadership role (cannot be standard Leader on any event)."* | Section 7 Mapping: Predefined Chief Leader pool; non-exclusive role membership; dual-role users may participate as Leaders on non-assigned events; same-event exclusivity invariant enforced. |
| `docs/architecture/MODULE_BOUNDARIES.md` | Implicit single-role assumption / strict role separation. | Identity module explicitly supports non-exclusive role assignments (`UserRole`). Staffing module allows dual-role participation on non-led events. |

---

## 10. Audit Verification & Ratification Sign-off

- **Product Discovery**: Reconciled and marked superseded.
- **Conceptual Domain Model**: Reconciled with multi-role identity and same-event exclusivity invariant.
- **Role Permission Matrix**: Reconciled with event-scoped participation capabilities.
- **Lifecycle Specification**: Reconciled with same-event exclusivity invariant; confirms zero lifecycle state changes.
- **Architecture Baseline**: Reconciled across C1, C2, Module Boundaries, and ADR-003.
- **Ratification Status**: **RATIFIED & LOCKED** under CHANGE-001 governance.
