# Experience audit

The product coordinates internal event staffing. Administrators need to see where an event is blocked and how to move it forward. Chief Leaders need to find eligible people, see the current roster and capture attendance on site. Leaders need to tell which event prompted a notification and respond quickly from a phone.

Discovery used the [locked project resource](https://app.notion.com/p/Carpediem-Project-Resource-Locked-Engineering-Source-of-Truth-3ddbf826725381938090c73592b2ad83), the [engineering constitution](../../../.specify/memory/constitution.md), the [approved feature slices](../../roadmap/V1_FEATURE_SLICES.md), [role matrix](../../requirements/ROLE_PERMISSION_MATRIX.md), [ADRs](../../decisions/README.md), [API notes](../../api/README.md), active Angular guards/routes and client calls, and the corresponding ASP.NET endpoints and stores. The repository's BMAD artifacts cover notification, history, and dashboard slices. The route inventory below reflects the implementation.

## Findings in operational order

| Priority | Finding | Evidence and consequence | Treatment |
| --- | --- | --- | --- |
| P0 | Reschedule and closeout deep links lacked a concurrency version. | Dashboard route links supplied no history state. Both mutations require a valid event version. | Load the current version and event title from the existing Admin context. Keep the server's concurrency check and recover on a stale version. |
| P0 | Appointment notifications used event ID in an assignment-ID route. | The notification contract supplies the event ID and title. The appointment detail API expects the appointment's ID. | Open the appointment list, then use the actual appointment ID from the result. |
| P0 | Profile and staffing implied facts the APIs do not expose. | Fixed public metrics; synthetic service hours, reliability, qualifications and staffing percentages. | Omit claims and use the returned counts and statuses. |
| P1 | Navigation used over 200 px at phone width and repeated links in two places. | [390 px dashboard](screenshots/before/operations-390.png). Content and primary event actions start below competing page furniture. | Add a compact workspace menu, focused quick navigation and a desktop rail. |
| P1 | Dashboard filters showed counts from different predicates than the rows they displayed. | Published, Closed and Needs attention diverged from some corresponding header counts. | Derive counts and rows from the same groups. |
| P1 | Candidate lists and attendance relied on decorative progress graphics and variable-density rows. | [Team selection](screenshots/before/team-390.png) and [roll call](screenshots/before/attendance-390.png). | Use factual context, a searchable roster, and adjacent attendance actions. |
| P1 | Some confirmation interfaces claimed modal semantics without making the background inert. | Inspect publish and Leader cancellation before opening keyboard focus sequences. | Use native dialogs for destructive or consequential confirmation. Present event management as routable forms. |
| P1 | Draft pagination was only implemented at the API. | The first request returned up to 20 records with no front-end route through older rows. | Add Previous and Next using the existing pagination contract. |
| P2 | Repetitive panels, uppercase labels, and mixed icon treatments competed with the task. | [Welcome](screenshots/before/welcome-1440.png), [draft list](screenshots/before/drafts-1440.png), and [profile](screenshots/before/profile-1440.png). | Use the supplied brand art at entry, flat lists for queues, and explicit headings for action. |

## Product decisions outside the redesign

- The availability response has no event name, schedule or location. The page displays the name when a matching notice is available in the signed-in Leader's inbox. It still lacks a current schedule and venue. No standalone Leader discovery or general published-event detail route is implemented.
- There is no separate application or final-confirmation screen in use. The approved implementation is availability and active assignment under the current slices.
- The replacement context returns cancelled assignments. Previously linked cancellations cannot be identified reliably on reload from that payload.
- The implementation provides factual attendance and cancellation counts. A commitment formula and qualification data are outside the current contract.
- Profile self-editing, Arabic localization, and broader feedback visibility require approved product scope. The current UI is English and uses logical layout properties.
