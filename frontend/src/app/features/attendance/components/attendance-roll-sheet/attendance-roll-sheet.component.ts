import { afterNextRender, Component, computed, ElementRef, inject, Injector, Input, OnInit, signal, viewChild, viewChildren } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import {
  AttendanceOutcomeType,
  AttendanceRollSheet,
  FinalizeAttendanceRequest,
  CorrectAttendanceRequest
} from '../../data/attendance.model';
import { AttendanceService } from '../../data/attendance.service';
import { SessionService } from '../../../../core/auth/session.service';
import { OperationalSpineComponent } from '../../../../shared/components/operational-spine/operational-spine.component';

@Component({
  selector: 'app-attendance-roll-sheet',
  standalone: true,
  imports: [RouterLink, ReactiveFormsModule, OperationalSpineComponent],
  templateUrl: './attendance-roll-sheet.component.html',
  styleUrl: './attendance-roll-sheet.component.scss',
})
export class AttendanceRollSheetComponent implements OnInit {
  private readonly service = inject(AttendanceService);
  private readonly route = inject(ActivatedRoute);
  private readonly injector = inject(Injector);
  private readonly correctionOutcomeField = viewChild<ElementRef<HTMLSelectElement>>('correctionOutcomeField');
  private readonly correctionReasonField = viewChild<ElementRef<HTMLInputElement>>('correctionReasonField');
  private readonly correctionActions = viewChildren<ElementRef<HTMLButtonElement>>('correctAction');
  readonly sessionService = inject(SessionService);

  @Input() eventId?: string;

  readonly effectiveEventId = computed(
    () => this.eventId ?? this.route.snapshot.paramMap.get('eventId') ?? ''
  );

  readonly rollSheet = signal<AttendanceRollSheet | null>(null);
  readonly outcomesState = signal<Map<string, AttendanceOutcomeType>>(new Map());
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly statusMessage = signal<string | null>(null);
  readonly correctingOutcomeId = signal<string | null>(null);
  readonly correctionForm = new FormGroup({
    outcome: new FormControl<AttendanceOutcomeType>('Attended', { nonNullable: true }),
    reason: new FormControl('', { nonNullable: true }),
  });
  readonly correctionReasonRequired = signal(false);
  readonly correctionSubmitting = signal(false);

  readonly entries = computed(() => this.rollSheet()?.entries ?? []);
  readonly totalCount = computed(() => this.entries().length);

  private readonly counts = computed(() => {
    const counts = { Attended: 0, NoShow: 0, Unrecorded: 0 };
    const pending = this.outcomesState();
    for (const entry of this.entries()) {
      counts[pending.get(entry.teamAssignmentId) ?? entry.outcome ?? 'Unrecorded']++;
    }
    return counts;
  });

  readonly attendedCount = computed(() => this.counts().Attended);
  readonly noShowCount = computed(() => this.counts().NoShow);
  readonly unrecordedCount = computed(() => this.counts().Unrecorded);
  readonly hasChanges = computed(() => this.outcomesState().size > 0);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const id = this.effectiveEventId();
    if (!id) {
      this.loading.set(false);
      this.errorMessage.set('An event is required to open attendance.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.service.getRollSheet(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: sheet => {
          this.rollSheet.set(sheet);
          this.loadFailed.set(false);
          const editable = new Set(sheet.entries.filter(entry => !entry.finalizedAt).map(entry => entry.teamAssignmentId));
          this.outcomesState.update(pending => new Map([...pending].filter(([assignmentId]) => editable.has(assignmentId))));
          const correctionId = this.correctingOutcomeId();
          if (correctionId && !sheet.entries.some(entry => entry.teamAssignmentId === correctionId && entry.attendanceOutcomeId)) {
            this.cancelCorrection();
          }
        },
        error: err => {
          this.loadFailed.set(true);
          this.errorMessage.set(err.error?.detail ?? 'Failed to load attendance roll sheet.');
        }
      });
  }

  setOutcome(teamAssignmentId: string, outcome: AttendanceOutcomeType): void {
    const entry = this.entries().find(item => item.teamAssignmentId === teamAssignmentId);
    if (!entry || entry.finalizedAt || this.loading() || this.loadFailed() || this.submitting() || this.correctionSubmitting()) return;

    const map = new Map(this.outcomesState());
    map.set(teamAssignmentId, outcome);
    this.outcomesState.set(map);
    this.statusMessage.set(`Marked ${entry.volunteerName} as ${outcome === 'Attended' ? 'attended' : 'no show'}.`);
  }

  getEffectiveOutcome(teamAssignmentId: string): AttendanceOutcomeType | null {
    if (this.outcomesState().has(teamAssignmentId)) {
      return this.outcomesState().get(teamAssignmentId) ?? null;
    }
    const entry = this.entries().find(e => e.teamAssignmentId === teamAssignmentId);
    return entry?.outcome ?? null;
  }

  markAllAttended(): void {
    if (this.loading() || this.loadFailed() || this.submitting() || this.correctionSubmitting()) return;

    const map = new Map(this.outcomesState());
    for (const entry of this.entries()) {
      if (!entry.finalizedAt) {
        map.set(entry.teamAssignmentId, 'Attended');
      }
    }
    this.outcomesState.set(map);
    this.statusMessage.set('Marked all unfinalized volunteers as Attended.');
  }

  submit(): void {
    const id = this.effectiveEventId();
    if (!id || this.loading() || this.loadFailed() || this.submitting() || this.correctionSubmitting() || !this.hasChanges()) return;

    this.submitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const submissions = Array.from(this.outcomesState().entries()).map(([teamAssignmentId, outcome]) => ({
      teamAssignmentId,
      outcome
    }));

    const request: FinalizeAttendanceRequest = { entries: submissions };

    this.service.finalizeAttendance(id, request)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          this.outcomesState.set(new Map());
          this.successMessage.set('Attendance finalized successfully!');
          this.load();
        },
        error: err => {
          this.errorMessage.set(err.error?.detail ?? 'Failed to finalize attendance roll call.');
        }
      });
  }

  startCorrection(teamAssignmentId: string, currentOutcome: AttendanceOutcomeType): void {
    this.correctingOutcomeId.set(teamAssignmentId);
    this.correctionForm.reset({ outcome: currentOutcome === 'Attended' ? 'NoShow' : 'Attended', reason: '' });
    this.correctionReasonRequired.set(false);
    afterNextRender(() => this.correctionOutcomeField()?.nativeElement.focus(), { injector: this.injector });
  }

  cancelCorrection(): void {
    const assignmentId = this.correctingOutcomeId();
    this.correctingOutcomeId.set(null);
    this.correctionReasonRequired.set(false);
    afterNextRender(() => {
      const action = this.correctionActions().find(button => button.nativeElement.dataset['correctionId'] === assignmentId);
      action?.nativeElement.focus();
    }, { injector: this.injector });
  }

  submitCorrection(teamAssignmentId: string): void {
    const entry = this.entries().find(e => e.teamAssignmentId === teamAssignmentId);
    if (!this.sessionService.isAdmin() || !entry?.attendanceOutcomeId || this.correctionSubmitting() || this.loading() || this.loadFailed() || this.submitting()) return;

    const form = this.correctionForm.getRawValue();
    const reason = form.reason.trim();
    if (!reason) {
      this.correctionReasonRequired.set(true);
      this.correctionReasonField()?.nativeElement.focus();
      return;
    }

    this.correctionSubmitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.correctionForm.disable();

    const request: CorrectAttendanceRequest = {
      newOutcome: form.outcome,
      reason
    };

    this.service.correctAttendance(entry.attendanceOutcomeId, request, entry.version)
      .pipe(finalize(() => {
        this.correctionSubmitting.set(false);
        this.correctionForm.enable();
      }))
      .subscribe({
        next: response => {
          this.rollSheet.update(sheet => sheet && ({
            ...sheet,
            entries: sheet.entries.map(item => item.teamAssignmentId === response.teamAssignmentId
              ? { ...item, outcome: response.newOutcome, version: response.version }
              : item),
          }));
          this.successMessage.set('Attendance corrected successfully.');
          this.cancelCorrection();
        },
        error: err => {
          if (err.status === 409) this.loadFailed.set(true);
          this.errorMessage.set(err.error?.detail ?? 'Failed to correct attendance record.');
        }
      });
  }
}
