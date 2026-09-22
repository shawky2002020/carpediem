import { expect, test } from '@playwright/test';
import { mockDraftApi, seededDraft } from './draft-api.fixture';

test('Admin appoints one Chief Leader and reopens the Pending appointment', async ({ page }) => {
  const api = await mockDraftApi(page);
  api.seed(seededDraft);
  await page.goto(`/draft-events/${seededDraft.id}/chief-leader`);

  await expect(page.getByRole('heading', { name: 'Appoint Chief Leader' })).toBeVisible();
  await page.getByLabel('chief.one').check();
  await page.getByRole('button', { name: 'Appoint Chief Leader' }).click();

  await expect(page.getByText('Pending', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'chief.one' })).toBeVisible();
  expect(api.appointmentCount).toBe(1);
  await page.reload();
  await expect(page.getByRole('heading', { name: 'chief.one' })).toBeVisible();
  await expect(page.getByText('awaiting a response', { exact: false })).toBeVisible();
});

test('uncertain appointment retries the same intent without a duplicate', async ({ page }) => {
  const api = await mockDraftApi(page, { appointmentUncertainOnce: true });
  api.seed(seededDraft);
  await page.goto(`/draft-events/${seededDraft.id}/chief-leader`);
  await page.getByLabel('chief.two').check();
  await page.getByRole('button', { name: 'Appoint Chief Leader' }).click();

  await expect(page.getByText('outcome is uncertain', { exact: false })).toBeVisible();
  await page.getByTestId('retry-appointment').click();
  await expect(page.getByText('Pending', { exact: true })).toBeVisible();
  expect(api.appointmentCount).toBe(2);
  expect(api.appointment?.candidate.userName).toBe('chief.two');
});

test('stale candidate eligibility provides an actionable reload path', async ({ page }) => {
  const api = await mockDraftApi(page, { appointmentIneligibleOnce: true });
  api.seed(seededDraft);
  await page.goto(`/draft-events/${seededDraft.id}/chief-leader`);
  await page.getByLabel('chief.one').check();
  await page.getByRole('button', { name: 'Appoint Chief Leader' }).click();

  await expect(page.getByText('candidate list may have changed', { exact: false })).toBeVisible();
  await page.getByRole('button', { name: 'Reload candidates' }).click();
  await expect(page.getByLabel('chief.one')).toBeVisible();
});

test('ordinary Leader cannot open the Admin appointment flow', async ({ page }) => {
  const api = await mockDraftApi(page, { role: 'Leader' });
  api.seed(seededDraft);
  await page.goto(`/draft-events/${seededDraft.id}/chief-leader`);

  await expect(page).toHaveURL(/access-denied/);
  await expect(page.getByText('chief.one')).toHaveCount(0);
});

test('leaving an unconfirmed choice creates no appointment', async ({ page }) => {
  const api = await mockDraftApi(page);
  api.seed(seededDraft);
  await page.goto(`/draft-events/${seededDraft.id}/chief-leader`);
  await page.getByLabel('chief.one').check();
  page.once('dialog', dialog => dialog.accept());

  await page.getByRole('button', { name: 'Cancel' }).click();

  await expect(page).toHaveURL(new RegExp(`/draft-events/${seededDraft.id}$`));
  expect(api.appointmentCount).toBe(0);
});
