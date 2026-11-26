import { Component, computed, ElementRef, inject, OnInit, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Location, NgTemplateOutlet } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, finalize } from 'rxjs';
import { SessionService } from '../core/auth/session.service';
import { NotificationInboxService } from '../features/notifications/notification-inbox.service';
import { NavigationIconComponent } from './navigation-icon.component';
import { RouteMotion } from './route-motion';
import { QuickNavigationComponent } from './quick-navigation.component';

@Component({
  selector: 'app-shell',
  host: { '(document:pointerdown)': 'dismissOutside($event)' },
  imports: [NavigationIconComponent, NgTemplateOutlet, QuickNavigationComponent, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.scss',
})
export class AppShellComponent implements OnInit {
  protected readonly session = inject(SessionService);
  protected readonly notifications = inject(NotificationInboxService);
  private readonly routeMotion = new RouteMotion();
  private readonly router = inject(Router);
  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');
  private readonly masthead = viewChild.required<ElementRef<HTMLElement>>('masthead');
  private readonly menuButton = viewChild<ElementRef<HTMLButtonElement>>('menuButton');

  // Reserve the rail on a direct workspace entry while the session request resolves.
  private readonly entryPath = inject(Location).path().split(/[?#]/, 1)[0];
  protected readonly workspaceLayout = computed(() => this.session.isAuthenticated() ||
    (!this.session.loaded() && !['', '/', '/sign-in', '/register'].includes(this.entryPath)));

  protected readonly menuOpen = signal(false);
  protected readonly signingOut = signal(false);
  protected readonly signOutError = signal(false);
  protected readonly roleLabel = computed(() => {
    if (this.session.isAdmin()) return 'Administrator';
    if (this.session.isChiefLeader()) return 'Chief Leader';
    if (this.session.isLeader()) return 'Leader';
    return '';
  });
  protected readonly managesEvents = computed(() => this.session.isAdmin() || this.session.isChiefLeader());
  protected readonly unreadLabel = computed(() => {
    const unread = this.notifications.unreadCount();
    return unread ? 'Notifications, ' + unread + ' unread' : null;
  });

  constructor() {
    let previousPath: string | undefined;
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      takeUntilDestroyed(),
    ).subscribe(event => {
      const path = event.urlAfterRedirects.split(/[?#]/, 1)[0];
      this.menuOpen.set(false);
      if (previousPath !== undefined && previousPath !== path) {
        this.main().nativeElement.focus();
        window.scrollTo(0, 0);
        this.routeMotion.enter(this.main().nativeElement, previousPath, path);
      }
      previousPath = path;
    });
  }

  ngOnInit(): void {
    this.session.ensureLoaded().subscribe();
  }

  protected closeMenu(): void {
    if (!this.menuOpen()) return;
    this.menuOpen.set(false);
    this.menuButton()?.nativeElement.focus();
  }

  protected dismissOutside(event: PointerEvent): void {
    if (this.menuOpen() && event.target instanceof Node && !this.masthead().nativeElement.contains(event.target)) {
      this.menuOpen.set(false);
    }
  }

  protected leaveMenu(event: FocusEvent): void {
    if (this.menuOpen() && event.relatedTarget instanceof Node && !this.masthead().nativeElement.contains(event.relatedTarget)) {
      this.menuOpen.set(false);
    }
  }

  protected skipToContent(event: MouseEvent): void {
    event.preventDefault();
    this.main().nativeElement.focus();
  }

  protected signOut(): void {
    this.signOutError.set(false);
    this.signingOut.set(true);
    this.session.logout().pipe(finalize(() => this.signingOut.set(false))).subscribe({
      next: () => void this.router.navigateByUrl('/sign-in'),
      error: () => this.signOutError.set(true),
    });
  }
}
