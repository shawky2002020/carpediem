# Carpediem Product Demo — Storyboard

## Production Specifications

- **Output File**: `docs/demo/carpediem-product-demo.mp4`
- **Thumbnail File**: `docs/demo/demo-thumbnail.png`
- **Resolution**: `1920 × 1080` (Full HD 1080p, 16:9)
- **Frame Rate**: `30 fps` (constant frame rate)
- **Codec**: `H.264 (libx264)`, `yuv420p` pixel format, `+faststart` web streaming flag
- **Target Duration**: ~2 minutes 25 seconds (`145s`)
- **Visual Style**: Authentic live browser captures from the running Angular 22 + ASP.NET Core 10 + SQL Server 2025 application, framed with Carpediem editorial forest-and-stone title cards, role badges, and concise lower-third captions.

---

## Scene-by-Scene Breakdown

| Scene | Timecode | Duration | Role / Viewport | Screen / Route | Visual Action & Narrative Focus | On-Screen Caption |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **1. Intro** | `00:00 – 00:10` | `10s` | Brand Title Card + Live Home (`1920×1080`) | `/` | Editorial title card introducing **Carpediem — Event Staffing & Volunteer Operations Platform**, transitioning smoothly to the live public workspace. | *Carpediem — Structured Event Staffing & Volunteer Operations* |
| **2. Product Overview** | `00:10 – 00:24` | `14s` | Public / Overview (`1920×1080`) | `/` & `/sign-in` | Highlights the three coordinated operational roles (Operations Administrators, Chief Leaders, Volunteer Leaders) and signs in as Operations Administrator (`admin`). | *Coordinating Admins, Chief Leaders, and Volunteer Leaders across every staffing cycle* |
| **3. Admin Experience** | `00:24 – 01:00` | `36s` | `Admin` (`1920×1080`) | `/dashboard`, `/draft-events`, `/draft-events/:id`, `/admin/accounts`, `/admin/feedback`, `/admin/incidents` | 1. Inspects the Operational Dashboard (`Cairo Community Gala`, `Alexandria Coastal Cleanup`, `Giza Heritage Night`).<br>2. Opens Draft Event editor & Chief Leader appointment panel.<br>3. Reviews Event Reschedule & Event Completion dialogs.<br>4. Visits Account Governance (`Approve / reactivate` & role matrix), Post-Event Feedback Review, and Incident Review. | *Admin Role — Event authoring, Chief Leader appointment, account approval & governance* |
| **4. Chief Leader Experience** | `01:00 – 01:36` | `36s` | `ChiefLeader` (`1920×1080`) | `/my-appointments`, `/events/:id/team-selection`, `/events/:id/replacement-assignment`, `/events/:id/attendance`, `/events/:id/feedback` | 1. Signs in as `chief.one` and reviews Chief Leader appointments.<br>2. Opens Team Selection to inspect the Active Team Roster, Available Volunteers pool, and Direct Draft Pool, plus a Leader's participation history.<br>3. Opens Replacement Leader Assignment to link a cancelled slot to an eligible replacement.<br>4. Opens the On-Site Attendance Roll Call sheet, marks outcomes, and reviews Post-Event Leader Rating. | *Chief Leader Role — Appointment response, team selection, replacements & roll-call attendance* |
| **5. Leader Experience** | `01:36 – 02:02` | `26s` | `Leader` (`1920×1080`) | `/events/:id/availability`, `/events/:id/my-assignment`, `/profile`, `/notifications` | 1. Signs in as `leader.one` and declares availability (`Available` / `Not Available`) on a published event.<br>2. Reviews active placement status on `My Event Assignment` and inspects the controlled self-cancellation modal.<br>3. Opens `My profile` to view factual participation counts (`Events attended`, `No-shows`, `Self-cancellations`) and checks `Notifications`. | *Volunteer Leader Role — Availability declaration, assignment tracking, history & notifications* |
| **6. Mobile Experience** | `02:02 – 02:16` | `14s` | Mobile (`390×844` framed on `1920×1080` stage) | `/dashboard`, `/events/:id/attendance`, `/events/:id/availability`, `/profile` | Demonstrates mobile-first responsiveness at `390 × 844` across the Operational Dashboard, On-Site Attendance Roll Call sheet, Leader Availability Declaration, and Leader Profile with 44×44px touch targets and zero horizontal overflow. | *Mobile-First Responsive UX — 44px touch targets & single-column field layouts (`390 × 844`)* |
| **7. Closing Summary** | `02:16 – 02:25` | `9s` | Closing Card (`1920×1080`) | Summary Card | Summarizes the verified technology stack (`Angular 22`, `ASP.NET Core 10`, `EF Core 10`, `SQL Server 2025`, `Playwright E2E`) and complete V1 operational lifecycle. | *Carpediem — Built with Angular 22, ASP.NET Core 10, EF Core 10 & SQL Server 2025* |
