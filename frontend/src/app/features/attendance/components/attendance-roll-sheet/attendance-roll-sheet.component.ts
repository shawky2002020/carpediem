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

@Component({
  selector: 'app-attendance-roll-sheet',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
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

    this.service
      .getRollSheet(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: data => {
          this.rollSheet.set(data);
          // Initialize state with current outcomes
          const initialMap = new Map<string, AttendanceOutcomeType>();
          for (const entry of data.entries) {
            if (entry.outcome) {
              initialMap.set(entry.teamAssignmentId, entry.outcome);
            }
          }
          this.outcomesState.set(initialMap);
        },
        error: err => {
          const detail = err.error?.detail ?? err.message ?? 'Failed to load attendance roll sheet.';
          this.errorMessage.set(detail);
        },
      });
  }

  getEffectiveOutcome(assignmentId: string): AttendanceOutcomeType | null {
    const state = this.outcomesState();
    if (state.has(assignmentId)) {
      return state.get(assignmentId) ?? null;
    }
    const entry = this.entries().find(e => e.teamAssignmentId === assignmentId);
    return entry?.outcome ?? null;
  }

  setOutcome(assignmentId: string, outcome: AttendanceOutcomeType): void {
    const updated = new Map(this.outcomesState());
    updated.set(assignmentId, outcome);
    this.outcomesState.set(updated);
    this.successMessage.set(null);
  }

  markAllAttended(): void {
    const updated = new Map(this.outcomesState());
    for (const entry of this.entries()) {
      updated.set(entry.teamAssignmentId, 'Attended');
    }
    this.outcomesState.set(updated);
    this.successMessage.set(null);
  }

  submit(): void {
    const id = this.effectiveEventId();
    if (!id) return;

    const state = this.outcomesState();
    const entriesToSubmit = Array.from(state.entries()).map(([teamAssignmentId, outcome]) => ({
      teamAssignmentId,
      outcome,
    }));

    if (entriesToSubmit.length === 0) {
      this.errorMessage.set('Please mark at least one volunteer attendance outcome before submitting.');
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const request: FinalizeAttendanceRequest = {
      entries: entriesToSubmit,
    };

    this.service
      .finalizeAttendance(id, request)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: response => {
          this.successMessage.set(
            `Attendance finalized successfully! (${response.attendedCount} Attended, ${response.noShowCount} No Show)`
          );
          this.load();
        },
        error: err => {
          const detail = err.error?.detail ?? err.message ?? 'Failed to finalize attendance.';
          this.errorMessage.set(detail);
        },
      });
  }

  startCorrection(teamAssignmentId: string, currentOutcome: AttendanceOutcomeType): void {
    this.correctingOutcomeId.set(teamAssignmentId);
    this.correctionOutcome.set(currentOutcome === 'Attended' ? 'NoShow' : 'Attended');
    this.correctionReason.set('');
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  cancelCorrection(): void {
    this.correctingOutcomeId.set(null);
    this.correctionReason.set('');
  }

  submitCorrection(teamAssignmentId: string): void {
    const entry = this.entries().find(e => e.teamAssignmentId === teamAssignmentId);
    if (!entry || !entry.attendanceOutcomeId) return;

    if (!this.correctionReason().trim()) {
      this.errorMessage.set('Please provide a mandatory reason for the correction.');
      return;
    }

    this.correctionSubmitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const request: CorrectAttendanceRequest = {
      newOutcome: this.correctionOutcome(),
      reason: this.correctionReason().trim(),
    };

    this.service
      .correctAttendance(entry.attendanceOutcomeId, request, entry.version)
      .pipe(finalize(() => this.correctionSubmitting.set(false)))
      .subscribe({
        next: () => {
          this.successMessage.set('Attendance corrected successfully.');
          this.correctingOutcomeId.set(null);
          this.load();
        },
        error: err => {
          const detail = err.error?.detail ?? err.message ?? 'Failed to correct attendance.';
          this.errorMessage.set(detail);
        },
      });
  }
}
