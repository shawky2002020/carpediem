import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { toApiProblem } from '../../core/http/api.interceptor';

interface Feedback { id: string; eventTitle: string; kind: string; raterUserName: string; leaderUserName: string | null; rating: number; comment: string | null; submittedAt: string; }
interface FeedbackPage { items: Feedback[]; page: number; pageSize: number; hasMore: boolean; }

@Component({
  selector: 'app-feedback-admin',
  template: `<section class="feedback-admin-page">
    <header class="page-header">
      <p class="eyebrow">Quality & Governance</p>
      <h1>Feedback</h1>
      <p class="subtitle">Post-event ratings are visible to Admins.</p>
    </header>
    @if (error()) { <p role="alert" class="alert-error">{{ error() }}</p> }
    @if (loading()) { <p role="status">Loading ratings…</p> }
    @if (!loading() && !items().length) { <p class="empty-state">No ratings submitted yet.</p> }
    <ul>@for (item of items(); track item.id) {
      <li class="feedback-card">
        <div class="card-top">
          <h2>{{ item.eventTitle }}</h2>
          <span class="rating-pill">{{ item.rating }} of 5</span>
        </div>
        <p class="meta-line"><strong>{{ item.kind === 'Event' ? 'Event' : item.leaderUserName }}</strong> · {{ item.rating }} of 5</p>
        <p class="meta-sub">Rated by {{ item.raterUserName }} on <time [attr.datetime]="item.submittedAt">{{ formatDate(item.submittedAt) }}</time></p>
        @if (item.comment) { <p class="comment-body">{{ item.comment }}</p> }
      </li>
    }</ul>
    <nav aria-label="Feedback pages" class="pagination-nav"><button type="button" [disabled]="page() === 1 || loading()" (click)="load(page() - 1)">Previous</button>
      <span>Page {{ page() }}</span><button type="button" [disabled]="!hasMore() || loading()" (click)="load(page() + 1)">Next</button></nav>
  </section>`,
  styles: [`
    .feedback-admin-page { display: grid; gap: var(--cd-space-5); }
    .eyebrow { margin: 0 0 var(--cd-space-1); font-size: var(--cd-font-size-xs); font-weight: var(--cd-weight-bold); text-transform: uppercase; letter-spacing: var(--cd-letter-spacing-caps); color: var(--cd-text-secondary); }
    .subtitle { margin: 0; color: var(--cd-text-secondary); }
    .alert-error { margin: 0; padding: var(--cd-space-3) var(--cd-space-4); border-radius: var(--cd-radius-control); background: var(--cd-status-danger-bg); border-inline-start: 4px solid var(--cd-status-danger-fg); color: var(--cd-status-danger-fg); }
    .empty-state { padding: var(--cd-space-6); border: 1px solid var(--cd-border-default); border-radius: var(--cd-radius-panel); background: var(--cd-bg-surface); color: var(--cd-text-secondary); }
    ul { list-style: none; padding: 0; margin: 0; display: grid; gap: var(--cd-space-4); }
    .feedback-card { border: 1px solid var(--cd-border-default); border-radius: var(--cd-radius-panel); padding: var(--cd-space-5); background: var(--cd-bg-surface); display: grid; gap: var(--cd-space-2); }
    .card-top { display: flex; justify-content: space-between; align-items: flex-start; gap: var(--cd-space-3); }
    .card-top h2 { margin: 0; font-size: var(--cd-font-size-lg); }
    .rating-pill { padding: 0.2rem 0.65rem; border-radius: var(--cd-radius-pill); background: var(--cd-status-success-bg); color: var(--cd-status-success-fg); font-size: var(--cd-font-size-xs); font-weight: var(--cd-weight-bold); }
    .meta-line { margin: 0; color: var(--cd-text-primary); }
    .meta-sub { margin: 0; font-size: var(--cd-font-size-sm); color: var(--cd-text-secondary); }
    .comment-body { margin: var(--cd-space-2) 0 0; padding: var(--cd-space-3); border-radius: var(--cd-radius-control); background: var(--cd-bg-canvas); border: 1px solid var(--cd-border-default); }
    .pagination-nav { display: flex; align-items: center; justify-content: space-between; gap: var(--cd-space-4); padding-block-start: var(--cd-space-3); border-block-start: 1px solid var(--cd-border-default); }
  `],
})
export class FeedbackAdminComponent implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly items = signal<Feedback[]>([]);
  protected readonly loading = signal(true);
  protected readonly page = signal(1);
  protected readonly hasMore = signal(false);
  protected readonly error = signal('');
  ngOnInit(): void { this.load(); }
  protected load(page = this.page()): void {
    this.loading.set(true);
    this.http.get<FeedbackPage>(`/api/admin/feedback?page=${page}`).subscribe({
      next: data => { this.items.set(data.items); this.page.set(data.page); this.hasMore.set(data.hasMore); this.loading.set(false); },
      error: err => { this.error.set(toApiProblem(err, 'Feedback could not be loaded.').title); this.loading.set(false); },
    });
  }
  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat('en', { timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) + ' Cairo';
  }
}
