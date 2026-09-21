# Quickstart Validation: SLICE-02 Draft Events

This guide starts and verifies the implemented Draft Event creation and editing slice. See [plan.md](plan.md), [data-model.md](data-model.md), and the [Draft Event API contract](contracts/draft-events.openapi.yaml) for the approved design.

## Prerequisites

- .NET 10 SDK.
- Node.js 24.15 or newer for Angular 22.
- Docker with Compose.
- k6.
- A Playwright-supported browser.
- ICU and time-zone data for `Africa/Cairo`.

## Start the feature locally

1. Copy `.env.example` to `.env` and replace all placeholder passwords with local values. Do not commit `.env`.
2. Start SQL Server: `docker compose --env-file .env up -d sqlserver`.
3. Load the environment values from `.env` into the shell, then restore the local EF tool: `dotnet tool restore`.
4. Apply the migration:

   `dotnet ef database update --project src/Infrastructure/Carpediem.Infrastructure.csproj --startup-project src/Carpediem.Web/Carpediem.Web.csproj`

5. Start the API with its HTTPS profile:

   `dotnet run --project src/Carpediem.Web/Carpediem.Web.csproj --launch-profile https`

6. Install and start the client: `npm --prefix frontend ci`, then `npm --prefix frontend start`. The checked-in proxy sends `/api` and `/healthz` to `https://localhost:7076`.
7. Open `http://localhost:4200/sign-in`, sign in as `admin` with the local `DevelopmentSeed__Password`, and open **Draft Events**. Chief Leader and Leader fixture roles do not grant this access.

Development fixtures create one Admin, two Chief Leaders, and five Leaders only when a local seed password is configured. Production account administration remains outside this slice.

## Manual acceptance walk-through

1. Choose **Create Event**. Confirm a single-column form at 320 CSS pixels, a visible primary action, an `Africa/Cairo` label, clear fields, no horizontal scrolling, keyboard focus order, and touch-sized actions.
2. Enter Title, Location, Start and End as Cairo local times with Start before End; optionally enter Notes. Save. Confirm one Draft is created, success is announced, and the values appear when reopened. The persisted timestamps represent the corresponding UTC instants and one initial current `StaffingCycle` exists.
3. Repeat without Notes; creation succeeds.
4. Edit the Draft, including a changed schedule. Save and reopen. Values persist and status remains `Draft`. A Cairo calendar-date change leaves exactly one current cycle while preserving the earlier cycle as history.
5. Leave an unsaved creation after entering values. Confirm the loss warning and that no Event was created.
6. Submit missing or whitespace-only required fields, equal dates, reversed dates, and an unusable Cairo local time. Each case identifies the field, preserves values for correction, and leaves persisted state unchanged. A valid past Draft schedule is accepted.
7. Simulate an uncertain create response and retry the same intent. The returned Event ID matches the committed Event, with no duplicate Event or audit. A distinct intent may create another Event with identical business values.
8. Open one Draft in two Admin sessions. Save from the first, then save stale values from the second. The second receives a conflict, retains its entered values, and does not overwrite the first save.
9. Attempt Draft operations as Chief Leader, Leader, and anonymously. Confirm safe 403 or 401 outcomes and no data changes. An unknown Event returns 404; a non-Draft Event cannot be edited here.
10. Interrupt create or edit responses. Confirm clear failure or uncertain-outcome feedback, retained form content, and safe retry.

## Automated verification

Run commands from the repository root unless a different directory is shown.

| Layer | Command | Verified evidence (2026-09-21) |
|---|---|---|
| Build | `dotnet build Carpediem.slnx --configuration Release` | PASS, 0 warnings and 0 errors |
| Domain | `dotnet test tests/Carpediem.Domain.Tests/Carpediem.Domain.Tests.csproj --configuration Release` | PASS, 25/25 |
| API and SQL Server | `pwsh -NoProfile -File tests/run-sql-integration.ps1` | PASS, 40/40 against an isolated SQL Server 2025 container |
| Angular | `npm --prefix frontend test` | PASS, 13/13 |
| Angular production build | `npm --prefix frontend run build` | PASS |
| Browser, mobile, accessibility | `npm --prefix tests/e2e test` | PASS, 75/75 across 320, 360, 390, 430 CSS-pixel and desktop projects; includes axe, 200% text, and reduced-motion checks |
| Formatting | `git diff --check` and `dotnet format Carpediem.slnx --verify-no-changes --no-restore` | PASS |
| OpenAPI | Compare `artifacts/openapi/generated-draft-events.json` with both approved contracts | PASS; cookie security and documented success/error responses present |
| Migration | Inspect `artifacts/migrations/001-draft-event-creation-editing.sql` | PASS; named checks/FKs/indexes, UTC constraints, RCSI, rollback, and no later-slice schema |
| Performance baseline | `k6 run --insecure-skip-tls-verify tests/performance/draft-events.k6.js` with `BASE_URL`, `CARPEDIEM_ADMIN_USERNAME`, and `CARPEDIEM_ADMIN_PASSWORD` set | PASS, 100 journeys and 700/700 checks; 0% HTTP errors; journey p95 1.60 s. This is observational because no latency threshold is approved. |
| Real stack smoke | Angular 22 client → HTTPS API → SQL Server 2025 at 320 CSS pixels | PASS; sign-in, create, reopen, edit, success feedback, and no horizontal overflow |

The integration script generates its own test credential, starts an isolated SQL Server container on an available local port, and removes it afterward. Review migration SQL before applying it outside local development; the application does not run production migrations automatically.

## Story checkpoints

- **US1 Create/reopen**: PASS through domain, SQL integration, Angular, browser, and real-stack smoke evidence.
- **US2 Correct invalid input**: PASS for missing/Unicode-whitespace fields, malformed and unusable Cairo times, equal/reversed windows, retained values, and successful correction.
- **US3 Edit Draft**: PASS for complete updates, unchanged Draft lifecycle, cycle history, audit allow-list, rowversion conflicts, and retained stale values.
- **US4 Access/failures**: PASS for the role matrix, current-role and active-user checks, antiforgery, 401/403/404/409/500 contracts, uncertain create retry, and form retention.
