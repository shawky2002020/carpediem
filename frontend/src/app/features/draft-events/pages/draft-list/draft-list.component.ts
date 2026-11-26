import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiProblem, toApiProblem } from '../../../../core/http/api.interceptor';
import { DraftEventsService, DraftSummary } from '../../data/draft-events.service';

@Component({
  selector: 'app-draft-list',
  imports: [DatePipe, RouterLink],
  templateUrl: './draft-list.component.html',
  styleUrl: './draft-list.component.scss',
})
export class DraftListComponent implements OnInit {
  private readonly draftsService = inject(DraftEventsService);
  readonly drafts = signal<DraftSummary[]>([]);
  readonly loading = signal(true);
  readonly problem = signal<ApiProblem | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(20);

  ngOnInit(): void { this.load(); }

  load(page = this.page()): void {
    this.loading.set(true);
    this.problem.set(null);
    this.page.set(page);
    this.draftsService.listDrafts(page, this.pageSize()).subscribe({
      next: response => {
        this.drafts.set(response.items);
        this.page.set(response.page);
        this.pageSize.set(response.pageSize);
        this.loading.set(false);
      },
      error: error => {
        this.problem.set(toApiProblem(error, 'Draft Events could not be loaded.'));
        this.loading.set(false);
      },
    });
  }

  displayDate(value: string): string {
    const [date, time] = value.split('T');
    return `${date} at ${time.slice(0, 5)} Cairo`;
  }
}
