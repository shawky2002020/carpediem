# Carpediem — UI & Design System Masterpiece Autonomous Overhaul

You are acting as a coordinated elite engineering and design team:
- **Principal Product Designer & Design System Lead** — responsible for aesthetic identity, typographic hierarchy, spatial rhythm, and design token architecture.
- **Senior Frontend Architect** — responsible for Angular 22 component architecture, signals, CDK dialogs, and SCSS bundle performance.
- **Senior Full-Stack Engineer** — responsible for preserving backend API contracts, security invariants, and domain rules.
- **Senior QA Automation Engineer** — responsible for full regression verification across xUnit domain tests, SQL Server integration tests, and Playwright cross-browser/accessibility suites.
- **Release Engineer** — responsible for staging, verifying, committing, and pushing deliverables.

---

## Mission

Execute a complete, autonomous overhaul of Carpediem's UI and design system to transform it into a **masterpiece of civic orchestration and field dispatch**, executing all specifications defined in:
📄 **[`docs/quality/ui-enhancement-masterpiece-report.md`](docs/quality/ui-enhancement-masterpiece-report.md)**

**This is an end-to-end execution task, not a planning task.**
Work autonomously through all phases from token cleansing to component standardization, layout refactoring, comprehensive test suite verification, screenshot regeneration, and Git deployment. Do not stop after writing a plan.

---

## 0. Mandatory Architectural Context & Rules of Engagement

1. **Canonical Source of Truth**:
   - Masterpiece Specification: [`docs/quality/ui-enhancement-masterpiece-report.md`](docs/quality/ui-enhancement-masterpiece-report.md)
   - Quality Review: [`docs/quality/final-review.md`](docs/quality/final-review.md)
   - Notion Architecture Spine: `https://app.notion.com/p/3ddbf826725381938090c73592b2ad83`
2. **Locked Engineering Architecture**:
   - Angular 22 (Standalone Components, Signals, `linkedSignal`, `resource`, RxJS)
   - Angular CDK + Custom SCSS tokens (No Tailwind or Bootstrap runtime bloat)
   - ASP.NET Core 10 / .NET 10 LTS Modular Monolith
   - Entity Framework Core 10 + SQL Server 2025
   - Playwright + Axe-core for end-to-end and WCAG 2.2 AA verification
3. **Non-Negotiable Constraints**:
   - **Zero Business Logic Regressions**: Preserve 100% of existing domain invariants, API endpoints, and authentication policies.
   - **Preserve Binary Artifacts**: **NEVER modify, stage, or delete `logo.pdf`** in the repository root.
   - **Keep External Worktrees Untouched**: Do not modify or delete `C:/dev/web/Carpediem-design-system-foundation`.
   - **Budget Discipline**: Component SCSS files must remain within Angular bundle budgets (< 4.00 kB raw size).
   - **100% Passing Tests**: All 96 Domain tests, 183 SQL integration tests, 100 Angular tests, and 410 Playwright tests must pass before completion.

---

## Phase 1 — Token & Primitives Cleansing (P0)

1. **Purge Remaining Hardcoded Hex Colors**:
   - Cleanse `frontend/src/app/features/availability/components/availability-declaration/availability-declaration.component.scss` (replace `#2563eb` and `#eff6ff` with `--cd-status-info-fg` and `--cd-status-info-bg`).
   - Cleanse `frontend/src/app/features/volunteer-assignment/components/volunteer-assignment.component.scss` (replace `#065f46`, `#f8fafc`, and `#fff` with official semantic tokens).
2. **Standardize Master Design Tokens**:
   - In `design-system/styles/_semantic-tokens.scss`, formalize the Masterpiece Token System:
     - Surface states: `--cd-surface-hover`, `--cd-surface-pressed`, `--cd-surface-active`.
     - Signal palette: High-contrast `--cd-signal-success`, `--cd-signal-warning`, `--cd-signal-danger`, `--cd-signal-info` and their subtle background pairs.
     - Elevation shadows: `--cd-shadow-sm`, `--cd-shadow-md`, `--cd-shadow-lg`, `--cd-shadow-modal`.
     - Typography discipline: `tabular-nums` support and prose reading limits (`max-inline-size: 65ch`).

---

## Phase 2 — Design System Component Layer (P1)

Create reusable, standardized SCSS classes in `frontend/src/styles.scss` (or a dedicated `design-system/styles/_components.scss` import) to eliminate fragmented class conventions:

1. **Buttons (`.cd-btn`)**:
   - Variants: `.cd-btn--primary`, `.cd-btn--secondary`, `.cd-btn--subtle`, `.cd-btn--danger`.
   - Enforce minimum $44\times44\text{px}$ touch targets, smooth tactile hover/active states, and visible focus rings with `outline-offset: 3px`.
2. **Status Badges & Pills (`.cd-badge`)**:
   - Variants: `.cd-badge--info`, `.cd-badge--success`, `.cd-badge--warning`, `.cd-badge--danger`, `.cd-badge--neutral`.
   - Pill radius, tabular number support, and high-contrast text ratios ($\ge 4.5:1$).
3. **Alert Callouts (`.cd-callout`)**:
   - High-contrast leading border (`4px solid`), accessible semantic icons, clear heading and prose hierarchy.
4. **Data Tables & Roster Lists (`.cd-table`, `.cd-roster`)**:
   - Clean column alignment, subtle dividers, hover row highlights, and responsive reflow.

---

## Phase 3 — App Shell & Unified Operational Spine (P1)

1. **Modernize App Shell Header (`app-shell.component.*`)**:
   - Replace the plain brand text with an elegant brand mark and typographic treatment.
   - Display structured user role badge (`Admin`, `Chief Leader`, `Volunteer Leader`).
   - On mobile viewports ($\le 640\text{px}$), provide an ergonomic header layout with smooth bottom navigation.
2. **Build the Reusable Operational Spine Component**:
   - Create a shared `OperationalSpineComponent` or layout header rendered across all event routes:
     - Event Title & Venue.
     - Five-stage visual lifecycle breadcrumb: `Draft ➔ Published ➔ Staffed ➔ In Progress ➔ Concluded`.
     - Cairo date/time badge with relative countdown (*"Starts in 3 hours"* or *"Live Now"*).
     - Roster fulfillment metric ($14/15$ slots filled) and assigned Chief Leader identity.

---

## Phase 4 — High-Impact Operations Workbenches (P1)

1. **Operational Dashboard (`operational-dashboard.component.*`)**:
   - Top executive KPI status ribbon summarizing Draft, Active Staffing, Pending Attendance, and Concluded events.
   - Interactive filter chips (`All`, `Requires Action`, `Published`, `Drafts`, `Concluded`).
   - Card leading-edge color bars (Amber for attention, pulsing dot for Live In-Progress).
   - Card action prioritization: Single prominent **Next Recommended Action** button; secondary utilities organized cleanly.
2. **Team Selection Workbench (`team-selection.component.*`)**:
   - Desktop split workbench layout:
     - Left: Available Volunteer Pool with name search, skill badges, and past hours.
     - Right: Active Team Roster slotted into defined roles with a live fulfillment meter.
   - Unambiguous visual indicators for direct draft pool candidates (*"Non-Respondent"*, *"Declined"*).
3. **On-Site Attendance Roll Call (`attendance-roll-sheet.component.*`)**:
   - **Tactile Outdoor Mode**: Edge-to-edge touch targets ($\ge 56\text{px}$ on mobile) with high-contrast emerald and crimson fills.
   - Sticky summary counter: `Attended · No Show · Unrecorded`.
   - One-tap inline substitute drawer when a volunteer is marked `No Show`.

---

## Phase 5 — Public Experience & Volunteer Portals (P2)

1. **Landing Page (`pages/home.component.ts`)**:
   - Replace generic SaaS card stack with an editorial civic manifesto.
   - Render a live platform pulse ticker (*"3 Events Live · 48 Volunteers On Duty · 98.4% Fulfillment"*).
   - Interactive 3-step event progression walkthrough.
2. **Volunteer Profile & Service History (`leader-history.component.*`)**:
   - Total hours contributed counter with tabular typography.
   - Attendance reliability badge (e.g., `100% Reliability · 12 Shifts Completed`).
   - Field qualification skill tags.
3. **Notification Center (`notification-inbox.component.*`)**:
   - Group notifications chronologically: `Today`, `This Week`, `Older`.
   - Provide direct 1-click contextual actions on notification cards (*"View Shift Details"*, *"Update Availability"*).

---

## Phase 6 — Full Verification & Quality Assurance Gate

Execute the complete verification battery and verify 100% passing results before proceeding:

1. **Domain Tests**:
   ```bash
   dotnet test tests/Carpediem.Domain.Tests/Carpediem.Domain.Tests.csproj
   ```
   *Expected: 96 / 96 passed*
2. **SQL Server Integration Tests**:
   ```bash
   powershell -File tests/run-sql-integration.ps1
   ```
   *Expected: 183 / 183 passed*
3. **Angular Unit Tests**:
   ```bash
   cd frontend && npx ng test --watch=false
   ```
   *Expected: 100 / 100 passed*
4. **Angular Production Build & SCSS Budget Check**:
   ```bash
   cd frontend && npx ng build
   ```
   *Expected: 0 errors, 0 budget warnings*
5. **Playwright Cross-Browser & Accessibility Tests**:
   ```bash
   cd tests/e2e && npx playwright test
   ```
   *Expected: 410 / 410 passed, 0 Axe-core WCAG 2.2 AA violations, 0 horizontal overflows across 320px–1440px viewports*

---

## Phase 7 — Screenshot Regeneration, Documentation & Deployment

1. **Regenerate Genuine Screenshots & Demo Video**:
   - Run `node frontend/scripts/capture-screenshots.mjs` to refresh all 28 screenshots with the elevated UI.
   - Run `node frontend/scripts/produce-demo-video.mjs` to re-encode the Full HD demonstration video with the updated visuals.
2. **Update Documentation**:
   - Update `README.md`, `docs/screenshots/README.md`, and `docs/quality/final-review.md` with final verification data and refreshed visual links.
3. **Git Stage, Commit & Push**:
   - Strictly verify `git status` to ensure `logo.pdf` is **NOT staged**.
   - Commit with message:
     ```text
     feat(ui): elevate interface to civic dispatch masterpiece
     ```
   - Push to `origin/main`.
4. **Provide Completion Report**:
   - Deliver a clear summary with clickable file links to all updated components, screenshots, and test outcomes.
