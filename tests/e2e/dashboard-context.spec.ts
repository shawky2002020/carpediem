import { expect, test, type Page } from '@playwright/test';

const targetTitle = 'Camp logistics session 12';
const targetId = eventId(12);

test('the operational view starts with current work and keeps closed events accessible', async ({ page }) => {
  await fixture(page);
  await page.goto('/dashboard');

  await expect(page.getByRole('button', { name: 'Operations 15', pressed: true })).toBeVisible();
  const events = page.getByRole('heading', { level: 4 });
  await expect(events).toHaveCount(15);
  await expect(events.nth(0)).toHaveText('First aid workshop underway');
  await expect(events.nth(1)).toHaveText('River trail attendance due');
  await expect(events.nth(2)).toHaveText('Camp logistics session 1');
  await expect(page.getByRole('heading', { name: 'Closed training 1', exact: true })).toHaveCount(0);

  await page.getByRole('button', { name: 'Closed 26' }).click();
  await expect(events).toHaveCount(26);
  await expect(page.getByRole('heading', { name: 'Closed training 1', exact: true })).toBeVisible();
});

test('browser Back returns focus to the opened event and reload retains the selected view', async ({ page }) => {
  await fixture(page, 'ChiefLeader');
  await page.goto('/dashboard');
  await page.getByRole('button', { name: 'Published 14' }).click();
  const selectedAction = teamAction(page);
  await selectedAction.click();
  await expect(page.getByRole('heading', { name: 'Team Selection' })).toBeVisible();

  await page.goBack();
  await expect(page.getByRole('button', { name: 'Published 14', pressed: true })).toBeVisible();
  await expect(selectedAction).toBeFocused();
  await expect(selectedAction).toBeInViewport();

  await page.reload();
  await expect(page.getByRole('button', { name: 'Published 14', pressed: true })).toBeVisible();
});

test('the dashboard return link retains context and falls back when the opened event leaves the selected view', async ({ page }) => {
  const data = await fixture(page);
  await page.goto('/dashboard');
  await page.getByRole('button', { name: 'Published 14' }).click();
  await teamAction(page).click();
  const backLink = page.getByRole('main').getByRole('link', { name: /Dashboard/ });
  await backLink.click();
  await expect(teamAction(page)).toBeFocused();
  await expect(teamAction(page)).toBeInViewport();

  await teamAction(page).click();
  data.closeTarget();
  await backLink.click();
  const selected = page.getByRole('button', { name: 'Published 13', pressed: true });
  await expect(selected).toBeFocused();
  await expect(selected).toBeInViewport();
  await expect(page.getByRole('heading', { name: targetTitle })).toHaveCount(0);
});

test('a different viewer in the same tab starts in Operations', async ({ page }) => {
  const data = await fixture(page);
  await page.goto('/dashboard');
  await page.getByRole('button', { name: 'Closed 26' }).click();

  data.switchViewer();
  await page.reload();
  await expect(page.getByRole('button', { name: 'Operations 15', pressed: true })).toBeVisible();
});

function eventId(index: number): string {
  return `55000000-0000-4000-8000-${String(index).padStart(12, '0')}`;
}

function teamAction(page: Page) {
  return page.getByRole('listitem').filter({ has: page.getByRole('heading', { name: targetTitle }) })
    .getByRole('link', { name: 'Team selection' });
}

function row(index: number, title: string, start: string, status: string) {
  return {
    eventId: eventId(index), title, status, location: 'Cairo Field Station',
    startDateTime: start, endDateTime: new Date(Date.parse(start) + 4 * 3600000).toISOString(),
    inProgress: false, pendingAttendance: false, activeRosterCount: 4, availableUnassignedCount: 6,
  };
}

async function fixture(page: Page, role: 'Admin' | 'ChiefLeader' = 'Admin') {
  let viewer = 'dashboard-test-viewer';
  let targetClosed = false;
  const closed = Array.from({ length: 25 }, (_, i) => row(
    100 + i, `Closed training ${i + 1}`, new Date(Date.UTC(2026, 6, 1 + i, 8)).toISOString(), i === 0 ? 'Cancelled' : 'Completed',
  ));
  const pending = { ...row(1010, 'River trail attendance due', '2026-11-24T08:00:00Z', 'Completed'), pendingAttendance: true };
  const running = { ...row(1020, 'First aid workshop underway', '2026-11-27T08:00:00Z', 'Published'), inProgress: true };
  const published = Array.from({ length: 13 }, (_, i) => row(
    i + 1, `Camp logistics session ${i + 1}`, new Date(Date.UTC(2026, 11, 1 + i, 8)).toISOString(), 'Published',
  ));
  const draft = row(1030, 'Spring outing draft', '2027-01-12T08:00:00Z', 'Draft');
  const target = published.find(item => item.eventId === targetId)!;

  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/session/me') return route.fulfill({ json: { userId: viewer, userName: 'dashboard.qa', roles: [role] } });
    if (path === '/api/session/antiforgery') return route.fulfill({ json: { requestToken: 'e2e' } });
    if (path === '/api/notifications/unread-count') return route.fulfill({ json: { count: 0 } });
    if (path === '/api/dashboard/events') return route.fulfill({ json: [
      ...closed, pending, running,
      ...published.map(item => item.eventId === targetId && targetClosed ? { ...item, status: 'Completed' } : item), draft,
    ] });
    if (path === `/api/events/${targetId}/team-selection`) return route.fulfill({ json: {
      eventId: targetId, eventTitle: target.title, startDateTime: target.startDateTime, endDateTime: target.endDateTime,
      location: target.location, staffingCycleId: eventId(9999), isWindowOpen: true, availableCandidates: [], activeTeam: [],
    } });
    return route.fulfill({ json: { candidates: [], isWindowOpen: true } });
  });

  return { closeTarget: () => { targetClosed = true; }, switchViewer: () => { viewer = 'another-dashboard-test-viewer'; } };
}
