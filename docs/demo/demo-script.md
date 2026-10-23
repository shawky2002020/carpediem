# Carpediem Product Demo — Narration & Caption Script

This script documents the timed lower-third captions, scene transitions, and narrative flow for [`docs/demo/carpediem-product-demo.mp4`](file:///c:/dev/web/Carpediem/docs/demo/carpediem-product-demo.mp4). The video is designed to be 100% self-explanatory without audio (for silent autoplay in GitHub PRs, documentation previews, and stakeholder reviews) while also serving as a timed voiceover script when presented live.

---

## Scene 1 — Introduction (`00:00 – 00:10`)

- **Visual**: Carpediem title card in deep forest (`#1D5041`) and warm stone (`#F8F9F5`), transitioning to the live Home workspace (`/`).
- **Caption**: `Carpediem — Event Staffing & Volunteer Operations Platform`
- **Voiceover / Narrative**:
  > "Carpediem is a mobile-first web application built to replace fragmented spreadsheets and chat threads with a structured, auditable workflow for event staffing and volunteer leadership."

---

## Scene 2 — Product Overview & Authentication (`00:10 – 00:24`)

- **Visual**: Scroll across the three role capability cards on `/`, then navigate to `/sign-in` and authenticate as `admin`.
- **Caption**: `Role-Based Operations — Administrators, Chief Leaders, and Volunteer Leaders`
- **Voiceover / Narrative**:
  > "Carpediem coordinates three distinct operational roles: Operations Administrators who author events and govern accounts, Chief Leaders who build and lead field teams, and Volunteer Leaders who declare availability and staff events."

---

## Scene 3 — Administrator Experience (`00:24 – 01:00`)

- **Visual**:
  - `00:24 – 00:34`: Operational Dashboard (`/dashboard`) showing live roster counts, `In Progress` and `Pending Attendance` alerts, and direct workflow actions.
  - `00:34 – 00:44`: Draft Events list (`/draft-events`) and Draft Event editor (`/draft-events/:id`) with Chief Leader appointment and publish readiness checks.
  - `00:44 – 00:52`: Event Reschedule dialog (`/events/:id/reschedule`) and Complete Event dialog (`/events/:id/complete`).
  - `00:52 – 01:00`: Account Governance (`/admin/accounts`), Feedback Review (`/admin/feedback`), and Incident Review (`/admin/incidents`).
- **Caption**: `Operations Administrator — Event Lifecycle, Chief Leader Appointment & Governance`
- **Voiceover / Narrative**:
  > "From the Operational Dashboard, Administrators monitor active roster counts and pending attendance across every event. They author draft events in Africa/Cairo time, appoint a Chief Leader before publishing, manage schedule changes that automatically reset staffing cycles, and oversee account approvals, post-event ratings, and incident reviews."

---

## Scene 4 — Chief Leader Experience (`01:00 – 01:36`)

- **Visual**:
  - `01:00 – 01:08`: Chief Leader (`chief.one`) views `/my-appointments` to accept or decline event leadership appointments.
  - `01:08 – 01:18`: Team Selection (`/events/:id/team-selection`) showing the Active Team Roster, Available Volunteers pool, Direct Draft Pool, and a volunteer's factual participation history (`/events/:id/leaders/:leaderId/history`).
  - `01:18 – 01:26`: Replacement Leader Assignment (`/events/:id/replacement-assignment`) linking a cancelled volunteer slot to an eligible replacement candidate.
  - `01:26 – 01:36`: On-Site Attendance Roll Call (`/events/:id/attendance`) marking `Attended` and `No Show` outcomes, followed by Post-Event Leader Ratings (`/events/:id/feedback`).
- **Caption**: `Chief Leader — Team Selection, Replacement Staffing & On-Site Roll Call`
- **Voiceover / Narrative**:
  > "Once appointed, the Chief Leader assembles the event team from volunteers who declared availability—or drafts directly from the wider Leader pool when needed. If a team member cancels before the event starts, the Chief Leader links a replacement to the vacant slot. On event day, the Chief Leader records attendance on the roll sheet and submits post-event ratings."

---

## Scene 5 — Volunteer Leader Experience (`01:36 – 02:02`)

- **Visual**:
  - `01:36 – 01:44`: Volunteer Leader (`leader.one`) opens `/events/:id/availability` and toggles availability (`Available` / `Not Available`).
  - `01:44 – 01:52`: Opens `/events/:id/my-assignment` to inspect active placement details and the controlled self-cancellation dialog requiring a justification reason.
  - `01:52 – 02:02`: Views factual participation metrics on `/profile` (`Events attended`, `No-shows`, `Self-cancellations`) and checks in-app operational alerts on `/notifications`.
- **Caption**: `Volunteer Leader — Availability Declaration, Active Placement & Factual History`
- **Voiceover / Narrative**:
  > "Volunteer Leaders can declare or update their availability while the event window is open, inspect their active team placement, cancel with a required justification if an emergency arises, and review their factual attendance history and notifications."

---

## Scene 6 — Mobile-First Field Experience (`02:02 – 02:16`)

- **Visual**: Side-by-side and sequential mobile viewport (`390 × 844`) captures showing the Operational Dashboard, On-Site Attendance Roll Call sheet, Availability Declaration, and Leader Profile.
- **Caption**: `Mobile-First Responsive Design — 44×44px Touch Targets & Zero Horizontal Overflow`
- **Voiceover / Narrative**:
  > "Because Chief Leaders and Volunteer Leaders operate on-site from mobile devices, every screen is engineered mobile-first with 44-pixel minimum touch targets, single-column stacked cards, and WCAG 2.2 AA contrast."

---

## Scene 7 — Closing Summary (`02:16 – 02:25`)

- **Visual**: Closing summary card highlighting the architecture and verification suite (`Angular 22`, `ASP.NET Core 10`, `Entity Framework Core 10`, `SQL Server 2025`, `Playwright`).
- **Caption**: `Carpediem — Verified Modular Monolith Architecture & Complete V1 Operational Suite`
- **Voiceover / Narrative**:
  > "Carpediem delivers a complete, verified event staffing platform powered by Angular 22, ASP.NET Core 10, Entity Framework Core 10, and SQL Server 2025."
