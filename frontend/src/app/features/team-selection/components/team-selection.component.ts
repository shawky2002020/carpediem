import { Component, computed, inject, Input, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../../core/http/api.interceptor';
import {
  ActiveTeamMember,
  AvailableCandidate,
  DirectAssignmentCandidate,
  TeamSelectionContext,
} from '../data/team-selection.model';
import { TeamSelectionService } from '../data/team-selection.service';

@Component({
  selector: 'app-team-selection',
  imports: [RouterLink],
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

  readonly isWindowOpen = computed(() => this.context()?.isWindowOpen ?? false);
  readonly availableCandidates = computed(() => this.context()?.availableCandidates ?? []);
  readonly activeTeam = computed(() => this.context()?.activeTeam ?? []);
  readonly activeTeamCount = computed(() => this.activeTeam().length);
  readonly availableCandidatesCount = computed(() => this.availableCandidates().length);
  readonly directCandidatesCount = computed(() => this.directCandidates().length);

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
          this.problem.set(toApiProblem(error, 'Team selection context could not be loaded.'));
        },
      });

    this.service.getDirectAssignmentCandidates(id)
      .subscribe({
        next: data => {
          this.directCandidates.set(data.candidates);
        },
        error: () => {
          // Gracefully maintain direct candidate state
        },
      });
  }

  selectVolunteer(candidate: AvailableCandidate): void {
    const id = this.effectiveEventId();
    const current = this.context();
    if (!id || !current || !this.isWindowOpen() || this.submittingId() !== null) return;

    this.submittingId.set(candidate.userId);
    this.problem.set(null);
    this.statusMessage.set(null);

    this.service.selectVolunteer(id, candidate.userId)
      .pipe(finalize(() => this.submittingId.set(null)))
      .subscribe({
        next: response => {
          const updatedAvailable = current.availableCandidates.filter(
            c => c.userId !== candidate.userId,
          );
          const newTeamMember: ActiveTeamMember = {
            assignmentId: response.id,
            userId: response.userId,
            userName: candidate.userName,
            assignedAt: response.assignedAt,
            version: response.version,
          };
          const updatedActive = [...current.activeTeam, newTeamMember];

          this.context.set({
            ...current,
            availableCandidates: updatedAvailable,
            activeTeam: updatedActive,
          });

          this.statusMessage.set(`Volunteer ${candidate.userName} added to active team roster.`);
        },
        error: error => {
          const apiProblem = toApiProblem(error, 'Volunteer could not be selected.');
          this.problem.set(apiProblem);
          if (error.status === 409) {
            this.load(true);
          }
        },
      });
  }

  directAssignLeader(candidate: DirectAssignmentCandidate): void {
    const id = this.effectiveEventId();
    const current = this.context();
    if (!id || !current || !this.isWindowOpen() || this.submittingId() !== null) return;

    this.submittingId.set(candidate.userId);
    this.problem.set(null);
    this.statusMessage.set(null);

    this.service.directAssignLeader(id, candidate.userId)
      .pipe(finalize(() => this.submittingId.set(null)))
      .subscribe({
        next: response => {
          this.directCandidates.update(list =>
            list.filter(c => c.userId !== candidate.userId),
          );
          const newTeamMember: ActiveTeamMember = {
            assignmentId: response.id,
            userId: response.userId,
            userName: candidate.userName,
            assignedAt: response.assignedAt,
            version: response.version,
          };
          const updatedActive = [...current.activeTeam, newTeamMember];

          this.context.set({
            ...current,
            activeTeam: updatedActive,
          });

          this.statusMessage.set(`Leader ${candidate.userName} directly assigned to active team roster.`);
        },
        error: error => {
          const apiProblem = toApiProblem(error, 'Leader could not be directly assigned.');
          this.problem.set(apiProblem);
          if (error.status === 409) {
            this.load(true);
          }
        },
      });
  }

  formatDate(value: string | null | undefined): string {
    if (!value) return '';
    return new Intl.DateTimeFormat('en', {
      timeZone: 'Africa/Cairo',
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value)) + ' Cairo';
  }
}
