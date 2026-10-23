import { Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type LifecycleStage = 'Draft' | 'Published' | 'Staffed' | 'InProgress' | 'Concluded';

@Component({
  selector: 'app-operational-spine',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './operational-spine.component.html',
  styleUrl: './operational-spine.component.scss',
})
export class OperationalSpineComponent {
  readonly title = input.required<string>();
  readonly venue = input<string>('');
  readonly startDateTime = input<string>('');
  readonly endDateTime = input<string>('');
  readonly stage = input<LifecycleStage | string>('Draft');
  readonly fulfillmentCurrent = input<number | null>(null);
  readonly fulfillmentTarget = input<number | null>(null);
  readonly chiefLeaderName = input<string | null>(null);

  readonly stages: { id: LifecycleStage; label: string }[] = [
    { id: 'Draft', label: 'Draft' },
    { id: 'Published', label: 'Published' },
    { id: 'Staffed', label: 'Staffed' },
    { id: 'InProgress', label: 'In Progress' },
    { id: 'Concluded', label: 'Concluded' },
  ];

  private readonly stageOrder: Record<string, number> = {
    draft: 0,
    published: 1,
    staffed: 2,
    inprogress: 3,
    active: 3,
    concluded: 4,
    completed: 4,
    cancelled: -1,
  };

  readonly currentStageIndex = computed(() => {
    const raw = (this.stage() || 'draft').toLowerCase().replace(/\s+/g, '');
    return this.stageOrder[raw] ?? 0;
  });

  readonly formattedCairoDate = computed(() => {
    const start = this.startDateTime();
    if (!start) return '';
    try {
      const date = new Date(start);
      return new Intl.DateTimeFormat('en-GB', {
        timeZone: 'Africa/Cairo',
        weekday: 'short',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      }).format(date) + ' Cairo';
    } catch {
      return start;
    }
  });

  readonly countdownText = computed(() => {
    const startStr = this.startDateTime();
    const endStr = this.endDateTime();
    if (!startStr) return '';

    try {
      const now = Date.now();
      const start = new Date(startStr).getTime();
      const end = endStr ? new Date(endStr).getTime() : start + 8 * 3600 * 1000;

      if (now >= start && now <= end) {
        return 'Live Now';
      }
      if (now > end) {
        return 'Concluded';
      }
      const diffMs = start - now;
      const diffHours = Math.round(diffMs / (1000 * 60 * 60));
      if (diffHours <= 1) {
        return 'Starts in < 1 hour';
      }
      if (diffHours < 24) {
        return `Starts in ${diffHours} hours`;
      }
      const diffDays = Math.round(diffHours / 24);
      return `Starts in ${diffDays} day${diffDays > 1 ? 's' : ''}`;
    } catch {
      return '';
    }
  });

  readonly isLiveNow = computed(() => this.countdownText() === 'Live Now');

  readonly fulfillmentPercent = computed(() => {
    const curr = this.fulfillmentCurrent();
    const target = this.fulfillmentTarget();
    if (curr === null || target === null || target <= 0) return null;
    return Math.min(100, Math.round((curr / target) * 100));
  });
}
