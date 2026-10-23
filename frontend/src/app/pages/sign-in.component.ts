import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { SessionService } from '../core/auth/session.service';

@Component({
  selector: 'app-sign-in',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="auth-card cd-reading">
      <p class="eyebrow">Account access</p>
      <h1>Sign in</h1>
      <p class="subtitle">Sign in with your Carpediem username and password to access your operational workspace.</p>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <div class="field">
          <label for="userName">Username</label>
          <input id="userName" autocomplete="username" formControlName="userName">
        </div>
        <div class="field">
          <label for="password">Password</label>
          <input id="password" type="password" autocomplete="current-password" formControlName="password">
        </div>
        <button type="submit" [disabled]="form.invalid || saving()">{{ saving() ? 'Signing in…' : 'Sign in' }}</button>
        @if (error()) { <p role="alert">Sign in failed. Check your details and try again.</p> }
      </form>
      <p class="auth-footer">Need a volunteer account? <a routerLink="/register">Register as a Leader</a></p>
    </section>
  `,
  styles: [`
    .auth-card {
      margin-inline: auto;
      padding: var(--cd-space-6);
      background: var(--cd-bg-surface);
      border: 1px solid var(--cd-border-default);
      border-radius: var(--cd-radius-panel);
      box-shadow: var(--cd-shadow-raised);
    }
    .eyebrow {
      margin: 0 0 var(--cd-space-1);
      font-size: var(--cd-font-size-xs);
      font-weight: var(--cd-weight-bold);
      text-transform: uppercase;
      letter-spacing: var(--cd-letter-spacing-caps);
      color: var(--cd-action-primary);
    }
    .subtitle {
      color: var(--cd-text-secondary);
      margin-block-end: var(--cd-space-5);
    }
    form { display: grid; gap: var(--cd-space-4); }
    .field { display: grid; gap: var(--cd-space-2); }
    label { font-weight: var(--cd-weight-semibold); }
    input {
      inline-size: 100%;
      border: 1px solid var(--cd-form-border);
      border-radius: var(--cd-radius-control);
      padding-inline: var(--cd-space-3);
    }
    button {
      margin-block-start: var(--cd-space-2);
      color: var(--cd-text-inverse);
      background: var(--cd-action-primary);
      border: 1px solid var(--cd-action-primary);
      border-radius: var(--cd-radius-control);
      padding-inline: var(--cd-space-4);
      font-weight: var(--cd-weight-semibold);
    }
    [role=alert] {
      margin: 0;
      padding: var(--cd-space-3);
      border-radius: var(--cd-radius-control);
      background: var(--cd-status-danger-bg);
      border-inline-start: 4px solid var(--cd-status-danger-fg);
      color: var(--cd-status-danger-fg);
      font-weight: var(--cd-weight-medium);
    }
    .auth-footer {
      margin: var(--cd-space-5) 0 0;
      padding-block-start: var(--cd-space-4);
      border-block-start: 1px solid var(--cd-border-default);
      font-size: var(--cd-font-size-sm);
      color: var(--cd-text-secondary);
    }
    .auth-footer a {
      display: inline-flex;
      align-items: center;
      min-block-size: var(--cd-touch-target-min);
      padding-inline: var(--cd-space-2);
      color: var(--cd-action-primary);
      font-weight: var(--cd-weight-semibold);
    }
  `],
})
export class SignInComponent {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  protected readonly saving = signal(false);
  protected readonly error = signal(false);
  protected readonly form = new FormGroup({ userName: new FormControl('', { nonNullable: true, validators: Validators.required }), password: new FormControl('', { nonNullable: true, validators: Validators.required }) });
  protected submit(): void {
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true); this.error.set(false);
    this.session.login(this.form.getRawValue()).pipe(finalize(() => this.saving.set(false))).subscribe({ next: () => void this.router.navigate(['/']), error: () => this.error.set(true) });
  }
}
