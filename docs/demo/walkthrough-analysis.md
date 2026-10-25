# Product Walkthrough: Carpediem Volunteer & Event Staffing Platform

## Product Overview
**Carpediem** is an enterprise-grade volunteer management and event staffing orchestration platform built with Angular 22, ASP.NET Core 10, Entity Framework Core 10, and SQL Server 2025. It coordinates three core user roles—Operations Administrators, Chief Leaders, and Volunteer Leaders—through strict, auditable event lifecycles: from drafting and requirements planning, to Chief Leader delegation, team slotting, on-site real-time attendance roll call, and post-event incident governance.

---

## Flow Walkthroughs

### 1. Operations Administrator — Event Inception & Delegation
1. **Sign-In Portal (`/sign-in`)**
   - **What Appears**: High-contrast, accessibility-focused sign-in form with username/password fields and CSRF anti-forgery protection.
   - **Action Available**: Enter credentials (`admin`) and click "Sign in".
2. **Operational Dashboard (`/dashboard`)**
   - **What Appears**: High-level KPI summary cards displaying Draft Planning, Active Staffing, Pending Roll Call, and Completed Events. Quick-action buttons for creating draft events and managing pending volunteer accounts.
   - **Action Available**: Click "Draft Events" in the primary navigation or "New Draft Event".
3. **Draft Events Management (`/draft-events`)**
   - **What Appears**: Chronological table of draft events with timezone badges (`Africa/Cairo`), venue details, staffing fulfillment status, and Chief Leader appointment status.
   - **Action Available**: Select a draft event to open the Draft Editor.
4. **Draft Event Editor & Chief Appointment (`/draft-events/:id/chief-leader`)**
   - **What Appears**: Vetted pool of qualified Chief Leaders who are eligible for event leadership. Radio buttons display candidate name, contact details, and past verified event participation record.
   - **Action Available**: Select candidate radio and click "Appoint Chief Leader".
   - **What Changes**: Event lifecycle transitions to `ChiefAssigned`. Automated notification is dispatched to the appointed Chief Leader.

### 2. Chief Leader — Team Formation & Field Execution
1. **Chief Leadership Appointments (`/my-appointments`)**
   - **What Appears**: Formal leadership invitations showing event dates, location, staffing requirements, and Chief Leader responsibilities.
   - **Action Available**: Click "Accept Leadership" or "Decline".
2. **Team Selection & Role Slotting (`/events/:id/team-selection`)**
   - **What Appears**: Interactive slot assignment matrix matching required roles with available volunteer leaders who declared shift availability.
   - **Action Available**: Click "Assign" to slot leaders; click "View history" to inspect candidate attendance reliability (attended vs no-show counts). Click "Lock Team Roster".
3. **Live On-Site Attendance Roll Call (`/events/:id/attendance`)**
   - **What Appears**: High-visibility field check-in console with summary KPI counters (`Attended`, `No Show`, `Unrecorded`). Each volunteer card has large fluid touch targets ($\ge 44\text{px}$).
   - **Action Available**: Click "Attended" (turns emerald green) or "No Show" (turns amber/red). On-the-fly replacement assignment is available for emergency no-shows.
   - **What Changes**: Live attendance summary counters update in real time with accessible ARIA live-region announcements.

### 3. Volunteer Leader — Availability & Participation
1. **Availability Declaration (`/events/:id/availability`)**
   - **What Appears**: Active scheduling window where volunteers declare their availability for upcoming published events.
   - **Action Available**: Click "Mark Available" or "Mark Not Available" with optional personal schedule notes.
2. **My Assignment & Notification Center (`/events/:id/my-assignment`, `/notifications`)**
   - **What Appears**: Direct placement confirmation, role instructions, check-in reporting location, and audit log of in-app operational alerts.
   - **Action Available**: Acknowledge alerts, review shift hours, or trigger controlled assignment cancellation with mandatory justification.

---

## Key Findings

### First Impressions & Strengths
- **Deterministic State Machine**: Event status strictly guards allowed actions (Draft -> Published -> ChiefAssigned -> TeamFormed -> Active -> Completed), eliminating out-of-order operations.
- **Zero Ambiguity Attendance**: The attendance roll sheet gives immediate visual and auditory clarity on venue check-ins.
- **High-Contrast Design Tokens**: Clean dark green (`#12261C`), crisp white (`#F8F9F5`), and vivid emerald green (`#4ADE80`) provide full WCAG 2.2 AA contrast compliance.

### Friction Points Addressed
- **Demo Length & Static Hops**: Previous demo felt like a slow slideshow jumping between admin pages. Refactoring into a fast, cohesive, continuous 30-second live journey with high-visibility interaction feedback fixes this.
- **Click Visibility**: Added multi-frame radiant click beacons and expanding ripple shockwaves so every touch is unmistakably highlighted.

---

## Recommendations
1. **One-Click Quick Roll Call**: Provide a "Check In All Present" button for shifts with 100% turnout to expedite large festivals.
2. **Geofenced Check-In**: Enable automated volunteer arrival detection when mobile devices enter venue coordinates.
3. **Progressive Web App (PWA) Offline Roll Call**: Cache the attendance roll sheet locally in IndexedDB for venues with poor cellular reception.

---

## Pages Visited
- `/sign-in`
- `/dashboard`
- `/draft-events`
- `/draft-events/:id`
- `/draft-events/:id/chief-leader`
- `/events/:id/team-selection`
- `/events/:id/attendance`
- `/events/:id/availability`
- `/events/:id/my-assignment`
- `/profile`
- `/notifications`

## Rerun Inputs
```yaml
workflow: firecrawl-demo-walkthrough
url: http://127.0.0.1:4200
focus: full-product-lifecycle
```
