import { Component, HostListener, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormControl, ReactiveFormsModule, ValidationErrors, ValidatorFn } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../../../core/http/api.interceptor';
import {
  ChiefLeaderAppointmentContext,
  ChiefLeaderAppointmentService,
} from '../../data/chief-leader-appointment.service';

@Component({
  selector: 'app-chief-leader-appointment',
  imports: [RouterLink, ReactiveFormsModule],
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
  readonly recordingExternal = signal(false);
  readonly externalUncertain = signal(false);
  readonly success = signal<string | null>(null);
  readonly problem = signal<ApiProblem | null>(null);
  readonly justification = new FormControl('', {
    nonNullable: true,
    validators: [meaningfulText(), control =>
      control.value.length <= 1000 ? null : { maxlength: true }],
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.problem.set(null);
    this.selectedCandidateId.set(null);
    this.uncertain.set(false);
    this.externalUncertain.set(false);
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

  openExternalAcceptance(): void {
    if (this.saving() || this.context()?.currentAppointment?.status !== 'Pending') return;
    this.recordingExternal.set(true);
    this.problem.set(null);
    this.success.set(null);
  }

  recordExternalAcceptance(): void {
    const appointment = this.context()?.currentAppointment;
    this.justification.markAsTouched();
    this.justification.updateValueAndValidity();
    if (!appointment || appointment.status !== 'Pending' || this.justification.invalid || this.saving()) return;
    this.saving.set(true);
    this.problem.set(null);
    this.success.set(null);
    this.appointments.recordExternalAcceptance(this.eventId, appointment.id,
      appointment.version, this.justification.value.trim())
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: result => {
          const current = this.context();
          if (current) this.context.set({ ...current, currentAppointment: result, candidates: [] });
          this.externalUncertain.set(false);
          this.recordingExternal.set(false);
          this.justification.reset();
          this.success.set(`External acceptance recorded for ${result.candidate.userName}.`);
        },
        error: error => {
          const problem = toApiProblem(error,
            'External acceptance could not be recorded. Your justification is still here.');
          this.problem.set(problem);
          if (error.status === 0 || error.status >= 500) this.externalUncertain.set(true);
          if (problem.code === 'appointment_conflict' || problem.code === 'team_assignment_conflict') {
            this.load();
          }
        },
      });
  }

  retryExternalAcceptance(): void { this.recordExternalAcceptance(); }

  cancelExternalAcceptance(): void {
    if (this.saving()) return;
    this.recordingExternal.set(false);
    this.externalUncertain.set(false);
    this.problem.set(null);
    this.justification.reset();
  }

  justificationError(): string | null {
    const server = this.problem()?.errors?.['justification']?.[0];
    if (server) return server;
    if (!this.justification.touched) return null;
    if (this.justification.hasError('required')) return 'Enter why this external confirmation is being recorded.';
    if (this.justification.hasError('maxlength')) return 'Justification must be 1,000 characters or fewer.';
    return null;
  }

  cancel(): void {
    void this.router.navigate(['/draft-events', this.eventId]);
  }

  canDeactivate(): boolean {
    if (this.hasUnsavedExternalAcceptance())
      return window.confirm('Leave without recording this external acceptance?');
    return this.context()?.currentAppointment !== null || !this.selectedCandidateId() ||
      window.confirm('Leave without appointing this Chief Leader?');
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (!this.hasUnsavedExternalAcceptance() &&
        (this.context()?.currentAppointment !== null || !this.selectedCandidateId())) return;
    event.preventDefault();
    event.returnValue = '';
  }

  private hasUnsavedExternalAcceptance(): boolean {
    return this.recordingExternal() && this.context()?.currentAppointment?.status === 'Pending' &&
      this.justification.value.length > 0;
  }
}

function meaningfulText(): ValidatorFn {
  return control => control.value.trim().length > 0 ? null : { required: true } as ValidationErrors;
}
