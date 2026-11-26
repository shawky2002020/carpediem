import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionService } from '../core/auth/session.service';

const destinations = {
  dashboard: { url: '/dashboard', label: 'Operational dashboard', description: 'Review staffing, upcoming events, and attendance.', action: 'Open dashboard' },
  drafts: { url: '/draft-events', label: 'Draft events', description: 'Prepare events and appoint Chief Leaders.' },
  appointments: { url: '/my-appointments', label: 'My appointments', description: 'Review and respond to invitations to lead.', action: 'Review appointments' },
  notifications: { url: '/notifications', label: 'Notifications', description: 'Open event updates and requests that need your response.', action: 'Open notifications' },
  profile: { url: '/profile', label: 'My profile', description: 'Review your assignments and participation history.' },
} as const;

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {
  protected readonly session = inject(SessionService);
  protected readonly featured = computed(() => {
    if (this.session.isAdmin()) return destinations.dashboard;
    if (this.session.isChiefLeader()) return destinations.appointments;
    return destinations.notifications;
  });
  protected readonly destinations = computed(() => [
    ...(this.session.isAdmin() || this.session.isChiefLeader() ? [destinations.dashboard] : []),
    ...(this.session.isAdmin() ? [destinations.drafts] : []),
    ...(this.session.isChiefLeader() ? [destinations.appointments] : []),
    destinations.notifications,
    ...(this.session.isLeader() ? [destinations.profile] : []),
  ].filter(item => item.url !== this.featured().url));
}
