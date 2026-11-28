import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { effect, inject, Injectable } from '@angular/core';
import { SessionService } from '../../core/auth/session.service';

export const accountRoles = ['Leader', 'ChiefLeader', 'Admin'] as const;
export type AccountRole = typeof accountRoles[number];
export type AccountStatus = 'active' | 'inactive' | 'pending';
export type AccountView = 'all' | 'review';
export type AccountSort = 'name' | 'name-desc' | 'oldest';
export type AccessAction = 'approve' | 'deactivate';

export const accountStatuses: Record<AccountStatus, string> = {
  active: 'Active', inactive: 'Inactive', pending: 'Pending approval',
};

export interface Account {
  userId: string;
  userName: string;
  email: string;
  roles: string[];
  status: AccountStatus;
  pendingSince: string | null;
}

export interface AccountDetail extends Account {
  version: string | null;
  history: { action: string; occurredAt: string }[];
}

export interface AccountPage {
  items: Account[];
  page: number;
  pageSize: number;
  hasMore: boolean;
  totalAccounts: number;
  reviewCount: number;
  matchingAccounts: number;
}

export interface AccountQuery {
  search: string;
  view: AccountView;
  roles: AccountRole[];
  status: AccountStatus | '';
  sort: AccountSort;
  page: number;
}

export interface AccountListPosition {
  key: string;
  viewerId: string | undefined;
  accountId: string;
  query: AccountQuery;
  scrollY: number;
}

export function accountName(account: Account): string {
  return account.userName.trim() || account.email.trim() || account.userId;
}

export function secondaryEmail(account: Account): string {
  const email = account.email.trim();
  return accountName(account).toLowerCase() === email.toLowerCase() ? '' : email;
}

export function roleLabel(role: string): string {
  return role === 'ChiefLeader' ? 'Chief Leader' : role;
}

@Injectable({ providedIn: 'root' })
export class AccountDirectoryService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionService);
  private readonly baseUrl = '/api/admin/accounts';

  // Search text stays in memory. A history entry only carries an opaque return key.
  position: AccountListPosition | undefined;

  constructor() {
    effect(() => {
      if (this.session.session()?.userId !== this.position?.viewerId) this.position = undefined;
    });
  }

  list(query: AccountQuery) {
    let params = new HttpParams().set('page', query.page).set('view', query.view).set('sort', query.sort);
    if (query.status) params = params.set('status', query.status);
    for (const role of query.roles) params = params.append('roles', role);
    // The proxy logs request URLs. Search terms may contain personal information.
    const headers = query.search ? { 'X-Account-Search': encodeURIComponent(query.search) } : undefined;
    return this.http.get<AccountPage>(this.baseUrl, { params, headers });
  }

  detail(userId: string) {
    return this.http.get<AccountDetail>(`${this.baseUrl}/${userId}`);
  }

  saveRoles(account: AccountDetail, roles: string[]) {
    return this.http.put<{ roles: string[] }>(`${this.baseUrl}/${account.userId}/roles`, { roles }, { headers: this.versionHeader(account) });
  }

  changeAccess(account: AccountDetail, action: AccessAction) {
    return this.http.post<{ isActive: boolean }>(`${this.baseUrl}/${account.userId}/${action}`, {}, { headers: this.versionHeader(account) });
  }

  private versionHeader(account: AccountDetail): HttpHeaders {
    return new HttpHeaders({ 'If-Match': `"${account.version ?? ''}"` });
  }
}
