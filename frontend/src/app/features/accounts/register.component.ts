import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, switchMap } from 'rxjs';
import { SessionService } from '../../core/auth/session.service';
import { toApiProblem } from '../../core/http/api.interceptor';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule],
  template: `<section class="cd-reading"><h1>Register as a Leader</h1>
    <p>An Admin will review your account before you can sign in.</p>
    @if (done()) { <p role="status">Registration received. You can sign in after an Admin approves your account.</p> }
    @else { <form [formGroup]="form" (ngSubmit)="submit()">
      <label for="register-name">Username</label><input id="register-name" autocomplete="username" formControlName="userName">
      <label for="register-email">Email</label><input id="register-email" type="email" autocomplete="email" formControlName="email">
      <label for="register-password">Password</label><input id="register-password" type="password" autocomplete="new-password" formControlName="password">
      <p>Use at least 6 characters with upper and lower case letters, a number, and a symbol.</p>
      <button type="submit" [disabled]="form.invalid || saving()">{{ saving() ? 'Submitting…' : 'Register' }}</button>
      @if (error()) { <p role="alert">{{ error() }}</p> }
    </form> }
  </section>`,
  styles: ['form { display:grid; gap:var(--cd-space-3); } input { inline-size:100%; border:1px solid var(--cd-form-border); border-radius:var(--cd-radius-control); padding-inline:var(--cd-space-3); } button { color:var(--cd-text-inverse); background:var(--cd-action-primary); border:0; border-radius:var(--cd-radius-control); padding:var(--cd-space-3); }'],
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
