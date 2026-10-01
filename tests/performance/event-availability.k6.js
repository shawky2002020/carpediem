import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    event_availability: {
      executor: 'per-vu-iterations',
      vus: Number(__ENV.CARPEDIEM_K6_VUS || 100),
      iterations: 1,
      maxDuration: '5m',
    },
  },
  thresholds: {
    event_availability_operation_errors: ['count==0'],
    http_req_failed: ['rate==0'],
  },
};

const errors = new Counter('event_availability_operation_errors');
const journey = new Trend('event_availability_journey', true);
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
    title: `Leader Availability Baseline ${suffix}`,
    start: { localDateTime: '2030-10-10T14:30' },
    end: { localDateTime: '2030-10-10T16:30' },
    location: 'Cairo Baseline Venue',
    notes: null,
  }), { headers: writeHeaders(token, { 'Idempotency-Key': uuid() }) });
  if (create.status !== 201) throw new Error(`Create draft failed: ${create.status} ${create.body}`);
  const eventId = create.json('id');

  const context = http.get(`${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment`);
  if (context.status !== 200) throw new Error(`Read candidates failed: ${context.status} ${context.body}`);
  const candidateId = context.json('candidates.0.userId');
  if (!candidateId) throw new Error('No candidate available for appointment.');

  const appointment = http.post(`${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment`,
    JSON.stringify({ candidateUserId: candidateId }),
    { headers: writeHeaders(token, { 'Idempotency-Key': uuid() }) });
  if (appointment.status !== 201) throw new Error(`Appoint failed: ${appointment.status} ${appointment.body}`);
  const assignmentId = appointment.json('id');
  const appointVersion = appointment.json('version');

  const acceptanceUrl = `${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment/${assignmentId}/external-acceptance`;
  const acceptHeaders = writeHeaders(token, { 'If-Match': `"${appointVersion}"` });
  const acceptResponse = http.post(acceptanceUrl, JSON.stringify({ justification: `Confirmed verbally for ${suffix}` }), { headers: acceptHeaders });
  if (acceptResponse.status !== 200) throw new Error(`Acceptance failed: ${acceptResponse.status} ${acceptResponse.body}`);

  const draftDetail = http.get(`${baseUrl}/api/events/drafts/${eventId}`);
  if (draftDetail.status !== 200) throw new Error(`Draft detail failed: ${draftDetail.status} ${draftDetail.body}`);
  const draftVersion = draftDetail.json('version');

  const publishUrl = `${baseUrl}/api/events/drafts/${eventId}/publish`;
  const publishHeaders = writeHeaders(token, { 'If-Match': `"${draftVersion}"` });
  const publishResponse = http.post(publishUrl, null, { headers: publishHeaders });
  if (publishResponse.status !== 200) throw new Error(`Publish failed: ${publishResponse.status} ${publishResponse.body}`);

  return { eventId };
}

export default function (data) {
  const started = Date.now();
  const password = __ENV.CARPEDIEM_LEADER_PASSWORD || __ENV.CARPEDIEM_ADMIN_PASSWORD;
  if (!password) throw new Error('Set CARPEDIEM_LEADER_PASSWORD or CARPEDIEM_ADMIN_PASSWORD.');

  const vuId = exec.vu.idInTest;
  const username = `leader.${vuId}`;

  let token = antiforgery();
  login(username, password, token);
  token = antiforgery();

  const eventId = __ENV.CARPEDIEM_TARGET_EVENT_ID || (data && data.eventId);
  if (!eventId) throw new Error('Target event ID is missing.');

  // Read current availability status
  const readUrl = `${baseUrl}/api/events/${eventId}/availability/me`;
  const readRes = http.get(readUrl);
  expectStatus(readRes, 200, 'get_my_availability');
  if (readRes.status !== 200) return;

  const currentVersion = readRes.json('version');
  const targetResponse = vuId % 2 === 0 ? 'Available' : 'NotAvailable';

  // Declare availability
  const declareUrl = `${baseUrl}/api/events/${eventId}/availability`;
  const headers = writeHeaders(token, currentVersion ? { 'If-Match': `"${currentVersion}"` } : {});
  const postRes = http.post(declareUrl, JSON.stringify({ response: targetResponse }), { headers });

  const isInitial = currentVersion === null;
  const expectedStatus = isInitial ? 201 : 200;
  expectStatus(postRes, expectedStatus, 'declare_availability');
  if (postRes.status !== expectedStatus) return;

  check(postRes, {
    'availability response matches': value =>
      value.json('eventId') === eventId &&
      value.json('response') === targetResponse &&
      typeof value.json('version') === 'string',
  }) || errors.add(1, { operation: 'availability_shape' });

  const savedVersion = postRes.json('version');

  // Idempotent replay: submit identical payload again
  const replayHeaders = writeHeaders(token, { 'If-Match': `"${savedVersion}"` });
  const replayRes = http.post(declareUrl, JSON.stringify({ response: targetResponse }), { headers: replayHeaders });
  expectStatus(replayRes, 200, 'replay_availability');
  check(replayRes, {
    'replay has idempotent header': value =>
      value.headers['Idempotent-Replay'] === 'true' &&
      value.json('eventId') === eventId &&
      value.json('response') === targetResponse,
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
