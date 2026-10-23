# Carpediem — Phase 1 UI/UX & Accessibility Audit

**Audit Date:** 2026-10-22  
**Environment:** Angular 22 (`http://127.0.0.1:4200`) + ASP.NET Core 10 (`https://localhost:7076`) + SQL Server 2025 (`localhost:14333`)  
**Browser & Automation:** Chromium via Playwright + `@axe-core/playwright` (WCAG 2.2 Level A & AA tags)  
**Viewports Inspected:**
- Small Mobile: `360 × 800`
- Mobile: `390 × 844`
- Tablet: `768 × 1024`
- Desktop: `1440 × 900`

---

## 1. Executive Summary

A full-stack browser exploration was executed across **24 distinct application screens and states** spanning all three authenticated roles (**Admin**, **Chief Leader**, **Leader**) and unauthenticated public flows (**Home**, **Sign In**, **Leader Registration**), producing **93 screen-viewport audit captures**.

- **Horizontal Overflow:** `0` screens exhibited horizontal overflow (`scrollWidth <= innerWidth` across all 93 combinations).
- **Automated `axe-core` Violations:** `0` automated WCAG 2.2 AA rule violations were triggered in initial baseline scans, though manual DOM and visual inspection uncovered a nested `<div role="main">` landmark and a sticky footer obscuring form controls (`WCAG 2.4.11`).
- **Visual & UX Findings:** Real browser rendering revealed **10 concrete UI/UX and accessibility issues** across typography loading, design token consistency, sticky bar overlap, unstyled controls in later V1 slices, touch target dimensions (`< 44 × 44 px`), and workflow navigation links.

---

## 2. Screens & User Journeys Audited

| # | Screen / Route | Role | Viewports Audited |
| :--- | :--- | :--- | :--- |
| 01 | Home (`/`) | Public / Authenticated | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 02 | Sign In (`/sign-in`) | Public | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 03 | Leader Registration (`/register`) | Public | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 04 | Operational Dashboard (`/dashboard`) | Admin, Chief Leader | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 05 | Draft Events List (`/draft-events`) | Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 06 | Create / Edit Draft Event (`/draft-events/new`, `/draft-events/:id`) | Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 07 | Appoint Chief Leader & External Acceptance (`/draft-events/:id/chief-leader`) | Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 08 | Account & Role Administration (`/admin/accounts`) | Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 09 | Post-Event Feedback Review (`/admin/feedback`) | Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 10 | Incident Administration & Review (`/admin/incidents`) | Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 11 | Notification Inbox (`/notifications`) | Admin, Chief Leader, Leader | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 12 | Chief Leader Pending Appointments (`/my-appointments`) | Chief Leader | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 13 | Chief Leader Appointment Review (`/my-appointments/:id`) | Chief Leader | `1440×900` |
| 14 | Event Availability Declaration (`/events/:eventId/availability`) | Leader | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 15 | Factual Leader Profile & History (`/profile`, `/events/:eventId/leaders/:leaderId/history`) | Leader, Chief Leader, Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 16 | Team Selection & Direct Draft Pool (`/events/:eventId/team-selection`) | Chief Leader, Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 17 | My Event Assignment & Self-Cancellation Modal (`/events/:eventId/my-assignment`) | Leader | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 18 | Event Incident Reporting (`/events/:eventId/incidents`) | Leader, Chief Leader, Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 19 | Post-Event Rating Submission (`/events/:eventId/feedback`) | Leader, Chief Leader | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 20 | Replacement Leader Assignment (`/events/:eventId/replacement-assignment`) | Chief Leader, Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 21 | On-Site Attendance Roll Sheet & Admin Correction (`/events/:eventId/attendance`) | Chief Leader, Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 22 | Event Reschedule Dialog (`/events/:eventId/reschedule`) | Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 23 | Event Cancellation (`/events/:eventId/cancel`) | Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |
| 24 | Event Completion & Closeout Dialog (`/events/:eventId/complete`) | Admin | `360×800`, `390×844`, `768×1024`, `1440×900` |

---

## 3. Detailed Issue Catalog

### UI-001 — Sticky Form Action Bar Obscures Date/Time Inputs on Draft Event Edit
- **Location:** `frontend/src/app/features/draft-events/components/draft-event-form.component.scss` (`.form-actions`)
- **Severity:** Critical
- **Impact:** Usability & Accessibility (`WCAG 2.2 SC 2.4.11 Focus Not Obscured`). When the Draft Edit page renders Chief Leader and Publication panels above the form, `.form-actions` (`position: sticky; inset-block-end: 0; z-index: var(--cd-layer-sticky);`) floats directly over the `Start date and time *` and `End date and time *` controls when scrolling or capturing the page.
- **Evidence:** `06-admin-draft-edit-desktop-1440.png` shows the `Cancel` and `Save Changes` bar visually bisecting the `Start date and time *` input field.
- **Proposed Correction:** Remove `position: sticky; inset-block-end: 0;` from `.form-actions` so the form actions sit cleanly below the `Notes` field as a static footer bar.

### UI-002 — Self-Hosted Brand Typography (`Bricolage Grotesque` & `Hanken Grotesk`) Overridden in Global Styles
- **Location:** `frontend/src/styles.scss` and `frontend/public/fonts/`
- **Severity:** High
- **Impact:** Visual Identity & Typography Hierarchy. Although `design-system/styles/_semantic-tokens.scss` defines `--cd-font-display: 'Bricolage Grotesque'` and `--cd-font-ui: 'Hanken Grotesk'` and the repository contains the licensed WOFF2 subsets in `docs/ux/specimens/fonts/`, `frontend/src/styles.scss` overrode both variables with `ui-sans-serif, system-ui, -apple-system, 'Segoe UI', sans-serif` and never registered `@font-face`.
- **Evidence:** Computed `fontFamily` on `body` and `h1` across all 93 captures resolved to `ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif`.
- **Proposed Correction:** Copy `bricolage-grotesque-latin-600.woff2` and `hanken-grotesk-latin-variable.woff2` (with their SIL OFL license files) into `frontend/public/fonts/`, declare `@font-face` in `frontend/src/styles.scss`, and restore `--cd-font-display` and `--cd-font-ui` from the design system.

### UI-003 — Palette Fragmentation: Hardcoded Royal Blue (`#2563eb`) and Slate Gray Instead of Carpediem Forest (`#1D5041`)
- **Location:**
  - `frontend/src/app/features/replacement-assignment/components/replacement-assignment.component.scss`
  - `frontend/src/app/features/attendance/components/attendance-roll-sheet/attendance-roll-sheet.component.scss`
  - `frontend/src/app/features/event-reschedule/components/reschedule-dialog/reschedule-dialog.component.scss`
  - `frontend/src/app/features/event-completion/components/complete-event-dialog/complete-event-dialog.component.scss`
- **Severity:** High
- **Impact:** Brand Cohesion & Design System Consistency. Four mid-lifecycle screens use hardcoded `#2563eb` blue buttons and `#111827`/`#e5e7eb` grays instead of Carpediem's `--cd-action-primary` (`#1D5041` forest green) and warm neutral tokens (`--cd-bg-canvas`, `--cd-border-default`, `--cd-text-primary`).
- **Evidence:** `20-chief-replacement-desktop-1440.png`, `21-attendance-roll-sheet-desktop-1440.png`, `22-admin-reschedule-desktop-1440.png`, and `24-admin-complete-desktop-1440.png`.
- **Proposed Correction:** Refactor these four stylesheets to use Carpediem semantic CSS custom properties (`--cd-action-primary`, `--cd-action-primary-hover`, `--cd-text-primary`, `--cd-text-secondary`, `--cd-border-default`, `--cd-border-strong`, `--cd-status-*`, `--cd-font-display`).

### UI-004 — Unstyled Native Browser Controls on Later V1 Slices (`SLICE-13`, `SLICE-17`..`22`)
- **Location:**
  - `frontend/src/app/features/operational-dashboard/operational-dashboard.component.scss`
  - `frontend/src/app/features/accounts/account-admin.component.ts`
  - `frontend/src/app/features/engagement/feedback-admin.component.ts`
  - `frontend/src/app/features/engagement/incident-admin.component.ts`
  - `frontend/src/app/features/engagement/event-feedback.component.ts`
  - `frontend/src/app/features/engagement/event-incidents.component.ts`
  - `frontend/src/app/features/event-cancellation/event-cancellation.component.scss`
  - `frontend/src/app/features/notifications/notification-inbox.component.scss`
- **Severity:** High
- **Impact:** Visual Polish & Perceived Product Quality. Buttons (`Refresh`, `Deactivate`, `Approve / reactivate`, `Previous`, `Next`, `Mark reviewed`, `Submit rating`, `Submit incident`, `Cancel event`, `Mark as read`), inputs, selects, textareas, and fieldsets fall back to raw browser chrome (`#efefef` gray buttons, inset borders, groove fieldsets).
- **Evidence:** `04-admin-dashboard-desktop-1440.png`, `08-admin-accounts-desktop-1440.png`, `18-event-incidents-desktop-1440.png`, `23-admin-cancel-desktop-1440.png`.
- **Proposed Correction:** Add clean, reusable control, button, badge, card, and pagination rules in `design-system/styles/_foundation.scss` and refine component styles across all eight components.

### UI-005 — Primary Navigation Bar Lacks Active-Route State, Visual Polish, and Sign-Out Action
- **Location:** `frontend/src/app/layout/app-shell.component.html`, `frontend/src/app/layout/app-shell.component.scss`, `frontend/src/app/layout/app-shell.component.ts`
- **Severity:** Medium
- **Impact:** Navigation Clarity & Ergonomics. Navigation links display default browser underlines with no `routerLinkActive` highlight, the `.brand` link (`122 × 36 px`) is shorter than the `44 px` touch target guideline, and authenticated users lack a visible `Sign out` button in the header.
- **Evidence:** `04-admin-dashboard-desktop-1440.png` and `04-admin-dashboard-mobile-390.png`; Playwright touch-target scan recorded `a:Carpediem (122x36)` across all screens.
- **Proposed Correction:** Style `.site-header` and `nav a` with `routerLinkActive="active"`, remove raw link underlines in favor of structured pill/underline states, enforce `min-block-size: var(--cd-touch-target-min)` on `.brand`, and add a `Sign out` button alongside the user identity badge.

### UI-006 — Interactive Action Links and Checkbox Labels Below 44 × 44 px Touch Target
- **Location:**
  - `OperationalDashboardComponent` (`a:Cancel event` at `90 × 21 px`)
  - `TeamSelectionComponent` (`a:View history` at `86 × 24 px`)
  - `EventCancellationComponent` (`a:Keep event` at `78 × 24 px`)
  - `AccountAdminComponent` (`input[type="checkbox"]` at `13 × 13 px` without padded label hit area)
- **Severity:** Medium
- **Impact:** Mobile Ergonomics (`Constitution Principle XI: minimum 44×44px touch targets`).
- **Evidence:** Automated Playwright bounding-box audit in `audit-results.json`.
- **Proposed Correction:** Style action links as button-sized interactive chips/links (`min-block-size: var(--cd-touch-target-min); display: inline-flex; align-items: center;`) and give checkbox labels `min-block-size: var(--cd-touch-target-min)` with flex alignment and padding.

### UI-007 — Redundant `role="main"` Landmark Inside `<main id="main-content">`
- **Location:** `frontend/src/app/features/attendance/components/attendance-roll-sheet/attendance-roll-sheet.component.html` (line 1)
- **Severity:** Medium
- **Impact:** Screen Reader Landmark Semantics (`WCAG 1.3.1` / ARIA landmark best practices). `AppShellComponent` already wraps `<router-outlet />` in `<main id="main-content">`, so `<div class="cd-attendance-container" role="main">` creates a nested `main` landmark.
- **Evidence:** DOM inspection of `/events/:eventId/attendance`.
- **Proposed Correction:** Replace `<div class="cd-attendance-container" role="main" aria-labelledby="attendance-page-title">` with `<section class="cd-attendance-container" aria-labelledby="attendance-page-title">`.

### UI-008 — Sparse Landing Page (`/`) and Unframed Auth Forms (`/sign-in`, `/register`)
- **Location:**
  - `frontend/src/app/pages/home.component.ts`
  - `frontend/src/app/pages/sign-in.component.ts`
  - `frontend/src/app/features/accounts/register.component.ts`
- **Severity:** Medium
- **Impact:** First Impression & Onboarding Usability. After signing in (which redirects to `/`), users land on a sparse three-line page with no quick links to their role workspace. Similarly, `/sign-in` and `/register` render bare inputs directly on the page canvas without surface framing or cross-navigation links.
- **Evidence:** `01-home-public-desktop-1440.png`, `02-sign-in-desktop-1440.png`, `03-register-desktop-1440.png`.
- **Proposed Correction:** Enrich `HomeComponent` with Carpediem's "field journal for operations" hero, role-aware quick actions (while preserving existing headings/copy required by tests), and operational pillar cards; frame `SignInComponent` and `RegisterComponent` in clean surface panels with helpful cross-links.

### UI-009 — Operational Dashboard Event Cards Lack Direct Workflow Links for Active Lifecycle Steps
- **Location:** `frontend/src/app/features/operational-dashboard/operational-dashboard.component.html`
- **Severity:** Low / Medium
- **Impact:** Operational Task Flow. Dashboard cards display status and roster counts, but do not link directly to `Edit draft` (for `Draft` events), `Team selection` / `Replacements` (for `Published` events when Chief Leader/Admin), `My availability` / `My assignment` (when Leader), `Reschedule`, or `Complete event`.
- **Evidence:** `04-admin-dashboard-desktop-1440.png`.
- **Proposed Correction:** Add clean, role-gated action links in a structured card footer on `OperationalDashboardComponent` so users can navigate directly from the dashboard into every valid workflow for an event.

### UI-010 — Missing Whitespace After `Schedule:` Label in Event Summary Cards
- **Location:**
  - `frontend/src/app/features/team-selection/components/team-selection.component.html`
  - `frontend/src/app/features/volunteer-assignment/components/volunteer-assignment.component.html`
  - `frontend/src/app/features/replacement-assignment/components/replacement-assignment.component.html`
- **Severity:** Low
- **Impact:** Typography & Readability. `<strong>Schedule:</strong>` followed by `<time>` renders without a separating space (`Schedule:Nov 15, 2026...`).
- **Evidence:** `16-chief-team-selection-desktop-1440.png`, `17-leader-assignment-desktop-1440.png`, `20-chief-replacement-desktop-1440.png`.
- **Proposed Correction:** Add explicit spacing and flex/grid alignment for metadata labels and values in event summary cards.


---

## 4. Resolution & Post-Fix Browser Verification Summary

All 10 identified UI/UX and accessibility issues were remediated in Phase 2 and re-verified across all **93 screen-viewport combinations** (`360×800`, `390×844`, `768×1024`, `1440×900`):

| Issue ID | Severity | Status | Verification Evidence |
| :--- | :--- | :--- | :--- |
| `UI-001` | Critical | **Resolved** | `.form-actions` on `/draft-events/:id` and `/draft-events/new` flows naturally at the end of the form card (`WCAG 2.4.11` passed, zero field occlusion). |
| `UI-002` | High | **Resolved** | Self-hosted `Bricolage Grotesque` and `Hanken Grotesk` WOFF2 fonts copied to `frontend/public/fonts/` and registered in `frontend/src/styles.scss`. Computed styles confirm `Bricolage Grotesque` on headings and `Hanken Grotesk` on body copy across all 93 combinations. |
| `UI-003` | High | **Resolved** | Replaced all hardcoded `#2563eb` / `#111827` / `#e5e7eb` values in `ReplacementAssignmentComponent`, `AttendanceRollSheetComponent`, `RescheduleDialogComponent`, and `CompleteEventDialogComponent` with `--cd-*` semantic tokens. |
| `UI-004` | High | **Resolved** | Added `:where(...)` low-specificity base element rules in `frontend/src/styles.scss` and component styles for `OperationalDashboardComponent`, `AccountAdminComponent`, `FeedbackAdminComponent`, `IncidentAdminComponent`, `EventFeedbackComponent`, `EventIncidentsComponent`, `EventCancellationComponent`, and `NotificationInboxComponent`. |
| `UI-005` | Medium | **Resolved** | `.brand` link height increased to `44px` (`--cd-touch-target-min`), `routerLinkActive="active"` added to all primary navigation links, identity pill styled, and `Sign out` button added for authenticated sessions. |
| `UI-006` | Medium | **Resolved** | All action links (`OperationalDashboardComponent`, `TeamSelectionComponent`, `EventCancellationComponent`, `LeaderHistoryComponent`) and `.role-option` checkbox labels (`AccountAdminComponent`) now meet `min-block-size: var(--cd-touch-target-min)` (`44px`). |
| `UI-007` | Medium | **Resolved** | Replaced `<div class="cd-attendance-container" role="main">` with `<section class="cd-attendance-container" aria-labelledby="attendance-page-title">`. `axe-core` reports **0 violations** across all 93 screen-viewport combinations. |
| `UI-008` | Medium | **Resolved** | Framed `HomeComponent`, `SignInComponent`, and `RegisterComponent` in cohesive Carpediem surface cards with role-aware quick actions and authentication cross-links. |
| `UI-009` | Low / Medium | **Resolved** | Added direct workflow action links (`Edit draft`, `Team selection`, `Replacements`, `Reschedule`, `Complete event`) to `OperationalDashboardComponent` cards. |
| `UI-010` | Low | **Resolved** | Added explicit non-breaking spacing after `<strong>Schedule:</strong>` in `TeamSelectionComponent`, `VolunteerAssignmentComponent`, and `ReplacementAssignmentComponent`. |
