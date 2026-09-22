import { Component, HostListener, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../../../core/http/api.interceptor';
import {
  ChiefLeaderAppointmentContext,
  ChiefLeaderAppointmentService,
} from '../../data/chief-leader-appointment.service';

@Component({
  selector: 'app-chief-leader-appointment',
  imports: [RouterLink],
  templateUrl: './appointment.component.html',
  styleUrl: './appointment.component.scss',
})
export class AppointmentComponent implements OnInit {
  private readonly appointments = inject(ChiefLeaderAppointmentService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly eventId = this.route.snapshot.paramMap.get('id')!;
  private requestToken = crypto.randomUUID();

  readonly context = signal<ChiefLeaderAppointmentContext | null>(null);
  readonly selectedCandidateId = signal<string | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly uncertain = signal(false);
  readonly success = signal<string | null>(null);
  readonly problem = signal<ApiProblem | null>(null);

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.problem.set(null);
    this.selectedCandidateId.set(null);
    this.uncertain.set(false);
    this.appointments.getContext(this.eventId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: value => this.context.set(value),
        error: error => this.problem.set(toApiProblem(error, 'Chief Leader appointment could not be opened.')),
      });
  }

  selectCandidate(candidateUserId: string): void {
    if (this.saving() || this.uncertain()) return;
    if (this.selectedCandidateId() !== candidateUserId) this.requestToken = crypto.randomUUID();
    this.selectedCandidateId.set(candidateUserId);
    this.problem.set(null);
  }

  appoint(): void {
    const candidateUserId = this.selectedCandidateId();
    if (!candidateUserId || this.saving()) return;
    this.saving.set(true);
    this.problem.set(null);
    this.success.set(null);
    this.appointments.appoint(this.eventId, candidateUserId, this.requestToken)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: appointment => {
          const current = this.context();
          if (current) this.context.set({ ...current, currentAppointment: appointment, candidates: [] });
          this.uncertain.set(false);
          this.success.set(`${appointment.candidate.userName} is awaiting a response.`);
        },
        error: error => {
          const problem = toApiProblem(error, 'The appointment could not be saved. Your selection is still here.');
          this.problem.set(problem);
          if (error.status === 0 || error.status >= 500) this.uncertain.set(true);
          if (problem.code === 'chief_leader_already_assigned' || problem.code === 'appointment_conflict') {
            this.load();
          }
          if (problem.code === 'candidate_ineligible') this.requestToken = crypto.randomUUID();
        },
      });
  }

  retry(): void { this.appoint(); }

  cancel(): void {
    void this.router.navigate(['/draft-events', this.eventId]);
  }

  canDeactivate(): boolean {
    return this.context()?.currentAppointment !== null || !this.selectedCandidateId() ||
      window.confirm('Leave without appointing this Chief Leader?');
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.context()?.currentAppointment !== null || !this.selectedCandidateId()) return;
    event.preventDefault();
    event.returnValue = '';
  }
}
