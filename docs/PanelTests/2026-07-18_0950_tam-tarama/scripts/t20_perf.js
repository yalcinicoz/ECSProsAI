const { launch, record, login } = require('./lib');
const BASE = 'https://51.178.208.59';
(async () => {
  const { browser, page } = await launch();
  await login(page);
  const reqs = [];
  page.on('request', r => { if (r.url().includes('/api/')) reqs.push({ t: Date.now(), url: r.url().replace(BASE, '').slice(0, 90) }); });
  const t0 = Date.now();
  await page.goto(BASE + '/admin/catalog/attribute-types', { waitUntil: 'domcontentloaded' });
  // ilk tablo görünümü
  await page.waitForSelector('table tbody tr', { timeout: 30000 });
  const renderMs = Date.now() - t0;
  await page.waitForTimeout(18000);
  record('performans', 'Özellik Tipleri ilk tablo render', renderMs < 4000 ? 'PASS' : 'WARN', renderMs + 'ms');
  const timeline = reqs.map(r => `+${((r.t - t0) / 1000).toFixed(1)}s ${r.url}`);
  record('performans', 'API istek zaman çizelgesi (ilk 20 sn)', 'INFO', timeline.join(' || ').slice(0, 900));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
