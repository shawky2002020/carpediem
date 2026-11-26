import { createRequire } from 'node:module';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { eventId, wireFixtures } from './visual-fixtures.mjs';

const require = createRequire(import.meta.url);
const { chromium, expect } = require('../../tests/e2e/node_modules/@playwright/test');
const base = process.env.CARPEDIEM_BASE_URL ?? 'http://127.0.0.1:4216';
const folder = fileURLToPath(new URL('../../docs/ux/excellence/recordings/', import.meta.url));
await mkdir(folder, { recursive:true });
const browser = await chromium.launch();
const results = [];

// The wait between actions is playback pacing only. Application timers are untouched.
async function readFrame(page) { await page.waitForTimeout(400); }
async function capture(name, role, width, journey) {
  const viewport = { width, height:width < 600 ? 844 : 900 };
  const context = await browser.newContext({ viewport, recordVideo:{ dir:folder, size:viewport } });
  const page = await context.newPage();
  await wireFixtures(page, role);
  await page.addInitScript(() => {
    const samples = { shifts:0, largestPaint:0, frames:[], longTasks:[] };
    window.fieldMetrics = samples;
    for (const [type, accept] of [
      ['layout-shift', e => { if (!e.hadRecentInput) samples.shifts += e.value; }],
      ['largest-contentful-paint', e => samples.largestPaint = e.startTime],
      ['longtask', e => samples.longTasks.push(Math.round(e.duration))],
    ]) {
      if (PerformanceObserver.supportedEntryTypes.includes(type)) new PerformanceObserver(list => list.getEntries().forEach(accept)).observe({type, buffered:true});
    }
    let previous;
    function tick(t) {
      if (previous !== undefined) samples.frames.push(Math.round((t - previous)*10)/10);
      previous = t;
      requestAnimationFrame(tick);
    }
    requestAnimationFrame(tick);
  });
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  let video;
  try {
    await journey(page);
    await readFrame(page);
    if (await page.locator('vite-error-overlay').count()) throw new Error('Angular compilation overlay');
    const metric = await page.evaluate(() => {
      const { shifts, largestPaint, frames, longTasks } = window.fieldMetrics;
      frames.sort((a,b) => a-b);
      return { cls:+shifts.toFixed(4), lcpMs:Math.round(largestPaint), frameSamples:frames.length,
        medianFrameMs:frames[Math.floor(frames.length/2)], p95FrameMs:frames[Math.floor(frames.length*.95)],
        intervalsOver50Ms:frames.filter(n=>n>50).length, longTasksMs:longTasks };
    });
    results.push({name, viewport, ...metric, errors});
    if (errors.length) throw new Error(errors.join('\n'));
    video = page.video();
  } finally { await context.close(); }
  if (video) { await video.saveAs(path.join(folder,name+'.webm')); await video.delete(); }
}

try {
  await capture('mobile-navigation', 'Admin', 390, async page => {
    await page.goto(base+'/dashboard');
    await page.getByRole('heading', {name:'Operational dashboard'}).waitFor();
    await page.getByRole('button', {name:/Needs attention/}).click();
    await readFrame(page);
    await page.getByRole('button',{name:'Menu'}).click();
    await readFrame(page);
    await page.getByRole('link',{name:'Accounts',exact:true}).click();
    await page.getByRole('button',{name:'Deactivate',exact:true}).click();
    await expect(page.getByRole('button',{name:'Keep account active'})).toBeFocused();
    await readFrame(page);
    await page.keyboard.press('Escape');
    await expect(page.getByRole('button',{name:'Deactivate',exact:true})).toBeFocused();
  });
  await capture('draft-validation','Admin',1280,async page => {
    await page.goto(base+'/draft-events');
    await page.getByRole('link',{name:'Create Event'}).first().click();
    await page.getByRole('button',{name:/create draft/i}).click();
    await expect(page.getByRole('alert').first()).toBeVisible();
    await readFrame(page);
    await page.getByLabel(/^Title/).fill('Orchard check-in');
    await page.getByLabel(/^Location/).fill('Mango Tree Campsite');
  });
  await capture('attendance-finalization','ChiefLeader',390,async page => {
    let saved;
    let beginWrite;
    const started = new Promise(resolve => beginWrite=resolve);
    let finishWrite;
    const release = new Promise(resolve => finishWrite=resolve);
    await page.route('**/api/events/*/attendance',async route=>{
      if (route.request().method()==='POST') {
        saved = route.request().postDataJSON().entries;
        beginWrite();
        await release;
        return route.fulfill({json:{eventId}});
      }
      if (!saved) return route.fallback();
      const entries=saved.map((item,i)=>({...item, volunteerName:['Mariam Hassan','Youssef Ali','Nour Samir'][i],
        attendanceOutcomeId:item.teamAssignmentId, finalizedAt:'2026-12-12T14:00:00Z',version:'AAAAAAA='}));
      return route.fulfill({json:{eventId,eventTitle:'Winter camp orientation',location:'Mango Tree Campsite',
        startDateTime:'2026-12-12T07:00:00Z',endDateTime:'2026-12-12T13:00:00Z',entries}});
    });
    await page.goto(base+'/events/'+eventId+'/attendance');
    await page.getByRole('button',{name:'Mark all volunteers as attended'}).click();
    await readFrame(page);
    await page.getByRole('button',{name:'Finalize attendance roll call'}).click();
    await started;
    await readFrame(page);
    finishWrite();
    await expect(page.getByText('Attendance finalized successfully!')).toBeVisible();
  });
  await writeFile(path.join(folder,'measurements.json'), JSON.stringify(results,null,2)+'\n');
} finally { await browser.close(); }
