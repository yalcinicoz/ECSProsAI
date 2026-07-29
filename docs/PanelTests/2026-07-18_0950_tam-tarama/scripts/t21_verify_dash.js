const { chromium } = require('playwright-core');
(async () => {
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] });
  const page = await (await browser.newContext({ viewport: { width: 1366, height: 950 } })).newPage();
  const errs = [];
  page.on('console', m => { if (m.type() === 'error') errs.push(m.text().slice(0, 150)); });
  await page.goto('http://localhost:8090/', { waitUntil: 'networkidle' });
  await page.waitForTimeout(1200);
  await page.screenshot({ path: 'dash-check.png', fullPage: true });
  await page.goto('http://localhost:8090/2026-07-18_0950_tam-tarama/report.html', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2500);
  await page.screenshot({ path: 'report-check.png' });
  console.log('console errors:', JSON.stringify(errs));
  await browser.close();
})().catch(e => { console.error('FATAL:', e.message); process.exit(1); });
