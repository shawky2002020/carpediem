import { Component, HostListener, inject, signal, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../../../core/http/api.interceptor';
import { DraftEventFormComponent } from '../../components/draft-event-form.component';
import { DraftEventsService, DraftWrite } from '../../data/draft-events.service';

@Component({
  selector: 'app-draft-create',
  imports: [DraftEventFormComponent],
  templateUrl: './draft-create.component.html',
  styleUrl: './draft-create.component.scss',
})
export class DraftCreateComponent {
  private readonly drafts = inject(DraftEventsService);
  private readonly router = inject(Router);
  private readonly intentKey = crypto.randomUUID();
  private pending: DraftWrite | null = null;
  private completed = false;

  @ViewChild(DraftEventFormComponent) form?: DraftEventFormComponent;
  readonly saving = signal(false);
  readonly uncertain = signal(false);
  readonly problem = signal<ApiProblem | null>(null);

  save(write: DraftWrite): void {
    if (this.saving() || this.uncertain()) return;
    this.pending = structuredClone(write);
    this.submitPending();
  }

  retry(): void {
    if (!this.pending || this.saving()) return;
    this.submitPending();
  }

  cancel(): void {
    if (this.canDeactivate()) void this.router.navigate(['/draft-events']);
  }

  canDeactivate(): boolean {
    return this.completed || !this.form?.dirty || window.confirm('Discard this unfinished Draft Event?');
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.completed || !this.form?.dirty) return;
    event.preventDefault();
    event.returnValue = '';
  }

  private submitPending(): void {
    if (!this.pending) return;
    this.saving.set(true);
    this.problem.set(null);
    this.drafts.createDraft(this.pending, this.intentKey)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: draft => {
          this.completed = true;
          this.form?.markSaved();
          void this.router.navigate(['/draft-events', draft.id], { state: { notice: 'Draft Event created.' } });
        },
        error: error => {
          if (error.status === 0 || error.status >= 500) {
            this.uncertain.set(true);
            this.problem.set({ type: '', title: 'The save outcome is uncertain. Retry the same request to confirm it.',
              status: error.status, code: 'unexpected_error', traceId: error.error?.traceId ?? '' });
            return;
          }
          if (error.status === 409) this.uncertain.set(true);
          this.problem.set(toApiProblem(error, 'The Draft could not be saved. Your entries are still here.'));
          if (error.status !== 409) this.pending = null;
        },
      });
  }
}
