import { expect, test } from '@playwright/test';
import { mockDraftApi } from './draft-api.fixture';

test('Draft authoring fits the viewport and keeps primary actions touch sized', async ({ page }) => {
  await mockDraftApi(page);
  await page.goto('/draft-events/new');

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);
  const buttons = page.locator('button');
  for (let index = 0; index < await buttons.count(); index += 1) {
    const box = await buttons.nth(index).boundingBox();
    expect(box?.height).toBeGreaterThanOrEqual(44);
    expect(box?.width).toBeGreaterThanOrEqual(44);
  }
  await expect(page.getByRole('button', { name: 'Create Draft' })).toBeInViewport();
});
