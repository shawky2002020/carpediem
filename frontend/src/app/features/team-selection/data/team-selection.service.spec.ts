import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TeamSelectionService } from './team-selection.service';
import {
  TeamAssignmentResponse,
  TeamSelectionContext,
} from './team-selection.model';

describe('TeamSelectionService', () => {
  let service: TeamSelectionService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        TeamSelectionService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(TeamSelectionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('retrieves team selection context for event', () => {
    const eventId = '11111111-1111-1111-1111-111111111111';
    const mockContext: TeamSelectionContext = {
      eventId,
      staffingCycleId: '22222222-2222-2222-2222-222222222222',
      eventTitle: 'Youth Marathon 2026',
      startDateTime: '2026-10-15T08:00:00Z',
      endDateTime: '2026-10-15T16:00:00Z',
      location: 'Zamalek, Cairo',
      isWindowOpen: true,
      availableCandidates: [
        {
          userId: '33333333-3333-3333-3333-333333333333',
          userName: 'ahmed_leader',
          declaredAt: '2026-10-01T10:00:00Z',
        },
      ],
      activeTeam: [
        {
          assignmentId: '44444444-4444-4444-4444-444444444444',
          userId: '55555555-5555-5555-5555-555555555555',
          userName: 'sara_leader',
          assignedAt: '2026-10-01T09:30:00Z',
          version: 'AAAAAAA=',
        },
      ],
    };

    service.getTeamSelectionContext(eventId).subscribe(res => {
      expect(res).toEqual(mockContext);
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/team-selection`);
    expect(req.request.method).toBe('GET');
    req.flush(mockContext);
  });

  it('submits volunteer selection request', () => {
    const eventId = '11111111-1111-1111-1111-111111111111';
    const candidateUserId = '33333333-3333-3333-3333-333333333333';
    const mockResponse: TeamAssignmentResponse = {
      id: '66666666-6666-6666-6666-666666666666',
      eventId,
      staffingCycleId: '22222222-2222-2222-2222-222222222222',
      userId: candidateUserId,
      status: 'Active',
      assignedAt: '2026-10-01T12:00:00Z',
      assignedByUserId: '77777777-7777-7777-7777-777777777777',
      version: 'BBBBBBB=',
    };

    service.selectVolunteer(eventId, candidateUserId).subscribe(res => {
      expect(res).toEqual(mockResponse);
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/team-selection`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ candidateUserId });
    req.flush(mockResponse);
  });
});
