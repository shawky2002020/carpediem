import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NotificationInboxItem, NotificationInboxService } from './notification-inbox.service';

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

  readonly groupedNotifications = computed<NotificationGroup[]>(() => {
    const items = this.inbox.items();
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
    if (thisWeekItems.length > 0) groups.push({ label: 'This Week', items: thisWeekItems });
    if (olderItems.length > 0) groups.push({ label: 'Older', items: olderItems });

    // Fallback if timestamps are far in the future or weird
    if (groups.length === 0 && items.length > 0) {
      groups.push({ label: 'Recent Notifications', items });
    }

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
    let payload: Record<string, unknown> = {};
    try {
      const value: unknown = JSON.parse(item.contextPayload ?? '{}');
      if (value && typeof value === 'object' && !Array.isArray(value)) {
        payload = value as Record<string, unknown>;
      }
    } catch { /* Older payloads may not contain JSON. */ }
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
    if (!item.eventId && item.notificationType !== 'ChiefLeaderAppointment') return null;

    switch (item.notificationType) {
      case 'VolunteerSelected':
      case 'DirectLeaderAssigned':
      case 'ReplacementLeaderAssigned':
        return item.eventId ? { label: 'View Shift Details', link: ['/events', item.eventId, 'my-assignment'] } : null;
      case 'EventRescheduled':
        return item.eventId ? { label: 'Update Availability', link: ['/events', item.eventId, 'availability'] } : null;
      case 'ChiefLeaderAppointment':
        return { label: 'View Appointment', link: item.eventId ? ['/my-appointments', item.eventId] : ['/my-appointments'] };
      case 'EventPublished':
        return item.eventId ? { label: 'Declare Availability', link: ['/events', item.eventId, 'availability'] } : null;
      default:
        return null;
    }
  }
}
