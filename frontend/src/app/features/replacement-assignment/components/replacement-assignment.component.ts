import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../../core/http/api.interceptor';
import {
  ReplacementAssignmentContextResponse,
  ReplacementCandidateItem,
} from '../data/replacement-assignment.model';
import { ReplacementAssignmentService } from '../data/replacement-assignment.service';

@Component({
  selector: 'app-replacement-assignment',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './replacement-assignment.component.html',
  styleUrls: ['./replacement-assignment.component.scss'],
})
export class ReplacementAssignmentComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(ReplacementAssignmentService);

  readonly context = signal<ReplacementAssignmentContextResponse | null>(null);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly selectedReplacedAssignmentId = signal<string | null>(null);
  readonly justification = signal('');
  readonly statusMessage = signal<string | null>(null);
  readonly problem = signal<ApiProblem | null>(null);

  readonly activeCount = computed(() => this.context()?.activeTeam.length ?? 0);
  readonly cancelledCount = computed(() => this.context()?.cancelledAssignments.length ?? 0);
  readonly candidates = computed(() => this.context()?.candidates ?? []);
  readonly isWindowOpen = computed(() => this.context()?.isWindowOpen ?? false);

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
          this.context.set(data);
          if (data.cancelledAssignments.length > 0 && !this.selectedReplacedAssignmentId()) {
            this.selectedReplacedAssignmentId.set(data.cancelledAssignments[0].assignmentId);
          }
        },
        error: error => {
          this.problem.set(toApiProblem(error, 'Unable to load replacement staffing context.'));
        },
      });
  }

  selectSlot(assignmentId: string): void {
    this.selectedReplacedAssignmentId.set(assignmentId);
  }

  onJustificationChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.justification.set(input.value);
  }

  assignReplacement(candidate: ReplacementCandidateItem): void {
    const curr = this.context();
    if (!curr) return;

    this.submitting.set(true);
    this.problem.set(null);
    this.statusMessage.set(null);

    const payload = {
      candidateUserId: candidate.userId,
      replacedAssignmentId: this.selectedReplacedAssignmentId(),
      justification: this.justification().trim() || null,
    };

    this.service.assignReplacementLeader(curr.eventId, payload)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: response => {
          this.statusMessage.set(`Volunteer ${candidate.userName} assigned as replacement.`);

          const newActiveTeam = [
            ...curr.activeTeam,
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

          this.context.set({
            ...curr,
            activeTeam: newActiveTeam,
            candidates: remainingCandidates,
          });
        },
        error: error => {
          this.problem.set(toApiProblem(error, 'Replacement assignment could not be completed.'));
        },
      });
  }

  formatDate(isoDate: string | null | undefined): string {
    if (!isoDate) return '—';
    try {
      return new Intl.DateTimeFormat('en-US', {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(isoDate));
    } catch {
      return isoDate;
    }
  }
}
