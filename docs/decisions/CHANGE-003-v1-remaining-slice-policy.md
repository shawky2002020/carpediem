# Change Record: CHANGE-003 — Remaining V1 Slice Policy

> **Date:** 2026-10-22  
> **Status:** Ratified by product stakeholder in this implementation session  
> **Scope:** SLICE-13 and SLICE-20–22

The stakeholder selected the following V1 policies to resolve the roadmap's open decision gates. Detailed validation and presentation choices should follow these policies without introducing new lifecycle states or exposing one user's private records to another.

| Slice | Ratified policy |
| --- | --- |
| SLICE-13 Event Cancellation | Preserve existing assignments as historical records, remove them from active rosters, and notify affected Leaders. The locked lifecycle specification already requires an Admin reason, Event `Cancelled` status, current active assignments transitioning to `Cancelled`, and Chief Leader notification for a Published Event. Draft cancellation has no Leader-facing notification. |
| SLICE-20 Account Administration | Allow public Leader self-registration. Newly registered accounts require Admin approval before sign-in or event access. |
| SLICE-21 Feedback | Use simple ratings visible to Admins. Chief Leaders rate Leaders after events; Leaders rate events. |
| SLICE-22 Incidents | Use a basic incident log with Admin review. Admins see all reports; Chief Leaders see reports only for events they actively lead. |

This record does not authorize synthetic volunteer scores, public access to operational history, external notification channels, or automatic incident escalation. Values such as rating scale labels and incident form lengths are implementation details to define in the corresponding slice specs.
