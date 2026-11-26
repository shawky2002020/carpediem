import { HttpClient } from '@angular/common/http';
import { afterNextRender, Component, Injector, inject, OnInit, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { toApiProblem } from '../../core/http/api.interceptor';
import { SessionService } from '../../core/auth/session.service';

interface Account { userId: string; userName: string; email: string; isActive: boolean; roles: string[]; }
interface AccountPage { items: Account[]; page: number; pageSize: number; hasMore: boolean; }
const roles = ['Leader', 'ChiefLeader', 'Admin'];

@Component({
  selector: 'app-account-admin',
  template: `<section class="accounts-page cd-reading">
    <header class="page-header">
      <h1>Accounts</h1>
      <p class="subtitle">Approve registrations and manage account access.</p>
    </header>
    @if (error()) {
      <div role="alert" class="cd-alert cd-alert--error alert-error">
        <p>{{ error() }}</p>
        <button type="button" (click)="load()" [disabled]="loading()">Refresh accounts</button>
      </div>
    }
    @if (!loading() && !error() && !accounts().length) {
      <p>No accounts on this page.</p>
    }
    @if (loading()) {
      <div role="status" class="cd-loading-state">
        <p>Loading accounts…</p>
      </div>
    }
    <ul class="accounts">@for (account of accounts(); track account.userId) {
      <li class="account-card">
        <div class="account-header">
          <div>
            <h2>{{ account.userName }}</h2>
            <p class="account-meta">{{ account.email }}</p>
          </div>
          <span class="cd-badge status-pill" [class.cd-badge--success]="account.isActive" [class.cd-badge--warning]="!account.isActive" [class.active]="account.isActive">
            {{ account.isActive ? 'Active' : 'Pending or inactive' }}
          </span>
        </div>
        <fieldset class="roles-fieldset" [disabled]="!!busy() || loading()">
          <legend>Roles<span class="cd-visually-hidden"> for {{ account.userName }}</span></legend>
          <div class="roles-grid">
            @for (role of roleOptions; track role) {
              <label class="role-option">
                <input type="checkbox" [checked]="account.roles.includes(role)" (change)="setRole(account, role, $event)">
                <span>{{ role === 'ChiefLeader' ? 'Chief Leader' : role }}</span>
              </label>
            }
          </div>
        </fieldset>
        <div class="account-actions">
          @if (account.isActive) {
            <button type="button" class="cd-btn cd-btn--sm cd-btn--secondary btn-secondary" [disabled]="!!busy() || loading() || account.userId === session.session()?.userId" (click)="confirmDeactivate(account, deactivateDialog)">
              Deactivate
            </button>
          } @else {
            <button type="button" class="cd-btn cd-btn--sm cd-btn--primary btn-primary" [disabled]="!!busy() || loading()" (click)="changeActive(account, true)">
              Approve / reactivate
            </button>
          }
        </div>
      </li>
    }</ul>
    @if (page() > 1 || hasMore()) { <nav aria-label="Account pages" class="pagination-nav">
      <button type="button" class="cd-btn cd-btn--sm cd-btn--secondary" [disabled]="page() === 1 || loading()" (click)="load(page() - 1)">Previous</button>
      <span class="page-indicator cd-tabular">Page {{ page() }}</span>
      <button type="button" class="cd-btn cd-btn--sm cd-btn--secondary" [disabled]="!hasMore() || loading()" (click)="load(page() + 1)">Next</button>
    </nav> }
    <dialog #deactivateDialog class="deactivation-dialog" aria-labelledby="deactivation-title">
      @if (deactivationTarget(); as account) {
        <h2 id="deactivation-title">Deactivate {{ account.userName }}?</h2>
        <p>The account will lose access until an Admin reactivates it.</p>
        <div class="deactivation-actions">
          <button type="button" (click)="deactivateDialog.close()" autofocus>Keep account active</button>
          <button type="button" class="cd-btn cd-btn--danger"
            (click)="deactivateDialog.close(); changeActive(account, false)">Confirm deactivation</button>
        </div>
      }
    </dialog>
  </section>`,
  styleUrl: './account-admin.component.scss',
})
export class AccountAdminComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);
  protected readonly session = inject(SessionService);
  protected readonly roleOptions = roles;
  protected readonly accounts = signal<Account[]>([]);
  protected readonly loading = signal(true);
  protected readonly page = signal(1);
  protected readonly hasMore = signal(false);
  protected readonly busy = signal<string | null>(null);
  protected readonly error = signal('');
  protected readonly deactivationTarget = signal<Account | null>(null);
  ngOnInit(): void { this.load(); }
  protected load(page = this.page()): void {
    this.loading.set(true);
    this.error.set('');
    this.http.get<AccountPage>(`/api/admin/accounts?page=${page}`).pipe(finalize(() => this.loading.set(false)))
      .subscribe({ next: data => { this.accounts.set(data.items); this.page.set(data.page); this.hasMore.set(data.hasMore); },
        error: err => this.error.set(toApiProblem(err, 'Accounts could not be loaded.').title) });
  }
  protected confirmDeactivate(account: Account, dialog: HTMLDialogElement): void {
    this.deactivationTarget.set(account);
    afterNextRender(() => dialog.showModal(), { injector: this.injector });
  }
  protected changeActive(account: Account, active: boolean): void {
    if (this.busy() || this.loading()) return;
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
