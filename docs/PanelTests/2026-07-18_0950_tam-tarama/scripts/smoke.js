const { chromium } = require('playwright-core');
(async () => {
  const browser = await chromium.launch({ headless: true, args: ['--no-sandbox', '--ignore-certificate-errors'] });
  const ctx = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 900 }, locale: 'tr-TR' });
  const page = await ctx.newPage();
  await page.goto('https://51.178.208.59/admin/', { waitUntil: 'networkidle', timeout: 30000 });
  console.log('URL:', page.url());
  console.log('TITLE:', await page.title());
  await page.screenshot({ path: 'smoke-login.png' });
  await browser.close();
})().catch(e => { console.error('FAIL:', e.message); process.exit(1); });
