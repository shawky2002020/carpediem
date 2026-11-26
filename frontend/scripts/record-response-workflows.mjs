import { createRequire } from 'node:module';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { eventId, wireFixtures } from './visual-fixtures.mjs';

const require = createRequire(import.meta.url);
const { chromium, expect } = require('../../tests/e2e/node_modules/@playwright/test');
const base = process.env.CARPEDIEM_BASE_URL ?? 'http://127.0.0.1:4216';
const folder = fileURLToPath(new URL('../../docs/ux/excellence/recordings/', import.meta.url));
await mkdir(folder, { recursive: true });
const browser = await chromium.launch();
const stamp = '2026-11-26T08:00:00Z';

async function record(name, role, viewport, journey) {
  const context = await browser.newContext({ viewport, recordVideo: { dir: folder, size: viewport } });
  const page = await context.newPage();
  const video = page.video();
  try {
    await wireFixtures(page, role);
    await journey(page);
    await page.waitForTimeout(1500);
    await context.close();
    await video.saveAs(path.join(folder, name + '.webm'));
  } finally {
    await context.close();
    await video.delete();
  }
}

try {
  await record('availability-response', 'Leader', { width: 390, height: 844 }, async page => {
    await page.route('**/api/events/*/availability', async route => {
      const { response } = route.request().postDataJSON();
      await route.fulfill({ json: { eventId, response, declaredAt: stamp, version: 'AQ==' } });
    });
    await page.goto(base + '/events/' + eventId + '/availability');
    const yes = page.getByRole('button', { name: 'Mark Available' });
    const no = page.getByRole('button', { name: 'Mark Not Available' });
    await expect(yes).toBeVisible();
    await page.waitForTimeout(500);
    await yes.focus();
    await yes.press('Enter');
    await expect(yes).toHaveAttribute('aria-pressed', 'true');
    await page.waitForTimeout(650);
    await no.click();
    await expect(no).toHaveAttribute('aria-pressed', 'true');
    await expect(yes).toHaveAttribute('aria-pressed', 'false');
  });
  await record('appointment-acceptance', 'ChiefLeader', { width: 1152, height: 780 }, async page => {
    await page.goto(base + '/my-appointments');
    const request = page.waitForResponse(r => new URL(r.url()).pathname.endsWith('/appointments/' + eventId));
    const pendingLink = page.getByRole('link', { name: /Winter camp orientation/ });
    await expect(pendingLink).toBeVisible();
    await page.waitForTimeout(650);
    await pendingLink.click();
    const pending = await (await request).json();
    await page.route('**/api/chief-leader/appointments/*/accept', route =>
      route.fulfill({ json: { ...pending, status: 'Active', acceptedAt: stamp, version: 'AQ==' } }));
    const accept = page.getByRole('button', { name: 'Accept appointment' });
    await expect(accept).toBeEnabled();
    await page.waitForTimeout(600);
    await accept.click();
    await expect(page.getByText('Appointment accepted. This Event remains a Draft.')).toBeVisible();
  });
  await record('linked-replacement', 'ChiefLeader', { width: 1152, height: 780 }, async page => {
    await page.goto(base + '/events/' + eventId + '/replacement-assignment');
    const disclosure = page.locator('.assignment-options summary');
    await disclosure.click();
    await page.getByLabel('Assignment note').fill('Cover the registration shift.');
    await page.getByRole('radio', { name: /Farida Habib/ }).check();
    await page.waitForTimeout(550);
    await disclosure.click();
    const search = page.getByRole('searchbox');
    await search.fill('Salma');
    await page.route('**/api/events/*/replacement-assignment', async route => {
      if (route.request().method() !== 'POST') return route.fallback();
      const request = route.request().postDataJSON();
      expect(request.replacedAssignmentId).toBeTruthy();
      expect(request.justification).toBe('Cover the registration shift.');
      await route.fulfill({ json: { id: 'f3000000-0000-4000-8000-000000000004', assignedAt: stamp, version: 'AQ==' } });
    });
    await page.waitForTimeout(500);
    await page.getByRole('button', { name: 'Assign Salma Adel as replacement' }).click();
    const member = page.locator('.roster-list').getByText('Salma Adel');
    await expect(member).toBeVisible();
    await search.fill('');
    await member.hover();
    await page.screenshot({ path: path.join(folder, 'linked-replacement-result.png') });
    await expect(page.locator('.live-region')).toContainText('assigned as replacement');
  });
} finally {
  await browser.close();
}
