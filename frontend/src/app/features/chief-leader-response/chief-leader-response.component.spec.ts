import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { ChiefLeaderResponseComponent } from './chief-leader-response.component';
import { ChiefLeaderResponse, ChiefLeaderResponseService } from './chief-leader-response.service';

describe('ChiefLeaderResponseComponent', () => {
  const pending: ChiefLeaderResponse = {
    id: 'assignment-1', eventId: 'event-1', eventTitle: 'Beach cleanup',
    startDateTime: '2026-10-10T11:30:00Z', endDateTime: '2026-10-10T13:30:00Z',
    location: 'North shore', status: 'Pending', appointedAt: '2026-09-22T09:00:00Z',
    acceptedAt: null, declinedAt: null, version: 'AQIDBA==',
  };

  it('shows a candidate-only Pending list with a route to each appointment', async () => {
    const fixture = await createFixture(null, {
      listPending: vi.fn(() => of([pending])), get: vi.fn(), respond: vi.fn(),
    });
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Beach cleanup');
    expect(element.textContent).toContain('Pending');
    expect(element.querySelector<HTMLAnchorElement>('.appointment-card')?.getAttribute('href'))
      .toBe('/my-appointments/assignment-1');
  });

  it('keeps a clear loading state until the Pending list arrives', async () => {
    const pendingList = new Subject<ChiefLeaderResponse[]>();
    const fixture = await createFixture(null, {
      listPending: vi.fn(() => pendingList.asObservable()), get: vi.fn(), respond: vi.fn(),
    });
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Loading appointments');
    pendingList.next([]);
    pendingList.complete();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No pending appointments');
  });

  it('accepts once while saving and shows Active result', async () => {
    const result = new Subject<ChiefLeaderResponse>();
    const respond = vi.fn(() => result.asObservable());
    const fixture = await createFixture('assignment-1', {
      listPending: vi.fn(), get: vi.fn(() => of(pending)), respond,
    });
    const element = fixture.nativeElement as HTMLElement;
    const accept = element.querySelector<HTMLButtonElement>('.primary')!;
    accept.click();
    fixture.detectChanges();
    expect(accept.disabled).toBe(true);
    element.querySelector<HTMLButtonElement>('.secondary')!.click();
    expect(respond).toHaveBeenCalledTimes(1);
    result.next({ ...pending, status: 'Active', acceptedAt: '2026-09-22T10:00:00Z' });
    result.complete();
    fixture.detectChanges();
    expect(element.textContent).toContain('Appointment accepted');
    expect(element.querySelector('.actions')).toBeNull();
  });

  it('retains Event context and allows current-state check after an uncertain save', async () => {
    const get = vi.fn(() => of(pending));
    const fixture = await createFixture('assignment-1', {
      listPending: vi.fn(), get,
      respond: vi.fn(() => throwError(() => ({ status: 0 }))),
    });
    const element = fixture.nativeElement as HTMLElement;
    element.querySelector<HTMLButtonElement>('.primary')!.click();
    fixture.detectChanges();
    expect(element.textContent).toContain('Beach cleanup');
    expect(element.textContent).toContain('Check the current state');
    element.querySelector<HTMLButtonElement>('.message button')!.click();
    fixture.detectChanges();
    expect(get).toHaveBeenCalledTimes(2);
  });

  it('shows a safe not-found state for an unknown appointment', async () => {
    const fixture = await createFixture('missing', {
      listPending: vi.fn(),
      get: vi.fn(() => throwError(() => ({
        status: 404, error: { title: 'Appointment was not found.', code: 'appointment_not_found' },
      }))),
      respond: vi.fn(),
    });
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('This appointment could not be found.');
    expect(element.querySelector('.actions')).toBeNull();
  });

  async function createFixture(id: string | null,
    service: Pick<ChiefLeaderResponseService, 'listPending' | 'get' | 'respond'>) {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [ChiefLeaderResponseComponent],
      providers: [
        provideRouter([]),
        { provide: ChiefLeaderResponseService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => id } } } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(ChiefLeaderResponseComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }
});
