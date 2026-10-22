import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs';
import { toApiProblem } from '../../core/http/api.interceptor';
import { OperationalDashboardService, OperationalEvent } from './operational-dashboard.service';

@Component({
  selector: 'app-operational-dashboard',
  imports: [RouterLink],
  templateUrl: './operational-dashboard.component.html',
  styleUrl: './operational-dashboard.component.scss',
})
export class OperationalDashboardComponent implements OnInit {
  private readonly service = inject(OperationalDashboardService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private timer?: ReturnType<typeof setInterval>;
  private pending = false;
  readonly events = signal<OperationalEvent[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

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
