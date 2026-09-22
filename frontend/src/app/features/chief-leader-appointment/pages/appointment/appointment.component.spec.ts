import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import {
  ChiefLeaderAppointment,
  ChiefLeaderAppointmentContext,
  ChiefLeaderAppointmentService,
} from '../../data/chief-leader-appointment.service';
import { AppointmentComponent } from './appointment.component';

describe('AppointmentComponent', () => {
  const context: ChiefLeaderAppointmentContext = {
    eventId: 'event-1', eventTitle: 'Beach cleanup', eventStatus: 'Draft', currentAppointment: null,
    candidates: [
      { userId: 'chief-1', userName: 'chief.one' },
      { userId: 'chief-2', userName: 'chief.two' },
    ],
  };
  const appointment: ChiefLeaderAppointment = {
    id: 'assignment-1', eventId: 'event-1', candidate: context.candidates[0], status: 'Pending',
    appointedAt: '2026-09-22T09:00:00Z', appointedByUserId: 'admin-1', version: 'AQIDBA==',
  };

  it('selects one candidate, suppresses duplicate submit, and renders Pending success', async () => {
    const pending = new Subject<ChiefLeaderAppointment>();
    const appoint = vi.fn(() => pending.asObservable());
    const fixture = await createFixture({ getContext: vi.fn(() => of(context)), appoint });
    const element = fixture.nativeElement as HTMLElement;
    const choices = element.querySelectorAll<HTMLInputElement>('input[type="radio"]');
    choices[0].click();
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.primary')!.click();
    element.querySelector<HTMLButtonElement>('.primary')!.click();

    expect(appoint).toHaveBeenCalledTimes(1);
    pending.next(appointment);
    pending.complete();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('chief.one');
    expect(fixture.nativeElement.textContent).toContain('Pending');
    expect(fixture.nativeElement.textContent).toContain('awaiting a response');
  });

  it('retries an uncertain appointment with the same candidate and request token', async () => {
    const appoint = vi.fn(() => throwError(() => ({ status: 0 })));
    const fixture = await createFixture({ getContext: vi.fn(() => of(context)), appoint });
    const element = fixture.nativeElement as HTMLElement;
    element.querySelector<HTMLInputElement>('input[type="radio"]')!.click();
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.primary')!.click();
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('[data-testid="retry-appointment"]')!.click();

    expect(appoint).toHaveBeenCalledTimes(2);
    expect(appoint.mock.calls[0]).toEqual(appoint.mock.calls[1]);
  });

  it('shows an empty state when no eligible candidate exists', async () => {
    const fixture = await createFixture({
      getContext: vi.fn(() => of({ ...context, candidates: [] })), appoint: vi.fn(),
    });
    expect(fixture.nativeElement.textContent).toContain('No eligible Chief Leaders');
  });

  it('shows a stable loading state while candidates are being retrieved', async () => {
    const pending = new Subject<ChiefLeaderAppointmentContext>();
    const fixture = await createFixture({ getContext: vi.fn(() => pending.asObservable()), appoint: vi.fn() });

    expect(fixture.nativeElement.textContent).toContain('Loading eligible Chief Leaders');
    pending.next(context);
    pending.complete();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('chief.one');
    expect(fixture.nativeElement.textContent).toContain('chief.two');
  });

  it('renders a reopened Pending appointment without appointment controls', async () => {
    const fixture = await createFixture({
      getContext: vi.fn(() => of({ ...context, currentAppointment: appointment, candidates: [] })),
      appoint: vi.fn(),
    });

    expect(fixture.nativeElement.textContent).toContain('Pending');
    expect(fixture.nativeElement.textContent).toContain('chief.one');
    expect(fixture.nativeElement.querySelector('input[type="radio"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('.primary')).toBeNull();
  });

  it('offers candidate reload after save-time ineligibility and clears stale selection on reload', async () => {
    const getContext = vi.fn(() => of(context));
    const appoint = vi.fn(() => throwError(() => ({
      status: 422,
      error: { title: 'Review the selected candidate.', code: 'candidate_ineligible' },
    })));
    const fixture = await createFixture({ getContext, appoint });
    const element = fixture.nativeElement as HTMLElement;
    element.querySelector<HTMLInputElement>('input[type="radio"]')!.click();
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.primary')!.click();
    fixture.detectChanges();

    expect(element.textContent).toContain('Reload and choose an eligible Chief Leader');
    element.querySelector<HTMLButtonElement>('.message.error button')!.click();
    fixture.detectChanges();
    expect(getContext).toHaveBeenCalledTimes(2);
    expect(element.querySelector<HTMLInputElement>('input[type="radio"]')!.checked).toBe(false);
    expect(element.querySelector<HTMLButtonElement>('.primary')!.disabled).toBe(true);
  });

  it('asks the route guard before leaving an unconfirmed selection', async () => {
    const fixture = await createFixture({ getContext: vi.fn(() => of(context)), appoint: vi.fn() });
    const element = fixture.nativeElement as HTMLElement;
    element.querySelector<HTMLInputElement>('input[type="radio"]')!.click();
    fixture.detectChanges();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const router = TestBed.inject(Router) as unknown as { navigate: ReturnType<typeof vi.fn> };

    expect(fixture.componentInstance.canDeactivate()).toBe(false);
    element.querySelector<HTMLButtonElement>('.secondary')!.click();

    expect(confirm).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/draft-events', 'event-1']);
    confirm.mockRestore();
  });

  async function createFixture(service: Pick<ChiefLeaderAppointmentService, 'getContext' | 'appoint'>) {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AppointmentComponent],
      providers: [
        provideRouter([]),
        { provide: ChiefLeaderAppointmentService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'event-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(AppointmentComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }
});
