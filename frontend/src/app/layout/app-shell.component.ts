import { Component, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SessionService } from '../core/auth/session.service';
import { NotificationInboxService } from '../features/notifications/notification-inbox.service';

@Component({
  selector: 'app-shell',
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.scss',
})
export class AppShellComponent implements OnInit {
  protected readonly session = inject(SessionService);
  protected readonly notifications = inject(NotificationInboxService);
  private readonly router = inject(Router);

  readonly roleLabel = computed(() => {
    if (this.session.isAdmin()) return 'Admin';
    if (this.session.isChiefLeader()) return 'Chief Leader';
    if (this.session.isLeader()) return 'Volunteer';
    return '';
  });

  readonly roleBadgeClass = computed(() => {
    if (this.session.isAdmin()) return 'cd-badge--danger';
    if (this.session.isChiefLeader()) return 'cd-badge--warning';
    if (this.session.isLeader()) return 'cd-badge--success';
    return 'cd-badge--neutral';
  });

  readonly dashboardLink = computed(() => {
    if (this.session.isAdmin() || this.session.isChiefLeader()) return '/dashboard';
    if (this.session.isLeader()) return '/profile';
    return '/sign-in';
  });

  readonly dashboardLabel = computed(() => {
    if (this.session.isAdmin() || this.session.isChiefLeader()) return 'Dashboard';
    if (this.session.isLeader()) return 'My Profile';
    return 'Sign in';
  });

  ngOnInit(): void {
    this.session.ensureLoaded().subscribe();
  }

  signOut(): void {
    this.session.logout().subscribe({
      next: () => {
        void this.router.navigateByUrl('/sign-in');
      },
    });
  }
}
