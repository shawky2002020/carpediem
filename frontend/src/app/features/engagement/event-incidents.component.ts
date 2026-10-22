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
  template: `<section class="cd-reading"><h1>Event incidents</h1>
    <p>Report a factual incident. An Admin will review it.</p>
    @if (error()) { <p role="alert">{{ error() }}</p> }
    @if (success()) { <p role="status">{{ success() }}</p> }
    <form [formGroup]="form" (ngSubmit)="submit()">
      <label for="incident-title">Title</label><input id="incident-title" formControlName="title" maxlength="200">
      <label for="incident-description">Description</label><textarea id="incident-description" formControlName="description" maxlength="4000"></textarea>
      <label for="incident-occurred">Occurred at (optional, your local time)</label><input id="incident-occurred" type="datetime-local" formControlName="occurredAt">
      <button type="submit" [disabled]="form.invalid || saving()">Submit incident</button>
    </form>
    <h2>Reports</h2>
    @if (loading()) { <p role="status">Loading incidents…</p> }
    @if (!loading() && !items().length) { <p>No incidents are visible in your scope.</p> }
    <ul>@for (item of items(); track item.id) {
      <li><h3>{{ item.title }}</h3><p>{{ item.description }}</p><p>Occurred: <time [attr.datetime]="item.occurredAt">{{ formatDate(item.occurredAt) }}</time></p>
        <p>{{ item.reviewedAt ? 'Reviewed by Admin' : 'Awaiting Admin review' }}</p>
        @if (item.reviewNote) { <p>Review note: {{ item.reviewNote }}</p> }
      </li>
    }</ul>
  </section>`,
  styles: ['form { display:grid; gap:var(--cd-space-2); } input,textarea { inline-size:100%; } textarea { min-block-size:8rem; } button { min-block-size:var(--cd-touch-target-min); } li { margin-block:var(--cd-space-4); }'],
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
