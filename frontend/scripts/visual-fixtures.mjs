export const eventId = 'f1000000-0000-4000-8000-000000000001';
const cycle = 'f1000000-0000-4000-8000-000000000002';
const date = '2026-12-12T07:00:00Z';
const location = 'Mango Tree Campsite';
const eventTitle = 'Winter camp orientation';
const version = 'AAAAAAA=';
const event = { eventId, eventTitle, location, staffingCycleId: cycle, startDateTime: date, endDateTime: '2026-12-12T13:00:00Z' };
const openEvent = { ...event, isWindowOpen: true };
const user = (index, userName) => ({ userId: `f2000000-0000-4000-8000-00000000000${index}`, userName });
const assigned = [
  { ...user(1, 'Mariam Hassan'), assignmentId: 'f3000000-0000-4000-8000-000000000001', assignedAt: date, status: 'Active', version },
  { ...user(2, 'Youssef Ali'), assignmentId: 'f3000000-0000-4000-8000-000000000002', assignedAt: date, status: 'Active', version },
  { ...user(3, 'Nour Samir'), assignmentId: 'f3000000-0000-4000-8000-000000000003', assignedAt: date, status: 'Active', version },
];
const candidates = [user(4, 'Salma Adel'), user(5, 'Karim Tarek')].map(person => ({ ...person, declaredAt: '2026-11-23T11:00:00Z', availabilityStatus: 'Available' }));
const draft = {
  id: eventId, title: 'December volunteer briefing', location, status: 'Draft',
  start: { localDateTime: '2026-12-12T09:00:00', offset: '+02:00' },
  end: { localDateTime: '2026-12-12T15:00:00', offset: '+02:00' },
  startDateTimeUtc: date, endDateTimeUtc: event.endDateTime, notes: 'Review check-in, the roster and the equipment list.', version,
};
const appointment = { ...event, id: eventId, status: 'Pending', appointedAt: '2026-11-23T11:00:00Z', acceptedAt: null, declinedAt: null, version };
const published = { ...event, title: eventTitle, status: 'Published', inProgress: false, pendingAttendance: false, activeRosterCount: 3, availableUnassignedCount: 2 };
const inbox = [{
  id: eventId, eventId, notificationType: 'EventPublished',
  contextPayload: JSON.stringify({ eventTitle }), createdAt: '2026-11-24T09:00:00Z', readAt: null,
}];
const data = new Map([
  ['/api/session/antiforgery', { requestToken: 'visual-fixture' }],
  ['/api/dashboard/events', [
    published,
    { ...published, eventId: cycle, title: 'Outdoor skills day', location: 'Wadi Degla', pendingAttendance: true, availableUnassignedCount: 0 },
    { ...published, eventId: assigned[0].assignmentId, title: draft.title, status: 'Draft', activeRosterCount: 0, availableUnassignedCount: 0 },
  ]],
  ['/api/events/drafts', { items: [draft, { ...draft, id: cycle, title: 'Trail team briefing', location: 'Wadi Degla' }], page: 1, pageSize: 20 }],
  [`/api/events/drafts/${eventId}`, draft],
  [`/api/events/drafts/${eventId}/chief-leader-appointment`, {
    eventId, eventTitle: draft.title, eventStatus: 'Draft', currentAppointment: null,
    declinedHistory: [], candidates: [user(4, 'Salma Adel'), user(5, 'Karim Tarek')],
  }],
  ['/api/chief-leader/appointments', [appointment]],
  [`/api/chief-leader/appointments/${eventId}`, appointment],
  [`/api/events/${eventId}/team-selection`, { ...openEvent, availableCandidates: candidates, activeTeam: assigned }],
  [`/api/events/${eventId}/direct-assignment/candidates`, { ...openEvent, candidates: [{ ...user(6, 'Hana Omar'), availabilityStatus: 'NoResponse', declaredAt: null }] }],
  [`/api/events/${eventId}/replacement-assignment`, {
    ...openEvent, activeTeam: assigned, candidates,
    cancelledAssignments: [{ ...user(7, 'Farida Habib'), assignmentId: cycle, status: 'Cancelled', cancelledAt: '2026-11-23T14:00:00Z', cancellationReason: 'Schedule conflict' }],
  }],
  [`/api/events/${eventId}/attendance`, {
    ...event, status: 'Published', isCompleted: false, isAllFinalized: false,
    entries: assigned.map(person => ({
      teamAssignmentId: person.assignmentId, volunteerUserId: person.userId, volunteerName: person.userName,
      attendanceOutcomeId: null, outcome: null, finalizedAt: null, version: null,
    })),
  }],
  [`/api/events/${eventId}/availability/me`, {
    eventId, staffingCycleId: cycle, isWindowOpen: true,
    itinerary: { title: eventTitle, location, startDateTime: event.startDateTime, endDateTime: event.endDateTime },
    response: null, declaredAt: null, version: null, eventStatus: 'Published', isChiefLeaderOnEvent: false,
  }],
  [`/api/events/${eventId}/my-assignment`, {
    ...event, ...assigned[0], isWindowOpen: true, cancelledAt: null, cancellationReason: null,
  }],
  [`/api/events/${eventId}/cancellation`, { eventId, title: eventTitle, status: 'Published', version }],
  [`/api/events/${eventId}/feedback/context`, {
    eventId, eventTitle, canRateEvent: false, canRateLeaders: true, leaders: [assigned[0]], ownFeedback: [],
  }],
  [`/api/events/${eventId}/incidents`, []],
  ['/api/admin/accounts', {
    items: [{ ...assigned[0], email: 'mariam@localhost.invalid', isActive: true, roles: ['Leader'] }],
    page: 1, pageSize: 50, hasMore: false,
  }],
  ['/api/admin/feedback', {
    items: [{
      id: eventId, eventTitle, kind: 'LeaderRating', raterUserName: 'sara.ibrahim', leaderUserName: 'Mariam Hassan',
      rating: 5, comment: 'Kept the check-in moving on a busy morning.', submittedAt: date,
    }], page: 1, pageSize: 50, hasMore: false,
  }],
  ['/api/admin/incidents', {
    items: [{
      id: eventId, eventTitle, reporterUserName: 'sara.ibrahim', title: 'Late equipment delivery',
      description: 'The equipment arrived after registration opened.', occurredAt: date, reportedAt: date,
      reviewedAt: null, reviewNote: null,
    }], page: 1, pageSize: 50, hasMore: false,
  }],
  ['/api/notifications', inbox],
  ['/api/notifications/unread-count', { count: 1 }],
  ['/api/profile/me', {
    ...assigned[0], eventsAttended: 1, noShows: 0, selfCancellations: 0,
    events: [{ ...event, assignmentId: assigned[0].assignmentId, staffingCycleNumber: 1, effectiveEventDate: date.slice(0, 10), isCurrentCycle: true, assignmentStatus: 'Active', assignedAt: date, cancelledAt: null, attendanceOutcome: 'Attended', selfCancelled: false }],
  }],
]);
data.set(`/api/leaders/${assigned[0].userId}/history`, data.get('/api/profile/me'));

export async function wireFixtures(page, role, missing = message => { throw new Error(message); }) {
  await page.route('**/api/**', async route => {
    const pathname = new URL(route.request().url()).pathname;
    if (pathname === '/api/session/me') {
      return route.fulfill(role ? { json: { ...user(9, role === 'Leader' ? 'mariam.hassan' : 'sara.ibrahim'), roles: [role] } } : { status: 401, json: { title: 'Sign in required' } });
    }
    if (data.has(pathname)) return route.fulfill({ json: data.get(pathname) });
    missing(`missing visual fixture for ${pathname}`);
    return route.fulfill({ status: 404, json: { title: 'Visual fixture missing.' } });
  });
}
