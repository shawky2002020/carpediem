import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

@Component({
  selector: 'app-access-denied',
  imports: [RouterLink],
  template: `
    <section class="access-notice" aria-labelledby="access-title">
      <div class="notice-symbol" aria-hidden="true">
        <svg viewBox="0 0 48 48" width="48" height="48" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M14 23v-7a10 10 0 0 1 20 0v7M10 23h28v19H10zM24 29v7"/>
        </svg>
      </div>
      <div>
        <h1 id="access-title">{{ adminRequired ? 'Administrator access is required' : 'Access restricted' }}</h1>
        <p>Your account cannot open this page. If you need access, contact an administrator.</p>
        <a routerLink="/" class="cd-btn cd-btn--primary">Return home</a>
      </div>
    </section>
  `,
  styles: [`
    .access-notice { display: grid; gap: 1.75rem; max-width: 48rem; margin: clamp(1rem, 6vh, 4.5rem) auto; }
    .notice-symbol { display: grid; place-items: center; width: 5rem; height: 5rem; background: #efe6d2; color: #854221; border-radius: 50%; }
    h1 { font-size: clamp(2.15rem, 5.2vw, 4.5rem); letter-spacing: -.045em; line-height: .99; max-width: 15ch; }
    p { max-width: 42ch; margin-block: 1rem 1.5rem; }
    @media (min-width: 48rem) { .access-notice { grid-template-columns: auto minmax(0,1fr); } }
  `],
})
export class AccessDeniedComponent {
  protected readonly adminRequired = inject(Router).currentNavigation()?.extras.state?.['adminRequired'] === true;
}
