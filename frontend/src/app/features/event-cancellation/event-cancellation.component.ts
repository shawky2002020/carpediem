import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { toApiProblem } from '../../core/http/api.interceptor';
import { EventCancellationContext, EventCancellationService } from './event-cancellation.service';

@Component({
  selector: 'app-event-cancellation',
  imports: [RouterLink],
  templateUrl: './event-cancellation.component.html',
  styleUrl: './event-cancellation.component.scss',
})
export class EventCancellationComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(EventCancellationService);
  readonly context = signal<EventCancellationContext | null>(null);
  readonly reason = signal('');
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void { this.load(); }

  load(): void {
    const eventId = this.route.snapshot.paramMap.get('eventId');
    if (!eventId) return;
    this.loading.set(true);
    this.service.getContext(eventId).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: context => this.context.set(context),
      error: error => this.error.set(toApiProblem(error, 'Could not load Event.').title),
    });
  }

  submit(): void {
    const context = this.context();
    if (!context || this.saving()) return;
    if (this.reason().trim().length < 10 || this.reason().trim().length > 500) {
      this.error.set('Enter a reason between 10 and 500 characters.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.service.cancel(context.eventId, context.version, this.reason().trim())
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => this.router.navigate(['/dashboard']),
        error: error => this.error.set(toApiProblem(error, 'Event could not be cancelled.').title),
      });
  }
}
