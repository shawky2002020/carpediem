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
    <header>
      <p class="eyebrow">Field Operations</p>
      <h1>Event incidents</h1>
      <p class="subtitle">Report a factual incident. An Admin will review it.</p>
    </header>
    @if (error()) { <p role="alert" class="alert-error">{{ error() }}</p> }
    @if (success()) { <p role="status" class="alert-success">{{ success() }}</p> }
    <form [formGroup]="form" (ngSubmit)="submit()">
      <label for="incident-title">Title</label><input id="incident-title" formControlName="title" maxlength="200">
      <label for="incident-description">Description</label><textarea id="incident-description" formControlName="description" maxlength="4000"></textarea>
      <label for="incident-occurred">Occurred at (optional, your local time)</label><input id="incident-occurred" type="datetime-local" formControlName="occurredAt">
      <button type="submit" [disabled]="form.invalid || saving()">Submit incident</button>
    </form>
    <section class="reports-section">
      <h2>Reports</h2>
      @if (loading()) { <p role="status">Loading incidents…</p> }
      @if (!loading() && !items().length) { <p class="empty-state">No incidents are visible in your scope.</p> }
      <ul>@for (item of items(); track item.id) {
        <li class="report-card">
          <h3>{{ item.title }}</h3>
          <p>{{ item.description }}</p>
          <p class="report-meta">Occurred: <time [attr.datetime]="item.occurredAt">{{ formatDate(item.occurredAt) }}</time></p>
          <p class="report-status" [class.reviewed]="!!item.reviewedAt">{{ item.reviewedAt ? 'Reviewed by Admin' : 'Awaiting Admin review' }}</p>
          @if (item.reviewNote) { <p class="review-note">Review note: {{ item.reviewNote }}</p> }
        </li>
      }</ul>
    </section>
  </section>`,
  styles: [`
    .incidents-page { display: grid; gap: var(--cd-space-5); }
    .eyebrow { margin: 0 0 var(--cd-space-1); font-size: var(--cd-font-size-xs); font-weight: var(--cd-weight-bold); text-transform: uppercase; letter-spacing: var(--cd-letter-spacing-caps); color: var(--cd-text-secondary); }
    .subtitle { margin: 0; color: var(--cd-text-secondary); }
    .alert-error { margin: 0; padding: var(--cd-space-3) var(--cd-space-4); border-radius: var(--cd-radius-control); background: var(--cd-status-danger-bg); border-inline-start: 4px solid var(--cd-status-danger-fg); color: var(--cd-status-danger-fg); }
    .alert-success { margin: 0; padding: var(--cd-space-3) var(--cd-space-4); border-radius: var(--cd-radius-control); background: var(--cd-status-success-bg); border-inline-start: 4px solid var(--cd-status-success-fg); color: var(--cd-status-success-fg); }
    form { display: grid; gap: var(--cd-space-3); padding: var(--cd-space-5); border: 1px solid var(--cd-border-default); border-radius: var(--cd-radius-panel); background: var(--cd-bg-surface); }
    label { font-weight: var(--cd-weight-semibold); }
    input, textarea { inline-size: 100%; }
    textarea { min-block-size: 7rem; }
    button { min-block-size: var(--cd-touch-target-min); justify-self: start; }
    .reports-section { display: grid; gap: var(--cd-space-3); }
    .empty-state { padding: var(--cd-space-4); border: 1px solid var(--cd-border-default); border-radius: var(--cd-radius-panel); background: var(--cd-bg-surface); color: var(--cd-text-secondary); }
    ul { list-style: none; padding: 0; margin: 0; display: grid; gap: var(--cd-space-3); }
    .report-card { padding: var(--cd-space-4); border: 1px solid var(--cd-border-default); border-radius: var(--cd-radius-panel); background: var(--cd-bg-surface); display: grid; gap: var(--cd-space-2); }
    .report-card h3 { margin: 0; }
    .report-meta { margin: 0; font-size: var(--cd-font-size-sm); color: var(--cd-text-secondary); }
    .report-status { margin: 0; display: inline-flex; width: fit-content; padding: 0.2rem 0.65rem; border-radius: var(--cd-radius-pill); background: var(--cd-status-warning-bg); color: var(--cd-status-warning-fg); font-size: var(--cd-font-size-xs); font-weight: var(--cd-weight-bold); }
    .report-status.reviewed { background: var(--cd-status-success-bg); color: var(--cd-status-success-fg); }
    .review-note { margin: 0; padding: var(--cd-space-3); border-radius: var(--cd-radius-control); background: var(--cd-bg-canvas); border: 1px solid var(--cd-border-default); }
  `],
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
