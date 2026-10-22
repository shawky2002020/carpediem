import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { toApiProblem } from '../../core/http/api.interceptor';

interface Feedback { id: string; eventTitle: string; kind: string; raterUserName: string; leaderUserName: string | null; rating: number; comment: string | null; submittedAt: string; }
interface FeedbackPage { items: Feedback[]; page: number; pageSize: number; hasMore: boolean; }

@Component({
  selector: 'app-feedback-admin',
  template: `<section><h1>Feedback</h1><p>Post-event ratings are visible to Admins.</p>
    @if (error()) { <p role="alert">{{ error() }}</p> }
    @if (loading()) { <p role="status">Loading ratings…</p> }
    @if (!loading() && !items().length) { <p>No ratings submitted yet.</p> }
    <ul>@for (item of items(); track item.id) {
      <li><h2>{{ item.eventTitle }}</h2><p>{{ item.kind === 'Event' ? 'Event' : item.leaderUserName }} · {{ item.rating }} of 5</p>
        <p>Rated by {{ item.raterUserName }} on <time [attr.datetime]="item.submittedAt">{{ formatDate(item.submittedAt) }}</time></p>
        @if (item.comment) { <p>{{ item.comment }}</p> }
      </li>
    }</ul>
    <nav aria-label="Feedback pages"><button type="button" [disabled]="page() === 1 || loading()" (click)="load(page() - 1)">Previous</button>
      <span>Page {{ page() }}</span><button type="button" [disabled]="!hasMore() || loading()" (click)="load(page() + 1)">Next</button></nav>
  </section>`,
  styles: ['ul { list-style:none; padding:0; display:grid; gap:var(--cd-space-4); } li { border:1px solid var(--cd-border-default); padding:var(--cd-space-4); }'],
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
