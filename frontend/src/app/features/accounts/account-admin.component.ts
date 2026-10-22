import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { toApiProblem } from '../../core/http/api.interceptor';
import { SessionService } from '../../core/auth/session.service';

interface Account { userId: string; userName: string; email: string; isActive: boolean; roles: string[]; }
interface AccountPage { items: Account[]; page: number; pageSize: number; hasMore: boolean; }
const roles = ['Leader', 'ChiefLeader', 'Admin'];

@Component({
  selector: 'app-account-admin',
  template: `<section><h1>Accounts</h1><p>Approve registrations and manage access.</p>
    @if (error()) { <p role="alert">{{ error() }}</p> }
    @if (loading()) { <p role="status">Loading accounts…</p> }
    <ul class="accounts">@for (account of accounts(); track account.userId) {
      <li><h2>{{ account.userName }}</h2><p>{{ account.email }} · {{ account.isActive ? 'Active' : 'Pending or inactive' }}</p>
        <fieldset [disabled]="busy() === account.userId"><legend>Roles</legend>
          @for (role of roleOptions; track role) {
            <label><input type="checkbox" [checked]="account.roles.includes(role)" (change)="setRole(account, role, $event)"> {{ role }}</label>
          }
        </fieldset>
        @if (account.isActive) { <button type="button" [disabled]="busy() === account.userId || account.userId === session.session()?.userId" (click)="changeActive(account, false)">Deactivate</button> }
        @else { <button type="button" [disabled]="busy() === account.userId" (click)="changeActive(account, true)">Approve / reactivate</button> }
      </li>
    }</ul>
    <nav aria-label="Account pages">
      <button type="button" [disabled]="page() === 1 || loading()" (click)="load(page() - 1)">Previous</button>
      <span>Page {{ page() }}</span>
      <button type="button" [disabled]="!hasMore() || loading()" (click)="load(page() + 1)">Next</button>
    </nav>
  </section>`,
  styles: ['.accounts { list-style:none; padding:0; display:grid; gap:var(--cd-space-4); } li { border:1px solid var(--cd-border-default); border-radius:var(--cd-radius-control); padding:var(--cd-space-4); } fieldset { display:flex; flex-wrap:wrap; gap:var(--cd-space-3); margin-block:var(--cd-space-3); } button { min-block-size:var(--cd-touch-target-min); }'],
})
export class AccountAdminComponent implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly session = inject(SessionService);
  protected readonly roleOptions = roles;
  protected readonly accounts = signal<Account[]>([]);
  protected readonly loading = signal(true);
  protected readonly page = signal(1);
  protected readonly hasMore = signal(false);
  protected readonly busy = signal<string | null>(null);
  protected readonly error = signal('');
  ngOnInit(): void { this.load(); }
  protected load(page = this.page()): void {
    this.loading.set(true);
    this.http.get<AccountPage>(`/api/admin/accounts?page=${page}`).pipe(finalize(() => this.loading.set(false)))
      .subscribe({ next: data => { this.accounts.set(data.items); this.page.set(data.page); this.hasMore.set(data.hasMore); },
        error: err => this.error.set(toApiProblem(err, 'Accounts could not be loaded.').title) });
  }
  protected changeActive(account: Account, active: boolean): void {
    this.error.set(''); this.busy.set(account.userId);
    this.http.post(`/api/admin/accounts/${account.userId}/${active ? 'approve' : 'deactivate'}`, {})
      .pipe(finalize(() => this.busy.set(null)))
      .subscribe({ next: () => this.load(), error: err => this.error.set(toApiProblem(err, 'Account could not be updated.').title) });
  }
  protected setRole(account: Account, role: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const selected = checked ? [...account.roles, role] : account.roles.filter(item => item !== role);
    if (selected.length === 0 || (account.userId === this.session.session()?.userId && !selected.includes('Admin'))) {
      (event.target as HTMLInputElement).checked = !checked;
      return;
    }
    this.error.set(''); this.busy.set(account.userId);
    this.http.put(`/api/admin/accounts/${account.userId}/roles`, { roles: selected })
      .pipe(finalize(() => this.busy.set(null)))
      .subscribe({ next: () => this.load(), error: err => { (event.target as HTMLInputElement).checked = !checked; this.error.set(toApiProblem(err, 'Roles could not be updated.').title); } });
  }
}
