import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { DraftEventFormComponent } from '../../components/draft-event-form.component';
import { DraftEvent, DraftEventsService, DraftWrite } from '../../data/draft-events.service';
import { DraftEditComponent } from './draft-edit.component';
import { ChiefLeaderAppointmentService } from '../../../chief-leader-appointment/data/chief-leader-appointment.service';

describe('DraftEditComponent', () => {
  const draft: DraftEvent = {
    id: 'event-1',
    title: 'Beach cleanup',
    start: { localDateTime: '2026-10-10T14:30:00', offset: '+03:00' },
    end: { localDateTime: '2026-10-10T16:30:00', offset: '+03:00' },
    startDateTimeUtc: '2026-10-10T11:30:00Z',
    endDateTimeUtc: '2026-10-10T13:30:00Z',
    location: 'North shore',
    notes: 'Bring gloves',
    status: 'Draft',
    version: 'AAAAAAAAB9E=',
  };

  it('loads a Draft into the Cairo authoring form and refreshes its version after save', async () => {
    const pending = new Subject<DraftEvent>();
    const updateDraft = vi.fn(() => pending.asObservable());
    const fixture = await createFixture({ getDraft: vi.fn(() => of(draft)), updateDraft });
    const form = fixture.debugElement.children.find(child => child.componentInstance instanceof DraftEventFormComponent)!
      .componentInstance as DraftEventFormComponent;

    expect(form.form.controls.title.value).toBe('Beach cleanup');
    expect(form.form.controls.startLocal.value).toBe('2026-10-10T14:30:00');

    const write: DraftWrite = {
      title: 'Updated cleanup', start: draft.start, end: draft.end,
      location: draft.location, notes: draft.notes,
    };
    form.saveDraft.emit(write);
    const updated: DraftEvent = {
      ...draft,
      title: write.title,
      location: write.location,
      notes: write.notes,
      version: 'AAAAAAAAB9I=',
    };
    pending.next(updated);
    pending.complete();
    fixture.detectChanges();

    expect(updateDraft).toHaveBeenCalledWith('event-1', { ...write, version: 'AAAAAAAAB9E=' });
    expect(fixture.componentInstance.draft()?.version).toBe('AAAAAAAAB9I=');
    expect(fixture.nativeElement.textContent).toContain('Draft Event saved.');
  });

  it('keeps entered values available when a stale save is rejected', async () => {
    const updateDraft = vi.fn(() => throwError(() => ({
      status: 409,
      error: { type: '', title: 'The Draft changed.', status: 409, code: 'stale_draft', traceId: 'trace-1' },
    })));
    const fixture = await createFixture({ getDraft: vi.fn(() => of(draft)), updateDraft });
    const form = fixture.debugElement.children.find(child => child.componentInstance instanceof DraftEventFormComponent)!
      .componentInstance as DraftEventFormComponent;
    form.form.controls.title.setValue('My unsaved title');
    form.saveDraft.emit({
      title: 'My unsaved title', start: draft.start, end: draft.end,
      location: draft.location, notes: draft.notes,
    });
    fixture.detectChanges();

    expect(form.form.controls.title.value).toBe('My unsaved title');
    expect(fixture.nativeElement.textContent).toContain('The Draft changed since you opened it');
  });

  it('disables publish and shows prerequisite message when chief leader is not active', async () => {
    const fixture = await createFixture(
      { getDraft: vi.fn(() => of(draft)) },
      { currentAppointment: null }
    );

    const publishBtn = fixture.nativeElement.querySelector('.btn-publish');
    expect(publishBtn).toBeTruthy();
    expect(publishBtn.disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('An accepted Chief Leader is required before publishing');
  });

  it('enables publish button when an active chief leader is present and confirms before publishing', async () => {
    const activeAppointment = {
      id: 'app-1', eventId: 'event-1',
      candidate: { userId: 'user-1', userName: 'chief.one' },
      status: 'Active' as const, appointedAt: '2026-10-01T10:00:00Z',
      appointedByUserId: 'admin-1', version: 'ver-1',
    };
    const publishDraftEvent = vi.fn(() => of({
      id: 'event-1', title: 'Beach cleanup',
      startDateTime: '2026-10-10T11:30:00Z', endDateTime: '2026-10-10T13:30:00Z',
      location: 'North shore', notes: null, status: 'Published' as const,
      activeChiefLeader: { userId: 'user-1', userName: 'chief.one', assignmentId: 'app-1' },
      version: 'AAAAAAAAB9M=', publishedAt: '2026-10-01T12:00:00Z',
    }));

    const fixture = await createFixture(
      { getDraft: vi.fn(() => of(draft)), publishDraftEvent },
      { currentAppointment: activeAppointment }
    );

    const publishBtn = fixture.nativeElement.querySelector('.btn-publish');
    expect(publishBtn.disabled).toBe(false);

    // Click publish button to open confirmation dialog
    publishBtn.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.confirm-publish-card')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Are you sure you want to publish now?');

    // Confirm publication
    const confirmBtn = fixture.nativeElement.querySelector('.confirm-actions .btn-primary');
    confirmBtn.click();
    fixture.detectChanges();

    expect(publishDraftEvent).toHaveBeenCalledWith('event-1', 'AAAAAAAAB9E=');
    expect(fixture.componentInstance.draft()?.status).toBe('Published');
    expect(fixture.nativeElement.textContent).toContain('Event published successfully');
  });

  async function createFixture(
    service: Partial<DraftEventsService>,
    appointmentContext: { currentAppointment?: any } = {}
  ) {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [DraftEditComponent],
      providers: [
        provideRouter([]),
        { provide: DraftEventsService, useValue: {
          getDraft: vi.fn(() => of(draft)),
          updateDraft: vi.fn(),
          publishDraftEvent: vi.fn(),
          ...service,
        } },
        { provide: ChiefLeaderAppointmentService, useValue: {
          getContext: vi.fn(() => of({
            eventId: 'event-1', eventTitle: 'Beach cleanup', eventStatus: 'Draft',
            currentAppointment: null, declinedHistory: [], candidates: [],
            ...appointmentContext,
          })),
        } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'event-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(DraftEditComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }
});
