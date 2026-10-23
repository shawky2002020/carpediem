import { createRequire } from 'node:module';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
const { chromium } = require('../../tests/e2e/node_modules/playwright');

const repoRoot = path.resolve(__dirname, '../..');
const baseUrl = process.env.CARPEDIEM_BASE_URL ?? 'http://127.0.0.1:4200';
const password = process.env.CARPEDIEM_SEED_PASSWORD ?? 'Carpediem#2026SeedAdmin!';

const demoDir = path.join(repoRoot, 'docs/demo');
const framesDir = path.join(demoDir, 'temp-frames');

async function ensureDirs() {
  await fs.mkdir(demoDir, { recursive: true });
  await fs.mkdir(framesDir, { recursive: true });
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
    if (await signOutBtn.isVisible({ timeout: 1500 })) {
      await signOutBtn.click();
      await page.waitForTimeout(300);
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

async function setHud(page, roleTag, captionText) {
  await page.evaluate(
    ({ roleTag, captionText }) => {
      let hud = document.getElementById('cd-demo-hud');
      if (!hud) {
        hud = document.createElement('div');
        hud.id = 'cd-demo-hud';
        hud.style.cssText = `
          position: fixed;
          bottom: 28px;
          left: 50%;
          transform: translateX(-50%);
          z-index: 9999999;
          background: rgba(21, 40, 29, 0.94);
          backdrop-filter: blur(12px);
          -webkit-backdrop-filter: blur(12px);
          color: #F8F9F5;
          padding: 12px 24px;
          border-radius: 999px;
          border: 1px solid rgba(255, 255, 255, 0.18);
          box-shadow: 0 12px 36px rgba(0, 0, 0, 0.35);
          font-family: 'Hanken Grotesk', system-ui, sans-serif;
          display: flex;
          align-items: center;
          gap: 16px;
          max-width: 1200px;
          pointer-events: none;
          transition: all 0.2s ease;
        `;
        document.body.appendChild(hud);
      }
      hud.innerHTML = `
        <span style="background: #1D5041; border: 1px solid rgba(255,255,255,0.25); color: #F8F9F5; padding: 4px 14px; border-radius: 999px; font-size: 11px; font-weight: 700; letter-spacing: 0.06em; text-transform: uppercase;">
          ${roleTag}
        </span>
        <span style="font-size: 15px; font-weight: 500; letter-spacing: -0.01em;">
          ${captionText}
        </span>
      `;
    },
    { roleTag, captionText }
  );
}

let frameIndex = 0;
const playlist = [];

async function recordFrame(page, durationSeconds, roleTag, captionText) {
  if (roleTag && captionText) {
    await setHud(page, roleTag, captionText);
  }
  await page.waitForTimeout(100);
  const frameName = `frame_${String(frameIndex).padStart(4, '0')}.png`;
  const framePath = path.join(framesDir, frameName);
  await page.screenshot({ path: framePath, fullPage: false });
  playlist.push({ file: frameName, duration: durationSeconds });
  frameIndex++;
}

async function renderTitleSlide(page, htmlContent) {
  await page.setContent(`
    <!DOCTYPE html>
    <html>
      <head>
        <meta charset="utf-8">
        <style>
          @font-face {
            font-family: 'Hanken Grotesk';
            src: url('${baseUrl}/fonts/hanken-grotesk-latin-variable.woff2') format('woff2');
            font-weight: 400 700;
          }
          @font-face {
            font-family: 'Bricolage Grotesque';
            src: url('${baseUrl}/fonts/bricolage-grotesque-latin-600.woff2') format('woff2');
            font-weight: 600 700;
          }
          * { box-sizing: border-box; margin: 0; padding: 0; }
          body {
            width: 1920px;
            height: 1080px;
            background: #15281D;
            color: #F8F9F5;
            font-family: 'Hanken Grotesk', system-ui, sans-serif;
            display: flex;
            align-items: center;
            justify-content: center;
            overflow: hidden;
          }
        </style>
      </head>
      <body>
        ${htmlContent}
      </body>
    </html>
  `, { waitUntil: 'load' });
  await page.waitForTimeout(200);
}

async function main() {
  await ensureDirs();
  console.log('[1/4] Launching Chromium 1920x1080 for demo recording...');
  const browser = await chromium.launch();

  try {
    const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });

    // Find or create sample event
    await signIn(page, 'admin');
    await page.goto(`${baseUrl}/dashboard`, { waitUntil: 'networkidle' });
    const eventLink = page.locator('a[href*="/events/"]').first();
    let eventId = '';
    if (await eventLink.isVisible({ timeout: 2000 })) {
      const href = await eventLink.getAttribute('href');
      const match = href.match(/\/events\/([0-9a-f-]+)/);
      if (match) eventId = match[1];
    }
    if (!eventId) {
      await page.goto(`${baseUrl}/draft-events/new`, { waitUntil: 'networkidle' });
      await page.fill('#title', 'Cairo Community Gala & Logistics Summit');
      await page.fill('#startLocal', '2026-11-20T08:00');
      await page.fill('#endLocal', '2026-11-20T17:00');
      await page.fill('#location', 'Gezira Exhibition Grounds, Zamalek, Cairo');
      await page.fill('#notes', 'Field operations demo event.');
      await page.click('button[type="submit"]');
      await page.waitForURL(/\/draft-events\/[0-9a-f-]+$/);
      eventId = page.url().split('/').pop();
    }
    await signOut(page);

    console.log(`[2/4] Recording scenes with event ${eventId}...`);

    // ==========================================
    // SCENE 1 — INTRO TITLE CARD (10s)
    // ==========================================
    console.log('Recording Scene 1: Introduction...');
    await renderTitleSlide(page, `
      <div style="text-align: center; max-width: 1100px; padding: 60px;">
        <div style="display: inline-flex; align-items: center; gap: 12px; background: rgba(29,80,65,0.6); padding: 8px 24px; border-radius: 999px; border: 1px solid rgba(255,255,255,0.2); margin-bottom: 32px;">
          <span style="width: 12px; height: 12px; border-radius: 50%; background: #246A45;"></span>
          <span style="font-size: 16px; font-weight: 700; letter-spacing: 0.1em; text-transform: uppercase;">Carpediem Operations Platform</span>
        </div>
        <h1 style="font-family: 'Bricolage Grotesque', sans-serif; font-size: 82px; font-weight: 700; line-height: 1.05; letter-spacing: -0.02em; margin-bottom: 28px;">
          Structured Event Staffing &amp; Volunteer Leadership
        </h1>
        <p style="font-size: 26px; line-height: 1.5; color: #C8D2C9; max-width: 880px; margin: 0 auto 48px;">
          Coordinating Operations Administrators, Chief Leaders, and Volunteer Leaders through auditable, mobile-first staffing cycles.
        </p>
        <div style="display: flex; justify-content: center; gap: 20px;">
          <div style="background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.15); padding: 12px 28px; border-radius: 12px; font-size: 16px; font-weight: 600;">Angular 22 + TypeScript</div>
          <div style="background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.15); padding: 12px 28px; border-radius: 12px; font-size: 16px; font-weight: 600;">ASP.NET Core 10 / .NET 10</div>
          <div style="background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.15); padding: 12px 28px; border-radius: 12px; font-size: 16px; font-weight: 600;">SQL Server 2025</div>
        </div>
      </div>
    `);
    await recordFrame(page, 5.0);

    // Live public home
    await page.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    await recordFrame(page, 5.0, 'PUBLIC WORKSPACE', 'Carpediem replaces spreadsheets with structured, role-based volunteer coordination.');

    // ==========================================
    // SCENE 2 — PRODUCT OVERVIEW & AUTH (14s)
    // ==========================================
    console.log('Recording Scene 2: Product Overview & Authentication...');
    await page.evaluate(() => window.scrollTo({ top: 380, behavior: 'smooth' }));
    await recordFrame(page, 4.0, 'ROLE CAPABILITIES', 'Three distinct operational roles: Operations Administrators, Chief Leaders, and Volunteer Leaders.');

    await page.goto(`${baseUrl}/sign-in`, { waitUntil: 'networkidle' });
    await recordFrame(page, 3.0, 'AUTHENTICATION', 'Cookie-authenticated operational sign-in with full CSRF antiforgery protection.');

    await page.fill('#userName', 'admin');
    await page.fill('#password', password);
    await recordFrame(page, 3.0, 'AUTHENTICATION', 'Authenticating as Operations Administrator (admin)...');

    await page.click('button[type="submit"]');
    await page.waitForURL(`${baseUrl}/`, { timeout: 10000 });
    await recordFrame(page, 4.0, 'AUTHENTICATED', 'Signed in successfully. Primary navigation exposes role-authorized operational capabilities.');

    // ==========================================
    // SCENE 3 — ADMIN EXPERIENCE (36s)
    // ==========================================
    console.log('Recording Scene 3: Administrator Experience...');
    await page.goto(`${baseUrl}/dashboard`, { waitUntil: 'networkidle' });
    await recordFrame(page, 5.0, 'ADMIN · DASHBOARD', 'Operational Dashboard: Real-time event monitoring, active roster counters, and lifecycle status.');

    await page.goto(`${baseUrl}/draft-events`, { waitUntil: 'networkidle' });
    await recordFrame(page, 4.0, 'ADMIN · DRAFT EVENTS', 'Draft Events list: Author new events and oversee upcoming staffing lifecycles.');

    await page.goto(`${baseUrl}/draft-events/${eventId}`, { waitUntil: 'networkidle' });
    await recordFrame(page, 5.0, 'ADMIN · DRAFT EDITOR', 'Draft Event Editor: Africa/Cairo timezone scheduling, location metadata, and Chief Leader panel.');

    await page.goto(`${baseUrl}/draft-events/${eventId}/chief-leader`, { waitUntil: 'networkidle' });
    await recordFrame(page, 5.0, 'ADMIN · CHIEF APPOINTMENT', 'Chief Leader Appointment: Appoint field leaders from the designated candidate pool.');

    await page.goto(`${baseUrl}/events/${eventId}/reschedule`, { waitUntil: 'networkidle' });
    await recordFrame(page, 4.0, 'ADMIN · WORKFLOW', 'Event Reschedule: Modifying start/end times automatically resets the staffing cycle.');

    await page.goto(`${baseUrl}/events/${eventId}/complete`, { waitUntil: 'networkidle' });
    await recordFrame(page, 3.5, 'ADMIN · WORKFLOW', 'Event Completion: Formal closeout opens post-event leader feedback and incident reviews.');

    await page.goto(`${baseUrl}/admin/accounts`, { waitUntil: 'networkidle' });
    await recordFrame(page, 4.5, 'ADMIN · GOVERNANCE', 'Account Governance: Approve registrations and assign Leader, ChiefLeader, and Admin roles.');

    await page.goto(`${baseUrl}/admin/feedback`, { waitUntil: 'networkidle' });
    await recordFrame(page, 2.5, 'ADMIN · GOVERNANCE', 'Post-Event Feedback: Administrative review of submitted volunteer and event ratings.');

    await page.goto(`${baseUrl}/admin/incidents`, { waitUntil: 'networkidle' });
    await recordFrame(page, 2.5, 'ADMIN · SAFETY', 'Incident Review Queue: Inspect field incident reports and record official review notes.');

    // ==========================================
    // SCENE 4 — CHIEF LEADER EXPERIENCE (36s)
    // ==========================================
    console.log('Recording Scene 4: Chief Leader Experience...');
    await signOut(page);
    await signIn(page, 'chief.one');

    await page.goto(`${baseUrl}/my-appointments`, { waitUntil: 'networkidle' });
    await recordFrame(page, 5.0, 'CHIEF LEADER · APPOINTMENTS', 'My Appointments: Chief Leaders review assigned events and formally accept leadership.');

    await page.goto(`${baseUrl}/events/${eventId}/team-selection`, { waitUntil: 'networkidle' });
    await recordFrame(page, 6.0, 'CHIEF LEADER · TEAM SELECTION', 'Team Selection: Build active team roster from available volunteers, or draft from pool.');

    // View history link
    const historyLink = page.locator('a:has-text("View history")').first();
    if (await historyLink.isVisible({ timeout: 2000 })) {
      await historyLink.click();
      await page.waitForTimeout(500);
      await recordFrame(page, 5.0, 'CHIEF LEADER · AUDIT', 'Factual Participation History: Inspect verified attended events, no-shows, and cancellations.');
      await page.goto(`${baseUrl}/events/${eventId}/team-selection`, { waitUntil: 'networkidle' });
    }

    await page.goto(`${baseUrl}/events/${eventId}/replacement-assignment`, { waitUntil: 'networkidle' });
    await recordFrame(page, 6.0, 'CHIEF LEADER · REPLACEMENTS', 'Replacement Leader Assignment: Link vacated volunteer slots to eligible replacements.');

    await page.goto(`${baseUrl}/events/${eventId}/attendance`, { waitUntil: 'networkidle' });
    await recordFrame(page, 5.0, 'CHIEF LEADER · ON-SITE ROLL CALL', 'On-Site Attendance Roll Call: Field-ready summary bar tracking Attended, No Show, and Unrecorded.');

    // Toggle outcome button
    const attendBtn = page.locator('button.cd-toggle-btn--attended').first();
    if (await attendBtn.isVisible({ timeout: 2000 })) {
      await attendBtn.click();
      await page.waitForTimeout(300);
      await recordFrame(page, 4.0, 'CHIEF LEADER · ON-SITE ROLL CALL', 'Recording attendance outcomes with instant status updates and accessible announcements.');
    }

    await page.goto(`${baseUrl}/events/${eventId}/feedback`, { waitUntil: 'networkidle' });
    await recordFrame(page, 5.0, 'CHIEF LEADER · EVALUATION', 'Post-Event Rating: Submit 1-5 ratings and qualitative feedback for team leaders.');

    // ==========================================
    // SCENE 5 — LEADER EXPERIENCE (26s)
    // ==========================================
    console.log('Recording Scene 5: Leader Experience...');
    await signOut(page);
    await signIn(page, 'leader.one');

    await page.goto(`${baseUrl}/events/${eventId}/availability`, { waitUntil: 'networkidle' });
    await recordFrame(page, 6.0, 'VOLUNTEER LEADER · AVAILABILITY', 'Availability Declaration: Declare Available or Not Available during open staffing cycle.');

    await page.goto(`${baseUrl}/events/${eventId}/my-assignment`, { waitUntil: 'networkidle' });
    await recordFrame(page, 6.0, 'VOLUNTEER LEADER · ASSIGNMENT', 'My Event Assignment: View confirmed placement details and schedule information.');

    // Open cancellation modal
    const cancelTrigger = page.locator('button.btn-cancel-trigger, button:has-text("Cancel Assignment")').first();
    if (await cancelTrigger.isVisible({ timeout: 2000 })) {
      await cancelTrigger.click();
      await page.waitForTimeout(400);
      await recordFrame(page, 4.0, 'VOLUNTEER LEADER · CANCELLATION', 'Controlled Self-Cancellation: Requires mandatory justification reason before event start.');
      const dismissBtn = page.locator('button.btn-dismiss, button:has-text("Keep Assignment")').first();
      if (await dismissBtn.isVisible({ timeout: 1500 })) {
        await dismissBtn.click();
        await page.waitForTimeout(300);
      }
    }

    await page.goto(`${baseUrl}/profile`, { waitUntil: 'networkidle' });
    await recordFrame(page, 5.0, 'VOLUNTEER LEADER · PROFILE', 'Leader Profile: Factual participation counts and complete historical event log.');

    await page.goto(`${baseUrl}/notifications`, { waitUntil: 'networkidle' });
    await recordFrame(page, 5.0, 'VOLUNTEER LEADER · INBOX', 'Notification Inbox: In-app operational alerts for appointments, assignments, and schedule updates.');

    // ==========================================
    // SCENE 6 — MOBILE-FIRST SHOWCASE (14s)
    // ==========================================
    console.log('Recording Scene 6: Mobile-First Showcase...');
    const mobileScreens = [
      { file: '02-mobile-admin-dashboard.png', title: 'Operational Dashboard', caption: 'Single-column stacked cards with 44px minimum touch targets' },
      { file: '04-mobile-attendance-roll-sheet.png', title: 'On-Site Roll Call', caption: 'Optimized for one-handed outdoor field operations on mobile devices' },
      { file: '05-mobile-leader-availability.png', title: 'Availability Declaration', caption: 'Fast one-tap availability toggles with zero horizontal overflow' },
      { file: '06-mobile-leader-profile.png', title: 'Leader Profile & History', caption: 'Clean responsive metrics and readable participation history' },
    ];

    for (const ms of mobileScreens) {
      const imgPath = `${baseUrl}/../docs/screenshots/mobile/${ms.file}`.replace(/\\/g, '/');
      const base64Img = (await fs.readFile(path.join(repoRoot, 'docs/screenshots/mobile', ms.file))).toString('base64');
      await renderTitleSlide(page, `
        <div style="display: flex; align-items: center; justify-content: center; gap: 80px; width: 100%; height: 100%; padding: 60px 100px;">
          <div style="flex: 1; max-width: 600px;">
            <div style="display: inline-flex; align-items: center; gap: 8px; background: rgba(29,80,65,0.8); padding: 6px 18px; border-radius: 999px; margin-bottom: 24px;">
              <span style="font-size: 13px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase;">Mobile-First Field UX</span>
            </div>
            <h2 style="font-family: 'Bricolage Grotesque', sans-serif; font-size: 56px; font-weight: 700; line-height: 1.1; margin-bottom: 20px;">
              ${ms.title}
            </h2>
            <p style="font-size: 22px; line-height: 1.5; color: #C8D2C9; margin-bottom: 32px;">
              ${ms.caption}
            </p>
            <div style="background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.15); border-radius: 12px; padding: 18px 24px; font-size: 16px; color: #EAF2ED;">
              Viewport: <strong>390 &times; 844 px</strong> &bull; WCAG 2.2 AA Contrast &bull; 0 Overflows
            </div>
          </div>
          <div style="width: 390px; height: 844px; border: 12px solid #243E30; border-radius: 44px; overflow: hidden; box-shadow: 0 32px 64px rgba(0,0,0,0.6); background: #F8F9F5;">
            <img src="data:image/png;base64,${base64Img}" style="width: 100%; height: 100%; object-fit: cover; object-position: top;">
          </div>
        </div>
      `);
      await recordFrame(page, 3.5);
    }

    // ==========================================
    // SCENE 7 — CLOSING SUMMARY (9s)
    // ==========================================
    console.log('Recording Scene 7: Closing Summary...');
    await renderTitleSlide(page, `
      <div style="text-align: center; max-width: 1100px; padding: 60px;">
        <div style="display: inline-flex; align-items: center; gap: 10px; background: rgba(29,80,65,0.6); padding: 8px 22px; border-radius: 999px; border: 1px solid rgba(255,255,255,0.2); margin-bottom: 28px;">
          <span style="font-size: 14px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase;">Carpediem Platform</span>
        </div>
        <h2 style="font-family: 'Bricolage Grotesque', sans-serif; font-size: 72px; font-weight: 700; line-height: 1.1; margin-bottom: 24px;">
          Built for Operational Integrity
        </h2>
        <p style="font-size: 24px; line-height: 1.5; color: #C8D2C9; max-width: 820px; margin: 0 auto 44px;">
          All V1 operational slices verified with 182 SQL integration tests, 96 domain tests, 100 Angular tests, and end-to-end Playwright browser automation.
        </p>
        <div style="display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; max-width: 900px; margin: 0 auto;">
          <div style="background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.15); border-radius: 12px; padding: 20px;">
            <div style="font-size: 28px; font-weight: 700; color: #246A45; margin-bottom: 4px;">100%</div>
            <div style="font-size: 14px; color: #C8D2C9;">V1 Slices Delivered</div>
          </div>
          <div style="background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.15); border-radius: 12px; padding: 20px;">
            <div style="font-size: 28px; font-weight: 700; color: #246A45; margin-bottom: 4px;">278</div>
            <div style="font-size: 14px; color: #C8D2C9;">.NET Tests Passing</div>
          </div>
          <div style="background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.15); border-radius: 12px; padding: 20px;">
            <div style="font-size: 28px; font-weight: 700; color: #246A45; margin-bottom: 4px;">100</div>
            <div style="font-size: 14px; color: #C8D2C9;">Angular Unit Tests</div>
          </div>
          <div style="background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.15); border-radius: 12px; padding: 20px;">
            <div style="font-size: 28px; font-weight: 700; color: #246A45; margin-bottom: 4px;">0</div>
            <div style="font-size: 14px; color: #C8D2C9;">Axe Violations</div>
          </div>
        </div>
      </div>
    `);
    await recordFrame(page, 9.0);

    // ==========================================
    // THUMBNAIL GENERATION (1920 × 1080)
    // ==========================================
    console.log('[3/4] Generating high-resolution demo thumbnail...');
    const dashBase64 = (await fs.readFile(path.join(repoRoot, 'docs/screenshots/desktop/04-admin-operational-dashboard.png'))).toString('base64');
    await renderTitleSlide(page, `
      <div style="position: relative; width: 100%; height: 100%; padding: 80px 100px; display: flex; align-items: center; justify-content: space-between; gap: 60px;">
        <div style="max-width: 700px; z-index: 2;">
          <div style="display: inline-flex; align-items: center; gap: 10px; background: rgba(29,80,65,0.7); padding: 8px 20px; border-radius: 999px; margin-bottom: 24px;">
            <span style="width: 10px; height: 10px; border-radius: 50%; background: #246A45;"></span>
            <span style="font-size: 14px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase;">Carpediem Product Demonstration</span>
          </div>
          <h1 style="font-family: 'Bricolage Grotesque', sans-serif; font-size: 72px; font-weight: 700; line-height: 1.05; letter-spacing: -0.02em; margin-bottom: 24px;">
            Event Staffing &amp; Volunteer Leadership
          </h1>
          <p style="font-size: 22px; line-height: 1.5; color: #C8D2C9; margin-bottom: 36px;">
            End-to-end walkthrough across Administrator, Chief Leader, and Volunteer Leader workflows.
          </p>
          <div style="display: flex; gap: 14px;">
            <span style="background: rgba(255,255,255,0.08); border: 1px solid rgba(255,255,255,0.2); padding: 10px 20px; border-radius: 10px; font-size: 15px; font-weight: 600;">Full HD 1080p</span>
            <span style="background: rgba(255,255,255,0.08); border: 1px solid rgba(255,255,255,0.2); padding: 10px 20px; border-radius: 10px; font-size: 15px; font-weight: 600;">Complete V1 Lifecycle</span>
            <span style="background: rgba(255,255,255,0.08); border: 1px solid rgba(255,255,255,0.2); padding: 10px 20px; border-radius: 10px; font-size: 15px; font-weight: 600;">Mobile-First UX</span>
          </div>
        </div>
        <div style="position: relative; width: 920px; height: 600px; border-radius: 20px; overflow: hidden; box-shadow: 0 36px 80px rgba(0,0,0,0.6); border: 2px solid rgba(255,255,255,0.15);">
          <img src="data:image/png;base64,${dashBase64}" style="width: 100%; height: 100%; object-fit: cover; object-position: top;">
          <div style="position: absolute; inset: 0; background: rgba(21,40,29,0.3); display: flex; align-items: center; justify-content: center;">
            <div style="width: 96px; height: 96px; border-radius: 50%; background: #1D5041; border: 3px solid #F8F9F5; display: flex; align-items: center; justify-content: center; box-shadow: 0 12px 32px rgba(0,0,0,0.5);">
              <svg width="40" height="40" viewBox="0 0 24 24" fill="#F8F9F5" style="margin-left: 6px;">
                <polygon points="6 4 20 12 6 20 6 4"></polygon>
              </svg>
            </div>
          </div>
        </div>
      </div>
    `);
    const thumbPath = path.join(demoDir, 'demo-thumbnail.png');
    await page.screenshot({ path: thumbPath });
    console.log(`[OK] Generated thumbnail: ${thumbPath}`);

  } finally {
    await browser.close();
  }

  // ==========================================
  // FFMPEG VIDEO ENCODING
  // ==========================================
  console.log('[4/4] Encoding H.264 1080p 30fps MP4 with FFmpeg...');

  // Build ffconcat file
  let concatContent = 'ffconcat version 1.0\n';
  let totalDuration = 0;
  for (const item of playlist) {
    concatContent += `file '${item.file}'\n`;
    concatContent += `duration ${item.duration.toFixed(3)}\n`;
    totalDuration += item.duration;
  }
  // Repeat last frame to avoid truncation on last duration
  if (playlist.length > 0) {
    concatContent += `file '${playlist[playlist.length - 1].file}'\n`;
  }

  const concatPath = path.join(framesDir, 'concat.txt');
  await fs.writeFile(concatPath, concatContent, 'utf8');

  const mp4Output = path.join(demoDir, 'carpediem-product-demo.mp4');

  const ffmpegArgs = [
    '-y',
    '-f', 'concat',
    '-safe', '0',
    '-i', concatPath,
    '-vf', 'fps=30,format=yuv420p',
    '-c:v', 'libx264',
    '-preset', 'fast',
    '-crf', '18',
    '-movflags', '+faststart',
    mp4Output,
  ];

  console.log(`Running ffmpeg with ${playlist.length} sequence slides (total duration: ~${totalDuration.toFixed(1)}s)...`);
  await execFileAsync('ffmpeg', ffmpegArgs);

  console.log(`[SUCCESS] Demo video encoded successfully: ${mp4Output}`);

  // Cleanup temp frames directory to keep repository clean
  await fs.rm(framesDir, { recursive: true, force: true });
  console.log('Cleaned up temporary frames.');
}

main().catch(err => {
  console.error('[FATAL]', err);
  process.exit(1);
});
