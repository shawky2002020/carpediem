import { NgTemplateOutlet } from '@angular/common';
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
  imports: [NgTemplateOutlet],
  template: `<section class="feedback-page cd-reading">
    <header class="page-header">
      <h1>Event feedback</h1>
      @if (details(); as info) { <p class="feedback-context">{{ info.eventTitle }}</p> }
    </header>
    @if (error()) {
      <div role="alert" class="cd-alert cd-alert--error alert-error">
        <p>{{ error() }}</p>
        @if (!details()) { <button type="button" (click)="load()" [disabled]="loading()">Try again</button> }
      </div>
    }
    @if (success()) {
      <div role="status" class="cd-alert cd-alert--success alert-success">
        <p>{{ success() }}</p>
      </div>
    }
    @if (loading()) {
      <div role="status" class="cd-loading-state">
        <p>Loading feedback…</p>
      </div>
    }
    <ng-template #ratingScale let-subject>
      <fieldset class="rating-scale">
        <legend>Rating <span class="cd-visually-hidden">for {{ subject }}</span></legend>
        <div class="rating-options">
          @for (value of ratings; track value) {
            <label><input type="radio" name="rating" [value]="value" required />
              <span>{{ value }}<span class="cd-visually-hidden"> of 5</span></span>
            </label>
          }
        </div>
      </fieldset>
    </ng-template>
    @if (details(); as info) {
      @if (!info.canRateEvent && !info.canRateLeaders) {
        <div class="cd-callout cd-callout--info info-note">
          <p>There are no ratings available to submit for this event right now.</p>
        </div>
      }
      @if (info.canRateEvent && !rated(null, info)) {
        <form class="feedback-form" (submit)="submit($event, null)">
          <h2>Rate this Event</h2>
          <ng-container [ngTemplateOutlet]="ratingScale" [ngTemplateOutletContext]="{ $implicit: info.eventTitle }" />
          <div class="field">
            <label for="event-comment">Comment (optional)</label>
            <textarea id="event-comment" name="comment" maxlength="500" ></textarea>
          </div>
          <button type="submit" class="cd-btn cd-btn--primary" [disabled]="saving()">
            {{ saving() ? 'Submitting…' : 'Submit rating' }}
          </button>
        </form>
      }
      @if (info.canRateLeaders) {
        @for (leader of info.leaders; track leader.userId) {
          @if (!rated(leader.userId, info)) {
            <form class="feedback-form" (submit)="submit($event, leader.userId)">
              <h2>Rate {{ leader.userName }}</h2>
              <ng-container [ngTemplateOutlet]="ratingScale" [ngTemplateOutletContext]="{ $implicit: leader.userName }" />
              <div class="field">
                <label [for]="'comment-' + leader.userId">Comment (optional)</label>
                <textarea [id]="'comment-' + leader.userId" name="comment" maxlength="500" ></textarea>
              </div>
              <button type="submit" class="cd-btn cd-btn--primary" [disabled]="saving()">
                {{ saving() ? 'Submitting…' : 'Submit rating' }}
              </button>
            </form>
          }
        }
      }
      @if (info.ownFeedback.length) {
        <div class="submitted-section">
          <h2>Your submitted ratings</h2>
          <ul class="submitted-list">@for (item of info.ownFeedback; track item.id) {
            <li class="submitted-item">
              <div class="submitted-header">
                <strong>{{ item.kind }}:</strong>
                <span class="cd-badge cd-badge--success">★ {{ item.rating }} of 5</span>
              </div>
              @if (item.comment) { <p class="submitted-comment">{{ item.comment }}</p> }
            </li>
          }</ul>
        </div>
      }
    }
  </section>`,
  styleUrl: './event-feedback.component.scss',
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
    this.error.set('');
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
