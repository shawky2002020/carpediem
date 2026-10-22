# V1 implementation status

As of 2026-10-22, FOUNDATION-01 and SLICE-02 through SLICE-22 are implemented in this repository. The last decision gates were resolved in [CHANGE-003](../decisions/CHANGE-003-v1-remaining-slice-policy.md).

| Slice | Delivered behavior |
| --- | --- |
| 13 | Admin Event cancellation preserves assignment rows as history, closes the active roster, and notifies affected Leaders and the Chief Leader. |
| 17 | Recipient-scoped notification inbox with unread count, read actions, and client refresh. |
| 18 | Factual Leader history from assignments, attendance, and self-cancellations. |
| 19 | Role-scoped operational dashboard with derived in-progress and pending-attendance indicators. |
| 20 | Public Leader registration pending Admin approval; Admin account and role management with session revocation. Registration is rate limited and duplicate identifiers receive the same public response as accepted registrations. Admin account results are paged. |
| 21 | Post-completion 1–5 ratings: Chief Leader to active assigned Leaders on the current staffing cycle, Leader to Event; one rating per rater and subject, optional 500-character comment, Admin-only review list. |
| 22 | Event incident reporting with title, description, optional occurrence time, and reporter identity. A Leader sees their own reports; a Chief Leader sees reports for Events they actively lead; Admins see all and mark reports reviewed with an optional note. Reporting and review are audited. |

The other slices were delivered before or earlier in the current implementation branch. The shared draft fixture now uses a future date so staffing tests remain meaningful after October 2026.

Production requires `ReverseProxy__TrustedNetworks__0` and subsequent entries with the ALB source CIDRs and CloudFront origin-facing CIDRs. The API trusts `X-Forwarded-For` and `X-Forwarded-Proto` only from those networks and uses the viewer IP for the public registration limit. Keep the list current as proxy networks change; startup rejects a Production configuration without trusted networks.

Deployment artifacts and operational readiness:

- [`Dockerfile`](../../Dockerfile) builds a multi-stage, non-root `.NET 10` runtime container (`carpediem-api`) listening on port `8080`.
- [`deploy/proxy.Dockerfile`](../../deploy/proxy.Dockerfile) and [`deploy/nginx.conf`](../../deploy/nginx.conf) compile the Angular 22 SPA and serve it alongside same-origin `/api/*` and `/healthz` reverse-proxy routing per `ADR-008`.
- [`deploy/generate-migration-script.ps1`](../../deploy/generate-migration-script.ps1) generates the idempotent SQL Server migration bundle at [`deploy/migrations/v1-idempotent.sql`](../../deploy/migrations/v1-idempotent.sql) for out-of-process migration execution per `ADR-008` and `ADR-009`.
- [`compose.yaml`](../../compose.yaml) preserves the default local `sqlserver` service and adds `carpediem-migrator`, `carpediem-api`, and `carpediem-proxy` under `--profile stack`.
- `/healthz` provides lightweight liveness probing (`Predicate = _ => false`) and `/api/health` provides database readiness probing (`DatabaseReadinessHealthCheck`).

Verification on 2026-10-22:

- SQL Server API integration tests: 184 passed.
- Domain tests: 96 passed.
- Angular tests: 100 passed; production build succeeded.
- Playwright E2E browser tests (`tests/e2e/v1-remaining-slices.spec.ts`): 20 passed across all 5 viewports (`320px`, `360px`, `390px`, `430px`, `1280px`) with WCAG 2.2 AA `axe-core` verification.
- EF Core reports no pending model changes after the feedback and incident migration.
- The transitive `System.Security.Cryptography.Pkcs` dependency is pinned to the patched 9.0.21 release.
