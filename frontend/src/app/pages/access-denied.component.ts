import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-access-denied',
  imports: [RouterLink],
  template: `
    <section class="cd-reading" aria-labelledby="access-title">
      <p class="eyebrow">Access denied</p>
      <h1 id="access-title">Administrator access is required</h1>
      <p>Your signed-in account cannot open Draft Events. Contact an administrator if your access should have changed.</p>
      <a routerLink="/">Return home</a>
    </section>
  `,
  styles: [`
    .eyebrow { color: var(--cd-text-secondary); font-weight: var(--cd-weight-bold); text-transform: uppercase; }
    a { display: inline-flex; min-block-size: var(--cd-touch-target-min); align-items: center; color: var(--cd-action-primary); }
  `],
})
export class AccessDeniedComponent {}
