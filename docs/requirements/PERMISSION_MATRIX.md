# Draft Role & Permission Matrix (Superseded)

> [!CAUTION]
> **SUPERSEDED BY [ROLE_PERMISSION_MATRIX.md](file:///c:/dev/web/Carpediem/docs/requirements/ROLE_PERMISSION_MATRIX.md)**
> This preliminary draft has been audited and superseded by the official Per-Role Permission Governance Matrix in `ROLE_PERMISSION_MATRIX.md`.
> Refer to [ROLE_PERMISSION_MATRIX.md](file:///c:/dev/web/Carpediem/docs/requirements/ROLE_PERMISSION_MATRIX.md) for the authoritative, per-role grounded classifications and open stakeholder questions.

---

## Permission Governance Matrix

| Action / Capability | Admin | Chief Leader | Leader | Classification | Notes / Open Questions |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Event Viewing** | Yes (All) | Yes (All / Assigned) | Yes (Eligible / Published) | **KNOWN** | Leaders can only view published events relevant to them. |
| **Event Creation** | Yes | No | No | **KNOWN** | Only Admins create events in V1. |
| **Event Editing** | Yes | Proposed: Limited | No | **UNRESOLVED** | Can Chief Leaders edit event details (meeting point, notes), or only Admins? |
| **Event Cancellation** | Yes | No | No | **KNOWN** | Only Admins cancel whole events. |
| **Chief Leader Assignment** | Yes | No | No | **KNOWN** | Admins assign Chief Leaders to events. |
| **Availability Response** | Proposed: View All | View (Assigned Event) | Yes (Own profile) | **KNOWN** | Leaders mark their own availability. |
| **Application Submission** | No | No | Yes (Own profile) | **UNRESOLVED** | Dependent on whether Application and Availability are separate flows. |
| **Team Selection** | Yes (Override) | Yes (Assigned Event) | No | **KNOWN** | Chief Leader selects the team; Admin retains oversight. |
| **Leader Replacement** | Yes | Proposed: Yes (Assigned) | No | **UNRESOLVED** | Can Chief Leader replace confirmed leaders directly or only propose replacements to Admin? |
| **Final Confirmation** | Proposed: Override | Proposed: View Status | Yes (Own Assignment) | **KNOWN** | Leaders execute final confirmation of their assignment. |
| **Attendance Recording** | Yes (Override) | Yes (Assigned Event) | No | **KNOWN** | Chief Leader logs attendance at the event. |
| **Evaluation / Feedback Logging** | Yes | Yes (Assigned Team) | Proposed: Event Feedback | **UNRESOLVED** | Chief Leader rates team; whether Leaders can provide event feedback is unresolved. |
| **Sensitive Feedback Viewing** | Yes | View (Assigned Team) | No | **UNRESOLVED** | Can Leaders view post-event feedback written about them by Chief Leaders? |
| **Incident Reporting** | Yes | Yes (Assigned Event) | Proposed: Submit Report | **PROPOSED** | Chief Leader logs incidents; whether Leaders can report incidents is open. |
| **Incident Viewing** | Yes (All) | Yes (Assigned Event) | No | **KNOWN** | Incidents are confidential operational records. |
| **Leader History Viewing** | Yes (Full) | Yes (Assigned Candidates) | Yes (Own History Only) | **KNOWN** | Chief Leader views candidate history to inform team selection. |
| **Account Management** | Yes | No | No | **KNOWN** | Admins manage authorized accounts and role assignments. |
| **Profile Self-Editing** | Yes (Any) | Yes (Own) | Yes (Own) | **PROPOSED** | Leaders update own contact details, skills, and emergency contact. |
| **Account Activation / Invite** | Yes | No | No | **KNOWN** | Admin invites new users into the platform. |
| **Account Deactivation** | Yes | No | Proposed: Request Only | **UNRESOLVED** | Can Leaders self-deactivate, or is deactivation solely an Admin function? |

---

## Key Open Permission Decisions Requiring Stakeholder Resolution

1. **Admin vs. Chief Leader Replacement Scope**: Does a Chief Leader have total autonomy to replace a dropped Leader immediately, or does the system notify an Admin?
2. **Feedback Transparency**: Is Chief Leader feedback about a Leader visible to that Leader, partially redacted, or strictly confidential to Admins/Chief Leaders?
3. **Event Editing Bounds Post-Selection**: If an Admin edits an event after a team is selected, who receives automatic alerts and does it reset leader confirmations?
