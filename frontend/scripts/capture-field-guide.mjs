import { createRequire } from 'node:module';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { wireFixtures } from './visual-fixtures.mjs';

const require = createRequire(import.meta.url);
const { chromium, expect } = require('../../tests/e2e/node_modules/@playwright/test');
const { AxeBuilder } = require('../../tests/e2e/node_modules/@axe-core/playwright');
const phase = process.argv[2] ?? 'after';
if (!['before', 'round-1', 'round-2', 'after'].includes(phase)) throw new Error('Unknown review stage');
const out = fileURLToPath(new URL('../../docs/ux/excellence/components/' + phase + '/', import.meta.url));
await mkdir(out, { recursive: true });
const browser = await chromium.launch();
try {
  for (const width of [390, 1440]) {
    const context = await browser.newContext({ viewport: { width, height: width === 390 ? 844 : 960 } });
    const page = await context.newPage();
    await wireFixtures(page, 'Admin');
    await page.goto((process.env.CARPEDIEM_BASE_URL ?? 'http://127.0.0.1:4216') + '/design-lab');
    await page.getByRole('heading', {name:'Component field guide'}).waitFor();
    await page.evaluate(() => document.fonts.ready);
    if (await page.locator('vite-error-overlay').count()) throw new Error('Angular compilation overlay');
    await page.screenshot({ path:path.join(out, 'guide-' + width + '.png'), fullPage:true });
    await page.locator('#actions').screenshot({ path:path.join(out, 'actions-' + width + '.png') });
    await page.getByRole('button', {name:'Toggle loading'}).click();
    await page.getByRole('button', {name:'Confirm selection'}).click();
    await page.locator('dialog[open]').screenshot({ path:path.join(out, 'confirmation-' + width + '.png'), animations:'disabled' });
    await page.keyboard.press('Escape');
    if (!await page.getByRole('button', {name:'Confirm selection'}).evaluate(e => e === document.activeElement)) throw new Error('Dialog focus did not return');
    if (phase === 'after') {
      const primary = page.getByRole('button', {name:'Confirm selection'});
      await primary.hover();
      await page.locator('#actions').screenshot({ path:path.join(out, 'hover-' + width + '.png') });
      await page.keyboard.press('Tab');
      await expect(page.getByRole('button', {name:'Toggle loading'})).toBeFocused();
      await page.locator('#actions').screenshot({ path:path.join(out, 'focus-' + width + '.png') });
      await page.getByRole('checkbox').check();
      await page.getByRole('radio', {name:'No-show'}).check();
      await page.getByRole('combobox').selectOption({label:'Attended'});
      await page.getByText('Assignment history', {exact:true}).click();
      await page.locator('#controls').screenshot({path:path.join(out, 'choices-' + width + '.png')});
      await page.getByText('Preview account controls').click();
      await page.getByRole('button', {name:'Show password'}).click();
      await expect(page.getByLabel('Password', {exact:true})).toHaveAttribute('type','text');
      await page.getByRole('button', {name:'Create Draft'}).click();
      await expect(page.getByRole('alert')).toContainText('Review the highlighted fields');
      await page.locator('.form-error-summary').screenshot({path:path.join(out, 'validation-' + width + '.png')});
      const axe = await new AxeBuilder({page}).withTags(['wcag2a','wcag2aa','wcag21aa','wcag22aa']).analyze();
      if (axe.violations.length) throw new Error(JSON.stringify(axe.violations.map(v=>[v.id,v.nodes.map(n=>n.target)])));
      await page.emulateMedia({reducedMotion:'reduce'});
      await primary.click();
      expect(await page.locator('dialog').evaluate(el => getComputedStyle(el).transitionDuration.split(',').every(n => parseFloat(n) === 0))).toBe(true);
      await page.keyboard.press('Escape');
    }
    await context.close();
  }
} finally { await browser.close(); }
