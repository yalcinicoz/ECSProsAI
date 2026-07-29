const { chromium } = require('playwright-core');
const fs = require('fs');
const path = require('path');

const RUN_DIR = path.resolve(__dirname, '../PanelTests/run-2026-07-18_tam-tarama');
const SHOT_DIR = path.join(RUN_DIR, 'screenshots');
const RESULTS = path.join(RUN_DIR, 'results.jsonl');
const BASE = 'https://51.178.208.59';

async function launch() {
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox', '--ignore-certificate-errors'] });
  const ctx = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 900 }, locale: 'tr-TR' });
  const page = await ctx.newPage();
  page.setDefaultTimeout(15000);
  // Console/network hatalarını topla
  const errors = [];
  page.on('console', m => { if (m.type() === 'error') errors.push({ kind: 'console', text: m.text().slice(0, 300), url: page.url() }); });
  page.on('response', r => { if (r.status() >= 400 && r.url().includes('/api/')) errors.push({ kind: 'http', status: r.status(), url: r.url().slice(0, 200), page: page.url() }); });
  return { browser, ctx, page, errors };
}

function record(suite, step, status, note, shot) {
  const row = { ts: new Date().toISOString(), suite, step, status, note: note || '', shot: shot || '' };
  fs.appendFileSync(RESULTS, JSON.stringify(row) + '\n');
  console.log(`[${status}] ${suite} :: ${step}${note ? ' — ' + note : ''}`);
}

async function shot(page, name) {
  const file = name.replace(/[^a-z0-9-_]/gi, '_') + '.png';
  await page.screenshot({ path: path.join(SHOT_DIR, file), fullPage: false });
  return file;
}

async function fullShot(page, name) {
  const file = name.replace(/[^a-z0-9-_]/gi, '_') + '.png';
  await page.screenshot({ path: path.join(SHOT_DIR, file), fullPage: true });
  return file;
}

async function login(page) {
  await page.goto(BASE + '/admin/login', { waitUntil: 'networkidle' });
  await page.locator('input[type="text"]').first().fill('admin');
  await page.locator('input[type="password"]').first().fill('Admin123!');
  await page.locator('button[type="submit"], button:has-text("Giriş")').first().click();
  await page.waitForURL(u => !u.toString().includes('/login'), { timeout: 15000 });
  await page.waitForLoadState('networkidle');
}

module.exports = { launch, record, shot, fullShot, login, BASE, RUN_DIR, SHOT_DIR };
