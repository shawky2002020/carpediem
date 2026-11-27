import { expect, Page, test } from '@playwright/test';

const eventId = '60000000-0000-0000-0000-000000000001';
const candidate = { userId: '50000000-0000-0000-0000-000000000025', userName: 'Field Leader 25' };
const teamPath = `/events/${eventId}/team-selection`;
const query = 'Field Leader';

for (const wayBack of ['in-page link', 'browser Back'] as const) {
  test(`returns to the same filtered candidate and focus via ${wayBack}`, async ({ page }) => {
    await mockHistoryNavigation(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto(teamPath);
    const search = page.getByRole('searchbox', { name: 'Find a candidate' });
    await search.fill(query);
    const trigger = page.getByRole('link', { name: `View history for ${candidate.userName}` });
    await trigger.evaluate(link => link.scrollIntoView({ block: 'center' }));
    const top = await trigger.evaluate(link => link.getBoundingClientRect().top);
    await trigger.focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: candidate.userName })).toBeVisible();
    await expect(page).toHaveURL(`/events/${eventId}/leaders/${candidate.userId}/history`);
    if (wayBack === 'browser Back') await page.goBack();
    else await page.getByRole('link', { name: /Back to team selection/ }).click();

    await expect(page).toHaveURL(teamPath);
    await expect(search).toHaveValue(query);
    await expect(page.getByText('Unrelated candidate')).toHaveCount(0);
    await expect(trigger).toBeFocused();
    expect(Math.abs((await trigger.evaluate(link => link.getBoundingClientRect().top)) - top)).toBeLessThanOrEqual(4);
  });
}

test('keeps the search and focuses it when the invoking candidate leaves the pool', async ({ page }) => {
  const removeCandidate = await mockHistoryNavigation(page);
  await page.goto(teamPath);
  const search = page.getByRole('searchbox', { name: 'Find a candidate' });
  await search.fill(candidate.userName);
  const trigger = page.getByRole('link', { name: `View history for ${candidate.userName}` });
  await trigger.click();
  await expect(page.getByRole('heading', { name: candidate.userName })).toBeVisible();
  removeCandidate();
  await page.getByRole('link', { name: /Back to team selection/ }).click();

  await expect(search).toHaveValue(candidate.userName);
  await expect(search).toBeFocused();
  await expect(trigger).toHaveCount(0);
});

test('a later visit to the same history does not restore a search from an abandoned review', async ({ page }) => {
  await mockHistoryNavigation(page);
  await page.goto(teamPath);
  const search = page.getByRole('searchbox', { name: 'Find a candidate' });
  await search.fill(candidate.userName);
  await page.getByRole('link', { name: `View history for ${candidate.userName}` }).click();
  await expect(page.getByRole('heading', { name: candidate.userName })).toBeVisible();
  const historyPath = page.url();
  await page.getByRole('link', { name: 'Carpediem home' }).click();
  await expect(page).toHaveURL('/');
  await page.goBack();
  await expect(page).toHaveURL(historyPath);
  await page.getByRole('link', { name: /Back to team selection/ }).click();
  await expect(search).toHaveValue('');
});

async function mockHistoryNavigation(page: Page): Promise<() => void> {
  const declaredAt = '2026-10-01T10:00:00Z';
  const pool = Array.from({ length: 31 }, (_, index) => {
    const number = String(index + 1).padStart(2, '0');
    return { userId: `50000000-0000-0000-0000-0000000000${number}`, userName: `Field Leader ${number}`, declaredAt };
  });
  pool.push({ userId: '50000000-0000-0000-0000-000000000032', userName: 'Unrelated candidate', declaredAt });
  let candidatePresent = true;
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/session/me') return route.fulfill({ json: {
      userId: '40000000-0000-0000-0000-000000000001', userName: 'chief_qa', roles: ['ChiefLeader'],
    } });
    if (path === '/api/session/antiforgery') return route.fulfill({ json: { requestToken: 'e2e' } });
    if (path === '/api/notifications/unread-count') return route.fulfill({ json: { count: 0 } });
    if (path === '/api/notifications') return route.fulfill({ json: [] });
    if (path === `/api/events/${eventId}/team-selection`) return route.fulfill({ json: {
      eventId, eventTitle: 'North shore staffing', location: 'Ranger station',
      currentStaffingCycleId: '70000000-0000-0000-0000-000000000001',
      startDateTime: '2026-12-15T08:00:00Z', endDateTime: '2026-12-15T12:00:00Z',
      isWindowOpen: true, activeTeam: [],
      availableCandidates: candidatePresent ? pool : pool.filter(item => item.userId !== candidate.userId),
    } });
    if (path === `/api/events/${eventId}/direct-assignment/candidates`) return route.fulfill({ json: { candidates: [] } });
    if (path === `/api/leaders/${candidate.userId}/history`) return route.fulfill({ json: {
      ...candidate, eventsAttended: 0, noShows: 0, selfCancellations: 0, events: [],
    } });
    return route.fulfill({ status: 404, json: { title: 'Fixture not found' } });
  });
  return () => { candidatePresent = false; };
}
