import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { VolunteerAssignmentService } from './volunteer-assignment.service';
import {
  CancelledAssignmentResponse,
  MyAssignmentResponse,
} from './volunteer-assignment.model';

describe('VolunteerAssignmentService', () => {
  let service: VolunteerAssignmentService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        VolunteerAssignmentService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(VolunteerAssignmentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('retrieves volunteer assignment context for event', () => {
    const eventId = '11111111-1111-1111-1111-111111111111';
    const mockAssignment: MyAssignmentResponse = {
      assignmentId: '22222222-2222-2222-2222-222222222222',
      eventId,
      staffingCycleId: '33333333-3333-3333-3333-333333333333',
      userId: '44444444-4444-4444-4444-444444444444',
      userName: 'mona_leader',
      eventTitle: 'Cairo Tech Summit 2026',
      startDateTime: '2026-10-20T09:00:00Z',
      endDateTime: '2026-10-20T17:00:00Z',
      location: 'New Cairo Convention Center',
      status: 'Active',
      assignedAt: '2026-10-02T08:00:00Z',
      cancelledAt: null,
      cancellationReason: null,
      isWindowOpen: true,
      version: 'AQIDBAUGCAk=',
    };

    service.getMyAssignment(eventId).subscribe(res => {
      expect(res).toEqual(mockAssignment);
      expect(res.status).toBe('Active');
      expect(res.isWindowOpen).toBe(true);
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/my-assignment`);
    expect(req.request.method).toBe('GET');
    expect(req.request.withCredentials).toBe(true);
    req.flush(mockAssignment);
  });

  it('submits assignment cancellation with mandatory justification and If-Match header', () => {
    const eventId = '11111111-1111-1111-1111-111111111111';
    const assignmentId = '22222222-2222-2222-2222-222222222222';
    const justification = 'Academic exam conflict on Saturday.';
    const ifMatch = 'AQIDBAUGCAk=';

    const mockResponse: CancelledAssignmentResponse = {
      id: assignmentId,
      eventId,
      staffingCycleId: '33333333-3333-3333-3333-333333333333',
      userId: '44444444-4444-4444-4444-444444444444',
      status: 'Cancelled',
      assignedAt: '2026-10-02T08:00:00Z',
      assignedByUserId: '55555555-5555-5555-5555-555555555555',
      cancellationReason: justification,
      cancelledAt: '2026-10-02T10:00:00Z',
      version: 'CQgHBgUEAwI=',
    };

    service.cancelAssignment(eventId, assignmentId, justification, ifMatch).subscribe(res => {
      expect(res).toEqual(mockResponse);
      expect(res.status).toBe('Cancelled');
      expect(res.cancellationReason).toBe(justification);
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/assignments/${assignmentId}/cancel`);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('If-Match')).toBe(`"${ifMatch}"`);
    expect(req.request.body).toEqual({ justification });
    expect(req.request.withCredentials).toBe(true);
    req.flush(mockResponse);
  });
});
