import { CommonModule } from '@angular/common';
import { Component, EventEmitter, HostListener, Input, OnInit, Output, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { EventRescheduleService } from '../../data/event-reschedule.service';
import { RescheduleEventRequest, RescheduleEventResponse } from '../../data/event-reschedule.model';

@Component({
  selector: 'app-reschedule-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './reschedule-dialog.component.html',
  styleUrl: './reschedule-dialog.component.scss',
})
export class RescheduleDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly rescheduleService = inject(EventRescheduleService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  @Input() eventId = '';
  @Input() eventTitle = '';
  @Input() currentVersion = '';
  @Input() currentStart = '';
  @Input() currentEnd = '';

  @Output() rescheduled = new EventEmitter<RescheduleEventResponse>();
  @Output() cancelled = new EventEmitter<void>();

  readonly form: FormGroup = this.fb.group({
    newStartDateTime: ['', [Validators.required]],
    newEndDateTime: ['', [Validators.required]],
    justification: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(500)]],
  });

  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly liveStatus = signal<string>('');

  ngOnInit(): void {
    if (!this.eventId) {
      this.eventId = this.route.snapshot.paramMap.get('eventId') ?? '';
    }
    const state = globalThis.history?.state;
    if (state) {
      if (!this.eventTitle && state.eventTitle) this.eventTitle = state.eventTitle;
      if (!this.currentVersion && state.version) this.currentVersion = state.version;
      if (!this.currentStart && state.start) this.currentStart = state.start;
      if (!this.currentEnd && state.end) this.currentEnd = state.end;
    }
  }

  @HostListener('keydown.escape')
  onEscape(): void {
    if (!this.isSubmitting()) {
      this.cancel();
    }
  }

  cancel(): void {
    this.cancelled.emit();
    if (this.route.snapshot.paramMap.get('eventId')) {
      this.router.navigate(['/']);
    }
  }

  submitReschedule(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.errorMessage.set('Please provide all required fields with a valid justification.');
      this.liveStatus.set('Validation failed. Please review the form fields.');
      return;
    }

    const { newStartDateTime, newEndDateTime, justification } = this.form.value;

    const startDate = new Date(newStartDateTime);
    const endDate = new Date(newEndDateTime);
    const now = new Date();

    if (startDate <= now) {
      this.errorMessage.set('New start date and time must be in the future.');
      this.liveStatus.set('Error: New start time must be in the future.');
      return;
    }

    if (endDate <= startDate) {
      this.errorMessage.set('New end date and time must be after the start time.');
      this.liveStatus.set('Error: New end time must be after the start time.');
      return;
    }

    const request: RescheduleEventRequest = {
      newStartDateTime: startDate.toISOString(),
      newEndDateTime: endDate.toISOString(),
      justification: justification.trim(),
    };

    this.isSubmitting.set(true);
    this.liveStatus.set('Submitting event reschedule...');

    this.rescheduleService
      .rescheduleEvent(this.eventId, request, this.currentVersion)
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (response) => {
          this.successMessage.set('Event rescheduled successfully. Staffing cycle rotated.');
          this.liveStatus.set('Success: Event rescheduled and staffing cycle rotated.');
          this.rescheduled.emit(response);
          if (this.route.snapshot.paramMap.get('eventId')) {
            setTimeout(() => this.router.navigate(['/']), 1000);
          }
        },
        error: (err) => {
          const detail = err?.error?.detail ?? err?.error?.title ?? 'Failed to reschedule event. Please try again.';
          this.errorMessage.set(detail);
          this.liveStatus.set(`Error: ${detail}`);
        },
      });
  }
}
