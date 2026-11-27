import { NgTemplateOutlet } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NavigationIconComponent } from '../../layout/navigation-icon.component';
import {
  accountName, AccountPage, AccountStatus, accountStatuses, AccountView, roleLabel, secondaryEmail,
} from './account-directory.service';

const relativeTime = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });

@Component({
  selector: 'app-account-results',
  imports: [NgTemplateOutlet, RouterLink, NavigationIconComponent],
  templateUrl: './account-results.component.html',
  styleUrl: './account-results.component.scss',
})
export class AccountResultsComponent {
  readonly page = input.required<AccountPage | null>();
  readonly view = input.required<AccountView>();
  readonly loading = input.required<boolean>();
  readonly failed = input.required<boolean>();
  readonly detailNavigation = input.required<{ accountsListKey: string }>();
  readonly pageChange = output<number>();
  readonly queryReset = output<void>();
  readonly accountOpen = output<{ event: MouseEvent; accountId: string }>();

  protected readonly accountName = accountName;
  protected readonly secondaryEmail = secondaryEmail;
  protected readonly roleLabel = roleLabel;

  protected statusLabel(status: AccountStatus): string {
    return accountStatuses[status];
  }

  protected requestAge(requested: string): string {
    const hours = Math.max(0, Math.floor((Date.now() - Date.parse(requested)) / 3_600_000));
    if (!hours) return 'less than an hour ago';
    return hours < 24 ? relativeTime.format(-hours, 'hour') : relativeTime.format(-Math.floor(hours / 24), 'day');
  }
}
