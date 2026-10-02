import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    admin_attendance_correction: {
      executor: 'per-vu-iterations',
      vus: 50,
      iterations: 1,
      maxDuration: '5m',
    },
  },
  thresholds: {
    attendance_correction_errors: ['count==0'],
    http_req_failed: ['rate==0'],
  },
};

const errors = new Counter('attendance_correction_errors');
const journey = new Trend('attendance_journey', true);
const baseUrl = __ENV.BASE_URL || 'https://localhost:7076';

export default function () {
  const started = Date.now();
  const password = __ENV.CARPEDIEM_ADMIN_PASSWORD;
  if (!password) throw new Error('Set CARPEDIEM_ADMIN_PASSWORD.');
  let token = antiforgery();
  login('admin', password, token);
  token = antiforgery();

  const suffix = `${exec.vu.idInTest}-${Date.now()}`;

  // 1. Create Draft Event
  const create = http.post(`${baseUrl}/api/events/drafts`, JSON.stringify({
    title: `Attendance baseline ${suffix}`,
    start: { localDateTime: '2030-10-10T14:30' },
    end: { localDateTime: '2030-10-10T16:30' },
    location: 'Cairo baseline venue',
    notes: null,
  }), { headers: writeHeaders(token, { 'Idempotency-Key': uuid() }) });
  expectStatus(create, 201, 'create_draft');
  if (create.status !== 201) return;
  const eventId = create.json('id');

  // 2. Read candidates to appoint Chief Leader
  const context = http.get(`${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment`);
  expectStatus(context, 200, 'read_candidates');
  const candidateId = context.json('candidates.0.userId');
  if (!check(candidateId, { 'candidate exists': value => typeof value === 'string' })) {
    errors.add(1, { operation: 'candidate_exists' });
    return;
  }

  // 3. Appoint Chief Leader
  const appointment = http.post(`${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment`,
    JSON.stringify({ candidateUserId: candidateId }),
    { headers: writeHeaders(token, { 'Idempotency-Key': uuid() }) });
  expectStatus(appointment, 201, 'appoint');
  if (appointment.status !== 201) return;
  const assignmentId = appointment.json('id');
  const appointVersion = appointment.json('version');

  // 4. External Acceptance to satisfy publication prerequisite
  const acceptanceUrl = `${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment/${assignmentId}/external-acceptance`;
  const acceptHeaders = writeHeaders(token, { 'If-Match': `"${appointVersion}"` });
  const acceptResponse = http.post(acceptanceUrl, JSON.stringify({ justification: `Confirmed verbally for ${suffix}` }), { headers: acceptHeaders });
  expectStatus(acceptResponse, 200, 'record_external_acceptance');
  if (acceptResponse.status !== 200) return;

  // 5. Retrieve current draft version for publish
  const draftDetail = http.get(`${baseUrl}/api/events/drafts/${eventId}`);
  expectStatus(draftDetail, 200, 'get_draft_detail');
  const draftVersion = draftDetail.json('version');

  // 6. Publish Draft Event
  const publishUrl = `${baseUrl}/api/events/drafts/${eventId}/publish`;
  const publishHeaders = writeHeaders(token, { 'If-Match': `"${draftVersion}"` });
  const publishResponse = http.post(publishUrl, null, { headers: publishHeaders });
  expectStatus(publishResponse, 200, 'publish_event');
  if (publishResponse.status !== 200) return;

  // 7. Direct assign volunteer to populate active roster
  const directCandidatesRes = http.get(`${baseUrl}/api/events/${eventId}/direct-assignment/candidates`);
  expectStatus(directCandidatesRes, 200, 'get_direct_candidates');
  const volunteerCandidateId = directCandidatesRes.json('candidates.0.userId');
  if (!check(volunteerCandidateId, { 'volunteer candidate exists': value => typeof value === 'string' })) {
    errors.add(1, { operation: 'volunteer_candidate_exists' });
    return;
  }

  const directAssignRes = http.post(`${baseUrl}/api/events/${eventId}/direct-assignment`,
    JSON.stringify({ candidateUserId: volunteerCandidateId }),
    { headers: writeHeaders(token) });
  expectStatus(directAssignRes, 201, 'direct_assign_volunteer');
  if (directAssignRes.status !== 201) return;
  const volunteerAssignmentId = directAssignRes.json('id');

  // 8. Inspect Attendance Roll Sheet (Unrecorded initial state)
  const rollSheetRes = http.get(`${baseUrl}/api/events/${eventId}/attendance`);
  expectStatus(rollSheetRes, 200, 'get_roll_sheet');
  const rollSheet = rollSheetRes.json();
  check(rollSheetRes, {
    'roll sheet has valid structure': () =>
      rollSheet &&
      rollSheet.eventId === eventId &&
      rollSheet.status === 'Published' &&
      rollSheet.isCompleted === false &&
      rollSheet.isAllFinalized === false &&
      Array.isArray(rollSheet.entries) &&
      rollSheet.entries.length === 1 &&
      rollSheet.entries[0].outcome === null,
  }) || errors.add(1, { operation: 'roll_sheet_shape' });

  // 9. Finalize Attendance Roll Call (Outcome = Attended)
  const finalizeUrl = `${baseUrl}/api/events/${eventId}/attendance`;
  const finalizePayload = JSON.stringify({
    entries: [
      { teamAssignmentId: volunteerAssignmentId, outcome: 'Attended' }
    ]
  });
  const finalizeRes = http.post(finalizeUrl, finalizePayload, { headers: writeHeaders(token) });
  expectStatus(finalizeRes, 200, 'finalize_attendance');
  const finalizeData = finalizeRes.json();
  check(finalizeRes, {
    'finalize response counts valid': () =>
      finalizeData &&
      finalizeData.eventId === eventId &&
      finalizeData.totalActiveAssignments === 1 &&
      finalizeData.attendedCount === 1 &&
      finalizeData.noShowCount === 0 &&
      finalizeData.unrecordedCount === 0,
  }) || errors.add(1, { operation: 'finalize_counts' });

  // 10. Admin Correction (Outcome updated to NoShow)
  // We need to fetch the roll sheet to get the outcome id and version
  const sheetBeforeCorrection = http.get(`${baseUrl}/api/events/${eventId}/attendance`);
  expectStatus(sheetBeforeCorrection, 200, 'get_roll_sheet_for_correction');
  const sheetData = sheetBeforeCorrection.json();
  const outcomeId = sheetData.entries[0].attendanceOutcomeId;
  const outcomeVersion = sheetData.entries[0].version;

  const correctUrl = `${baseUrl}/api/attendance/${outcomeId}/correct`;
  const correctionPayload = JSON.stringify({
    newOutcome: 'NoShow',
    reason: `Admin correction for ${suffix}`
  });
  
  const correctHeaders = writeHeaders(token, { 'If-Match': `"${outcomeVersion}"` });
  const correctionRes = http.post(correctUrl, correctionPayload, { headers: correctHeaders });
  expectStatus(correctionRes, 200, 'admin_correct_attendance');
  const correctionData = correctionRes.json();
  check(correctionRes, {
    'corrected response valid': () =>
      correctionData &&
      correctionData.attendanceOutcomeId === outcomeId &&
      correctionData.newOutcome === 'NoShow',
  }) || errors.add(1, { operation: 'correction_response_valid' });

  // 11. Verify Roll Sheet Reflects Corrected Outcome and isAllFinalized = true
  const finalRollSheet = http.get(`${baseUrl}/api/events/${eventId}/attendance`);
  expectStatus(finalRollSheet, 200, 'get_final_roll_sheet');
  const finalSheet = finalRollSheet.json();
  check(finalRollSheet, {
    'final roll sheet is all finalized': () =>
      finalSheet &&
      finalSheet.isAllFinalized === true &&
      Array.isArray(finalSheet.entries) &&
      finalSheet.entries.length === 1 &&
      finalSheet.entries[0].outcome === 'NoShow',
  }) || errors.add(1, { operation: 'final_roll_sheet_verified' });

  journey.add(Date.now() - started);
}

function expectStatus(response, expected, operation) {
  const ok = check(response, {
    [`${operation} status ${expected}`]: res => res.status === expected,
  });
  if (!ok) {
    errors.add(1, { operation });
    console.error(`Operation ${operation} failed: status=${response.status} body=${response.body}`);
  }
  return ok;
}

function antiforgery() {
  const res = http.get(`${baseUrl}/api/session/antiforgery`);
  if (!check(res, { 'antiforgery status 200': r => r.status === 200 })) {
    throw new Error(`Antiforgery failed: ${res.status}`);
  }
  return res.json('requestToken');
}

function login(userName, password, token) {
  const res = http.post(`${baseUrl}/api/session/login`,
    JSON.stringify({ userName, password }),
    { headers: writeHeaders(token) });
  if (!check(res, { 'login status 200': r => r.status === 200 })) {
    throw new Error(`Login failed for ${userName}: ${res.status}`);
  }
}

function writeHeaders(token, extra = {}) {
  return Object.assign({
    'Content-Type': 'application/json',
    'X-XSRF-TOKEN': token,
  }, extra);
}

function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}
