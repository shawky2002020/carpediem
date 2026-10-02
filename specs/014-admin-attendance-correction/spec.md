# SLICE-16: Admin Attendance Correction & Audit Logging

## Scope
- Admin corrects an attendance outcome post-finalization.
- Requires a mandatory correction justification reason.
- Audit trail immutably records `OldOutcome`, `NewOutcome`, acting Admin, timestamp, and justification.
- Persisted Entity State: `attendance.AttendanceOutcomes` (`Outcome` updated in-place).
- Audit & Notification: Immutable audit log written (`AttendanceCorrected`). Zero notifications dispatched.

## Invariants
1. **Endpoint**: `POST /api/attendance/{attendanceId}/correct` — Admin only (`RequireAdmin` policy). Requires antiforgery token.
2. **Mandatory Justification**: Non-empty `reason` string required. Empty reason → `400 Bad Request` (`missing_correction_reason`).
3. **AdminOverrideRequirement**: Verified in `EfAttendanceStore` — actor must have `Admin` role AND non-empty reason in command.
4. **Target Record Must Exist**: `404 Not Found` (`attendance_record_not_found`).
5. **Outcome Must Change**: Idempotent corrections (same outcome) allowed but noted.
6. **Optimistic Concurrency**: `attendance.AttendanceOutcomes` has `RowVersion rowversion NOT NULL`. Use `If-Match` header. `409 Conflict`.
7. **Immutable Audit Trail**: Append `AuditLog.AttendanceCorrected(...)`.
8. **Domain Method**: Add `CorrectOutcome` to `AttendanceOutcome`.
9. **Zero Notifications**: No notifications dispatched.
10. **Schema**: No schema change required.
