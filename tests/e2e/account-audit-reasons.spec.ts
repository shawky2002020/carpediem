import { expect, test } from '@playwright/test';

test('role changes and deactivation require a reason and keep it through an API error', async ({ page }) => {
  const accountId = '50000000-0000-0000-0000-000000000001';
  const adminId = '50000000-0000-0000-0000-000000000002';
  let account = {
    userId: accountId, userName: 'fixture.leader', email: 'fixture.leader@example.invalid',
    isActive: true, status: 'active', roles: ['Leader'], version: 'first', history: [],
  };
  const roleRequests: unknown[] = [];
  const deactivateRequests: unknown[] = [];

  await page.route('**/api/**', async route => {
    const request = route.request();
    const pathname = new URL(request.url()).pathname;
    if (pathname === '/api/session/me') return route.fulfill({ json: { userId: adminId, userName: 'fixture.admin', roles: ['Admin'] } });
    if (pathname === '/api/session/antiforgery') return route.fulfill({ json: { requestToken: 'fixture-token' } });
    if (pathname === `/api/admin/accounts/${accountId}`) return route.fulfill({ json: account });
    if (pathname.endsWith('/roles')) {
      const body = request.postDataJSON();
      roleRequests.push(body);
      account = { ...account, roles: body.roles, version: 'second' };
      return route.fulfill({ json: { roles: account.roles } });
    }
    if (pathname.endsWith('/deactivate')) {
      deactivateRequests.push(request.postDataJSON());
      if (deactivateRequests.length === 1) {
        return route.fulfill({ status: 503, json: {
          code: 'account_update_failed', status: 503, title: 'Account access is temporarily unavailable.',
        } });
      }
      account = { ...account, isActive: false, status: 'inactive', version: 'third' };
      return route.fulfill({ json: { isActive: false } });
    }
    return route.fulfill({ json: [] });
  });

  await page.goto(`/admin/accounts/${accountId}`);
  await page.getByRole('button', { name: 'Edit roles' }).click();
  await page.getByRole('checkbox', { name: 'Chief Leader', exact: true }).check();
  const save = page.getByRole('button', { name: 'Save changes' });
  await expect(save).toBeDisabled();
  const rolesReason = page.getByLabel('Reason for changing roles');
  await rolesReason.fill('  ');
  await expect(save).toBeDisabled();
  await rolesReason.fill('Lead the field team');
  await save.click();
  await expect(page.getByText('Roles saved.')).toBeVisible();
  expect(roleRequests).toEqual([{ roles: ['Leader', 'ChiefLeader'], reason: 'Lead the field team' }]);

  const deactivate = page.getByRole('button', { name: 'Deactivate', exact: true });
  await deactivate.click();
  const dialog = page.getByRole('dialog');
  const confirm = dialog.getByRole('button', { name: 'Confirm deactivation' });
  const reason = dialog.getByLabel('Reason for deactivating');
  await expect(confirm).toBeDisabled();
  await reason.fill('Access review completed');
  await confirm.click();
  await expect(page.getByRole('alert')).toContainText('Account access is temporarily unavailable.');
  await deactivate.click();
  await expect(reason).toHaveValue('Access review completed');
  await confirm.click();
  await expect(page.getByText('Account deactivated.')).toBeVisible();
  expect(deactivateRequests).toEqual([{ reason: 'Access review completed' }, { reason: 'Access review completed' }]);
});
