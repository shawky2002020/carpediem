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

Production requires `ReverseProxy__TrustedNetworks__0` and subsequent entries with the ALB source CIDRs and CloudFront origin-facing CIDRs. The API trusts `X-Forwarded-For` only from those networks and uses the viewer IP for the public registration limit. Keep the list current as proxy networks change; startup rejects a Production configuration without trusted networks.

Verification on 2026-10-22:

- SQL Server API integration tests: 182 passed.
- Domain tests: 96 passed.
- Angular tests: 100 passed; production build succeeded.
- EF Core reports no pending model changes after the feedback and incident migration.
- The transitive `System.Security.Cryptography.Pkcs` dependency is pinned to the patched 9.0.21 release.
