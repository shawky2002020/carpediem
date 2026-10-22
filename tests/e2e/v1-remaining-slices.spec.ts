import AxeBuilder from '@axe-core/playwright';
import { expect, Page, Route, test } from '@playwright/test';

const targetEventId = '70000000-0000-0000-0000-000000000001';
const targetLeaderId = '40000000-0000-0000-0000-000000000001';

test.describe('V1 Slices 13 & 17-22 E2E Lifecycle', () => {
  test('SLICE-20: Public Leader registration and Admin account approval & role management', async ({ page }) => {
    const state = await mockRemainingSlicesApi(page, { role: null });

    await page.goto('/register');
    await expect(page.getByRole('heading', { name: 'Register as a Leader' })).toBeVisible();
    await page.getByLabel('Username').fill('new.volunteer');
    await page.getByLabel('Email').fill('new.volunteer@localhost.invalid');
    await page.getByLabel('Password').fill('NewVolunteer#2026!');
    await page.getByRole('button', { name: 'Register' }).click();

    await expect(page.getByRole('status')).toContainText('Registration received. You can sign in after an Admin approves your account.');
    expect(state.registrations).toBe(1);

    state.setRole('Admin');
    await page.goto('/admin/accounts');
    await expect(page.getByRole('heading', { name: 'Accounts' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'new.volunteer' })).toBeVisible();

    await page.getByRole('button', { name: 'Approve / reactivate' }).click();
    expect(state.approvals).toBe(1);
    await expect(page.getByRole('button', { name: 'Deactivate' })).toBeVisible();

    const axe = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
      .analyze();
    expect(axe.violations).toEqual([]);
  });

  test('SLICE-19 & SLICE-13: Operational dashboard and Admin event cancellation', async ({ page }) => {
    const state = await mockRemainingSlicesApi(page, { role: 'Admin' });

    await page.goto('/dashboard');
    await expect(page.getByRole('heading', { name: 'Operational dashboard' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Cairo River Cleanup' })).toBeVisible();
    await expect(page.getByText('Pending Attendance')).toBeVisible();

    await page.getByRole('link', { name: 'Cancel event' }).click();
    await expect(page).toHaveURL(`/events/${targetEventId}/cancel`);
    await expect(page.getByRole('heading', { name: 'Cancel event' })).toBeVisible();

    await page.getByLabel('Reason for cancellation').fill('Severe weather warning across the riverfront');
    await page.getByRole('button', { name: 'Cancel event' }).click();

    expect(state.cancellations).toBe(1);
    await expect(page).toHaveURL('/dashboard');
  });

  test('SLICE-17 & SLICE-18: Notification inbox and Factual Leader history', async ({ page }) => {
    const state = await mockRemainingSlicesApi(page, { role: 'Leader' });

    await page.goto('/notifications');
    await expect(page.getByRole('heading', { name: 'Notifications' })).toBeVisible();
    await expect(page.getByText('1 unread')).toBeVisible();
    await page.getByRole('button', { name: 'Mark as read' }).click();
    expect(state.notificationsRead).toBe(1);
    await expect(page.getByText('0 unread')).toBeVisible();

    await page.goto('/profile');
    await expect(page.getByRole('heading', { name: 'My profile' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'leader.one' })).toBeVisible();
    await expect(page.getByText('Cairo River Cleanup')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Event feedback' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Incidents' })).toBeVisible();
  });

  test('SLICE-21 & SLICE-22: Post-event feedback and Incident reporting & Admin review', async ({ page }) => {
    const state = await mockRemainingSlicesApi(page, { role: 'ChiefLeader' });

    await page.goto(`/events/${targetEventId}/feedback`);
    await expect(page.getByRole('heading', { name: 'Event feedback' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Rate leader.one' })).toBeVisible();

    await page.locator(`#rating-${targetLeaderId}`).selectOption('5');
    await page.locator(`#comment-${targetLeaderId}`).fill('Excellent coordination on site.');
    await page.getByRole('button', { name: 'Submit rating' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Rating submitted.' })).toBeVisible();
    expect(state.feedbackSubmissions).toBe(1);

    await page.goto(`/events/${targetEventId}/incidents`);
    await expect(page.getByRole('heading', { name: 'Event incidents' })).toBeVisible();
    await page.getByLabel('Title').fill('Equipment supply delay');
    await page.getByLabel('Description').fill('Gloves and safety vests arrived 20 minutes after check-in.');
    await page.getByRole('button', { name: 'Submit incident' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Incident submitted for Admin review.' })).toBeVisible();
    expect(state.incidentSubmissions).toBe(1);

    state.setRole('Admin');
    await page.goto('/admin/feedback');
    await expect(page.getByRole('heading', { name: 'Feedback' })).toBeVisible();
    await expect(page.getByText('Excellent coordination on site.')).toBeVisible();

    await page.goto('/admin/incidents');
    await expect(page.getByRole('heading', { name: 'Incident review' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Equipment supply delay' })).toBeVisible();
    await page.getByLabel('Review note (optional)').fill('Followed up with logistics supplier.');
    await page.getByRole('button', { name: 'Mark reviewed' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Incident marked reviewed.' })).toBeVisible();
    expect(state.incidentReviews).toBe(1);
  });
});

interface MockSlicesOptions {
  role: 'Admin' | 'ChiefLeader' | 'Leader' | null;
}

async function mockRemainingSlicesApi(page: Page, options: MockSlicesOptions) {
  let currentRole = options.role;
  let registrations = 0;
  let approvals = 0;
  let cancellations = 0;
  let notificationsRead = 0;
  let feedbackSubmissions = 0;
  let incidentSubmissions = 0;
  let incidentReviews = 0;
  let accountActive = false;
  let notificationReadAt: string | null = null;
  let submittedRating = false;
  let incidentReviewed = false;

  await page.route('**/api/**', async (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;

    if (path === '/api/session/antiforgery') {
      return json(route, 200, { requestToken: 'test-xsrf-token' });
    }

    if (path === '/api/session/me') {
      if (!currentRole) {
        return json(route, 401, { code: 'unauthenticated', title: 'Authentication is required.', status: 401, traceId: 'e2e' });
      }
      return json(route, 200, {
        userId: '10000000-0000-0000-0000-000000000001',
        userName: currentRole === 'Leader' ? 'leader.one' : 'admin',
        roles: [currentRole],
      });
    }

    if (path === '/api/notifications/unread-count') {
      return json(route, 200, { count: notificationReadAt ? 0 : 1 });
    }

    if (path === '/api/notifications') {
      return json(route, 200, [
        {
          id: '50000000-0000-0000-0000-000000000001',
          eventId: targetEventId,
          notificationType: 'EventPublished',
          contextPayload: JSON.stringify({ eventTitle: 'Cairo River Cleanup' }),
          createdAt: '2026-10-20T09:00:00Z',
          readAt: notificationReadAt,
        },
      ]);
    }

    if (path === '/api/notifications/50000000-0000-0000-0000-000000000001/read' && request.method() === 'POST') {
      notificationsRead += 1;
      notificationReadAt = '2026-10-22T12:00:00Z';
      return route.fulfill({ status: 204 });
    }

    if (path === '/api/accounts/register' && request.method() === 'POST') {
      registrations += 1;
      return json(route, 202, { status: 'PendingApproval' });
    }

    if (path === '/api/admin/accounts' && request.method() === 'GET') {
      return json(route, 200, {
        items: [
          {
            userId: '60000000-0000-0000-0000-000000000001',
            userName: 'new.volunteer',
            email: 'new.volunteer@localhost.invalid',
            isActive: accountActive,
            roles: ['Leader'],
          },
        ],
        page: 1,
        pageSize: 50,
        hasMore: false,
      });
    }

    if (path === '/api/admin/accounts/60000000-0000-0000-0000-000000000001/approve' && request.method() === 'POST') {
      approvals += 1;
      accountActive = true;
      return json(route, 200, { userId: '60000000-0000-0000-0000-000000000001', isActive: true });
    }

    if (path === '/api/dashboard/events' && request.method() === 'GET') {
      return json(route, 200, [
        {
          eventId: targetEventId,
          title: 'Cairo River Cleanup',
          status: 'Published',
          startDateTime: '2026-11-01T08:00:00Z',
          endDateTime: '2026-11-01T12:00:00Z',
          location: 'Zamalek Promenade',
          inProgress: false,
          pendingAttendance: true,
          activeRosterCount: 4,
          availableUnassignedCount: 2,
        },
      ]);
    }

    if (path === `/api/events/${targetEventId}/cancellation` && request.method() === 'GET') {
      return json(route, 200, {
        eventId: targetEventId,
        title: 'Cairo River Cleanup',
        status: 'Published',
        version: 'AAAAAAAAB9E=',
      });
    }

    if (path === `/api/events/${targetEventId}/cancellation` && request.method() === 'POST') {
      cancellations += 1;
      return json(route, 200, { eventId: targetEventId, status: 'Cancelled' });
    }

    if (path === '/api/profile/me' && request.method() === 'GET') {
      return json(route, 200, {
        userId: targetLeaderId,
        userName: 'leader.one',
        eventsAttended: 3,
        noShows: 0,
        selfCancellations: 1,
        events: [
          {
            assignmentId: '80000000-0000-0000-0000-000000000001',
            eventId: targetEventId,
            eventTitle: 'Cairo River Cleanup',
            effectiveEventDate: '2026-11-01',
            staffingCycleNumber: 1,
            isCurrentCycle: true,
            assignmentStatus: 'Active',
            assignedAt: '2026-10-18T10:00:00Z',
            cancelledAt: null,
            selfCancelled: false,
            attendanceOutcome: 'Attended',
          },
        ],
      });
    }

    if (path === `/api/events/${targetEventId}/feedback/context` && request.method() === 'GET') {
      return json(route, 200, {
        eventId: targetEventId,
        eventTitle: 'Cairo River Cleanup',
        canRateEvent: false,
        canRateLeaders: true,
        leaders: [{ userId: targetLeaderId, userName: 'leader.one' }],
        ownFeedback: submittedRating
          ? [{ id: '91000000-0000-0000-0000-000000000001', kind: 'Leader', leaderUserId: targetLeaderId, rating: 5, comment: 'Excellent coordination on site.' }]
          : [],
      });
    }

    if (path === `/api/events/${targetEventId}/feedback/leaders/${targetLeaderId}` && request.method() === 'POST') {
      feedbackSubmissions += 1;
      submittedRating = true;
      return json(route, 201, { id: '91000000-0000-0000-0000-000000000001' });
    }

    if (path === '/api/admin/feedback' && request.method() === 'GET') {
      return json(route, 200, {
        items: [
          {
            id: '91000000-0000-0000-0000-000000000001',
            eventTitle: 'Cairo River Cleanup',
            kind: 'Leader',
            raterUserName: 'chief.one',
            leaderUserName: 'leader.one',
            rating: 5,
            comment: 'Excellent coordination on site.',
            submittedAt: '2026-10-22T12:00:00Z',
          },
        ],
        page: 1,
        pageSize: 50,
        hasMore: false,
      });
    }

    if (path === `/api/events/${targetEventId}/incidents` && request.method() === 'GET') {
      return json(route, 200, [
        {
          id: '92000000-0000-0000-0000-000000000001',
          title: 'Equipment supply delay',
          description: 'Gloves and safety vests arrived 20 minutes after check-in.',
          occurredAt: '2026-10-22T09:15:00Z',
          reportedAt: '2026-10-22T10:00:00Z',
          reviewedAt: incidentReviewed ? '2026-10-22T12:30:00Z' : null,
          reviewNote: incidentReviewed ? 'Followed up with logistics supplier.' : null,
        },
      ]);
    }

    if (path === `/api/events/${targetEventId}/incidents` && request.method() === 'POST') {
      incidentSubmissions += 1;
      return json(route, 201, { id: '92000000-0000-0000-0000-000000000001' });
    }

    if (path === '/api/admin/incidents' && request.method() === 'GET') {
      return json(route, 200, {
        items: [
          {
            id: '92000000-0000-0000-0000-000000000001',
            eventTitle: 'Cairo River Cleanup',
            reporterUserName: 'chief.one',
            title: 'Equipment supply delay',
            description: 'Gloves and safety vests arrived 20 minutes after check-in.',
            occurredAt: '2026-10-22T09:15:00Z',
            reportedAt: '2026-10-22T10:00:00Z',
            reviewedAt: incidentReviewed ? '2026-10-22T12:30:00Z' : null,
            reviewNote: incidentReviewed ? 'Followed up with logistics supplier.' : null,
          },
        ],
        page: 1,
        pageSize: 50,
        hasMore: false,
      });
    }

    if (path === '/api/admin/incidents/92000000-0000-0000-0000-000000000001/review' && request.method() === 'POST') {
      incidentReviews += 1;
      incidentReviewed = true;
      return json(route, 200, { id: '92000000-0000-0000-0000-000000000001' });
    }

    return route.fallback();
  });

  return {
    setRole(role: 'Admin' | 'ChiefLeader' | 'Leader' | null) { currentRole = role; },
    get registrations() { return registrations; },
    get approvals() { return approvals; },
    get cancellations() { return cancellations; },
    get notificationsRead() { return notificationsRead; },
    get feedbackSubmissions() { return feedbackSubmissions; },
    get incidentSubmissions() { return incidentSubmissions; },
    get incidentReviews() { return incidentReviews; },
  };
}

function json(route: Route, status: number, body: unknown) {
  return route.fulfill({
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}
