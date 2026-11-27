import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { SessionService } from '../../core/auth/session.service';
import { NotificationInboxItem, NotificationInboxService } from './notification-inbox.service';
import { readNotificationContext } from './notification-context';

export interface NotificationGroup {
  label: string;
  items: NotificationInboxItem[];
}

@Component({
  selector: 'app-notification-inbox',
  imports: [CommonModule, RouterLink],
  templateUrl: './notification-inbox.component.html',
  styleUrl: './notification-inbox.component.scss',
})
export class NotificationInboxComponent {
  protected readonly inbox = inject(NotificationInboxService);
  private readonly session = inject(SessionService);
  protected readonly unreadOnly = signal(false);
  private readonly unreadCursors = linkedSignal({
    source: () => this.session.session()?.userId,
    computation: (): string[] => [],
  });
  protected readonly hasNewerUnread = computed(() => this.unreadCursors().length > 0);
  protected readonly unreadPage = rxResource({
    params: () => {
      const userId = this.session.session()?.userId;
      if (!this.unreadOnly() || !userId) return undefined;
      return { userId, beforeId: this.unreadCursors().at(-1), version: this.inbox.unreadRefreshVersion() };
    },
    stream: ({ params }) => this.inbox.getUnreadPage(params.beforeId),
  });
  protected readonly displayedItems = computed(() => this.unreadOnly()
    ? (this.unreadPage.hasValue() ? this.unreadPage.value().items : [])
    : this.inbox.items());
  protected readonly listLoading = computed(() => this.unreadOnly() ? this.unreadPage.isLoading() : this.inbox.loading());
  protected readonly olderUnread = computed(() => this.unreadPage.hasValue() ? this.unreadPage.value().nextBeforeId : null);

  protected selectView(unreadOnly: boolean): void {
    if (this.unreadOnly() === unreadOnly) return;
    this.unreadCursors.set([]);
    this.unreadOnly.set(unreadOnly);
  }

  protected browseUnread(beforeId?: string): void {
    this.unreadCursors.update(cursors => beforeId ? [...cursors, beforeId] : cursors.slice(0, -1));
  }

  readonly groupedNotifications = computed<NotificationGroup[]>(() => {
    const items = this.displayedItems();
    if (items.length === 0) return [];

    const now = Date.now();
    const oneDayMs = 24 * 60 * 60 * 1000;
    const sevenDaysMs = 7 * oneDayMs;

    const todayItems: NotificationInboxItem[] = [];
    const thisWeekItems: NotificationInboxItem[] = [];
    const olderItems: NotificationInboxItem[] = [];

    for (const item of items) {
      const created = new Date(item.createdAt).getTime();
      const diff = now - created;

      if (diff <= oneDayMs) {
        todayItems.push(item);
      } else if (diff <= sevenDaysMs) {
        thisWeekItems.push(item);
      } else {
        olderItems.push(item);
      }
    }

    const groups: NotificationGroup[] = [];
    if (todayItems.length > 0) groups.push({ label: 'Today', items: todayItems });
    if (thisWeekItems.length > 0) groups.push({ label: 'Last 7 days', items: thisWeekItems });
    if (olderItems.length > 0) groups.push({ label: 'Older', items: olderItems });

    return groups;
  });

  protected title(item: NotificationInboxItem): string {
    switch (item.notificationType) {
      case 'ChiefLeaderAppointment': return 'Chief Leader appointment';
      case 'ChiefLeaderDeclined': return 'Chief Leader declined';
      case 'EventPublished': return 'Event published';
      case 'VolunteerSelected': return 'You were selected for a team';
      case 'DirectLeaderAssigned': return 'You were assigned to a team';
      case 'TeamMemberCancelled': return 'Team member cancelled';
      case 'ReplacementLeaderAssigned': return 'You were selected as a replacement';
      case 'EventRescheduled': return 'Event rescheduled';
      case 'EventCancelled': return 'Event cancelled';
      default: return 'Notification';
    }
  }

  protected details(item: NotificationInboxItem): string {
    const payload = readNotificationContext(item.contextPayload);
    const eventTitle = typeof payload['eventTitle'] === 'string' ? payload['eventTitle'] : 'this event';
    const volunteerName = typeof payload['volunteerName'] === 'string' ? payload['volunteerName'] : null;
    const justification = typeof payload['justification'] === 'string' ? payload['justification'] : null;
    if (item.notificationType === 'TeamMemberCancelled') {
      return `${volunteerName ?? 'A team member'} cancelled their assignment for ${eventTitle}${justification ? `. Reason: ${justification}` : '.'}`;
    }
    if (item.notificationType === 'EventRescheduled') {
      const newStart = typeof payload['newStart'] === 'string' ? this.date(payload['newStart']) : null;
      return `${eventTitle} has a new schedule${newStart ? ` starting ${newStart}` : ''}${justification ? `. Reason: ${justification}` : '.'}`;
    }
    if (item.notificationType === 'EventCancelled') {
      const reason = typeof payload['reason'] === 'string' ? payload['reason'] : null;
      return `${eventTitle} was cancelled${reason ? `. Reason: ${reason}` : '.'}`;
    }
    return `Event: ${eventTitle}`;
  }

  protected date(value: string): string {
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? value : new Intl.DateTimeFormat('en', {
      timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short',
    }).format(parsed) + ' Cairo';
  }

  protected getContextualAction(item: NotificationInboxItem): { label: string; link: string[] } | null {
    const eventId = item.eventId;
    switch (item.notificationType) {
      case 'VolunteerSelected':
      case 'DirectLeaderAssigned':
      case 'ReplacementLeaderAssigned':
        return this.session.isLeader() && eventId ? { label: 'View assignment', link: ['/events', eventId, 'my-assignment'] } : null;
      case 'EventRescheduled':
        return this.session.isLeader() && eventId ? { label: 'Update availability', link: ['/events', eventId, 'availability'] } : null;
      case 'ChiefLeaderAppointment':
        return this.session.isChiefLeader() ? { label: 'Review appointments', link: ['/my-appointments'] } : null;
      case 'EventPublished':
        return this.session.isLeader() && eventId ? { label: 'Declare availability', link: ['/events', eventId, 'availability'] } : null;
      default:
        return null;
    }
  }
}
