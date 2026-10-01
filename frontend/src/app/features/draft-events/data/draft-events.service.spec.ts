import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { apiInterceptor } from '../../../core/http/api.interceptor';
import { SessionService } from '../../../core/auth/session.service';
import { DraftEventsService, DraftWrite } from './draft-events.service';

describe('DraftEventsService', () => {
  let service: DraftEventsService;
  let http: HttpTestingController;
  const write: DraftWrite = {
    title: 'Beach cleanup',
    start: { localDateTime: '2026-10-10T14:30', offset: null },
    end: { localDateTime: '2026-10-10T16:30', offset: null },
    location: 'North shore',
    notes: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(DraftEventsService);
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(SessionService).antiforgeryToken.set('test-token');
  });

  afterEach(() => http.verify());

  it('sends one create intent key with credentials and antiforgery', () => {
    const key = '3ee226b8-1d46-4b5d-b6eb-fdb611698f1d';
    service.createDraft(write, key).subscribe();

    const request = http.expectOne('/api/events/drafts');
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBe(true);
    expect(request.request.headers.get('Idempotency-Key')).toBe(key);
    expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('test-token');
    expect(request.request.body).toEqual(write);
    request.flush({ id: 'event-1' });
  });

  it('sends the read version with a complete Draft edit', () => {
    service.updateDraft('event-1', { ...write, version: 'AQIDBA==' }).subscribe();

    const request = http.expectOne('/api/events/drafts/event-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.version).toBe('AQIDBA==');
    request.flush({ id: 'event-1' });
  });

  it('sends If-Match rowversion and antiforgery token when publishing', () => {
    service.publishDraftEvent('event-1', 'AQIDBA==').subscribe();

    const request = http.expectOne('/api/events/drafts/event-1/publish');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('If-Match')).toBe('AQIDBA==');
    expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('test-token');
    request.flush({ id: 'event-1', status: 'Published' });
  });
});
