import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../core/http/api.interceptor';
import { LeaderHistory, LeaderHistoryEntry } from './data/leader-history.model';
import { LeaderHistoryService } from './data/leader-history.service';

@Component({
  selector: 'app-leader-history',
  imports: [RouterLink],
  templateUrl: './leader-history.component.html',
  styleUrl: './leader-history.component.scss',
})
export class LeaderHistoryComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(LeaderHistoryService);

  readonly history = signal<LeaderHistory | null>(null);
  readonly loading = signal(true);
  readonly problem = signal<ApiProblem | null>(null);
  readonly eventId = signal<string | null>(null);
  readonly isOwn = signal(true);

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      this.eventId.set(params.get('eventId'));
      const leaderId = params.get('leaderId');
      this.isOwn.set(!leaderId);
      this.load(leaderId);
    });
  }

  load(leaderId = this.route.snapshot.paramMap.get('leaderId')): void {
    this.loading.set(true);
    this.problem.set(null);
    this.history.set(null);
    const request = leaderId
      ? this.service.getLeader(leaderId, this.eventId() ?? undefined)
      : this.service.getOwn();
    request.pipe(finalize(() => this.loading.set(false))).subscribe({
      next: history => this.history.set(history),
      error: error => this.problem.set(toApiProblem(error, 'Leader history could not be loaded.')),
    });
  }

  effectiveDate(entry: LeaderHistoryEntry): string {
    const [year, month, day] = entry.effectiveEventDate.split('-').map(Number);
    return new Intl.DateTimeFormat('en', {
      timeZone: 'Africa/Cairo', dateStyle: 'medium',
    }).format(new Date(Date.UTC(year, month - 1, day, 12)));
  }

  dateTime(value: string): string {
    return new Intl.DateTimeFormat('en', {
      timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short',
    }).format(new Date(value)) + ' Cairo';
  }
}
