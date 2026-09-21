import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Subject, throwError } from 'rxjs';
import { DraftEventFormComponent } from '../../components/draft-event-form.component';
import { DraftEvent, DraftWrite, DraftEventsService } from '../../data/draft-events.service';
import { DraftCreateComponent } from './draft-create.component';

describe('DraftCreateComponent', () => {
  const write: DraftWrite = {
    title: 'Beach cleanup', start: { localDateTime: '2026-10-10T14:30', offset: null },
    end: { localDateTime: '2026-10-10T16:30', offset: null }, location: 'North shore', notes: null,
  };
  const created = { id: 'event-1', title: write.title, status: 'Draft' } as DraftEvent;

  it('suppresses duplicate submit while saving and navigates after success', async () => {
    const pending = new Subject<DraftEvent>();
    const createDraft = vi.fn(() => pending.asObservable());
    const navigate = vi.fn();
    await TestBed.configureTestingModule({
      imports: [DraftCreateComponent],
      providers: [
        { provide: DraftEventsService, useValue: { createDraft } },
        { provide: Router, useValue: { navigate } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(DraftCreateComponent);
    fixture.detectChanges();
    const form = fixture.debugElement.children.find(child => child.componentInstance instanceof DraftEventFormComponent)!.componentInstance as DraftEventFormComponent;

    form.saveDraft.emit(write);
    form.saveDraft.emit(write);
    fixture.detectChanges();
    expect(createDraft).toHaveBeenCalledTimes(1);
    expect(form.saving).toBe(true);
    pending.next(created);
    pending.complete();
    expect(navigate).toHaveBeenCalledWith(['/draft-events', 'event-1'], expect.anything());
  });

  it('retries an uncertain create with the same key and original payload', async () => {
    const createDraft = vi.fn(() => throwError(() => ({ status: 0 })));
    await TestBed.configureTestingModule({
      imports: [DraftCreateComponent],
      providers: [
        { provide: DraftEventsService, useValue: { createDraft } },
        { provide: Router, useValue: { navigate: vi.fn() } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(DraftCreateComponent);
    fixture.detectChanges();
    const form = fixture.debugElement.children.find(child => child.componentInstance instanceof DraftEventFormComponent)!.componentInstance as DraftEventFormComponent;

    form.saveDraft.emit(write);
    fixture.detectChanges();
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[data-testid="retry-create"]')!.click();

    expect(createDraft).toHaveBeenCalledTimes(2);
    expect(createDraft.mock.calls[0]).toEqual(createDraft.mock.calls[1]);
  });
});
