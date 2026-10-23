import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs';
import { toApiProblem } from '../../core/http/api.interceptor';
import { OperationalDashboardService, OperationalEvent } from './operational-dashboard.service';
import { SessionService } from '../../core/auth/session.service';

export type DashboardFilter = 'All' | 'RequiresAction' | 'Published' | 'Drafts' | 'Concluded';

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
  private pending = false;

  readonly events = signal<OperationalEvent[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly activeFilter = signal<DashboardFilter>('All');

  // Executive KPI Status Ribbon Metrics
  readonly draftCount = computed(() => this.events().filter(e => e.status === 'Draft').length);
  readonly activeStaffingCount = computed(() => this.events().filter(e => e.status === 'Published' && !e.inProgress).length);
  readonly pendingAttendanceCount = computed(() => this.events().filter(e => e.pendingAttendance).length);
  readonly concludedCount = computed(() => this.events().filter(e => e.status === 'Completed').length);

  // Filtered Events
  readonly filteredEvents = computed(() => {
    const list = this.events();
    switch (this.activeFilter()) {
      case 'RequiresAction':
        return list.filter(e => e.pendingAttendance || (e.status === 'Published' && e.activeRosterCount === 0));
      case 'Published':
        return list.filter(e => e.status === 'Published');
      case 'Drafts':
        return list.filter(e => e.status === 'Draft');
      case 'Concluded':
        return list.filter(e => e.status === 'Completed' || e.status === 'Cancelled');
      default:
        return list;
    }
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
    if (this.pending) return;
    this.pending = true;
    this.service.getEvents().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: events => {
        this.events.set(events);
        this.loading.set(false);
        this.error.set(null);
        this.pending = false;
      },
      error: error => {
        this.loading.set(false);
        this.error.set(toApiProblem(error, 'Dashboard could not be refreshed.').title);
        this.pending = false;
      },
    });
  }

  formatDate(value: string): string {
    return new Intl.DateTimeFormat('en', {
      timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short',
    }).format(new Date(value)) + ' Cairo';
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
