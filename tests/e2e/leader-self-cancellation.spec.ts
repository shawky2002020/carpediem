import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const targetEventId = '80000000-0000-0000-0000-000000000001';
const targetAssignmentId = '90000000-0000-0000-0000-000000000001';
const currentUserId = '50000000-0000-0000-0000-000000000001';

test('Volunteer views active assignment and event summary', async ({ page }) => {
  const api = await mockSelfCancellationApi(page);
  await page.goto(`/events/${targetEventId}/my-assignment`);

  await expect(page.locator('#assignment-title')).toBeVisible();
  await expect(page.locator('.event-title')).toContainText('Pyramids Heritage Run 2026');
  await expect(page.locator('.event-meta')).toContainText('North Shore Park');

  const statusBadge = page.locator('.status-badge-active');
  await expect(statusBadge).toContainText('Active');

  const cancelBtn = page.getByRole('button', { name: 'Cancel Assignment' });
  await expect(cancelBtn).toBeVisible();
  await expect(cancelBtn).toBeEnabled();

  expect(api.getCalls).toBe(1);
});

test('Volunteer opens cancellation modal, verifies character counter, and dismisses without cancelling', async ({ page }) => {
  const api = await mockSelfCancellationApi(page);
  await page.goto(`/events/${targetEventId}/my-assignment`);

  const cancelBtn = page.getByRole('button', { name: 'Cancel Assignment' });
  await cancelBtn.click();

  const modalDialog = page.getByRole('dialog');
  await expect(modalDialog).toBeVisible();
  await expect(page.locator('#cancel-modal-title')).toHaveText('Cancel Event Assignment');

  const confirmBtn = page.getByRole('button', { name: 'Confirm Cancellation' });
  await expect(confirmBtn).toBeDisabled();

  const charCounter = page.locator('#char-count');
  await expect(charCounter).toContainText('0 / 500 characters');

  const reasonInput = page.locator('#cancellation-reason');
  await reasonInput.fill('Academic exam conflict on Saturday morning.');
  await expect(charCounter).toContainText('43 / 500 characters');
  await expect(confirmBtn).toBeEnabled();

  const keepBtn = page.getByRole('button', { name: 'Keep Assignment' });
  await keepBtn.click();

  await expect(modalDialog).toHaveCount(0);
  await expect(page.locator('.status-badge-active')).toContainText('Active');
  expect(api.postCalls).toBe(0);
});

test('Volunteer successfully cancels assignment with mandatory justification reason', async ({ page }) => {
  const api = await mockSelfCancellationApi(page);
  await page.goto(`/events/${targetEventId}/my-assignment`);

  await page.getByRole('button', { name: 'Cancel Assignment' }).click();

  const reasonInput = page.locator('#cancellation-reason');
  const justificationText = 'Emergency medical appointment scheduled at hospital.';
  await reasonInput.fill(justificationText);

  const confirmBtn = page.getByRole('button', { name: 'Confirm Cancellation' });
  await confirmBtn.click();

  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.locator('.status-badge-cancelled')).toContainText('Cancelled');
  await expect(page.locator('.reason-text')).toContainText(justificationText);
  await expect(page.locator('.live-region')).toContainText('Assignment successfully cancelled');
  await expect(page.getByRole('button', { name: 'Cancel Assignment' })).toHaveCount(0);

  expect(api.postCalls).toBe(1);
  expect(api.lastPayload?.justification).toBe(justificationText);
  expect(api.lastIfMatch).toBe('"AAAAAAAAAAA="');
});

test('Displays warning notice and hides cancellation trigger when cancellation window is closed', async ({ page }) => {
  await mockSelfCancellationApi(page, { isWindowOpen: false });
  await page.goto(`/events/${targetEventId}/my-assignment`);

  await expect(page.getByRole('heading', { name: 'Cancellation Window Closed' })).toBeVisible();
  await expect(page.getByText('This event has already started. Self-cancellation is no longer available.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Cancel Assignment' })).toHaveCount(0);
});

test('Displays error alert when cancellation POST returns 409 conflict', async ({ page }) => {
  await mockSelfCancellationApi(page, {
    conflictStatus: 409,
    conflictCode: 'window_closed',
    conflictTitle: 'Cancellation Window Closed',
    conflictDetail: 'The cancellation window for this event has elapsed.',
  });
  await page.goto(`/events/${targetEventId}/my-assignment`);

  await page.getByRole('button', { name: 'Cancel Assignment' }).click();
  await page.locator('#cancellation-reason').fill('Car breakdown');
  await page.getByRole('button', { name: 'Confirm Cancellation' }).click();

  await expect(page.locator('.alert-box.error')).toBeVisible();
  await expect(page.getByText('Cancellation is closed because the event has already started.')).toBeVisible();
  await expect(page.locator('.status-badge-active')).toBeVisible();
});

test('Self-cancellation interface meets WCAG 2.2 AA, 44px touch targets, and 200% zoom reflow', async ({ page }) => {
  await mockSelfCancellationApi(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/events/${targetEventId}/my-assignment`);

  // Main page WCAG 2.2 AA accessibility audit
  const axeResultsPage = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(axeResultsPage.violations).toEqual([]);

  // Minimum 44px touch target on initial action button
  const cancelBtn = page.getByRole('button', { name: 'Cancel Assignment' });
  const initialBox = await cancelBtn.boundingBox();
  expect(initialBox!.height).toBeGreaterThanOrEqual(44);
  expect(initialBox!.width).toBeGreaterThanOrEqual(44);

  // Open modal and audit dialog accessibility
  await cancelBtn.click();
  const axeResultsModal = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(axeResultsModal.violations).toEqual([]);

  // Check modal touch targets
  const keepBtn = page.getByRole('button', { name: 'Keep Assignment' });
  const keepBox = await keepBtn.boundingBox();
  expect(keepBox!.height).toBeGreaterThanOrEqual(44);
  expect(keepBox!.width).toBeGreaterThanOrEqual(44);

  const confirmBtn = page.getByRole('button', { name: 'Confirm Cancellation' });
  const confirmBox = await confirmBtn.boundingBox();
  expect(confirmBox!.height).toBeGreaterThanOrEqual(44);
  expect(confirmBox!.width).toBeGreaterThanOrEqual(44);

  // 200% font zoom reflow (no horizontal scroll overflow > 1px)
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);

  // Focus visibility
  await keepBtn.focus();
  await expect(page.locator(':focus')).toBeVisible();
});

interface MockApiOptions {
  isWindowOpen?: boolean;
  conflictStatus?: number;
  conflictCode?: string;
  conflictTitle?: string;
  conflictDetail?: string;
}

async function mockSelfCancellationApi(page: Page, options: MockApiOptions = {}) {
  let getCalls = 0;
  let postCalls = 0;
  let lastPayload: any = null;
  let lastIfMatch: string | null = null;
  let currentStatus = 'Active';
  let cancellationReason: string | null = null;
  let cancelledAt: string | null = null;
  const initialVersion = 'AAAAAAAAAAA=';
  const updatedVersion = 'AQAAAAAAAAA=';

  await page.route('**/api/**', async (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;

    if (path === '/api/session/me') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          userId: currentUserId,
          userName: 'volunteer.leader',
          roles: ['Leader'],
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

    if (path === `/api/events/${targetEventId}/my-assignment` && request.method() === 'GET') {
      getCalls++;
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          assignmentId: targetAssignmentId,
          eventId: targetEventId,
          eventTitle: 'Pyramids Heritage Run 2026',
          startDateTime: '2026-10-15T09:00:00Z',
          endDateTime: '2026-10-15T13:00:00Z',
          location: 'North Shore Park',
          status: currentStatus,
          assignedAt: '2026-10-01T10:00:00Z',
          cancelledAt,
          cancellationReason,
          isWindowOpen: options.isWindowOpen ?? true,
          version: currentStatus === 'Active' ? initialVersion : updatedVersion,
        }),
      });
    }

    if (path === `/api/events/${targetEventId}/assignments/${targetAssignmentId}/cancel` && request.method() === 'POST') {
      postCalls++;
      lastIfMatch = request.headers()['if-match'] || null;
      lastPayload = request.postDataJSON();

      if (options.conflictStatus) {
        return route.fulfill({
          status: options.conflictStatus,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            status: options.conflictStatus,
            title: options.conflictTitle || 'Conflict',
            detail: options.conflictDetail || 'Conflict occurred.',
            code: options.conflictCode || 'conflict',
          }),
        });
      }

      currentStatus = 'Cancelled';
      cancellationReason = lastPayload?.justification;
      cancelledAt = '2026-10-02T08:00:00Z';

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          assignmentId: targetAssignmentId,
          eventId: targetEventId,
          status: 'Cancelled',
          cancelledAt,
          cancellationReason,
          version: updatedVersion,
        }),
      });
    }

    return route.continue();
  });

  return {
    get getCalls() { return getCalls; },
    get postCalls() { return postCalls; },
    get lastPayload() { return lastPayload; },
    get lastIfMatch() { return lastIfMatch; },
  };
}
