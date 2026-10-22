import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs';
import { toApiProblem } from '../../core/http/api.interceptor';

interface LeaderOption { userId: string; userName: string; }
interface Rating { id: string; kind: string; leaderUserId: string | null; rating: number; comment: string | null; }
interface FeedbackContext { eventId: string; eventTitle: string; canRateEvent: boolean; canRateLeaders: boolean; leaders: LeaderOption[]; ownFeedback: Rating[]; }

@Component({
  selector: 'app-event-feedback',
  template: `<section class="cd-reading"><h1>Event feedback</h1>
    @if (error()) { <p role="alert">{{ error() }}</p> }
    @if (success()) { <p role="status">{{ success() }}</p> }
    @if (loading()) { <p role="status">Loading feedback…</p> }
    @if (details(); as info) {
      <h2>{{ info.eventTitle }}</h2>
      @if (!info.canRateEvent && !info.canRateLeaders) { <p>Ratings open after this Event is completed.</p> }
      @if (info.canRateEvent && !rated(null, info)) {
        <form (submit)="submit($event, null)"><h3>Rate this Event</h3>
          <label for="event-rating">Rating</label><select id="event-rating" name="rating" required>
            @for (value of ratings; track value) { <option [value]="value">{{ value }} of 5</option> }
          </select>
          <label for="event-comment">Comment (optional)</label><textarea id="event-comment" name="comment" maxlength="500"></textarea>
          <button type="submit" [disabled]="saving()">Submit rating</button>
        </form>
      }
      @if (info.canRateLeaders) {
        @for (leader of info.leaders; track leader.userId) {
          @if (!rated(leader.userId, info)) {
            <form (submit)="submit($event, leader.userId)"><h3>Rate {{ leader.userName }}</h3>
              <label [for]="'rating-' + leader.userId">Rating</label><select [id]="'rating-' + leader.userId" name="rating" required>
                @for (value of ratings; track value) { <option [value]="value">{{ value }} of 5</option> }
              </select>
              <label [for]="'comment-' + leader.userId">Comment (optional)</label><textarea [id]="'comment-' + leader.userId" name="comment" maxlength="500"></textarea>
              <button type="submit" [disabled]="saving()">Submit rating</button>
            </form>
          }
        }
      }
      @if (info.ownFeedback.length) { <h3>Your submitted ratings</h3><ul>@for (item of info.ownFeedback; track item.id) {
        <li>{{ item.kind }}: {{ item.rating }} of 5 @if (item.comment) { — {{ item.comment }} }</li>
      }</ul> }
    }
  </section>`,
  styles: ['form { display:grid; gap:var(--cd-space-2); border:1px solid var(--cd-border-default); padding:var(--cd-space-4); margin-block:var(--cd-space-4); } select,textarea { inline-size:100%; } textarea { min-block-size:5rem; } button { min-block-size:var(--cd-touch-target-min); }'],
})
export class EventFeedbackComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  protected readonly ratings = [1, 2, 3, 4, 5];
  protected readonly details = signal<FeedbackContext | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal('');
  protected readonly success = signal('');
  private get eventId(): string { return this.route.snapshot.paramMap.get('eventId') ?? ''; }
  ngOnInit(): void { this.load(); }
  protected rated(leaderId: string | null, info: FeedbackContext): boolean {
    return info.ownFeedback.some(item => item.leaderUserId === leaderId);
  }
  protected load(): void {
    this.loading.set(true);
    this.http.get<FeedbackContext>(`/api/events/${this.eventId}/feedback/context`)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({ next: data => this.details.set(data), error: err => this.error.set(toApiProblem(err, 'Feedback could not be loaded.').title) });
  }
  protected submit(event: Event, leaderId: string | null): void {
    event.preventDefault();
    if (this.saving()) return;
    const form = event.target as HTMLFormElement;
    const data = new FormData(form);
    this.error.set(''); this.success.set(''); this.saving.set(true);
    const path = leaderId ? `leaders/${leaderId}` : 'event';
    this.http.post(`/api/events/${this.eventId}/feedback/${path}`, {
      rating: Number(data.get('rating')), comment: String(data.get('comment') ?? ''),
    }).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.success.set('Rating submitted.'); this.load(); },
      error: err => this.error.set(toApiProblem(err, 'Rating could not be submitted.').title),
    });
  }
}
