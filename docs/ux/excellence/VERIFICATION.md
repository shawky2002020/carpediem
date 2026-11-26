# Verification and recordings

## Mobile record review on 26 November 2026

The latest production build passed TypeScript and totaled 398.79 kB raw, with 104.52 kB estimated transfer. All 104 Angular tests, 430 Playwright cases and 184 SQL-backed API integration tests passed. The earlier 96-test domain result is recorded below.

Availability, appointment, assignment, feedback, incident reporting, cancellation and rescheduling passed checks at eight widths from 320 to 1920 px. The capture harness found zero issues across these 56 page/width combinations and 14 phone and desktop axe scans. A separate independent pass exercised keyboard interaction, a delayed save, repeated submission, error recovery and all seven routes at 320 px with doubled text. [Independent review and raw measurements](mobile-records/INDEPENDENT-REVIEW.md)

In the live rehearsal a Leader opened a direct link with an empty inbox. The draft read withheld the itinerary. After publication the page showed Mango Tree Campsite and Sunday 7 March 2027, 10:00–14:00 Cairo, matching the stored 08:00–12:00 UTC window. The rehearsal continued through staffing, attendance, closeout and the Leader's event history. A separate Leader request for an Admin resource returned HTTP 403.

## Earlier whole-product review

The comparison set covers 28 fixture cases across 27 product routes. The guest and authenticated home share a route. Eight viewport widths were exercised at 320, 375, 390, 430, 768, 1024, 1440 and 1920 CSS px. At 390 and 1440 px the harness captured a page screenshot and ran axe with WCAG A/AA tags. The original comparison baseline has 56 images. The final comparison set has 56. The responsive harness checks document overflow, missing fixture calls, static resource failures, browser exceptions and Angular error overlays. The two screens revised after independent review were recaptured at both sizes and rechecked at all eight widths. [Page comparisons](PAGE-REVIEW.md)

## What the recordings exercise

| Recording | Verified interaction |
| --- | --- |
| [Registration and sign-in](public/recordings/registration-and-sign-in.webm) | Invalid email, keyboard password visibility, pending request, approved registration response, navigation and sign-in error. |
| [Mobile navigation](recordings/mobile-navigation.webm) | Filter the queue, open the menu, enter account management, invoke the bottom sheet and restore focus after Escape. |
| [Draft validation](recordings/draft-validation.webm) | Enter the editor from the registry, submit with missing details, observe the error summary and correct the identity fields. |
| [Appointment](recordings/appointment-acceptance.webm) | Review an invitation, accept and inspect the committed state. |
| [Availability](recordings/availability-response.webm) | Commit a response and see the status change while the choices stay in position. |
| [Replacement](recordings/linked-replacement.webm) | Select the cancellation and replacement, enter the justification and confirm roster insertion. |
| [Attendance](recordings/attendance-finalization.webm) | Select outcomes, check totals, submit, see a pending request and confirm the persisted, finalized fixture records. |

The recordings use deterministic API responses and synthetic identities. Some fixture requests are held to expose an asynchronous state for the camera. The application adds no such wait. Motion review included decoding the navigation, validation and attendance footage into sequential frames. An earlier availability recording exposed a layout shift on success. The final response badge changes in place, with a separate live announcement.

## Browser and runtime evidence

| Check | Result |
| --- | --- |
| Production bundle and TypeScript | Angular production build within the repository's budgets. Initial bundle 398.63 kB raw and 104.53 kB estimated transfer. TypeScript passed. |
| Angular | 104 tests passed in 26 files. |
| Playwright regression suite | All 430 tests passed in the final run across five viewport projects, including five-point feedback, closeout, keyboard focus and 200% text. |
| SQL-backed API and domain | 183 integration tests on an isolated SQL Server 2025 instance and 96 domain tests passed, with zero skips. |
| Responsive and axe | 224 page/width checks and 56 tagged axe scans. Zero reported issues in the captured states. The two revised screens also passed their repeat checks. |
| Component field guide | Checked confirmation focus return, hover, keyboard focus, selected options, inline validation, account-password visibility, reduced motion and two additional tagged axe scans. |
| Browser stress | Chromium, Firefox and WebKit each passed the 320 px, 200% dashboard-text case with 60 records, unbroken titles, attention filtering and draft pagination. All three role-home variants also passed the enlarged-text check. A separate final pass checked all 28 cases at 320 px and 200% text and found zero document overflow or browser exceptions. |
| Live integration | Admin drafted and published an event, Chief accepted an appointment and selected a team, Leader responded and opened a cancellation dialog. The Leader saw the authorization denial and HTTP 403. Separate local rehearsals completed attendance and history, and rescheduled and cancelled an event. The final closeout composition passed a subsequent live rehearsal. |
| Devices and assistive technology | Desktop browser engines and emulated phone viewports. Physical-device use, outdoor sunlight, human screen-reader use and field latency remain unmeasured. |

## Performance observations

The first desktop draft recording had 0.140 cumulative layout shift. Inspection of browser shift sources identified the main region moving 228 px as the signed-in rail appeared. Reserving the desktop workspace layout while the initial session resolves removed this shift in the final recorded draft journey.

| Production Chromium journey | LCP | CLS | p95 frame interval | Intervals above 50 ms |
| --- | ---: | ---: | ---: | ---: |
| Mobile navigation | 156 ms | 0.0658 | 16.8 ms | 0 |
| Desktop draft validation | 172 ms | 0 | 16.8 ms | 0 |
| Mobile attendance | 132 ms | 0 | 16.8 ms | 0 |

The performance observer sampled 125, 68 and 89 intervals respectively on the local browser host. All three sessions reported zero page exceptions. These measurements are single local runs with synthetic network responses. The mobile navigation run still shows measurable initial movement. [Raw measurements](recordings/measurements.json)

## Reproduction

Use the repository's Angular development server for the component guide. Use the production build with SPA fallback for release captures. From the repository root, set `CARPEDIEM_BASE_URL` and `CARPEDIEM_CAPTURE_DIR=docs/ux/excellence/after` for the page harness. [Page capture](../../../frontend/scripts/capture-transformation.mjs) · [Guide capture](../../../frontend/scripts/capture-field-guide.mjs) · [Fixture data](../../../frontend/scripts/visual-fixtures.mjs)

Run the three recording harnesses from the same root. The entry harness also checks the expanded-text role workspaces. [Entry and access](../../../frontend/scripts/capture-entry-interactions.mjs) · [Navigation, draft and attendance](../../../frontend/scripts/record-field-workflows.mjs) · [Response workflows](../../../frontend/scripts/record-response-workflows.mjs)
