const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';
const S = 'katalog';

(async () => {
  const { browser, page, errors } = await launch();
  await login(page);
  await page.goto(BASE + '/admin/catalog/products', { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('table tbody tr', { timeout: 20000 });

  const search = page.locator('input[placeholder*="rün"], input[placeholder*="kod"]').first();
  for (const term of ['Kaban', 'kaşe', 'Ful-7492', 'P-00000084', 'bluz']) {
    await search.fill('');
    await page.waitForTimeout(600);
    await search.fill(term);
    await page.waitForTimeout(2200);
    const rows = await page.locator('table tbody tr').count();
    const first = (await page.locator('table tbody tr').first().innerText().catch(() => '')).replace(/\s+/g, ' ').slice(0, 90);
    const notFound = /bulunamadı/i.test(first);
    record(S, `Arama "${term}"`, notFound ? 'FAIL' : 'PASS', `${notFound ? 0 : rows} sonuç | ilk: ${first}`);
  }
  const fS = await shot(page, 'katalog-04-arama-sonuclari');

  // Temiz liste, satır tıklama
  await search.fill('');
  await page.waitForTimeout(2000);
  await page.locator('table tbody tr').first().click();
  await page.waitForTimeout(3000);
  const f2 = await shot(page, 'katalog-05-urun-detay');
  record(S, 'Satır tıklama → ürün detayı', page.url().includes('/products/') ? 'PASS' : 'FAIL', 'URL: ' + page.url(), f2);

  // Sekmeleri dump et
  const tabs = await page.evaluate(() => [...document.querySelectorAll('.stab')].map(b => b.textContent.trim()));
  record(S, 'Ürün detay sekmeleri', tabs.length >= 5 ? 'PASS' : 'WARN', tabs.join(' | '));

  // Her sekmeye tıkla + screenshot
  for (let i = 0; i < tabs.length; i++) {
    try {
      await page.locator('.stab').nth(i).click();
      await page.waitForTimeout(1800);
      const f = await shot(page, `katalog-06-sekme-${i}-${tabs[i]}`);
      const formCount = await page.locator('input, select, textarea').count();
      record(S, `Sekme "${tabs[i]}"`, 'PASS', `${formCount} form alanı`, f);
    } catch (e) { record(S, `Sekme "${tabs[i]}"`, 'ERROR', e.message.slice(0, 120)); }
  }

  if (errors.length) record(S, 'Konsol/HTTP hataları (detay)', 'WARN', JSON.stringify(errors.slice(0, 5)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
