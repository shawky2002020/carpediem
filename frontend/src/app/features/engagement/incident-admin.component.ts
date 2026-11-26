import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { toApiProblem } from '../../core/http/api.interceptor';

interface Incident { id: string; eventTitle: string; reporterUserName: string; title: string; description: string; occurredAt: string; reportedAt: string; reviewedAt: string | null; reviewNote: string | null; }
interface IncidentPage { items: Incident[]; page: number; pageSize: number; hasMore: boolean; }

@Component({
  selector: 'app-incident-admin',
  template: `<section class="incident-admin-page cd-reading">
    <header class="page-header">
      <h1>Incident review</h1>
      <p class="subtitle">Read incident reports and record follow-up.</p>
    </header>
    @if (error()) {
      <div role="alert" class="cd-alert cd-alert--error alert-error">
        <p>{{ error() }}</p>
        <button type="button" (click)="load()" [disabled]="loading() || !!busy()">Refresh incidents</button>
      </div>
    }
    @if (success()) {
      <div role="status" class="cd-alert cd-alert--success alert-success">
        <p>{{ success() }}</p>
      </div>
    }
    @if (loading()) {
      <div role="status" class="cd-loading-state">
        <p>Loading incidents…</p>
      </div>
    }
    @if (!loading() && !error() && !items().length) {
      <div class="cd-empty-state empty-state">
        <p>No incidents reported yet.</p>
      </div>
    }
    <ul class="incidents-list">@for (item of items(); track item.id) {
      <li class="incident-card">
        <div class="card-top">
          <div>
            <h2>{{ item.title }}</h2>
            <p class="meta-line">{{ item.eventTitle }} · Reported by <strong>{{ item.reporterUserName }}</strong></p>
          </div>
          <span class="cd-badge status-pill" [class.cd-badge--success]="!!item.reviewedAt" [class.cd-badge--warning]="!item.reviewedAt" [class.reviewed]="!!item.reviewedAt">
            <span class="cd-badge__dot"></span>
            {{ item.reviewedAt ? 'Reviewed' : 'Pending Review' }}
          </span>
        </div>
        <p class="incident-desc">{{ item.description }}</p>
        <p class="meta-sub">Occurred: <time class="cd-tabular" [attr.datetime]="item.occurredAt">{{ formatDate(item.occurredAt) }}</time></p>
        @if (item.reviewedAt) {
          <div class="cd-callout cd-callout--success review-summary">
            <p><strong>Reviewed.</strong> @if (item.reviewNote) { {{ item.reviewNote }} }</p>
          </div>
        } @else {
          <form class="review-form" (submit)="review($event, item)">
            <label [for]="'note-' + item.id">Review note (optional)</label>
            <textarea [id]="'note-' + item.id" name="note" maxlength="1000"></textarea>
            <button type="submit" class="cd-btn cd-btn--sm cd-btn--primary" [disabled]="!!busy() || loading()">
              {{ busy() === item.id ? 'Saving…' : 'Mark reviewed' }}
            </button>
          </form>
        }
      </li>
    }</ul>
    @if (page() > 1 || hasMore()) { <nav aria-label="Incident pages" class="pagination-nav">
      <button type="button" class="cd-btn cd-btn--sm cd-btn--secondary" [disabled]="page() === 1 || loading()" (click)="load(page() - 1)">Previous</button>
      <span class="page-indicator cd-tabular">Page {{ page() }}</span>
      <button type="button" class="cd-btn cd-btn--sm cd-btn--secondary" [disabled]="!hasMore() || loading()" (click)="load(page() + 1)">Next</button>
    </nav> }
  </section>`,
  styleUrl: './incident-admin.component.scss',
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
    this.error.set('');
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
