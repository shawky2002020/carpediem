import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const targetEventId = '70000000-0000-0000-0000-000000000001';

test('Admin views and submits valid event closeout', async ({ page }) => {
  const api = await mockCompletionApi(page);
  await page.goto(`/events/${targetEventId}/complete`);

  await expect(page.locator('#completion-dialog-title')).toBeVisible();
  await expect(page.getByText('Complete Event')).toBeVisible();
  await expect(page.locator('.cd-notice--info')).toBeVisible();
  await expect(page.getByText('Active team assignments will remain active')).toBeVisible();

  const submitBtn = page.getByRole('button', { name: 'Confirm Closeout' });
  await expect(submitBtn).toBeEnabled();
  await submitBtn.click();

  // Verify success feedback
  await expect(page.locator('#completion-success')).toBeVisible();
  await expect(page.locator('.cd-sr-only')).toContainText('Event marked as completed');

  expect(api.postCalls).toBe(1);
  expect(api.lastIfMatch).toBe('"test-version=="');
});

test('Displays error alert on conflict or concurrency response', async ({ page }) => {
  await mockCompletionApi(page, {
    conflictDetail: 'The event was modified concurrently. Please refresh.',
    status: 412,
  });
  await page.goto(`/events/${targetEventId}/complete`);

  const submitBtn = page.getByRole('button', { name: 'Confirm Closeout' });
  await submitBtn.click();

  await expect(page.locator('#completion-error')).toBeVisible();
  await expect(page.locator('#completion-error')).toContainText('The event was modified concurrently. Please refresh.');
});

test('Displays error alert when event is not published (409 Conflict)', async ({ page }) => {
  await mockCompletionApi(page, {
    conflictDetail: 'Event is in Draft status and cannot be completed.',
    status: 409,
  });
  await page.goto(`/events/${targetEventId}/complete`);

  const submitBtn = page.getByRole('button', { name: 'Confirm Closeout' });
  await submitBtn.click();

  await expect(page.locator('#completion-error')).toBeVisible();
  await expect(page.locator('#completion-error')).toContainText('Event is in Draft status and cannot be completed.');
});

test('Event completion UI meets WCAG 2.2 AA, 44px touch targets, and 200% zoom reflow', async ({ page }) => {
  await mockCompletionApi(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/events/${targetEventId}/complete`);

  // WCAG 2.2 AA accessibility audit
  const axeResults = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(axeResults.violations).toEqual([]);

  // Minimum 44px touch target on submit button
  const submitBtn = page.getByRole('button', { name: 'Confirm Closeout' });
  const submitBox = await submitBtn.boundingBox();
  expect(submitBox!.height).toBeGreaterThanOrEqual(44);
  expect(submitBox!.width).toBeGreaterThanOrEqual(44);

  // Minimum 44px touch target on cancel button
  const cancelBtn = page.getByRole('button', { name: 'Cancel' });
  const cancelBox = await cancelBtn.boundingBox();
  expect(cancelBox!.height).toBeGreaterThanOrEqual(44);
  expect(cancelBox!.width).toBeGreaterThanOrEqual(44);

  // 200% font zoom reflow (no horizontal scroll overflow > 1px)
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);

  // Focus visibility
  await submitBtn.focus();
  await expect(page.locator(':focus')).toBeVisible();
});

interface MockCompletionApiOptions {
  conflictDetail?: string;
  status?: number;
  role?: 'Admin' | 'Leader';
}

async function mockCompletionApi(page: Page, options: MockCompletionApiOptions = {}) {
  let postCalls = 0;
  let lastIfMatch: string | null = null;

  await page.addInitScript(() => {
    Object.defineProperty(globalThis.history, 'state', {
      value: {
        eventTitle: 'Community Gala',
        version: 'test-version==',
        end: '2026-10-01 17:00 UTC',
      },
      writable: true,
      configurable: true,
    });
  });

  await page.route('**/api/**', async (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;

    if (path === '/api/session/me') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          userId: '10000000-0000-0000-0000-000000000001',
          userName: 'admin_user',
          roles: [options.role ?? 'Admin'],
          isActive: true,
        }),
      });
    }

    if (path === '/api/session/antiforgery') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ requestToken: 'test-token' }),
      });
    }

    if (path === `/api/events/${targetEventId}/complete` && request.method() === 'POST') {
      postCalls++;
      lastIfMatch = request.headers()['if-match'] ?? null;

      if (options.conflictDetail) {
        return route.fulfill({
          status: options.status ?? 409,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10',
            title: options.status === 412 ? 'concurrency_conflict' : 'invalid_event_status',
            status: options.status ?? 409,
            detail: options.conflictDetail,
            instance: `/api/events/${targetEventId}/complete`,
          }),
        });
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: {
          'ETag': '"completed-version=="',
        },
        body: JSON.stringify({
          eventId: targetEventId,
          title: 'Community Gala',
          status: 'Completed',
          startDateTime: '2026-10-01T09:00:00Z',
          endDateTime: '2026-10-01T17:00:00Z',
          location: 'Grand Ballroom',
          notes: null,
          completedAt: '2026-10-02T12:00:00Z',
          version: 'completed-version==',
        }),
      });
    }

    return route.fallback();
  });

  return {
    get postCalls() { return postCalls; },
    get lastIfMatch() { return lastIfMatch; },
  };
}
