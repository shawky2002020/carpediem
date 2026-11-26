import { Component, computed, inject, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NavigationIconComponent } from './navigation-icon.component';
import { SessionService } from '../core/auth/session.service';

@Component({
  selector: 'app-quick-navigation',
  imports: [NavigationIconComponent, RouterLink, RouterLinkActive],
  templateUrl: './quick-navigation.component.html',
  styleUrl: './quick-navigation.component.scss',
})
export class QuickNavigationComponent {
  protected readonly session = inject(SessionService);
  protected readonly managesEvents = computed(() => this.session.isAdmin() || this.session.isChiefLeader());
  readonly unreadLabel = input.required<string | null>();
}
