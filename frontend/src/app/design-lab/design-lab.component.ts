import { Component, signal } from '@angular/core';
import { DraftEventFormComponent } from '../features/draft-events/components/draft-event-form.component';
import { AccountAccessComponent } from '../pages/account-access.component';
import { NavigationIconComponent } from '../layout/navigation-icon.component';
import { OperationalSpineComponent } from '../shared/components/operational-spine/operational-spine.component';

@Component({
  imports: [NavigationIconComponent, AccountAccessComponent, DraftEventFormComponent, OperationalSpineComponent],
  templateUrl: './design-lab.component.html',
  styleUrl: './design-lab.component.scss',
})
export class DesignLabComponent {
  protected readonly working = signal(false);
  protected readonly intent = signal<'selection' | 'removal'>('selection');
  protected readonly outcome = signal('');
  protected readonly tones = ['neutral', 'info', 'success', 'warning', 'danger'];
}
