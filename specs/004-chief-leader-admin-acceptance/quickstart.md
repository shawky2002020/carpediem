# Quickstart: Validate SLICE-05

## Prerequisites

Implemented SLICE-02 through SLICE-04 host and SQL Server 2025 schema; an active Admin; a Draft Event with an active Chief Leader candidate's Pending appointment; .NET 10, supported Node dependencies, Playwright browsers, Docker, and k6.

## Validation

1. Run domain tests for Pending-to-Active external acceptance, UTC time, Admin attribution, non-Pending rejection, and same-Event team conflict.
2. Run SQL API integration with `tests/run-sql-integration.ps1 -ContainerTestHost`. Verify active Admin authorization, 1–1,000-character reason validation, unchanged Draft Event, `AdminExternal` provenance, audit atomicity, same-intent replay, and direct-response races.
3. Run Angular tests and production build. Check explicit attestation copy, labeled justification, length/whitespace validation, dirty navigation, loading/saving/success/conflict states, and preserved input after recoverable failure.
4. Run Playwright at 320/360/390/430 CSS px and desktop. Check no horizontal scrolling, 44×44 targets, keyboard/focus, 200% text, reduced motion, and accessible validation/status feedback.
5. Inspect generated OpenAPI against [the contract](contracts/chief-leader-admin-acceptance.openapi.yaml) and run a focused SQL-backed k6 journey for external acceptance and safe replay.
6. Review all exposed actions and verify publication, replacement, staffing, inbox controls, and external-channel integration are absent.

Expected: one Pending appointment becomes one Active `AdminExternal` assignment with exactly one complete audit record; every invalid, failed, stale, or losing concurrent operation leaves no partial or contradictory result.
