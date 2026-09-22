# Quickstart: Validate SLICE-04

## Prerequisites

SLICE-03 host and SQL Server 2025 schema; an active Admin, active Chief Leader candidate, and a Draft Event with that candidate's Pending appointment; .NET 10, Node dependencies, Playwright browsers, and Docker.

## Validation

1. Run domain tests for Pending → Active, Pending → Declined, non-Pending rejection, UTC times, and same-Event team conflict.
2. Run SQL API integration with `tests/run-sql-integration.ps1 -ContainerTestHost`. Verify candidate access, role revocation, unchanged Draft, audit/alert atomicity, replay, and competing decisions.
3. Run Angular tests and production build. Check loading, saving, success, conflict, and recoverable failures.
4. Run Playwright at 320/360/390/430 CSS px and desktop. Check scrolling, 44×44 targets, keyboard/focus, 200% text, reduced motion, and accessible status.
5. Inspect generated OpenAPI against [the contract](contracts/chief-leader-direct-response.openapi.yaml) and run focused SQL-backed k6 response journeys.

Expected: one final direct outcome; acceptance activates one assignment, decline preserves history and alerts active Admins, and failures do not create partial results.
