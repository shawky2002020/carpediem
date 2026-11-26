# Components and motion

Open the [interactive field guide](http://127.0.0.1:4216/design-lab) in the local development build. It uses the production shell, route icons, event context component, account layout, draft editor and global CSS. Its example actions update local specimen state. Shell destinations and product forms on normal routes use their usual services. The build enables the guide route only in development.

## Component decisions

| Family | Baseline finding and disposition | Implementation and review |
| --- | --- | --- |
| Shell and brand | RECOMPOSE. The broad dark rail dominated every record. | Pale sage desktop rail, compact official badge, forest active destination and a common set of line icons. Bottom shortcuts remain role-specific. |
| Mobile menu and contextual links | REPOSITION. Expanding the list pushed the entire task down the page. | Menu overlays the page below the masthead. Escape returns focus. Outside pointer input and keyboard focus leaving the menu dismiss it. Contextual back links stay with each task. |
| Inbox indicator and account | REFINE. Counter and sign-out looked like competing actions. | Compact numerical unread marker, accessible unread label, secondary account identity and exit control. |
| Primary, secondary, inline and destructive actions | REDESIGN. Equally boxed actions concealed priority. | Solid evergreen commit, pale secondary action, underlined inline link, squared destructive outline. Minimum action height is 44 CSS px. Disabled text uses darker ink on a pale fill. |
| Loading and selected actions | REFINE. Pending operations had little separation from resting state. | Disabled in-flight controls retain pending text. A busy specimen shows the keyed spinner. Radio and checkbox marks remain visible at rest and use native semantics. |
| Text, password, search, multiline, date and select | REDESIGN. An outline around every field competed with the form sections. | Filled writing area with a bottom edge, focus ring and separate error edge. Browser date pickers, keyboard interaction and password manager attributes are retained. |
| Checkbox, radio and segmented selection | REDESIGN. Native controls and ad hoc pressed buttons mixed unrelated weights. | Native inputs, explicit check or inset dot, forced-colors fallback. Attendance shows current text, pressed state and selected row edge. |
| Validation and form actions | REPOSITION. Sticky controls obscured content in isolated full-page captures. | The editor exposes field errors and focuses the actual error summary. Sticky commit actions clear mobile navigation. The specimen opts out of sticky positioning. |
| Account access | RECOMPOSE. Empty desktop surface and repetitive account branding. | Public print pairs with a measured form column. A shallow print band saves space on phones. |
| Event context | REBUILD. Title and schedule read like undifferentiated metadata. | Vermilion edge aligns the event title, venue, schedule and Cairo time. The reusable component suppresses the metadata line when both inputs are absent. |
| Status | REDESIGN. Tiny colored pills visually competed with buttons. | Dot, hollow circle, diamond or square precedes an explicit state label. Status stays legible in a monochrome reading. |
| Calendar, event lists, filters and pagination | RECOMPOSE. Repeated calendar tiles and a control on every row hid dates. | Date headings group events. Desktop uses a filter index beside the schedule. Mobile puts the active filter above the first event. Native disclosures retain secondary operations. |
| Rosters, candidate rows, replacement and attendance | RECOMPOSE. Existing roster volume displaced the next action. | Task-specific reading order, searchable candidate pools, visible replacement linkage and thumb-reachable attendance actions. The real workflow recordings exercise these controls. |
| Identity and history | RECOMPOSE. History and participant identity carried similar weight. | A nameplate introduces an actual participant. Counts and dated history entries come from the approved API. |
| Notification timeline and review queues | REFINE. Repetitive cards gave results the same emphasis as pending work. | Date groups and open rows align timestamps with the next action. Review forms stay with the record being changed. |
| Dialogs and sheets | REBUILD. Bare specimen retained browser frame. First mobile sheet suggested an unsupported drag gesture. | Native dialog and backdrop, constrained desktop width, bottom-aligned sheet on phones. Distinct safe and destructive choices. Removed the drag handle. |
| Loading, empty, error and success | REFINE. Blank gaps and repeated boxes obscured the state change. | Short status copy, quiet tonal empty regions, and messages with keyed edges. Retry stays next to the failure. |
| Unused families | REMOVE. Unused global KPI cards, filter pills, duplicate bottom bar, blurred sticky grid and continuously pulsing indicator had no live callers. | Removed those styles. The workbench has no standalone toast, tooltip, data-table or universal popover requirement. Real inline notices, disclosures and native pickers cover the existing interactions. |

## Typography and materials

Self-hosted Bricolage Grotesque carries titles, dates and participant names. Hanken Grotesk carries instructions, controls and variable-length operational records. Numbers use tabular figures where they must align. Headings wrap unbroken fixture titles at 200% text size. Both fonts retain their OFL licenses in the existing fonts directory. Both bundled faces cover Latin. System fallbacks handle other scripts. The product's approved UI content is English.

Paper and sage divide adjacent working areas. Evergreen carries foreground and strong action, vermilion identifies event context and the active mobile destination. Most lists need only a shared baseline and spacing. The official mark, the artwork's deeper green and warm orange connect public access to the work surfaces.

## Motion behavior

| Trigger | Behavior | Interruption or accessible alternative |
| --- | --- | --- |
| Press or selection | Press scales to 0.98. Selected inputs reveal their mark. Button color and field state respond over approximately 120 ms. | Native semantics and data update immediately. Forced colors use platform controls. |
| Route change | The incoming route briefly resolves from 88% opacity. Navigating deeper or shallower changes the direction of an 8 px movement. Lateral routes keep their position. | The last animation and queued frame are cancelled on successive navigation. Scroll and focus update independently. |
| Menu or disclosure | Menu changes opacity and offset below the stationary masthead. Opening details reveals the associated contents with a 2 px movement. | Escape and navigation remain immediate. Native disclosure handles keyboard input. |
| Confirmation | The surface moves at most 16 px. Opacity and the discrete top-layer transition carry opening and closing. | Native modal focus and dismissal remain available during transitions. |
| Feedback | A short 3 px reveal introduces an error summary or success message. Changes to filters, rosters and counts are immediate. | No artificial application delay. The recording harness can hold a fixture response to expose a pending state. |
| Reduced motion | Global media query cancels temporal transitions, spinner rotation and translation. The route animator bypasses movement entirely. | Selected, busy, success and error text convey the result. |

Timing tokens are 120, 180 and 240 ms for short, normal and extended changes. No animation dependency or spring overshoot is needed. The demo measurements cover a local Chromium session. Frame intervals measure overall delivery and cannot attribute individual frames to a CSS rule.

## Three review rounds

1. Compare the old and first redesigned controls. The black dialog outline survived the first pass. The secondary and destructive controls were too similar. [Before](components/before/guide-1440.png) · [First pass](components/round-1/confirmation-390.png)
2. Inspect the native sheet and event summary independently. An operations reviewer flagged the nonfunctional drag handle and disabled text contrast. The editor's sticky save row also interrupted its isolated specimen. [Second pass](components/round-2/guide-1440.png) · [Sheet before the handle removal](components/round-2/confirmation-390.png)
3. Remove the handle, deepen disabled ink, preserve field grouping in the specimen, and exercise focused and selected states. The final guide checks validation, keyboard return, a native radio, a checkbox, password visibility and reduced-motion dismissal. [Mobile sheet](components/after/confirmation-390.png) · [Keyboard focus](components/after/focus-1440.png) · [Selected controls](components/after/choices-390.png) · [Final guide](components/after/guide-1440.png)
