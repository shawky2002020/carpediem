import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { toApiProblem } from '../../core/http/api.interceptor';

interface Incident { id: string; eventTitle: string; reporterUserName: string; title: string; description: string; occurredAt: string; reportedAt: string; reviewedAt: string | null; reviewNote: string | null; }
interface IncidentPage { items: Incident[]; page: number; pageSize: number; hasMore: boolean; }

@Component({
  selector: 'app-incident-admin',
  template: `<section class="incident-admin-page">
    <header class="page-header">
      <p class="eyebrow">Safety & Governance</p>
      <h1>Incident review</h1>
      <p class="subtitle">Review factual incident reports submitted by Chief Leaders and Volunteer Leaders.</p>
    </header>
    @if (error()) { <p role="alert" class="alert-error">{{ error() }}</p> }
    @if (success()) { <p role="status" class="alert-success">{{ success() }}</p> }
    @if (loading()) { <p role="status">Loading incidents…</p> }
    @if (!loading() && !items().length) { <p class="empty-state">No incidents reported yet.</p> }
    <ul>@for (item of items(); track item.id) {
      <li class="incident-card">
        <div class="card-top">
          <h2>{{ item.title }}</h2>
          <span class="status-pill" [class.reviewed]="!!item.reviewedAt">{{ item.reviewedAt ? 'Reviewed' : 'Pending Review' }}</span>
        </div>
        <p class="meta-line">{{ item.eventTitle }} · Reported by {{ item.reporterUserName }}</p>
        <p class="incident-desc">{{ item.description }}</p>
        <p class="meta-sub">Occurred: <time [attr.datetime]="item.occurredAt">{{ formatDate(item.occurredAt) }}</time></p>
        @if (item.reviewedAt) { <p class="review-summary">Reviewed. @if (item.reviewNote) { {{ item.reviewNote }} }</p> }
        @else { <form class="review-form" (submit)="review($event, item)">
          <label [for]="'note-' + item.id">Review note (optional)</label><textarea [id]="'note-' + item.id" name="note" maxlength="1000"></textarea>
          <button type="submit" [disabled]="busy() === item.id">Mark reviewed</button>
        </form> }
      </li>
    }</ul>
    <nav aria-label="Incident pages" class="pagination-nav"><button type="button" [disabled]="page() === 1 || loading()" (click)="load(page() - 1)">Previous</button>
      <span>Page {{ page() }}</span><button type="button" [disabled]="!hasMore() || loading()" (click)="load(page() + 1)">Next</button></nav>
  </section>`,
  styles: [`
    .incident-admin-page { display: grid; gap: var(--cd-space-5); }
    .eyebrow { margin: 0 0 var(--cd-space-1); font-size: var(--cd-font-size-xs); font-weight: var(--cd-weight-bold); text-transform: uppercase; letter-spacing: var(--cd-letter-spacing-caps); color: var(--cd-text-secondary); }
    .subtitle { margin: 0; color: var(--cd-text-secondary); }
    .alert-error { margin: 0; padding: var(--cd-space-3) var(--cd-space-4); border-radius: var(--cd-radius-control); background: var(--cd-status-danger-bg); border-inline-start: 4px solid var(--cd-status-danger-fg); color: var(--cd-status-danger-fg); }
    .alert-success { margin: 0; padding: var(--cd-space-3) var(--cd-space-4); border-radius: var(--cd-radius-control); background: var(--cd-status-success-bg); border-inline-start: 4px solid var(--cd-status-success-fg); color: var(--cd-status-success-fg); }
    .empty-state { padding: var(--cd-space-6); border: 1px solid var(--cd-border-default); border-radius: var(--cd-radius-panel); background: var(--cd-bg-surface); color: var(--cd-text-secondary); }
    ul { list-style: none; padding: 0; margin: 0; display: grid; gap: var(--cd-space-4); }
    .incident-card { border: 1px solid var(--cd-border-default); border-radius: var(--cd-radius-panel); padding: var(--cd-space-5); background: var(--cd-bg-surface); display: grid; gap: var(--cd-space-2); }
    .card-top { display: flex; justify-content: space-between; align-items: flex-start; gap: var(--cd-space-3); }
    .card-top h2 { margin: 0; font-size: var(--cd-font-size-lg); }
    .status-pill { padding: 0.2rem 0.65rem; border-radius: var(--cd-radius-pill); background: var(--cd-status-warning-bg); color: var(--cd-status-warning-fg); font-size: var(--cd-font-size-xs); font-weight: var(--cd-weight-bold); }
    .status-pill.reviewed { background: var(--cd-status-success-bg); color: var(--cd-status-success-fg); }
    .meta-line { margin: 0; font-weight: var(--cd-weight-semibold); color: var(--cd-text-secondary); font-size: var(--cd-font-size-sm); }
    .incident-desc { margin: var(--cd-space-1) 0; }
    .meta-sub { margin: 0; font-size: var(--cd-font-size-sm); color: var(--cd-text-secondary); }
    .review-summary { margin: var(--cd-space-2) 0 0; padding: var(--cd-space-3); border-radius: var(--cd-radius-control); background: var(--cd-status-success-bg); color: var(--cd-status-success-fg); font-weight: var(--cd-weight-medium); }
    .review-form { margin-block-start: var(--cd-space-3); padding-block-start: var(--cd-space-3); border-block-start: 1px solid var(--cd-border-default); display: grid; gap: var(--cd-space-2); }
    label { font-weight: var(--cd-weight-semibold); font-size: var(--cd-font-size-sm); }
    textarea { display: block; inline-size: 100%; min-block-size: 5rem; }
    button { min-block-size: var(--cd-touch-target-min); justify-self: start; }
    .pagination-nav { display: flex; align-items: center; justify-content: space-between; gap: var(--cd-space-4); padding-block-start: var(--cd-space-3); border-block-start: 1px solid var(--cd-border-default); }
  `],
})
export class IncidentAdminComponent implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly items = signal<Incident[]>([]);
  protected readonly loading = signal(true);
  protected readonly page = signal(1);
  protected readonly hasMore = signal(false);
  protected readonly busy = signal<string | null>(null);
  protected readonly error = signal('');
  protected readonly success = signal('');
  ngOnInit(): void { this.load(); }
  protected load(page = this.page()): void {
    this.loading.set(true);
    this.http.get<IncidentPage>(`/api/admin/incidents?page=${page}`).pipe(finalize(() => this.loading.set(false)))
      .subscribe({ next: data => { this.items.set(data.items); this.page.set(data.page); this.hasMore.set(data.hasMore); },
        error: err => this.error.set(toApiProblem(err, 'Incidents could not be loaded.').title) });
  }
  protected review(event: Event, item: Incident): void {
    event.preventDefault();
    if (this.busy()) return;
    const note = String(new FormData(event.target as HTMLFormElement).get('note') ?? '');
    this.error.set(''); this.success.set(''); this.busy.set(item.id);
    this.http.post(`/api/admin/incidents/${item.id}/review`, { note })
      .pipe(finalize(() => this.busy.set(null)))
      .subscribe({ next: () => { this.success.set('Incident marked reviewed.'); this.load(); },
        error: err => this.error.set(toApiProblem(err, 'Review could not be saved.').title) });
  }
  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat('en', { timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) + ' Cairo';
  }
}
