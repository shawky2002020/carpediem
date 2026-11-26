import { createRequire } from 'node:module';
import { mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require('../../tests/e2e/node_modules/playwright');
const { AxeBuilder } = require('../../tests/e2e/node_modules/@axe-core/playwright');
const baseUrl = process.env.CARPEDIEM_BASE_URL ?? 'http://127.0.0.1:4216';
const phase = process.argv[2];
if (!['before', 'after'].includes(phase)) throw new Error('Specify before or after.');
const output = process.env.CARPEDIEM_CAPTURE_DIR ? process.env.CARPEDIEM_CAPTURE_DIR.replace(/[/\\]?$/, '/') : fileURLToPath(new URL(`../../docs/ux/transformation/screenshots/${phase}/`, import.meta.url));
await mkdir(output, { recursive: true });

import { eventId, wireFixtures } from './visual-fixtures.mjs';
const corePages = [
  ['welcome', '/', null],
  ['sign-in', '/sign-in', null],
  ['register', '/register', null],
  ['operations', '/dashboard', 'Admin'],
  ['drafts', '/draft-events', 'Admin'],
  ['draft-create', '/draft-events/new', 'Admin'],
  ['appointments', '/my-appointments', 'ChiefLeader'],
  ['team', `/events/${eventId}/team-selection`, 'ChiefLeader'],
  ['replacement', `/events/${eventId}/replacement-assignment`, 'ChiefLeader'],
  ['attendance', `/events/${eventId}/attendance`, 'ChiefLeader'],
  ['availability', `/events/${eventId}/availability`, 'Leader'],
  ['inbox', '/notifications', 'Leader'],
  ['profile', '/profile', 'Leader'],
];
const secondaryPages = [
  ['workspace', '/', 'Admin'],
  ['draft-edit', `/draft-events/${eventId}`, 'Admin'],
  ['appoint', `/draft-events/${eventId}/chief-leader`, 'Admin'],
  ['appointment', `/my-appointments/${eventId}`, 'ChiefLeader'],
  ['assignment', `/events/${eventId}/my-assignment`, 'Leader'],
  ['leader-history', `/events/${eventId}/leaders/f2000000-0000-4000-8000-000000000001/history`, 'ChiefLeader'],
  ['cancel', `/events/${eventId}/cancel`, 'Admin'],
  ['reschedule', `/events/${eventId}/reschedule`, 'Admin'],
  ['complete', `/events/${eventId}/complete`, 'Admin'],
  ['accounts', '/admin/accounts', 'Admin'],
  ['feedback-review', '/admin/feedback', 'Admin'],
  ['incident-review', '/admin/incidents', 'Admin'],
  ['event-feedback', `/events/${eventId}/feedback`, 'ChiefLeader'],
  ['event-incidents', `/events/${eventId}/incidents`, 'ChiefLeader'],
  ['access-denied', '/admin/accounts', 'Leader'],
];
const allPages = phase === 'before' && !process.env.CARPEDIEM_ALL_ROUTES ? corePages : [...corePages, ...secondaryPages];
const selected = process.env.CARPEDIEM_PAGES?.split(',');
const pages = allPages.filter(([name]) => !selected || selected.includes(name));

const browser = await chromium.launch();
const widths = process.env.CARPEDIEM_WIDTHS ? process.env.CARPEDIEM_WIDTHS.split(',').map(Number) : phase === 'before' ? [390, 1440] : [320, 375, 390, 430, 768, 1024, 1440, 1920];
const issues = [];
try {
  for (const width of widths) {
    for (const [name, url, role] of pages) {
      const context = await browser.newContext({ viewport: { width, height: width < 600 ? 844 : 960 }, reducedMotion: 'reduce' });
      const page = await context.newPage();
      page.on('pageerror', error => issues.push(`${name}@${width} browser exception: ${error.message}`));
      page.on('response', response => {
        const pathname = new URL(response.url()).pathname;
        if (response.status() >= 400 && !(pathname === '/api/session/me' && !role) && !pathname.startsWith('/api/')) {
          issues.push(`${name}@${width} HTTP ${response.status()} for ${pathname}`);
        }
      });
      await wireFixtures(page, role, message => issues.push(`${name}@${width} ${message}`));
      await page.goto(baseUrl + url, { waitUntil: 'networkidle' });
      await page.locator('main h1').first().waitFor();
      await page.evaluate(() => document.fonts.ready);
      if (await page.locator('vite-error-overlay').count()) throw new Error('Angular dev-server error overlay on ' + name);
      if (process.env.CARPEDIEM_LARGE_TEXT === '1') {
        await page.evaluate(() => document.documentElement.style.fontSize = '200%');
      }
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      if (overflow > 1) issues.push(`${name}@${width} horizontal overflow ${overflow}px`);
      if (width === 390 || width === 1440) {
        await page.screenshot({ path: `${output}${name}-${width}.png`, animations: 'disabled' });
        if (phase === 'after' && !process.env.CARPEDIEM_SKIP_AXE) {
          const audit = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
          for (const violation of audit.violations) {
            issues.push(`${name}@${width} axe ${violation.id}: ${violation.nodes.map(node => node.target.join(' ')).join(', ')}`);
          }
        }
      }
      await context.close();
    }
  }
} finally {
  await browser.close();
}
console.log(JSON.stringify({ phase, pages: pages.length, widths, issues }, null, 2));
if (phase === 'after' && issues.length) process.exitCode = 1;
