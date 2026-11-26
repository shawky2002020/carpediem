import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs';
import { toApiProblem } from '../../core/http/api.interceptor';
import { OperationalDashboardService, OperationalEvent } from './operational-dashboard.service';
import { SessionService } from '../../core/auth/session.service';

export type DashboardFilter = 'All' | 'RequiresAction' | 'Published' | 'Drafts' | 'Concluded';

const eventFilters = [
  { id: 'All', label: 'All', matches: () => true },
  { id: 'RequiresAction', label: 'Needs attention', matches: (event: OperationalEvent) => event.pendingAttendance || (event.status === 'Published' && event.activeRosterCount === 0) },
  { id: 'Published', label: 'Published', matches: (event: OperationalEvent) => event.status === 'Published' },
  { id: 'Drafts', label: 'Drafts', matches: (event: OperationalEvent) => event.status === 'Draft' },
  { id: 'Concluded', label: 'Closed', matches: (event: OperationalEvent) => event.status === 'Completed' || event.status === 'Cancelled' },
] as const;

const dateFormats = {
  date: new Intl.DateTimeFormat('en', { timeZone: 'Africa/Cairo', dateStyle: 'full' }),
  time: new Intl.DateTimeFormat('en', { timeZone: 'Africa/Cairo', timeStyle: 'short' }),
};

@Component({
  selector: 'app-operational-dashboard',
  imports: [CommonModule, RouterLink],
  templateUrl: './operational-dashboard.component.html',
  styleUrl: './operational-dashboard.component.scss',
})
export class OperationalDashboardComponent implements OnInit {
  private readonly service = inject(OperationalDashboardService);
  readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private timer?: ReturnType<typeof setInterval>;
  readonly events = signal<OperationalEvent[]>([]);
  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly error = signal<string | null>(null);
  readonly activeFilter = signal<DashboardFilter>('All');

  private readonly eventGroups = computed(() => {
    const events = this.events();
    return eventFilters.map(({ id, label, matches }) => ({ id, label, events: events.filter(matches) }));
  });

  readonly filters = computed(() => this.eventGroups().map(({ id, label, events }) => ({ id, label, count: events.length })));
  readonly filteredEvents = computed(() => this.eventGroups().find(group => group.id === this.activeFilter())?.events ?? []);

  readonly dateGroups = computed(() => {
    const groups: { date: string; firstId: string; events: OperationalEvent[] }[] = [];
    for (const event of this.filteredEvents()) {
      const date = this.formatDate(event.startDateTime, 'date');
      const previous = groups.at(-1);
      if (previous?.date === date) previous.events.push(event);
      else groups.push({ date, firstId: event.eventId, events: [event] });
    }
    return groups;
  });

  setFilter(filterName: DashboardFilter): void {
    this.activeFilter.set(filterName);
  }

  ngOnInit(): void {
    this.refresh();
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(() => this.refresh());
    document.addEventListener('visibilitychange', this.onVisibility);
    window.addEventListener('focus', this.onFocus);
    this.startPolling();
    this.destroyRef.onDestroy(() => {
      this.stopPolling();
      document.removeEventListener('visibilitychange', this.onVisibility);
      window.removeEventListener('focus', this.onFocus);
    });
  }

  refresh(): void {
    if (this.refreshing()) return;
    this.refreshing.set(true);
    this.service.getEvents().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: events => {
        this.events.set(events);
        this.loading.set(false);
        this.error.set(null);
        this.refreshing.set(false);
      },
      error: error => {
        this.loading.set(false);
        this.error.set(toApiProblem(error, 'Dashboard could not be refreshed.').title);
        this.refreshing.set(false);
      },
    });
  }

  formatDate(value: string, part: keyof typeof dateFormats): string {
    return dateFormats[part].format(new Date(value));
  }

  private readonly onVisibility = () => {
    if (document.visibilityState === 'visible') {
      this.refresh();
      this.startPolling();
    } else this.stopPolling();
  };

  private readonly onFocus = () => {
    if (document.visibilityState === 'visible') this.refresh();
  };

  private startPolling(): void {
    if (this.timer || document.visibilityState !== 'visible') return;
    this.timer = setInterval(() => this.refresh(), 90_000);
  }

  private stopPolling(): void {
    if (this.timer) clearInterval(this.timer);
    this.timer = undefined;
  }
}
