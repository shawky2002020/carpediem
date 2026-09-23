import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionService } from '../../core/auth/session.service';
import { apiInterceptor } from '../../core/http/api.interceptor';
import { ChiefLeaderResponseService } from './chief-leader-response.service';

describe('ChiefLeaderResponseService', () => {
  let service: ChiefLeaderResponseService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([apiInterceptor])), provideHttpClientTesting()],
    });
    service = TestBed.inject(ChiefLeaderResponseService);
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(SessionService).antiforgeryToken.set('test-token');
  });

  afterEach(() => http.verify());

  it('lists only the signed-in candidate pending appointments', () => {
    service.listPending().subscribe();
    const request = http.expectOne('/api/chief-leader/appointments');
    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBe(true);
    request.flush([]);
  });

  it('sends explicit decision with version and antiforgery token', () => {
    service.respond('assignment-1', 'accept', 'AQIDBA==').subscribe();
    const request = http.expectOne('/api/chief-leader/appointments/assignment-1/accept');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('If-Match')).toBe('"AQIDBA=="');
    expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('test-token');
    request.flush({ id: 'assignment-1', status: 'Active' });
  });
});
