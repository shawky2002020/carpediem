import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { SessionService } from '../core/auth/session.service';

@Component({
  selector: 'app-sign-in',
  imports: [ReactiveFormsModule],
  template: `<section class="cd-reading"><h1>Sign in</h1><form [formGroup]="form" (ngSubmit)="submit()"><label for="userName">Username</label><input id="userName" autocomplete="username" formControlName="userName"><label for="password">Password</label><input id="password" type="password" autocomplete="current-password" formControlName="password"><button type="submit" [disabled]="form.invalid || saving()">{{ saving() ? 'Signing in…' : 'Sign in' }}</button>@if (error()) { <p role="alert">Sign in failed. Check your details and try again.</p> }</form></section>`,
  styles: ['form { display: grid; gap: var(--cd-space-3); } input { inline-size: 100%; border: 1px solid var(--cd-form-border); border-radius: var(--cd-radius-control); padding-inline: var(--cd-space-3); } button { margin-block-start: var(--cd-space-3); color: var(--cd-text-inverse); background: var(--cd-action-primary); border: 0; border-radius: var(--cd-radius-control); padding-inline: var(--cd-space-4); } [role=alert] { color: var(--cd-status-danger-fg); }'],
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
