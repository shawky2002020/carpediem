# ADR-006: In-Platform Notification Strategy

* **Status**: ACCEPTED
* **Deciders**: Solution Architect, Engineering Team
* **Date**: 2026-09-17
* **Technical Domain**: Communications, User Experience, API Architecture, Notification Delivery

---

## Context and Problem Statement

Carpediem is an internal event-staffing platform serving a focused user base:
- 100–150 volunteer Leaders
- 10–15 Chief Leaders
- ~3 Operations Administrators
- 2–20 Events per month

Users predominantly interact with the application via mobile web browsers (iOS Safari, Android Chrome) in the field and at event venues. External notification channels—such as SMS, WhatsApp, or native mobile OS push notifications—are explicitly **outside the V1 scope**. The platform requires an exclusively **in-platform** notification system to alert users of operational events (appointments, team selections, volunteer cancellations, schedule adjustments).

We must decide the architectural mechanism for notification generation, persistence, and client delivery:
1. How is notification generation decoupled from domain state mutations?
2. What delivery transport best serves mobile browsers without incurring unwarranted operational complexity or infrastructure costs?
3. How do we ensure resilience across mobile tab hibernation, intermittent field connectivity, and page reloads?

---

## Decision Drivers

1. **Decoupled Architecture (Business Event $\neq$ Transport)**: Core domain workflows must record notification intent without being coupled to physical delivery transports, connection states, or network latency.
2. **Mobile Web Browser Robustness**: Must operate reliably across iOS Safari and Android Chrome, which aggressively suspend background tabs, terminate long-lived connections to save battery, and reload tabs upon device wake.
3. **Operational Simplicity & Cost**: For an organization of ~150 users generating a few dozen alerts monthly, infrastructure must minimize moving parts, eliminating external message brokers, cache clusters (e.g., Redis), or dedicated real-time socket infrastructure.
4. **Resilience & Reconnection**: Notifications must never be lost if a user is offline, navigating away, or in a sleeping tab when an alert is triggered.
5. **Clear State Semantics**: Unread counters and dismissal/read states must be authoritative and centrally synchronized.
6. **Strict Scope Control**: Differentiate ratified business notification requirements from candidate triggers; avoid inventing unapproved alerts or complex priority schemes.

---

## Notification Boundaries: Approved vs. Candidate Triggers

### A. Approved Business Notification Requirements (Ratified)
1. **Chief Leader Appointment**: Appointed candidate receives an in-platform alert prompting acceptance or decline ([SEQ-001](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-001-event-creation-chief-leader-publication.md)).
2. **Volunteer Team Selection**: Selected or directly drafted volunteer receives an in-platform alert confirming active placement ([SEQ-002](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-002-leader-availability-and-selection.md), [SEQ-003](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-003-direct-leader-assignment.md), [SEQ-005](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-005-dual-role-chief-leader-as-leader.md)).
3. **Volunteer Self-Cancellation**: Assigned Chief Leader and Operations Admins receive an in-platform alert when an active volunteer cancels ([SEQ-004](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-004-cancellation-and-replacement.md)).
4. **Material Same-Day Schedule Modification**: Volunteers and Chief Leaders receive notification of same-day time/location adjustments ([SEQ-007](file:///c:/dev/web/Carpediem/docs/architecture/sequences/SEQ-007-event-reschedule-staffing-cycle.md)).

### B. Candidate Notification Triggers (Pending Feature Specification)
1. **Event Publication Broadcast**: Notifying Leaders that an event is published and open for availability.
2. **Chief Leader Declination Alert**: Notifying Admins that an appointed candidate declined.
3. **True Date Reschedule Broadcast**: Notifying prior volunteers to declare availability for a new date.

---

## Considered Options

* **Option A: Persisted Relational Notifications + Navigation/Page-Load Fetching Only**
  Alerts are stored in SQL Server. The Angular client queries unread notifications and badge counts only upon full application boot or major route transitions.
* **Option B: Persisted Relational Notifications + Client-Initiated Refresh & Lifecycle Polling (Chosen)**
  Alerts are stored in SQL Server. The Angular client fetches notifications on route transitions, user navigation, window/tab focus (Page Visibility API), and via a configurable HTTP background polling timer while the tab is actively visible.
* **Option C: Real-Time Live Push via ASP.NET Core SignalR WebSocket Hub**
  Maintains persistent bi-directional WebSocket connections from each client to the ASP.NET Core host, streaming live toast notifications instantly upon event occurrence.
* **Option D: Third-Party Hosted Notification Gateway (Pusher, Firebase, OneSignal)**
  Offloads delivery to a cloud-managed notification SaaS platform.

---

## Decision Outcome

**Chosen Option**: **Option B: Persisted Relational Notifications + Client-Initiated Refresh & Lifecycle Polling**.

### Architecture & Technical Mechanics

#### 1. Separation of Notification Intent from Delivery
- **Transactional Staging**: When a domain command completes (e.g., `AppointChiefLeader`, `SelectCandidate`, `SelfCancelAssignment`), the domain workflow records notification intent in the `notifications` schema.
- **Relational Persistence**: Notifications reside in SQL Server (`notifications` schema). Each record captures:
  - `NotificationId` (Guid, Primary Key)
  - `RecipientUserId` (Guid, indexed for query performance)
  - `EventId` (Guid, contextual link)
  - `NotificationType` (Enum / String, e.g., `ChiefLeaderAppointment`, `RosterPlacement`, `StaffingCancellation`)
  - `Message` (Formatted localized summary text)
  - `CreatedAt` (DateTimeOffset UTC)
  - `IsRead` (Boolean) / `ReadAt` (Nullable DateTimeOffset UTC)
  - `IsDismissed` (Boolean)
- **Zero Loss Guarantee**: Because notifications are persisted relationally before delivery, they can never be missed due to closed browser tabs, network drops, or expired web sockets.

#### 2. Client-Side Retrieval Strategy (Angular 22)
The Angular SPA manages notifications via an injectable `NotificationService` utilizing Angular Signals:
- **Navigation & Lifecycle Queries**:
  - Fetches the current unread count and latest notifications whenever the user logs in, reloads the application, or completes a route navigation.
  - Utilizes the browser's native **Page Visibility API** (`visibilitychange` event): when a user wakes their mobile device or switches back to the Carpediem browser tab, the client immediately refreshes unread status and notifications.
- **Configurable Polling Loop**:
  - While the browser tab is **active and visible**, the client executes a lightweight HTTP `GET /api/notifications/unread-count` on a configurable interval (an initial operational tuning range of 60–120 seconds serves as the configurable default, not an immutable architecture invariant).
  - If the unread count increases, the client requests updated notification records.
  - When the browser tab is **hidden, minimized, or backgrounded**, the polling timer is **suspended**, conserving mobile battery, device memory, and server network resources.
- **Read & Dismissal Flow**:
  - Opening the notification drawer marks items as read (`POST /api/notifications/{id}/read` or batch `/mark-all-read`), updating persisted database state.

#### 3. SignalR Scale-Out Reality & Rejection Rationale for V1
A rigorous architectural analysis demonstrates why SignalR (Option C) is unwarranted for V1:
- **Affinity vs. Message Distribution**: In a multi-replica deployment (multiple ASP.NET Core ECS tasks behind an ALB):
  - A SignalR connection normally requires connection affinity (sticky sessions) to the specific server handling that connection, subject to documented WebSockets/SkipNegotiation exceptions.
  - **Crucially, sticky sessions alone do NOT propagate messages across servers**: if an Admin connected to Server 1 appoints a Chief Leader who is connected to Server 2, Server 1 cannot dispatch the notification to Server 2 without a multi-node message distribution mechanism.
  - Therefore, running SignalR across multiple application instances mandates introducing a **scale-out mechanism / backplane** (e.g., Redis Pub/Sub, Azure SignalR Service, or AWS messaging).
  - While Redis is one possible backplane, introducing any distributed backplane infrastructure solely to deliver a few dozen notifications per month is completely unjustified for a ~150-user modular monolith.
- **Mobile Browser Connection Fragility**: iOS Safari and Android Chrome aggressively tear down idle WebSockets within seconds of a user switching apps or locking the screen. When the device wakes, clients flood the server with reconnect handshakes, negotiation queries, and state resynchronizations.
- **Business Reality**: Event staffing is not live chat or high-frequency trading. An alert visible upon navigation, window focus, or within the configurable visible-tab polling window fully satisfies all operational requirements with zero backplane infrastructure.

---

## Pros and Cons of the Options

### Option B: Persisted Notifications + Lifecycle Polling (Chosen)
* **Good**: Extreme operational simplicity: uses existing SQL Server and standard REST endpoints without additional moving parts.
* **Good**: 100% resilient across mobile tab hibernation, page reloads, and network dropouts.
* **Good**: Completely avoids sticky sessions on the ALB and eliminates any need for a scale-out backplane cluster.
* **Good**: Minimal battery drain: polling automatically suspends when browser tabs are in the background.
* **Good**: Trivial horizontal scaling: any API container replica can service any notification read/poll request statelessly.
* **Bad**: Notifications do not appear with sub-second immediacy while a user sits idle on a single screen without interacting (bounded by the configurable visible-tab polling window).

### Option C: Live Push via ASP.NET Core SignalR
* **Good**: Sub-second live delivery of alerts and badge count increments.
* **Bad**: Highly fragile on mobile browsers; requires complex reconnection and missed-message reconciliation logic.
* **Bad**: Requires addressing both connection affinity and multi-node message distribution via a scale-out backplane across multiple ECS/Fargate containers.
* **Bad**: Significantly increases connection concurrency and operational footprint for minimal business gain.

### Option A: Page-Load Fetching Only
* **Good**: Simplest possible implementation.
* **Bad**: If a coordinator sits on the dashboard for an extended period, badge counters never update until manual page refresh.

### Option D: Third-Party SaaS Gateway (Pusher / Firebase)
* **Good**: Offloads connection management to external infrastructure.
* **Bad**: Introduces external commercial dependencies, SaaS costs, vendor lock-in, and compliance complexities for an internal tool.

---

## Security & Reliability Impact

1. **Authentication & Authorization**: All notification endpoints require authenticated sessions (ADR-002 cookies). The `NotificationService` strictly queries where `RecipientUserId == CurrentUserId`, preventing horizontal data leaks.
2. **Database Performance**: Indexed on `(RecipientUserId, IsRead, CreatedAt)`. Given the organization's scale (~150 users generating ~500 notifications/month), the entire active notification table will consume negligible memory and CPU.
3. **Rate Limiting**: Standard API rate-limiting middleware protects notification polling endpoints from runaway client loops.

---

## Revisit Triggers

Re-evaluate introducing ASP.NET Core SignalR (Option C) if:
1. Carpediem introduces live on-site emergency incident broadcasting or real-time collaborative dispatching workflows in post-V1 phases.
2. User experience research indicates that volunteer Chief Leaders on-site demand sub-second live roster status updates without polling latency.
3. If adopted, SignalR must be introduced with a properly evaluated scale-out mechanism without violating infrastructure constraints.
