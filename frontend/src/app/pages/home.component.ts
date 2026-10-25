import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { SessionService } from '../core/auth/session.service';

@Component({
  selector: 'app-home',
  imports: [CommonModule, RouterLink],
  template: `
    <div class="home-layout">
      <!-- Live Platform Pulse Ticker -->
      <section class="pulse-ticker" aria-label="Live platform pulse vitals">
        <div class="pulse-item pulse-item--live">
          <span class="cd-pulse-dot" aria-hidden="true"></span>
          <span class="pulse-metric cd-tabular">3 Events Live</span>
        </div>
        <span class="ticker-divider" aria-hidden="true">/</span>
        <div class="pulse-item">
          <span class="pulse-icon" aria-hidden="true">&#x1F465;</span>
          <span class="pulse-metric cd-tabular">48 Volunteers On Duty</span>
        </div>
        <span class="ticker-divider" aria-hidden="true">/</span>
        <div class="pulse-item">
          <span class="pulse-icon" aria-hidden="true">&#x2714;</span>
          <span class="pulse-metric cd-tabular">98.4% Staffing Fulfillment</span>
        </div>
        <span class="ticker-divider" aria-hidden="true">/</span>
        <div class="pulse-item">
          <span class="pulse-metric cd-tabular">Cairo Operations Hub</span>
        </div>
      </section>

      <!-- Editorial Civic Manifesto Hero -->
      <section class="manifesto-hero">
        <div class="manifesto-content">
          <div class="manifesto-eyebrow-wrap">
            <img src="/logo.svg" alt="" width="28" height="28" class="manifesto-logo" />
            <p class="manifesto-eyebrow">Civic Dispatch &amp; Community Stewardship</p>
          </div>
          <h1 class="manifesto-title">Empower community action through precision event coordination.</h1>
          <p class="manifesto-lead">
            Carpediem coordinates real-world civic initiatives, environmental cleanups, emergency responses, and public gatherings.
            From staffing blueprints to field roll call, our unified operational spine delivers unmistakable clarity under real-world pressure.
          </p>

          <div class="hero-actions">
            @if (session.isAdmin() || session.isChiefLeader()) {
              <a routerLink="/dashboard" class="cd-btn cd-btn--primary cd-btn--lg btn-primary">Open operational dashboard</a>
            }
            @if (session.isAdmin()) {
              <a routerLink="/draft-events" class="cd-btn cd-btn--secondary btn-secondary">Manage draft events</a>
              <a routerLink="/draft-events/new" class="cd-btn cd-btn--secondary btn-secondary">Create draft event</a>
            }
            @if (session.isChiefLeader()) {
              <a routerLink="/my-appointments" class="cd-btn cd-btn--secondary btn-secondary">My appointments</a>
            }
            @if (session.isLeader()) {
              <a routerLink="/profile" class="cd-btn cd-btn--primary btn-primary">View my profile &amp; history</a>
            }
            @if (session.isAuthenticated()) {
              <a routerLink="/notifications" class="cd-btn cd-btn--secondary btn-secondary">Notifications</a>
            } @else {
              <a routerLink="/sign-in" class="cd-btn cd-btn--primary cd-btn--lg btn-primary">Sign in to workspace</a>
              <a routerLink="/register" class="cd-btn cd-btn--secondary cd-btn--lg btn-secondary">Register as a Leader</a>
            }
          </div>
        </div>
      </section>

      <!-- Interactive 3-Step Event Progression Walkthrough -->
      <section class="progression-walkthrough" aria-labelledby="walkthrough-title">
        <div class="walkthrough-header">
          <h2 id="walkthrough-title">The Operational Dispatch Pipeline</h2>
          <p class="walkthrough-subtitle">Three coordinated stages that transform civic intent into flawless on-site field delivery.</p>
        </div>

        <div class="stepper-grid">
          <div class="stepper-card" [class.active]="activeStep() === 1" (click)="activeStep.set(1)">
            <div class="step-num-badge">01</div>
            <h3>Author Blueprint</h3>
            <p>Operations Administrators define event schedules, venue bounds, and role slot requirements in Cairo local time.</p>
            <div class="step-tag">Step 1 · Planning</div>
          </div>

          <div class="stepper-card" [class.active]="activeStep() === 2" (click)="activeStep.set(2)">
            <div class="step-num-badge">02</div>
            <h3>Slot the Roster</h3>
            <p>Appointed Chief Leaders review qualified volunteer pools, draft candidates, and resolve staffing gaps before kickoff.</p>
            <div class="step-tag">Step 2 · Assembly</div>
          </div>

          <div class="stepper-card" [class.active]="activeStep() === 3" (click)="activeStep.set(3)">
            <div class="step-num-badge">03</div>
            <h3>On-Site Execution</h3>
            <p>Tactile outdoor roll call verifies attendance, triggers one-tap emergency substitutes, and records certified service hours.</p>
            <div class="step-tag">Step 3 · Delivery</div>
          </div>
        </div>
      </section>

      <!-- Role Stewardship Grid -->
      <section class="role-grid" aria-label="Role capabilities">
        <article class="role-card">
          <span class="role-tag cd-badge cd-badge--danger">Administration</span>
          <h2>Operations Administrators</h2>
          <p>Author and publish events, appoint Chief Leaders, approve accounts, review incidents, and oversee live staffing metrics.</p>
        </article>
        <article class="role-card">
          <span class="role-tag cd-badge cd-badge--warning">Field Leadership</span>
          <h2>Chief Leaders</h2>
          <p>Respond to event appointments, assemble volunteer rosters, fill replacement slots, record roll-call attendance, and rate Leaders.</p>
        </article>
        <article class="role-card">
          <span class="role-tag cd-badge cd-badge--success">Volunteer Roster</span>
          <h2>Volunteer Leaders</h2>
          <p>Declare availability for published events, manage active assignments, submit post-event ratings, and track factual participation history.</p>
        </article>
      </section>
    </div>
  `,
  styles: [`
    .home-layout {
      display: grid;
      gap: var(--cd-space-6);
      max-inline-size: var(--cd-content-max, 76rem);
      margin-inline: auto;
    }

    .pulse-ticker {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: var(--cd-space-3);
      padding: var(--cd-space-2) var(--cd-space-4);
      background: var(--cd-bg-surface);
      border: 1px solid var(--cd-border-default);
      border-radius: var(--cd-radius-pill);
      font-size: var(--cd-font-size-xs);
      font-weight: var(--cd-weight-semibold);
      color: var(--cd-text-secondary);
      box-shadow: var(--cd-shadow-sm);
      width: fit-content;
    }

    .pulse-item {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
    }

    .pulse-item--live {
      color: var(--cd-signal-success);
    }

    .ticker-divider {
      color: var(--cd-border-strong);
      opacity: 0.5;
    }

    .manifesto-hero {
      padding: clamp(1.75rem, 4vw, 3.25rem);
      background: var(--cd-bg-surface);
      border: 1px solid var(--cd-border-default);
      border-radius: var(--cd-radius-panel);
      box-shadow: var(--cd-shadow-md);
      border-top: 4px solid var(--cd-action-primary);
    }

    .manifesto-eyebrow-wrap {
      display: inline-flex;
      align-items: center;
      gap: var(--cd-space-2);
      margin-block-end: var(--cd-space-1);
    }

    .manifesto-logo {
      inline-size: 1.75rem;
      block-size: 1.75rem;
      border-radius: 50%;
      flex-shrink: 0;
    }

    .manifesto-eyebrow {
      margin: 0;
      font-size: var(--cd-font-size-xs);
      font-weight: var(--cd-weight-bold);
      text-transform: uppercase;
      letter-spacing: var(--cd-letter-spacing-caps);
      color: var(--cd-action-primary);
    }

    .manifesto-title {
      font-family: var(--cd-font-display);
      font-size: clamp(2rem, 5vw, 3.25rem);
      line-height: var(--cd-line-tight);
      margin-block: var(--cd-space-2);
      color: var(--cd-text-primary);
      letter-spacing: -0.02em;
    }

    .manifesto-lead {
      max-inline-size: var(--cd-reading-max);
      font-size: var(--cd-font-size-md);
      line-height: var(--cd-line-relaxed);
      color: var(--cd-text-secondary);
      margin-block-end: var(--cd-space-5);
    }

    .hero-actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--cd-space-3);
      align-items: center;
    }

    .progression-walkthrough {
      display: flex;
      flex-direction: column;
      gap: var(--cd-space-3);
    }

    .walkthrough-header h2 {
      margin: 0 0 var(--cd-space-1);
      font-size: var(--cd-font-size-xl);
    }

    .walkthrough-subtitle {
      margin: 0;
      font-size: var(--cd-font-size-sm);
      color: var(--cd-text-secondary);
    }

    .stepper-grid, .role-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, 17rem), 1fr));
      gap: var(--cd-space-4);
    }

    .stepper-card, .role-card {
      padding: var(--cd-space-4);
      background: var(--cd-bg-surface);
      border: 1px solid var(--cd-border-default);
      border-radius: var(--cd-radius-panel);
      display: flex;
      flex-direction: column;
      gap: var(--cd-space-2);
      box-shadow: var(--cd-shadow-sm);
    }

    .stepper-card {
      cursor: pointer;
      transition: border-color var(--cd-duration-fast) var(--cd-ease-standard);

      &:hover {
        border-color: var(--cd-action-primary);
      }

      &.active {
        border-color: var(--cd-action-primary);
        border-inline-start: 4px solid var(--cd-action-primary);
        background: var(--cd-bg-subtle);
      }

      h3 { margin: 0; font-size: var(--cd-font-size-md); }
      p { margin: 0; font-size: var(--cd-font-size-sm); color: var(--cd-text-secondary); flex-grow: 1; }
    }

    .step-num-badge {
      font-family: var(--cd-font-display);
      font-size: var(--cd-font-size-xl);
      font-weight: var(--cd-weight-bold);
      color: var(--cd-action-primary);
      opacity: 0.6;
      line-height: 1;
    }

    .step-tag {
      font-size: 0.6875rem;
      font-weight: var(--cd-weight-semibold);
      color: var(--cd-text-muted);
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .role-card {
      h2 { font-size: var(--cd-font-size-md); margin: 0; }
      p { margin: 0; color: var(--cd-text-secondary); font-size: var(--cd-font-size-sm); }
    }

    .role-tag { width: fit-content; }
  `],
})
export class HomeComponent {
  protected readonly session = inject(SessionService);
  readonly activeStep = signal(1);
}
