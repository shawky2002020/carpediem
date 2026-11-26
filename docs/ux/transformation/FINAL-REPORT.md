# Carpediem transformation report

## Implementation

Carpediem now opens into a role-aware workspace. A desktop rail holds the working destinations, and phones use quick links with a full disclosure for the rest of the workspace. The public entry, sign-in and registration share a brand composition based on the campsite’s supplied wordmark. The design system uses self-hosted type, semantic color and status tokens, 44 px touch targets, visible focus, and restrained motion.

The inventory identifies **27 implemented routes** and **28 rendered cases** with separate guest and signed-in home experiences. Eight cases were rebuilt, eleven redesigned and nine polished. The welcome and workspace, authentication, dashboard, team selection, attendance and profile were rebuilt. The draft workflow, replacement, availability, assignment, inbox, event transitions, and account pages were redesigned. Appointment, denial, and review/reporting pages use the shared language. [Page inventory](CORE-PAGES.md)

- **Administrators** can scan event dates and attention filters, page through drafts, confirm publication or account deactivation, and enter reschedule and closeout from the dashboard with a current event version.
- **Chief Leaders** can use appointments as the entry point for decisions, search both staffing pools, review factual candidate history, assign replacements, and finalize attendance with controls that remain reachable.
- **Leaders** can follow a published event from the inbox, respond to availability, view assignments, cancel with a focused confirmation, and see recorded participation history.
- **Assets** include the official compact badge and the supplied closing-slide signature, exported to a 1.7 KB transparent WebP. The original Drive presentation, the campsite stills, and the activity folders were reviewed. [Asset inventory](ASSET-INVENTORY.md)

The design chose real task states in place of hard-coded public staffing figures, inferred hours, reliability percentages, and qualifications. It keeps the existing resource permissions, concurrency headers, staffing windows and role workflow. [Creative direction](CREATIVE-DIRECTION.md) · [User flows](USER-FLOWS.md) · [Design system](DESIGN-SYSTEM.md)

## Verification

| Evidence | Result |
| --- | --- |
| Angular production build and TypeScript | Passed. The measured production entry is about 385 KB raw and 98 KB estimated transfer. |
| Angular unit tests | 104 passed in 26 files. |
| Playwright suite | 430 passed across the repository's five viewport projects, including native Admin deactivation and queue reflow with expanded event actions. |
| Responsive and automated accessibility | 28 fixture cases at each of 320, 375, 390, 430, 768, 1024, 1440 and 1920 px. No horizontal overflow, page exceptions, missing fixture calls, or broken static responses. Axe WCAG A/AA tags returned zero reported violations in 56 audits. |
| Keyboard and large rosters | Menu focus return, appointment, publishing, candidate disclosures, assignment cancellation, linkage radios and attendance controls were exercised. The 14-person staffing fixture reflowed at 200% text at all eight widths. Chromium, Firefox and WebKit also passed a 60-event and draft-pagination stress test at 320 px with long content and 200% dashboard text. |
| Real browser integration | Two isolated local rehearsals signed in as Admin, Chief Leader and Leader. They exercised draft, appointment, publication, availability, selection, attendance, closeout, history, reschedule and cancellation. A Leader received the expected access denial in the app and HTTP 403 from the Admin endpoint. |
| Backend tests | 183 SQL integration and 96 domain tests passed with zero skips. ASP.NET Core ran in a Linux container against SQL Server 2025. |
| Lint and clean diff | The Angular package has no lint script. Angular template compilation, TypeScript and Git whitespace checks were used. |

The local SQL setup required a fresh test database and the existing migration script with quoted identifiers enabled. The stock Compose migrator logged SQL errors with its default session options. This limitation is in the development setup.

The [visual evidence and rubric](VISUAL-QA.md) link desktop and mobile comparisons. The evidence directory contains 26 before and 56 after screenshots.

## Remaining limitations

- The campsite photos and footage stay in the review area pending reuse and subject clearance. Event screens do not display unrelated imagery.
- Availability can show the title from a matching inbox notice. It still needs an approved, authoritative event schedule and venue for direct entry. There is no standalone event discovery surface in the current application.
- Replacement selection shows the cancelled assignment that will be linked. The API does not report whether a cancellation was already linked by a previous write.
- Draft listing, appointments, assignment and availability remain below the requested 90-point rubric on the recorded review. Device, screen-reader, and production field performance evidence is outside this verification run. The mission’s full acceptance gate remains open.
- The canonical project context leaves separate applications, confirmation rules, profile editing, and commitment calculations unresolved. Current approved slices are the basis for the redesign.

No production deployment was performed.
