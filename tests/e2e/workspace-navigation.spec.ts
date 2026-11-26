import { expect, test } from '@playwright/test';

test('workspace navigation keeps administrator destinations and restores keyboard focus', async ({ page }, testInfo) => {
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/session/me') return route.fulfill({ json: { userId: 'admin-qa', userName: 'admin.qa', roles: ['Admin'] } });
    if (path === '/api/session/antiforgery') return route.fulfill({ json: { requestToken: 'e2e' } });
    if (path === '/api/notifications/unread-count') return route.fulfill({ json: { count: 1 } });
    if (path === '/api/admin/accounts') return route.fulfill({ json: {
      items: [{ userId: 'leader-qa', userName: 'leader.qa', email: 'leader@localhost.invalid', isActive: true, roles: ['Leader'] }],
      page: 1, pageSize: 50, hasMore: false,
    } });
    return route.fulfill({ json: [] });
  });
  await page.goto('/dashboard');
  const workspace = page.getByRole('navigation', { name: 'Workspace navigation' });

  if (testInfo.project.name.startsWith('mobile-')) {
    const menu = page.getByRole('button', { name: 'Menu' });
    await menu.focus();
    await page.keyboard.press('Enter');
    await expect(workspace.getByRole('link', { name: 'Accounts' })).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(menu).toBeFocused();
    await expect(menu).toHaveAttribute('aria-expanded', 'false');
    await menu.click();
  }

  await workspace.getByRole('link', { name: 'Accounts' }).click();
  await expect(page).toHaveURL('/admin/accounts');
  await expect(page.getByRole('main')).toBeFocused();

  if (testInfo.project.name.startsWith('mobile-')) {
    await expect(page.getByRole('button', { name: 'Menu' })).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByRole('navigation', { name: 'Quick navigation' })
      .getByRole('link', { name: 'Notifications, 1 unread' })).toBeVisible();
  } else {
    await expect(workspace.getByRole('link', { name: 'Accounts' })).toHaveAttribute('aria-current', 'page');
  }

  const deactivate = page.getByRole('button', { name: 'Deactivate', exact: true });
  await deactivate.click();
  const dialog = page.getByRole('dialog', { name: 'Deactivate leader.qa?' });
  await expect(dialog.getByRole('button', { name: 'Keep account active' })).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await expect(deactivate).toBeFocused();
});

test('Leader sees their own workspace and receives the existing access denial on Admin routes', async ({ page }) => {
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/session/me') return route.fulfill({ json: { userId: 'leader-qa', userName: 'leader.qa', roles: ['Leader'] } });
    if (path === '/api/session/antiforgery') return route.fulfill({ json: { requestToken: 'e2e' } });
    if (path === '/api/notifications/unread-count') return route.fulfill({ json: { count: 0 } });
    return route.fulfill({ json: [] });
  });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Your workspace' })).toBeVisible();
  await expect(page.getByRole('main').getByRole('link', { name: /My profile/ })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Accounts' })).toHaveCount(0);
  await page.goto('/admin/accounts');
  await expect(page).toHaveURL('/access-denied');
  await expect(page.getByRole('heading', { name: 'Administrator access is required' })).toBeVisible();
});

test('Chief Leader appointment notification opens the appointment list when only the event id is available', async ({ page }) => {
  const eventId = '53000000-0000-4000-8000-000000000001';
  const appointmentId = '53000000-0000-4000-8000-000000000002';
  const appointment = {
    id: appointmentId, eventId, eventTitle: 'Camp orientation', status: 'Pending',
    startDateTime: '2026-12-12T09:00:00Z', endDateTime: '2026-12-12T13:00:00Z',
    location: 'Mango Tree Campsite', appointedAt: '2026-11-22T09:00:00Z', version: 'AAAAAAA=',
  };
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/session/me') return route.fulfill({ json: { userId: 'chief-qa', userName: 'chief.qa', roles: ['ChiefLeader', 'Leader'] } });
    if (path === '/api/session/antiforgery') return route.fulfill({ json: { requestToken: 'e2e' } });
    if (path === '/api/notifications/unread-count') return route.fulfill({ json: { count: 1 } });
    if (path === '/api/notifications') return route.fulfill({ json: [{
      id: 'notification-qa', eventId, notificationType: 'ChiefLeaderAppointment', readAt: null,
      contextPayload: JSON.stringify({ eventTitle: 'Camp orientation' }), createdAt: '2026-11-22T09:00:00Z',
    }] });
    if (path === '/api/chief-leader/appointments') return route.fulfill({ json: [appointment] });
    if (path === `/api/chief-leader/appointments/${appointmentId}`) return route.fulfill({ json: appointment });
    return route.fulfill({ status: 404, json: { title: 'Fixture not found' } });
  });

  await page.goto('/notifications');
  await page.getByRole('link', { name: 'Review appointments' }).click();
  await expect(page).toHaveURL('/my-appointments');
  await page.getByRole('link', { name: /Camp orientation/ }).click();
  await expect(page).toHaveURL(`/my-appointments/${appointmentId}`);
  await expect(page.getByRole('heading', { name: 'Camp orientation' })).toBeVisible();
});

test('the event queue keeps counts and expanded actions usable at 200% text size', async ({ page }) => {
  const title = 'CampOrientation'.repeat(12);
  const event = {
    eventId: 'event-qa', title, location: 'CampsiteEntrance'.repeat(7), status: 'Published',
    startDateTime: '2026-12-12T08:00:00Z', endDateTime: '2026-12-12T16:00:00Z',
    inProgress: false, pendingAttendance: true, activeRosterCount: 0, availableUnassignedCount: 6,
  };
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/session/me') return route.fulfill({ json: { userId: 'admin-qa', userName: 'admin.qa', roles: ['Admin'] } });
    if (path === '/api/session/antiforgery') return route.fulfill({ json: { requestToken: 'e2e' } });
    if (path === '/api/notifications/unread-count') return route.fulfill({ json: { count: 0 } });
    if (path === '/api/dashboard/events') return route.fulfill({ json: [
      event, { ...event, eventId: 'event-qa-2', title: 'Staffing briefing', pendingAttendance: false, activeRosterCount: 4 },
    ] });
    return route.fulfill({ json: [] });
  });
  await page.goto('/dashboard');
  await expect(page.getByRole('heading', { name: title })).toBeVisible();
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
  await page.getByRole('button', { name: 'Needs attention 1' }).click();
  await expect(page.getByRole('heading', { name: 'Staffing briefing' })).toHaveCount(0);
  await page.getByText('More actions for ' + title).click();
  await expect(page.getByRole('link', { name: 'Replacements' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
});
