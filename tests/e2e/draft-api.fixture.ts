import { Page, Route } from '@playwright/test';

export interface MockDraft {
  id: string;
  title: string;
  start: { localDateTime: string; offset: string };
  end: { localDateTime: string; offset: string };
  startDateTimeUtc: string;
  endDateTimeUtc: string;
  location: string;
  notes: string | null;
  status: 'Draft';
  version: string;
}

export interface DraftApiOptions {
  role?: 'Admin' | 'ChiefLeader' | 'Leader';
  updateFailure?: 'stale' | 'network' | 'unauthenticated';
  createUncertainOnce?: boolean;
  createValidationOnce?: { field: string; message: string };
  appointmentUncertainOnce?: boolean;
  appointmentIneligibleOnce?: boolean;
}

export async function mockDraftApi(page: Page, options: DraftApiOptions = {}) {
  let draft: MockDraft | null = null;
  let createCount = 0;
  let appointmentCount = 0;
  let appointment: { id: string; eventId: string; candidate: { userId: string; userName: string }; status: 'Pending'; appointedAt: string; appointedByUserId: string; version: string } | null = null;
  const candidates = [
    { userId: '30000000-0000-0000-0000-000000000001', userName: 'chief.one' },
    { userId: '30000000-0000-0000-0000-000000000002', userName: 'chief.two' },
  ];

  await page.route('**/api/**', async route => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    if (path === '/api/session/me') {
      return json(route, 200, { userId: '10000000-0000-0000-0000-000000000001', userName: 'admin', roles: [options.role ?? 'Admin'] });
    }
    if (path === '/api/session/antiforgery') return json(route, 200, { requestToken: 'test-token' });
    if (path === '/api/events/drafts' && request.method() === 'GET') {
      return json(route, 200, { items: draft ? [summary(draft)] : [], page: 1, pageSize: 20 });
    }
    if (path === '/api/events/drafts' && request.method() === 'POST') {
      createCount += 1;
      if (options.createValidationOnce && createCount === 1) {
        const { field, message } = options.createValidationOnce;
        return route.fulfill({
          status: 422,
          contentType: 'application/problem+json',
          body: JSON.stringify({ type: 'https://httpstatuses.com/422', title: 'Review the highlighted fields.',
            status: 422, code: 'validation_failed', traceId: 'e2e-trace', errors: { [field]: [message] } }),
        });
      }
      const body = request.postDataJSON();
      draft = {
        id: '20000000-0000-0000-0000-000000000001',
        title: body.title,
        start: { localDateTime: normalize(body.start.localDateTime), offset: '+03:00' },
        end: { localDateTime: normalize(body.end.localDateTime), offset: '+03:00' },
        startDateTimeUtc: '2026-10-10T11:30:00Z',
        endDateTimeUtc: '2026-10-10T13:30:00Z',
        location: body.location,
        notes: body.notes,
        status: 'Draft',
        version: 'AAAAAAAAB9E=',
      };
      if (options.createUncertainOnce && createCount === 1) return route.abort('connectionfailed');
      return json(route, createCount > 1 ? 200 : 201, draft,
        createCount > 1 ? { 'Idempotent-Replay': 'true' } : { Location: `/api/events/drafts/${draft.id}` });
    }
    if (path.endsWith('/chief-leader-appointment') && request.method() === 'GET') {
      if (!draft) return problem(route, 404, 'event_not_found', 'Draft Event was not found.');
      return json(route, 200, {
        eventId: draft.id, eventTitle: draft.title, eventStatus: 'Draft', currentAppointment: appointment,
        declinedHistory: [],
        candidates: appointment ? [] : candidates,
      });
    }
    if (path.endsWith('/chief-leader-appointment') && request.method() === 'POST') {
      appointmentCount += 1;
      if (!draft) return problem(route, 404, 'event_not_found', 'Draft Event was not found.');
      if (options.appointmentIneligibleOnce && appointmentCount === 1) {
        return problem(route, 422, 'candidate_ineligible', 'Review the selected candidate.');
      }
      const body = request.postDataJSON();
      const candidate = candidates.find(item => item.userId === body.candidateUserId)!;
      appointment ??= {
        id: '40000000-0000-0000-0000-000000000001', eventId: draft.id, candidate, status: 'Pending',
        appointedAt: '2026-09-22T09:00:00Z', appointedByUserId: '10000000-0000-0000-0000-000000000001',
        version: 'AAAAAAAAB9M=',
      };
      if (options.appointmentUncertainOnce && appointmentCount === 1) return route.abort('connectionfailed');
      return json(route, appointmentCount > 1 ? 200 : 201, appointment,
        appointmentCount > 1 ? { 'Idempotent-Replay': 'true' } : undefined);
    }
    if (path.startsWith('/api/events/drafts/') && request.method() === 'GET') {
      return draft
        ? json(route, 200, draft)
        : problem(route, 404, 'event_not_found', 'Draft Event was not found.');
    }
    if (path.startsWith('/api/events/drafts/') && request.method() === 'PUT') {
      if (options.updateFailure === 'network') return route.abort('connectionfailed');
      if (options.updateFailure === 'unauthenticated') return problem(route, 401, 'unauthenticated', 'Authentication is required.');
      if (options.updateFailure === 'stale') return problem(route, 409, 'stale_draft', 'The Draft changed since it was opened.');
      if (!draft) return problem(route, 404, 'event_not_found', 'Draft Event was not found.');
      const body = request.postDataJSON();
      draft = {
        ...draft,
        title: body.title,
        start: { localDateTime: normalize(body.start.localDateTime), offset: body.start.offset ?? '+03:00' },
        end: { localDateTime: normalize(body.end.localDateTime), offset: body.end.offset ?? '+03:00' },
        location: body.location,
        notes: body.notes,
        version: 'AAAAAAAAB9I=',
      };
      return json(route, 200, draft);
    }
    return problem(route, 404, 'not_found', 'Not found.');
  });

  return {
    get draft() { return draft; },
    get createCount() { return createCount; },
    get appointmentCount() { return appointmentCount; },
    get appointment() { return appointment; },
    seed(value: MockDraft) { draft = value; },
  };
}

export const seededDraft: MockDraft = {
  id: '20000000-0000-0000-0000-000000000001',
  title: 'Beach cleanup',
  start: { localDateTime: '2026-10-10T14:30:00', offset: '+03:00' },
  end: { localDateTime: '2026-10-10T16:30:00', offset: '+03:00' },
  startDateTimeUtc: '2026-10-10T11:30:00Z',
  endDateTimeUtc: '2026-10-10T13:30:00Z',
  location: 'North shore',
  notes: 'Bring gloves',
  status: 'Draft',
  version: 'AAAAAAAAB9E=',
};

function summary(draft: MockDraft) {
  return { id: draft.id, title: draft.title, start: draft.start, location: draft.location, status: draft.status };
}

function normalize(value: string): string {
  return value.length === 16 ? `${value}:00` : value;
}

function json(route: Route, status: number, body: unknown, headers?: Record<string, string>) {
  return route.fulfill({ status, contentType: 'application/json', headers, body: JSON.stringify(body) });
}

function problem(route: Route, status: number, code: string, title: string) {
  return route.fulfill({
    status,
    contentType: 'application/problem+json',
    body: JSON.stringify({ type: `https://httpstatuses.com/${status}`, title, status, code, traceId: 'e2e-trace' }),
  });
}
