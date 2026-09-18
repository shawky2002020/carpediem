# Quickstart Validation: SLICE-02 Draft Events

This is a run-and-verify guide for the planned first vertical slice, to be used after implementation. The repository currently has no application host or Angular workspace; these commands become runnable when the planned FOUNDATION-01 bootstrap is implemented. See [plan.md](plan.md), [data-model.md](data-model.md), and the [Draft Event API contract](contracts/draft-events.openapi.yaml) for expected behavior.

## Prerequisites

- .NET 10 SDK, Node.js version supported by Angular 22, Docker with Compose, and a browser supported by Playwright.
- Local SQL Server 2025 container configured by the implementation's `compose.yaml`, with `READ_COMMITTED_SNAPSHOT ON`.
- Development-only deterministic fixtures enabled: one Admin, two Chief Leaders, five Leaders. Use the generated/locally configured fixture credentials; do not put them in this document or commit secrets.
- Host environment has ICU/time-zone data for `Africa/Cairo`.

## Start the feature locally

1. Start the SQL Server Compose service: `docker compose up -d sqlserver`.
2. Apply the first feature migration locally with the host DbContext: `dotnet ef database update --project src/Infrastructure/Carpediem.Infrastructure.csproj --startup-project src/Carpediem.Web/Carpediem.Web.csproj`. Confirm RCSI and migration status using the implementation's database checks.
3. Start the API: `dotnet run --project src/Carpediem.Web/Carpediem.Web.csproj`.
4. Install/start the Angular client: `npm --prefix frontend install`, then `npm --prefix frontend start`. The development proxy must keep `/api/*` same-origin from the browser's perspective.
5. Open the app, sign in as the seeded Admin, and navigate to Draft Events. A Chief Leader or Leader fixture must not gain Draft authoring access.

These paths and commands describe the planned source layout; if the implementation chooses different executable project names within the locked architecture, update this guide during implementation.

## Manual acceptance walk-through

1. Choose **Create Event**. Confirm a single-column narrow-mobile form, visible primary action, Cairo time-zone label, clear fields, no normal horizontal scroll, keyboard/focus order, and 44×44px touch targets.
2. Enter Title, Location, Start and End as Cairo local times with Start before End; optionally enter Notes. Save. Confirm one Draft is created, success is announced, and the supplied values appear when reopened. Confirm the persisted timestamps represent the corresponding UTC instants and one initial current `StaffingCycle` exists.
3. Repeat without Notes; creation must still succeed.
4. Edit the Draft, including a changed schedule. Save and reopen. Confirm values persist and status remains `Draft`. A Cairo calendar-date change must leave exactly one current cycle; the previous cycle remains historical.
5. Leave an unsaved creation after entering values. Confirm the loss warning and that no Event was created.
6. Submit missing/whitespace-only required fields, equal dates, reversed dates, and unusable Cairo local time. Each case must identify the field, preserve entered values where practical, and leave persisted state unchanged. A valid past Draft schedule must not be rejected solely for being past.
7. Simulate an uncertain create response and retry the same intent. The returned Event ID must match the first committed Event, with no duplicate Event or audit. A new deliberate creation with identical business values and a different intent key may create a separate Event.
8. Open one Draft in two Admin sessions. Save from the first, then save stale values from the second. The second receives a conflict; its unsaved form remains available and the first saved values remain persisted.
9. Attempt read/create/update as a Chief Leader and a Leader. Confirm safe 403 outcomes and no data changes. An unauthenticated session receives 401. An unknown Event receives 404; a non-Draft Event cannot be edited through this feature.
10. Simulate recoverable network failure during create/edit. Confirm failure or uncertain-state feedback, retained form content where practical, and safe retry. Check that no incomplete Event/cycle/audit combination appears.

## Automated verification

| Layer | Planned command/path | Evidence |
|---|---|
| Domain | `dotnet test tests/Carpediem.Domain.Tests` | Required values, whitespace, date ordering, Draft state, Cairo gap/overlap |
| API/SQL integration | `dotnet test tests/Carpediem.Api.IntegrationTests` | Real SQL Server constraints, atomicity, audit, authorization, rowversion conflicts, retry ledger |
| Angular | `npm --prefix frontend test` | Form validation, state transitions, field/global errors, retained input |
| Browser | `npx playwright test` from `tests/e2e` | Admin Cairo create→open→edit journey plus focused mobile/keyboard and negative flows |
| Contract | Validate generated OpenAPI against [contract](contracts/draft-events.openapi.yaml) | Success schemas, 400/401/403/404/409/422/500 error cases |

Review the migration SQL script before applying it outside local development. Do not rely on automatic production migration at app startup. The app must expose a healthy `/healthz` response without leaking credentials or submitted form text in logs.
