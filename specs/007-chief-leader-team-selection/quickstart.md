# Quickstart Guide: SLICE-08 — Chief Leader Team Selection from Available Pool

## Overview
This guide provides concise instructions to test and verify Chief Leader team selection locally.

---

## 1. Prerequisites
- A published event with an accepted Chief Leader (`chief.one`) and volunteer Leaders (`leader.one`, `leader.two`) who declared `Available`.
- The current UTC time is before the event's `startDateTime`.

---

## 2. API Endpoints

### Query Team Selection Context
```http
GET /api/events/{eventId}/team-selection
Accept: application/json
Cookie: .AspNetCore.Identity.Application=...
```
- **Response 200 OK**:
  Returns event details, `availableCandidates` array, and `activeTeam` roster array.

### Select a Volunteer for the Team
```http
POST /api/events/{eventId}/team-selection
Content-Type: application/json
X-XSRF-TOKEN: <request-token>
Cookie: .AspNetCore.Identity.Application=...

{
  "candidateUserId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```
- **Response 201 Created**:
  Returns the created `TeamAssignment` with `status: "Active"`.
- **Response 200 OK (Idempotent replay)**:
  Returns identical `TeamAssignment` with `Idempotent-Replay: true` header.

---

## 3. UI Navigation
1. Log in as the assigned Chief Leader (`chief.one`).
2. Navigate to `/events/{eventId}/team-selection`.
3. View the candidate pool of available volunteers.
4. Click "Select for Team" on a candidate.
5. The volunteer appears immediately on the "Active Team Roster" and the count badge increments.
