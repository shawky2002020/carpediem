import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, switchMap } from 'rxjs';
import { SessionService } from '../../core/auth/session.service';
import { toApiProblem } from '../../core/http/api.interceptor';
import { AccountAccessComponent } from '../../pages/account-access.component';

@Component({
  selector: 'app-register',
  imports: [AccountAccessComponent, ReactiveFormsModule, RouterLink],
  template: `
    <app-account-access #access>
      <section class="account-form" aria-labelledby="register-title">
        <h1 id="register-title">Register as a Leader</h1>
        @if (done()) {
          <div role="status" class="cd-alert cd-alert--success">
            <p>Registration received. You can sign in after an Admin approves your account.</p>
          </div>
        } @else {
          <p class="intro">An Admin will review your account before you can sign in.</p>
          <form [formGroup]="form" (ngSubmit)="submit()">
            <div class="field">
              <label for="register-name">Username</label>
              <input id="register-name" autocomplete="username" formControlName="userName" maxlength="100" required
                [attr.aria-invalid]="form.controls.userName.touched && form.controls.userName.invalid || null"
                [attr.aria-describedby]="form.controls.userName.touched && form.controls.userName.invalid ? 'register-name-error' : null">
              @if (form.controls.userName.touched && form.controls.userName.invalid) {
                <p class="field-error" id="register-name-error">Enter a username.</p>
              }
            </div>
            <div class="field">
              <label for="register-email">Email</label>
              <input id="register-email" type="email" autocomplete="email" formControlName="email" maxlength="256" required
                [attr.aria-invalid]="form.controls.email.touched && form.controls.email.invalid || null"
                [attr.aria-describedby]="form.controls.email.touched && form.controls.email.invalid ? 'register-email-error register-email-hint' : 'register-email-hint'">
              @if (form.controls.email.touched && form.controls.email.invalid) {
                <p class="field-error" id="register-email-error">Enter a valid email address.</p>
              }
              <p class="hint" id="register-email-hint">Administrators can see your email when they review your account.</p>
            </div>
            <div class="field">
              <label for="register-password">Password</label>
              <div class="password-control">
                <input id="register-password" [type]="access.passwordVisible() ? 'text' : 'password'" autocomplete="new-password" formControlName="password" required
                  [attr.aria-invalid]="form.controls.password.touched && form.controls.password.invalid || null"
                  [attr.aria-describedby]="form.controls.password.touched && form.controls.password.invalid ? 'register-password-error register-password-hint' : 'register-password-hint'">
                <button type="button" class="password-toggle" aria-controls="register-password" (click)="access.togglePassword()">
                  {{ access.passwordVisible() ? 'Hide' : 'Show' }}<span class="cd-visually-hidden"> password</span>
                </button>
              </div>
              @if (form.controls.password.touched && form.controls.password.invalid) {
                <p class="field-error" id="register-password-error">Enter a password.</p>
              }
              <p class="hint" id="register-password-hint">Use at least 6 characters with upper and lower case letters, a number, and a symbol.</p>
            </div>
            @if (error(); as message) {
              <div role="alert" class="cd-alert cd-alert--error"><p>{{ message }}</p></div>
            }
            <button type="submit" class="cd-btn cd-btn--primary" [disabled]="form.invalid || saving()">
              {{ saving() ? 'Submitting…' : 'Register' }}
            </button>
          </form>
        }
        <p class="auth-footer">Already have an approved account? <a routerLink="/sign-in">Sign in</a></p>
      </section>
    </app-account-access>
  `,
  styleUrl: '../../pages/account-form.scss',
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
