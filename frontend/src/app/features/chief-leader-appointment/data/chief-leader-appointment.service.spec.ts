import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionService } from '../../../core/auth/session.service';
import { apiInterceptor } from '../../../core/http/api.interceptor';
import { ChiefLeaderAppointmentService } from './chief-leader-appointment.service';

describe('ChiefLeaderAppointmentService', () => {
  let service: ChiefLeaderAppointmentService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiInterceptor])), provideHttpClientTesting()],
    });
    service = TestBed.inject(ChiefLeaderAppointmentService);
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(SessionService).antiforgeryToken.set('test-token');
  });

  afterEach(() => http.verify());

  it('reads the appointment context with credentials', () => {
    service.getContext('event-1').subscribe();
    const request = http.expectOne('/api/events/drafts/event-1/chief-leader-appointment');
    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBe(true);
    request.flush({ eventId: 'event-1', eventTitle: 'Event', eventStatus: 'Draft', candidates: [] });
  });

  it('posts one appointment intent with antiforgery and idempotency', () => {
    service.appoint('event-1', 'candidate-1', 'request-1').subscribe();
    const request = http.expectOne('/api/events/drafts/event-1/chief-leader-appointment');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ candidateUserId: 'candidate-1' });
    expect(request.request.headers.get('Idempotency-Key')).toBe('request-1');
    expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('test-token');
    request.flush({ id: 'assignment-1' });
  });
});
