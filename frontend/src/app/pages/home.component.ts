import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionService } from '../core/auth/session.service';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  template: `
    <div class="home-layout">
      <section class="hero-card">
        <p class="eyebrow">Operations workspace</p>
        <h1>Plan events with confidence.</h1>
        <p class="lead">
          Authorized administrators can create and manage Draft Events, appoint Chief Leaders,
          coordinate volunteer Leader staffing cycles, and track on-site attendance in one unified platform.
        </p>
        <div class="hero-actions">
          @if (session.isAdmin() || session.isChiefLeader()) {
            <a routerLink="/dashboard" class="btn-primary">Open operational dashboard</a>
          }
          @if (session.isAdmin()) {
            <a routerLink="/draft-events" class="btn-secondary">Manage draft events</a>
            <a routerLink="/draft-events/new" class="btn-secondary">Create draft event</a>
          }
          @if (session.isChiefLeader()) {
            <a routerLink="/my-appointments" class="btn-secondary">My appointments</a>
          }
          @if (session.isLeader()) {
            <a routerLink="/profile" class="btn-primary">View my profile & history</a>
          }
          @if (session.isAuthenticated()) {
            <a routerLink="/notifications" class="btn-secondary">Notifications</a>
          } @else {
            <a routerLink="/sign-in" class="btn-primary">Sign in to workspace</a>
            <a routerLink="/register" class="btn-secondary">Register as a Leader</a>
          }
        </div>
      </section>

      <section class="role-grid" aria-label="Role capabilities">
        <article class="role-card">
          <p class="role-tag">Administration</p>
          <h2>Operations Administrators</h2>
          <p>Author and publish events, appoint Chief Leaders, approve accounts, review incidents, and oversee live staffing metrics.</p>
        </article>
        <article class="role-card">
          <p class="role-tag">Field Leadership</p>
          <h2>Chief Leaders</h2>
          <p>Respond to event appointments, assemble volunteer rosters, fill replacement slots, record roll-call attendance, and rate Leaders.</p>
        </article>
        <article class="role-card">
          <p class="role-tag">Volunteer Roster</p>
          <h2>Volunteer Leaders</h2>
          <p>Declare availability for published events, manage active assignments, submit post-event ratings, and track factual participation history.</p>
        </article>
      </section>
    </div>
  `,
  styles: [`
    .home-layout { display: grid; gap: var(--cd-space-6); }
    .hero-card {
      padding: clamp(1.5rem, 4vw, 2.75rem);
      background: var(--cd-bg-surface);
      border: 1px solid var(--cd-border-default);
      border-radius: var(--cd-radius-panel);
      box-shadow: var(--cd-shadow-raised);
    }
    .eyebrow {
      margin: 0;
      font-size: var(--cd-font-size-xs);
      font-weight: var(--cd-weight-bold);
      text-transform: uppercase;
      letter-spacing: var(--cd-letter-spacing-caps);
      color: var(--cd-action-primary);
    }
    h1 {
      font-family: var(--cd-font-display);
      font-size: clamp(2rem, 5vw, 3.25rem);
      line-height: var(--cd-line-tight);
      margin-block: var(--cd-space-3);
      color: var(--cd-text-primary);
    }
    .lead {
      max-inline-size: var(--cd-reading-max);
      font-size: var(--cd-font-size-lg);
      line-height: var(--cd-line-relaxed);
      color: var(--cd-text-secondary);
      margin-block-end: var(--cd-space-6);
    }
    .hero-actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--cd-space-3);
    }
    .btn-primary, .btn-secondary {
      min-block-size: var(--cd-touch-target-min);
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: var(--cd-space-2) var(--cd-space-5);
      border-radius: var(--cd-radius-control);
      font-weight: var(--cd-weight-semibold);
      text-decoration: none;
    }
    .btn-primary {
      background: var(--cd-action-primary);
      border: 1px solid var(--cd-action-primary);
      color: var(--cd-text-inverse);
    }
    .btn-primary:hover {
      background: var(--cd-action-primary-hover);
      border-color: var(--cd-action-primary-hover);
    }
    .btn-secondary {
      background: var(--cd-bg-surface);
      border: 1px solid var(--cd-border-strong);
      color: var(--cd-text-primary);
    }
    .btn-secondary:hover {
      background: var(--cd-bg-subtle);
    }
    .role-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, 18rem), 1fr));
      gap: var(--cd-space-4);
    }
    .role-card {
      padding: var(--cd-space-5);
      background: var(--cd-bg-surface);
      border: 1px solid var(--cd-border-default);
      border-radius: var(--cd-radius-panel);
    }
    .role-tag {
      margin: 0 0 var(--cd-space-2);
      font-size: var(--cd-font-size-xs);
      font-weight: var(--cd-weight-bold);
      text-transform: uppercase;
      letter-spacing: var(--cd-letter-spacing-caps);
      color: var(--cd-text-secondary);
    }
    .role-card h2 {
      font-size: var(--cd-font-size-lg);
      margin: 0 0 var(--cd-space-2);
    }
    .role-card p:last-child {
      margin: 0;
      color: var(--cd-text-secondary);
      font-size: var(--cd-font-size-sm);
      line-height: var(--cd-line-relaxed);
    }
  `],
})
export class HomeComponent {
  protected readonly session = inject(SessionService);
}
