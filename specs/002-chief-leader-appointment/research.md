# Research: SLICE-03 — Chief Leader Appointment & Notification

## Decision 1: One unresolved appointment per Event

**Decision**: Treat Pending and Active as unresolved states. Reject a new appointment when either exists; permit a new record after all prior assignments are Declined. Add separate filtered unique indexes for Pending and Active, with the domain transaction guarding cross-status coexistence.

**Rationale**: The locked lifecycle only defines appointment from no assignment or declined history. It defines no replacement transition for Pending and requires declined history preservation.

**Alternatives considered**: Multiple Pending candidates would contradict the singular response lifecycle. Replacing Pending in place would invent a transition and destroy history.

## Decision 2: Candidate identity and eligibility

**Decision**: Query active users with current Chief Leader role membership through an Identity-owned directory contract. Return stable ID and existing user name only; recheck membership and active status during the appointment transaction.

**Rationale**: The role pool is authoritative and small. Additional display-name/profile fields remain unresolved elsewhere and are not needed to distinguish current development identities.

**Alternatives considered**: Caching candidate membership risks stale authorization. Adding profile fields expands scope. Returning emails or all roles exposes unnecessary identity data.

## Decision 3: Atomic cross-module side effects

**Decision**: Stage the Events assignment, Notifications intent, Audit record, and idempotency request in one host `CarpediemDbContext` transaction through narrow public module contracts.

**Rationale**: ADR-006 and ADR-005 require zero-loss notification intent and audit consistency. The modular monolith deliberately shares one SQL Server database and unit of work.

**Alternatives considered**: In-process events after commit can lose notification/audit state. A broker or distributed transaction violates the project's simplicity and monolith constraints.

## Decision 4: Appointment retry semantics

**Decision**: Use a caller-generated request token scoped to the Admin and a deterministic Event/candidate fingerprint. Identical replay returns the committed assignment; changed payload conflicts. Pending uniqueness resolves concurrent different intents.

**Rationale**: The spec explicitly requires safe uncertain retries and no duplicate assignment, audit, or notification. The established SLICE-02 pattern is already proven.

**Alternatives considered**: UI-only button disabling cannot protect network retry or manipulated clients. Field-level uniqueness cannot distinguish a deliberate later appointment after decline.

## Decision 5: Notification scope

**Decision**: Persist an unread `ChiefLeaderAppointment` semantic notification with Event context. Do not add retrieval, polling, read, dismissal, push, SMS, or email behavior.

**Rationale**: Appointment notification intent is approved for SLICE-03, while the notification experience is explicitly SLICE-17. The relational model omits unapproved dismissal behavior.

**Alternatives considered**: Building the inbox now pulls a later vertical slice forward. Omitting persistence fails the approved appointment side effect.

## Decision 6: Mobile interaction

**Decision**: Use a focused narrow-screen appointment route with a semantic single-choice candidate list and one confirmation action. Existing Draft detail links to it and reports the returned Pending state.

**Rationale**: The decision is easy to complete on mobile, provides room for status and errors, and avoids a cramped table or modal dependency.

**Alternatives considered**: A desktop table conflicts with the mobile constitution. A custom combobox adds complexity at the current pool size and increases accessibility risk.

## Decision 7: Load verification

**Decision**: Exercise reads and concurrent appointment attempts at the established 100–150-user project scale, asserting integrity and successful safe conflict handling without inventing a latency threshold.

**Rationale**: The constitution requires performance verification, while no authoritative NFR defines a numeric response target.

**Alternatives considered**: Skipping load verification violates the testing pyramid. Creating an arbitrary service-level target would invent a product requirement.
