import { Component, computed, input } from '@angular/core';

const cairoDate = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeZone: 'Africa/Cairo' });
const cairoTime = new Intl.DateTimeFormat('en-GB', { timeStyle: 'short', timeZone: 'Africa/Cairo' });
const cairoDateTime = new Intl.DateTimeFormat('en-GB', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'Africa/Cairo',
});

@Component({
  selector: 'app-operational-spine',
  templateUrl: './operational-spine.component.html',
  styleUrl: './operational-spine.component.scss',
})
export class OperationalSpineComponent {
  readonly title = input.required<string>();
  readonly venue = input('');
  readonly startDateTime = input('');
  readonly endDateTime = input('');

  readonly schedule = computed(() => {
    const start = new Date(this.startDateTime());
    const end = new Date(this.endDateTime());
    if (Number.isNaN(start.getTime())) return null;

    const startLabel = cairoDateTime.format(start);
    if (Number.isNaN(end.getTime())) return { start: startLabel, end: '' };

    const sameDay = cairoDate.format(start) === cairoDate.format(end);
    return { start: startLabel, end: sameDay ? cairoTime.format(end) : cairoDateTime.format(end) };
  });
}
