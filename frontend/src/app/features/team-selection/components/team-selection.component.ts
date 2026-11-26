import { afterNextRender, Component, computed, inject, Injector, Input, OnInit, signal } from '@angular/core';
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
import { OperationalSpineComponent } from '../../../shared/components/operational-spine/operational-spine.component';

const cairoDateTime = new Intl.DateTimeFormat('en-GB', {
  dateStyle: 'medium', timeStyle: 'short', timeZone: 'Africa/Cairo',
});

@Component({
  selector: 'app-team-selection',
  imports: [RouterLink, OperationalSpineComponent],
  templateUrl: './team-selection.component.html',
  styleUrl: './team-selection.component.scss',
})
export class TeamSelectionComponent implements OnInit {
  private readonly service = inject(TeamSelectionService);
  private readonly route = inject(ActivatedRoute);
  private readonly injector = inject(Injector);

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
  readonly directProblem = signal<ApiProblem | null>(null);
  readonly directLoading = signal(true);
  readonly candidateSearch = signal('');
  readonly rosterExpanded = signal(false);

  readonly isWindowOpen = computed(() => this.context()?.isWindowOpen ?? false);
  readonly availableCandidates = computed(() => this.context()?.availableCandidates ?? []);
  readonly activeTeam = computed(() => this.context()?.activeTeam ?? []);
  readonly activeTeamCount = computed(() => this.activeTeam().length);
  readonly availableCandidatesCount = computed(() => this.availableCandidates().length);
  readonly directCandidatesCount = computed(() => this.directCandidates().length);

  private readonly normalizedSearch = computed(() => this.candidateSearch().toLowerCase().trim());
  readonly filteredAvailableCandidates = computed(() => this.filterByName(this.availableCandidates()));
  readonly filteredDirectCandidates = computed(() => this.filterByName(this.directCandidates()));

  ngOnInit(): void {
    this.load();
  }

  toggleRoster(): void {
    this.rosterExpanded.update(expanded => !expanded);
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

    this.loadDirectCandidates();
  }

  loadDirectCandidates(): void {
    const id = this.effectiveEventId();
    if (!id) return;

    this.directLoading.set(true);
    this.directProblem.set(null);
    this.service.getDirectAssignmentCandidates(id)
      .pipe(finalize(() => this.directLoading.set(false)))
      .subscribe({
        next: data => {
          this.directCandidates.set(data.candidates);
        },
        error: error => {
          this.directProblem.set(toApiProblem(error, 'Could not load direct assignment candidates.'));
        },
      });
  }

  selectVolunteer(candidate: AvailableCandidate, event?: MouseEvent): void {
    const id = this.effectiveEventId();
    if (!id || !this.isWindowOpen() || this.loading() || this.submittingId()) return;
    const action = event?.currentTarget as HTMLButtonElement | undefined;

    this.submittingId.set(candidate.userId);
    this.problem.set(null);
    this.statusMessage.set(null);

    this.service.selectVolunteer(id, candidate.userId)
      .pipe(finalize(() => this.submittingId.set(null)))
      .subscribe({
        next: response => {
          this.applySelection(response, candidate.userName);
          this.keepPoolFocus(action, '.btn-select');
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

  directAssignLeader(candidate: DirectAssignmentCandidate, event?: MouseEvent): void {
    const id = this.effectiveEventId();
    if (!id || !this.isWindowOpen() || this.loading() || this.directLoading() || this.submittingId()) return;
    const action = event?.currentTarget as HTMLButtonElement | undefined;

    this.submittingId.set(candidate.userId);
    this.problem.set(null);
    this.statusMessage.set(null);

    this.service.directAssignLeader(id, candidate.userId)
      .pipe(finalize(() => this.submittingId.set(null)))
      .subscribe({
        next: response => {
          this.applyDirectAssignment(response, candidate.userName);
          this.keepPoolFocus(action, '.btn-direct-assign');
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
      return cairoDateTime.format(new Date(value)) + ' Cairo';
    } catch {
      return value;
    }
  }

  private filterByName<T extends { userName: string }>(candidates: T[]): T[] {
    const query = this.normalizedSearch();
    return query ? candidates.filter(candidate => candidate.userName.toLowerCase().includes(query)) : candidates;
  }

  private keepPoolFocus(action: HTMLButtonElement | undefined, selector: string): void {
    const section = action?.closest('section');
    if (!section) return;

    afterNextRender(() => {
      const document = section.ownerDocument;
      if (document.activeElement !== document.body) return;

      const nextAction = section.querySelector<HTMLElement>(`${selector}:enabled`);
      (nextAction ?? section.querySelector<HTMLElement>('h2'))?.focus();
    }, { injector: this.injector });
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
    const updatedTeam = [...current.activeTeam.filter(member => member.assignmentId !== response.id), newMember];

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
    const updatedTeam = [...current.activeTeam.filter(member => member.assignmentId !== response.id), newMember];

    this.directCandidates.set(updatedDirect);
    this.context.set({
      ...current,
      activeTeam: updatedTeam,
    });

    this.statusMessage.set(`Leader ${userName} directly assigned to active team roster.`);
  }
}
