import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const targetEventId = '60000000-0000-0000-0000-000000000001';
const staffingCycleId = '70000000-0000-0000-0000-000000000001';

test('Chief Leader views direct draft pool and directly assigns non-respondent and unavailable leaders', async ({ page }) => {
  const api = await mockDirectAssignmentApi(page);
  await page.goto(`/events/${targetEventId}/team-selection`);

  await expect(page.locator('#team-selection-title')).toBeVisible();
  await expect(page.locator('.event-title')).toContainText('Pyramids Heritage Run 2026');

  // Verify initial counts and lists
  const rosterCount = page.locator('.roster-section .count-badge');
  const directCandidatesCount = page.locator('.direct-assignment-section .count-badge');
  await expect(rosterCount).toContainText('1');
  await expect(directCandidatesCount).toContainText('2');

  // Verify badges in direct draft pool
  const directItems = page.locator('.direct-candidates-list .candidate-item');
  await expect(directItems).toHaveCount(2);

  const firstItem = directItems.nth(0);
  await expect(firstItem.locator('.candidate-name')).toContainText('omar_not_available');
  await expect(firstItem.locator('.badge-unavailable')).toContainText('Not Available');
  await expect(firstItem.locator('.candidate-timestamp')).toContainText('Declined:');

  const secondItem = directItems.nth(1);
  await expect(secondItem.locator('.candidate-name')).toContainText('fatma_no_response');
  await expect(secondItem.locator('.badge-no-response')).toContainText('Response Needed');

  // Direct-assign the first candidate
  const btnDirectAssign = page.getByRole('button', { name: 'Directly assign omar_not_available to team' });
  await expect(btnDirectAssign).toBeEnabled();
  await btnDirectAssign.click();

  // Verify immediate placement on active team roster
  await expect(rosterCount).toContainText('2');
  await expect(directCandidatesCount).toContainText('1');
  await expect(page.locator('.live-region')).toContainText('Leader omar_not_available directly assigned to active team roster');

  const activeMembers = page.locator('.team-list .candidate-name, .team-list .member-name');
  await expect(activeMembers).toContainText(['mariam_leader', 'omar_not_available']);

  expect(api.directPostCalls).toBe(1);
  expect(api.lastDirectCandidateUserId).toBe('50000000-0000-0000-0000-000000000003');
});

test('Disables direct assign buttons and shows banner when selection window is closed', async ({ page }) => {
  await mockDirectAssignmentApi(page, { isWindowOpen: false });
  await page.goto(`/events/${targetEventId}/team-selection`);

  await expect(page.getByRole('heading', { name: 'Selection Window Closed' })).toBeVisible();
  await expect(page.getByText('This event has already started')).toBeVisible();

  const btnDirectAssign = page.getByRole('button', { name: 'Directly assign omar_not_available to team' });
  await expect(btnDirectAssign).toBeDisabled();
});

test('Displays error alert when direct assignment returns 409 conflict', async ({ page }) => {
  await mockDirectAssignmentApi(page, {
    conflictCode: 'chief_leader_conflict',
    conflictDetail: 'The Chief Leader cannot be directly assigned as a volunteer team member on this event.',
  });
  await page.goto(`/events/${targetEventId}/team-selection`);

  const btnDirectAssign = page.getByRole('button', { name: 'Directly assign omar_not_available to team' });
  await expect(btnDirectAssign).toBeEnabled();
  await btnDirectAssign.click();

  await expect(page.locator('.alert-box.error')).toBeVisible();
  await expect(page.getByText('The Chief Leader cannot be selected as a volunteer team member')).toBeVisible();
});

test('Direct draft UI meets WCAG 2.2 AA, 44px touch targets, and 200% zoom reflow', async ({ page }) => {
  await mockDirectAssignmentApi(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/events/${targetEventId}/team-selection`);

  // WCAG 2.2 AA accessibility audit
  const axeResults = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(axeResults.violations).toEqual([]);

  // Minimum 44px touch target on direct assign button
  const btnDirectAssign = page.getByRole('button', { name: 'Directly assign omar_not_available to team' });
  const box = await btnDirectAssign.boundingBox();
  expect(box!.height).toBeGreaterThanOrEqual(44);
  expect(box!.width).toBeGreaterThanOrEqual(44);

  // 200% font zoom reflow (no horizontal scroll overflow > 1px)
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);

  // Focus visibility
  await btnDirectAssign.focus();
  await expect(page.locator(':focus')).toBeVisible();
});

interface MockDirectAssignmentOptions {
  isWindowOpen?: boolean;
  conflictCode?: string;
  conflictDetail?: string;
}

async function mockDirectAssignmentApi(page: Page, options: MockDirectAssignmentOptions = {}) {
  let directPostCalls = 0;
  let lastDirectCandidateUserId: string | null = null;

  const mockAvailableCandidates = [
    {
      userId: '50000000-0000-0000-0000-000000000001',
      userName: 'ahmed_leader',
      declaredAt: '2026-10-01T10:00:00Z',
    },
  ];

  const mockActiveTeam = [
    {
      assignmentId: '80000000-0000-0000-0000-000000000001',
      userId: '50000000-0000-0000-0000-000000000002',
      userName: 'mariam_leader',
      assignedAt: '2026-10-01T09:30:00Z',
      version: 'AAAAAAA=',
    },
  ];

  const mockDirectCandidates = [
    {
      userId: '50000000-0000-0000-0000-000000000003',
      userName: 'omar_not_available',
      availabilityStatus: 'NotAvailable',
      declaredAt: '2026-10-01T11:00:00Z',
    },
    {
      userId: '50000000-0000-0000-0000-000000000004',
      userName: 'fatma_no_response',
      availabilityStatus: 'NoResponse',
      declaredAt: null,
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
        body: JSON.stringify({ requestToken: 'e2e-csrf-token' }),
      });
    }

    if (path === `/api/events/${targetEventId}/team-selection` && request.method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          eventId: targetEventId,
          staffingCycleId,
          eventTitle: 'Pyramids Heritage Run 2026',
          startDateTime: '2026-10-20T07:00:00Z',
          endDateTime: '2026-10-20T15:00:00Z',
          location: 'Giza Plateau, Cairo',
          isWindowOpen: options.isWindowOpen ?? true,
          availableCandidates: mockAvailableCandidates,
          activeTeam: mockActiveTeam,
        }),
      });
    }

    if (path === `/api/events/${targetEventId}/direct-assignment/candidates` && request.method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          eventId: targetEventId,
          staffingCycleId,
          isWindowOpen: options.isWindowOpen ?? true,
          candidates: mockDirectCandidates,
        }),
      });
    }

    if (path === `/api/events/${targetEventId}/direct-assignment` && request.method() === 'POST') {
      directPostCalls += 1;
      const body = JSON.parse(request.postData() ?? '{}');
      lastDirectCandidateUserId = body.candidateUserId;

      if (options.conflictCode) {
        return route.fulfill({
          status: 409,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            type: 'https://tools.ietf.org/html/rfc7807',
            title: 'Conflict',
            status: 409,
            code: options.conflictCode,
            detail: options.conflictDetail ?? 'Conflict occurred.',
            traceId: 'e2e-trace-id',
          }),
        });
      }

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: '80000000-0000-0000-0000-000000000003',
          eventId: targetEventId,
          staffingCycleId,
          userId: body.candidateUserId,
          status: 'Active',
          assignedAt: '2026-10-01T12:00:00Z',
          assignedByUserId: '40000000-0000-0000-0000-000000000001',
          version: 'CCCCCCC=',
        }),
      });
    }

    return route.continue();
  });

  return {
    get directPostCalls() { return directPostCalls; },
    get lastDirectCandidateUserId() { return lastDirectCandidateUserId; },
  };
}
