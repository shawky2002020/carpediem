import { effect, inject, Injectable } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { SessionService } from '../../../core/auth/session.service';

interface CandidateHistoryReturn {
  eventId: string;
  historyPath: string;
  search: string;
  controlKey: string;
  controlTop: number;
  rosterExpanded: boolean;
}

@Injectable({ providedIn: 'root' })
export class CandidateHistoryReturnService {
  private readonly session = inject(SessionService);
  private pending?: CandidateHistoryReturn & { viewerId: string };

  constructor() {
    effect(() => {
      if (this.session.session()?.userId !== this.pending?.viewerId) this.pending = undefined;
    });
    inject(Router).events.pipe(
      filter(event => event instanceof NavigationEnd),
      takeUntilDestroyed(),
    ).subscribe(event => {
      if (event.urlAfterRedirects !== this.pending?.historyPath) this.pending = undefined;
    });
  }

  remember(review: CandidateHistoryReturn): void {
    const viewerId = this.session.session()?.userId;
    if (viewerId) this.pending = { ...review, viewerId };
  }

  take(eventId: string, previousPath?: string): CandidateHistoryReturn | undefined {
    const review = this.pending;
    this.pending = undefined;
    if (review && review.viewerId === this.session.session()?.userId &&
      review.eventId === eventId && review.historyPath === previousPath) return review;
    return undefined;
  }
}
