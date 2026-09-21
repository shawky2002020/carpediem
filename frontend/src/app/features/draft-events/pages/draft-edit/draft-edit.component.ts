import { Component, HostListener, inject, OnInit, signal, ViewChild } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../../../core/http/api.interceptor';
import { DraftEventFormComponent } from '../../components/draft-event-form.component';
import { DraftEvent, DraftEventsService, DraftWrite } from '../../data/draft-events.service';

@Component({
  selector: 'app-draft-edit',
  imports: [DraftEventFormComponent, RouterLink],
  templateUrl: './draft-edit.component.html',
  styleUrl: './draft-edit.component.scss',
})
export class DraftEditComponent implements OnInit {
  private readonly drafts = inject(DraftEventsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly id = this.route.snapshot.paramMap.get('id')!;

  @ViewChild(DraftEventFormComponent) form?: DraftEventFormComponent;
  readonly draft = signal<DraftEvent | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly savedNotice = signal<string | null>(globalThis.history.state?.notice ?? null);
  readonly problem = signal<ApiProblem | null>(null);

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.problem.set(null);
    this.drafts.getDraft(this.id).subscribe({
      next: draft => { this.draft.set(draft); this.loading.set(false); },
      error: error => {
        this.problem.set(toApiProblem(error, 'The Draft Event could not be opened.'));
        this.loading.set(false);
      },
    });
  }

  save(write: DraftWrite): void {
    const current = this.draft();
    if (!current || this.saving()) return;
    this.saving.set(true);
    this.problem.set(null);
    this.savedNotice.set(null);
    this.drafts.updateDraft(current.id, { ...write, version: current.version })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: updated => {
          this.draft.set(updated);
          this.form?.markSaved();
          this.savedNotice.set('Draft Event saved.');
        },
        error: error => this.problem.set(toApiProblem(error,
          'The Draft could not be saved. Your entries are still here.')),
      });
  }

  cancel(): void {
    if (this.canDeactivate()) void this.router.navigate(['/draft-events']);
  }

  canDeactivate(): boolean {
    return !this.form?.dirty || window.confirm('Discard your unsaved Draft Event changes?');
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (!this.form?.dirty) return;
    event.preventDefault();
    event.returnValue = '';
  }
}
