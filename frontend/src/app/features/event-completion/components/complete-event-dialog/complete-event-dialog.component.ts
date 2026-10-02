import { CommonModule } from '@angular/common';
import { Component, EventEmitter, HostListener, Input, OnInit, Output, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { EventCompletionService } from '../../data/event-completion.service';
import { CompleteEventResponse } from '../../data/event-completion.model';

@Component({
  selector: 'app-complete-event-dialog',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './complete-event-dialog.component.html',
  styleUrl: './complete-event-dialog.component.scss',
})
export class CompleteEventDialogComponent implements OnInit {
  private readonly completionService = inject(EventCompletionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  @Input() eventId = '';
  @Input() eventTitle = '';
  @Input() currentVersion = '';
  @Input() endDateTime = '';

  @Output() completed = new EventEmitter<CompleteEventResponse>();
  @Output() cancelled = new EventEmitter<void>();

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
      if (!this.endDateTime && state.end) this.endDateTime = state.end;
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

  submitCompletion(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);

    if (!this.eventId) {
      this.errorMessage.set('Event identifier is missing.');
      this.liveStatus.set('Error: Event identifier is missing.');
      return;
    }

    if (!this.currentVersion) {
      this.errorMessage.set('Current event version is missing. Please refresh.');
      this.liveStatus.set('Error: Current event version is missing.');
      return;
    }

    this.isSubmitting.set(true);
    this.liveStatus.set('Submitting event closeout...');

    this.completionService
      .completeEvent(this.eventId, this.currentVersion)
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (response) => {
          this.successMessage.set('Event completed successfully.');
          this.liveStatus.set('Success: Event marked as completed.');
          this.completed.emit(response);
          if (this.route.snapshot.paramMap.get('eventId')) {
            setTimeout(() => this.router.navigate(['/']), 1000);
          }
        },
        error: (err) => {
          const detail = err?.error?.detail ?? err?.error?.title ?? 'Failed to complete event. Please try again.';
          this.errorMessage.set(detail);
          this.liveStatus.set(`Error: ${detail}`);
        },
      });
  }
}
