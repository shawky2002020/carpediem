# Quickstart: SLICE-09 — Direct Leader Assignment

## 1. Running the Feature End-to-End

### Backend Endpoints
- `GET /api/events/{eventId}/direct-assignment/candidates`
  - Headers: Cookie session (Chief Leader or Admin)
  - Returns eligible direct draft candidates with availability status (`NotAvailable` or `NoResponse`)
- `POST /api/events/{eventId}/direct-assignment`
  - Headers: `X-XSRF-TOKEN`, Cookie session
  - Body: `{"candidateUserId": "..."}`
  - Returns 201 Created (or 200 with `Idempotent-Replay: true`)

### Frontend Interface
- Navigate to `/events/{eventId}/team-selection` as Chief Leader or Admin.
- View the "Direct Draft Pool" section showing non-respondents ("Response Needed") and leaders who marked "Not Available".
- Click "Direct Assign" next to a candidate.
- Observe immediate placement onto the "Active Team Roster" with polite live announcement.
