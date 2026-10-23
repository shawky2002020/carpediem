# Carpediem — Application Screenshots

This directory contains genuine, unretouched screenshots captured directly from the running Carpediem application (`Angular 22` + `ASP.NET Core 10` + `SQL Server 2025`) using Playwright.

---

## Directory Structure

```text
docs/screenshots/
├── desktop/      # Desktop viewport captures (1440 × 900)
├── mobile/       # Mobile-first field viewport captures (390 × 844)
├── workflows/    # Modal and multi-step operational workflow captures (1440 × 900)
└── README.md     # Screenshot inventory and regeneration guide
```

---

## Screenshot Inventory

### Desktop (`1440 × 900`)

| File | Role | Route | Description |
| :--- | :--- | :--- | :--- |
| [`desktop/01-home-public.png`](./desktop/01-home-public.png) | Public | `/` | Public landing page introducing the Carpediem operations workspace and role capabilities. |
| [`desktop/02-sign-in.png`](./desktop/02-sign-in.png) | Public | `/sign-in` | Cookie-authenticated sign-in form with registration cross-link. |
| [`desktop/03-register-leader.png`](./desktop/03-register-leader.png) | Public | `/register` | Volunteer Leader self-registration form with password policy guidance. |
| [`desktop/04-admin-operational-dashboard.png`](./desktop/04-admin-operational-dashboard.png) | `Admin` | `/dashboard` | Operational Dashboard displaying live event status badges, roster counters, and workflow actions. |
| [`desktop/05-admin-draft-events-list.png`](./desktop/05-admin-draft-events-list.png) | `Admin` | `/draft-events` | Draft Events management list with creation CTA. |
| [`desktop/06-admin-draft-event-edit-and-chief-leader.png`](./desktop/06-admin-draft-event-edit-and-chief-leader.png) | `Admin` | `/draft-events/:id` | Draft Event editor and Chief Leader appointment/publish panel (`Africa/Cairo` timezone). |
| [`desktop/07-admin-accounts-governance.png`](./desktop/07-admin-accounts-governance.png) | `Admin` | `/admin/accounts` | Account approval, deactivation, and role assignment matrix (`Leader`, `ChiefLeader`, `Admin`). |
| [`desktop/08-admin-feedback-review.png`](./desktop/08-admin-feedback-review.png) | `Admin` | `/admin/feedback` | Admin-only post-event rating review dashboard. |
| [`desktop/09-admin-incident-review.png`](./desktop/09-admin-incident-review.png) | `Admin` | `/admin/incidents` | Factual incident review queue with Admin review notes. |
| [`desktop/10-chief-leader-appointments.png`](./desktop/10-chief-leader-appointments.png) | `ChiefLeader` | `/my-appointments` | Chief Leader appointment response workspace. |
| [`desktop/11-chief-leader-team-selection.png`](./desktop/11-chief-leader-team-selection.png) | `ChiefLeader` | `/events/:id/team-selection` | Team Selection workspace with Active Team Roster, Available Volunteers pool, and Direct Draft Pool. |
| [`desktop/12-chief-leader-replacement-assignment.png`](./desktop/12-chief-leader-replacement-assignment.png) | `ChiefLeader` | `/events/:id/replacement-assignment` | Replacement Leader Assignment workspace linking cancelled slots to eligible replacement volunteers. |
| [`desktop/13-chief-leader-attendance-roll-sheet.png`](./desktop/13-chief-leader-attendance-roll-sheet.png) | `ChiefLeader` | `/events/:id/attendance` | On-Site Attendance Roll Call sheet with live summary counters (`Total Roster`, `Attended`, `No Show`, `Unrecorded`). |
| [`desktop/14-leader-availability-declaration.png`](./desktop/14-leader-availability-declaration.png) | `Leader` | `/events/:id/availability` | Volunteer Leader availability declaration (`Available` / `Not Available`). |
| [`desktop/15-leader-my-assignment.png`](./desktop/15-leader-my-assignment.png) | `Leader` | `/events/:id/my-assignment` | Volunteer Leader active placement status and controlled self-cancellation workspace. |
| [`desktop/16-leader-profile-history.png`](./desktop/16-leader-profile-history.png) | `Leader` | `/profile` | Factual participation history (`Events attended`, `No-shows`, `Self-cancellations`, and per-cycle history). |
| [`desktop/17-leader-notifications-inbox.png`](./desktop/17-leader-notifications-inbox.png) | `Leader` | `/notifications` | In-app Notification Inbox with unread count badge and read-state actions. |

### Mobile (`390 × 844`)

| File | Role | Route | Description |
| :--- | :--- | :--- | :--- |
| [`mobile/01-mobile-home.png`](./mobile/01-mobile-home.png) | Public | `/` | Mobile-first landing page (`390 × 844`). |
| [`mobile/02-mobile-admin-dashboard.png`](./mobile/02-mobile-admin-dashboard.png) | `Admin` | `/dashboard` | Mobile Operational Dashboard with stacked event cards and `44×44px` action buttons. |
| [`mobile/03-mobile-team-selection.png`](./mobile/03-mobile-team-selection.png) | `ChiefLeader` | `/events/:id/team-selection` | Mobile Team Selection workspace with full-width selection controls. |
| [`mobile/04-mobile-attendance-roll-sheet.png`](./mobile/04-mobile-attendance-roll-sheet.png) | `ChiefLeader` | `/events/:id/attendance` | Mobile On-Site Attendance Roll Call sheet designed for field use. |
| [`mobile/05-mobile-leader-availability.png`](./mobile/05-mobile-leader-availability.png) | `Leader` | `/events/:id/availability` | Mobile Volunteer Leader Availability Declaration. |
| [`mobile/06-mobile-leader-profile.png`](./mobile/06-mobile-leader-profile.png) | `Leader` | `/profile` | Mobile Volunteer Leader Profile & Factual History. |

### Workflows (`1440 × 900`)

| File | Role | Route | Description |
| :--- | :--- | :--- | :--- |
| [`workflows/01-workflow-event-reschedule-dialog.png`](./workflows/01-workflow-event-reschedule-dialog.png) | `Admin` | `/events/:id/reschedule` | Event Reschedule modal workflow that resets the active staffing cycle and notifies stakeholders. |
| [`workflows/02-workflow-event-completion-dialog.png`](./workflows/02-workflow-event-completion-dialog.png) | `Admin` | `/events/:id/complete` | Complete Event confirmation workflow. |
| [`workflows/03-workflow-event-cancellation.png`](./workflows/03-workflow-event-cancellation.png) | `Admin` | `/events/:id/cancel` | Event Cancellation workflow with mandatory reason input (`10–500` characters). |
| [`workflows/04-workflow-post-event-feedback.png`](./workflows/04-workflow-post-event-feedback.png) | `ChiefLeader` / `Leader` | `/events/:id/feedback` | Post-event rating submission workflow (`1–5` rating and optional comment). |
| [`workflows/05-workflow-event-incident-reporting.png`](./workflows/05-workflow-event-incident-reporting.png) | `ChiefLeader` / `Leader` | `/events/:id/incidents` | Factual event incident reporting workflow. |

---

## Demo Data & Privacy

All screenshots use deterministic, synthetic development accounts seeded by `DevelopmentAdminSeeder` (`admin`, `chief.one`, `chief.two`, `leader.one`..`leader.five` under `@carpediem.local`). No real personal data, credentials, or production secrets appear in any screenshot.

---

## How to Regenerate Screenshots

With the SQL Server container, ASP.NET Core backend (`https://localhost:7076`), and Angular dev server (`http://127.0.0.1:4200`) running:

```bash
node frontend/scripts/capture-screenshots.mjs
```
