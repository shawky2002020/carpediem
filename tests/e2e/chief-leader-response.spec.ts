import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const assignmentId = '40000000-0000-0000-0000-000000000001';
const eventId = '20000000-0000-0000-0000-000000000001';
const pending = {
  id: assignmentId, eventId, eventTitle: 'Beach cleanup',
  startDateTime: '2026-10-10T11:30:00Z', endDateTime: '2026-10-10T13:30:00Z',
  location: 'North shore', status: 'Pending', appointedAt: '2026-09-22T09:00:00Z',
  acceptedAt: null, declinedAt: null, version: 'AQIDBAUGBwg=',
};

test('candidate finds and accepts their Pending appointment', async ({ page }) => {
  const api = await mockResponseApi(page);
  await page.goto('/my-appointments');
  await expect(page.getByRole('heading', { name: 'My pending appointments' })).toBeVisible();
  await page.getByRole('link', { name: /Beach cleanup/ }).click();
  await expect(page.getByText('North shore')).toBeVisible();
  await page.getByRole('button', { name: 'Accept appointment' }).click();
  await expect(page.getByRole('status')).toContainText('Appointment accepted');
  await expect(page.getByText('Active', { exact: true })).toBeVisible();
  expect(api.accepts).toBe(1);
  expect(api.declines).toBe(0);
  await page.reload();
  await expect(page.getByText('Active', { exact: true })).toBeVisible();
});

test('candidate declines and cannot send an opposite response', async ({ page }) => {
  const api = await mockResponseApi(page);
  await page.goto(`/my-appointments/${assignmentId}`);
  await page.getByRole('button', { name: 'Decline appointment' }).click();
  await expect(page.getByRole('status')).toContainText('Appointment declined');
  await expect(page.getByText('Declined', { exact: true })).toBeVisible();
  expect(api.declines).toBe(1);
  await expect(page.getByRole('button', { name: 'Accept appointment' })).toHaveCount(0);
});

test('uncertain response preserves Event context and reloads committed outcome', async ({ page }) => {
  const api = await mockResponseApi(page, { uncertainOnce: true });
  await page.goto(`/my-appointments/${assignmentId}`);
  await page.getByRole('button', { name: 'Accept appointment' }).click();
  await expect(page.getByText('Check the current state')).toBeVisible();
  await expect(page.getByText('Beach cleanup')).toBeVisible();
  await page.getByRole('button', { name: 'Check current state' }).click();
  await expect(page.getByText('Active', { exact: true })).toBeVisible();
  expect(api.accepts).toBe(1);
});

test('ordinary Leader cannot open the candidate route', async ({ page }) => {
  await mockResponseApi(page, { role: 'Leader' });
  await page.goto('/my-appointments');
  await expect(page).toHaveURL(/access-denied/);
  await expect(page.getByText('Beach cleanup')).toHaveCount(0);
});

test('Admin reviews declined history and can choose a replacement candidate', async ({ page }) => {
  await mockResponseApi(page, { role: 'Admin' });
  await page.goto(`/draft-events/${eventId}/chief-leader`);

  await expect(page.getByRole('heading', { name: 'Previous responses' })).toBeVisible();
  const history = page.locator('.declined-history');
  await expect(history).toContainText('chief.one');
  await expect(history).toContainText('Declined');
  await expect(page.getByRole('radio', { name: 'chief.two' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Appoint Chief Leader' })).toBeVisible();
});

test('mobile response is accessible and reflows at 200 percent text', async ({ page }) => {
  await mockResponseApi(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/my-appointments/${assignmentId}`);
  await expect(page.getByRole('button', { name: 'Accept appointment' })).toBeVisible();
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(results.violations).toEqual([]);
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);
  const target = await page.getByRole('button', { name: 'Accept appointment' }).boundingBox();
  expect(target!.height).toBeGreaterThanOrEqual(44);
  await page.getByRole('button', { name: 'Accept appointment' }).focus();
  await expect(page.locator(':focus')).toBeVisible();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('status')).toContainText('Appointment accepted');
});

async function mockResponseApi(page: Page,
  options: { role?: 'Admin' | 'ChiefLeader' | 'Leader'; uncertainOnce?: boolean } = {}) {
  let appointment = { ...pending, status: 'Pending' as string,
    acceptedAt: null as string | null, declinedAt: null as string | null };
  let accepts = 0;
  let declines = 0;
  await page.route('**/api/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === '/api/session/me') return json(route, 200,
      { userId: '30000000-0000-0000-0000-000000000001', userName: options.role === 'Admin' ? 'admin' : 'candidate',
        roles: [options.role ?? 'ChiefLeader'] });
    if (path === '/api/session/antiforgery')
      return json(route, 200, { requestToken: 'test-token' });
    if (path === '/api/chief-leader/appointments' && request.method() === 'GET')
      return json(route, 200, appointment.status === 'Pending' ? [appointment] : []);
    if (path === `/api/chief-leader/appointments/${assignmentId}` && request.method() === 'GET')
      return json(route, 200, appointment);
    if (path === `/api/chief-leader/appointments/${assignmentId}/accept` &&
        request.method() === 'POST') {
      accepts++;
      appointment = { ...appointment, status: 'Active', acceptedAt: '2026-09-22T10:00:00Z' };
      if (options.uncertainOnce && accepts === 1) return route.abort('connectionfailed');
      return json(route, 200, appointment);
    }
    if (path === `/api/chief-leader/appointments/${assignmentId}/decline` &&
        request.method() === 'POST') {
      declines++;
      appointment = { ...appointment, status: 'Declined', declinedAt: '2026-09-22T10:00:00Z' };
      return json(route, 200, appointment);
    }
    if (path === `/api/events/drafts/${eventId}/chief-leader-appointment` &&
        request.method() === 'GET') {
      return json(route, 200, {
        eventId,
        eventTitle: 'Beach cleanup',
        eventStatus: 'Draft',
        currentAppointment: null,
        declinedHistory: [{
          id: assignmentId,
          eventId,
          candidate: { userId: '30000000-0000-0000-0000-000000000001', userName: 'chief.one' },
          status: 'Declined',
          appointedAt: '2026-09-22T09:00:00Z',
          appointedByUserId: '10000000-0000-0000-0000-000000000001',
          version: 'AQIDBAUGBwg=',
        }],
        candidates: [{ userId: '30000000-0000-0000-0000-000000000002', userName: 'chief.two' }],
      });
    }
    return json(route, 404, { title: 'Not found', code: 'not_found', status: 404 });
  });
  return {
    get accepts() { return accepts; },
    get declines() { return declines; },
  };
}

function json(route: Route, status: number, body: unknown) {
  return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}
