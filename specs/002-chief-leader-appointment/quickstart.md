# Quickstart: Validate SLICE-03

## Prerequisites

- Docker Desktop with the repository SQL Server container available
- .NET 10 SDK and Node dependencies installed as documented by the root project
- The SLICE-02 schema and app baseline
- Development seed password configured for local sign-in

## Automated validation

```powershell
dotnet test tests/Carpediem.Domain.Tests/Carpediem.Domain.Tests.csproj --configuration Release
.\tests\run-sql-integration.ps1 -ContainerTestHost
npm --prefix frontend test -- --watch=false
npm --prefix tests/e2e test
.\tests\performance\run-chief-leader-appointment.ps1
```

The container test host runs the real SQL Server suite in Linux when Windows Application Control blocks freshly built test DLLs. Generate and inspect the migration SQL and host OpenAPI artifact using the repository commands recorded in implementation tasks. The performance script starts an isolated migrated SQL Server and HTTPS host, then runs the 100-user k6 appointment workload.

## Critical end-to-end proof

1. Sign in as the seeded Admin and create a valid Draft Event.
2. Open Chief Leader appointment from that Draft.
3. Verify active `chief.one` and `chief.two` candidates are distinguishable, while ordinary Leaders are absent.
4. Appoint one candidate and verify the UI shows `Pending` while the Event remains `Draft`.
5. Query SQL Server and verify one assignment, one `ChiefLeaderAppointed` audit entry, one unread `ChiefLeaderAppointment` notification, and one request record share the committed operation context.
6. Repeat the exact request token and verify the same assignment is returned with no new rows.
7. Attempt another candidate and verify a conflict leaves the first assignment unchanged.
8. Repeat through Chief Leader and Leader sessions and verify candidate data and mutation are denied.

## Failure and integrity proof

- Unknown Event: safe 404, no writes.
- Non-Draft Event: stable 409, no writes.
- Candidate made inactive or removed from the role after page load: field-addressable rejection, no writes.
- Concurrent same-candidate submissions: one assignment/audit/notification.
- Concurrent different candidates: one wins, one conflicts, one unresolved assignment.
- Forced persistence failure: no partial assignment, request, audit, or notification.
- Declined historical fixture: new Pending row is added without modifying history.

## Mobile and accessibility proof

Run the appointment journey at 320, 360, 390, and 430 CSS px and desktop. Verify no horizontal scrolling; at least 44×44 targets; keyboard-only completion; visible focus; accessible loading, error, and success announcements; non-color-only Pending status; 200% text; reduced motion; and automated WCAG checks.

## Scope proof

Inspect routes, OpenAPI, and UI actions. They must contain no accept, decline, external acceptance, publish, team assignment, notification inbox/read/dismiss, or external-channel operation.
