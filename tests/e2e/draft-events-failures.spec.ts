import { expect, test } from '@playwright/test';
import { mockDraftApi, seededDraft } from './draft-api.fixture';

test('non Admin is sent to a clear access denied state', async ({ page }) => {
  await mockDraftApi(page, { role: 'Leader' });
  await page.goto('/draft-events');
  await expect(page).toHaveURL(/\/access-denied$/);
  await expect(page.getByRole('heading', { name: 'Administrator access is required' })).toBeVisible();
});

test('unknown Draft has a safe not found state', async ({ page }) => {
  await mockDraftApi(page);
  await page.goto(`/draft-events/${seededDraft.id}`);
  await expect(page.getByText('This Draft may have been removed or the link may be incorrect.')).toBeVisible();
  await expect(page.getByText('e2e-trace')).toHaveCount(0);
});

test('recoverable edit failure preserves entered values', async ({ page }) => {
  const api = await mockDraftApi(page, { updateFailure: 'network' });
  api.seed(seededDraft);
  await page.goto(`/draft-events/${seededDraft.id}`);
  await page.getByLabel('Title *').fill('Retained after failure');
  await page.getByRole('button', { name: 'Save Changes' }).click();
  await expect(page.getByRole('alert')).toContainText('could not be saved');
  await expect(page.getByLabel('Title *')).toHaveValue('Retained after failure');
});

test('expired session during save gives a sign-in path and preserves values', async ({ page }) => {
  const api = await mockDraftApi(page, { updateFailure: 'unauthenticated' });
  api.seed(seededDraft);
  await page.goto(`/draft-events/${seededDraft.id}`);
  await page.getByLabel('Title *').fill('Retained through sign in');
  await page.getByRole('button', { name: 'Save Changes' }).click();

  await expect(page.getByRole('heading', { name: 'Your session has expired' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Open sign in' })).toBeVisible();
  await expect(page.getByLabel('Title *')).toHaveValue('Retained through sign in');
});
