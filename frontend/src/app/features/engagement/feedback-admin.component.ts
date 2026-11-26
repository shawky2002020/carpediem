import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { toApiProblem } from '../../core/http/api.interceptor';

interface Feedback { id: string; eventTitle: string; kind: string; raterUserName: string; leaderUserName: string | null; rating: number; comment: string | null; submittedAt: string; }
interface FeedbackPage { items: Feedback[]; page: number; pageSize: number; hasMore: boolean; }

@Component({
  selector: 'app-feedback-admin',
  template: `<section class="feedback-admin-page cd-reading">
    <header class="page-header">
      <h1>Feedback</h1>
      <p class="subtitle">Review event and Leader ratings.</p>
    </header>
    @if (error()) {
      <div role="alert" class="cd-alert cd-alert--error alert-error">
        <p>{{ error() }}</p>
        <button type="button" (click)="load()" [disabled]="loading()">Try again</button>
      </div>
    }
    @if (loading()) {
      <div role="status" class="cd-loading-state">
        <p>Loading ratings…</p>
      </div>
    }
    @if (!loading() && !error() && !items().length) {
      <div class="cd-empty-state empty-state">
        <p>No ratings submitted yet.</p>
      </div>
    }
    <ul class="feedback-grid">@for (item of items(); track item.id) {
      <li class="feedback-card">
        <div class="card-top">
          <div>
            <h2>{{ item.eventTitle }}</h2>
            <p class="meta-line">
              <span class="cd-badge cd-badge--neutral">{{ item.kind === 'Event' ? 'Event Evaluation' : 'Leader: ' + item.leaderUserName }}</span>
            </p>
          </div>
          <span class="rating-pill">
            <strong>{{ item.rating }}</strong> of 5
          </span>
        </div>
        <p class="meta-sub">Rated by <strong>{{ item.raterUserName }}</strong> on <time class="cd-tabular" [attr.datetime]="item.submittedAt">{{ formatDate(item.submittedAt) }}</time></p>
        @if (item.comment) {
          <div class="comment-callout">
            <p class="comment-body">{{ item.comment }}</p>
          </div>
        }
      </li>
    }</ul>
    @if (page() > 1 || hasMore()) { <nav aria-label="Feedback pages" class="pagination-nav">
      <button type="button" class="cd-btn cd-btn--sm cd-btn--secondary" [disabled]="page() === 1 || loading()" (click)="load(page() - 1)">Previous</button>
      <span class="page-indicator cd-tabular">Page {{ page() }}</span>
      <button type="button" class="cd-btn cd-btn--sm cd-btn--secondary" [disabled]="!hasMore() || loading()" (click)="load(page() + 1)">Next</button>
    </nav> }
  </section>`,
  styleUrl: './feedback-admin.component.scss',
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
    this.error.set('');
    this.http.get<FeedbackPage>(`/api/admin/feedback?page=${page}`).subscribe({
      next: data => { this.items.set(data.items); this.page.set(data.page); this.hasMore.set(data.hasMore); this.loading.set(false); },
      error: err => { this.error.set(toApiProblem(err, 'Feedback could not be loaded.').title); this.loading.set(false); },
    });
  }
  protected formatDate(value: string): string {
    return new Intl.DateTimeFormat('en', { timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) + ' Cairo';
  }
}
