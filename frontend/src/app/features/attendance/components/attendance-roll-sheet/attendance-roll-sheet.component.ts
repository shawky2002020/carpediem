import { afterNextRender, Component, computed, ElementRef, HostListener, inject, Injector, Input, OnInit, signal, viewChild, viewChildren } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { attendanceDraftKey, readAttendanceDraft, writeAttendanceDraft } from '../../data/attendance-draft.storage';
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
  readonly draftNotice = signal<string | null>(null);
  readonly draftRecoverable = signal(true);
  readonly signInRequired = signal(false);
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
    this.signInRequired.set(false);
    this.errorMessage.set(null);

    this.service.getRollSheet(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: sheet => {
          const previousCycle = this.rollSheet()?.staffingCycleId;
          const key = this.draftKey(id);
          const remembered = key ? readAttendanceDraft(key, sheet.staffingCycleId) : new Map();
          const pending = this.outcomesState();
          const source = previousCycle === sheet.staffingCycleId && pending.size ? pending : remembered;
          const editable = new Set(sheet.entries.filter(entry => entry.outcome === null && !entry.finalizedAt)
            .map(entry => entry.teamAssignmentId));
          const remaining = new Map([...source].filter(([assignmentId]) => editable.has(assignmentId)));

          this.rollSheet.set(sheet);
          this.loadFailed.set(false);
          this.setDraft(remaining);
          if (source.size > remaining.size) {
            this.draftNotice.set('One or more draft entries were already recorded or are no longer on the roster. Review the latest outcomes.');
          } else if (remaining.size && !pending.size) {
            this.draftNotice.set(`Recovered ${remaining.size} unsaved ${remaining.size === 1 ? 'entry' : 'entries'} in this tab.`);
          }
          const correctionId = this.correctingOutcomeId();
          if (correctionId && !sheet.entries.some(entry => entry.teamAssignmentId === correctionId && entry.attendanceOutcomeId)) {
            this.cancelCorrection();
          }
        },
        error: err => {
          this.loadFailed.set(true);
          if (err.status === 401 || err.status === 403) {
            this.rollSheet.set(null);
            this.outcomesState.set(new Map());
            this.draftNotice.set(null);
          }
          this.signInRequired.set(err.status === 401);
          this.errorMessage.set(err.error?.detail ?? err.error?.title ?? 'Failed to load attendance roll sheet.');
        }
      });
  }

  setOutcome(teamAssignmentId: string, outcome: AttendanceOutcomeType): void {
    const entry = this.entries().find(item => item.teamAssignmentId === teamAssignmentId);
    if (!entry || entry.finalizedAt || this.loading() || this.loadFailed() || this.submitting() || this.correctionSubmitting()) return;

    const map = new Map(this.outcomesState());
    map.set(teamAssignmentId, outcome);
    this.setDraft(map);
    this.statusMessage.set(`Marked ${entry.volunteerName} as ${outcome === 'Attended' ? 'attended' : 'no show'}.`);
  }

  getEffectiveOutcome(teamAssignmentId: string): AttendanceOutcomeType | null {
    if (this.outcomesState().has(teamAssignmentId)) {
      return this.outcomesState().get(teamAssignmentId) ?? null;
    }
    const entry = this.entries().find(e => e.teamAssignmentId === teamAssignmentId);
    return entry?.outcome ?? null;
  }

  markRemainingAttended(): void {
    if (this.loading() || this.loadFailed() || this.submitting() || this.correctionSubmitting() || !this.unrecordedCount()) return;

    const map = new Map(this.outcomesState());
    for (const entry of this.entries()) {
      if (!entry.finalizedAt && entry.outcome === null && !map.has(entry.teamAssignmentId)) {
        map.set(entry.teamAssignmentId, 'Attended');
      }
    }
    this.setDraft(map);
    this.statusMessage.set('Marked unrecorded volunteers as Attended. Existing choices were kept.');
  }

  discardDraft(): void {
    if (!window.confirm('Discard unsaved attendance for this event?')) return;
    this.setDraft(new Map());
    this.draftNotice.set(null);
    this.statusMessage.set('Unsaved attendance entries discarded.');
  }

  canDeactivate(): boolean {
    return !this.hasChanges() || this.draftRecoverable() ||
      window.confirm('Attendance recovery is unavailable. Discard unsaved entries and leave?');
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasChanges() && !this.draftRecoverable()) event.preventDefault();
  }

  private draftKey(eventId: string): string | null {
    const userId = this.sessionService.session()?.userId;
    return userId ? attendanceDraftKey(userId, eventId) : null;
  }

  private setDraft(entries: Map<string, AttendanceOutcomeType>): void {
    this.outcomesState.set(entries);
    const sheet = this.rollSheet();
    const key = sheet ? this.draftKey(sheet.eventId) : null;
    this.draftRecoverable.set(!!key && !!sheet && writeAttendanceDraft(key, sheet.staffingCycleId, entries));
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

    const request: FinalizeAttendanceRequest = { entries: submissions, requireUnrecorded: true };

    this.service.finalizeAttendance(id, request)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          this.setDraft(new Map());
          this.draftNotice.set(null);
          this.successMessage.set('Attendance finalized successfully!');
          this.load();
        },
        error: err => {
          this.signInRequired.set(err.status === 401);
          if (err.status === 409 || err.status === 401 || err.status === 403) this.loadFailed.set(true);
          this.errorMessage.set(err.error?.detail ?? err.error?.title ?? 'Failed to finalize attendance roll call.');
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
