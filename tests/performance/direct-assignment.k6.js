import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    direct_assignment: {
      executor: 'per-vu-iterations',
      vus: Number(__ENV.CARPEDIEM_K6_VUS || 100),
      iterations: 1,
      maxDuration: '5m',
    },
  },
  thresholds: {
    direct_assignment_operation_errors: ['count==0'],
    http_req_failed: ['rate==0'],
  },
};

const errors = new Counter('direct_assignment_operation_errors');
const journey = new Trend('direct_assignment_journey', true);
const baseUrl = __ENV.BASE_URL || 'https://localhost:7076';

export function setup() {
  if (__ENV.CARPEDIEM_TARGET_EVENT_ID) {
    return { eventId: __ENV.CARPEDIEM_TARGET_EVENT_ID };
  }

  const adminPassword = __ENV.CARPEDIEM_ADMIN_PASSWORD;
  if (!adminPassword) throw new Error('Set CARPEDIEM_ADMIN_PASSWORD to initialize target event.');

  let token = antiforgery();
  login('admin', adminPassword, token);
  token = antiforgery();

  const suffix = `perf-${Date.now()}`;
  const create = http.post(`${baseUrl}/api/events/drafts`, JSON.stringify({
    title: `Direct Assignment Benchmark ${suffix}`,
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

  // Seed 100 leaders: 50 leaders declare 'NotAvailable', 50 remain NoResponse
  const vusCount = Number(__ENV.CARPEDIEM_K6_VUS || 100);
  const halfCount = Math.floor(vusCount / 2);
  for (let i = 1; i <= halfCount; i++) {
    const leaderUser = `leader.${i}`;
    let leaderToken = antiforgery();
    login(leaderUser, adminPassword, leaderToken);
    leaderToken = antiforgery();

    const availUrl = `${baseUrl}/api/events/${eventId}/availability`;
    const availRes = http.post(availUrl, JSON.stringify({ response: 'NotAvailable' }), {
      headers: writeHeaders(leaderToken),
    });
    if (availRes.status !== 201 && availRes.status !== 200) {
      throw new Error(`Leader ${leaderUser} availability failed: ${availRes.status} ${availRes.body}`);
    }
  }

  // Read direct assignment candidates pool
  let adminToken = antiforgery();
  login('admin', adminPassword, adminToken);
  adminToken = antiforgery();

  const poolRes = http.get(`${baseUrl}/api/events/${eventId}/direct-assignment/candidates`);
  if (poolRes.status !== 200) throw new Error(`Read direct candidates pool failed: ${poolRes.status} ${poolRes.body}`);
  const directCandidates = poolRes.json('candidates') || [];

  return { eventId, candidates: directCandidates };
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

  // Chief Leader executes direct assignment
  let token = antiforgery();
  login('chief.one', password, token);
  token = antiforgery();

  // Read candidates pool
  const readUrl = `${baseUrl}/api/events/${eventId}/direct-assignment/candidates`;
  const readRes = http.get(readUrl);
  expectStatus(readRes, 200, 'get_direct_assignment_candidates');
  if (readRes.status !== 200) return;

  // Direct assign leader
  const directAssignUrl = `${baseUrl}/api/events/${eventId}/direct-assignment`;
  const assignRes = http.post(directAssignUrl, JSON.stringify({ candidateUserId: candidate.userId }), {
    headers: writeHeaders(token),
  });

  expectStatus(assignRes, 201, 'direct_assign_leader');
  if (assignRes.status !== 201) return;

  check(assignRes, {
    'assignment active': val => val.json('status') === 'Active',
    'assignment candidate match': val => val.json('userId') === candidate.userId,
    'assignment event match': val => val.json('eventId') === eventId,
  }) || errors.add(1, { operation: 'direct_assign_shape' });

  // Idempotent replay
  const replayRes = http.post(directAssignUrl, JSON.stringify({ candidateUserId: candidate.userId }), {
    headers: writeHeaders(token),
  });

  expectStatus(replayRes, 200, 'replay_direct_assign');
  check(replayRes, {
    'replay has idempotent header': val => val.headers['Idempotent-Replay'] === 'true',
    'replay status active': val => val.json('status') === 'Active',
  }) || errors.add(1, { operation: 'replay_shape' });

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
