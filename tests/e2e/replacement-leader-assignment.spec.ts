import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const targetEventId = '70000000-0000-0000-0000-000000000001';
const staffingCycleId = '80000000-0000-0000-0000-000000000001';

test('Chief Leader views staffing gaps and active team roster on replacement page', async ({ page }) => {
  const api = await mockReplacementApi(page);
  await page.goto(`/events/${targetEventId}/replacement-assignment`);

  await expect(page.locator('#replacement-title')).toBeVisible();
  await expect(page.locator('.event-title')).toContainText('Cairo Tech Summit 2026');
  await expect(page.locator('.event-meta')).toContainText('New Cairo Convention Center');

  // Verify counts
  const gapsCount = page.locator('.gaps-section .count-badge');
  const rosterCount = page.locator('.roster-section .count-badge');
  const candidatesCount = page.locator('.candidates-section .count-badge');

  await expect(gapsCount).toContainText('1');
  await expect(rosterCount).toContainText('1');
  await expect(candidatesCount).toContainText('2');

  // Verify staffing gap item
  const gapItem = page.locator('.gap-item').first();
  await expect(gapItem.locator('.cancelled-name')).toContainText('tarek_cancelled');
  await expect(gapItem.locator('.reason-text')).toContainText('Medical emergency conflict');

  // Verify candidate badges
  const candidateItems = page.locator('.candidate-item');
  await expect(candidateItems.nth(0).locator('.candidate-name')).toContainText('salma_replacement');
  await expect(candidateItems.nth(0).locator('.badge-available')).toContainText('Available');

  await expect(candidateItems.nth(1).locator('.candidate-name')).toContainText('hassan_replacement');
  await expect(candidateItems.nth(1).locator('.badge-not-available')).toContainText('Unavailable');

  expect(api.getCalls).toBe(1);
});

test('Chief Leader assigns replacement candidate and verifies active roster update and live announcement', async ({ page }) => {
  const api = await mockReplacementApi(page);
  await page.goto(`/events/${targetEventId}/replacement-assignment`);

  // Fill justification
  const justificationInput = page.locator('#replacement-justification');
  await justificationInput.fill('Replacing tarek with salma for morning track coordination.');

  // Assign first candidate
  const assignBtn = page.getByRole('button', { name: 'Assign salma_replacement as replacement' });
  await expect(assignBtn).toBeEnabled();
  await assignBtn.click();

  // Verify update in counts and lists
  await expect(page.locator('.roster-section .count-badge')).toContainText('2');
  await expect(page.locator('.candidates-section .count-badge')).toContainText('1');
  await expect(page.locator('.live-region')).toContainText('Volunteer salma_replacement assigned as replacement');

  const activeMembers = page.locator('.team-item .member-name');
  await expect(activeMembers).toContainText(['ahmed_leader', 'salma_replacement']);

  expect(api.postCalls).toBe(1);
  expect(api.lastPayload?.candidateUserId).toBe('50000000-0000-0000-0000-000000000002');
  expect(api.lastPayload?.replacedAssignmentId).toBe('90000000-0000-0000-0000-000000000001');
  expect(api.lastPayload?.justification).toBe('Replacing tarek with salma for morning track coordination.');
});

test('Disables replacement assign buttons when selection window is closed', async ({ page }) => {
  await mockReplacementApi(page, { isWindowOpen: false });
  await page.goto(`/events/${targetEventId}/replacement-assignment`);

  await expect(page.getByRole('heading', { name: 'Replacement Window Closed' })).toBeVisible();
  await expect(page.getByText('This event has already started. Replacement leader assignments are closed.')).toBeVisible();

  const assignBtn = page.getByRole('button', { name: 'Assign salma_replacement as replacement' });
  await expect(assignBtn).toBeDisabled();
});

test('Displays error alert when replacement assignment returns 409 conflict', async ({ page }) => {
  await mockReplacementApi(page, {
    conflictCode: 'chief_leader_conflict',
    conflictTitle: 'Chief Leader Conflict',
    conflictDetail: 'The Chief Leader cannot be assigned as a replacement volunteer member.',
  });
  await page.goto(`/events/${targetEventId}/replacement-assignment`);

  const assignBtn = page.getByRole('button', { name: 'Assign salma_replacement as replacement' });
  await assignBtn.click();

  await expect(page.locator('.alert-box.error')).toBeVisible();
  await expect(page.getByText('The Chief Leader cannot be assigned as a replacement volunteer member.')).toBeVisible();
});

test('Replacement assignment UI meets WCAG 2.2 AA, 44px touch targets, and 200% zoom reflow', async ({ page }) => {
  await mockReplacementApi(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/events/${targetEventId}/replacement-assignment`);

  // WCAG 2.2 AA accessibility audit
  const axeResults = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(axeResults.violations).toEqual([]);

  // Minimum 44px touch target on assign button
  const assignBtn = page.getByRole('button', { name: 'Assign salma_replacement as replacement' });
  const btnBox = await assignBtn.boundingBox();
  expect(btnBox!.height).toBeGreaterThanOrEqual(44);
  expect(btnBox!.width).toBeGreaterThanOrEqual(44);

  // Minimum 44px touch target on selectable gap card
  const gapItem = page.locator('.gap-item').first();
  const gapBox = await gapItem.boundingBox();
  expect(gapBox!.height).toBeGreaterThanOrEqual(44);

  // 200% font zoom reflow (no horizontal scroll overflow > 1px)
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);

  // Focus visibility
  await assignBtn.focus();
  await expect(page.locator(':focus')).toBeVisible();
});

interface MockReplacementApiOptions {
  isWindowOpen?: boolean;
  conflictCode?: string;
  conflictTitle?: string;
  conflictDetail?: string;
}

async function mockReplacementApi(page: Page, options: MockReplacementApiOptions = {}) {
  let getCalls = 0;
  let postCalls = 0;
  let lastPayload: any = null;

  const mockActiveTeam = [
    {
      assignmentId: '30000000-0000-0000-0000-000000000001',
      userId: '50000000-0000-0000-0000-000000000001',
      userName: 'ahmed_leader',
      status: 'Active',
      assignedAt: '2026-10-01T10:00:00Z',
      version: 'AQIDBAUGCAk=',
    },
  ];

  const mockCancelledAssignments = [
    {
      assignmentId: '90000000-0000-0000-0000-000000000001',
      userId: '60000000-0000-0000-0000-000000000001',
      userName: 'tarek_cancelled',
      status: 'Cancelled',
      assignedAt: '2026-10-01T09:00:00Z',
      cancelledAt: '2026-10-01T15:30:00Z',
      cancellationReason: 'Medical emergency conflict',
      version: 'BAUGCAkLCww=',
    },
  ];

  const mockCandidates = [
    {
      userId: '50000000-0000-0000-0000-000000000002',
      userName: 'salma_replacement',
      availabilityStatus: 'Available',
      declaredAt: '2026-10-01T11:00:00Z',
    },
    {
      userId: '50000000-0000-0000-0000-000000000003',
      userName: 'hassan_replacement',
      availabilityStatus: 'NotAvailable',
      declaredAt: '2026-10-01T11:30:00Z',
    },
  ];

  await page.route('**/api/**', async (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;

    if (path === '/api/session/me') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          userId: '40000000-0000-0000-0000-000000000001',
          userName: 'chief_leader_user',
          roles: ['ChiefLeader', 'Leader'],
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

    if (path === `/api/events/${targetEventId}/replacement-assignment` && request.method() === 'GET') {
      getCalls++;
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          eventId: targetEventId,
          eventTitle: 'Cairo Tech Summit 2026',
          staffingCycleId,
          startDateTime: '2026-10-20T09:00:00Z',
          endDateTime: '2026-10-20T17:00:00Z',
          location: 'New Cairo Convention Center',
          isWindowOpen: options.isWindowOpen ?? true,
          activeTeam: mockActiveTeam,
          cancelledAssignments: mockCancelledAssignments,
          candidates: mockCandidates,
        }),
      });
    }

    if (path === `/api/events/${targetEventId}/replacement-assignment` && request.method() === 'POST') {
      postCalls++;
      lastPayload = request.postDataJSON();

      if (options.conflictCode) {
        return route.fulfill({
          status: 409,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            status: 409,
            title: options.conflictTitle || 'Conflict',
            detail: options.conflictDetail || 'Conflict occurred.',
            code: options.conflictCode,
          }),
        });
      }

      return route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: '12345678-1234-1234-1234-1234567890ab',
          eventId: targetEventId,
          userId: lastPayload?.candidateUserId,
          status: 'Active',
          assignedAt: '2026-10-02T08:30:00Z',
          version: 'AQAAAAAAAAA=',
        }),
      });
    }

    return route.continue();
  });

  return {
    get getCalls() { return getCalls; },
    get postCalls() { return postCalls; },
    get lastPayload() { return lastPayload; },
  };
}
