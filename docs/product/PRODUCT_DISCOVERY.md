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
   - *Status*: UNRESOLVED.
   - *Issue*: Are "Availability" (indicating passive willingness/schedule match) and "Application" (actively requesting a staffing slot on a specific event) separate concepts/entities, or two representations of a single participation response?
3. **Exact Event Lifecycle & State Machine**:
   - *Status*: UNRESOLVED.
   - *Issue*: What is the formal event state machine? Candidate states: `Draft`, `Published/Scheduled`, `Selection Open`, `Selection Closed`, `In Progress`, `Completed`, `Rescheduled`, `Cancelled`. What triggers transition into each state?
4. **Rescheduling & Event Edits After Team Selection**:
   - *Status*: UNRESOLVED.
   - *Issue*: If an event's date, time, location, or required headcount is edited after Leaders have been selected or confirmed:
     - Does it invalidate existing confirmations?
     - Are Leaders automatically transitioned to a re-confirmation state?
     - What constitutes an "important change" requiring re-confirmation vs. a minor administrative update?
5. **Replacement Rules & Permissions (Admin vs. Chief Leader)**:
   - *Status*: UNRESOLVED.
   - *Issue*: Can a Chief Leader directly replace a confirmed Leader who cancels, or does replacement require Admin intervention? Does replacement pull from remaining applicants, or does it re-open general availability?
6. **No-Show vs. Late Cancellation Behavior**:
   - *Status*: UNRESOLVED.
   - *Issue*: At what threshold does a cancellation become a "Late Cancellation"? Is "No-Show" marked exclusively during attendance tracking? Can an Admin or Chief Leader override a No-Show if an emergency occurs?

---

### P1 — Blocks Specific Feature Implementation
*These decisions block individual feature designs, endpoint contracts, or UI workflows, but do not invalidate core domain boundaries.*

7. **Mandatory vs. Optional Event Fields**:
   - *Status*: UNRESOLVED.
   - *Issue*: Minimum required fields at creation time (Title, Start/End DateTime, Location, Role requirements, Dress code, Meeting point, Required leader count, Chief Leader ID, Notes). Can an event be created without an assigned Chief Leader?
8. **Final Confirmation Behavior & Deadlines**:
   - *Status*: UNRESOLVED.
   - *Issue*: Is confirmation mandatory for attendance eligibility? Is there an automated expiration deadline (e.g., 48 hours before event)? If a Leader fails to confirm within the window, are they automatically dropped or flagged?
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
