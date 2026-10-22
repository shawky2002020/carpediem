import { Component, inject } from '@angular/core';
import { NotificationInboxItem, NotificationInboxService } from './notification-inbox.service';

@Component({
  selector: 'app-notification-inbox',
  templateUrl: './notification-inbox.component.html',
  styleUrl: './notification-inbox.component.scss',
})
export class NotificationInboxComponent {
  protected readonly inbox = inject(NotificationInboxService);

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
    return `Event: ${eventTitle}`;
  }

  protected date(value: string): string {
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? value : new Intl.DateTimeFormat('en', {
      timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short',
    }).format(parsed) + ' Cairo';
  }
}
