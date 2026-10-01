import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const eventId = '20000000-0000-0000-0000-000000000001';
const chiefLeaderUserId = '30000000-0000-0000-0000-000000000001';
const assignmentId = '40000000-0000-0000-0000-000000000001';

test('Admin publishes draft event after confirmation and locks draft editing', async ({ page }) => {
  const api = await mockPublicationApi(page, { appointmentStatus: 'Active' });
  await page.goto(`/draft-events/${eventId}`);

  await expect(page.getByText('Chief Leader accepted. Ready to publish.')).toBeVisible();
  const publishBtn = page.getByRole('button', { name: 'Publish Event' });
  await expect(publishBtn).toBeEnabled();

  await publishBtn.click();
  await expect(page.getByRole('heading', { name: 'Confirm Event Publication' })).toBeVisible();
  await expect(page.getByText('Publishing will notify eligible volunteer Leaders')).toBeVisible();

  await page.getByRole('button', { name: 'Yes, Publish Event' }).click();
  await expect(page.locator('.save-status')).toContainText('Event published successfully');
  await expect(page.getByText('This Event is published and cannot be modified via draft editing.')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Published Event' })).toBeVisible();
  expect(api.publishCalls).toBe(1);
});

test('Admin cancels confirmation dialog leaving draft untouched', async ({ page }) => {
  const api = await mockPublicationApi(page, { appointmentStatus: 'Active' });
  await page.goto(`/draft-events/${eventId}`);

  await page.getByRole('button', { name: 'Publish Event' }).click();
  await expect(page.getByRole('heading', { name: 'Confirm Event Publication' })).toBeVisible();

  await page.getByRole('dialog').getByRole('button', { name: 'Cancel' }).click();
  await expect(page.getByRole('heading', { name: 'Confirm Event Publication' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Publish Event' })).toBeVisible();
  expect(api.publishCalls).toBe(0);
});

test('Publish button disabled when Chief Leader is not Active', async ({ page }) => {
  await mockPublicationApi(page, { appointmentStatus: 'Pending' });
  await page.goto(`/draft-events/${eventId}`);

  await expect(page.getByText('An accepted Chief Leader is required before publishing.')).toBeVisible();
  const publishBtn = page.getByRole('button', { name: 'Publish Event' });
  await expect(publishBtn).toBeDisabled();
});

test('Ordinary Leader cannot access Admin draft edit or publication', async ({ page }) => {
  await mockPublicationApi(page, { role: 'Leader', appointmentStatus: 'Active' });
  await page.goto(`/draft-events/${eventId}`);
  await expect(page).toHaveURL(/access-denied/);
  await expect(page.getByRole('button', { name: 'Publish Event' })).toHaveCount(0);
});

test('Publishing interface is accessible, touch-sized, and reflows cleanly', async ({ page }) => {
  await mockPublicationApi(page, { appointmentStatus: 'Active' });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/draft-events/${eventId}`);

  // Accessibility check
  const axeResults = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(axeResults.violations).toEqual([]);

  // Touch target minimum check (44px)
  const publishBtn = page.getByRole('button', { name: 'Publish Event' });
  const target = await publishBtn.boundingBox();
  expect(target!.height).toBeGreaterThanOrEqual(44);
  expect(target!.width).toBeGreaterThanOrEqual(44);

  // Zoom 200% overflow check
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);

  // Focus visibility check
  await publishBtn.focus();
  await expect(page.locator(':focus')).toBeVisible();
});

interface MockApiOptions {
  role?: 'Admin' | 'Leader';
  appointmentStatus?: 'Active' | 'Pending' | null;
}

async function mockPublicationApi(page: Page, options: MockApiOptions = {}) {
  let publishCalls = 0;
  let draftStatus = 'Draft';
  let draftVersion = 'AAAAAAAAAAA=';

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
          userName: 'admin',
          roles: [options.role ?? 'Admin'],
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

    if (path === `/api/events/drafts/${eventId}` && request.method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: eventId,
          title: 'Community Cleanup',
          start: { localDateTime: '2026-10-10T14:30', offset: '+02:00' },
          end: { localDateTime: '2026-10-10T16:30', offset: '+02:00' },
          startDateTimeUtc: '2026-10-10T12:30:00Z',
          endDateTimeUtc: '2026-10-10T14:30:00Z',
          location: 'North Shore Park',
          notes: 'Bring gloves',
          status: draftStatus,
          version: draftVersion,
        }),
      });
    }

    if (path === `/api/events/drafts/${eventId}/chief-leader-appointment` && request.method() === 'GET') {
      const currentAppointment = options.appointmentStatus ? {
        id: assignmentId,
        eventId,
        candidate: { userId: chiefLeaderUserId, userName: 'chief.one' },
        status: options.appointmentStatus,
        appointedAt: '2026-10-01T10:00:00Z',
        appointedByUserId: '10000000-0000-0000-0000-000000000001',
        acceptedAt: options.appointmentStatus === 'Active' ? '2026-10-01T10:05:00Z' : null,
        acceptanceSource: options.appointmentStatus === 'Active' ? 'Direct' : null,
        acceptedByUserId: options.appointmentStatus === 'Active' ? chiefLeaderUserId : null,
        version: 'AAAAAAAAAAA=',
      } : null;

      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          eventId,
          eventTitle: 'Community Cleanup',
          status: draftStatus,
          currentAppointment,
          declinedAppointments: [],
          eligibleCandidates: [],
        }),
      });
    }

    if (path === `/api/events/drafts/${eventId}/publish` && request.method() === 'POST') {
      publishCalls += 1;
      draftStatus = 'Published';
      draftVersion = 'AQAAAAAAAAA=';
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: eventId,
          title: 'Community Cleanup',
          startDateTime: '2026-10-10T12:30:00Z',
          endDateTime: '2026-10-10T14:30:00Z',
          location: 'North Shore Park',
          notes: 'Bring gloves',
          status: 'Published',
          activeChiefLeader: {
            userId: chiefLeaderUserId,
            userName: 'chief.one',
            assignmentId,
          },
          version: draftVersion,
          publishedAt: new Date().toISOString(),
        }),
      });
    }

    return route.continue();
  });

  return {
    get publishCalls() { return publishCalls; },
  };
}
