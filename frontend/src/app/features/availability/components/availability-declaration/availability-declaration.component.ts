import { Component, computed, inject, Input, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../../../core/http/api.interceptor';
import { AvailabilityType, MyAvailabilityResponse } from '../../data/availability.model';
import { AvailabilityService } from '../../data/availability.service';

@Component({
  selector: 'app-availability-declaration',
  imports: [],
  templateUrl: './availability-declaration.component.html',
  styleUrl: './availability-declaration.component.scss',
})
export class AvailabilityDeclarationComponent implements OnInit {
  private readonly service = inject(AvailabilityService);
  private readonly route = inject(ActivatedRoute);

  @Input() eventId?: string;

  readonly effectiveEventId = computed(() =>
    this.eventId ?? this.route.snapshot.paramMap.get('eventId') ?? ''
  );

  readonly availability = signal<MyAvailabilityResponse | null>(null);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly statusMessage = signal<string | null>(null);
  readonly problem = signal<ApiProblem | null>(null);

  readonly isChiefLeader = computed(() => this.availability()?.isChiefLeaderOnEvent === true);
  readonly isClosed = computed(() => {
    const a = this.availability();
    return a !== null && !a.isWindowOpen;
  });

  readonly canDeclare = computed(() => {
    const a = this.availability();
    return a !== null && a.isWindowOpen && !a.isChiefLeaderOnEvent && !this.submitting();
  });

  readonly currentStatusLabel = computed(() => {
    const r = this.availability()?.response;
    if (!r) return 'Response Needed';
    return r === 'Available' ? 'Available' : 'Not Available';
  });

  readonly statusBadgeClass = computed(() => {
    const r = this.availability()?.response;
    if (!r) return 'status-badge-needed';
    return r === 'Available' ? 'status-badge-available' : 'status-badge-not-available';
  });

  ngOnInit(): void {
    this.load();
  }

  load(preserveProblem = false): void {
    const id = this.effectiveEventId();
    if (!id) return;

    this.loading.set(true);
    if (!preserveProblem) this.problem.set(null);

    this.service.getMyAvailability(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: data => {
          this.availability.set(data);
        },
        error: error => {
          this.problem.set(toApiProblem(error, 'Availability could not be loaded.'));
        },
      });
  }

  declare(targetResponse: AvailabilityType): void {
    const id = this.effectiveEventId();
    const current = this.availability();
    if (!id || !this.canDeclare()) return;

    this.submitting.set(true);
    this.problem.set(null);
    this.statusMessage.set(null);

    this.service.declareAvailability(id, targetResponse, current?.version)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: result => {
          if (current) {
            this.availability.set({
              ...current,
              response: result.response,
              declaredAt: result.declaredAt,
              version: result.version,
            });
          }
          const label = result.response === 'Available' ? 'Available' : 'Not Available';
          this.statusMessage.set(`Availability declared as ${label}.`);
        },
        error: error => {
          const apiProblem = toApiProblem(error, 'Availability could not be updated.');
          this.problem.set(apiProblem);
          if (error.status === 409) {
            this.load(true);
          }
        },
      });
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
