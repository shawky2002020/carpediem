import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ReplacementAssignmentService } from './replacement-assignment.service';
import {
  AssignReplacementLeaderRequest,
  ReplacementAssignmentContextResponse,
  TeamAssignmentResponse,
} from './replacement-assignment.model';

describe('ReplacementAssignmentService', () => {
  let service: ReplacementAssignmentService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ReplacementAssignmentService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(ReplacementAssignmentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('retrieves replacement context for published event', () => {
    const eventId = '11111111-1111-1111-1111-111111111111';
    const mockContext: ReplacementAssignmentContextResponse = {
      eventId,
      eventTitle: 'Cairo Tech Summit 2026',
      staffingCycleId: '22222222-2222-2222-2222-222222222222',
      startDateTime: '2026-10-20T09:00:00Z',
      endDateTime: '2026-10-20T17:00:00Z',
      location: 'New Cairo Convention Center',
      isWindowOpen: true,
      activeTeam: [
        {
          assignmentId: '33333333-3333-3333-3333-333333333333',
          userId: '44444444-4444-4444-4444-444444444444',
          userName: 'ahmed_leader',
          status: 'Active',
          assignedAt: '2026-10-01T10:00:00Z',
          version: 'AQIDBAUGCAk=',
        },
      ],
      cancelledAssignments: [
        {
          assignmentId: '55555555-5555-5555-5555-555555555555',
          userId: '66666666-6666-6666-6666-666666666666',
          userName: 'mariam_leader',
          status: 'Cancelled',
          cancelledAt: '2026-10-02T08:00:00Z',
          cancellationReason: 'Academic conflict',
        },
      ],
      candidates: [
        {
          userId: '77777777-7777-7777-7777-777777777777',
          userName: 'tarek_leader',
          availabilityStatus: 'Available',
          declaredAt: '2026-10-01T11:00:00Z',
        },
      ],
    };

    service.getReplacementContext(eventId).subscribe(res => {
      expect(res).toEqual(mockContext);
      expect(res.activeTeam.length).toBe(1);
      expect(res.cancelledAssignments.length).toBe(1);
      expect(res.candidates.length).toBe(1);
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/replacement-assignment`);
    expect(req.request.method).toBe('GET');
    expect(req.request.withCredentials).toBe(true);
    req.flush(mockContext);
  });

  it('submits replacement leader assignment request', () => {
    const eventId = '11111111-1111-1111-1111-111111111111';
    const payload: AssignReplacementLeaderRequest = {
      candidateUserId: '77777777-7777-7777-7777-777777777777',
      replacedAssignmentId: '55555555-5555-5555-5555-555555555555',
      justification: 'Replacement for dropped slot',
    };

    const mockResponse: TeamAssignmentResponse = {
      id: '88888888-8888-8888-8888-888888888888',
      eventId,
      staffingCycleId: '22222222-2222-2222-2222-222222222222',
      userId: payload.candidateUserId,
      status: 'Active',
      assignedAt: '2026-10-02T08:30:00Z',
      assignedByUserId: '99999999-9999-9999-9999-999999999999',
      version: 'AQAAAAAAAAA=',
    };

    service.assignReplacementLeader(eventId, payload).subscribe(res => {
      expect(res).toEqual(mockResponse);
      expect(res.status).toBe('Active');
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/replacement-assignment`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    expect(req.request.withCredentials).toBe(true);
    req.flush(mockResponse);
  });
});
