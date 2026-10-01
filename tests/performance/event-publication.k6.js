import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';

export const options = {
  insecureSkipTLSVerify: true,
  scenarios: {
    event_publication: {
      executor: 'per-vu-iterations',
      vus: Number(__ENV.CARPEDIEM_K6_VUS || 100),
      iterations: 1,
      maxDuration: '5m',
    },
  },
  thresholds: {
    event_publication_operation_errors: ['count==0'],
    http_req_failed: ['rate==0'],
  },
};

const errors = new Counter('event_publication_operation_errors');
const journey = new Trend('event_publication_journey', true);
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
    title: `Publication baseline ${suffix}`,
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
  const appointVersion = appointment.json('version');

  // Activate via external acceptance to satisfy publishing prerequisite invariant
  const acceptanceUrl = `${baseUrl}/api/events/drafts/${eventId}/chief-leader-appointment/${assignmentId}/external-acceptance`;
  const acceptHeaders = writeHeaders(token, { 'If-Match': `"${appointVersion}"` });
  const acceptResponse = http.post(acceptanceUrl, JSON.stringify({ justification: `Confirmed verbally for ${suffix}` }), { headers: acceptHeaders });
  expectStatus(acceptResponse, 200, 'record_external_acceptance');
  if (acceptResponse.status !== 200) return;

  // Retrieve current draft version for publish
  const draftDetail = http.get(`${baseUrl}/api/events/drafts/${eventId}`);
  expectStatus(draftDetail, 200, 'get_draft_detail');
  const draftVersion = draftDetail.json('version');

  // Publish Draft Event
  const publishUrl = `${baseUrl}/api/events/drafts/${eventId}/publish`;
  const publishHeaders = writeHeaders(token, { 'If-Match': `"${draftVersion}"` });
  const publishResponse = http.post(publishUrl, null, { headers: publishHeaders });
  expectStatus(publishResponse, 200, 'publish_event');
  check(publishResponse, {
    'published event has authoritative state': value =>
      value.json('id') === eventId &&
      value.json('status') === 'Published' &&
      value.json('activeChiefLeader.userId') === candidateId &&
      typeof value.json('version') === 'string' &&
      typeof value.json('publishedAt') === 'string',
  }) || errors.add(1, { operation: 'publish_shape' });

  // Idempotent Replay
  const replayResponse = http.post(publishUrl, null, { headers: publishHeaders });
  expectStatus(replayResponse, 200, 'publish_replay');
  check(replayResponse, {
    'replay has idempotent header and published status': value =>
      value.headers['Idempotent-Replay'] === 'true' &&
      value.json('id') === eventId &&
      value.json('status') === 'Published',
  }) || errors.add(1, { operation: 'replay_shape' });

  journey.add(Date.now() - started);
}

function antiforgery() {
  const response = http.get(`${baseUrl}/api/session/antiforgery`);
  expectStatus(response, 200, 'antiforgery');
  return response.json('requestToken');
}

function login(userName, password, token) {
  const response = http.post(`${baseUrl}/api/session/login`, JSON.stringify({
    userName,
    password,
  }), { headers: writeHeaders(token) });
  expectStatus(response, 200, 'login');
}

function writeHeaders(token, extra = {}) {
  return Object.assign({
    'Content-Type': 'application/json',
    'X-XSRF-TOKEN': token,
  }, extra);
}

function expectStatus(response, expected, operation) {
  const passed = check(response, {
    [`${operation} status is ${expected}`]: r => r.status === expected,
  });
  if (!passed) {
    errors.add(1, { operation, status: String(response.status) });
  }
}

function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = Math.random() * 16 | 0;
    const v = c === 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}
