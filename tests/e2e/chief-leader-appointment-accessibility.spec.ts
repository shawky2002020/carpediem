import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { mockDraftApi, seededDraft } from './draft-api.fixture';

test('appointment has no automatically detectable WCAG A or AA violations', async ({ page }) => {
  const api = await mockDraftApi(page);
  api.seed(seededDraft);
  await page.goto(`/draft-events/${seededDraft.id}/chief-leader`);
  await expect(page.getByRole('group', { name: 'Choose a Chief Leader' })).toBeVisible();

  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(results.violations).toEqual([]);
});

test('appointment works by keyboard with visible focus and announced status', async ({ page }) => {
  const api = await mockDraftApi(page);
  api.seed(seededDraft);
  await page.goto(`/draft-events/${seededDraft.id}/chief-leader`);
  await page.getByLabel('chief.one').focus();
  await page.keyboard.press('Space');
  await expect(page.getByLabel('chief.one')).toBeChecked();
  await page.getByRole('button', { name: 'Appoint Chief Leader' }).focus();
  await expect(page.locator(':focus')).toBeVisible();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('status')).toContainText('awaiting a response');
});

test('appointment reflows at 200 percent text with reduced motion and no horizontal scroll', async ({ page }) => {
  const api = await mockDraftApi(page);
  api.seed(seededDraft);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto(`/draft-events/${seededDraft.id}/chief-leader`);
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });

  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);
  await expect(page.getByRole('button', { name: 'Appoint Chief Leader' })).toBeVisible();
  const target = await page.getByRole('button', { name: 'Appoint Chief Leader' }).boundingBox();
  expect(target!.height).toBeGreaterThanOrEqual(44);
});
