import { TestBed } from '@angular/core/testing';
import { DraftEventFormComponent } from './draft-event-form.component';

describe('DraftEventFormComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [DraftEventFormComponent] }).compileComponents();
  });

  it('labels Cairo times and exposes validation after a missing-field submission', async () => {
    const fixture = TestBed.createComponent(DraftEventFormComponent);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.textContent).toContain('Africa/Cairo');
    expect(root.querySelector('label[for="title"]')).not.toBeNull();
    (root.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(root.textContent).toContain('Review the highlighted fields.');
    expect(root.textContent).toContain('Title is required.');
  });

  it('rejects whitespace-only required text without emitting a save', () => {
    const fixture = TestBed.createComponent(DraftEventFormComponent);
    const save = vi.fn();
    fixture.componentInstance.saveDraft.subscribe(save);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    setValue(root, 'title', ' \u2003 ');
    setValue(root, 'location', 'Cairo');
    setValue(root, 'startLocal', '2026-10-10T14:30');
    setValue(root, 'endLocal', '2026-10-10T16:30');
    fixture.detectChanges();

    (root.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    expect(save).not.toHaveBeenCalled();
  });

  it('emits valid fields with optional Notes omitted as null', () => {
    const fixture = TestBed.createComponent(DraftEventFormComponent);
    const save = vi.fn();
    fixture.componentInstance.saveDraft.subscribe(save);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    setValue(root, 'title', '  Beach cleanup  ');
    setValue(root, 'location', 'North shore');
    setValue(root, 'startLocal', '2026-10-10T14:30');
    setValue(root, 'endLocal', '2026-10-10T16:30');
    fixture.detectChanges();
    (root.querySelector('button[type="submit"]') as HTMLButtonElement).click();

    expect(save).toHaveBeenCalledWith({
      title: '  Beach cleanup  ',
      start: { localDateTime: '2026-10-10T14:30', offset: null },
      end: { localDateTime: '2026-10-10T16:30', offset: null },
      location: 'North shore',
      notes: null,
    });
  });

  it.each([
    ['2026-10-10T14:30', '2026-10-10T14:30'],
    ['2026-10-10T16:30', '2026-10-10T14:30'],
  ])('rejects a non-increasing date range (%s to %s)', (start, end) => {
    const fixture = TestBed.createComponent(DraftEventFormComponent);
    const save = vi.fn();
    fixture.componentInstance.saveDraft.subscribe(save);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    setValue(root, 'title', 'Beach cleanup');
    setValue(root, 'location', 'North shore');
    setValue(root, 'startLocal', start);
    setValue(root, 'endLocal', end);
    fixture.detectChanges();

    (root.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    expect(save).not.toHaveBeenCalled();
  });

  function setValue(root: HTMLElement, id: string, value: string): void {
    const input = root.querySelector(`#${id}`) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }
});
