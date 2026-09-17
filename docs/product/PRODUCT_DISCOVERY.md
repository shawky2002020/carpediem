# Carpediem Product Discovery & Unresolved Decisions Baseline

## 1. Executive Summary & Locked Product Context

Carpediem replaces a fragmented event-staffing workflow previously conducted across WhatsApp groups, spreadsheets, and manual phone calls.
- **Target Scale**: ~100–150 Leaders, ~3 Admins, ~10–15 Chief Leaders, ~2–20 events/month.
- **Platform**: Responsive Web Application, **Mobile-First UX** (Non-negotiable).
- **Core Outcome**: Eliminate coordination chaos, establish auditable staffing selection, track attendance and leader reliability, and preserve operational history.

### Top Three Business Problems V1 Must Demonstrably Solve (Unresolved / Candidate Framing)
1. **Coordination Overhead & Fragmented Channels**: Eliminating chaotic WhatsApp threads and spreadsheet tracking by centralizing event publishing, candidate pooling, team selection, and confirmations in a single mobile-accessible portal.
2. **Staffing Reliability & Accountability**: Providing clear, real-time visibility into who is assigned, who has confirmed, who attended, and who cancelled/no-showed, establishing an auditable track record of leader commitments.
3. **Operational Handover & Chief Leader Empowerment**: Streamlining the transition from Admin event creation to Chief Leader on-the-ground staffing, attendance logging, and post-event reporting.

---

## 2. Locked Product Facts (Approved Baseline)

The following requirements and constraints are locked by the canonical project resource:
- **Roles**: Exactly three primary roles in V1: `Admin`, `Chief Leader`, `Leader`.
  - **Chief Leader Role Model & Assignment Flow (APPROVED)**: Chief Leaders are a predefined set of known people in the organization. They do not become "qualified" through the system, and there is no system qualification workflow. Admins choose and assign one of these known Chief Leaders to an event, which creates a `Pending` assignment. The assigned Chief Leader receives an in-platform notification and must explicitly `Accept` (transitions to `Active`, granting operational ownership and satisfying the publishing prerequisite) or `Decline` (transitions to `Declined`, preserved as historical evidence; event remains without an active Chief Leader).
  - **External Confirmation Exception (APPROVED)**: Admin may record acceptance on behalf of the Chief Leader ONLY when the Chief Leader actually confirmed externally (verbally/chat). The Admin explicitly selects "Record external acceptance", creating an immutable audit record: acting Admin, timestamp, Chief Leader, Event, and mandatory justification note. The audit trail strictly distinguishes direct acceptance from Admin-recorded external confirmation.
  - **Role Exclusivity Model (SUPERSEDED by [CHANGE-001](file:///c:/dev/web/Carpediem/docs/decisions/CHANGE-001-chief-leader-dual-role-participation.md))**: *Originally locked under Decision Blocker 2 ("Chief Leaders exclusively lead events; they cannot mark availability, apply, or participate as normal Leaders on any event").* Superseded by CHANGE-001: Chief Leader and Leader roles are non-exclusive. A user belonging to the predefined Chief Leader pool may also hold the `Leader` role if granted by an Admin. Dual-role users can participate as normal Leaders on events where they are not actively assigned as Chief Leader. On the same event, holding an active Chief Leader assignment and an active Team Assignment is strictly mutually exclusive.
- **Must Have V1 Scope**:
  - Event creation and lifecycle management
  - Role-based authorization & account management
  - Chief Leader assignment per event
  - Leader availability and participation workflows
  - Team selection / assignment
  - Final confirmation flow
  - Attendance tracking
  - Leader profiles and participation history
  - Basic operational dashboard
  - In-platform notifications
  - Mobile-first responsive web interface
- **Explicitly Out of V1 Scope**:
  - Native mobile applications (iOS/Android)
  - Advanced business intelligence / custom reporting engines
  - Excel / CSV report export
  - SMS delivery
  - WhatsApp integration / bot
  - Push notifications (web or mobile OS push)
  - Complex automated Leader ranking algorithms / synthetic scoring formulas
  - Microservices or distributed messaging infrastructure
- **Commitment Rule**: No synthetic score, composite rating, or algorithmic formula is approved for V1. The platform must display factual participation indicators (e.g., attended count, cancelled count, no-show count) rather than an invented formula.
- **Historical Record Rule**: Important participation changes (cancellations, replacements, attendance, evaluations) must preserve history rather than overwrite records.

---

## 3. Unresolved Decisions Taxonomy

All unresolved decisions from Section 13 of the canonical source are categorized below by their blocking impact.

### P0 — Blocks Architecture & Core Domain Modeling
*These decisions directly dictate domain entity boundaries, state machines, relational integrity rules, and core module interfaces.*

1. **Exact Participation / Confirmation State Machine**:
   - *Status*: UNRESOLVED.
   - *Issue*: What are the exact states and valid transitions for a Leader on an Event? Candidate states identified: `Invited`, `Available`, `Not Available`, `Maybe`, `No Response`, `Selected`, `Confirmed`, `Not Selected`, `Attended`, `Cancelled`, `No-show`. Which transitions are automated vs. manual? Who can trigger each?
2. **Relationship Between Availability and Applications**:
   - *Status*: **RESOLVED / APPROVED**.
   - *Resolution*: Combined single response. Leaders mark their availability directly on the event (`Available` / `Not Available`), which automatically qualifies them for team selection. There is no separate Application form or entity in V1.
3. **Exact Event Lifecycle & State Machine**:
   - *Status*: UNRESOLVED.
   - *Issue*: What is the formal event state machine? Candidate states: `Draft`, `Published/Scheduled`, `Selection Open`, `Selection Closed`, `In Progress`, `Completed`, `Rescheduled`, `Cancelled`. What triggers transition into each state?
4. **Rescheduling & Event Edits After Team Selection**:
   - *Status*: **RESOLVED / APPROVED**.
   - *Resolution*:
     - *Minor edits* (notes, description, etc.): Keep all availability and team assignments.
     - *Same-day time/location changes*: Keep availability and team assignments; notify affected personnel clearly.
     - *Date change / true reschedule*: Old availability is invalidated; availability is reopened; previous responses/assignments remain in history; staffing is rebuilt for the new date.
5. **Replacement Rules & Permissions (Admin vs. Chief Leader)**:
   - *Status*: **RESOLVED / APPROVED**.
   - *Resolution*: Leaders may cancel their own participation via a controlled action requiring explicit confirmation and a reason. Full assignment history is preserved, and the assigned Chief Leader and Admin are notified immediately. The assigned Chief Leader directly selects the replacement for the event from available candidates. Timing penalties and commitment effects remain unresolved.
6. **Attendance Authority, Finalization & Correction**:
   - *Status*: **RESOLVED / APPROVED**.
   - *Resolution*: The assigned Chief Leader has primary authority to directly record and finalize attendance (`Attended` / `NoShow`) on-site or post-event. There is no routine Admin approval gate. Submitted attendance immediately updates factual participation histories. Admins possess audited override/correction rights (with mandatory recorded justification) in case of disputes or errors. Missing attendance post-event is an operational condition (`Pending Attendance`), not an Event lifecycle state. Timing thresholds for "Late Cancellation" remain unresolved.

---

### P1 — Blocks Specific Feature Implementation
*These decisions block individual feature designs, endpoint contracts, or UI workflows, but do not invalidate core domain boundaries.*

7. **Chief Leader Assignment & Mandatory Publishing Prerequisite**:
   - *Status*: **RESOLVED / APPROVED**.
   - *Resolution*: An Event cannot transition to `Published` unless it possesses an accepted, `Active` Chief Leader assignment (`Event.Status == Published => Event has exactly one active accepted ChiefLeaderAssignment`). Admin assignment creates a `Pending` assignment; acceptance activates operational ownership. External acceptance confirmed outside the platform may be recorded by Admin with mandatory audit log. Every published event in Carpediem always has an accepted Chief Leader. Minimum optional/mandatory creation fields remain open. Post-publish replacement is deferred to Event Management feature spec.
8. **Final Confirmation Behavior & Deadlines**:
   - *Status*: **RESOLVED / APPROVED**.
   - *Resolution*: No separate final confirmation step. A Leader who marked availability and is selected by the Chief Leader becomes part of the confirmed team immediately. The Leader remains confirmed unless they explicitly cancel. Selection immediately dispatches an in-platform notification. Direct selection without prior availability is treated as a separate invitation flow.
9. **Feedback & Post-Event Evaluation Visibility**:
   - *Status*: UNRESOLVED.
   - *Issue*: Who sees post-event evaluations and Leader ratings?
     - Can Leaders see ratings/notes written about them by Chief Leaders, or is feedback strictly confidential to Chief Leaders and Admins?
     - Can Leaders submit feedback about the event or Chief Leader?
10. **Account Onboarding, Invitation & Activation Flow**:
    - *Status*: UNRESOLVED.
    - *Issue*: How do Leaders join Carpediem? Are accounts pre-created by Admins and activated via emailed invitation token, or is there an open sign-up with Admin approval?
11. **Account Deactivation & Reactivation Lifecycle**:
    - *Status*: UNRESOLVED.
    - *Issue*: Can Leaders delete/deactivate their own profile? If deactivated, what happens to their historical event attendance records? What is the procedure for an Admin to reactivate an account?
12. **In-Platform Notification Triggers & Priority**:
    - *Status*: UNRESOLVED.
    - *Issue*: Which specific lifecycle events generate an in-app notification (e.g., Event published, Chief Leader assigned, Selection confirmed, Schedule changed, Event reminder, Replacement alert)? How are notifications cleared or acknowledged?
13. **Audit History Scope & Query Requirements**:
    - *Status*: UNRESOLVED.
    - *Issue*: Exactly which entities require detailed audit history logs? Does audit history require a dedicated audit trail table tracking `OldValue`, `NewValue`, `ChangedBy`, `ChangedAt`, or are event-sourced logs / status history tables sufficient?
14. **Cancellation Timing & Impact on Commitment**:
    - *Status*: UNRESOLVED.
    - *Issue*: Does early cancellation (e.g., 7 days prior) differ from late cancellation (e.g., <24 hours)? How are these distinctions captured in factual history without a synthetic formula?

---

### P2 — Can Safely Be Deferred
*These decisions can be deferred past the initial core foundation without blocking V1 module boundaries.*

15. **Data Retention & Historical Purge Policies**:
    - *Status*: UNRESOLVED.
    - *Issue*: How long must past event data, attendance records, and evaluations be preserved (e.g., 1 year, 5 years, indefinite)?
16. **Privacy Expectations & PII Visibility Controls**:
    - *Status*: UNRESOLVED.
    - *Issue*: Which personal profile details (phone number, dietary requirements, emergency contact) are visible to Chief Leaders vs. fellow Leaders vs. Admins?
17. **Refined Operational Dashboard Metrics**:
    - *Status*: UNRESOLVED.
    - *Issue*: Exact metrics displayed on Admin vs. Chief Leader dashboard (e.g., staffing fill rate, upcoming events needing Chief Leaders, unconfirmed leader counts).
