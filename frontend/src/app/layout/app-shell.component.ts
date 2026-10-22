import { Component, inject, OnInit } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { SessionService } from '../core/auth/session.service';
import { NotificationInboxService } from '../features/notifications/notification-inbox.service';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterOutlet],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.scss',
})
export class AppShellComponent implements OnInit {
  protected readonly session = inject(SessionService);
  protected readonly notifications = inject(NotificationInboxService);
  ngOnInit(): void { this.session.ensureLoaded().subscribe(); }
}
