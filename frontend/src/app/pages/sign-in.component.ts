import { Location } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { SessionService } from '../core/auth/session.service';
import { AccountAccessComponent } from './account-access.component';

@Component({
  selector: 'app-sign-in',
  imports: [AccountAccessComponent, ReactiveFormsModule, RouterLink],
  template: `
    <app-account-access #access>
      <section class="account-form" aria-labelledby="sign-in-title">
        <h1 id="sign-in-title">Sign in</h1>
        <p class="intro">Use your Carpediem account to open your workspace.</p>
        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="field">
            <label for="userName">Username</label>
            <input id="userName" autocomplete="username" formControlName="userName" required
              [attr.aria-invalid]="form.controls.userName.touched && form.controls.userName.invalid || null"
              [attr.aria-describedby]="form.controls.userName.touched && form.controls.userName.invalid ? 'username-error' : null">
            @if (form.controls.userName.touched && form.controls.userName.invalid) {
              <p class="field-error" id="username-error">Enter your username.</p>
            }
          </div>
          <div class="field">
            <label for="password">Password</label>
            <div class="password-control">
              <input id="password" [type]="access.passwordVisible() ? 'text' : 'password'" autocomplete="current-password" formControlName="password" required
                [attr.aria-invalid]="form.controls.password.touched && form.controls.password.invalid || null"
                [attr.aria-describedby]="form.controls.password.touched && form.controls.password.invalid ? 'password-error' : null">
              <button type="button" class="password-toggle" aria-controls="password" (click)="access.togglePassword()">
                {{ access.passwordVisible() ? 'Hide' : 'Show' }}<span class="cd-visually-hidden"> password</span>
              </button>
            </div>
            @if (form.controls.password.touched && form.controls.password.invalid) {
              <p class="field-error" id="password-error">Enter your password.</p>
            }
          </div>
          @if (error(); as message) {
            <div role="alert" class="cd-alert cd-alert--error"><p>{{ message }}</p></div>
          }
          <button type="submit" class="cd-btn cd-btn--primary" [disabled]="form.invalid || saving()">
            {{ saving() ? 'Signing in…' : 'Sign in' }}
          </button>
        </form>
        <p class="auth-footer">
          <span>New Leader accounts require Admin approval before sign-in.</span>
          <a routerLink="/register">Register as a Leader</a>
        </p>
      </section>
    </app-account-access>
  `,
  styleUrl: './account-form.scss',
})
export class SignInComponent {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  protected readonly saving = signal(false);
  protected readonly error = signal('');
  protected readonly form = new FormGroup({
    userName: new FormControl('', { nonNullable: true, validators: Validators.required }),
    password: new FormControl('', { nonNullable: true, validators: Validators.required }),
  });

  protected submit(): void {
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    this.session.login(this.form.getRawValue()).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => void this.router.navigateByUrl(this.destination(), { replaceUrl: true }),
      error: failure => {
        const status = failure.status;
        if (status === 0 || status >= 500) {
          this.error.set('Carpediem could not be reached. Check your connection and try again.');
        } else if (status === 429) {
          this.error.set('Too many attempts. Wait a moment before signing in again.');
        } else {
          this.error.set('Sign in failed. Check your username and password and try again.');
        }
      },
    });
  }

  private destination(): string {
    const state = this.location.getState();
    const url = state && typeof state === 'object' && 'returnUrl' in state ? state.returnUrl : null;
    // The guard stores the Angular URL in history state. Public query parameters are not used.
    return typeof url === 'string' && url.startsWith('/') && !url.startsWith('//') && !url.includes('\\')
      ? url
      : '/';
  }
}
