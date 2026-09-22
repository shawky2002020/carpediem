import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    chief_leader_appointment_baseline: {
      executor: 'per-vu-iterations',
      vus: Number(__ENV.CARPEDIEM_K6_VUS || 100),
      iterations: 1,
      maxDuration: '5m',
    },
  },
  thresholds: {
    appointment_operation_errors: ['count==0'],
    http_req_failed: ['rate==0'],
  },
};

const operationErrors = new Counter('appointment_operation_errors');
const journey = new Trend('chief_leader_appointment_journey', true);
const baseUrl = __ENV.BASE_URL || 'https://localhost:7076';

export default function () {
  const started = Date.now();
  const username = __ENV.CARPEDIEM_ADMIN_USERNAME;
  const password = __ENV.CARPEDIEM_ADMIN_PASSWORD;
  if (!username || !password) throw new Error('Set CARPEDIEM_ADMIN_USERNAME and CARPEDIEM_ADMIN_PASSWORD.');

  let token = antiforgery();
  const headers = { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token, 'X-Correlation-ID': uuid() };
  expectStatus(http.post(`${baseUrl}/api/session/login`, JSON.stringify({ userName: username, password }),
    { redirects: 0, headers }), 200, 'login');
  token = antiforgery();
  headers['X-XSRF-TOKEN'] = token;

  const suffix = `${exec.vu.idInTest}-${Date.now()}`;
  const create = http.post(`${baseUrl}/api/events/drafts`, JSON.stringify({
    title: `Appointment baseline ${suffix}`,
    start: { localDateTime: '2030-10-10T14:30' },
    end: { localDateTime: '2030-10-10T16:30' },
    location: 'Cairo baseline venue',
    notes: null,
  }), { headers: { ...headers, 'Idempotency-Key': uuid() } });
  expectStatus(create, 201, 'create_draft');
  if (create.status !== 201) return;
  const eventId = create.json('id');

  const context = http.get(`${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment`,
    { headers: { 'X-Correlation-ID': uuid() } });
  expectStatus(context, 200, 'read_candidates');
  const candidateId = context.json('candidates.0.userId');
  check(candidateId, { 'candidate exists': value => typeof value === 'string' }) ||
    operationErrors.add(1, { operation: 'candidate_exists' });
  if (!candidateId) return;

  const requestKey = uuid();
  const appointment = http.post(`${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment`,
    JSON.stringify({ candidateUserId: candidateId }),
    { headers: { ...headers, 'Idempotency-Key': requestKey, 'X-Correlation-ID': uuid() } });
  expectStatus(appointment, 201, 'appoint');
  check(appointment, {
    'appointment is Pending': response => response.json('status') === 'Pending',
    'appointment Event is unchanged': response => response.json('eventId') === eventId,
  }) || operationErrors.add(1, { operation: 'appointment_shape' });

  const replay = http.post(`${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment`,
    JSON.stringify({ candidateUserId: candidateId }),
    { headers: { ...headers, 'Idempotency-Key': requestKey, 'X-Correlation-ID': uuid() } });
  expectStatus(replay, 200, 'replay');
  check(replay, { 'replay returns same assignment': response => response.json('id') === appointment.json('id') }) ||
    operationErrors.add(1, { operation: 'replay_identity' });
  journey.add(Date.now() - started);
}

function antiforgery() {
  const response = http.get(`${baseUrl}/api/session/antiforgery`);
  expectStatus(response, 200, 'antiforgery');
  return response.json('requestToken');
}

function expectStatus(response, expected, operation) {
  const success = check(response, { [`${operation} returns ${expected}`]: value => value.status === expected });
  if (!success) {
    operationErrors.add(1, { operation, status: String(response.status) });
    if (exec.vu.idInTest <= 2) {
      console.error(`${operation} expected ${expected}, received ${response.status}: ${response.body}`);
    }
  }
}

function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, character => {
    const random = Math.floor(Math.random() * 16);
    const value = character === 'x' ? random : (random & 0x3) | 0x8;
    return value.toString(16);
  });
}
