import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const targetEventId = '70000000-0000-0000-0000-000000000001';

test('Admin views and submits valid event reschedule', async ({ page }) => {
  const api = await mockRescheduleApi(page);
  await page.goto(`/events/${targetEventId}/reschedule`);

  await expect(page.locator('#reschedule-dialog-title')).toBeVisible();
  await expect(page.getByText('Reschedule Event')).toBeVisible();
  await expect(page.locator('.cd-notice--warning')).toBeVisible();

  // Fill in new schedule
  const startInput = page.locator('#newStartDateTime');
  const endInput = page.locator('#newEndDateTime');
  const justificationInput = page.locator('#justification');

  await startInput.fill('2026-11-20T09:00');
  await endInput.fill('2026-11-20T17:00');
  await justificationInput.fill('Venue unavailable on original date due to facility maintenance.');

  const submitBtn = page.getByRole('button', { name: 'Confirm Reschedule' });
  await expect(submitBtn).toBeEnabled();
  await submitBtn.click();

  // Verify success feedback
  await expect(page.locator('#reschedule-success')).toBeVisible();
  await expect(page.locator('.cd-sr-only')).toContainText('Event rescheduled and staffing cycle rotated');

  expect(api.postCalls).toBe(1);
  expect(api.lastPayload?.justification).toBe('Venue unavailable on original date due to facility maintenance.');
});

test('Validates required fields and justification length', async ({ page }) => {
  await mockRescheduleApi(page);
  await page.goto(`/events/${targetEventId}/reschedule`);

  const submitBtn = page.getByRole('button', { name: 'Confirm Reschedule' });
  await submitBtn.click();

  await expect(page.locator('#start-time-error')).toBeVisible();
  await expect(page.locator('#end-time-error')).toBeVisible();
  await expect(page.locator('#justification-error')).toBeVisible();

  // Short justification (< 10 chars)
  const justificationInput = page.locator('#justification');
  await justificationInput.fill('Short');
  await expect(page.locator('#justification-error')).toBeVisible();
});

test('Displays error alert on conflict response', async ({ page }) => {
  await mockRescheduleApi(page, {
    conflictDetail: 'The event cannot be rescheduled because its scheduled start time has already passed.',
  });
  await page.goto(`/events/${targetEventId}/reschedule`);

  const startInput = page.locator('#newStartDateTime');
  const endInput = page.locator('#newEndDateTime');
  const justificationInput = page.locator('#justification');

  await startInput.fill('2026-11-20T09:00');
  await endInput.fill('2026-11-20T17:00');
  await justificationInput.fill('Venue unavailable on original date due to facility maintenance.');

  const submitBtn = page.getByRole('button', { name: 'Confirm Reschedule' });
  await submitBtn.click();

  await expect(page.locator('#reschedule-error')).toBeVisible();
  await expect(page.locator('#reschedule-error')).toContainText('The event cannot be rescheduled because its scheduled start time has already passed.');
});

test('Event reschedule UI meets WCAG 2.2 AA, 44px touch targets, and 200% zoom reflow', async ({ page }) => {
  await mockRescheduleApi(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/events/${targetEventId}/reschedule`);

  // WCAG 2.2 AA accessibility audit
  const axeResults = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(axeResults.violations).toEqual([]);

  // Minimum 44px touch target on submit button
  const submitBtn = page.getByRole('button', { name: 'Confirm Reschedule' });
  const submitBox = await submitBtn.boundingBox();
  expect(submitBox!.height).toBeGreaterThanOrEqual(44);
  expect(submitBox!.width).toBeGreaterThanOrEqual(44);

  // Minimum 44px touch target on cancel button
  const cancelBtn = page.getByRole('button', { name: 'Cancel' });
  const cancelBox = await cancelBtn.boundingBox();
  expect(cancelBox!.height).toBeGreaterThanOrEqual(44);
  expect(cancelBox!.width).toBeGreaterThanOrEqual(44);

  // Minimum 44px touch target on date inputs
  const startInput = page.locator('#newStartDateTime');
  const startBox = await startInput.boundingBox();
  expect(startBox!.height).toBeGreaterThanOrEqual(44);

  // 200% font zoom reflow (no horizontal scroll overflow > 1px)
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);

  // Focus visibility
  await submitBtn.focus();
  await expect(page.locator(':focus')).toBeVisible();
});

interface MockRescheduleApiOptions {
  conflictDetail?: string;
  role?: 'Admin' | 'Leader';
}

async function mockRescheduleApi(page: Page, options: MockRescheduleApiOptions = {}) {
  let postCalls = 0;
  let lastPayload: any = null;

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

    if (path === `/api/events/${targetEventId}/reschedule` && request.method() === 'POST') {
      postCalls++;
      lastPayload = request.postDataJSON();

      if (options.conflictDetail) {
        return route.fulfill({
          status: 409,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10',
            title: 'window_closed',
            status: 409,
            detail: options.conflictDetail,
            instance: `/api/events/${targetEventId}/reschedule`,
          }),
        });
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          eventId: targetEventId,
          title: 'Community Gala',
          status: 'Published',
          startDateTime: lastPayload.newStartDateTime,
          endDateTime: lastPayload.newEndDateTime,
          location: 'Grand Ballroom',
          notes: null,
          activeStaffingCycleId: '80000000-0000-0000-0000-000000000002',
          cycleNumber: 2,
          version: 'new-version==',
          rescheduledAt: '2026-10-02T12:00:00Z',
          notificationsDispatched: 3,
        }),
      });
    }

    return route.fallback();
  });

  return {
    get postCalls() { return postCalls; },
    get lastPayload() { return lastPayload; },
  };
}
