import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const eventId = '20000000-0000-0000-0000-000000000001';
const assignmentId = '40000000-0000-0000-0000-000000000001';

test('Admin records externally confirmed acceptance with a justification', async ({ page }) => {
  const api = await mockExternalAcceptanceApi(page);
  await page.goto(`/draft-events/${eventId}/chief-leader`);
  await expect(page.getByText('chief.one')).toBeVisible();
  await page.getByRole('button', { name: 'Record external acceptance' }).click();
  await expect(page.getByText(/only after chief.one has accepted verbally/)).toBeVisible();
  await page.getByLabel('Justification').fill('Confirmed by phone');
  await page.getByRole('button', { name: 'Record external acceptance' }).click();
  await expect(page.getByRole('status')).toContainText('External acceptance recorded');
  await expect(page.getByText('Active', { exact: true })).toBeVisible();
  await expect(page.getByText('This Event remains in Draft')).toBeVisible();
  expect(api.records).toBe(1);
  expect(api.lastReason).toBe('Confirmed by phone');
});

test('invalid justification stays inline and cancellation creates no record', async ({ page }) => {
  const api = await mockExternalAcceptanceApi(page);
  await page.goto(`/draft-events/${eventId}/chief-leader`);
  await page.getByRole('button', { name: 'Record external acceptance' }).click();
  await page.getByRole('button', { name: 'Record external acceptance' }).click();
  await expect(page.getByRole('alert')).toContainText('Enter why this external confirmation');
  await page.getByLabel('Justification').fill('Changed my mind');
  await page.getByRole('button', { name: 'Cancel' }).click();
  await expect(page.getByLabel('Justification')).toHaveCount(0);
  await expect(page.getByText('Pending', { exact: true })).toBeVisible();
  expect(api.records).toBe(0);
});

test('uncertain submission preserves evidence and reloads the committed state', async ({ page }) => {
  const api = await mockExternalAcceptanceApi(page, { uncertainOnce: true });
  await page.goto(`/draft-events/${eventId}/chief-leader`);
  await page.getByRole('button', { name: 'Record external acceptance' }).click();
  await page.getByLabel('Justification').fill('Confirmed by phone');
  await page.getByRole('button', { name: 'Record external acceptance' }).click();
  await expect(page.getByText('Check the current state')).toBeVisible();
  await expect(page.getByLabel('Justification')).toHaveValue('Confirmed by phone');
  await page.getByRole('button', { name: 'Check current state' }).click();
  await expect(page.getByText('Active', { exact: true })).toBeVisible();
  expect(api.records).toBe(1);
});

test('ordinary Leader cannot open the Admin external acceptance flow', async ({ page }) => {
  await mockExternalAcceptanceApi(page, { role: 'Leader' });
  await page.goto(`/draft-events/${eventId}/chief-leader`);
  await expect(page).toHaveURL(/access-denied/);
  await expect(page.getByText('Record external acceptance')).toHaveCount(0);
});

test('external acceptance is accessible and reflows on the smallest mobile viewport', async ({ page }) => {
  await mockExternalAcceptanceApi(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/draft-events/${eventId}/chief-leader`);
  await page.getByRole('button', { name: 'Record external acceptance' }).click();
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(results.violations).toEqual([]);
  await page.getByLabel('Justification').fill('Confirmed by phone');
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);
  const submit = page.getByRole('button', { name: 'Record external acceptance' });
  const target = await submit.boundingBox();
  expect(target!.height).toBeGreaterThanOrEqual(44);
  await submit.focus();
  await expect(page.locator(':focus')).toBeVisible();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('status')).toContainText('External acceptance recorded');
});

async function mockExternalAcceptanceApi(page: Page,
  options: { role?: 'Admin' | 'Leader'; uncertainOnce?: boolean } = {}) {
  let records = 0;
  let lastReason = '';
  let status: 'Pending' | 'Active' = 'Pending';
  let version = 'AQIDBAUGBwg=';
  const candidate = { userId: '30000000-0000-0000-0000-000000000001', userName: 'chief.one' };
  const appointment = () => ({
    id: assignmentId, eventId, candidate, status,
    appointedAt: '2026-09-22T09:00:00Z',
    appointedByUserId: '10000000-0000-0000-0000-000000000001',
    acceptedAt: status === 'Active' ? '2026-09-23T07:30:00Z' : null,
    acceptanceSource: status === 'Active' ? 'AdminExternal' : null,
    acceptedByUserId: status === 'Active' ? '10000000-0000-0000-0000-000000000001' : null,
    version,
  });

  await page.route('**/api/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === '/api/session/me') return json(route, 200, {
      userId: '10000000-0000-0000-0000-000000000001', userName: 'admin',
      roles: [options.role ?? 'Admin'],
    });
    if (path === '/api/session/antiforgery')
      return json(route, 200, { requestToken: 'test-token' });
    if (path === `/api/events/drafts/${eventId}/chief-leader-appointment` &&
        request.method() === 'GET') {
      return json(route, 200, {
        eventId, eventTitle: 'Beach cleanup', eventStatus: 'Draft',
        currentAppointment: appointment(), declinedHistory: [], candidates: [],
      });
    }
    if (path === `/api/events/drafts/${eventId}/chief-leader-appointment/${assignmentId}/external-acceptance` &&
        request.method() === 'POST') {
      records++;
      lastReason = request.postDataJSON().justification;
      status = 'Active';
      version = 'AgIDBAUGBwg=';
      if (options.uncertainOnce && records === 1) return route.abort('connectionfailed');
      return json(route, 200, appointment());
    }
    return json(route, 404, { title: 'Not found', code: 'not_found', status: 404 });
  });

  return {
    get records() { return records; },
    get lastReason() { return lastReason; },
  };
}

function json(route: Route, status: number, body: unknown) {
  return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
}
