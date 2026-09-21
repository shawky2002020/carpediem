import { expect, test } from '@playwright/test';
import { mockDraftApi } from './draft-api.fixture';

test('Admin creates, reopens, edits, and reopens a Draft', async ({ page }) => {
  const api = await mockDraftApi(page);
  await page.goto('/draft-events/new');

  await page.getByLabel('Title *').fill('Beach cleanup');
  await page.getByLabel('Start date and time *').fill('2026-10-10T14:30');
  await page.getByLabel('End date and time *').fill('2026-10-10T16:30');
  await page.getByLabel('Location *').fill('North shore');
  await page.getByLabel('Notes Optional').fill('Bring gloves');
  await page.getByRole('button', { name: 'Create Draft' }).click();

  await expect(page).toHaveURL(/\/draft-events\/20000000-/);
  await expect(page.getByText('Draft Event created.')).toBeVisible();
  await expect(page.getByLabel('Title *')).toHaveValue('Beach cleanup');
  expect(api.createCount).toBe(1);

  await page.getByLabel('Title *').fill('Updated cleanup');
  await page.getByRole('button', { name: 'Save Changes' }).click();
  await expect(page.getByText('Draft Event saved.')).toBeVisible();
  await page.reload();
  await expect(page.getByLabel('Title *')).toHaveValue('Updated cleanup');
  await expect(page.getByText('Changes keep this Event in Draft.')).toBeVisible();
});

test('Create without Notes succeeds once', async ({ page }) => {
  const api = await mockDraftApi(page);
  await page.goto('/draft-events/new');
  await page.getByLabel('Title *').fill('No notes Event');
  await page.getByLabel('Start date and time *').fill('2026-10-10T14:30');
  await page.getByLabel('End date and time *').fill('2026-10-10T16:30');
  await page.getByLabel('Location *').fill('Cairo');
  await page.getByRole('button', { name: 'Create Draft' }).click();

  await expect(page).toHaveURL(/\/draft-events\/20000000-/);
  expect(api.createCount).toBe(1);
  expect(api.draft?.notes).toBeNull();
});

test('uncertain create retry resolves the same creation intent without a duplicate', async ({ page }) => {
  const api = await mockDraftApi(page, { createUncertainOnce: true });
  await page.goto('/draft-events/new');
  await page.getByLabel('Title *').fill('Retry-safe Event');
  await page.getByLabel('Start date and time *').fill('2026-10-10T14:30');
  await page.getByLabel('End date and time *').fill('2026-10-10T16:30');
  await page.getByLabel('Location *').fill('Cairo');
  await page.getByRole('button', { name: 'Create Draft' }).click();
  await expect(page.getByText(/save outcome is uncertain/i).first()).toBeVisible();

  await page.getByRole('button', { name: 'Retry save' }).click();

  await expect(page).toHaveURL(/\/draft-events\/20000000-/);
  expect(api.createCount).toBe(2);
  expect(api.draft?.id).toBe('20000000-0000-0000-0000-000000000001');
});

test('leaving an unfinished creation asks before discarding it', async ({ page }) => {
  await mockDraftApi(page);
  await page.goto('/draft-events/new');
  await page.getByLabel('Title *').fill('Unfinished Event');
  page.once('dialog', dialog => dialog.dismiss());

  await page.getByRole('button', { name: 'Cancel' }).click();

  await expect(page).toHaveURL(/\/draft-events\/new$/);
  await expect(page.getByLabel('Title *')).toHaveValue('Unfinished Event');
});
