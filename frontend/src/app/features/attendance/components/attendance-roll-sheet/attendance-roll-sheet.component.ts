import { CommonModule } from '@angular/common';
import { Component, computed, inject, Input, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import {
  AttendanceOutcomeType,
  AttendanceRollSheet,
  FinalizeAttendanceRequest,
  CorrectAttendanceRequest
} from '../../data/attendance.model';
import { AttendanceService } from '../../data/attendance.service';
import { SessionService } from '../../../../core/auth/session.service';
import { FormsModule } from '@angular/forms';
import { OperationalSpineComponent } from '../../../../shared/components/operational-spine/operational-spine.component';

@Component({
  selector: 'app-attendance-roll-sheet',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, OperationalSpineComponent],
  templateUrl: './attendance-roll-sheet.component.html',
  styleUrl: './attendance-roll-sheet.component.scss',
})
export class AttendanceRollSheetComponent implements OnInit {
  private readonly service = inject(AttendanceService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly sessionService = inject(SessionService);

  @Input() eventId?: string;

  readonly effectiveEventId = computed(
    () => this.eventId ?? this.route.snapshot.paramMap.get('eventId') ?? ''
  );

  readonly rollSheet = signal<AttendanceRollSheet | null>(null);
  readonly outcomesState = signal<Map<string, AttendanceOutcomeType>>(new Map());
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly statusMessage = signal<string | null>(null);
  readonly outdoorMode = signal(true);

  // Correction state
  readonly correctingOutcomeId = signal<string | null>(null);
  readonly correctionReason = signal<string>('');
  readonly correctionOutcome = signal<AttendanceOutcomeType>('Attended');
  readonly correctionSubmitting = signal(false);

  readonly entries = computed(() => this.rollSheet()?.entries ?? []);
  readonly totalCount = computed(() => this.entries().length);

  readonly attendedCount = computed(() => {
    let count = 0;
    const state = this.outcomesState();
    for (const entry of this.entries()) {
      const outcome = state.has(entry.teamAssignmentId)
        ? state.get(entry.teamAssignmentId)
        : entry.outcome;
      if (outcome === 'Attended') count++;
    }
    return count;
  });

  readonly noShowCount = computed(() => {
    let count = 0;
    const state = this.outcomesState();
    for (const entry of this.entries()) {
      const outcome = state.has(entry.teamAssignmentId)
        ? state.get(entry.teamAssignmentId)
        : entry.outcome;
      if (outcome === 'NoShow') count++;
    }
    return count;
  });

  readonly unrecordedCount = computed(() => {
    let count = 0;
    const state = this.outcomesState();
    for (const entry of this.entries()) {
      const outcome = state.has(entry.teamAssignmentId)
        ? state.get(entry.teamAssignmentId)
        : entry.outcome;
      if (!outcome) count++;
    }
    return count;
  });

  readonly hasChanges = computed(() => {
    return this.outcomesState().size > 0;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const id = this.effectiveEventId();
    if (!id) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    this.service.getRollSheet(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: sheet => {
          this.rollSheet.set(sheet);
          this.outcomesState.set(new Map());
        },
        error: err => {
          this.errorMessage.set(err.error?.detail ?? 'Failed to load attendance roll sheet.');
        }
      });
  }

  setOutcome(teamAssignmentId: string, outcome: AttendanceOutcomeType): void {
    const map = new Map(this.outcomesState());
    map.set(teamAssignmentId, outcome);
    this.outcomesState.set(map);

    const entry = this.entries().find(e => e.teamAssignmentId === teamAssignmentId);
    if (entry) {
      this.statusMessage.set(`Marked ${entry.volunteerName} as ${outcome === 'Attended' ? 'Attended' : 'No Show'}.`);
    }
  }

  getEffectiveOutcome(teamAssignmentId: string): AttendanceOutcomeType | null {
    if (this.outcomesState().has(teamAssignmentId)) {
      return this.outcomesState().get(teamAssignmentId) ?? null;
    }
    const entry = this.entries().find(e => e.teamAssignmentId === teamAssignmentId);
    return entry?.outcome ?? null;
  }

  markAllAttended(): void {
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
    if (!id || this.submitting() || !this.hasChanges()) return;

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
    this.correctionOutcome.set(currentOutcome === 'Attended' ? 'NoShow' : 'Attended');
    this.correctionReason.set('');
  }

  cancelCorrection(): void {
    this.correctingOutcomeId.set(null);
    this.correctionReason.set('');
  }

  submitCorrection(teamAssignmentId: string): void {
    const entry = this.entries().find(e => e.teamAssignmentId === teamAssignmentId);
    if (!entry || !entry.attendanceOutcomeId) return;

    const reason = this.correctionReason().trim();
    if (!reason) {
      this.errorMessage.set('Please provide a mandatory reason for the correction.');
      return;
    }

    this.correctionSubmitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const request: CorrectAttendanceRequest = {
      newOutcome: this.correctionOutcome(),
      reason
    };

    this.service.correctAttendance(entry.attendanceOutcomeId, request)
      .pipe(finalize(() => this.correctionSubmitting.set(false)))
      .subscribe({
        next: () => {
          this.successMessage.set('Attendance corrected successfully.');
          this.cancelCorrection();
          this.load();
        },
        error: err => {
          this.errorMessage.set(err.error?.detail ?? 'Failed to correct attendance record.');
        }
      });
  }
}
