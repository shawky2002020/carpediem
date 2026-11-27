import { expect, Page, test } from '@playwright/test';

async function mockSession(page: Page, roles: string[], allowLogin = true) {
  const session = { userId: '10000000-0000-0000-0000-000000000001', userName: 'fixture-user', roles };
  await page.route('**/api/**', async route => {
    switch (new URL(route.request().url()).pathname) {
      case '/api/session/me':
        return route.fulfill({ status: 401, json: { title: 'Sign in required' } });
      case '/api/session/antiforgery':
        return route.fulfill({ json: { requestToken: 'test-token' } });
      case '/api/session/login':
        return route.fulfill(allowLogin
          ? { json: session }
          : { status: 401, json: { title: 'Sign in failed' } });
      default:
        return route.fulfill({ json: [] });
    }
  });
}

async function submitSignIn(page: Page) {
  await page.getByLabel('Username', { exact: true }).fill('fixture-user');
  await page.locator('#password').fill('fixture-test-password');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
}

test('sign-in resumes the protected route and preserves the query and fragment through refresh', async ({ page }) => {
  await mockSession(page, ['Admin']);
  await page.goto('/draft-events/new?origin=field#form');
  await expect(page).toHaveURL(/\/sign-in$/);
  await page.reload();
  await submitSignIn(page);
  await expect(page).toHaveURL(/\/draft-events\/new\?origin=field#form$/);
  await expect(page.getByRole('heading', { name: 'Create Draft Event' })).toBeVisible();
});

test('return to a protected route still enforces its role guard', async ({ page }) => {
  await mockSession(page, ['Leader']);
  await page.goto('/admin/accounts');
  await expect(page).toHaveURL(/\/sign-in$/);
  await submitSignIn(page);
  await expect(page).toHaveURL(/\/access-denied$/);
});

test('direct sign-in ignores return destinations from public query parameters', async ({ page }) => {
  await mockSession(page, ['Leader']);
  await page.goto('/sign-in?returnUrl=https%3A%2F%2Fexample.invalid');
  await submitSignIn(page);
  await expect(page.getByRole('heading', { name: 'Your workspace' })).toBeVisible();
  expect(new URL(page.url()).pathname).toBe('/');
});

test('pending registration guidance is shared with everyone and a failed login stays generic', async ({ page }) => {
  await mockSession(page, ['Leader'], false);
  await page.goto('/sign-in');
  const guidance = page.getByText('New Leader accounts require Admin approval before sign-in.');
  await expect(guidance).toBeVisible();
  await submitSignIn(page);
  await expect(page.getByRole('alert')).toHaveText('Sign in failed. Check your username and password and try again.');
  await expect(guidance).toBeVisible();
});
