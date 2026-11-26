import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { mkdir, rename } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { wireFixtures } from './visual-fixtures.mjs';

const require = createRequire(import.meta.url);
const { chromium } = require('../../tests/e2e/node_modules/playwright');
const directory = fileURLToPath(new URL('../../docs/ux/excellence/public/recordings/', import.meta.url));
const screenshots = fileURLToPath(new URL('../../docs/ux/excellence/public/round-3/', import.meta.url));
const baseURL = process.env.CARPEDIEM_BASE_URL || 'http://127.0.0.1:4216';
await mkdir(directory, { recursive: true });
await mkdir(screenshots, { recursive: true });
const browser = await chromium.launch({ slowMo: 150 });
try {
  const viewport = { width: 390, height: 844 };
  const context = await browser.newContext({ baseURL, viewport, recordVideo: { dir: directory, size: viewport } });
  const page = await context.newPage();
  await wireFixtures(page, null);
  // Hold only the fixture response so the recording can verify the pending state.
  let release;
  const pending = new Promise(resolve => { release = resolve; });
  await page.route('**/api/accounts/register', async route => {
    await pending;
    await route.fulfill({ status: 204 });
  });
  await page.goto('/');
  await page.getByRole('main').getByRole('link', { name: 'Register as a Leader' }).click();
  await page.getByLabel('Username').fill('sample.leader');
  await page.getByLabel('Email').fill('sample');
  const password = page.getByLabel('Password', { exact: true });
  await password.focus();
  await page.getByText('Enter a valid email address.').waitFor();
  await page.waitForTimeout(700);
  await page.getByLabel('Email').fill('sample@example.invalid');
  await password.fill('DemoOnly!2026');
  await page.getByRole('button', { name: 'Show password' }).focus();
  await page.keyboard.press('Enter');
  assert.equal(await password.getAttribute('type'), 'text');
  await page.waitForTimeout(600);
  await page.keyboard.press('Space');
  assert.equal(await password.getAttribute('type'), 'password');
  await page.getByRole('button', { name: 'Register', exact: true }).click();
  assert.equal(await page.getByRole('button', { name: 'Submitting…' }).isDisabled(), true);
  await page.waitForTimeout(700);
  release();
  await page.getByText(/Registration received/).waitFor();
  await page.waitForTimeout(900);
  await page.getByRole('main').getByRole('link', { name: 'Sign in' }).click();
  await page.route('**/api/session/login', route => route.fulfill({ status: 401, json: { title: 'Invalid credentials' } }));
  await page.getByLabel('Username').fill('sample.leader');
  await page.getByLabel('Password', { exact: true }).fill('DemoOnly!2026');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('alert').filter({ hasText: 'Sign in failed.' }).waitFor();
  await page.getByLabel('Username').focus();
  await page.waitForTimeout(800);
  const video = page.video();
  await context.close();
  await rename(await video.path(), directory + 'registration-and-sign-in.webm');
  for (const [role, featured] of [['Admin', 'Operational dashboard'], ['ChiefLeader', 'My appointments'], ['Leader', 'Notifications']]) {
    const dashboard = await browser.newPage({ baseURL, viewport, reducedMotion: 'reduce' });
    await wireFixtures(dashboard, role);
    await dashboard.goto('/');
    await dashboard.locator('.workspace-feature').filter({ hasText: featured }).waitFor();
    await dashboard.evaluate(() => document.fonts.ready);
    for (const width of [390, 1440]) {
      await dashboard.setViewportSize({ width, height: width === 390 ? 844 : 960 });
      await dashboard.screenshot({ path: screenshots + 'workspace-' + role + '-' + width + '.png' });
    }
    await dashboard.setViewportSize({ width: 320, height: 800 });
    const overflow = await dashboard.evaluate(() => {
      document.documentElement.style.fontSize = '200%';
      return document.documentElement.scrollWidth - document.documentElement.clientWidth;
    });
    assert.ok(overflow <= 1, role + ' workspace overflow at 200% text: ' + overflow);
    await dashboard.close();
  }
  console.log('Passed validation, keyboard password visibility, pending submission, registration confirmation, route navigation, sign-in error and all three role workspace reflow checks.');
} finally {
  await browser.close();
}
