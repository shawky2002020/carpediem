import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { mockDraftApi } from './draft-api.fixture';

test('Draft creation has no automatically detectable WCAG A or AA violations', async ({ page }) => {
  await mockDraftApi(page);
  await page.goto('/draft-events/new');
  await expect(page.getByRole('heading', { name: 'Create Draft Event' })).toBeVisible();

  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(results.violations).toEqual([]);
});

test('validation feedback is associated with the affected field and keyboard reachable', async ({ page }) => {
  await mockDraftApi(page);
  await page.goto('/draft-events/new');
  await page.getByLabel('Title *').fill('   ');
  await page.getByLabel('Title *').blur();

  await expect(page.getByLabel('Title *')).toHaveAttribute('aria-invalid', 'true');
  await expect(page.locator('#title-error')).toContainText('Title is required.');
  await page.keyboard.press('Tab');
  await expect(page.locator(':focus')).toBeVisible();
});

test('text scaling reflows and reduced motion removes entry animation', async ({ page }) => {
  await mockDraftApi(page);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/draft-events/new');
  await page.evaluate(() => {
    document.documentElement.style.fontSize = '200%';
    document.body.classList.add('cd-enter');
  });

  const overflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);
  await expect(page.getByRole('button', { name: 'Create Draft' })).toBeVisible();
  const animationName = await page.evaluate(() => getComputedStyle(document.body).animationName);
  expect(animationName).toBe('none');
});

test('invalid form submission moves keyboard focus to the rendered error summary', async ({ page }) => {
  await mockDraftApi(page);
  await page.goto('/draft-events/new');
  await page.getByRole('button', { name: 'Create Draft' }).press('Enter');
  const summary = page.getByRole('alert').filter({ hasText: 'Review the highlighted fields.' });
  await expect(summary).toBeFocused();
  await expect(summary).toHaveText('Review the highlighted fields.');
  await page.keyboard.press('Tab');
  await expect(page.locator('#title')).toBeFocused();
});
