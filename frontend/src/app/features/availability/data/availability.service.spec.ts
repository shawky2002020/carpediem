import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AvailabilityService } from './availability.service';
import { AvailabilityDeclarationResult, MyAvailabilityResponse } from './availability.model';

describe('AvailabilityService', () => {
  let service: AvailabilityService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AvailabilityService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(AvailabilityService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('retrieves current availability for event', () => {
    const eventId = '11111111-1111-1111-1111-111111111111';
    const mockResponse: MyAvailabilityResponse = {
      eventId,
      staffingCycleId: '22222222-2222-2222-2222-222222222222',
      response: 'Available',
      declaredAt: '2026-10-01T20:00:00Z',
      version: 'AAAAAAA=',
      isWindowOpen: true,
      eventStatus: 'Published',
      isChiefLeaderOnEvent: false,
    };

    service.getMyAvailability(eventId).subscribe(res => {
      expect(res).toEqual(mockResponse);
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/availability/me`);
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('submits availability declaration without version for initial declaration', () => {
    const eventId = '11111111-1111-1111-1111-111111111111';
    const mockResult: AvailabilityDeclarationResult = {
      id: '33333333-3333-3333-3333-333333333333',
      eventId,
      staffingCycleId: '22222222-2222-2222-2222-222222222222',
      userId: '44444444-4444-4444-4444-444444444444',
      response: 'Available',
      declaredAt: '2026-10-01T20:00:00Z',
      version: 'AAAAAAA=',
    };

    service.declareAvailability(eventId, 'Available').subscribe(res => {
      expect(res).toEqual(mockResult);
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/availability`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ response: 'Available' });
    expect(req.request.headers.has('If-Match')).toBe(false);
    req.flush(mockResult);
  });

  it('submits availability update with If-Match header when version is provided', () => {
    const eventId = '11111111-1111-1111-1111-111111111111';
    const version = 'BBBBBBB=';
    const mockResult: AvailabilityDeclarationResult = {
      id: '33333333-3333-3333-3333-333333333333',
      eventId,
      staffingCycleId: '22222222-2222-2222-2222-222222222222',
      userId: '44444444-4444-4444-4444-444444444444',
      response: 'NotAvailable',
      declaredAt: '2026-10-01T20:05:00Z',
      version: 'CCCCCCC=',
    };

    service.declareAvailability(eventId, 'NotAvailable', version).subscribe(res => {
      expect(res).toEqual(mockResult);
    });

    const req = httpMock.expectOne(`/api/events/${eventId}/availability`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ response: 'NotAvailable' });
    expect(req.request.headers.get('If-Match')).toBe(`"${version}"`);
    req.flush(mockResult);
  });
});
