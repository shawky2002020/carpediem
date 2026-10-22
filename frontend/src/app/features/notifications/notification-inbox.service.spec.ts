import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Session, SessionService } from '../../core/auth/session.service';
import { NOTIFICATION_POLL_INTERVAL_MS, NotificationInboxService } from './notification-inbox.service';

describe('NotificationInboxService', () => {
  const sessionState = signal<Session | null>(null);
  let service: NotificationInboxService;
  let http: HttpTestingController;
  let visible = true;

  beforeEach(() => {
    vi.useFakeTimers();
    sessionState.set(null);
    visible = true;
    vi.spyOn(document, 'visibilityState', 'get').mockImplementation(() => visible ? 'visible' : 'hidden');
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
        { provide: SessionService, useValue: { session: sessionState.asReadonly() } },
        { provide: NOTIFICATION_POLL_INTERVAL_MS, useValue: 1000 },
      ],
    });
    service = TestBed.inject(NotificationInboxService);
    http = TestBed.inject(HttpTestingController);
    TestBed.tick();
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  function signInAndFlush(): void {
    sessionState.set({ userId: 'user-a', userName: 'User A', roles: ['Leader'] });
    TestBed.tick();
    http.expectOne('/api/notifications/unread-count').flush({ count: 1 });
    http.expectOne('/api/notifications').flush([{
      id: 'notification-a', eventId: null, notificationType: 'EventPublished',
      contextPayload: '{"eventTitle":"A"}', createdAt: '2026-10-22T10:00:00Z', readAt: null,
    }]);
  }

  it('loads on session and polls count before loading a changed list', () => {
    signInAndFlush();
    expect(service.unreadCount()).toBe(1);
    expect(service.items()).toHaveLength(1);

    vi.advanceTimersByTime(1000);
    http.expectOne('/api/notifications/unread-count').flush({ count: 1 });
    http.expectNone('/api/notifications');

    vi.advanceTimersByTime(1000);
    http.expectOne('/api/notifications/unread-count').flush({ count: 2 });
    http.expectOne('/api/notifications').flush([]);
    expect(service.unreadCount()).toBe(2);
  });

  it('suspends hidden-tab polling and refreshes on visibility and focus', () => {
    signInAndFlush();
    visible = false;
    document.dispatchEvent(new Event('visibilitychange'));
    vi.advanceTimersByTime(5000);
    http.expectNone('/api/notifications/unread-count');

    visible = true;
    document.dispatchEvent(new Event('visibilitychange'));
    http.expectOne('/api/notifications/unread-count').flush({ count: 1 });
    http.expectOne('/api/notifications').flush([]);
    window.dispatchEvent(new Event('focus'));
    http.expectOne('/api/notifications/unread-count').flush({ count: 1 });
    http.expectOne('/api/notifications').flush([]);
    vi.advanceTimersByTime(1000);
    http.expectOne('/api/notifications/unread-count').flush({ count: 1 });
  });

  it('recovers on the next visible poll after a transient failure', () => {
    signInAndFlush();
    vi.advanceTimersByTime(1000);
    http.expectOne('/api/notifications/unread-count').flush('Unavailable', { status: 503, statusText: 'Unavailable' });
    expect(service.error()).toBe(true);
    vi.advanceTimersByTime(1000);
    http.expectOne('/api/notifications/unread-count').flush({ count: 1 });
    expect(service.error()).toBe(false);
  });
});
