# Visual quality and verification

## Browser evidence

The before set contains 26 Chromium screenshots across 13 core cases at 390 and 1440 px. The after set contains 56 screenshots across 28 cases, including both home experiences and every user-facing route. These screenshots use deterministic API fixtures and test accounts. The live SQL browser rehearsals are separate.

| Working surface | Desktop | Mobile |
| --- | --- | --- |
| Welcome | [Before](screenshots/before/welcome-1440.png) · [After](screenshots/after/welcome-1440.png) | [Before](screenshots/before/welcome-390.png) · [After](screenshots/after/welcome-390.png) |
| Sign in | [Before](screenshots/before/sign-in-1440.png) · [After](screenshots/after/sign-in-1440.png) | [Before](screenshots/before/sign-in-390.png) · [After](screenshots/after/sign-in-390.png) |
| Dashboard | [Before](screenshots/before/operations-1440.png) · [After](screenshots/after/operations-1440.png) | [Before](screenshots/before/operations-390.png) · [After](screenshots/after/operations-390.png) |
| Draft creation | [Before](screenshots/before/draft-create-1440.png) · [After](screenshots/after/draft-create-1440.png) | [Before](screenshots/before/draft-create-390.png) · [After](screenshots/after/draft-create-390.png) |
| Team selection | [Before](screenshots/before/team-1440.png) · [After](screenshots/after/team-1440.png) | [Before](screenshots/before/team-390.png) · [After](screenshots/after/team-390.png) |
| Replacements | [Before](screenshots/before/replacement-1440.png) · [After](screenshots/after/replacement-1440.png) | [Before](screenshots/before/replacement-390.png) · [After](screenshots/after/replacement-390.png) |
| Attendance | [Before](screenshots/before/attendance-1440.png) · [After](screenshots/after/attendance-1440.png) | [Before](screenshots/before/attendance-390.png) · [After](screenshots/after/attendance-390.png) |
| Availability | [Before](screenshots/before/availability-1440.png) · [After](screenshots/after/availability-1440.png) | [Before](screenshots/before/availability-390.png) · [After](screenshots/after/availability-390.png) |
| Notifications | [Before](screenshots/before/inbox-1440.png) · [After](screenshots/after/inbox-1440.png) | [Before](screenshots/before/inbox-390.png) · [After](screenshots/after/inbox-390.png) |
| Profile | [Before](screenshots/before/profile-1440.png) · [After](screenshots/after/profile-1440.png) | [Before](screenshots/before/profile-390.png) · [After](screenshots/after/profile-390.png) |

Run `node frontend/scripts/capture-transformation.mjs after` from the repository root with the local Angular app on port 4216. The script accepts `CARPEDIEM_BASE_URL`, captures both evidence sizes, and checks for page exceptions, broken assets and horizontal overflow at 320, 375, 390, 430, 768, 1024, 1440 and 1920 px. It runs axe with WCAG A/AA tags at the two evidence sizes. The original working-tree baseline is recorded under [before](screenshots/before).

## Critique and refinement

- The first mobile shell crowded the draft save controls. The sticky row now clears the measured navigation boundary. Tests assert the full save control is in the viewport and above the bottom bar.
- At 200% text sizing, the masthead wordmark overflowed on a 320 px viewport. It now wraps with a constrained width. The dashboard date column and actions now reflow at the same width with long event names and 60 records in Chromium, Firefox and WebKit. Team selection, replacement, and attendance were also checked with a 14-person fixture at all eight sizes and both text settings.
- Candidate actions initially sat below the active roster on phones. A disclosure keeps the roster available while bringing search and selection forward. A jump link reaches direct assignment on longer lists.
- Replacement registration options initially preceded the candidate list in full. The disclosure now shows which cancellation will be linked while allowing immediate candidate review. Keyboard tests open the options and select the native radio.
- Mock and real browser journeys verified focus on native confirmations and return to meaningful controls. Mobile Escape returns focus to the menu button. The desktop sidebar highlights current destinations. The dedicated rescheduling and closeout routes use the normal page focus sequence.

## Internal quality assessment

Scores use the requested weights in order: craft 15, identity 15, efficiency 15, mobile 15, hierarchy 10, interaction 10, consistency 10, accessibility 5 and performance 5. These are review judgments from screenshots, keyboard checks and the reported test runs. Device and screen-reader evidence is still missing.

| Core case | Weighted points | Total | Remaining issue or evidence boundary |
| --- | --- | --- | --- |
| Welcome | 14 / 15 / 14 / 14 / 10 / 9 / 9 / 4 / 4 | 93 | The supplied mark carries the visual identity. Photography awaits clearance. |
| Sign in | 14 / 13 / 15 / 14 / 10 / 9 / 10 / 4 / 4 | 93 | Live browser sign-in exercised three role accounts. |
| Registration | 13 / 13 / 14 / 14 / 10 / 9 / 10 / 4 / 4 | 91 | Registration and approval exercised with Playwright fixtures. |
| Dashboard | 14 / 12 / 15 / 14 / 10 / 9 / 10 / 4 / 4 | 92 | Real event actions use authorized API data. |
| Draft list | 12 / 11 / 14 / 14 / 9 / 9 / 10 / 4 / 4 | 87 | Browser fixtures confirm paging across 60 records and long-title reflow. Field device evidence remains open. |
| Draft creation and edit | 13 / 11 / 15 / 14 / 10 / 10 / 10 / 4 / 4 | 91 | Verified save, dirty guard, publication and sticky control reachability. |
| Appointments | 12 / 11 / 14 / 14 / 9 / 9 / 10 / 4 / 4 | 87 | Appointment context and actions are visible. The notification opens the list because it carries only an event ID. |
| Team selection | 13 / 12 / 15 / 14 / 9 / 9 / 10 / 4 / 4 | 90 | The independent review scored 89 before the direct-assignment jump. |
| Replacement | 13 / 12 / 14 / 14 / 9 / 10 / 10 / 4 / 4 | 90 | The independent review scored 87 before the options disclosure. The API still cannot identify filled cancellations on reload. |
| Attendance | 13 / 12 / 15 / 14 / 10 / 9 / 9 / 4 / 4 | 90 | Compact controls and totals passed keyboard and long-roster checks. |
| Availability | 12 / 11 / 12 / 14 / 9 / 9 / 10 / 4 / 4 | 85 | A matching inbox notice supplies the title. Schedule and venue are absent from the authoritative response. |
| Assignment | 12 / 11 / 14 / 14 / 9 / 10 / 10 / 4 / 4 | 88 | The role-specific context and dialog work on the live backend. Content remains a compact single-record view. |
| Notifications | 13 / 11 / 15 / 14 / 9 / 10 / 10 / 4 / 4 | 90 | Actual appointment and availability navigation exercised end to end. |
| Participation history | 13 / 12 / 14 / 14 / 9 / 10 / 10 / 4 / 4 | 90 | Actual attendance appears in a Leader's history after closeout. |

Review and administration pages have the same screenshot and automated audit coverage. They share the field and status system and use compact lists where appropriate. The quality target of 90 on every core case is still open.
