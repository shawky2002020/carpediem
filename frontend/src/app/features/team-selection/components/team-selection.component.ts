import { Component, computed, inject, Input, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { finalize } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../../core/http/api.interceptor';
import {
  ActiveTeamMember,
  AvailableCandidate,
  DirectAssignmentCandidate,
  TeamSelectionContext,
} from '../data/team-selection.model';
import { TeamSelectionService } from '../data/team-selection.service';
import { OperationalSpineComponent } from '../../../shared/components/operational-spine/operational-spine.component';

@Component({
  selector: 'app-team-selection',
  imports: [CommonModule, RouterLink, OperationalSpineComponent],
  templateUrl: './team-selection.component.html',
  styleUrl: './team-selection.component.scss',
})
export class TeamSelectionComponent implements OnInit {
  private readonly service = inject(TeamSelectionService);
  private readonly route = inject(ActivatedRoute);

  @Input() eventId?: string;

  readonly effectiveEventId = computed(() =>
    this.eventId ?? this.route.snapshot.paramMap.get('eventId') ?? ''
  );

  readonly context = signal<TeamSelectionContext | null>(null);
  readonly directCandidates = signal<DirectAssignmentCandidate[]>([]);
  readonly loading = signal(true);
  readonly submittingId = signal<string | null>(null);
  readonly statusMessage = signal<string | null>(null);
  readonly problem = signal<ApiProblem | null>(null);
  readonly candidateSearch = signal('');

  readonly isWindowOpen = computed(() => this.context()?.isWindowOpen ?? false);
  readonly availableCandidates = computed(() => this.context()?.availableCandidates ?? []);
  readonly activeTeam = computed(() => this.context()?.activeTeam ?? []);
  readonly activeTeamCount = computed(() => this.activeTeam().length);
  readonly availableCandidatesCount = computed(() => this.availableCandidates().length);
  readonly directCandidatesCount = computed(() => this.directCandidates().length);

  readonly filteredAvailableCandidates = computed(() => {
    const query = this.candidateSearch().toLowerCase().trim();
    const list = this.availableCandidates();
    if (!query) return list;
    return list.filter(c => c.userName.toLowerCase().includes(query));
  });

  ngOnInit(): void {
    this.load();
  }

  load(preserveProblem = false): void {
    const id = this.effectiveEventId();
    if (!id) return;

    this.loading.set(true);
    if (!preserveProblem) this.problem.set(null);

    this.service.getTeamSelectionContext(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: data => {
          this.context.set(data);
        },
        error: error => {
          this.problem.set(toApiProblem(error, 'Could not load team selection context.'));
        },
      });

    this.service.getDirectAssignmentCandidates(id)
      .subscribe({
        next: data => {
          this.directCandidates.set(data.candidates);
        },
        error: error => {
          if (!this.problem()) {
            this.problem.set(toApiProblem(error, 'Could not load direct assignment candidates.'));
          }
        },
      });
  }

  selectVolunteer(candidate: AvailableCandidate): void {
    const id = this.effectiveEventId();
    if (!id || !this.isWindowOpen()) return;

    this.submittingId.set(candidate.userId);
    this.problem.set(null);
    this.statusMessage.set(null);

    this.service.selectVolunteer(id, candidate.userId)
      .pipe(finalize(() => this.submittingId.set(null)))
      .subscribe({
        next: response => {
          this.applySelection(response, candidate.userName);
        },
        error: error => {
          const apiProblem = toApiProblem(error, 'Could not assign volunteer.');
          this.problem.set(apiProblem);
          if (apiProblem.status === 409) {
            this.load(true);
          }
        },
      });
  }

  directAssignLeader(candidate: DirectAssignmentCandidate): void {
    const id = this.effectiveEventId();
    if (!id || !this.isWindowOpen()) return;

    this.submittingId.set(candidate.userId);
    this.problem.set(null);
    this.statusMessage.set(null);

    this.service.directAssignLeader(id, candidate.userId)
      .pipe(finalize(() => this.submittingId.set(null)))
      .subscribe({
        next: response => {
          this.applyDirectAssignment(response, candidate.userName);
        },
        error: error => {
          const apiProblem = toApiProblem(error, 'Could not directly assign leader.');
          this.problem.set(apiProblem);
          if (apiProblem.status === 409) {
            this.load(true);
          }
        },
      });
  }

  formatDate(value: string | null | undefined): string {
    if (!value) return '';
    try {
      return new Intl.DateTimeFormat('en-GB', {
        dateStyle: 'medium',
        timeStyle: 'short',
        timeZone: 'Africa/Cairo',
      }).format(new Date(value)) + ' Cairo';
    } catch {
      return value;
    }
  }

  private applySelection(response: { id: string; userId: string; assignedAt: string; version: string }, userName: string): void {
    const current = this.context();
    if (!current) return;

    const newMember: ActiveTeamMember = {
      assignmentId: response.id,
      userId: response.userId,
      userName,
      assignedAt: response.assignedAt,
      version: response.version,
    };

    const updatedCandidates = current.availableCandidates.filter(c => c.userId !== response.userId);
    const updatedTeam = [...current.activeTeam, newMember];

    this.context.set({
      ...current,
      availableCandidates: updatedCandidates,
      activeTeam: updatedTeam,
    });

    this.statusMessage.set(`Volunteer ${userName} added to active team roster.`);
  }

  private applyDirectAssignment(response: { id: string; userId: string; assignedAt: string; version: string }, userName: string): void {
    const current = this.context();
    if (!current) return;

    const newMember: ActiveTeamMember = {
      assignmentId: response.id,
      userId: response.userId,
      userName,
      assignedAt: response.assignedAt,
      version: response.version,
    };

    const updatedDirect = this.directCandidates().filter(c => c.userId !== response.userId);
    const updatedTeam = [...current.activeTeam, newMember];

    this.directCandidates.set(updatedDirect);
    this.context.set({
      ...current,
      activeTeam: updatedTeam,
    });

    this.statusMessage.set(`Leader ${userName} directly assigned to active team roster.`);
  }
}
