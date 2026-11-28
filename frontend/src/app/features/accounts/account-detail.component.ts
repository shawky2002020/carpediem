import { DatePipe, Location } from '@angular/common';
import { afterNextRender, Component, computed, DestroyRef, ElementRef, inject, Injector, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SessionService } from '../../core/auth/session.service';
import { toApiProblem } from '../../core/http/api.interceptor';
import { HasPendingChanges } from '../../core/routing/pending-changes.guard';
import { NavigationIconComponent } from '../../layout/navigation-icon.component';
import {
  AccessAction, AccountDetail, AccountDirectoryService, accountName, accountRoles, accountStatuses,
  roleLabel, secondaryEmail,
} from './account-directory.service';

const historyLabels: Record<string, string> = {
  AccountRegistrationRequested: 'Registration submitted',
  AccountApproved: 'Access enabled',
  AccountDeactivated: 'Account deactivated',
  AccountRolesChanged: 'Roles updated',
};

@Component({
  selector: 'app-account-detail',
  imports: [DatePipe, NavigationIconComponent],
  templateUrl: './account-detail.component.html',
  styleUrl: './account-detail.component.scss',
  host: { '(window:beforeunload)': 'beforeUnload($event)' },
})
export class AccountDetailComponent implements OnInit, HasPendingChanges {
  private readonly directory = inject(AccountDirectoryService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly session = inject(SessionService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly userId = this.route.snapshot.paramMap.get('userId')!;
  private readonly editor = new FormControl<string[]>([], { nonNullable: true });

  protected readonly account = signal<AccountDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly editing = signal(false);
  protected readonly saving = signal<'roles' | AccessAction | null>(null);
  protected readonly loadError = signal('');
  protected readonly actionError = signal('');
  protected readonly conflict = signal(false);
  protected readonly success = signal('');
  protected readonly draft = toSignal(this.editor.valueChanges, { initialValue: this.editor.value });
  protected readonly isOwnAccount = computed(() => this.account()?.userId === this.session.session()?.userId);
  protected readonly dirty = computed(() => {
    const roles = this.account()?.roles ?? [];
    const draft = this.draft();
    return this.editing() && (roles.length !== draft.length || roles.some(role => !draft.includes(role)));
  });
  protected readonly unavailable = computed(() => this.loading() || !!this.saving() || !!this.loadError() || this.conflict());
  protected readonly canSave = computed(() => this.dirty() && !this.unavailable() && this.draft().length > 0);

  protected readonly accountName = accountName;
  protected readonly secondaryEmail = secondaryEmail;
  protected readonly roleLabel = roleLabel;
  protected readonly roleOptions = accountRoles;
  protected readonly statusLabels = accountStatuses;
  protected readonly historyLabels = historyLabels;
  protected readonly backUrl = this.location.prepareExternalUrl('/admin/accounts');

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    const previous = this.account();
    const editing = this.editing();
    const draft = this.draft();
    this.loading.set(true);
    this.loadError.set('');
    this.directory.detail(this.userId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: account => {
        this.account.set(account);
        this.loading.set(false);
        if (editing && previous) {
          const removed = previous.roles.filter(role => !draft.includes(role));
          const added = draft.filter(role => !previous.roles.includes(role));
          const rebased = [...new Set([...account.roles.filter(role => !removed.includes(role)), ...added])];
          this.editor.setValue(rebased);
          this.success.set('Account refreshed. Review your role changes before saving.');
        }
        if (this.conflict()) {
          this.conflict.set(false);
          this.actionError.set('');
        }
      },
      error: error => {
        this.loadError.set(toApiProblem(error, 'Account details could not be loaded.').title);
        this.loading.set(false);
      },
    });
  }

  protected goBack(event: MouseEvent): void {
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return;
    const historyState = this.location.getState() as Record<string, unknown> | null;
    const position = this.directory.position;
    if (position && position.key === historyState?.['accountsListKey'] &&
        position.viewerId === this.session.session()?.userId && position.accountId === this.userId) {
      event.preventDefault();
      this.location.back();
    } else {
      event.preventDefault();
      void this.router.navigate(['/admin/accounts']);
    }
  }

  protected editRoles(account: AccountDetail): void {
    this.editor.setValue([...account.roles]);
    this.editing.set(true);
    this.actionError.set('');
    this.success.set('');
    this.focus('#account-role-options input:not(:disabled)');
  }

  protected toggleRole(role: string, checked: boolean): void {
    const roles = this.draft();
    this.editor.setValue(checked ? [...roles, role] : roles.filter(item => item !== role));
  }

  protected cancelRoles(): void {
    this.editing.set(false);
    if (!this.conflict()) this.actionError.set('');
    this.success.set('');
    this.focus('#account-edit-roles');
  }

  protected saveRoles(account: AccountDetail): void {
    if (!this.canSave()) return;
    this.start('roles');
    this.directory.saveRoles(account, this.draft()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => {
        this.account.set({ ...account, roles: result.roles });
        this.editing.set(false);
        this.completed('Roles saved.', '#account-roles-heading');
      },
      error: error => this.failed(error, 'Roles could not be saved. Try saving again.'),
    });
  }

  protected changeAccess(account: AccountDetail, action: AccessAction): void {
    if (this.unavailable() || this.editing()) return;
    this.start(action);
    this.directory.changeAccess(account, action).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => {
        this.account.set({ ...account, status: result.isActive ? 'active' : 'inactive', pendingSince: null });
        const message = action === 'deactivate' ? 'Account deactivated.'
          : account.status === 'pending' ? 'Registration approved.' : 'Account reactivated.';
        this.completed(message, '#account-access-heading');
      },
      error: error => this.failed(error, 'Account access could not be updated. Try the action again.'),
    });
  }

  canDeactivate(): boolean {
    return !this.saving() && (!this.dirty() || window.confirm('Discard your unsaved role changes?'));
  }

  protected beforeUnload(event: BeforeUnloadEvent): void {
    if (this.saving() || this.dirty()) event.preventDefault();
  }

  private start(action: 'roles' | AccessAction): void {
    this.actionError.set('');
    this.success.set('');
    this.saving.set(action);
  }

  private completed(message: string, focusTarget: string): void {
    this.saving.set(null);
    this.success.set(message);
    this.load();
    this.focus(focusTarget);
  }

  private failed(error: unknown, fallback: string): void {
    const problem = toApiProblem(error, fallback);
    this.saving.set(null);
    this.conflict.set(problem.code === 'account_conflict');
    const message = problem.detail || problem.title;
    this.actionError.set(problem.code === 'account_update_failed' ? message + ' Try again.' : message);
    this.focus('#account-action-error');
  }

  private focus(selector: string): void {
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus(), { injector: this.injector });
  }
}
