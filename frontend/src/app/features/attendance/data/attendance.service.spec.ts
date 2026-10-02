import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AttendanceService } from './attendance.service';
import {
  AttendanceRollSheet,
  FinalizeAttendanceRequest,
  FinalizeAttendanceResponse,
} from './attendance.model';

describe('AttendanceService', () => {
  let service: AttendanceService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AttendanceService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(AttendanceService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('retrieves attendance roll sheet for event', () => {
    const eventId = '11111111-1111-1111-1111-111111111111';
    const mockRollSheet: AttendanceRollSheet = {
      eventId,
      eventTitle: 'Community Gala 2026',
      status: 'Published',
      startDateTime: '2026-10-15T08:00:00Z',
      endDateTime: '2026-10-15T16:00:00Z',
      location: 'Grand Ballroom',
      staffingCycleId: '22222222-2222-2222-2222-222222222222',
      isCompleted: false,
      isAllFinalized: false,
      entries: [
        {
          teamAssignmentId: '33333333-3333-3333-3333-333333333333',
          volunteerUserId: '44444444-4444-4444-4444-444444444444',
          volunteerName: 'Alice Volunteer',
          attendanceOutcomeId: null,
          outcome: null,
          finalizedAt: null,
          version: null,
        },
      ],
    };

    service.getRollSheet(eventId).subscribe(res => {
      expect(res).toEqual(mockRollSheet);
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/attendance`);
    expect(req.request.method).toBe('GET');
    req.flush(mockRollSheet);
  });

  it('finalizes attendance roll call successfully', () => {
    const eventId = '11111111-1111-1111-1111-111111111111';
    const payload: FinalizeAttendanceRequest = {
      entries: [
        {
          teamAssignmentId: '33333333-3333-3333-3333-333333333333',
          outcome: 'Attended',
        },
      ],
    };

    const mockResponse: FinalizeAttendanceResponse = {
      eventId,
      finalizedAt: '2026-10-15T12:00:00Z',
      totalActiveAssignments: 1,
      attendedCount: 1,
      noShowCount: 0,
      unrecordedCount: 0,
    };

    service.finalizeAttendance(eventId, payload).subscribe(res => {
      expect(res).toEqual(mockResponse);
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/attendance`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush(mockResponse);
  });
});
