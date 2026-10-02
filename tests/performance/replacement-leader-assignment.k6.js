import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    replacement_leader_assignment: {
      executor: 'per-vu-iterations',
      vus: Number(__ENV.CARPEDIEM_K6_VUS || 100),
      iterations: 1,
      maxDuration: '5m',
    },
  },
  thresholds: {
    replacement_assignment_errors: ['count==0'],
    http_req_failed: ['rate==0'],
  },
};

const errors = new Counter('replacement_assignment_errors');
const journey = new Trend('replacement_assignment_journey', true);
const baseUrl = __ENV.BASE_URL || 'https://localhost:7076';

export function setup() {
  if (__ENV.CARPEDIEM_TARGET_EVENT_ID) {
    return { eventId: __ENV.CARPEDIEM_TARGET_EVENT_ID, candidates: [] };
  }

  const adminPassword = __ENV.CARPEDIEM_ADMIN_PASSWORD;
  if (!adminPassword) throw new Error('Set CARPEDIEM_ADMIN_PASSWORD to initialize target event.');

  let token = antiforgery();
  login('admin', adminPassword, token);
  token = antiforgery();

  const suffix = `perf-${Date.now()}`;
  const create = http.post(`${baseUrl}/api/events/drafts`, JSON.stringify({
    title: `Replacement Leader Assignment Benchmark ${suffix}`,
    start: { localDateTime: '2030-10-10T14:30' },
    end: { localDateTime: '2030-10-10T16:30' },
    location: 'Cairo Benchmark Arena',
    notes: null,
  }), { headers: writeHeaders(token, { 'Idempotency-Key': uuid() }) });
  if (create.status !== 201) throw new Error(`Create draft failed: ${create.status} ${create.body}`);
  const eventId = create.json('id');

  // Appoint chief.one as Chief Leader
  const appointmentCtx = http.get(`${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment`);
  if (appointmentCtx.status !== 200) throw new Error(`Read candidates failed: ${appointmentCtx.status}`);
  const appointCandidates = appointmentCtx.json('candidates') || [];
  const chiefOne = appointCandidates.find(c => c.userName === 'chief.one') || appointCandidates[0];
  if (!chiefOne) throw new Error('Chief Leader candidate not found.');

  const appointment = http.post(`${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment`,
    JSON.stringify({ candidateUserId: chiefOne.userId }),
    { headers: writeHeaders(token, { 'Idempotency-Key': uuid() }) });
  if (appointment.status !== 201) throw new Error(`Appoint failed: ${appointment.status} ${appointment.body}`);
  const assignmentId = appointment.json('id');
  const appointVersion = appointment.json('version');

  // Accept appointment externally
  const acceptUrl = `${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment/${assignmentId}/external-acceptance`;
  const acceptHeaders = writeHeaders(token, { 'If-Match': `"${appointVersion}"` });
  const acceptResponse = http.post(acceptUrl, JSON.stringify({ justification: `Confirmed verbally for ${suffix}` }), { headers: acceptHeaders });
  if (acceptResponse.status !== 200) throw new Error(`Acceptance failed: ${acceptResponse.status} ${acceptResponse.body}`);

  // Publish event
  const draftDetail = http.get(`${baseUrl}/api/events/drafts/${eventId}`);
  if (draftDetail.status !== 200) throw new Error(`Draft detail failed: ${draftDetail.status} ${draftDetail.body}`);
  const draftVersion = draftDetail.json('version');

  const publishUrl = `${baseUrl}/api/events/drafts/${eventId}/publish`;
  const publishHeaders = writeHeaders(token, { 'If-Match': `"${draftVersion}"` });
  const publishResponse = http.post(publishUrl, null, { headers: publishHeaders });
  if (publishResponse.status !== 200) throw new Error(`Publish failed: ${publishResponse.status} ${publishResponse.body}`);

  // Chief Leader logs in
  let chiefToken = antiforgery();
  login('chief.one', adminPassword, chiefToken);
  chiefToken = antiforgery();

  // Fetch candidates pool to find a leader to assign and cancel for a real vacancy record
  const poolRes = http.get(`${baseUrl}/api/events/${eventId}/direct-assignment/candidates`);
  if (poolRes.status !== 200) throw new Error(`Read candidates pool failed: ${poolRes.status} ${poolRes.body}`);
  const initialPool = poolRes.json('candidates') || [];
  const seedVolunteer = initialPool.find(c => c.userName === 'leader.one') || initialPool[0];
  if (!seedVolunteer) throw new Error('Seed volunteer not found.');

  // Direct assign seed volunteer
  const assignRes = http.post(`${baseUrl}/api/events/${eventId}/direct-assignment`,
    JSON.stringify({ candidateUserId: seedVolunteer.userId }),
    { headers: writeHeaders(chiefToken) });
  if (assignRes.status !== 201) throw new Error(`Initial assign failed: ${assignRes.status} ${assignRes.body}`);
  const seedAssignId = assignRes.json('id');
  const seedAssignVersion = assignRes.json('version');

  // Seed volunteer logs in and self-cancels to create vacancy
  let seedToken = antiforgery();
  login(seedVolunteer.userName, adminPassword, seedToken);
  seedToken = antiforgery();

  const cancelUrl = `${baseUrl}/api/events/${eventId}/assignments/${seedAssignId}/cancel`;
  const cancelRes = http.post(cancelUrl, JSON.stringify({ justification: `Academic exam conflict ${suffix}` }), {
    headers: writeHeaders(seedToken, { 'If-Match': `"${seedAssignVersion}"` }),
  });
  if (cancelRes.status !== 200) throw new Error(`Seed cancellation failed: ${cancelRes.status} ${cancelRes.body}`);

  // Chief Leader queries replacement context to get all available candidates
  chiefToken = antiforgery();
  login('chief.one', adminPassword, chiefToken);
  chiefToken = antiforgery();

  const ctxRes = http.get(`${baseUrl}/api/events/${eventId}/replacement-assignment`);
  if (ctxRes.status !== 200) throw new Error(`Replacement context query failed: ${ctxRes.status} ${ctxRes.body}`);
  const candidates = ctxRes.json('candidates') || [];

  const vusCount = Number(__ENV.CARPEDIEM_K6_VUS || 100);
  if (candidates.length < vusCount) {
    throw new Error(`Insufficient replacement candidates: expected ${vusCount}, found ${candidates.length}`);
  }

  return {
    eventId,
    cancelledAssignmentId: seedAssignId,
    candidates,
  };
}

export default function (data) {
  const started = Date.now();
  const password = __ENV.CARPEDIEM_LEADER_PASSWORD || __ENV.CARPEDIEM_ADMIN_PASSWORD;
  if (!password) throw new Error('Set CARPEDIEM_LEADER_PASSWORD or CARPEDIEM_ADMIN_PASSWORD.');

  const vuId = exec.vu.idInTest;
  const eventId = data && data.eventId;
  const candidates = (data && data.candidates) || [];
  const candidate = candidates[vuId - 1];

  if (!candidate) {
    errors.add(1, { operation: 'candidate_lookup' });
    return;
  }

  // Chief Leader logs in
  let token = antiforgery();
  login('chief.one', password, token);
  token = antiforgery();

  // Read replacement staffing context
  const readUrl = `${baseUrl}/api/events/${eventId}/replacement-assignment`;
  const readRes = http.get(readUrl);
  expectStatus(readRes, 200, 'get_replacement_assignment_context');
  if (readRes.status !== 200) return;

  check(readRes, {
    'context eventId match': val => val.json('eventId') === eventId,
    'context window open': val => val.json('isWindowOpen') === true,
    'context has cancelled assignments': val => (val.json('cancelledAssignments') || []).length > 0,
  }) || errors.add(1, { operation: 'get_replacement_context_shape' });

  const justification = `Replacement leader assignment for VU ${vuId}`;
  const payload = {
    candidateUserId: candidate.userId,
    replacedAssignmentId: data.cancelledAssignmentId,
    justification,
  };

  // Execute replacement leader assignment
  const assignUrl = `${baseUrl}/api/events/${eventId}/replacement-assignment`;
  const assignRes = http.post(assignUrl, JSON.stringify(payload), {
    headers: writeHeaders(token),
  });

  expectStatus(assignRes, 201, 'assign_replacement_leader');
  if (assignRes.status !== 201) return;

  check(assignRes, {
    'replacement status active': val => val.json('status') === 'Active',
    'replacement candidate userId match': val => val.json('userId') === candidate.userId,
    'replacement eventId match': val => val.json('eventId') === eventId,
    'replacement assignment id present': val => typeof val.json('id') === 'string',
  }) || errors.add(1, { operation: 'assign_replacement_shape' });

  // Execute safe idempotent replay
  const replayRes = http.post(assignUrl, JSON.stringify(payload), {
    headers: writeHeaders(token),
  });

  expectStatus(replayRes, 200, 'replay_assign_replacement_leader');
  check(replayRes, {
    'replay has idempotent header': val => val.headers['Idempotent-Replay'] === 'true',
    'replay status active': val => val.json('status') === 'Active',
    'replay candidate userId match': val => val.json('userId') === candidate.userId,
  }) || errors.add(1, { operation: 'replay_assign_replacement_shape' });

  journey.add(Date.now() - started);
}

function antiforgery() {
  const response = http.get(`${baseUrl}/api/session/antiforgery`);
  expectStatus(response, 200, 'antiforgery');
  const token = response.json('requestToken');
  if (!check(token, { 'antiforgery token present': value => typeof value === 'string' && value.length > 0 })) {
    errors.add(1, { operation: 'antiforgery_token' });
    throw new Error('Antiforgery token could not be acquired.');
  }
  return token;
}

function login(userName, password, token) {
  const response = http.post(`${baseUrl}/api/session/login`, JSON.stringify({ userName, password }), {
    headers: writeHeaders(token),
  });
  expectStatus(response, 200, 'login');
  check(response, {
    'session active': value => value.json('userName') === userName,
  }) || errors.add(1, { operation: 'session_active' });
}

function writeHeaders(token, extra = {}) {
  return Object.assign({
    'Content-Type': 'application/json',
    'X-XSRF-TOKEN': token,
  }, extra);
}

function expectStatus(response, status, operation) {
  const ok = check(response, {
    [`${operation} status is ${status}`]: res => res.status === status,
  });
  if (!ok) {
    errors.add(1, { operation, status: response.status });
  }
}

function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = Math.random() * 16 | 0;
    const v = c === 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}
