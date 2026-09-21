import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiProblem, toApiProblem } from '../../../../core/http/api.interceptor';
import { DraftEventsService, DraftSummary } from '../../data/draft-events.service';

@Component({
  selector: 'app-draft-list',
  imports: [RouterLink],
  templateUrl: './draft-list.component.html',
  styleUrl: './draft-list.component.scss',
})
export class DraftListComponent implements OnInit {
  private readonly draftsService = inject(DraftEventsService);
  readonly drafts = signal<DraftSummary[]>([]);
  readonly loading = signal(true);
  readonly problem = signal<ApiProblem | null>(null);

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.problem.set(null);
    this.draftsService.listDrafts().subscribe({
      next: response => { this.drafts.set(response.items); this.loading.set(false); },
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
