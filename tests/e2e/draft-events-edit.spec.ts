import { expect, test } from '@playwright/test';
import { mockDraftApi, seededDraft } from './draft-api.fixture';

test('stale edit preserves the Admin values for review', async ({ page }) => {
  const api = await mockDraftApi(page, { updateFailure: 'stale' });
  api.seed(seededDraft);
  await page.goto(`/draft-events/${seededDraft.id}`);

  await page.getByLabel('Title *').fill('My unsaved correction');
  await page.getByRole('button', { name: 'Save Changes' }).click();

  await expect(page.getByRole('heading', { name: 'The Draft changed since you opened it' })).toBeVisible();
  await expect(page.getByLabel('Title *')).toHaveValue('My unsaved correction');
});
