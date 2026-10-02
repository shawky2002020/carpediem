import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const targetEventId = '80000000-0000-0000-0000-000000000001';
const targetOutcomeId = '90000000-0000-0000-0000-000000000001';

test.describe('Admin Attendance Correction', () => {

  test('Admin can correct a finalized attendance record', async ({ page }) => {
    const api = await mockAttendanceCorrectionApi(page, { role: 'Admin' });
    await page.goto(`/events/${targetEventId}/attendance`);

    // The roster item should say Finalized and have a Correct button
    const rosterItem = page.locator('[data-assignment-id="30000000-0000-0000-0000-000000000001"]');
    await expect(rosterItem.locator('.cd-finalized-text')).toContainText('Finalized');
    await expect(rosterItem.locator('.cd-current-status')).toHaveText('NoShow');
    
    const correctBtn = rosterItem.getByRole('button', { name: 'Correct attendance for Alice Smith' });
    await expect(correctBtn).toBeVisible();
    
    // Open correction form
    await correctBtn.click();
    
    // Form should be visible
    const newOutcomeSelect = rosterItem.locator('select');
    const reasonInput = rosterItem.getByPlaceholder(/Reason required/i);
    const saveBtn = rosterItem.getByRole('button', { name: 'Save' });
    
    // Initial save should be blocked by empty reason client validation
    await saveBtn.click();
    await expect(page.locator('.cd-alert--error')).toContainText('Please provide a mandatory reason for the correction.');
    
    // Fill out correction
    await newOutcomeSelect.selectOption('Attended');
    await reasonInput.fill('Admin override - user arrived late');
    await saveBtn.click();
    
    // Wait for success
    await expect(page.locator('.cd-alert--success')).toContainText('Attendance corrected successfully.');
    expect(api.postCalls).toBe(1);
  });

  test('Chief Leader cannot see correction button', async ({ page }) => {
    await mockAttendanceCorrectionApi(page, { role: 'ChiefLeader' });
    
    await page.goto(`/events/${targetEventId}/attendance`);
    const rosterItem = page.locator('[data-assignment-id="30000000-0000-0000-0000-000000000001"]');
    await expect(rosterItem.locator('.cd-finalized-text')).toContainText('Finalized');
    
    const correctBtn = rosterItem.getByRole('button', { name: 'Correct attendance for Alice Smith' });
    await expect(correctBtn).not.toBeVisible();
  });

  test('Correction form accessibility', async ({ page }) => {
    await mockAttendanceCorrectionApi(page, { role: 'Admin' });
    await page.goto(`/events/${targetEventId}/attendance`);
    
    const rosterItem = page.locator('[data-assignment-id="30000000-0000-0000-0000-000000000001"]');
    const correctBtn = rosterItem.getByRole('button', { name: 'Correct attendance for Alice Smith' });
    await correctBtn.click();
    
    // Form should be visible
    await expect(rosterItem.getByPlaceholder(/Reason required/i)).toBeVisible();
    
    // WCAG 2.2 AA accessibility audit
    const axeResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
      .analyze();
    expect(axeResults.violations).toEqual([]);
    
    // Touch targets
    const saveBtn = rosterItem.getByRole('button', { name: 'Save' });
    const saveBox = await saveBtn.boundingBox();
    expect(saveBox!.height).toBeGreaterThanOrEqual(44);
    
    const cancelBtn = rosterItem.getByRole('button', { name: 'Cancel' });
    const cancelBox = await cancelBtn.boundingBox();
    expect(cancelBox!.height).toBeGreaterThanOrEqual(44);
  });
});

interface MockApiOptions {
  role: 'Admin' | 'ChiefLeader';
}

async function mockAttendanceCorrectionApi(page: Page, options: MockApiOptions) {
  let postCalls = 0;

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
          userName: 'test_user',
          roles: [options.role],
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
          isAllFinalized: true,
          entries: [
            {
              teamAssignmentId: '30000000-0000-0000-0000-000000000001',
              volunteerUserId: '40000000-0000-0000-0000-000000000001',
              volunteerName: 'Alice Smith',
              attendanceOutcomeId: targetOutcomeId,
              outcome: 'NoShow',
              finalizedAt: '2026-10-15T10:00:00Z',
              version: 'base64str',
            }
          ],
        }),
      });
    }

    if (path === `/api/attendance/${targetOutcomeId}/correct` && request.method() === 'POST') {
      postCalls++;
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          attendanceOutcomeId: targetOutcomeId,
          teamAssignmentId: '30000000-0000-0000-0000-000000000001',
          previousOutcome: 'NoShow',
          newOutcome: 'Attended',
          correctedAt: new Date().toISOString(),
          version: 'newbase64str',
        }),
      });
    }

    return route.fallback();
  });

  return {
    get postCalls() { return postCalls; },
  };
}
