import { Component, signal } from '@angular/core';

@Component({
  selector: 'app-account-access',
  template: `
    <div class="access-layout">
      <div class="access-brand" aria-hidden="true">
        <img src="/brand/camp-wordmark.webp" alt="" width="448" height="239" />
      </div>
      <div class="access-content"><ng-content /></div>
    </div>
  `,
  styleUrl: './account-access.component.scss',
})
export class AccountAccessComponent {
  readonly passwordVisible = signal(false);
  togglePassword(): void { this.passwordVisible.update(visible => !visible); }
}
