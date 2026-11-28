import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const targetEventId = '80000000-0000-0000-0000-000000000001';

test('Chief Leader views roll sheet and submits attendance outcomes', async ({ page }) => {
  const api = await mockAttendanceApi(page);
  await page.goto(`/events/${targetEventId}/attendance`);

  await expect(page.locator('#attendance-page-title')).toBeVisible();
  await expect(page.getByText('On-Site Attendance Roll Call')).toBeVisible();
  await expect(page.getByText('Community Gala 2026')).toBeVisible();
  await expect(page.getByTestId('stat-total')).toHaveText('2');
  await expect(page.getByTestId('stat-unrecorded')).toHaveText('2');

  // Toggle volunteer 1 as Attended
  const vol1AttendedBtn = page.getByRole('button', { name: 'Mark Alice Smith as attended' });
  await vol1AttendedBtn.click();
  await expect(vol1AttendedBtn).toHaveClass(/active/);
  await expect(page.getByTestId('stat-attended')).toHaveText('1');
  await expect(page.getByTestId('stat-unrecorded')).toHaveText('1');

  // Toggle volunteer 2 as No Show
  const vol2NoShowBtn = page.getByRole('button', { name: 'Mark Bob Jones as no show' });
  await vol2NoShowBtn.click();
  await expect(vol2NoShowBtn).toHaveClass(/active/);
  await expect(page.getByTestId('stat-noshow')).toHaveText('1');
  await expect(page.getByTestId('stat-unrecorded')).toHaveText('0');

  // Finalize attendance
  const submitBtn = page.getByRole('button', { name: 'Finalize attendance roll call' });
  await expect(submitBtn).toBeEnabled();
  await submitBtn.click();

  // Verify success alert
  await expect(page.locator('.cd-alert--success')).toBeVisible();
  await expect(page.locator('.cd-alert--success')).toContainText('Attendance finalized successfully!');

  expect(api.postCalls).toBe(1);
});

test('Bulk action marks all unrecorded volunteers', async ({ page }) => {
  await mockAttendanceApi(page);
  await page.goto(`/events/${targetEventId}/attendance`);

  const markAllBtn = page.getByRole('button', { name: 'Mark remaining as attended' });
  await markAllBtn.click();

  await expect(page.getByTestId('stat-attended')).toHaveText('2');
  await expect(page.getByTestId('stat-unrecorded')).toHaveText('0');

  const vol1Btn = page.getByRole('button', { name: 'Mark Alice Smith as attended' });
  const vol2Btn = page.getByRole('button', { name: 'Mark Bob Jones as attended' });
  await expect(vol1Btn).toHaveClass(/active/);
  await expect(vol2Btn).toHaveClass(/active/);
});

test('Displays error alert when API returns 409 conflict', async ({ page }) => {
  await mockAttendanceApi(page, {
    conflictDetail: 'Attendance operations are permitted only on Published or Completed events.',
    status: 409,
  });
  await page.goto(`/events/${targetEventId}/attendance`);

  const markAllBtn = page.getByRole('button', { name: 'Mark remaining as attended' });
  await markAllBtn.click();

  const submitBtn = page.getByRole('button', { name: 'Finalize attendance roll call' });
  await submitBtn.click();

  await expect(page.locator('.cd-alert--error')).toBeVisible();
  await expect(page.locator('.cd-alert--error')).toContainText('Attendance operations are permitted only on Published or Completed events.');
});

test('Attendance roll sheet meets WCAG 2.2 AA, 44px touch targets, and 200% zoom reflow', async ({ page }) => {
  await mockAttendanceApi(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/events/${targetEventId}/attendance`);

  // WCAG 2.2 AA accessibility audit
  const axeResults = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(axeResults.violations).toEqual([]);

  // Minimum 44px touch target on Mark All button
  const markAllBtn = page.getByRole('button', { name: 'Mark remaining as attended' });
  const markAllBox = await markAllBtn.boundingBox();
  expect(markAllBox!.height).toBeGreaterThanOrEqual(44);
  expect(markAllBox!.width).toBeGreaterThanOrEqual(44);

  // Minimum 44px touch target on toggle buttons
  const vol1AttendedBtn = page.getByRole('button', { name: 'Mark Alice Smith as attended' });
  const vol1Box = await vol1AttendedBtn.boundingBox();
  expect(vol1Box!.height).toBeGreaterThanOrEqual(44);
  expect(vol1Box!.width).toBeGreaterThanOrEqual(44);

  // 200% font zoom reflow (no horizontal scroll overflow > 1px)
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });

  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);

  // Focus visibility
  await markAllBtn.focus();
  await expect(page.locator(':focus')).toBeVisible();
});

test('recovers an unsaved no show after refresh and returning from navigation', async ({ page }) => {
  const api = await mockAttendanceApi(page);
  await page.goto(`/events/${targetEventId}/attendance`);
  const noShow = page.getByRole('button', { name: 'Mark Alice Smith as no show' });
  await noShow.click();

  await page.reload();
  await expect(noShow).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByText('Recovered 1 unsaved entry in this tab.')).toBeVisible();

  await page.getByRole('link', { name: 'Dashboard' }).first().click();
  await expect(page).toHaveURL(/dashboard$/);
  await page.goBack();
  await expect(noShow).toHaveAttribute('aria-pressed', 'true');
  expect(api.postCalls).toBe(0);
});

test('bulk marks only unrecorded rows and cannot overwrite an explicit exception', async ({ page }) => {
  await mockAttendanceApi(page);
  await page.goto(`/events/${targetEventId}/attendance`);
  const noShow = page.getByRole('button', { name: 'Mark Alice Smith as no show' });
  await noShow.click();
  const bulk = page.getByRole('button', { name: 'Mark remaining as attended' });
  await bulk.click();
  await expect(noShow).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId('stat-attended')).toHaveText('1');
  await expect(page.getByTestId('stat-noshow')).toHaveText('1');
  await expect(bulk).toBeDisabled();
});

test('failed submit keeps the draft and successful retry clears it', async ({ page }) => {
  const api = await mockAttendanceApi(page, { failOnce: true });
  await page.goto(`/events/${targetEventId}/attendance`);
  const noShow = page.getByRole('button', { name: 'Mark Alice Smith as no show' });
  const finalize = page.getByRole('button', { name: 'Finalize attendance roll call' });
  await noShow.click();
  await finalize.click();
  await expect(page.getByRole('alert')).toContainText('Server unavailable');
  await page.reload();
  await expect(noShow).toHaveAttribute('aria-pressed', 'true');

  await finalize.click();
  await expect(page.getByText('Attendance finalized successfully!')).toBeVisible();
  await page.reload();
  await expect(page.getByTestId('stat-noshow')).toHaveText('1');
  await expect(noShow).toHaveCount(0);
  await expect(finalize).toBeDisabled();
  expect(api.postCalls).toBe(2);
});

test('reload after another operator finalizes one row retains only remaining work', async ({ page }) => {
  const api = await mockAttendanceApi(page);
  await page.goto(`/events/${targetEventId}/attendance`);
  await page.getByRole('button', { name: 'Mark Alice Smith as no show' }).click();
  await page.getByRole('button', { name: 'Mark Bob Jones as attended' }).click();
  api.record('30000000-0000-0000-0000-000000000001', 'Attended');

  await page.reload();
  await expect(page.getByRole('button', { name: 'Mark Alice Smith as no show' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Mark Bob Jones as attended' })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByText(/one or more draft entries were already recorded/i)).toBeVisible();
});

test('expired sign-in keeps the draft and offers reauthentication', async ({ page }) => {
  const api = await mockAttendanceApi(page, { failOnce: true, failedStatus: 401 });
  await page.goto(`/events/${targetEventId}/attendance`);
  const noShow = page.getByRole('button', { name: 'Mark Alice Smith as no show' });
  await noShow.click();
  await page.getByRole('button', { name: 'Finalize attendance roll call' }).click();
  await expect(page.getByRole('link', { name: 'Sign in again' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Finalize attendance roll call' })).toBeDisabled();

  // The next authenticated GET models the same operator signing in again.
  await page.reload();
  await expect(noShow).toHaveAttribute('aria-pressed', 'true');
  expect(api.postCalls).toBe(1);
});

test('a different signed-in operator never sees the original draft', async ({ page }) => {
  const api = await mockAttendanceApi(page);
  await page.goto(`/events/${targetEventId}/attendance`);
  const noShow = page.getByRole('button', { name: 'Mark Alice Smith as no show' });
  await noShow.click();

  api.signInAs('10000000-0000-0000-0000-000000000099');
  await page.reload();
  await expect(noShow).toHaveAttribute('aria-pressed', 'false');
  await expect(page.getByText(/unsaved entry in this tab/i)).toHaveCount(0);
});

interface MockAttendanceApiOptions {
  conflictDetail?: string;
  status?: number;
  failOnce?: boolean;
  failedStatus?: number;
}

async function mockAttendanceApi(page: Page, options: MockAttendanceApiOptions = {}) {
  let postCalls = 0;
  let userId = '10000000-0000-0000-0000-000000000001';
  const recorded = new Map<string, string>();
  const outcomeFor = (ordinal: number) => recorded.get('30000000-0000-0000-0000-00000000000' + ordinal) ?? null;
  const finalizedAt = (ordinal: number) => outcomeFor(ordinal) ? '2026-10-15T12:00:00Z' : null;

  await page.route('**/api/**', async (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;

    if (path === '/api/session/me') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          userId,
          userName: 'chief_leader_user',
          roles: ['ChiefLeader'],
          isActive: true,
        }),
      });
    }

    if (path === '/api/session/antiforgery') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ requestToken: 'test-xsrf-token' }),
      });
    }

    if (path === `/api/events/${targetEventId}/attendance` && request.method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          eventId: targetEventId,
          eventTitle: 'Community Gala 2026',
          status: 'Published',
          startDateTime: '2026-10-15T08:00:00Z',
          endDateTime: '2026-10-15T16:00:00Z',
          location: 'Grand Ballroom',
          staffingCycleId: '20000000-0000-0000-0000-000000000001',
          isCompleted: false,
          isAllFinalized: recorded.size === 2,
          entries: [
            {
              teamAssignmentId: '30000000-0000-0000-0000-000000000001',
              volunteerUserId: '40000000-0000-0000-0000-000000000001',
              volunteerName: 'Alice Smith',
              outcome: outcomeFor(1),
              finalizedAt: finalizedAt(1),
              version: null,
            },
            {
              teamAssignmentId: '30000000-0000-0000-0000-000000000002',
              volunteerUserId: '40000000-0000-0000-0000-000000000002',
              volunteerName: 'Bob Jones',
              outcome: outcomeFor(2),
              finalizedAt: finalizedAt(2),
              version: null,
            },
          ],
        }),
      });
    }

    if (path === `/api/events/${targetEventId}/attendance` && request.method() === 'POST') {
      postCalls++;
      if (options.failOnce && postCalls === 1) {
        return route.fulfill({ status: options.failedStatus ?? 500, json: { title: 'Server unavailable', detail: 'Server unavailable' } });
      }

      if (options.conflictDetail) {
        return route.fulfill({
          status: options.status ?? 409,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10',
            title: 'invalid_event_status',
            status: options.status ?? 409,
            detail: options.conflictDetail,
            instance: `/api/events/${targetEventId}/attendance`,
          }),
        });
      }

      for (const item of request.postDataJSON().entries) recorded.set(item.teamAssignmentId, item.outcome);
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          eventId: targetEventId,
          finalizedAt: '2026-10-15T12:00:00Z',
          totalActiveAssignments: 2,
          attendedCount: 1,
          noShowCount: 1,
          unrecordedCount: 0,
        }),
      });
    }

    return route.fallback();
  });

  return {
    get postCalls() { return postCalls; },
    signInAs(id: string) { userId = id; },
    record(id: string, outcome: string) { recorded.set(id, outcome); },
  };
}
