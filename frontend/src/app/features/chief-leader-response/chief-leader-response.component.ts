import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize, Observable } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../core/http/api.interceptor';
import { ChiefLeaderResponse, ChiefLeaderResponseService } from './chief-leader-response.service';

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
  readonly saving = signal(false);
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
    this.saving.set(true);
    this.problem.set(null);
    this.success.set(null);
    this.service.respond(appointment.id, action, appointment.version)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: value => {
          this.detail.set(value);
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
    return new Intl.DateTimeFormat('en', {
      timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short',
    }).format(new Date(value)) + ' Cairo';
  }
}
