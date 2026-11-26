# Page inventory

This table describes 27 user-facing routes. The home route has separate guest and signed-in compositions. Contextual dialogs and state variants follow the table.

| Route | Roles | Intent and primary action | Secondary actions and dependencies | Priority and disposition |
| --- | --- | --- | --- | --- |
| `/` guest | Guest | Understand Carpediem and sign in | Register. Session and brand wordmark. | P1 REBUILD |
| `/` signed in | Any signed-in role | Open the relevant workspace | Role-specific links. Session. | P1 REBUILD |
| `/sign-in` | Guest | Sign in | Registration, client validation, error recovery. Session and antiforgery. | P0 REBUILD |
| `/register` | Guest | Register as Leader | Sign in and pending approval. Account API. | P1 REBUILD |
| `/access-denied` | Any | Understand access restriction and return | Role guards and home. | P1 POLISH |
| `/dashboard` | Admin, Chief | Triage events and open the next action | Filter, refresh, draft, roster, attendance, replacement, closeout, incident and cancellation paths. Dashboard store. | P0 REBUILD |
| `/draft-events` | Admin | Open or create a draft | Pagination and retry. Draft list. | P1 REDESIGN |
| `/draft-events/new` | Admin | Create a draft | Cancel, validate, retry the same request after uncertain save. Typed form and idempotency. | P1 REDESIGN |
| `/draft-events/:id` | Admin | Edit or publish | Appointment, confirmation, recover stale edits. Draft and appointment APIs. | P0 REDESIGN |
| `/draft-events/:id/chief-leader` | Admin | Select and appoint a Chief Leader | Record approved external acceptance, view declines, retry. Candidate and idempotent appointment contract. | P1 POLISH |
| `/my-appointments` | Chief | Find a pending appointment | Empty and recovery. Appointment list. | P1 POLISH |
| `/my-appointments/:id` | Chief | Accept or decline | Current state and reply history. Assignment ID and version. | P0 POLISH |
| `/events/:eventId/team-selection` | Admin, assigned Chief | Compare candidates and select a team | Search, direct assignment, view history and active roster. Staffing cycle and assigned-Chief policy. | P0 REBUILD |
| `/events/:eventId/replacement-assignment` | Admin, assigned Chief | Select a replacement | Optional cancellation link or note and roster review. Existing cycle and version rules. | P0 REDESIGN |
| `/events/:eventId/attendance` | Admin, assigned Chief | Mark and finalize attendance | Bulk attendance, closed state and Admin-only correction. Attendance API. | P0 REBUILD |
| `/events/:eventId/availability` | Leader | Submit or update availability | Recover stale responses; closed and own-Chief states. Current cycle. | P0 REDESIGN |
| `/events/:eventId/my-assignment` | Leader with assignment | Review assignment | Self-cancellation with reason and notice. Ownership and timing policy. | P1 REDESIGN |
| `/notifications` | Signed in | Read and act on event updates | Mark read, bulk read, retry, contextual links. Inbox poll and APIs. | P0 REDESIGN |
| `/profile` | Leader | Review participation history | Open current assignment, authorized feedback and incidents. Profile read model. | P1 REBUILD |
| `/events/:eventId/leaders/:leaderId/history` | Admin, authorized Chief | Review candidate history | Return to staffing. Event-scoped API. | P1 REDESIGN |
| `/events/:eventId/reschedule` | Admin | Reschedule using a valid version | Review consequence, retry stale context or cancel. Current version and UTC contract. | P0 REDESIGN |
| `/events/:eventId/complete` | Admin | Complete a published event | Review consequence, retry, cancel. Admin context and completion API. | P0 REDESIGN |
| `/events/:eventId/cancel` | Admin | Cancel with a reason | Keep the event or retry after conflict. Admin context and cancellation API. | P1 POLISH |
| `/admin/accounts` | Admin | Approve or reactivate accounts | Roles, deactivation confirmation, pagination and retry. Account Admin API. | P1 REDESIGN |
| `/events/:eventId/feedback` | Resource-authorized | Submit an eligible rating | View own submitted ratings. Server feedback scope. | P1 POLISH |
| `/admin/feedback` | Admin | Read submitted feedback | Pagination and retry. Admin feedback API. | P2 POLISH |
| `/events/:eventId/incidents` | Resource-authorized | Report an incident | View permitted reports and review notes. Server incident scope. | P1 POLISH |
| `/admin/incidents` | Admin | Review an incident | Add a note, paginate, retry. Admin incident API. | P1 POLISH |

The existing guarded routes remain available. A wildcard redirects to home. Contextual surfaces include publish confirmation, departure with unsaved draft changes, external acceptance, Leader cancellation, attendance correction, and account deactivation. They also include mobile navigation, dashboard action disclosure, replacement options, and assignment history.
