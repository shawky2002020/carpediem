import { afterNextRender, Component, computed, ElementRef, inject, Injector, OnInit, signal, viewChild } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize, Observable } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../core/http/api.interceptor';
import { ChiefLeaderResponse, ChiefLeaderResponseService } from './chief-leader-response.service';

const cairoDay = new Intl.DateTimeFormat('en-GB', { timeZone: 'Africa/Cairo', dateStyle: 'medium' });
const cairoDateTime = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short',
});

@Component({
  selector: 'app-chief-leader-response',
  imports: [RouterLink],
  templateUrl: './chief-leader-response.component.html',
  styleUrl: './chief-leader-response.component.scss',
})
export class ChiefLeaderResponseComponent implements OnInit {
  private readonly service = inject(ChiefLeaderResponseService);
  private readonly route = inject(ActivatedRoute);
  readonly id = this.route.snapshot.paramMap.get('id');

  readonly pending = signal<ChiefLeaderResponse[]>([]);
  readonly detail = signal<ChiefLeaderResponse | null>(null);
  readonly loading = signal(true);
  private readonly injector = inject(Injector);
  private readonly receipt = viewChild<ElementRef<HTMLElement>>('receipt');
  readonly pendingDecision = signal<'accept' | 'decline' | null>(null);
  readonly saving = computed(() => this.pendingDecision() !== null);
  readonly problem = signal<ApiProblem | null>(null);
  readonly success = signal<string | null>(null);
  readonly uncertain = signal(false);

  ngOnInit(): void { this.load(); }

  load(preserveProblem = false): void {
    this.loading.set(true);
    if (!preserveProblem) this.problem.set(null);
    const request: Observable<ChiefLeaderResponse | ChiefLeaderResponse[]> =
      this.id ? this.service.get(this.id) : this.service.listPending();
    request.pipe(finalize(() => this.loading.set(false))).subscribe({
      next: value => {
        if (this.id) this.detail.set(value as ChiefLeaderResponse);
        else this.pending.set(value as ChiefLeaderResponse[]);
        this.uncertain.set(false);
      },
      error: error => {
        this.problem.set(toApiProblem(error, 'Appointments could not be loaded.'));
        this.uncertain.set(true);
      },
    });
  }

  decide(action: 'accept' | 'decline'): void {
    const appointment = this.detail();
    if (!appointment || appointment.status !== 'Pending' || this.saving() || this.uncertain()) return;
    this.pendingDecision.set(action);
    this.problem.set(null);
    this.success.set(null);
    this.service.respond(appointment.id, action, appointment.version)
      .pipe(finalize(() => this.pendingDecision.set(null)))
      .subscribe({
        next: value => {
          this.detail.set(value);
          afterNextRender(() => {
            if (document.activeElement === document.body) this.receipt()?.nativeElement.focus();
          }, { injector: this.injector });
          this.success.set(action === 'accept'
            ? 'Appointment accepted. This Event remains a Draft.'
            : 'Appointment declined. Operations Administrators have been alerted.');
        },
        error: error => {
          const problem = toApiProblem(error, 'The response could not be saved.');
          this.problem.set(problem);
          this.uncertain.set(error.status === 0 || error.status >= 500);
          if (error.status === 409) this.load(true);
        },
      });
  }

  displayDate(value: string): string {
    return cairoDateTime.format(new Date(value)) + ' Cairo';
  }

  invitedOn(value: string): string { return cairoDay.format(new Date(value)); }

  schedule(start: string, end: string): string {
    return cairoDateTime.formatRange(new Date(start), new Date(end));
  }
}
