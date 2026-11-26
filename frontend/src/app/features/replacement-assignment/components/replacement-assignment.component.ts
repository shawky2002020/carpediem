import { afterNextRender, Component, computed, inject, Injector, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../../core/http/api.interceptor';
import {
  ReplacementAssignmentContextResponse,
  ReplacementCandidateItem,
} from '../data/replacement-assignment.model';
import { ReplacementAssignmentService } from '../data/replacement-assignment.service';
import { OperationalSpineComponent } from '../../../shared/components/operational-spine/operational-spine.component';

const cairoDateTime = new Intl.DateTimeFormat('en-GB', {
  dateStyle: 'medium', timeStyle: 'short', timeZone: 'Africa/Cairo',
});

@Component({
  selector: 'app-replacement-assignment',
  standalone: true,
  imports: [RouterLink, OperationalSpineComponent],
  templateUrl: './replacement-assignment.component.html',
  styleUrls: ['./replacement-assignment.component.scss'],
})
export class ReplacementAssignmentComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(ReplacementAssignmentService);
  private readonly injector = inject(Injector);

  readonly context = signal<ReplacementAssignmentContextResponse | null>(null);
  readonly loading = signal(true);
  readonly submittingId = signal<string | null>(null);
  readonly submitting = computed(() => this.submittingId() !== null);
  readonly selectedReplacedAssignmentId = signal<string | null>(null);
  readonly linkedCancellation = computed(() =>
    this.context()?.cancelledAssignments.find(item => item.assignmentId === this.selectedReplacedAssignmentId())
  );
  readonly justification = signal('');
  readonly statusMessage = signal<string | null>(null);
  readonly problem = signal<ApiProblem | null>(null);
  readonly candidateSearch = signal('');

  readonly activeCount = computed(() => this.context()?.activeTeam.length ?? 0);
  readonly cancelledCount = computed(() => this.context()?.cancelledAssignments.length ?? 0);
  readonly candidates = computed(() => this.context()?.candidates ?? []);
  readonly filteredCandidates = computed(() => {
    const query = this.candidateSearch().trim().toLowerCase();
    return this.candidates().filter(candidate => candidate.userName.toLowerCase().includes(query));
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const eventId = this.route.snapshot.paramMap.get('eventId');
    if (!eventId) {
      this.problem.set({
        type: '',
        status: 400,
        title: 'Missing Event ID',
        detail: 'An event identifier is required to view replacement context.',
        code: 'missing_id',
        traceId: '',
      });
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.problem.set(null);

    this.service.getReplacementContext(eventId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: data => {
          const selected = this.selectedReplacedAssignmentId();
          if (!this.context() || (selected && !data.cancelledAssignments.some(item => item.assignmentId === selected))) {
            this.selectedReplacedAssignmentId.set(data.cancelledAssignments[0]?.assignmentId ?? null);
          }
          this.context.set(data);
        },
        error: error => {
          this.problem.set(toApiProblem(error, 'Unable to load replacement staffing context.'));
        },
      });
  }

  selectSlot(assignmentId: string | null): void {
    this.selectedReplacedAssignmentId.set(assignmentId);
  }

  onJustificationChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.justification.set(input.value);
  }

  assignReplacement(candidate: ReplacementCandidateItem, event?: MouseEvent): void {
    const curr = this.context();
    if (!curr?.isWindowOpen || this.loading() || this.submitting()) return;
    const section = (event?.currentTarget as HTMLElement | undefined)?.closest('section');

    this.submittingId.set(candidate.userId);
    this.problem.set(null);
    this.statusMessage.set(null);

    const payload = {
      candidateUserId: candidate.userId,
      replacedAssignmentId: this.selectedReplacedAssignmentId(),
      justification: this.justification().trim() || null,
    };

    this.service.assignReplacementLeader(curr.eventId, payload)
      .pipe(finalize(() => this.submittingId.set(null)))
      .subscribe({
        next: response => {
          this.statusMessage.set(`Volunteer ${candidate.userName} assigned as replacement.`);

          const newActiveTeam = [
            ...curr.activeTeam.filter(member => member.assignmentId !== response.id),
            {
              assignmentId: response.id,
              userId: candidate.userId,
              userName: candidate.userName,
              status: 'Active',
              assignedAt: response.assignedAt,
              version: response.version,
            },
          ];

          const remainingCandidates = curr.candidates.filter(c => c.userId !== candidate.userId);

          let remainingCancelled = curr.cancelledAssignments;
          if (payload.replacedAssignmentId) {
            remainingCancelled = curr.cancelledAssignments.filter(
              c => c.assignmentId !== payload.replacedAssignmentId
            );
          }

          this.context.set({
            ...curr,
            activeTeam: newActiveTeam,
            candidates: remainingCandidates,
            cancelledAssignments: remainingCancelled,
          });

          if (payload.replacedAssignmentId) {
            this.selectedReplacedAssignmentId.set(remainingCancelled[0]?.assignmentId ?? null);
          }
          this.justification.set('');
          this.keepCandidateFocus(section);
        },
        error: error => {
          this.problem.set(toApiProblem(error, 'Replacement assignment could not be completed.'));
        },
      });
  }

  private keepCandidateFocus(section: HTMLElement | null | undefined): void {
    if (!section) return;

    afterNextRender(() => {
      const document = section.ownerDocument;
      if (document.activeElement !== document.body) return;

      const nextAction = section.querySelector<HTMLElement>('.btn-select-replacement:enabled');
      (nextAction ?? section.querySelector<HTMLElement>('h2'))?.focus();
    }, { injector: this.injector });
  }

  formatDate(isoDate: string | null | undefined): string {
    if (!isoDate) return '—';
    try {
      return cairoDateTime.format(new Date(isoDate)) + ' Cairo';
    } catch {
      return isoDate;
    }
  }
}
