import { Location } from '@angular/common';
import {
  afterNextRender, Component, ElementRef, Injector, inject, OnInit, signal, computed,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { catchError, map, of, Subject, switchMap, timer } from 'rxjs';
import { SessionService } from '../../core/auth/session.service';
import { toApiProblem } from '../../core/http/api.interceptor';
import { NavigationIconComponent } from '../../layout/navigation-icon.component';
import { AccountResultsComponent } from './account-results.component';
import {
  AccountPage, AccountQuery, accountRoles, AccountRole, AccountSort, accountStatuses,
  AccountStatus, AccountView, AccountDirectoryService, roleLabel,
} from './account-directory.service';

const initialQuery: AccountQuery = { search: '', view: 'all', roles: [], status: '', sort: 'name', page: 1 };

@Component({
  selector: 'app-account-admin',
  imports: [ReactiveFormsModule, NavigationIconComponent, AccountResultsComponent],
  templateUrl: './account-admin.component.html',
  styleUrl: './account-admin.component.scss',
})
export class AccountAdminComponent implements OnInit {
  private readonly directory = inject(AccountDirectoryService);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  private readonly viewerId = this.session.session()?.userId;
  private readonly returnPosition = this.readReturnPosition();
  private readonly listKey = this.returnPosition?.key ?? crypto.randomUUID();
  protected readonly detailNavigation = { accountsListKey: this.listKey };

  protected readonly query = signal(this.returnPosition?.query ?? initialQuery);
  protected readonly search = new FormControl(this.query().search, { nonNullable: true });
  protected readonly page = signal<AccountPage | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal('');
  protected readonly filterRoles = signal<AccountRole[]>([]);
  protected readonly filterStatus = signal<AccountStatus | ''>('');
  protected readonly filterCount = computed(() => this.query().roles.length + Number(!!this.query().status));
  protected readonly sortOptions = [
    { value: 'name', label: 'Name A–Z' },
    { value: 'name-desc', label: 'Name Z–A' },
  ] as const;
  protected readonly roleOptions = accountRoles;
  protected readonly statusOptions = ['active', 'pending', 'inactive'] as const;
  protected readonly statusLabels = accountStatuses;
  protected readonly roleLabel = roleLabel;

  private readonly requests = new Subject<{ query: AccountQuery; delay: number; focusResults: boolean }>();
  private restoreOnLoad = !!this.returnPosition;

  constructor() {
    this.requests.pipe(
      switchMap(request => {
        this.loading.set(true);
        this.error.set('');
        return (request.delay ? timer(request.delay) : of(0)).pipe(
          switchMap(() => this.directory.list(request.query)),
          map(page => ({ page, focusResults: request.focusResults, error: '' })),
          catchError(error => of({
            page: null,
            focusResults: false,
            error: toApiProblem(error, 'Accounts could not be loaded.').title,
          })),
        );
      }),
      takeUntilDestroyed(),
    ).subscribe(result => {
      this.loading.set(false);
      this.error.set(result.error);
      const page = result.page;
      if (!page) return;
      this.page.set(page);
      this.query.update(query => ({ ...query, page: page.page }));
      if (this.restoreOnLoad) {
        this.restoreOnLoad = false;
        afterNextRender(() => this.restorePosition(), { injector: this.injector });
      } else if (result.focusResults) {
        afterNextRender(() => this.focusResults(), { injector: this.injector });
      }
    });
    this.search.valueChanges.pipe(takeUntilDestroyed()).subscribe(value => {
      const search = value.trim();
      if (search !== this.query().search) this.update({ search }, search ? 275 : 0);
    });
  }

  ngOnInit(): void {
    this.reload();
  }

  protected reload(): void {
    this.requests.next({ query: this.query(), delay: 0, focusResults: false });
  }

  protected selectView(view: AccountView): void {
    if (view === this.query().view) return;
    this.update({ view, status: '', sort: view === 'review' ? 'oldest' : 'name' });
  }

  protected selectSort(event: Event): void {
    this.update({ sort: (event.target as HTMLSelectElement).value as AccountSort });
  }

  protected changePage(page: number): void {
    const query = { ...this.query(), page };
    this.query.set(query);
    this.requests.next({ query, delay: 0, focusResults: true });
  }

  protected openFilters(dialog: HTMLDialogElement): void {
    this.filterRoles.set(this.query().roles);
    this.filterStatus.set(this.query().status);
    dialog.showModal();
  }

  protected toggleRole(role: AccountRole, checked: boolean): void {
    this.filterRoles.update(roles => checked ? [...roles, role] : roles.filter(item => item !== role));
  }

  protected applyFilters(dialog: HTMLDialogElement): void {
    dialog.close();
    const roles = accountRoles.filter(role => this.filterRoles().includes(role));
    const status = this.filterStatus();
    if (status !== this.query().status || roles.join() !== this.query().roles.join()) this.update({ roles, status });
  }

  protected removeRole(role: AccountRole): void {
    this.update({ roles: this.query().roles.filter(item => item !== role) });
    this.focusFilters();
  }

  protected clearStatus(): void {
    this.update({ status: '' });
    this.focusFilters();
  }

  protected clearFilters(): void {
    this.update({ roles: [], status: '' });
    this.focusFilters();
  }

  protected clearSearch(input: HTMLInputElement): void {
    this.search.setValue('');
    input.focus();
  }

  protected resetQuery(): void {
    this.search.setValue('', { emitEvent: false });
    this.update({ search: '', roles: [], status: '' });
    this.host.nativeElement.querySelector<HTMLInputElement>('#account-search')?.focus();
  }

  protected rememberPosition(event: MouseEvent, accountId: string): void {
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return;
    this.directory.position = { key: this.listKey, viewerId: this.viewerId, accountId, query: this.query(), scrollY: window.scrollY };
    const currentState = this.location.getState() as Record<string, unknown>;
    this.location.replaceState(this.router.url, '', { ...currentState, accountsListKey: this.listKey });
  }

  private update(changes: Partial<AccountQuery>, delay = 0): void {
    const query = { ...this.query(), ...changes, page: 1 };
    this.query.set(query);
    this.requests.next({ query, delay, focusResults: false });
  }

  private readReturnPosition() {
    const position = this.directory.position;
    const navigationState = this.router.currentNavigation()?.extras.state;
    const historyState = this.location.getState() as Record<string, unknown> | null;
    const key = (navigationState ?? historyState)?.['accountsListKey'];
    if (position && position.key === key && position.viewerId === this.viewerId) return position;
    this.directory.position = undefined;
    return undefined;
  }

  private restorePosition(): void {
    const position = this.returnPosition;
    if (!position) return;
    const links = this.host.nativeElement.querySelectorAll<HTMLAnchorElement>('[data-account-id]');
    const link = Array.from(links).find(link => link.dataset['accountId'] === position.accountId && link.getClientRects().length);
    if (!link) {
      this.focusResults();
      return;
    }
    window.scrollTo(0, position.scrollY);
    link.focus({ preventScroll: true });
    const bounds = link.getBoundingClientRect();
    if (bounds.top < 0 || bounds.bottom > window.innerHeight) link.scrollIntoView({ block: 'nearest' });
  }

  private focusResults(): void {
    this.host.nativeElement.querySelector<HTMLElement>('#account-results')?.focus();
  }

  private focusFilters(): void {
    this.host.nativeElement.querySelector<HTMLButtonElement>('#account-filters')?.focus();
  }
}
