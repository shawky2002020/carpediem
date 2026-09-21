import { Component, ElementRef, EventEmitter, Input, OnChanges, Output, SimpleChanges, ViewChild } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiProblem } from '../../../core/http/api.interceptor';
import { DraftEvent, DraftWrite } from '../data/draft-events.service';

function requiredText(control: AbstractControl): ValidationErrors | null {
  return typeof control.value === 'string' && control.value.trim().length > 0
    ? null : { requiredText: true };
}

function validDateOrder(control: AbstractControl): ValidationErrors | null {
  const start = control.get('startLocal')?.value as string | undefined;
  const end = control.get('endLocal')?.value as string | undefined;
  if (!start || !end) return null;

  const startOffset = control.get('startOffset')?.value as string | null;
  const endOffset = control.get('endOffset')?.value as string | null;
  // Datetime-local values belong to Africa/Cairo, never the device zone.
  // Lexical ordering is chronological for their normalized ISO shape; when
  // both ambiguous values have explicit offsets, compare their actual instants.
  const startValue = startOffset && endOffset ? Date.parse(`${start}${startOffset}`) : start;
  const endValue = startOffset && endOffset ? Date.parse(`${end}${endOffset}`) : end;
  return startValue < endValue ? null : { dateOrder: true };
}

@Component({
  selector: 'app-draft-event-form',
  imports: [ReactiveFormsModule],
  templateUrl: './draft-event-form.component.html',
  styleUrl: './draft-event-form.component.scss',
})
export class DraftEventFormComponent implements OnChanges {
  @Input() initial: DraftEvent | null = null;
  @Input() saving = false;
  @Input() locked = false;
  @Input() submitLabel = 'Create Draft';
  @Input() serverProblem: ApiProblem | null = null;
  @Output() readonly saveDraft = new EventEmitter<DraftWrite>();
  @Output() readonly cancel = new EventEmitter<void>();
  @Output() readonly formChanged = new EventEmitter<void>();
  @ViewChild('errorSummary') private errorSummary?: ElementRef<HTMLElement>;

  readonly form = new FormGroup({
    title: new FormControl('', { nonNullable: true, validators: [requiredText, Validators.maxLength(200)] }),
    startLocal: new FormControl('', { nonNullable: true, validators: Validators.required }),
    startOffset: new FormControl<string | null>(null),
    endLocal: new FormControl('', { nonNullable: true, validators: Validators.required }),
    endOffset: new FormControl<string | null>(null),
    location: new FormControl('', { nonNullable: true, validators: requiredText }),
    notes: new FormControl('', { nonNullable: true }),
  }, { validators: validDateOrder });

  private submitted = false;
  private serverErrors: Record<string, string[]> = {};

  constructor() {
    this.form.controls.startLocal.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.form.controls.startOffset.setValue(null, { emitEvent: false });
      this.clearServerError('start');
    });
    this.form.controls.endLocal.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.form.controls.endOffset.setValue(null, { emitEvent: false });
      this.clearServerError('end');
    });
    this.form.controls.title.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.clearServerError('title'));
    this.form.controls.location.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.clearServerError('location'));
    this.form.controls.startOffset.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.clearServerError('start'));
    this.form.controls.endOffset.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.clearServerError('end'));
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.formChanged.emit());
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['initial'] && this.initial) {
      this.form.setValue({
        title: this.initial.title,
        startLocal: this.initial.start.localDateTime,
        startOffset: this.initial.start.offset,
        endLocal: this.initial.end.localDateTime,
        endOffset: this.initial.end.offset,
        location: this.initial.location,
        notes: this.initial.notes ?? '',
      }, { emitEvent: false });
      this.form.markAsPristine();
    }
    if (changes['serverProblem']) {
      this.serverErrors = { ...(this.serverProblem?.errors ?? {}) };
      if (Object.keys(this.serverErrors).length > 0) {
        queueMicrotask(() => this.errorSummary?.nativeElement.focus());
      }
    }
  }

  get dirty(): boolean { return this.form.dirty; }
  get showValidationSummary(): boolean {
    return (this.submitted && this.form.invalid) || Object.keys(this.serverErrors).length > 0;
  }

  errorFor(field: 'title' | 'start' | 'end' | 'location'): string | null {
    const control = field === 'start' ? this.form.controls.startLocal
      : field === 'end' ? this.form.controls.endLocal
      : this.form.controls[field];
    if (this.serverErrors[field]?.length) return this.serverErrors[field][0];
    if (!(control.touched || this.submitted)) return null;
    if (control.hasError('requiredText') || control.hasError('required')) return `${this.labelFor(field)} is required.`;
    if (field === 'end' && this.form.hasError('dateOrder')) return 'End date and time must be after start date and time.';
    if (control.hasError('maxlength')) return 'Title is too long.';
    return null;
  }

  offsetsFor(field: 'start' | 'end'): string[] {
    return this.serverProblem?.allowedOffsets?.[field] ?? [];
  }

  submit(): void {
    this.submitted = true;
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving || this.locked) {
      if (this.form.invalid) queueMicrotask(() => this.errorSummary?.nativeElement.focus());
      return;
    }
    const raw = this.form.getRawValue();
    if (this.offsetsFor('start').length && !raw.startOffset) return;
    if (this.offsetsFor('end').length && !raw.endOffset) return;
    this.saveDraft.emit({
      title: raw.title,
      start: { localDateTime: raw.startLocal, offset: raw.startOffset || null },
      end: { localDateTime: raw.endLocal, offset: raw.endOffset || null },
      location: raw.location,
      notes: raw.notes === '' && this.initial?.notes !== '' ? null : raw.notes,
    });
  }

  markSaved(): void { this.form.markAsPristine(); }

  private clearServerError(field: string): void {
    if (!this.serverErrors[field]) return;
    const next = { ...this.serverErrors };
    delete next[field];
    this.serverErrors = next;
  }

  private labelFor(field: string): string {
    return { title: 'Title', start: 'Start date and time', end: 'End date and time', location: 'Location' }[field] ?? field;
  }
}
