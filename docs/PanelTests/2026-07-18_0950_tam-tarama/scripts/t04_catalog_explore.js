const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';
const S = 'katalog';

async function dumpForm(page) {
  return page.evaluate(() => {
    const fields = [];
    document.querySelectorAll('input, select, textarea').forEach(el => {
      let label = '';
      const id = el.id;
      if (id) { const l = document.querySelector(`label[for="${id}"]`); if (l) label = l.textContent.trim(); }
      if (!label) { const l = el.closest('label') || el.closest('div')?.querySelector('label'); if (l) label = l.textContent.trim(); }
      fields.push({ tag: el.tagName.toLowerCase(), type: el.type || '', placeholder: el.placeholder || '', label: label.slice(0, 50), required: el.required, value: (el.value || '').slice(0, 30) });
    });
    const buttons = [...document.querySelectorAll('button')].map(b => b.textContent.trim()).filter(t => t && t.length < 40);
    return { fields, buttons };
  });
}

(async () => {
  const { browser, page, errors } = await launch();
  await login(page);

  // 1) Ürün arama
  await page.goto(BASE + '/admin/catalog/products', { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('table tbody tr', { timeout: 20000 });
  const search = page.locator('input[placeholder*="rün"], input[placeholder*="kod"]').first();
  await search.fill('kaban');
  await page.waitForTimeout(2000);
  const rows1 = await page.locator('table tbody tr').count();
  const firstRowText = await page.locator('table tbody tr').first().innerText().catch(() => '');
  const f1 = await shot(page, 'katalog-01-urun-arama-kaban');
  record(S, 'Ürün arama ("kaban")', rows1 > 0 && /kaban/i.test(firstRowText) ? 'PASS' : 'WARN', `${rows1} satır, ilk satır: ${firstRowText.replace(/\s+/g,' ').slice(0,80)}`, f1);

  // 2) Satır tıklama → detay
  await page.locator('table tbody tr').first().click();
  await page.waitForTimeout(2500);
  const detailUrl = page.url();
  const f2 = await shot(page, 'katalog-02-urun-detay-acilis');
  record(S, 'Liste satırı tıklama → detay', detailUrl.includes('/products/') ? 'PASS' : 'FAIL', 'URL: ' + detailUrl, f2);

  // 3) Detay sekmeleri
  const tabs = await page.evaluate(() => [...document.querySelectorAll('.stab, [role="tab"], button')].map(b => b.textContent.trim()).filter(t => t && t.length < 30));
  record(S, 'Detay sekme/butonları', 'INFO', tabs.join(' | ').slice(0, 400));

  // 4) Yeni Ürün formu keşfi
  await page.goto(BASE + '/admin/catalog/products', { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('table tbody tr', { timeout: 20000 });
  await page.locator('button:has-text("Yeni Ürün"), a:has-text("Yeni Ürün")').first().click();
  await page.waitForTimeout(2500);
  const f3 = await shot(page, 'katalog-03-yeni-urun-formu');
  record(S, 'Yeni Ürün formu açılışı', 'INFO', 'URL: ' + page.url(), f3);
  const form = await dumpForm(page);
  console.log('FORM:', JSON.stringify(form, null, 1).slice(0, 3000));

  if (errors.length) record(S, 'Konsol/HTTP hataları', 'WARN', JSON.stringify(errors.slice(0, 5)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
