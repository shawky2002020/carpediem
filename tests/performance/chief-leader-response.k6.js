import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    chief_leader_direct_response: {
      executor: 'per-vu-iterations',
      vus: Number(__ENV.CARPEDIEM_K6_VUS || 100),
      iterations: 1,
      maxDuration: '5m',
    },
  },
  thresholds: {
    response_operation_errors: ['count==0'],
    http_req_failed: ['rate==0'],
  },
};

const errors = new Counter('response_operation_errors');
const journey = new Trend('chief_leader_response_journey', true);
const baseUrl = __ENV.BASE_URL || 'https://localhost:7076';

export default function () {
  const started = Date.now();
  const password = __ENV.CARPEDIEM_ADMIN_PASSWORD;
  if (!password) throw new Error('Set CARPEDIEM_ADMIN_PASSWORD.');
  let token = antiforgery();
  login('admin', password, token);
  token = antiforgery();

  const suffix = `${exec.vu.idInTest}-${Date.now()}`;
  const create = http.post(`${baseUrl}/api/events/drafts`, JSON.stringify({
    title: `Response baseline ${suffix}`,
    start: { localDateTime: '2030-10-10T14:30' },
    end: { localDateTime: '2030-10-10T16:30' },
    location: 'Cairo baseline venue',
    notes: null,
  }), { headers: writeHeaders(token, { 'Idempotency-Key': uuid() }) });
  expectStatus(create, 201, 'create_draft');
  if (create.status !== 201) return;
  const eventId = create.json('id');

  const context = http.get(`${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment`);
  expectStatus(context, 200, 'read_candidates');
  const candidateId = context.json('candidates.0.userId');
  if (!check(candidateId, { 'candidate exists': value => typeof value === 'string' })) {
    errors.add(1, { operation: 'candidate_exists' });
    return;
  }

  const appointment = http.post(`${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment`,
    JSON.stringify({ candidateUserId: candidateId }),
    { headers: writeHeaders(token, { 'Idempotency-Key': uuid() }) });
  expectStatus(appointment, 201, 'appoint');
  if (appointment.status !== 201) return;
  const assignmentId = appointment.json('id');
  const candidateName = appointment.json('candidate.userName');

  expectStatus(http.post(`${baseUrl}/api/session/logout`, '{}',
    { headers: writeHeaders(token) }), 204, 'logout');
  token = antiforgery();
  login(candidateName, password, token);
  token = antiforgery();

  const list = http.get(`${baseUrl}/api/chief-leader/appointments`);
  expectStatus(list, 200, 'list_pending');
  check(list, { 'own Pending appointment listed': response =>
    response.json().some(item => item.id === assignmentId && item.status === 'Pending') }) ||
    errors.add(1, { operation: 'list_shape' });

  const detail = http.get(`${baseUrl}/api/chief-leader/appointments/${assignmentId}`);
  expectStatus(detail, 200, 'read_appointment');
  const version = detail.json('version');
  const action = exec.vu.idInTest % 2 === 0 ? 'accept' : 'decline';
  const expected = action === 'accept' ? 'Active' : 'Declined';
  const responseUrl = `${baseUrl}/api/chief-leader/appointments/${assignmentId}/${action}`;
  const headers = writeHeaders(token, { 'If-Match': `"${version}"` });
  const response = http.post(responseUrl, null, { headers });
  expectStatus(response, 200, action);
  check(response, { 'direct response has expected state': value =>
    value.json('status') === expected && value.json('eventId') === eventId }) ||
    errors.add(1, { operation: 'response_shape' });

  const replay = http.post(responseUrl, null, { headers });
  expectStatus(replay, 200, 'same_action_replay');
  check(replay, { 'replay returns same assignment': value =>
    value.json('id') === assignmentId && value.json('status') === expected }) ||
    errors.add(1, { operation: 'replay_shape' });
  journey.add(Date.now() - started);
}

function login(userName, password, token) {
  const response = http.post(`${baseUrl}/api/session/login`,
    JSON.stringify({ userName, password }),
    { redirects: 0, headers: writeHeaders(token) });
  expectStatus(response, 200, `login_${userName}`);
}

function antiforgery() {
  const response = http.get(`${baseUrl}/api/session/antiforgery`);
  expectStatus(response, 200, 'antiforgery');
  return response.json('requestToken');
}

function writeHeaders(token, extra = {}) {
  return { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token,
    'X-Correlation-ID': uuid(), ...extra };
}

function expectStatus(response, expected, operation) {
  const success = check(response, { [`${operation} returns ${expected}`]: value =>
    value.status === expected });
  if (!success) {
    errors.add(1, { operation, status: String(response.status) });
    if (exec.vu.idInTest <= 2)
      console.error(`${operation} expected ${expected}, got ${response.status}: ${response.body}`);
  }
}

function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, character => {
    const random = Math.floor(Math.random() * 16);
    const value = character === 'x' ? random : (random & 0x3) | 0x8;
    return value.toString(16);
  });
}
