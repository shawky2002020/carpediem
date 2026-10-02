import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, beforeEach, afterEach, it, expect } from 'vitest';
import { EventCompletionService } from './event-completion.service';
import { CompleteEventResponse } from './event-completion.model';

describe('EventCompletionService', () => {
  let service: EventCompletionService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        EventCompletionService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(EventCompletionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should post complete request with If-Match header and credentials', () => {
    const eventId = 'test-event-123';
    const version = 'AAAAAAAA+2I=';

    const mockResponse: CompleteEventResponse = {
      eventId,
      title: 'Community Gala',
      status: 'Completed',
      startDateTime: '2026-10-01T09:00:00Z',
      endDateTime: '2026-10-01T17:00:00Z',
      location: 'Grand Ballroom',
      notes: null,
      completedAt: '2026-10-02T12:00:00Z',
      version: 'BBBBBBBB+3J=',
    };

    service.completeEvent(eventId, version).subscribe((res) => {
      expect(res).toEqual(mockResponse);
      expect(res.status).toBe('Completed');
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/complete`);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('If-Match')).toBe(`"${version}"`);
    expect(req.request.withCredentials).toBe(true);
    expect(req.request.body).toEqual({});

    req.flush(mockResponse);
  });

  it('should handle already quoted version string properly', () => {
    const eventId = 'test-event-456';
    const version = '"AAAAAAAA+2I="';

    service.completeEvent(eventId, version).subscribe();

    const req = httpMock.expectOne(`/api/events/${eventId}/complete`);
    expect(req.request.headers.get('If-Match')).toBe('"AAAAAAAA+2I="');
    req.flush({});
  });
});
