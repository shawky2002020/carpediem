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
  template: `<section class="accounts-page">
    <header class="page-header">
      <p class="eyebrow">Identity & Access</p>
      <h1>Accounts</h1>
      <p class="subtitle">Approve registrations and manage access.</p>
    </header>
    @if (error()) { <p role="alert" class="alert-error">{{ error() }}</p> }
    @if (loading()) { <p role="status">Loading accounts…</p> }
    <ul class="accounts">@for (account of accounts(); track account.userId) {
      <li class="account-card">
        <div class="account-header">
          <div>
            <h2>{{ account.userName }}</h2>
            <p class="account-meta">{{ account.email }} · {{ account.isActive ? 'Active' : 'Pending or inactive' }}</p>
          </div>
          <span class="status-pill" [class.active]="account.isActive">{{ account.isActive ? 'Active' : 'Pending' }}</span>
        </div>
        <fieldset [disabled]="busy() === account.userId"><legend>Roles</legend>
          @for (role of roleOptions; track role) {
            <label class="role-option"><input type="checkbox" [checked]="account.roles.includes(role)" (change)="setRole(account, role, $event)"> {{ role }}</label>
          }
        </fieldset>
        <div class="account-actions">
          @if (account.isActive) { <button type="button" class="btn-secondary" [disabled]="busy() === account.userId || account.userId === session.session()?.userId" (click)="changeActive(account, false)">Deactivate</button> }
          @else { <button type="button" class="btn-primary" [disabled]="busy() === account.userId" (click)="changeActive(account, true)">Approve / reactivate</button> }
        </div>
      </li>
    }</ul>
    <nav aria-label="Account pages" class="pagination-nav">
      <button type="button" [disabled]="page() === 1 || loading()" (click)="load(page() - 1)">Previous</button>
      <span class="page-indicator">Page {{ page() }}</span>
      <button type="button" [disabled]="!hasMore() || loading()" (click)="load(page() + 1)">Next</button>
    </nav>
  </section>`,
  styles: [`
    .accounts-page { display: grid; gap: var(--cd-space-5); }
    .eyebrow { margin: 0 0 var(--cd-space-1); font-size: var(--cd-font-size-xs); font-weight: var(--cd-weight-bold); text-transform: uppercase; letter-spacing: var(--cd-letter-spacing-caps); color: var(--cd-text-secondary); }
    .subtitle { margin: 0; color: var(--cd-text-secondary); }
    .alert-error { margin: 0; padding: var(--cd-space-3) var(--cd-space-4); border-radius: var(--cd-radius-control); background: var(--cd-status-danger-bg); border-inline-start: 4px solid var(--cd-status-danger-fg); color: var(--cd-status-danger-fg); }
    .accounts { list-style: none; padding: 0; margin: 0; display: grid; gap: var(--cd-space-4); grid-template-columns: repeat(auto-fit, minmax(min(100%, 22rem), 1fr)); }
    .account-card { border: 1px solid var(--cd-border-default); border-radius: var(--cd-radius-panel); padding: var(--cd-space-5); background: var(--cd-bg-surface); display: grid; gap: var(--cd-space-3); }
    .account-header { display: flex; justify-content: space-between; align-items: flex-start; gap: var(--cd-space-3); }
    .account-header h2 { margin: 0 0 var(--cd-space-1); font-size: var(--cd-font-size-lg); }
    .account-meta { margin: 0; color: var(--cd-text-secondary); font-size: var(--cd-font-size-sm); }
    .status-pill { padding: 0.2rem 0.65rem; border-radius: var(--cd-radius-pill); font-size: var(--cd-font-size-xs); font-weight: var(--cd-weight-bold); background: var(--cd-status-warning-bg); color: var(--cd-status-warning-fg); }
    .status-pill.active { background: var(--cd-status-success-bg); color: var(--cd-status-success-fg); }
    fieldset { display: flex; flex-wrap: wrap; gap: var(--cd-space-2); margin: 0; padding: var(--cd-space-3); background: var(--cd-bg-canvas); border: 1px solid var(--cd-border-default); border-radius: var(--cd-radius-control); }
    .role-option { display: inline-flex; align-items: center; gap: var(--cd-space-2); min-block-size: var(--cd-touch-target-min); padding: var(--cd-space-1) var(--cd-space-3); border-radius: var(--cd-radius-control); background: var(--cd-bg-surface); border: 1px solid var(--cd-border-default); font-size: var(--cd-font-size-sm); font-weight: var(--cd-weight-medium); cursor: pointer; }
    .account-actions { display: flex; justify-content: flex-end; }
    .btn-primary { background: var(--cd-action-primary); border-color: var(--cd-action-primary); color: var(--cd-text-inverse); }
    .pagination-nav { display: flex; align-items: center; justify-content: space-between; gap: var(--cd-space-4); padding-block-start: var(--cd-space-3); border-block-start: 1px solid var(--cd-border-default); }
    .page-indicator { font-weight: var(--cd-weight-semibold); color: var(--cd-text-secondary); }
  `],
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
