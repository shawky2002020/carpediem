import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    leader_self_cancellation: {
      executor: 'per-vu-iterations',
      vus: Number(__ENV.CARPEDIEM_K6_VUS || 100),
      iterations: 1,
      maxDuration: '5m',
    },
  },
  thresholds: {
    leader_self_cancellation_errors: ['count==0'],
    http_req_failed: ['rate==0'],
  },
};

const errors = new Counter('leader_self_cancellation_errors');
const journey = new Trend('leader_self_cancellation_journey', true);
const baseUrl = __ENV.BASE_URL || 'https://localhost:7076';

export function setup() {
  if (__ENV.CARPEDIEM_TARGET_EVENT_ID) {
    return { eventId: __ENV.CARPEDIEM_TARGET_EVENT_ID, assignments: [] };
  }

  const adminPassword = __ENV.CARPEDIEM_ADMIN_PASSWORD;
  if (!adminPassword) throw new Error('Set CARPEDIEM_ADMIN_PASSWORD to initialize target event.');

  let token = antiforgery();
  login('admin', adminPassword, token);
  token = antiforgery();

  const suffix = `perf-${Date.now()}`;
  const create = http.post(`${baseUrl}/api/events/drafts`, JSON.stringify({
    title: `Leader Self-Cancellation Benchmark ${suffix}`,
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
  const candidates = appointmentCtx.json('candidates') || [];
  const chiefOne = candidates.find(c => c.userName === 'chief.one') || candidates[0];
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

  // Fetch candidates available for direct assignment
  const poolRes = http.get(`${baseUrl}/api/events/${eventId}/direct-assignment/candidates`);
  if (poolRes.status !== 200) throw new Error(`Read direct candidates pool failed: ${poolRes.status} ${poolRes.body}`);
  const directCandidates = poolRes.json('candidates') || [];

  const vusCount = Number(__ENV.CARPEDIEM_K6_VUS || 100);
  if (directCandidates.length < vusCount) {
    throw new Error(`Insufficient direct candidates: expected ${vusCount}, found ${directCandidates.length}`);
  }

  // Chief Leader executes direct assignment for all benchmark VUs
  let chiefToken = antiforgery();
  login('chief.one', adminPassword, chiefToken);
  chiefToken = antiforgery();

  const assignments = [];
  for (let i = 0; i < vusCount; i++) {
    const candidate = directCandidates[i];
    const assignUrl = `${baseUrl}/api/events/${eventId}/direct-assignment`;
    const assignRes = http.post(assignUrl, JSON.stringify({ candidateUserId: candidate.userId }), {
      headers: writeHeaders(chiefToken),
    });

    if (assignRes.status !== 201) {
      throw new Error(`Direct assign failed for ${candidate.userName}: ${assignRes.status} ${assignRes.body}`);
    }

    assignments.push({
      assignmentId: assignRes.json('id'),
      userId: candidate.userId,
      userName: candidate.userName,
      version: assignRes.json('version'),
    });
  }

  return { eventId, assignments };
}

export default function (data) {
  const started = Date.now();
  const password = __ENV.CARPEDIEM_LEADER_PASSWORD || __ENV.CARPEDIEM_ADMIN_PASSWORD;
  if (!password) throw new Error('Set CARPEDIEM_LEADER_PASSWORD or CARPEDIEM_ADMIN_PASSWORD.');

  const vuId = exec.vu.idInTest;
  const eventId = data && data.eventId;
  const assignments = (data && data.assignments) || [];
  const assignment = assignments[vuId - 1];

  if (!assignment) {
    errors.add(1, { operation: 'assignment_lookup' });
    return;
  }

  // Assigned leader logs in to manage own assignment
  let token = antiforgery();
  login(assignment.userName, password, token);
  token = antiforgery();

  // Read own assignment context
  const myAssignmentUrl = `${baseUrl}/api/events/${eventId}/my-assignment`;
  const readRes = http.get(myAssignmentUrl);
  expectStatus(readRes, 200, 'get_my_assignment');
  if (readRes.status !== 200) return;

  check(readRes, {
    'my-assignment active': val => val.json('status') === 'Active',
    'my-assignment id match': val => val.json('assignmentId') === assignment.assignmentId,
    'my-assignment window open': val => val.json('isWindowOpen') === true,
  }) || errors.add(1, { operation: 'get_my_assignment_shape' });

  const justification = `Personal academic scheduling conflict for vu ${vuId}`;

  // Execute self-cancellation
  const cancelUrl = `${baseUrl}/api/events/${eventId}/assignments/${assignment.assignmentId}/cancel`;
  const cancelHeaders = writeHeaders(token, { 'If-Match': `"${assignment.version}"` });
  const cancelRes = http.post(cancelUrl, JSON.stringify({ justification }), {
    headers: cancelHeaders,
  });

  expectStatus(cancelRes, 200, 'cancel_assignment');
  if (cancelRes.status !== 200) return;

  check(cancelRes, {
    'cancelled status': val => val.json('status') === 'Cancelled',
    'cancelled id match': val => (val.json('id') || val.json('assignmentId')) === assignment.assignmentId,
    'cancelled reason match': val => val.json('cancellationReason') === justification,
    'cancelled timestamp present': val => typeof val.json('cancelledAt') === 'string',
  }) || errors.add(1, { operation: 'cancel_assignment_shape' });

  // Execute idempotent replay with identical justification
  const replayRes = http.post(cancelUrl, JSON.stringify({ justification }), {
    headers: writeHeaders(token),
  });

  expectStatus(replayRes, 200, 'replay_cancel_assignment');
  check(replayRes, {
    'replay has idempotent header': val => val.headers['Idempotent-Replay'] === 'true',
    'replay status cancelled': val => val.json('status') === 'Cancelled',
    'replay reason match': val => val.json('cancellationReason') === justification,
  }) || errors.add(1, { operation: 'replay_cancel_shape' });

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
