import { afterNextRender, Component, computed, DestroyRef, ElementRef, inject, Injector, OnInit, signal, viewChild, viewChildren } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs';
import { toApiProblem } from '../../core/http/api.interceptor';
import { OperationalDashboardService, OperationalEvent } from './operational-dashboard.service';
import { SessionService } from '../../core/auth/session.service';

export type DashboardFilter = 'Operations' | 'All' | 'RequiresAction' | 'Published' | 'Drafts' | 'Concluded';

interface EventPosition {
  eventId: string;
  destination: string;
}

interface DashboardView {
  viewerId: string;
  filter: DashboardFilter;
  position?: EventPosition;
}

const viewStorageKey = 'carpediem.dashboard.view';

const eventFilters = [
  { id: 'Operations', label: 'Operations', matches: (event: OperationalEvent) => event.pendingAttendance || event.status === 'Published' },
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
  private readonly injector = inject(Injector);
  private readonly eventRows = viewChildren<ElementRef<HTMLElement>>('eventRow');
  private readonly filterGroup = viewChild<ElementRef<HTMLElement>>('filterGroup');
  private readonly arrival = this.router.currentNavigation()?.previousNavigation?.finalUrl?.toString().split(/[?#]/, 1)[0];
  private readonly savedView = this.readView();
  private returnPosition = this.savedView?.position?.destination === this.arrival ? this.savedView?.position : undefined;
  private timer?: ReturnType<typeof setInterval>;
  readonly events = signal<OperationalEvent[]>([]);
  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly error = signal<string | null>(null);
  readonly activeFilter = signal<DashboardFilter>(this.savedView?.filter ?? 'Operations');

  private readonly eventGroups = computed(() => {
    const events = this.events();
    return eventFilters.map(({ id, label, matches }) => ({ id, label, events: events.filter(matches) }));
  });

  readonly filters = computed(() => this.eventGroups().map(({ id, label, events }) => ({ id, label, count: events.length })));
  readonly filteredEvents = computed(() => this.eventGroups().find(group => group.id === this.activeFilter())?.events ?? []);

  readonly queueSections = computed(() => {
    const events = this.filteredEvents();
    if (this.activeFilter() !== 'Operations') return [{ name: null, dates: this.groupByDate(events) }];

    return [
      { name: 'In progress', events: events.filter(event => event.inProgress) },
      { name: 'Attendance to finish', events: events.filter(event => !event.inProgress && event.pendingAttendance) },
      { name: 'Published events', events: events.filter(event => !event.inProgress && !event.pendingAttendance) },
    ].filter(section => section.events.length)
      .map(section => ({ name: section.name, dates: this.groupByDate(section.events) }));
  });

  private groupByDate(events: OperationalEvent[]) {
    const groups: { date: string; events: OperationalEvent[] }[] = [];
    for (const event of events) {
      const date = this.formatDate(event.startDateTime, 'date');
      const previous = groups.at(-1);
      if (previous?.date === date) previous.events.push(event);
      else groups.push({ date, events: [event] });
    }
    return groups;
  }

  setFilter(filterName: DashboardFilter): void {
    this.activeFilter.set(filterName);
    this.returnPosition = undefined;
    this.saveView();
  }

  protected rememberPosition(event: MouseEvent, eventId: string): void {
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return;
    const link = (event.target as Element | null)?.closest<HTMLAnchorElement>('a[href]');
    if (link?.origin === window.location.origin) {
      this.saveView({ eventId, destination: link.pathname });
    }
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
        if (this.returnPosition) {
          const position = this.returnPosition;
          this.returnPosition = undefined;
          afterNextRender(() => this.restorePosition(position), { injector: this.injector });
        }
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

  private readView(): DashboardView | undefined {
    try {
      const state = JSON.parse(sessionStorage.getItem(viewStorageKey) ?? 'null') as DashboardView | null;
      if (state && state.viewerId === this.session.session()?.userId && eventFilters.some(filter => filter.id === state.filter)) {
        return state;
      }
    } catch { /* Tab storage can be unavailable. */ }
    return undefined;
  }

  private saveView(position?: EventPosition): void {
    const viewerId = this.session.session()?.userId;
    if (!viewerId) return;
    try {
      sessionStorage.setItem(viewStorageKey, JSON.stringify({ viewerId, filter: this.activeFilter(), position } satisfies DashboardView));
    } catch { /* Keep the current filter usable even without tab storage. */ }
  }

  private restorePosition(position: EventPosition): void {
    const row = this.eventRows().find(element => element.nativeElement.dataset['eventId'] === position.eventId)?.nativeElement;
    const action = row && Array.from(row.querySelectorAll<HTMLAnchorElement>('a[href]')).find(link => link.pathname === position.destination);
    const disclosure = action?.closest('details');
    if (disclosure) disclosure.open = true;

    const target = action ?? row ?? this.filterGroup()?.nativeElement.querySelector<HTMLElement>('[aria-pressed="true"]');
    if (!target) return;
    // Restore the event by identity after refreshing, even if its position has changed.
    target.scrollIntoView({ block: 'center' });
    target.focus({ preventScroll: true });
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
