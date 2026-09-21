import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { Counter, Trend } from 'k6/metrics';

export const options = {
  scenarios: {
    draft_authoring_baseline: {
      executor: 'per-vu-iterations',
      vus: 100,
      iterations: 1,
      maxDuration: '5m',
    },
  },
};

const operationErrors = new Counter('draft_operation_errors');
const authoringJourney = new Trend('draft_authoring_journey', true);
const baseUrl = __ENV.BASE_URL || 'https://localhost:7076';

export default function () {
  const started = Date.now();
  const username = __ENV.CARPEDIEM_ADMIN_USERNAME;
  const password = __ENV.CARPEDIEM_ADMIN_PASSWORD;
  if (!username || !password) throw new Error('Set CARPEDIEM_ADMIN_USERNAME and CARPEDIEM_ADMIN_PASSWORD.');

  let token = antiforgery();
  const correlation = uuid();
  const common = {
    redirects: 0,
    headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token, 'X-Correlation-ID': correlation },
  };
  expectStatus(http.post(`${baseUrl}/api/session/login`, JSON.stringify({ userName: username, password }), common), 200, 'login');

  // ASP.NET Core binds antiforgery tokens to the authenticated identity.
  // Refresh after login so all state-changing requests use the Admin session token.
  token = antiforgery();
  common.headers['X-XSRF-TOKEN'] = token;

  expectStatus(http.get(`${baseUrl}/api/events/drafts?page=1&pageSize=20`, {
    headers: { 'X-Correlation-ID': uuid() },
  }), 200, 'list');

  const suffix = `${exec.vu.idInTest}-${Date.now()}`;
  const create = http.post(`${baseUrl}/api/events/drafts`, JSON.stringify({
    title: `Baseline Draft ${suffix}`,
    start: { localDateTime: '2030-10-10T14:30' },
    end: { localDateTime: '2030-10-10T16:30' },
    location: 'Cairo baseline venue',
    notes: null,
  }), {
    ...common,
    headers: { ...common.headers, 'Idempotency-Key': uuid() },
  });
  expectStatus(create, 201, 'create');
  if (create.status !== 201) return;

  const draft = create.json();
  expectStatus(http.get(`${baseUrl}/api/events/drafts/${draft.id}`, {
    headers: { 'X-Correlation-ID': uuid() },
  }), 200, 'read');

  const update = http.put(`${baseUrl}/api/events/drafts/${draft.id}`, JSON.stringify({
    title: `Updated Baseline Draft ${suffix}`,
    start: { localDateTime: '2030-10-10T14:30' },
    end: { localDateTime: '2030-10-10T16:30' },
    location: 'Cairo baseline venue',
    notes: null,
    version: draft.version,
  }), { ...common, headers: { ...common.headers, 'X-Correlation-ID': uuid() } });
  expectStatus(update, 200, 'update');
  authoringJourney.add(Date.now() - started);
}

function antiforgery() {
  const response = http.get(`${baseUrl}/api/session/antiforgery`);
  expectStatus(response, 200, 'antiforgery');
  return response.json('requestToken');
}

function expectStatus(response, expected, operation) {
  const success = check(response, { [`${operation} returns ${expected}`]: value => value.status === expected });
  if (!success) operationErrors.add(1, { operation, status: String(response.status) });
}

function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, character => {
    const random = Math.floor(Math.random() * 16);
    const value = character === 'x' ? random : (random & 0x3) | 0x8;
    return value.toString(16);
  });
}
