const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';
const S = 'katalog-urun-crud';
(async () => {
  const { browser, page, errors } = await launch();
  await login(page);
  await page.goto(BASE + '/admin/catalog/products/TEST-PANEL-001', { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('.stab', { timeout: 20000 });
  await page.waitForTimeout(1200);

  await page.locator('button:has-text("Satışa Aç")').first().click();
  await page.waitForTimeout(1000);
  // Onay modalındaki yeşil "Satışa Aç" butonu
  await page.locator('div.fixed button:has-text("Satışa Aç")').last().click();
  await page.waitForTimeout(2500);
  const f = await shot(page, 'katalog-20-satisa-ac-onay-sonrasi');
  const body = await page.locator('body').innerText();
  const isOpen = body.includes('Satışta');
  record(S, '"Satışa Aç" (0 varyantlı ürün!)', isOpen ? 'PASS' : 'WARN', isOpen ? 'Durum "Satışta" — 0 varyant/0 stokta uyarı YOK (bulgu)' : 'Değişmedi', f);

  await page.locator('.stab').filter({ hasText: 'Varyant' }).first().click();
  await page.waitForTimeout(1500);
  const f2 = await shot(page, 'katalog-19-test-urun-varyantlar');
  const vTxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
  record(S, 'Varyant sekmesi (0 varyant)', 'INFO', (vTxt.match(/Varyantlar.{0,250}/) || [''])[0], f2);

  // Özellikler sekmesi
  await page.locator('.stab').filter({ hasText: 'Özellikler' }).first().click();
  await page.waitForTimeout(1500);
  const f3 = await shot(page, 'katalog-21-test-urun-ozellikler');
  record(S, 'Özellikler sekmesi görünümü', 'INFO', '', f3);

  if (errors.length) record(S, 'HTTP/konsol hataları', 'WARN', JSON.stringify(errors.slice(0, 5)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
