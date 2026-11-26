# Operational screens and independent design review

The mobile attendance roll and desktop incident review are the strongest operational compositions. A Chief can mark a roster in place. An Admin can read a report while writing the follow-up beside it. Both layouts turn a real task into the organizing principle for the page. [Attendance](round-3/attendance-390.png) · [Incident review](round-3/incident-review-1440.png)

## Visual debt and redesign decisions

| Screen or family | Action | Rejected baseline and resulting decision |
| --- | --- | --- |
| Draft Events | REDESIGN | Identical text rows buried the dates. A dated leaf aligns the list by month and day. Each row opens the complete draft and retains status, location and Cairo time. |
| Create and edit draft | REBUILD | One narrow box stacked dates, location, a timezone alert and notes. Group title with location and place the Cairo hint beside the schedule fields. The edit page pairs the Chief appointment prerequisite with publication. |
| Appoint Chief Leader | REDESIGN | Heavy outlines made every radio option look like an individual card. A native radio list now uses a common baseline and a tinted selection. |
| Pending appointments | RECOMPOSE | Each invitation occupied a generic panel. Invitations now use full-width links with event, location, date and a trailing destination cue. |
| Appointment decision | RECOMPOSE | Separate schedule and venue facts from the accept/decline decision within a single invitation. Use native buttons and the existing server response. |
| Availability | REBUILD | A white card gave two equally valid answers very different prominence. Put the event name on a field of evergreen beside two equally weighted choices. Change only the committed response to a solid fill and announce the result. |
| Assignment | RECOMPOSE | Nested cards gave the event and its cancellation action similar weight. Pair the itinerary and venue with a separate assignment-status area. Keep the destructive confirmation in a native dialog. |
| Candidate selection and direct assignment | REPOSITION | Desktop led with people already on the roster. Candidate search now starts the reading and tab order, with an adjacent roster on desktop and a direct roster jump on phones. Distinguish direct drafting from candidates who volunteered. |
| Replacements | REFINE | Show the cancelled assignment the Chief has chosen in a disclosure. Keep candidate search above the pool and existing members in an adjacent column. |
| Attendance | REBUILD | Four dark statistic tiles dominated the roster. Compact totals and a paired outcome control make space for the working roster. Changing an outcome updates the visible total immediately. |
| Profile and Leader history | RECOMPOSE | Separate the person's identity and factual participation totals from the event history. At desktop sizes the event and its record of participation can share a row. |
| Inbox | REDESIGN | Identical stacked notification outlines obscured grouping. Give date headings their own track on desktop and mark unread rows with a tonal fill, edge and label. |
| Reschedule, completion and cancellation | RECOMPOSE | Use the consequence and the action as the two parts of the composition. Keep UTC explicit in rescheduling. Keep cancellation justification and the non-destructive exit together. |
| Event feedback and feedback review | REDESIGN | Put the named event beside the actual rating form. Give recorded scores typographic emphasis, leave the comment as readable prose and size the submit action to its label. |
| Event incidents and incident review | REBUILD | Previously the reviewing Admin faced a full-canvas textarea beneath a report. Put the original text and review note side by side on desktop, in reading order on phones. Event reporters see their own form with its reports section. |
| Account management | RECOMPOSE | Align account identity, role choices and activation action in one desktop row. Preserve the native role checkboxes and focus-managed confirmation. |
| Authorization, identity, factual counts, concurrency, form validation | KEEP | Use the existing role permissions and data. All displayed counts come from the event or roster response. |
| Repeated timezone alert, nested card edges, duplicated local dialog decoration | REMOVE | Put the timezone instruction next to the dates. Let grouped content and the shared overlay supply hierarchy. |

The comparison set includes all 22 operational route cases at 390 and 1440 CSS pixels. These are deterministic browser fixtures. The source fixture uses example names, events and dates. [Baseline captures](../before) · [Current captures](../after)

## Review history

1. **First composition.** Rendered drafts, appointment review, profile and availability. The date index made the list easier to scan and editorial sections gave the long draft form structure. The optional notes area still broke the alignment. Availability also implied that “Available” was the preferred choice. [Draft editor](round-1/draft-create-1440.png) · [Availability](round-1/availability-1440.png)
2. **Hierarchy and decisions.** Realigned notes with the other fields and paired the date inputs whenever space permits. Replaced the primary/secondary availability buttons with equal controls. Reversed the desktop staffing composition, placed reading and response side by side for incident reviewers, and removed duplicated local dialog styling. At 390 px the first attendance totals occupied two rows, and differently sized response controls made the mobile availability prompt untidy. [Draft editor](round-2/draft-create-1440.png) · [Attendance](round-2/attendance-390.png) · [Availability](round-2/availability-390.png)
3. **Mobile interaction.** Allowed four compact attendance totals to sit in a row and reflow with enlarged text. Aligned and widened the availability choices on phones. Brought the actual candidate list ahead of the roster in the DOM and added a phone roster jump. An independent reviewer flagged the seamless idle fill between attendance choices. A divider now distinguishes them. Video review found that adding a visible success sentence shifted the response controls. The status badge now supplies persistent visual feedback while the live region announces the change without displacing the choices. [Roster](round-3/team-390.png) · [Final resolution on availability](../recordings/availability-response.webm) · [Attendance](../after/attendance-390.png)

## Independent criticism of the shared and public work

The first confirmation looked like an unstyled browser dialog, with a black frame around two closely spaced actions. The second rendering gave the mobile sheet an intentional silhouette, inset and hierarchy. The first desktop action strip also needed a stronger difference between a text destination and a destructive action. The second version addresses both. I flagged disabled-label contrast and the sheet's drag-like handle for the final shared review. [First confirmation](../components/round-1/confirmation-390.png) · [Second confirmation](../components/round-2/confirmation-390.png) · [Action hierarchy](../components/round-2/actions-1440.png)

The welcome artwork has a memorable mango canopy and legible brand signature. The first headline stranded “team” on its own desktop line, with tight tracking. The mobile dashboard also repeated the same event time and cost the user space before the first task. The public designer revised the heading and time hierarchy in response. [Welcome under review](../public/round-1/welcome-1440.png) · [Dashboard under review](../public/round-1/operations-390.png)

## Competitive evaluation

| Criterion | Weighted points | Evidence and deduction |
| --- | ---: | --- |
| Shared component craftsmanship | 18 / 20 | Controls have a readable hierarchy, paired outcomes, visible focus and consistent touch sizes. The native rating select remains a relatively ordinary part of the experience. |
| Page composition and art direction | 13 / 15 | The draft editor, incident review and staffing workbench have task-specific layouts. Closeout places two actions beside a short notice and feels sparse at a wide desktop size. |
| Originality and brand identity | 13 / 15 | The poster and official signature give the public experience a recognizable character. Some single-record operational pages still rely heavily on the same green-and-paper split. |
| Motion and interaction | 13 / 15 | Navigation, validation, saving, committed selection and attendance were exercised. The short local recordings do not establish consistency under slow field networks. |
| Mobile experience | 9 / 10 | Candidate access, important response controls and one-hand attendance improve materially. Long mobile lists still require deliberate scrolling to reach all related tasks. |
| Typography and hierarchy | 9 / 10 | Dates, names, status and decision controls have distinct positions and weights. Some long headings remain visually dense. |
| Task efficiency | 4 / 5 | Availability still lacks an authoritative schedule or venue. Direct entry can give a Leader too little evidence for a decision. |
| Cross-product consistency | 5 / 5 | Role navigation, field grouping, status semantics and shared confirmation treatment carry through the reviewed routes. |
| Accessibility and performance | 4 / 5 | Native input behavior and keyboard flows are verified. Field hardware, sustained slow-network sessions and human screen-reader use remain unmeasured. |
| **Internal assessment** | **88 / 100** | **The evidence supports a substantial visual improvement. It falls short of the requested 95-point target.** |

The three most useful recordings for comparing choices and their outcomes show a Leader responding, a Chief accepting an appointment and a Chief linking a replacement to a cancelled assignment. The harness asserts the resulting state and uses local request fixtures. Inspecting the video frames also revealed the success-message movement corrected in the availability flow. [Availability](../recordings/availability-response.webm) · [Appointment](../recordings/appointment-acceptance.webm) · [Replacement](../recordings/linked-replacement.webm) · [Recording harness](../../../../frontend/scripts/record-response-workflows.mjs)

The chief remaining design opportunities are the compact closeout screen, feedback rating entry and availability context. The replacement response still cannot settle whether an older cancellation was already linked on another session. Photography and identifying activity media remain subject to the existing reuse restrictions. [Asset inventory](../../transformation/ASSET-INVENTORY.md)

## Independent review after feedback and closeout reconstruction

**Revised assessment: 90 / 100.** Shared craftsmanship rises from 18 to 19 points and composition from 13 to 14. The other seven category assessments stand. Both revised screens resolve the specific weakness identified in the earlier review.

The feedback scale shows all five possible values when the form opens. Each number has a distinct touch target. In Chromium at 390 px, Space selected the first number, two Right Arrow presses selected and focused the third, and Tab moved to the comment. FormData returned the submitted value as 3. The visible focus ring is clear against the resting choices. The desktop feedback page still gives considerable visual weight to the event name beside a short form. [Mobile feedback](final-review/event-feedback-390.png) · [Keyboard selection](final-review/feedback-keyboard-390.png) · [Desktop feedback](final-review/event-feedback-1440.png)

Closeout now reads in one direction from event name to consequence to confirmation. On desktop the divider connects the two actions to the consequence they apply to. The same grouping fits the phone without separating an action from its explanation. Four independent captures at 390 and 1440 px reported zero horizontal overflows, browser errors, missing fixture requests or tagged axe violations. [Mobile closeout](final-review/complete-390.png) · [Desktop closeout](final-review/complete-1440.png)

The largest remaining usability deduction belongs to availability context. The single-record views also share a similar two-tone composition. Field hardware, human screen-reader use and a blinded comparison with other designs remain outside the evidence.

The subsequent review of availability and single-record pages raised the product score to **95/100**. It includes the direct-link test, mobile captures, response failure recovery and the final deductions. [Independent mobile review](../mobile-records/INDEPENDENT-REVIEW.md)
