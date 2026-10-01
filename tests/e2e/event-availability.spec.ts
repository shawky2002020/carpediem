import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const targetEventId = '60000000-0000-0000-0000-000000000001';
const staffingCycleId = '70000000-0000-0000-0000-000000000001';

test('Leader declares Available, sees updated badge and live region confirmation', async ({ page }) => {
  const api = await mockAvailabilityApi(page);
  await page.goto(`/events/${targetEventId}/availability`);

  await expect(page.locator('.status-badge')).toContainText('Response Needed');
  const btnAvailable = page.getByRole('button', { name: 'Mark Available' });
  await expect(btnAvailable).toBeEnabled();

  await btnAvailable.click();
  await expect(page.locator('.status-badge')).toContainText('Available');
  await expect(page.locator('.live-region')).toContainText('Availability declared as Available');
  expect(api.postCalls).toBe(1);
  expect(api.lastSubmittedResponse).toBe('Available');
});

test('Leader can toggle declared availability from Available to Not Available', async ({ page }) => {
  const api = await mockAvailabilityApi(page, {
    initialResponse: 'Available',
    initialVersion: 'VER_ONE',
  });
  await page.goto(`/events/${targetEventId}/availability`);

  await expect(page.locator('.status-badge')).toContainText('Available');
  const btnNotAvailable = page.getByRole('button', { name: 'Mark Not Available' });
  await expect(btnNotAvailable).toBeEnabled();

  await btnNotAvailable.click();
  await expect(page.locator('.status-badge')).toContainText('Not Available');
  await expect(page.locator('.live-region')).toContainText('Availability declared as Not Available');
  expect(api.postCalls).toBe(1);
  expect(api.lastSubmittedResponse).toBe('NotAvailable');
  expect(api.lastIfMatchHeader).toBe('"VER_ONE"');
});

test('Dual-role Chief Leader on led event sees notice banner and disabled buttons', async ({ page }) => {
  await mockAvailabilityApi(page, { isChiefLeaderOnEvent: true });
  await page.goto(`/events/${targetEventId}/availability`);

  await expect(page.getByRole('heading', { name: 'Chief Leader Assignment' })).toBeVisible();
  await expect(page.getByText('Chief Leaders cannot declare volunteer availability on events they lead')).toBeVisible();

  await expect(page.getByRole('button', { name: 'Mark Available' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Mark Not Available' })).toBeDisabled();
});

test('When availability window is closed, displays closed warning and disabled controls', async ({ page }) => {
  await mockAvailabilityApi(page, { isWindowOpen: false });
  await page.goto(`/events/${targetEventId}/availability`);

  await expect(page.getByRole('heading', { name: 'Window Closed' })).toBeVisible();
  await expect(page.getByText('The availability window for this event is closed')).toBeVisible();

  await expect(page.getByRole('button', { name: 'Mark Available' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Mark Not Available' })).toBeDisabled();
});

test('Availability declaration interface meets WCAG 2.2 AA, 44px targets, and 200% zoom reflow', async ({ page }) => {
  await mockAvailabilityApi(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/events/${targetEventId}/availability`);

  // Accessibility audit
  const axeResults = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(axeResults.violations).toEqual([]);

  // Minimum 44px touch targets
  const btnAvailable = page.getByRole('button', { name: 'Mark Available' });
  const btnNotAvailable = page.getByRole('button', { name: 'Mark Not Available' });

  const targetA = await btnAvailable.boundingBox();
  expect(targetA!.height).toBeGreaterThanOrEqual(44);
  expect(targetA!.width).toBeGreaterThanOrEqual(44);

  const targetB = await btnNotAvailable.boundingBox();
  expect(targetB!.height).toBeGreaterThanOrEqual(44);
  expect(targetB!.width).toBeGreaterThanOrEqual(44);

  // 200% font zoom reflow (no horizontal scroll overflow > 1px)
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);

  // Focus visibility
  await btnAvailable.focus();
  await expect(page.locator(':focus')).toBeVisible();
});

interface MockAvailabilityOptions {
  initialResponse?: 'Available' | 'NotAvailable' | null;
  initialVersion?: string | null;
  isWindowOpen?: boolean;
  isChiefLeaderOnEvent?: boolean;
}

async function mockAvailabilityApi(page: Page, options: MockAvailabilityOptions = {}) {
  let currentResponse = options.initialResponse ?? null;
  let currentVersion = options.initialVersion ?? null;
  let postCalls = 0;
  let lastSubmittedResponse: string | null = null;
  let lastIfMatchHeader: string | null = null;

  await page.route('**/api/**', async (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;

    if (path === '/api/session/me') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          userId: '50000000-0000-0000-0000-000000000001',
          userName: 'volunteer.leader',
          roles: ['Leader'],
        }),
      });
    }

    if (path === '/api/session/antiforgery') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ requestToken: 'e2e-csrf-token' }),
      });
    }

    if (path === `/api/events/${targetEventId}/availability/me` && request.method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          eventId: targetEventId,
          staffingCycleId,
          response: currentResponse,
          declaredAt: currentResponse ? '2026-10-01T20:00:00Z' : null,
          version: currentVersion,
          isWindowOpen: options.isWindowOpen ?? true,
          eventStatus: 'Published',
          isChiefLeaderOnEvent: options.isChiefLeaderOnEvent ?? false,
        }),
      });
    }

    if (path === `/api/events/${targetEventId}/availability` && request.method() === 'POST') {
      postCalls += 1;
      const body = JSON.parse(request.postData() ?? '{}');
      lastSubmittedResponse = body.response;
      lastIfMatchHeader = request.headers()['if-match'] ?? null;

      currentResponse = body.response;
      currentVersion = 'NEW_VER_123';

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: '80000000-0000-0000-0000-000000000001',
          eventId: targetEventId,
          staffingCycleId,
          userId: '50000000-0000-0000-0000-000000000001',
          response: currentResponse,
          declaredAt: '2026-10-01T20:10:00Z',
          version: currentVersion,
        }),
      });
    }

    return route.continue();
  });

  return {
    get postCalls() { return postCalls; },
    get lastSubmittedResponse() { return lastSubmittedResponse; },
    get lastIfMatchHeader() { return lastIfMatchHeader; },
  };
}
