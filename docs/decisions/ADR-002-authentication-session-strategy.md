# ADR-002: Authentication & Session Strategy

* **Status**: ACCEPTED
* **Deciders**: Solution Architect, Engineering Team
* **Date**: 2026-09-17
* **Technical Domain**: Security, Session Management, Frontend-Backend Integration

---

## Context and Problem Statement

Carpediem is a responsive, mobile-first internal event-staffing web application built with an Angular 22 Single-Page Application (SPA) frontend and an ASP.NET Core 10 Modular Monolith backend. The system serves approximately 100–150 volunteer Leaders, 10–15 Chief Leaders, and ~3 Operations Admins. Users predominantly interact with the platform via mobile web browsers (Safari on iOS, Chrome on Android) in the field and on-site at event venues.

The system requires an authentication and session management strategy that provides:
1. Strong resistance against token exfiltration via JavaScript injection compared with storing bearer tokens in browser storage (`localStorage` / `sessionStorage`).
2. Robust mobile browser behavior (avoiding third-party cookie restrictions, cross-origin quirks, and frequent unnecessary re-authentications on mobile devices).
3. Reliable administrative revocation (account deactivation, role demotions, credential resets).
4. Extreme operational simplicity for a ~150-user organization without distributed token caching infrastructure.

---

## Decision Drivers

1. **Mobile Browser Robustness**: Must operate reliably across iOS Safari and Android Chrome without being broken by cross-site cookie restrictions, third-party cookie partitioning, or unnecessary CORS preflight round-trips.
2. **Mitigating Token Exfiltration**: Session tickets should not be stored in client-accessible JavaScript storage (`localStorage` / `sessionStorage`), significantly raising the barrier against automated credential harvesting if an XSS flaw exists.
3. **Comprehensive Defense-in-Depth**: Acknowledge that while `HttpOnly` cookies prevent JavaScript from directly reading the session identifier, an XSS vulnerability could still allow an attacker to issue ambient authenticated HTTP requests in the user's browser context. Therefore, standard web application security defenses (Content Security Policy, framework output encoding, Angular DOM sanitization, strict input handling) remain indispensable.
4. **Administrative Revocation**: When an Admin deactivates an account or an assigned user role changes, active sessions must be invalidated promptly without waiting for extended token expirations.
5. **Clean Logout & Session Lifecycle**: Terminating a session must be definitive on both client and server without requiring distributed token blocklists.
6. **Operational Simplicity**: V1 scale (~150 users) requires minimizing moving parts, eliminating external identity brokers, Redis caches, or complex token-refresh interceptor loops.
7. **Unified Origin Alignment**: Production infrastructure (AWS CloudFront / ALB) allows exposing the SPA and API under a single domain origin.
8. **Decoupled Account Onboarding**: Account onboarding and invitation workflows remain unresolved (Section 13 Decision 9 / ADR-011); this ADR governs authenticated session mechanics once credentials exist.

---

## Considered Options

* **Option 1: ASP.NET Core Identity with Secure HttpOnly SameSite Cookies and Unified-Origin Routing** *(Recommended & Chosen)*
* **Option 2: Stateless JWT Bearer Access Tokens + Refresh Tokens stored in Browser Storage**
* **Option 3: Backend-for-Frontend (BFF) Token Exchange Gateway**

---

## Decision Outcome

**Chosen Option**: **Option 1: ASP.NET Core Identity with Secure HttpOnly SameSite Cookies and Unified-Origin Routing**.

### Architecture & Mechanics

#### 1. Topology: Unified Origin Routing (Production & Development)
- **Production (AWS CloudFront + ALB)**:
  - The application is served under a single canonical origin (e.g., `https://app.carpediem.org`).
  - CloudFront CDN path-based routing:
    - `/api/*` $\rightarrow$ Forwarded directly to the ASP.NET Core 10 container via Application Load Balancer (ALB).
    - `/*` (default) $\rightarrow$ Serves pre-compiled static Angular 22 SPA assets from Amazon S3.
  - **Preserves First-Party Cookie Semantics**: Because the SPA and API share the exact same scheme, host, and port, requests are **same-origin**. This avoids cross-site third-party-cookie restrictions (such as WebKit/Safari Intelligent Tracking Prevention third-party blocking) and simplifies cookie handling across mobile browsers.
  - **Zero CORS Preflight Overhead**: Cross-Origin Resource Sharing (CORS) preflight (`OPTIONS`) requests are completely eliminated for normal application API calls, reducing mobile network latency and battery consumption.
- **Local Development Environment**:
  - Angular CLI development server (`http://localhost:4200`) uses `proxy.conf.json` to transparently proxy `/api` requests to the ASP.NET Core backend (`http://localhost:5000` or `https://localhost:5001`).
  - Alternatively, Docker Compose runs a lightweight local reverse proxy (YARP or Nginx), mirroring production same-origin path routing.
  - Both approaches maintain same-origin semantics during local development, avoiding cross-origin cookie quirks or local SSL certificate conflicts across different ports.

#### 2. Cookie Security Attributes
The primary authentication session cookie is issued by ASP.NET Core Identity with strict security attributes:
- **`HttpOnly = true`**: Prevents client-side JavaScript (`document.cookie`) from directly reading the session ticket, materially reducing the risk of token exfiltration via XSS compared with browser-storage bearer tokens.
- **`Secure = true`**: Transmitted exclusively over TLS/HTTPS encrypted connections.
- **`SameSite = SameSiteMode.Lax`**: Provides defense against Cross-Site Request Forgery (CSRF) for cross-origin subrequests while permitting standard top-level navigation (e.g., clicking an event link in an email or communication).
- **`IsEssential = true`**: Marked as strictly necessary for core application functionality.

#### 3. Antiforgery & CSRF Protection
State-modifying operations (`POST`, `PUT`, `DELETE`, `PATCH`) are protected using ASP.NET Core's antiforgery system:
- **Framework Antiforgery Pair**: ASP.NET Core antiforgery protection utilizes the framework's antiforgery token pair mechanism. Protected cookies carry the server-validated security context, while the client application sends the corresponding antiforgery request token in the configured HTTP header (e.g., standard Angular XSRF request header).
- **Client Integration**: Angular's HTTP client is configured to send the request token on mutating calls. ASP.NET Core middleware validates the request token against the cookie context on all non-safe HTTP methods.
- **Cross-Site Defense**: An external origin cannot read or forge the custom request header under same-origin policy constraints, preventing unauthorized cross-site command submission.
- *Note*: Exact header and cookie naming conventions (e.g., `X-XSRF-TOKEN` / `XSRF-TOKEN`) are implementation details to be finalized during code implementation.

#### 4. Session Expiration & Mobile Lifecycle
- **Sliding Expiration**: Configured for 14 days of inactivity. Authenticated requests within the active window refresh the cookie expiration, accommodating volunteer Leaders who access the app periodically for event shifts.
- **Absolute Expiration Cap**: Maximum session lifetime is capped at 30 days, after which re-authentication is required.
- **Mobile Browser Behavior**: When mobile browsers (Safari on iOS or Chrome on Android) purge background tab memory to conserve system resources, the browser's persistent cookie jar retains the session ticket. When the user reloads the application on-site at a venue, the session ticket is sent with the initial HTTP request, avoiding unexpected forced logouts.
- *Note on Device Cleansing*: Operating system-level privacy resets or user-initiated cookie clearing will terminate the session; the client application must gracefully redirect to login when an unauthenticated status (`401 Unauthorized`) is returned.

#### 5. Revocation via `SecurityStamp`
- ASP.NET Core Identity embeds a cryptographic `SecurityStamp` in the user's encrypted cookie ticket.
- Whenever an Admin deactivates an account, resets a password, or modifies user roles (`AssignUserRole` / `RemoveUserRole`), the user's `SecurityStamp` in the database is regenerated (`UpdateSecurityStampAsync`).
- ASP.NET Core's `SecurityStampValidator` validates the stamp on a configurable periodic interval (e.g., 30 seconds, or immediate zero-interval on administrative override paths).
- Any request presenting a stale `SecurityStamp` is rejected with HTTP `401 Unauthorized`, prompting immediate session termination without requiring a distributed cache cluster (e.g., Redis).

#### 6. Decoupled Account Onboarding Boundary
- Specific workflows for account creation (Admin pre-creation with emailed activation token vs. self-registration with approval) remain an open business decision (Section 13 Decision 9 / ADR-011).
- ADR-002 defines only the authenticated session state once valid credentials exist.

---

## Pros and Cons of the Options

### Option 1: ASP.NET Core Identity + Secure HttpOnly Cookies (Unified Origin)
* **Good**: Materially reduces token-exfiltration risk: JavaScript cannot directly read the authentication ticket, eliminating the threat of trivial `localStorage` scraping.
* **Good**: Native, mature framework support in ASP.NET Core Identity (`AddIdentityCookies`).
* **Good**: Straightforward revocation via `SecurityStamp` without external cache dependencies (no Redis).
* **Good**: Eliminates CORS preflight round-trips and mobile battery drain for regular API calls.
* **Good**: No complex client-side refresh token rotation, retry queues, or race-condition handling in Angular HTTP interceptors.
* **Good**: Robust across mobile browsers compared with complex multi-domain token handshakes.
* **Bad**: Requires unified-origin routing (CloudFront/ALB path routing in production; proxy in local development).
* **Bad**: Does not eliminate XSS risk: malicious scripts executing in the DOM can still issue ambient authenticated requests in the victim's session; comprehensive XSS defenses (CSP, Angular sanitization, encoding) remain mandatory.

### Option 2: Stateless JWT Bearer Tokens + Refresh Tokens
* **Good**: Decouples API origin from frontend hosting without requiring reverse proxy path routing.
* **Bad**: Storing tokens in `localStorage` or `sessionStorage` leaves them exposed to direct exfiltration if an XSS flaw exists.
* **Bad**: Storing tokens in JavaScript memory causes immediate session loss whenever mobile browsers purge suspended background tabs or reload pages.
* **Bad**: Cannot immediately revoke access tokens upon administrative account deactivation or role modification without building a centralized server-side token blacklist (which reintroduces state and database lookups on every request, defeating statelessness).
* **Bad**: Significant implementation overhead in Angular: handling refresh token race conditions, concurrent request queuing, and clock skew.
* **Bad**: Architectural overkill for an internal 150-user application.

### Option 3: Backend-for-Frontend (BFF) Token Exchange Gateway
* **Good**: Ideal for enterprise Single Sign-On (SSO) with external OpenID Connect identity providers (Okta, Auth0, Entra ID).
* **Bad**: Requires deploying, monitoring, and scaling a dedicated BFF proxy container (e.g., Duende BFF). Unjustified because Carpediem is an internal modular monolith where ASP.NET Core Identity is the authoritative user directory.

---

## Security Impact

1. **Token Exfiltration Mitigation**: Authentication tickets remain inside the protected browser cookie store. Client-side scripts cannot directly read the cookie, mitigating automated credential harvesting via script injection.
2. **XSS Defense-in-Depth**: Recognizing that `HttpOnly` cookies do not stop an attacker from issuing authenticated requests via an active XSS payload, Carpediem enforces standard defensive measures:
   - Content Security Policy (CSP) headers restricting script execution sources.
   - Reliance on Angular 22's built-in contextual encoding and strict DOM sanitization.
   - Server-side input validation and encoding on all user-supplied text.
3. **CSRF Mitigation**: Multi-layered defense via `SameSite=Lax` cookie semantics, ASP.NET Core antiforgery token pair validation on mutating HTTP methods, and strict REST semantics.
4. **Session Protection**: Cookie payloads are encrypted and signed using ASP.NET Core Data Protection (AES-256-CBC with HMAC-SHA256) with automated key rotation.
5. **Brute-Force Protection**: ASP.NET Core Identity lockout policy configured for repeated failed password attempts (e.g., 5 failed attempts locks the account for 15 minutes).

---

## Testing Impact

- **Integration Tests**: `WebApplicationFactory<Program>` authenticates integration tests using standard cookie-container handlers (`TestServer.CreateClient()`).
- **End-to-End Tests (Playwright)**: Test workers capture authenticated browser storage state once during global setup (`storageState.json`) and reuse it across parallel workers without repetitive login sequences.

---

## Rejected Alternatives

- **JWT Tokens in LocalStorage / SessionStorage**: Formally rejected due to high exposure to token harvesting, inability to enforce instant administrative revocation without stateful tracking, and tab reload instability on mobile devices.
- **Separate API Subdomain without Path Routing (`api.carpediem.org` vs `app.carpediem.org`)**: Formally rejected because third-party cookie restrictions in modern mobile browsers (especially WebKit/Safari ITP) and CORS preflight latency degrade mobile user experience.

---

## Revisit Triggers

1. If Carpediem requires native mobile apps (iOS/Android) in post-V1 scope that cannot leverage standard browser cookie jars.
2. If Carpediem is mandated to integrate with an external enterprise SAML/OIDC federated directory (e.g., Azure AD / Okta).
