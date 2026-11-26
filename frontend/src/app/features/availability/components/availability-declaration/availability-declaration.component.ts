import { afterNextRender, Component, computed, DestroyRef, ElementRef, inject, Injector, Input, OnInit, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { distinctUntilChanged, finalize, map, Subscription } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../../../core/http/api.interceptor';
import { AvailabilityType, MyAvailabilityResponse } from '../../data/availability.model';
import { AvailabilityService } from '../../data/availability.service';
import { NotificationInboxService } from '../../../notifications/notification-inbox.service';
import { readNotificationContext } from '../../../notifications/notification-context';

const cairo = (options: Intl.DateTimeFormatOptions) =>
  new Intl.DateTimeFormat('en-GB', { ...options, timeZone: 'Africa/Cairo' });
const day = cairo({ day: 'numeric' });
const month = cairo({ month: 'short' });
const cairoYear = cairo({ year: 'numeric' });
const weekday = cairo({ weekday: 'long' });
const calendarDate = cairo({ dateStyle: 'medium' });
const clock = cairo({ hour: '2-digit', minute: '2-digit' });
const timestamp = cairo({ dateStyle: 'medium', timeStyle: 'short' });

@Component({
  selector: 'app-availability-declaration',
  imports: [RouterLink],
  templateUrl: './availability-declaration.component.html',
  styleUrl: './availability-declaration.component.scss',
})
export class AvailabilityDeclarationComponent implements OnInit {
  private readonly service = inject(AvailabilityService);
  private readonly route = inject(ActivatedRoute);
  private readonly inbox = inject(NotificationInboxService);

  private readonly injector = inject(Injector);
  private readonly responseHeading = viewChild<ElementRef<HTMLElement>>('responseHeading');
  private readonly destroyRef = inject(DestroyRef);
  private read?: Subscription;
  private write?: Subscription;
  @Input() eventId?: string;

  readonly effectiveEventId = signal('');

  readonly availability = signal<MyAvailabilityResponse | null>(null);
  readonly eventTitle = computed(() => {
    const record = this.availability();
    if (record?.itinerary?.title) return record.itinerary.title;
    const eventId = record?.eventId;
    if (!eventId) return null;
    for (const item of this.inbox.items()) {
      if (item.eventId !== eventId) continue;
      const title = readNotificationContext(item.contextPayload)['eventTitle'];
      if (typeof title === 'string' && title.trim()) return title;
    }
    return null;
  });
  readonly schedule = computed(() => {
    const itinerary = this.availability()?.itinerary;
    if (!itinerary) return null;
    const start = new Date(itinerary.startDateTime);
    const end = new Date(itinerary.endDateTime);
    if (!Number.isFinite(start.getTime()) || !Number.isFinite(end.getTime())) return null;
    const sameDate = calendarDate.format(start) === calendarDate.format(end);
    return {
      day: day.format(start), month: month.format(start), weekday: weekday.format(start),
      year: cairoYear.format(start), fullDate: calendarDate.format(start),
      start: clock.format(start), end: sameDate ? clock.format(end) : calendarDate.format(end) + ', ' + clock.format(end),
    };
  });

  readonly loading = signal(true);
  readonly needsRefresh = signal(false);
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
    return a !== null && !this.loading() && !this.needsRefresh() && a.isWindowOpen && !a.isChiefLeaderOnEvent && !this.submitting();
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
    this.route.paramMap.pipe(
      map(params => this.eventId ?? params.get('eventId') ?? ''),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(id => {
      this.read?.unsubscribe();
      this.write?.unsubscribe();
      this.effectiveEventId.set(id);
      this.availability.set(null);
      this.statusMessage.set(null);
      this.load();
    });
  }

  load(preserveProblem = false, restoreFocus = false): void {
    const id = this.effectiveEventId();
    if (!id) return;

    this.read?.unsubscribe();
    this.loading.set(true);

    if (!preserveProblem) this.problem.set(null);

    this.read = this.service.getMyAvailability(id)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({
        next: data => {
          this.needsRefresh.set(false);
          this.availability.set(data);
          if (restoreFocus) {
            afterNextRender(() => {
              if (document.activeElement === document.body) this.responseHeading()?.nativeElement.focus();
            }, { injector: this.injector });
          }
        },
        error: error => {
          this.needsRefresh.set(true);
          this.availability.set(null);
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

    this.write = this.service.declareAvailability(id, targetResponse, current?.version)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.submitting.set(false)))
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
          this.needsRefresh.set(error.status === 0 || error.status >= 500 || error.status === 409);
          if (error.status === 409) {
            this.load(true);
          }
        },
      });
  }

  formatDate(value: string | null | undefined): string {
    if (!value) return '';
    return timestamp.format(new Date(value)) + ' Cairo';
  }
}
