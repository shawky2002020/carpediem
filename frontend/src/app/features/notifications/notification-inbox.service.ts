import { HttpClient } from '@angular/common/http';
import { DestroyRef, effect, inject, Injectable, InjectionToken, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter, finalize, Subscription } from 'rxjs';
import { SessionService } from '../../core/auth/session.service';

export interface NotificationInboxItem {
  id: string;
  eventId: string | null;
  notificationType: string;
  contextPayload: string | null;
  createdAt: string;
  readAt: string | null;
}

export const NOTIFICATION_POLL_INTERVAL_MS = new InjectionToken<number>('Notification poll interval', {
  providedIn: 'root', factory: () => 90_000,
});

@Injectable({ providedIn: 'root' })
export class NotificationInboxService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly pollInterval = inject(NOTIFICATION_POLL_INTERVAL_MS);
  private requests = new Subscription();
  private readonly navigation = this.router.events.pipe(filter(event => event instanceof NavigationEnd))
    .subscribe(() => this.refreshAll());
  private timer: ReturnType<typeof setInterval> | undefined;
  private generation = 0;
  private activeUserId: string | null = null;
  private countPending = false;
  private listPending = false;
  private countRefreshQueued = false;
  private countChangeRequested = false;
  private listRefreshQueued = false;

  readonly items = signal<NotificationInboxItem[]>([]);
  readonly unreadCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly savingId = signal<string | null>(null);
  readonly savingAll = signal(false);

  constructor() {
    effect(() => {
      const userId = this.session.session()?.userId ?? null;
      if (userId === this.activeUserId) return;
      this.generation++;
      this.requests.unsubscribe();
      this.requests = new Subscription();
      this.activeUserId = userId;
      this.items.set([]);
      this.unreadCount.set(0);
      this.error.set(false);
      this.loading.set(false);
      this.savingId.set(null);
      this.savingAll.set(false);
      this.countPending = false;
      this.listPending = false;
      this.countRefreshQueued = false;
      this.countChangeRequested = false;
      this.listRefreshQueued = false;
      this.stopPolling();
      if (userId) {
        this.refreshAll();
        this.startPolling();
      }
    });

    const onVisibility = () => {
      if (document.visibilityState === 'visible') {
        this.refreshAll();
        this.startPolling();
      } else {
        this.stopPolling();
      }
    };
    const onFocus = () => {
      if (document.visibilityState === 'visible') this.refreshAll();
    };
    document.addEventListener('visibilitychange', onVisibility);
    window.addEventListener('focus', onFocus);
    this.destroyRef.onDestroy(() => {
      this.stopPolling();
      this.requests.unsubscribe();
      this.navigation.unsubscribe();
      document.removeEventListener('visibilitychange', onVisibility);
      window.removeEventListener('focus', onFocus);
    });
  }

  refreshAll(): void {
    if (!this.activeUserId) return;
    this.refreshCount(false);
    this.refreshList();
  }

  private refreshCount(loadListOnChange: boolean): void {
    if (!this.activeUserId) return;
    if (this.countPending) {
      this.countRefreshQueued = true;
      this.countChangeRequested ||= loadListOnChange;
      return;
    }
    this.countPending = true;
    const generation = this.generation;
    this.requests.add(this.http.get<{ count: number }>('/api/notifications/unread-count')
      .pipe(finalize(() => {
        if (generation !== this.generation) return;
        this.countPending = false;
        if (this.countRefreshQueued) {
          const checkChange = this.countChangeRequested;
          this.countRefreshQueued = false;
          this.countChangeRequested = false;
          this.refreshCount(checkChange);
        }
      }))
      .subscribe({
      next: response => {
        if (generation !== this.generation) return;
        const changed = response.count !== this.unreadCount();
        this.unreadCount.set(response.count);
        this.error.set(false);
        if (changed && loadListOnChange) this.refreshList();
      },
      error: () => { if (generation === this.generation) this.error.set(true); },
    }));
  }

  private refreshList(): void {
    if (!this.activeUserId) return;
    if (this.listPending) {
      this.listRefreshQueued = true;
      return;
    }
    this.listPending = true;
    this.loading.set(true);
    const generation = this.generation;
    this.requests.add(this.http.get<NotificationInboxItem[]>('/api/notifications')
      .pipe(finalize(() => {
        if (generation === this.generation) {
          this.listPending = false;
          this.loading.set(false);
          if (this.listRefreshQueued) {
            this.listRefreshQueued = false;
            this.refreshList();
          }
        }
      }))
      .subscribe({
      next: items => {
        if (generation !== this.generation) return;
        this.items.set(items);
        this.error.set(false);
      },
      error: () => { if (generation === this.generation) this.error.set(true); },
    }));
  }

  markRead(id: string): void {
    if (!this.activeUserId || this.savingId()) return;
    this.savingId.set(id);
    const generation = this.generation;
    this.requests.add(this.http.post<void>(`/api/notifications/${id}/read`, {})
      .pipe(finalize(() => { if (generation === this.generation) this.savingId.set(null); }))
      .subscribe({
      next: () => { if (generation === this.generation) this.refreshAll(); },
      error: () => { if (generation === this.generation) this.error.set(true); },
    }));
  }

  markAllRead(): void {
    if (!this.activeUserId || this.savingAll()) return;
    this.savingAll.set(true);
    const generation = this.generation;
    this.requests.add(this.http.post<{ updated: number }>('/api/notifications/mark-all-read', {})
      .pipe(finalize(() => { if (generation === this.generation) this.savingAll.set(false); }))
      .subscribe({
      next: () => { if (generation === this.generation) this.refreshAll(); },
      error: () => { if (generation === this.generation) this.error.set(true); },
    }));
  }

  private startPolling(): void {
    if (this.timer || !this.activeUserId || document.visibilityState !== 'visible') return;
    this.timer = setInterval(() => this.refreshCount(true), this.pollInterval);
  }

  private stopPolling(): void {
    if (this.timer) clearInterval(this.timer);
    this.timer = undefined;
  }
}
