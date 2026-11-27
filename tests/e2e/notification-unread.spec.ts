import { expect, test } from '@playwright/test';

test('older unread notices can be found and marked read without changing recent history', async ({ page }) => {
  const eventId = '53000000-0000-4000-8000-000000000001';
  const oldNotice = {
    id: '53000000-0000-4000-8000-000000000002', eventId, notificationType: 'EventPublished',
    contextPayload: JSON.stringify({ eventTitle: 'Older field briefing' }),
    createdAt: '2026-11-01T09:00:00Z', readAt: null,
  };
  const newerRead = Array.from({ length: 100 }, (_, index) => ({
    ...oldNotice, id: `recent-${index}`, contextPayload: JSON.stringify({ eventTitle: 'Recent exercise' }),
    createdAt: '2026-11-22T09:00:00Z', readAt: '2026-11-22T10:00:00Z',
  }));
  let markedRead = false;
  let failUnread = true;
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/session/me') return route.fulfill({ json: { userId: 'leader-qa', userName: 'leader.qa', roles: ['Leader'] } });
    if (path === '/api/session/antiforgery') return route.fulfill({ json: { requestToken: 'e2e' } });
    if (path === '/api/notifications/unread-count') return route.fulfill({ json: { count: markedRead ? 0 : 1 } });
    if (path === '/api/notifications') return route.fulfill({ json: newerRead });
    if (path === '/api/notifications/unread') {
      if (failUnread) {
        failUnread = false;
        return route.fulfill({ status: 503, json: { title: 'Temporarily unavailable' } });
      }
      return route.fulfill({ json: { items: markedRead ? [] : [oldNotice], nextBeforeId: null } });
    }
    if (path === `/api/notifications/${oldNotice.id}/read` && route.request().method() === 'POST') {
      markedRead = true;
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ status: 404, json: { title: 'Fixture not found' } });
  });

  await page.goto('/notifications');
  const inbox = page.getByRole('region', { name: 'Notifications' });
  await expect(inbox.getByText('1 unread', { exact: true })).toBeVisible();
  await expect(inbox.getByText('Event: Older field briefing')).toHaveCount(0);
  const unread = inbox.getByRole('button', { name: 'Unread', exact: true });
  await unread.focus();
  await page.keyboard.press('Enter');
  await expect(unread).toHaveAttribute('aria-pressed', 'true');
  await expect(inbox.getByRole('alert')).toContainText('Unread notifications could not be loaded.');
  await inbox.getByRole('button', { name: 'Try again' }).click();
  const oldCard = inbox.getByRole('listitem').filter({ hasText: 'Older field briefing' });
  await expect(oldCard).toBeVisible();
  await expect(oldCard.getByRole('link', { name: 'Declare availability' })).toHaveAttribute('href', `/events/${eventId}/availability`);
  await expect(inbox.getByText('Event: Recent exercise')).toHaveCount(0);
  expect(markedRead).toBe(false);
  await oldCard.getByRole('button', { name: 'Mark as read' }).click();
  await expect(inbox.getByText('0 unread', { exact: true })).toBeVisible();
  await expect(inbox.getByText('You’re caught up. No unread notifications.')).toBeVisible();
  await expect(unread).toBeFocused();
  await inbox.getByRole('button', { name: 'Recent', exact: true }).click();
  await expect(inbox.getByText('Event: Recent exercise')).toHaveCount(100);
});

test('unread pages provide a keyboard path to older and newer messages', async ({ page }) => {
  const message = {
    id: '53000000-0000-4000-8000-000000000004', eventId: null, notificationType: 'TeamMemberCancelled',
    contextPayload: JSON.stringify({ eventTitle: 'Recent shift' }),
    createdAt: '2026-11-22T09:00:00Z', readAt: null,
  };
  const older = { ...message, id: '53000000-0000-4000-8000-000000000005', contextPayload: JSON.stringify({ eventTitle: 'Earlier shift' }) };
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/session/me') return route.fulfill({ json: { userId: 'chief-qa', userName: 'chief.qa', roles: ['ChiefLeader'] } });
    if (url.pathname === '/api/session/antiforgery') return route.fulfill({ json: { requestToken: 'e2e' } });
    if (url.pathname === '/api/notifications/unread-count') return route.fulfill({ json: { count: 2 } });
    if (url.pathname === '/api/notifications') return route.fulfill({ json: [message] });
    if (url.pathname === '/api/notifications/unread') return route.fulfill({ json: url.searchParams.get('beforeId') === message.id
      ? { items: [older], nextBeforeId: null }
      : { items: [message], nextBeforeId: message.id } });
    return route.fulfill({ status: 404, json: { title: 'Fixture not found' } });
  });

  await page.goto('/notifications');
  const inbox = page.getByRole('region', { name: 'Notifications' });
  const unread = inbox.getByRole('button', { name: 'Unread', exact: true });
  await unread.click();
  await expect(inbox.getByText(/cancelled their assignment for Recent shift/)).toBeVisible();
  const olderButton = inbox.getByRole('button', { name: 'Older unread' });
  await olderButton.focus();
  await page.keyboard.press('Enter');
  await expect(inbox.getByText(/cancelled their assignment for Earlier shift/)).toBeVisible();
  await expect(unread).toBeFocused();
  await inbox.getByRole('button', { name: 'Newer unread' }).click();
  await expect(inbox.getByText(/cancelled their assignment for Recent shift/)).toBeVisible();
  await expect(inbox.getByRole('button', { name: 'Newer unread' })).toHaveCount(0);
});
