import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AccountAdminComponent } from './account-admin.component';
import { AccountPage } from './account-directory.service';

const accountUrl = '/api/admin/accounts';
const firstPage: AccountPage = {
  items: [{ userId: 'first', userName: 'First account', email: 'first@example.invalid', roles: ['Leader'],
    status: 'active', pendingSince: null }],
  page: 1, pageSize: 20, hasMore: true, totalAccounts: 21, reviewCount: 0, matchingAccounts: 21,
};

describe('AccountAdminComponent', () => {
  let fixture: ComponentFixture<AccountAdminComponent>;
  let http: HttpTestingController;
  let page: HTMLElement;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      imports: [AccountAdminComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AccountAdminComponent);
    page = fixture.nativeElement;
    fixture.detectChanges();
    http.expectOne(request => request.url === accountUrl).flush(firstPage);
    fixture.detectChanges();
  });

  afterEach(() => {
    try {
      http.verify();
    } finally {
      TestBed.resetTestingModule();
      vi.useRealTimers();
    }
  });

  it('cancels an obsolete search immediately and debounces the replacement', () => {
    const field = page.querySelector<HTMLInputElement>('#account-search')!;
    field.value = 'first';
    field.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(275);
    const obsolete = http.expectOne(request => request.headers.get('X-Account-Search') === 'first');

    field.value = 'second@example.invalid';
    field.dispatchEvent(new Event('input'));
    expect(obsolete.cancelled).toBe(true);
    vi.advanceTimersByTime(274);
    http.expectNone(request => request.url === accountUrl);
    vi.advanceTimersByTime(1);
    const latest = http.expectOne(request => request.headers.get('X-Account-Search') === 'second%40example.invalid');
    expect(latest.request.params.has('search')).toBe(false);
    expect(latest.request.params.get('page')).toBe('1');
    latest.flush({ ...firstPage, hasMore: false, matchingAccounts: 0, items: [] });
    fixture.detectChanges();
    expect(page.querySelector('.empty-results')?.textContent).toContain('No matching accounts');
    expect(page.querySelector('.account-row')).toBeNull();
  });

  it('preserves the loaded page when a page request fails and retries the requested page', () => {
    const next = page.querySelectorAll<HTMLButtonElement>('.pagination button')[1];
    next.click();
    http.expectOne(request => request.params.get('page') === '2').flush(null, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();

    expect(page.querySelector('#account-results')?.textContent).toContain('Showing last loaded results');
    expect(page.querySelector('.account-row')?.textContent).toContain('First account');
    expect(next.disabled).toBe(true);
    page.querySelector<HTMLButtonElement>('.result-error button')!.click();
    http.expectOne(request => request.params.get('page') === '2').flush({ ...firstPage, page: 2, hasMore: false });
    fixture.detectChanges();
    expect(page.querySelector('.displayed-range')?.textContent?.replace(/\s+/g, ' ').trim()).toBe('21–21 of 21');
    expect(next.disabled).toBe(true);
  });
});
