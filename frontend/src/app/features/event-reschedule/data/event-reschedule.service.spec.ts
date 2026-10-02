import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, beforeEach, afterEach, it, expect } from 'vitest';
import { EventRescheduleService } from './event-reschedule.service';
import { RescheduleEventRequest, RescheduleEventResponse } from './event-reschedule.model';

describe('EventRescheduleService', () => {
  let service: EventRescheduleService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        EventRescheduleService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(EventRescheduleService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should post reschedule request with If-Match header and credentials', () => {
    const eventId = 'test-event-123';
    const version = 'AAAAAAAA+2I=';
    const request: RescheduleEventRequest = {
      newStartDateTime: '2026-11-20T09:00:00Z',
      newEndDateTime: '2026-11-20T17:00:00Z',
      justification: 'Venue unavailable due to maintenance.',
    };

    const mockResponse: RescheduleEventResponse = {
      eventId,
      title: 'Community Gala',
      status: 'Published',
      startDateTime: '2026-11-20T09:00:00Z',
      endDateTime: '2026-11-20T17:00:00Z',
      location: 'Grand Ballroom',
      notes: null,
      activeStaffingCycleId: 'cycle-2',
      cycleNumber: 2,
      version: 'BBBBBBBB+3J=',
      rescheduledAt: '2026-10-02T12:00:00Z',
      notificationsDispatched: 3,
    };

    service.rescheduleEvent(eventId, request, version).subscribe((res) => {
      expect(res).toEqual(mockResponse);
      expect(res.cycleNumber).toBe(2);
      expect(res.notificationsDispatched).toBe(3);
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/reschedule`);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('If-Match')).toBe(`"${version}"`);
    expect(req.request.withCredentials).toBe(true);
    expect(req.request.body).toEqual(request);

    req.flush(mockResponse);
  });
});
