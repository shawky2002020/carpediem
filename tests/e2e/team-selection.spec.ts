import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const targetEventId = '60000000-0000-0000-0000-000000000001';
const staffingCycleId = '70000000-0000-0000-0000-000000000001';

test('Chief Leader views available pool and selects volunteer into immediate active roster', async ({ page }) => {
  const api = await mockTeamSelectionApi(page);
  await page.goto(`/events/${targetEventId}/team-selection`);

  await expect(page.locator('#team-selection-title')).toBeVisible();
  await expect(page.locator('.event-title')).toContainText('Pyramids Heritage Run 2026');

  // Verify initial counts and lists
  const rosterCount = page.locator('.roster-section .count-badge');
  const candidatesCount = page.locator('.candidates-section .count-badge');
  await expect(rosterCount).toContainText('1');
  await expect(candidatesCount).toContainText('1');

  await expect(page.locator('.team-item .member-name')).toContainText('mariam_leader');
  await expect(page.locator('.candidate-item .candidate-name')).toContainText('ahmed_leader');

  // Select the available candidate
  const btnSelect = page.getByRole('button', { name: 'Select ahmed_leader for team' });
  await expect(btnSelect).toBeEnabled();
  await btnSelect.click();

  // Verify immediate placement on active team roster
  await expect(rosterCount).toContainText('2');
  await expect(candidatesCount).toContainText('0');
  await expect(page.locator('.live-region')).toContainText('Volunteer ahmed_leader added to active team roster');
  await expect(page.locator('.candidates-section .empty-state')).toContainText('No available volunteers found');

  expect(api.postCalls).toBe(1);
  expect(api.lastSelectedCandidateUserId).toBe('50000000-0000-0000-0000-000000000001');
});

test('Displays warning banner and disables selection buttons when selection window is closed', async ({ page }) => {
  await mockTeamSelectionApi(page, { isWindowOpen: false });
  await page.goto(`/events/${targetEventId}/team-selection`);

  await expect(page.getByRole('heading', { name: 'Selection Window Closed' })).toBeVisible();
  await expect(page.getByText('This event has already started')).toBeVisible();

  const btnSelect = page.getByRole('button', { name: 'Select ahmed_leader for team' });
  await expect(btnSelect).toBeDisabled();
});

test('Displays error alert when selection returns 409 conflict', async ({ page }) => {
  await mockTeamSelectionApi(page, {
    conflictCode: 'chief_leader_conflict',
    conflictDetail: 'The Chief Leader cannot be selected as a volunteer team member on this event.',
  });
  await page.goto(`/events/${targetEventId}/team-selection`);

  const btnSelect = page.getByRole('button', { name: 'Select ahmed_leader for team' });
  await expect(btnSelect).toBeEnabled();
  await btnSelect.click();

  await expect(page.locator('.alert-box.error')).toBeVisible();
  await expect(page.getByText('The Chief Leader cannot be selected as a volunteer team member')).toBeVisible();
});

test('Team selection interface meets WCAG 2.2 AA, 44px touch targets, and 200% zoom reflow', async ({ page }) => {
  await mockTeamSelectionApi(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/events/${targetEventId}/team-selection`);

  // WCAG 2.2 AA accessibility audit
  const axeResults = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(axeResults.violations).toEqual([]);

  // Minimum 44px touch target on selection button
  const btnSelect = page.getByRole('button', { name: 'Select ahmed_leader for team' });
  const box = await btnSelect.boundingBox();
  expect(box!.height).toBeGreaterThanOrEqual(44);
  expect(box!.width).toBeGreaterThanOrEqual(44);

  // 200% font zoom reflow (no horizontal scroll overflow > 1px)
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);

  // Focus visibility
  await btnSelect.focus();
  await expect(page.locator(':focus')).toBeVisible();
});

interface MockTeamSelectionOptions {
  isWindowOpen?: boolean;
  conflictCode?: string;
  conflictDetail?: string;
}

async function mockTeamSelectionApi(page: Page, options: MockTeamSelectionOptions = {}) {
  let postCalls = 0;
  let lastSelectedCandidateUserId: string | null = null;

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
          candidates: [],
        }),
      });
    }

    if (path === `/api/events/${targetEventId}/team-selection` && request.method() === 'POST') {
      postCalls += 1;
      const body = JSON.parse(request.postData() ?? '{}');
      lastSelectedCandidateUserId = body.candidateUserId;

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
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: '80000000-0000-0000-0000-000000000002',
          eventId: targetEventId,
          staffingCycleId,
          userId: body.candidateUserId,
          status: 'Active',
          assignedAt: '2026-10-01T12:00:00Z',
          assignedByUserId: '40000000-0000-0000-0000-000000000001',
          version: 'BBBBBBB=',
        }),
      });
    }

    return route.continue();
  });

  return {
    get postCalls() { return postCalls; },
    get lastSelectedCandidateUserId() { return lastSelectedCandidateUserId; },
  };
}
