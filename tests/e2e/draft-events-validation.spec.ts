import { expect, test } from '@playwright/test';
import { mockDraftApi } from './draft-api.fixture';

test('missing, whitespace, equal, and reversed values are identified without losing input', async ({ page }) => {
  await mockDraftApi(page);
  await page.goto('/draft-events/new');

  await page.getByRole('button', { name: 'Create Draft' }).click();
  await expect(page.getByRole('alert')).toContainText('Review the highlighted fields.');
  await expect(page.locator('#title-error')).toContainText('Title is required.');

  await page.getByLabel('Title *').fill('\u2003');
  await page.getByLabel('Location *').fill('Cairo');
  await page.getByLabel('Start date and time *').fill('2026-10-10T16:30');
  await page.getByLabel('End date and time *').fill('2026-10-10T14:30');
  await page.getByRole('button', { name: 'Create Draft' }).click();
  await expect(page.locator('#title-error')).toContainText('Title is required.');
  await expect(page.locator('#end-error')).toContainText('must be after');

  await page.getByLabel('Title *').fill('Corrected Event');
  await page.getByLabel('End date and time *').fill('2026-10-10T18:30');
  await page.getByRole('button', { name: 'Create Draft' }).click();
  await expect(page).toHaveURL(/\/draft-events\/20000000-/);
});

test('server Cairo-time feedback stays inline and can be corrected and resubmitted', async ({ page }) => {
  await mockDraftApi(page, {
    createValidationOnce: { field: 'start', message: 'This Cairo local time does not exist. Choose another time.' },
  });
  await page.goto('/draft-events/new');
  await page.getByLabel('Title *').fill('DST Event');
  await page.getByLabel('Location *').fill('Cairo');
  await page.getByLabel('Start date and time *').fill('2026-04-24T00:30');
  await page.getByLabel('End date and time *').fill('2026-04-24T02:30');
  await page.getByRole('button', { name: 'Create Draft' }).click();

  await expect(page.locator('#start-error')).toContainText('does not exist');
  await expect(page.getByLabel('Title *')).toHaveValue('DST Event');
  await page.getByLabel('Start date and time *').fill('2026-04-24T01:30');
  await page.getByRole('button', { name: 'Create Draft' }).click();
  await expect(page).toHaveURL(/\/draft-events\/20000000-/);
});
