import {
  Component,
  computed,
  ElementRef,
  HostListener,
  inject,
  Input,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../../core/http/api.interceptor';
import { MyAssignmentResponse } from '../data/volunteer-assignment.model';
import { VolunteerAssignmentService } from '../data/volunteer-assignment.service';

@Component({
  selector: 'app-volunteer-assignment',
  imports: [],
  templateUrl: './volunteer-assignment.component.html',
  styleUrl: './volunteer-assignment.component.scss',
})
export class VolunteerAssignmentComponent implements OnInit {
  private readonly service = inject(VolunteerAssignmentService);
  private readonly route = inject(ActivatedRoute);

  @Input() eventId?: string;
  @ViewChild('reasonInput') reasonInput?: ElementRef<HTMLTextAreaElement>;

  readonly effectiveEventId = computed(() =>
    this.eventId ?? this.route.snapshot.paramMap.get('eventId') ?? ''
  );

  readonly assignment = signal<MyAssignmentResponse | null>(null);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly modalOpen = signal(false);
  readonly justification = signal('');
  readonly validationError = signal<string | null>(null);
  readonly statusMessage = signal<string | null>(null);
  readonly problem = signal<ApiProblem | null>(null);

  readonly isActive = computed(() => this.assignment()?.status === 'Active');
  readonly isCancelled = computed(() => this.assignment()?.status === 'Cancelled');
  readonly isWindowOpen = computed(() => this.assignment()?.isWindowOpen ?? false);
  readonly canCancel = computed(() => this.isActive() && this.isWindowOpen());
  readonly charCount = computed(() => this.justification().trim().length);

  ngOnInit(): void {
    this.load();
  }

  load(preserveProblem = false): void {
    const id = this.effectiveEventId();
    if (!id) return;

    this.loading.set(true);
    if (!preserveProblem) this.problem.set(null);

    this.service.getMyAssignment(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: data => {
          this.assignment.set(data);
        },
        error: error => {
          this.problem.set(toApiProblem(error, 'Assignment details could not be loaded.'));
        },
      });
  }

  openCancelModal(): void {
    if (!this.canCancel() || this.submitting()) return;
    this.justification.set('');
    this.validationError.set(null);
    this.modalOpen.set(true);
    setTimeout(() => {
      this.reasonInput?.nativeElement.focus();
    }, 0);
  }

  closeCancelModal(): void {
    if (this.submitting()) return;
    this.modalOpen.set(false);
    this.validationError.set(null);
  }

  onJustificationChange(event: Event): void {
    const value = (event.target as HTMLTextAreaElement).value;
    this.justification.set(value);
    if (this.validationError()) {
      const trimmed = value.trim();
      if (trimmed.length > 0 && trimmed.length <= 500) {
        this.validationError.set(null);
      }
    }
  }

  confirmCancellation(): void {
    const id = this.effectiveEventId();
    const curr = this.assignment();
    if (!id || !curr || !this.canCancel() || this.submitting()) return;

    const trimmed = this.justification().trim();
    if (trimmed.length === 0) {
      this.validationError.set('A non-empty cancellation justification is required.');
      this.reasonInput?.nativeElement.focus();
      return;
    }

    if (trimmed.length > 500) {
      this.validationError.set('Justification cannot exceed 500 characters.');
      this.reasonInput?.nativeElement.focus();
      return;
    }

    this.submitting.set(true);
    this.validationError.set(null);
    this.problem.set(null);

    this.service.cancelAssignment(id, curr.assignmentId, trimmed, curr.version)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: response => {
          this.assignment.set({
            ...curr,
            status: 'Cancelled',
            cancelledAt: response.cancelledAt,
            cancellationReason: response.cancellationReason,
            version: response.version,
          });
          this.modalOpen.set(false);
          this.statusMessage.set('Assignment successfully cancelled.');
        },
        error: error => {
          const apiProblem = toApiProblem(error, 'Assignment cancellation could not be processed.');
          this.problem.set(apiProblem);
          if (error.status === 409 || error.status === 412) {
            this.modalOpen.set(false);
            this.load(true);
          }
        },
      });
  }

  @HostListener('keydown.escape')
  handleEscape(): void {
    if (this.modalOpen()) {
      this.closeCancelModal();
    }
  }

  formatDate(value: string | null | undefined): string {
    if (!value) return '';
    return new Intl.DateTimeFormat('en', {
      timeZone: 'Africa/Cairo',
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value)) + ' Cairo';
  }
}
