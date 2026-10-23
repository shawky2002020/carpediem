import { createRequire } from 'node:module';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
const { chromium } = require('../../tests/e2e/node_modules/playwright');

const repoRoot = path.resolve(__dirname, '../..');
const baseUrl = process.env.CARPEDIEM_BASE_URL ?? 'http://127.0.0.1:4200';
const password = process.env.CARPEDIEM_SEED_PASSWORD ?? 'Carpediem#2026SeedAdmin!';

const outDirs = {
  desktop: path.join(repoRoot, 'docs/screenshots/desktop'),
  mobile: path.join(repoRoot, 'docs/screenshots/mobile'),
  workflows: path.join(repoRoot, 'docs/screenshots/workflows'),
};

async function ensureDirs() {
  for (const dir of Object.values(outDirs)) {
    await fs.mkdir(dir, { recursive: true });
  }
}

async function signIn(page, userName) {
  await page.goto(`${baseUrl}/sign-in`, { waitUntil: 'networkidle' });
  await page.fill('#userName', userName);
  await page.fill('#password', password);
  await page.click('button[type="submit"]');
  await page.waitForURL(`${baseUrl}/`, { timeout: 10000 });
}

async function signOut(page) {
  try {
    const signOutBtn = page.locator('button.sign-out-button, button:has-text("Sign out")');
    if (await signOutBtn.isVisible({ timeout: 2000 })) {
      await signOutBtn.click();
      await page.waitForTimeout(400);
    } else {
      await page.evaluate(async () => {
        await fetch('/api/session/logout', { method: 'POST', credentials: 'include' });
      });
      await page.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    }
  } catch {
    await page.evaluate(async () => {
      await fetch('/api/session/logout', { method: 'POST', credentials: 'include' });
    });
    await page.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
  }
}

async function capture(page, folder, filename) {
  await page.waitForTimeout(500);
  const target = path.join(outDirs[folder], filename);
  await page.screenshot({ path: target, fullPage: false });
  console.log(`[OK] Captured ${folder}/${filename}`);
}

async function captureFull(page, folder, filename) {
  await page.waitForTimeout(500);
  const target = path.join(outDirs[folder], filename);
  await page.screenshot({ path: target, fullPage: true });
  console.log(`[OK] Captured ${folder}/${filename} (fullPage)`);
}

async function main() {
  await ensureDirs();
  console.log('Starting screenshot capture with Playwright...');
  const browser = await chromium.launch();

  try {
    // 1. Setup session and create deterministic rich demo scenario
    const setupContext = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const setupPage = await setupContext.newPage();

    // Authenticate as Admin and create a new draft event
    await signIn(setupPage, 'admin');
    await setupPage.goto(`${baseUrl}/draft-events/new`, { waitUntil: 'networkidle' });
    await setupPage.fill('#title', 'Cairo Community Gala & Field Summit');
    await setupPage.fill('#startLocal', '2026-11-20T08:00');
    await setupPage.fill('#endLocal', '2026-11-20T17:00');
    await setupPage.fill('#location', 'Gezira Exhibition Grounds, Zamalek, Cairo');
    await setupPage.fill('#notes', 'Field radios, emergency response kits, and volunteer accreditation badges.');
    await setupPage.click('button[type="submit"]');
    await setupPage.waitForURL(/\/draft-events\/[0-9a-f-]+$/);
    const draftEventUrl = setupPage.url();
    const eventId = draftEventUrl.split('/').pop();
    console.log(`Seeded event ${eventId} for showcase`);

    // Appoint chief.one
    await setupPage.goto(`${baseUrl}/draft-events/${eventId}/chief-leader`, { waitUntil: 'networkidle' });
    await setupPage.locator('input[name="chiefLeaderCandidate"]').first().check();
    await setupPage.click('button:has-text("Appoint Chief Leader")');
    await setupPage.waitForTimeout(600);

    // Chief accepts appointment
    const chiefContext = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const chiefPage = await chiefContext.newPage();
    await signIn(chiefPage, 'chief.one');
    await chiefPage.goto(`${baseUrl}/my-appointments`, { waitUntil: 'networkidle' });
    const apptCard = chiefPage.locator('.appointment-card').first();
    if (await apptCard.isVisible({ timeout: 3000 })) {
      await apptCard.click();
      await chiefPage.waitForSelector('button:has-text("Accept appointment")');
      await chiefPage.click('button:has-text("Accept appointment")');
      await chiefPage.waitForTimeout(600);
    }

    // Admin publishes event
    await setupPage.goto(`${baseUrl}/draft-events/${eventId}`, { waitUntil: 'networkidle' });
    const publishBtn = setupPage.locator('button:has-text("Publish Event")');
    if (await publishBtn.isVisible({ timeout: 3000 })) {
      await publishBtn.click();
      await setupPage.click('button:has-text("Yes, Publish Event")');
      await setupPage.waitForTimeout(600);
    }

    // Leader one declares availability
    const leaderContext = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const leaderPage = await leaderContext.newPage();
    await signIn(leaderPage, 'leader.one');
    await leaderPage.goto(`${baseUrl}/events/${eventId}/availability`, { waitUntil: 'networkidle' });
    const availBtn = leaderPage.locator('button:has-text("Mark Available")');
    if (await availBtn.isVisible({ timeout: 3000 })) {
      await availBtn.click();
      await leaderPage.waitForTimeout(600);
    }

    // Chief selects leader.one for team
    await chiefPage.goto(`${baseUrl}/events/${eventId}/team-selection`, { waitUntil: 'networkidle' });
    const selectBtn = chiefPage.locator('.btn-select').first();
    if (await selectBtn.isVisible({ timeout: 3000 })) {
      await selectBtn.click();
      await chiefPage.waitForTimeout(600);
    }

    // Also declare availability for leader.two and leader.three
    await signOut(leaderPage);
    await signIn(leaderPage, 'leader.two');
    await leaderPage.goto(`${baseUrl}/events/${eventId}/availability`, { waitUntil: 'networkidle' });
    if (await leaderPage.locator('button:has-text("Mark Available")').isVisible({ timeout: 2000 })) {
      await leaderPage.locator('button:has-text("Mark Available")').click();
      await leaderPage.waitForTimeout(400);
    }

    await signOut(leaderPage);
    await signIn(leaderPage, 'leader.three');
    await leaderPage.goto(`${baseUrl}/events/${eventId}/availability`, { waitUntil: 'networkidle' });
    if (await leaderPage.locator('button:has-text("Mark Available")').isVisible({ timeout: 2000 })) {
      await leaderPage.locator('button:has-text("Mark Available")').click();
      await leaderPage.waitForTimeout(400);
    }

    // ==========================================
    // PHASE 3 — DESKTOP SCREENSHOTS (1440 × 900)
    // ==========================================
    console.log('\n--- Capturing Desktop Screenshots (1440 × 900) ---');
    const dContext = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const dPage = await dContext.newPage();

    // 01 Public Home
    await dPage.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '01-home-public.png');

    // 02 Sign In
    await dPage.goto(`${baseUrl}/sign-in`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '02-sign-in.png');

    // 03 Register Leader
    await dPage.goto(`${baseUrl}/register`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '03-register-leader.png');

    // 04 Admin Operational Dashboard
    await signIn(dPage, 'admin');
    await dPage.goto(`${baseUrl}/dashboard`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '04-admin-operational-dashboard.png');

    // 05 Admin Draft Events List
    await dPage.goto(`${baseUrl}/draft-events`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '05-admin-draft-events-list.png');

    // 06 Admin Draft Event Editor & Chief Leader Panel
    await dPage.goto(`${baseUrl}/draft-events/${eventId}`, { waitUntil: 'networkidle' });
    await captureFull(dPage, 'desktop', '06-admin-draft-event-edit-and-chief-leader.png');

    // 07 Admin Accounts Governance Matrix
    await dPage.goto(`${baseUrl}/admin/accounts`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '07-admin-accounts-governance.png');

    // 08 Admin Post-Event Feedback Review
    await dPage.goto(`${baseUrl}/admin/feedback`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '08-admin-feedback-review.png');

    // 09 Admin Incident Review Queue
    await dPage.goto(`${baseUrl}/admin/incidents`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '09-admin-incident-review.png');

    // Workflows (Desktop 1440 × 900)
    await dPage.goto(`${baseUrl}/events/${eventId}/reschedule`, { waitUntil: 'networkidle' });
    await capture(dPage, 'workflows', '01-workflow-event-reschedule-dialog.png');

    await dPage.goto(`${baseUrl}/events/${eventId}/complete`, { waitUntil: 'networkidle' });
    await capture(dPage, 'workflows', '02-workflow-event-completion-dialog.png');

    await dPage.goto(`${baseUrl}/events/${eventId}/cancel`, { waitUntil: 'networkidle' });
    await capture(dPage, 'workflows', '03-workflow-event-cancellation.png');

    // 10 Chief Leader Appointments
    await signOut(dPage);
    await signIn(dPage, 'chief.one');
    await dPage.goto(`${baseUrl}/my-appointments`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '10-chief-leader-appointments.png');

    // 11 Chief Leader Team Selection
    await dPage.goto(`${baseUrl}/events/${eventId}/team-selection`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '11-chief-leader-team-selection.png');

    // 12 Chief Leader Replacement Leader Assignment
    await dPage.goto(`${baseUrl}/events/${eventId}/replacement-assignment`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '12-chief-leader-replacement-assignment.png');

    // 13 Chief Leader Attendance Roll Sheet
    await dPage.goto(`${baseUrl}/events/${eventId}/attendance`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '13-chief-leader-attendance-roll-sheet.png');

    // Workflows: Feedback & Incidents
    await dPage.goto(`${baseUrl}/events/${eventId}/feedback`, { waitUntil: 'networkidle' });
    await capture(dPage, 'workflows', '04-workflow-post-event-feedback.png');

    await dPage.goto(`${baseUrl}/events/${eventId}/incidents`, { waitUntil: 'networkidle' });
    await capture(dPage, 'workflows', '05-workflow-event-incident-reporting.png');

    // 14 Volunteer Leader Availability
    await signOut(dPage);
    await signIn(dPage, 'leader.one');
    await dPage.goto(`${baseUrl}/events/${eventId}/availability`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '14-leader-availability-declaration.png');

    // 15 Volunteer Leader Active Placement & Cancellation Workspace
    await dPage.goto(`${baseUrl}/events/${eventId}/my-assignment`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '15-leader-my-assignment.png');

    // 16 Volunteer Leader Profile & Factual History
    await dPage.goto(`${baseUrl}/profile`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '16-leader-profile-history.png');

    // 17 In-App Notification Inbox
    await dPage.goto(`${baseUrl}/notifications`, { waitUntil: 'networkidle' });
    await capture(dPage, 'desktop', '17-leader-notifications-inbox.png');

    await dContext.close();

    // ========================================
    // PHASE 3 — MOBILE SCREENSHOTS (390 × 844)
    // ========================================
    console.log('\n--- Capturing Mobile Screenshots (390 × 844) ---');
    const mContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const mPage = await mContext.newPage();

    // 01 Mobile Home
    await mPage.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    await capture(mPage, 'mobile', '01-mobile-home.png');

    // 02 Mobile Admin Dashboard
    await signIn(mPage, 'admin');
    await mPage.goto(`${baseUrl}/dashboard`, { waitUntil: 'networkidle' });
    await capture(mPage, 'mobile', '02-mobile-admin-dashboard.png');

    // 03 Mobile Team Selection
    await signOut(mPage);
    await signIn(mPage, 'chief.one');
    await mPage.goto(`${baseUrl}/events/${eventId}/team-selection`, { waitUntil: 'networkidle' });
    await capture(mPage, 'mobile', '03-mobile-team-selection.png');

    // 04 Mobile Attendance Roll Sheet
    await mPage.goto(`${baseUrl}/events/${eventId}/attendance`, { waitUntil: 'networkidle' });
    await capture(mPage, 'mobile', '04-mobile-attendance-roll-sheet.png');

    // 05 Mobile Leader Availability
    await signOut(mPage);
    await signIn(mPage, 'leader.one');
    await mPage.goto(`${baseUrl}/events/${eventId}/availability`, { waitUntil: 'networkidle' });
    await capture(mPage, 'mobile', '05-mobile-leader-availability.png');

    // 06 Mobile Leader Profile & Factual History
    await mPage.goto(`${baseUrl}/profile`, { waitUntil: 'networkidle' });
    await capture(mPage, 'mobile', '06-mobile-leader-profile.png');

    await mContext.close();

    // Clean up
    await setupContext.close();
    await chiefContext.close();
    await leaderContext.close();

    console.log('\n[SUCCESS] All screenshots captured successfully.');
  } finally {
    await browser.close();
  }
}

main().catch(err => {
  console.error('[FATAL]', err);
  process.exit(1);
});
