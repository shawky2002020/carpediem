import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { toApiProblem } from '../../core/http/api.interceptor';

interface Incident { id: string; eventTitle: string; reporterUserName: string; title: string; description: string; occurredAt: string; reportedAt: string; reviewedAt: string | null; reviewNote: string | null; }
interface IncidentPage { items: Incident[]; page: number; pageSize: number; hasMore: boolean; }

@Component({
  selector: 'app-incident-admin',
  template: `<section><h1>Incident review</h1>
    @if (error()) { <p role="alert">{{ error() }}</p> }
    @if (success()) { <p role="status">{{ success() }}</p> }
    @if (loading()) { <p role="status">Loading incidents…</p> }
    @if (!loading() && !items().length) { <p>No incidents reported yet.</p> }
    <ul>@for (item of items(); track item.id) {
      <li><h2>{{ item.title }}</h2><p>{{ item.eventTitle }} · Reported by {{ item.reporterUserName }}</p>
        <p>{{ item.description }}</p><p>Occurred: <time [attr.datetime]="item.occurredAt">{{ formatDate(item.occurredAt) }}</time></p>
        @if (item.reviewedAt) { <p>Reviewed. @if (item.reviewNote) { {{ item.reviewNote }} }</p> }
        @else { <form (submit)="review($event, item)">
          <label [for]="'note-' + item.id">Review note (optional)</label><textarea [id]="'note-' + item.id" name="note" maxlength="1000"></textarea>
          <button type="submit" [disabled]="busy() === item.id">Mark reviewed</button>
        </form> }
      </li>
    }</ul>
    <nav aria-label="Incident pages"><button type="button" [disabled]="page() === 1 || loading()" (click)="load(page() - 1)">Previous</button>
      <span>Page {{ page() }}</span><button type="button" [disabled]="!hasMore() || loading()" (click)="load(page() + 1)">Next</button></nav>
  </section>`,
  styles: ['ul { list-style:none; padding:0; display:grid; gap:var(--cd-space-4); } li { border:1px solid var(--cd-border-default); padding:var(--cd-space-4); } textarea { display:block; inline-size:100%; min-block-size:5rem; } button { min-block-size:var(--cd-touch-target-min); }'],
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
