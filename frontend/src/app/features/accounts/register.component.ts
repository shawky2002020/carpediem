import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, switchMap } from 'rxjs';
import { SessionService } from '../../core/auth/session.service';
import { toApiProblem } from '../../core/http/api.interceptor';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="auth-card cd-reading">
      <p class="eyebrow">Volunteer onboarding</p>
      <h1>Register as a Leader</h1>
      <p class="subtitle">An Admin will review your account before you can sign in.</p>
      @if (done()) {
        <p role="status" class="status-banner">Registration received. You can sign in after an Admin approves your account.</p>
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="field">
            <label for="register-name">Username</label>
            <input id="register-name" autocomplete="username" formControlName="userName">
          </div>
          <div class="field">
            <label for="register-email">Email</label>
            <input id="register-email" type="email" autocomplete="email" formControlName="email">
          </div>
          <div class="field">
            <label for="register-password">Password</label>
            <input id="register-password" type="password" autocomplete="new-password" formControlName="password">
            <p class="password-hint">Use at least 6 characters with upper and lower case letters, a number, and a symbol.</p>
          </div>
          <button type="submit" [disabled]="form.invalid || saving()">{{ saving() ? 'Submitting…' : 'Register' }}</button>
          @if (error()) { <p role="alert">{{ error() }}</p> }
        </form>
      }
      <p class="auth-footer">Already have an approved account? <a routerLink="/sign-in">Sign in</a></p>
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
    .subtitle { color: var(--cd-text-secondary); margin-block-end: var(--cd-space-5); }
    form { display: grid; gap: var(--cd-space-4); }
    .field { display: grid; gap: var(--cd-space-2); }
    label { font-weight: var(--cd-weight-semibold); }
    input {
      inline-size: 100%;
      border: 1px solid var(--cd-form-border);
      border-radius: var(--cd-radius-control);
      padding-inline: var(--cd-space-3);
    }
    .password-hint {
      margin: 0;
      font-size: var(--cd-font-size-sm);
      color: var(--cd-text-secondary);
    }
    button {
      color: var(--cd-text-inverse);
      background: var(--cd-action-primary);
      border: 1px solid var(--cd-action-primary);
      border-radius: var(--cd-radius-control);
      padding: var(--cd-space-3);
      font-weight: var(--cd-weight-semibold);
    }
    .status-banner {
      padding: var(--cd-space-4);
      border-radius: var(--cd-radius-control);
      background: var(--cd-status-success-bg);
      border-inline-start: 4px solid var(--cd-status-success-fg);
      color: var(--cd-status-success-fg);
      font-weight: var(--cd-weight-medium);
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
export class RegisterComponent {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);
  protected readonly saving = signal(false);
  protected readonly done = signal(false);
  protected readonly error = signal('');
  protected readonly form = new FormGroup({
    userName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email, Validators.maxLength(256)] }),
    password: new FormControl('', { nonNullable: true, validators: Validators.required }),
  });
  protected submit(): void {
    if (this.form.invalid || this.saving()) return;
    this.error.set(''); this.saving.set(true);
    this.session.refreshAntiforgery().pipe(
      switchMap(() => this.http.post('/api/accounts/register', this.form.getRawValue())),
      finalize(() => this.saving.set(false)),
    ).subscribe({ next: () => this.done.set(true), error: err => this.error.set(
      err?.status === 429 ? 'Too many registration attempts. Please try again later.' :
      toApiProblem(err, 'Registration failed.').detail ?? 'Registration failed.') });
  }
}
