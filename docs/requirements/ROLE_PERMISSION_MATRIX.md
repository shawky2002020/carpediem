# Carpediem Role & Permission Matrix

> [!IMPORTANT]
> **Authoritative Baseline & Precedence**:
> This document establishes the permission governance model for Carpediem based strictly on the locked canonical project resource (`https://app.notion.com/p/Carpediem-Project-Resource-Locked-Engineering-Source-of-Truth-3ddbf826725381938090c73592b2ad83`) and `docs/SOURCE_OF_TRUTH.md`.
>
> In compliance with project governance:
> 1. No permission is inferred from the absence of that permission in another role.
> 2. No reasonable assumption or architectural convenience is classified as `KNOWN`.
> 3. Global blanket words (`all`, `full`, `override`, `only`, `any`) are forbidden unless explicitly supported by the canonical source.
> 4. Classifications are tracked **per role**, not globally per capability.

---

## A. Matrix Methodology & Classification Definitions

A single operational capability often contains differing levels of certainty across user roles. To eliminate false certainty and prevent premature implementation, each permission is classified independently per role:

- **`KNOWN`**: Explicitly mandated and documented in Section 4 or Section 5 of the locked canonical source of truth. Ready for specification when the surrounding state machine is frozen.
- **`PROPOSED`**: Logical architectural baseline or candidate permission derived from workflow discovery. Requires explicit stakeholder review and confirmation before locking.
- **`UNRESOLVED`**: Conflicting, underspecified, or explicitly listed as an Open Product Decision in Section 13 of the canonical source. Implementation agents are strictly prohibited from implementing or hardcoding these behaviors.

---

## B. Per-Role Permission Governance Matrix

The table below details every capability across the three primary V1 roles (`Admin`, `Chief Leader`, `Leader`), recording the specific permission scope, classification status, grounding evidence/rationale, and applicable open questions.

| Capability / Action | Role | Permission Scope | Status | Evidence / Rationale (Canonical Source) | Open Question / Ambiguity |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **1. Event Viewing Scope** | **Admin** | View events in the platform | **KNOWN** | Section 4: "Create and manage events", "View operational dashboards". | Are draft or archived events accessible under the same view as active events? |
| | **Chief Leader** | View assigned events | **KNOWN** | Section 4: "Chief Leader: View assigned events". | Can Chief Leaders view events to which they are not assigned? (Status: **UNRESOLVED**) |
| | **Leader** | View relevant events | **KNOWN** | Section 4: "Leader: View relevant events". | What exact criteria define an event as "relevant" to a Leader (e.g., published status, invitation, eligibility)? (Status: **UNRESOLVED**) |
| **2. Event Creation** | **Admin** | Create events | **KNOWN** | Section 3: "Admin creates or announces an event"; Section 4: "Create and manage events"; Section 5: Must Have. | None. |
| | **Chief Leader** | None | **PROPOSED** | Section 4: Chief Leader scope does not list event creation. | Can Chief Leaders ever submit an event draft, or is creation strictly an Admin responsibility? |
| | **Leader** | None | **KNOWN** | Section 4: Absent from Leader scope. | None. |
| **3. Event Editing & Rescheduling** | **Admin** | Edit events (minor edits keep roster; same-day shifts notify; date changes reopen availability & rebuild staffing) | **APPROVED** | Section 4: "Edit/cancel events"; Decision Blocker 3 APPROVED. | Date changes invalidate previous availability; past responses/assignments preserved immutably in history. |
| | **Chief Leader** | Edit operational notes/meeting details for assigned events | **PROPOSED** | Section 4 does not list event metadata editing for Chief Leaders. | Can Chief Leaders modify operational notes or meeting locations for assigned events without Admin approval? (Status: **UNRESOLVED**) |
| | **Leader** | None | **KNOWN** | Section 4: Absent from Leader scope. | None. |
| **4. Event Cancellation** | **Admin** | Cancel events according to approved permissions | **KNOWN** | Section 4: "Edit/cancel events according to approved permissions". | What automated notification and cleanup rules trigger upon event cancellation? |
| | **Chief Leader** | None | **PROPOSED** | Section 4: Absent from Chief Leader scope. | Can an on-site Chief Leader cancel an event during an emergency, or must Admin execute cancellation? (Status: **UNRESOLVED**) |
| | **Leader** | None | **KNOWN** | Section 4: Absent from Leader scope. | None. |
| **5. Chief Leader Assignment** | **Admin** | Appoint Chief Leader from predefined pool (creates `Pending` assignment); record externally confirmed acceptance with mandatory audit note | **APPROVED / KNOWN** | Section 4: "Assign Chief Leaders"; Section 5: Must Have; Chief Leader Assignment Flow APPROVED. | Can an Admin assign multiple Chief Leaders or co-leads to a single large event? |
| | **Chief Leader** | Accept or Decline pending assignment for an event | **APPROVED** | Chief Leader Assignment Flow APPROVED: Explicit acceptance required to activate operational ownership. | None. |
| | **Leader** | None | **KNOWN** | Section 4: Absent from Leader scope. | None. |
| **6. Availability Visibility** | **Admin** | View Leader availability across events | **PROPOSED** | Section 4: "View operational dashboards"; does not explicitly specify raw availability viewing scope. | Does Admin view raw availability entries or aggregated staffing metrics per event? |
| | **Chief Leader** | Review available/applying Leaders for assigned events | **KNOWN** | Section 4: "Review available/applying Leaders". | Can Chief Leaders see availability for events they are not assigned to lead? (Status: **UNRESOLVED**) |
| | **Leader** | View own submitted availability | **KNOWN** (Own); **PROPOSED** (No access to others) | Section 4: "Mark availability", "View own event history". | Are fellow Leaders' availability statuses visible to peers before selection? (Status: **UNRESOLVED**) |
| **7. Application / Participation Submission** | **Admin** | None | **KNOWN** | Administrative persona; does not apply to staffing pools. | None. |
| | **Chief Leader** | None (on all events) | **APPROVED** | Decision Blocker 2 APPROVED: Chief Leaders exclusively lead events; cannot participate as normal Leaders. | None. |
| | **Leader** | Mark Availability on event (acts as selection application) | **APPROVED / KNOWN** | Section 4: "Mark availability", "Apply/respond to invitations". Decision Group 2 APPROVED. | Marking "Available" directly qualifies Leader for selection; no separate application form exists. |
| **8. Team Selection & Direct Drafting** | **Admin** | Oversight & intervention/override / direct assignment | **APPROVED** | Decision Group 3 & Blocker 1 APPROVED: Can directly assign any Leader; immediate effect. | All overrides must be audited. |
| | **Chief Leader** | Select team from candidates or directly assign Leaders for assigned events | **APPROVED / KNOWN** | Decision Group 3 & Blocker 1 APPROVED: Direct assignment takes immediate effect. | Leader must cancel if unavailable. |
| | **Leader** | None | **KNOWN** | Section 4: Absent from Leader scope. | None. |
| **9. Admin Override Rights** | **Admin** | Authorized override of team selection, audited post-event attendance correction, and audited recording of external Chief Leader acceptance | **APPROVED** | Decision Group 3 APPROVED (selection); Decision 2 APPROVED (audited attendance correction); Chief Leader Flow APPROVED (external acceptance). | All overrides, attendance corrections, and external acceptances require an immutable audit trail with justification. |
| | **Chief Leader** | None | **KNOWN** | Primary operational actor; does not perform administrative overrides. | None. |
| | **Leader** | None | **KNOWN** | Section 4: Absent from Leader scope. | None. |
| **10. Leader Cancellation & Replacement** | **Admin** | Oversight & emergency cancellation/replacement | **APPROVED** | Decision Group 4 APPROVED: Admin can execute if needed; full audit trail required. | Admin oversight safety net. |
| | **Chief Leader** | Select replacement from available candidates for assigned event | **APPROVED** | Section 4: "Select and replace Leaders when permitted"; Decision Group 4 APPROVED. | Chief Leader directly handles replacements on assigned events upon cancellation notification. |
| | **Leader** | Cancel own participation (requires explicit confirmation & reason) | **APPROVED** | Decision Group 4 APPROVED: Controlled, auditable action with reason; notifies Chief Leader/Admin. | Cannot select own replacement; creates staffing gap. |
| **11. Final Confirmation** | **Admin** | Oversight of confirmed team roster | **APPROVED** | Decision Group 5 & Blocker 1 APPROVED: Selection immediately confirms team placement. | Direct assignment takes immediate effect; no separate confirmation step. |
| | **Chief Leader** | View confirmed team roster for assigned event | **APPROVED** | Decision Group 5 & Blocker 1 APPROVED: Selected candidates are immediately confirmed. | Chief Leader is notified of any cancellations. |
| | **Leader** | Immediate confirmation upon selection / direct assignment | **APPROVED** | Decision Group 5 & Blocker 1 APPROVED: Selection takes immediate effect; Leader cancels if unable to attend. | No separate invitation or confirmation step in V1. |
| **12. Attendance Recording & Finalization** | **Admin** | View attendance records; execute audited post-event attendance corrections | **APPROVED** | Decision 2 APPROVED: Audited correction capability with mandatory reason; no routine approval step required. | None. |
| | **Chief Leader** | Record and directly finalize attendance for assigned events | **KNOWN / APPROVED** | Section 4: "Chief Leader: Record attendance"; Decision 2 APPROVED: Immediate finalization without admin approval gate. | What is the permitted time window for submitting attendance (e.g., event duration vs. 24h post-event)? (Status: **UNRESOLVED**) |
| | **Leader** | None | **KNOWN** | Section 4: Absent from Leader scope. | Can a Leader view their own recorded attendance status after an event? (Status: **PROPOSED**) |
| **13. Evaluation / Feedback Logging** | **Admin** | Review evaluations across events | **PROPOSED** (Review); **UNRESOLVED** (Authoring) | Section 4: "Review incidents and commitment-related information". | Can an Admin author an evaluation of a Leader, or is authoring reserved exclusively for on-site Chief Leaders? |
| | **Chief Leader** | Record post-event feedback and rate Leaders for assigned events | **KNOWN** | Section 4: "Record post-event feedback", "Rate Leaders after events" (adhering strictly to no synthetic formula rule). | What structured rubric is used for post-event evaluations? (Status: **UNRESOLVED**) |
| | **Leader** | Provide feedback on event or Chief Leader | **PROPOSED**; **UNRESOLVED** | Section 13 Decision 8: "Can Leaders submit feedback about the event or Chief Leader?" | Are Leaders permitted to evaluate the Chief Leader or the event organization? |
| **14. Sensitive Feedback Visibility** | **Admin** | Review Leader evaluations and commitment information | **KNOWN** | Section 4: "Access Leader history", "Review incidents and commitment-related information". | Are any internal Chief Leader evaluations restricted from Admins? |
| | **Chief Leader** | View feedback/evaluations for candidates and assigned team | **PROPOSED**; **UNRESOLVED** | Section 4: "View relevant Leader history and operational information". Section 13 Decision 8. | Can a Chief Leader view evaluations written by other Chief Leaders when reviewing candidates? |
| | **Leader** | View feedback explicitly authorized for Leader role | **KNOWN** (Conditional rule); **UNRESOLVED** (Authorized scope) | Section 4: "View only feedback explicitly authorized for the Leader role". Section 13 Decision 8. | Is Chief Leader feedback partially visible to the evaluated Leader, or strictly confidential to Admins/Chief Leaders? |
| **15. Incident Reporting** | **Admin** | Log or document incident records | **PROPOSED**; **UNRESOLVED** | Section 4 lists "Review incidents"; creation mechanism is not explicitly detailed. | Can Admin author an incident report retroactively based on offline stakeholder reporting? |
| | **Chief Leader** | Report incidents occurring during assigned events | **PROPOSED**; **UNRESOLVED** | Section 2 lists "Post-event feedback and incidents"; Section 8 lists Incidents module. Not explicitly itemized in Section 4. | Is incident reporting part of the post-event attendance flow, or an ad-hoc emergency submission? |
| | **Leader** | Submit safety or conduct incident report | **PROPOSED**; **UNRESOLVED** | Absent from Section 4; whistleblowing / incident reporting rights not established. | Can a Leader submit an incident report directly through the platform? |
| **16. Incident Visibility** | **Admin** | Review incidents | **KNOWN** | Section 4: "Admin / Operations: Review incidents and commitment-related information". | Can incident reports be shared with external governance or legal representatives? |
| | **Chief Leader** | View incidents associated with assigned events | **PROPOSED**; **UNRESOLVED** | Section 4 does NOT list incident viewing under Chief Leader scope. | Can a Chief Leader view past incidents involving candidate Leaders during team selection? |
| | **Leader** | None | **PROPOSED** (No access); **UNRESOLVED** (If involved) | Absent from Section 4. | Does a Leader have the right to view an incident report filed about their conduct? |
| **17. Leader History Viewing** | **Admin** | Access Leader history | **KNOWN** | Section 4: "Admin / Operations: Access Leader history". | Does Leader history include administrative notes and attendance history? |
| | **Chief Leader** | View relevant Leader history and operational information | **KNOWN** (High-level); **UNRESOLVED** (Scope) | Section 4: "View relevant Leader history and operational information". | Does "relevant" history include full past event attendance details or only factual aggregates? |
| | **Leader** | View own event history | **KNOWN** (Own history); **KNOWN** (No access to others) | Section 4: "Leader: View own event history". | Does own history include unconfirmed participation records or only attended events? (Status: **UNRESOLVED**) |
| **18. Account Management** | **Admin** | Manage authorized accounts | **KNOWN** | Section 4: "Admin / Operations: Manage authorized accounts". | What specific account operations are supported beyond provisioning and status toggles? |
| | **Chief Leader** | None | **KNOWN** | Section 4: Absent from Chief Leader scope. | None. |
| | **Leader** | None | **KNOWN** | Section 4: Absent from Leader scope. | None. |
| **19. Role Changes** | **Admin** | Assign and modify user roles | **PROPOSED**; **UNRESOLVED** | Account management is known, but role change workflows, multi-role users, and event-vs-user Chief Leader scoping are open (Section 13). | Can a user hold both Admin and Leader roles simultaneously, and what is the promotion workflow? |
| | **Chief Leader** | None | **KNOWN** | Section 4: Absent from Chief Leader scope. | None. |
| | **Leader** | None | **KNOWN** | Section 4: Absent from Leader scope. | None. |
| **20. Profile Editing** | **Admin** | Edit Leader profiles administratively | **PROPOSED**; **UNRESOLVED** | Section 4 lists "Manage authorized accounts", but direct modification of profile fields is not explicitly detailed. | Can Admin edit Leader contact details directly, or must the Leader edit their own profile? |
| | **Chief Leader** | None (for other Leaders' profiles) | **KNOWN** | Section 4: Absent from Chief Leader scope. | None. |
| | **Leader** | Maintain own profile | **KNOWN** (High-level); **UNRESOLVED** (Field list) | Section 4: "Leader: Maintain profile". Section 13 Decision 20. | Which specific fields are user-editable (phone, emergency contact, dietary) vs. read-only system fields? |
| **21. Account Provisioning** | **Admin** | Provision authorized accounts | **KNOWN** | Section 4: "Manage authorized accounts"; Section 10: "Authentication and secure account activation". | Are accounts provisioned individually or via batch list upload in V1? |
| | **Chief Leader** | None | **KNOWN** | Section 4: Absent from Chief Leader scope. | None. |
| | **Leader** | None | **KNOWN** | Section 4: Absent from Leader scope. | None. |
| **22. Invitation / Activation Flow** | **Admin** | Issue invitations / trigger activation | **PROPOSED**; **UNRESOLVED** | Section 13 Decision 9: "Authentication / invitation flow details: How do Leaders join Carpediem? Are accounts pre-created by Admins and activated via emailed invitation token, or is there an open sign-up with Admin approval?" | Does Admin trigger an automated email token or copy a secure one-time activation link? |
| | **Chief Leader** | None | **PROPOSED** | Section 4: Absent from Chief Leader scope. | Can a Chief Leader send an invitation link to a prospective Leader? (Status: **UNRESOLVED**) |
| | **Leader** | Complete account activation via issued credential token | **PROPOSED**; **UNRESOLVED** | Section 13 Decision 9. | What is the exact activation credential setup flow for a newly invited user? |
| **23. Deactivation / Reactivation** | **Admin** | Deactivate and reactivate user accounts | **PROPOSED**; **UNRESOLVED** | Section 13 Decision 10: "Account deactivation / reactivation rules: Can Leaders delete/deactivate their own profile? If deactivated, what happens to their historical event attendance records? What is the procedure for an Admin to reactivate an account?" | What is the formal procedure to reactivate a suspended account, and are historical records preserved? |
| | **Chief Leader** | None | **KNOWN** | Section 4: Absent from Chief Leader scope. | None. |
| | **Leader** | Request account deactivation or self-deactivate | **PROPOSED**; **UNRESOLVED** | Section 13 Decision 10. | Can a Leader self-deactivate or delete their account, or is it strictly an Admin request? |

---

## C. Known Permissions (Firmly Grounded in Canonical Source)

These permissions are explicitly documented in Section 4 and Section 5 of the locked canonical source of truth:

### Admin
1. **Event Creation**: Create events in the platform (Section 3 step 1, Section 4, Section 5).
2. **Event Viewing**: View events in the platform and view operational dashboards (Section 4).
3. **Event Management & Pre-Selection Editing**: Edit and manage events according to approved permissions prior to team selection (Section 4).
4. **Event Cancellation**: Cancel events according to approved permissions (Section 4).
5. **Chief Leader Assignment**: Assign Chief Leaders to events (Section 4, Section 5).
6. **Account Provisioning & Management**: Manage authorized accounts (Section 4).
7. **Leader History Access**: Access Leader operational history (Section 4).
8. **Incident Review**: Review incidents and commitment-related information (Section 4).
9. **Sensitive Feedback Access**: Review Leader feedback and commitment-related records (Section 4).

### Chief Leader
1. **Assigned Event Viewing**: View assigned events (Section 4).
2. **Candidate Review**: Review available and applying Leaders for assigned events (Section 4).
3. **Team Selection**: Select the required team from available candidates for assigned events (Section 3 step 4, Section 4).
4. **Attendance Recording**: Record attendance for assigned events (Section 4).
5. **Post-Event Feedback & Rating**: Record post-event feedback and rate Leaders after assigned events without synthetic scoring (Section 4).
6. **Operational History Viewing**: View relevant Leader history and operational information necessary for team selection (Section 4).
7. **Leader Replacement (Principle)**: Replace Leaders on assigned events when permitted (Section 4).

### Leader
1. **Relevant Event Viewing**: View relevant events (Section 4).
2. **Availability Indication**: Mark availability for upcoming events (Section 4).
3. **Invitation Response / Application (Concept)**: Apply or respond to invitations for relevant events (Section 4).
4. **Final Confirmation (Concept)**: Confirm participation when selected for an event (Section 4).
5. **Profile Maintenance (High-Level)**: Maintain personal profile (Section 4).
6. **Own Event History**: View own event history (Section 4).
7. **Authorized Feedback Viewing (Rule)**: View only feedback explicitly authorized for the Leader role (Section 4).
8. **In-Platform Notifications**: Receive in-platform operational notifications (Section 4).

---

## D. Proposed Permissions (Candidate Baseline Pending Stakeholder Approval)

These permissions are architectural candidates derived from the problem space, but are not yet locked:

1. **Admin Availability Dashboard Viewing**: Admins view aggregated availability metrics across all scheduled events.
2. **Admin Leader Replacement**: Admins execute Leader replacements directly if a Chief Leader is unavailable.
3. **Chief Leader Event Operational Notes**: Chief Leaders edit operational fields (meeting point, arrival instructions, briefing notes) for their assigned events without full Admin privileges.
4. **Chief Leader Candidate Evaluation Visibility**: Chief Leaders view past factual attendance metrics and sanitized ratings from other Chief Leaders during candidate selection.
5. **Chief Leader Incident Submission**: Chief Leaders log on-site incident reports during attendance/post-event reporting.
6. **Leader Attendance Viewing**: Leaders view their own finalized attendance record (e.g., Attended, Late, Excused) after event completion.
7. **Leader Deactivation Request**: Leaders submit a request to deactivate their account/profile rather than executing hard deletion.

---

## E. Unresolved Permissions (Explicitly Blocked from Implementation)

The following capabilities contain open ambiguities from Section 13 of the canonical source and must not be implemented until formally resolved:

1. **Admin Team-Selection Override**: Whether an Admin can unilaterally override or replace a Chief Leader's team selection.
2. **Admin Attendance Override**: Whether an Admin can unilaterally alter an attendance record submitted by a Chief Leader, and the required audit trail.
3. **Admin vs. Chief Leader Replacement Scope**: Whether a Chief Leader has full autonomy to replace dropped Leaders immediately, or if replacements require Admin confirmation.
4. **Post-Selection Event Editing**: Permitted Admin edit scope after Leaders have confirmed, and whether date/time/location edits invalidate existing confirmations.
5. **Application vs. Availability Mechanism**: Whether applying is an explicit application entity or a state within availability responses.
6. **Final Confirmation Rules & Deadlines**: Whether confirmation is strictly mandatory, deadlines for response, and automated non-response drops.
7. **Feedback Transparency**: Whether post-event ratings and notes authored by Chief Leaders are visible to evaluated Leaders, partially sanitized, or strictly confidential to Admins/Chief Leaders.
8. **Leader Feedback on Chief Leaders/Events**: Whether Leaders have permission to submit evaluations regarding their Chief Leader or event operations.
9. **Incident Viewing by Chief Leaders**: Whether Chief Leaders can view incident reports filed for other events or involving candidates during selection.
10. **Account Invitation & Activation Flow**: Specific tokenized invitation, email link, or registration approval mechanism.
11. **Account Deactivation & Reactivation Lifecycle**: Data retention, historical record handling, and Admin reactivation workflows.
12. **Role Change & Multi-Role Support**: Whether users can hold multiple active roles, and how role promotion/demotion operates.
13. **Leader Profile Editable Field Bounds**: Specific personal data fields that Leaders are permitted to modify versus system-controlled fields.

---

## F. Permission-Related Stakeholder Questions

To advance this matrix from `PROPOSED`/`UNRESOLVED` to `LOCKED`, stakeholder answers are required for the following 10 targeted questions:

1. **Team Selection Authority**: [RESOLVED / APPROVED] Chief Leader selects the team directly from available candidates (immediate effect). Admins maintain oversight and possess intervention/override authority when necessary, which must be audited.
2. **Attendance Finality & Overrides**: Does an Admin have the authority to edit or override an attendance record submitted by a Chief Leader, and is an explicit audit justification required?
3. **Replacement Autonomy & Cancellation**: [RESOLVED / APPROVED] Leaders may cancel their own participation via a controlled action requiring explicit confirmation and a reason. Cancellation preserves assignment history, notifies the Chief Leader and Admin, and creates a staffing gap. The assigned Chief Leader directly selects the replacement. Timing thresholds remain unresolved.
4. **Post-Selection Event Modifications**: [RESOLVED / APPROVED] Minor edits preserve roster; same-day time/location shifts preserve roster and notify personnel; date changes (true rescheduling) invalidate availability, reopen availability, preserve previous history immutably, and rebuild staffing for the new date.
5. **Feedback Transparency Policy**: Are post-event Leader evaluations written by Chief Leaders visible to the evaluated Leader (for personal development), or strictly confidential to Admins and Chief Leaders?
6. **Peer Feedback Rights**: Can Leaders submit structured feedback or evaluations regarding the event organization or their Chief Leader?
7. **Incident Report Access**: Can Chief Leaders view incident reports submitted by other Chief Leaders regarding a Leader when reviewing candidate history for team selection?
8. **Chief Leader Cross-Event Participation**: [RESOLVED / APPROVED] Chief Leaders exclusively lead events and cannot mark availability, apply, or participate as regular Leaders on any event. Role separation is strict in V1.
9. **Account Invitation Mechanism**: Will new Leaders be pre-created by Admins and sent an activation email link, or will Leaders register independently via an open form subject to Admin approval?
10. **Leader Profile Boundaries**: Exactly which fields can a Leader modify on their profile (e.g., phone, emergency contact, dietary requirements, shirt size) versus read-only fields managed by Admins (e.g., status, qualifications, certifications)?

---

## G. Dependencies on State Machines (Normalized Domain Decomposition)

> [!WARNING]
> **No Event State Machine Is Approved Yet.**
> States such as `Draft`, `Published`, `Completed`, and `Cancelled` are proposed candidate states.
> `In Progress` is a derived status based on time, not a persisted state.
> Missing attendance after an event finishes is an operational condition (`Pending Attendance`), not an Event lifecycle state.

Permissions are strictly decoupled and bound across five orthogonal lifecycles and a schedule boundary:

1. **ChiefLeaderAssignment Lifecycle**:
   - Model: `No ChiefLeaderAssignment` → `Pending` → `Active` (Accept) or `Declined` (Decline).
   - External confirmation exception: `Pending` → `Active` via Admin action "Record external acceptance" with full audit trail.
   - Publishing Invariant: An Event cannot transition from `Draft` to `Published` unless it possesses an accepted `Active` Chief Leader assignment (`Event.Status == Published => Event has exactly one active accepted ChiefLeaderAssignment`).
2. **AvailabilityResponse Lifecycle**:
   - Model: `No AvailabilityResponse` (UI shows "Response Needed") → `Available` ↔ `NotAvailable`.
   - Controls candidate pool filtering for Chief Leaders. Absence of a record is not persisted as a status.
3. **TeamAssignment Lifecycle**:
   - Model: `No TeamAssignment` (Unassigned) → `Active` → `Cancelled`.
   - Selection or direct drafting immediately creates an `Active` assignment. Controlled cancellation transitions assignment to `Cancelled`. Replacement creates a new `Active` assignment record. Absence of an assignment is not persisted as a status.
4. **AttendanceOutcome Lifecycle**:
   - Values: `Unrecorded` → `Attended` / `NoShow`.
   - Evaluated strictly against an `Active` `TeamAssignment` during or after event execution. Decoupled from Event completion.
5. **StaffingCycle Rescheduling Boundary**:
   - Date rescheduling rotates the `StaffingCycle`, archiving previous availability and assignments immutably, and reopening a fresh cycle for the new date.
6. **Blocking Rule**:
   - No ASP.NET Core authorization policy, database constraint, or Angular route guard shall be coded until the Event Lifecycle, Chief Leader Assignment, and normalized Participation Lifecycles are formally ratified.
