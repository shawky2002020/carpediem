import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { toApiProblem } from '../../core/http/api.interceptor';

interface Incident { id: string; title: string; description: string; occurredAt: string; reportedAt: string; reviewedAt: string | null; reviewNote: string | null; }

@Component({
  selector: 'app-event-incidents',
  imports: [ReactiveFormsModule],
  template: `<section class="incidents-page cd-reading">
    <header class="page-header">
      <h1>Event incidents</h1>
      <p class="subtitle">Report what happened for an administrator to review.</p>
    </header>
    @if (error()) {
      <div role="alert" class="cd-alert cd-alert--error alert-error">
        <p>{{ error() }}</p>
      </div>
    }
    @if (success()) {
      <div role="status" class="cd-alert cd-alert--success alert-success">
        <p>{{ success() }}</p>
      </div>
    }
    <form class="incident-form" [formGroup]="form" (ngSubmit)="submit()">
      <div class="field">
        <label for="incident-title">Title</label>
        <input id="incident-title" formControlName="title" maxlength="200" required>
      </div>
      <div class="field">
        <label for="incident-description">Description</label>
        <textarea id="incident-description" formControlName="description" maxlength="4000" required></textarea>
      </div>
      <details class="incident-time">
        <summary>Time of incident <span>(optional)</span></summary>
        <label for="incident-occurred">Occurred at (your local time)</label>
        <input id="incident-occurred" type="datetime-local" formControlName="occurredAt">
      </details>
      <button type="submit" class="cd-btn cd-btn--primary" [disabled]="form.invalid || saving()">
        {{ saving() ? 'Submitting…' : 'Submit incident' }}
      </button>
    </form>
    <section class="reports-section">
      <h2>Reports @if (!loading() && !error()) { <span>{{ items().length }}</span> }</h2>
      @if (loading()) {
        <div role="status" class="cd-loading-state">
          <p>Loading incidents…</p>
        </div>
      }
      @if (!loading() && !items().length) {
        <div class="cd-empty-state empty-state">
          <p>No incidents are visible in your scope.</p>
        </div>
      }
      <ul class="reports-list">@for (item of items(); track item.id) {
        <li class="report-card">
          <div class="report-top">
            <h3>{{ item.title }}</h3>
            <span class="cd-badge report-status" [class.cd-badge--success]="!!item.reviewedAt" [class.cd-badge--warning]="!item.reviewedAt" [class.reviewed]="!!item.reviewedAt">
              <span class="cd-badge__dot"></span>
              {{ item.reviewedAt ? 'Reviewed by Admin' : 'Awaiting Admin review' }}
            </span>
          </div>
          <p class="report-desc">{{ item.description }}</p>
            <p class="report-meta">Occurred <time class="cd-tabular" [attr.datetime]="item.occurredAt">{{ formatDate(item.occurredAt) }}</time></p>
          @if (item.reviewNote) {
            <div class="cd-callout cd-callout--success review-note">
              <p><strong>Review note:</strong> {{ item.reviewNote }}</p>
            </div>
          }
        </li>
      }</ul>
    </section>
  </section>`,
  styleUrl: './event-incidents.component.scss',
})
export class EventIncidentsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  protected readonly items = signal<Incident[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal('');
  protected readonly success = signal('');
  protected readonly form = new FormGroup({
    title: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    description: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] }),
    occurredAt: new FormControl('', { nonNullable: true }),
  });
  private get eventId(): string { return this.route.snapshot.paramMap.get('eventId') ?? ''; }
  ngOnInit(): void { this.load(); }
  protected load(): void {
    this.loading.set(true);
    this.http.get<Incident[]>(`/api/events/${this.eventId}/incidents`)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({ next: data => this.items.set(data), error: err => this.error.set(toApiProblem(err, 'Incidents could not be loaded.').title) });
  }
  protected submit(): void {
    if (this.form.invalid || this.saving()) return;
    const values = this.form.getRawValue();
    const occurredAt = values.occurredAt ? new Date(values.occurredAt).toISOString() : null;
    this.error.set(''); this.success.set(''); this.saving.set(true);
    this.http.post(`/api/events/${this.eventId}/incidents`, { title: values.title, description: values.description, occurredAt })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({ next: () => { this.success.set('Incident submitted for Admin review.'); this.form.reset(); this.load(); },
        error: err => this.error.set(toApiProblem(err, 'Incident could not be submitted.').title) });
  }
  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat('en', { timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) + ' Cairo';
  }
}
