const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';

(async () => {
  const { browser, page, errors } = await launch();
  await login(page);

  // A) Vitrin önizleme
  let S = 'vitrin';
  try {
    await page.goto(BASE + '/admin/storefront/pages', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);
    await page.locator('button:has-text("Platform seç")').click();
    await page.waitForTimeout(800);
    await page.locator('li,button,div').filter({ hasText: /^mishar \(/ }).last().click();
    await page.waitForTimeout(3000);
    await page.locator('button:has-text("Önizleme")').first().click();
    await page.waitForTimeout(4000);
    const f = await shot(page, 'vitrin-06-onizleme');
    const ptxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    record(S, 'Önizleme modalı', /önizleme|preview|görünür/i.test(ptxt) ? 'PASS' : 'INFO', ptxt.slice(0, 250), f);
    await page.keyboard.press('Escape');
  } catch (e) { record(S, 'Önizleme', 'ERROR', e.message.slice(0, 150)); }

  // B) Depolar + detay
  S = 'stok';
  try {
    await page.goto(BASE + '/admin/inventory/warehouses', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    const rows = await page.locator('table tbody tr').count();
    const listTxt = (await page.locator('table').innerText()).replace(/\s+/g, ' ').slice(0, 350);
    const f = await shot(page, 'stok-01-depolar');
    record(S, `Depo listesi (${rows} satır)`, 'PASS', listTxt, f);
    await page.locator('table tbody tr').first().click();
    await page.waitForTimeout(2500);
    const f2 = await shot(page, 'stok-02-depo-detay');
    record(S, 'Depo detayı (kısım/birim görünümü?)', 'INFO', page.url() + ' | ' + (await page.locator('body').innerText()).replace(/\s+/g, ' ').slice(60, 350), f2);
  } catch (e) { record(S, 'Depolar', 'ERROR', e.message.slice(0, 150)); }

  // C) Stok listesi + arama
  try {
    await page.goto(BASE + '/admin/inventory/stocks', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    const inp = page.locator('input').first();
    if (await inp.count()) { await inp.fill('P-00000084'); await page.waitForTimeout(2500); }
    const f = await shot(page, 'stok-03-stok-arama');
    const sTxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    record(S, 'Stok arama (P-00000084)', /P-00000084/.test(sTxt) ? 'PASS' : 'WARN', sTxt.slice(100, 400), f);
    await page.goto(BASE + '/admin/inventory/transfers', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);
    const f2 = await shot(page, 'stok-04-transferler');
    record(S, 'Transferler sayfası', 'INFO', (await page.locator('body').innerText()).replace(/\s+/g, ' ').slice(60, 300), f2);
  } catch (e) { record(S, 'Stok', 'ERROR', e.message.slice(0, 150)); }

  // D) Ayarlar: kanallar detay
  S = 'ayarlar';
  try {
    await page.goto(BASE + '/admin/settings/channels', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    await page.locator('text=Mishar Web Sitesi').first().click();
    await page.waitForTimeout(2500);
    const f = await shot(page, 'ayarlar-01-kanal-detay');
    const kTxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    record(S, 'Kanal detayı (Mishar)', /showOutOfStock|Stok|stoğu/i.test(kTxt) ? 'PASS' : 'INFO', kTxt.slice(80, 450), f);
  } catch (e) { record(S, 'Kanallar', 'ERROR', e.message.slice(0, 150)); }

  // E) Firmalar detay (entegrasyon formu — şema tabanlı)
  try {
    await page.goto(BASE + '/admin/settings/firms', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    await page.locator('table tbody tr').filter({ hasText: /MİŞAROĞLU/i }).first().click();
    await page.waitForTimeout(2500);
    const f = await shot(page, 'ayarlar-02-firma-detay');
    const fTxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    record(S, 'Firma detayı', /Entegrasyon|SMTP|Servis/i.test(fTxt) ? 'PASS' : 'INFO', fTxt.slice(80, 400), f);
  } catch (e) { record(S, 'Firmalar', 'ERROR', e.message.slice(0, 150)); }

  // F) Servis kataloğu + arayüz çevirileri + migration (görüntüleme)
  try {
    await page.goto(BASE + '/admin/settings/integration-services', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    const f = await shot(page, 'ayarlar-03-servis-katalogu');
    record(S, 'Servis kataloğu', 'PASS', (await page.locator('table').innerText()).replace(/\s+/g, ' ').slice(0, 250), f);
    await page.goto(BASE + '/admin/settings/translations', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    const f2 = await shot(page, 'ayarlar-04-ceviriler');
    record(S, 'Arayüz çevirileri', 'INFO', (await page.locator('body').innerText()).replace(/\s+/g, ' ').slice(60, 280), f2);
    await page.goto(BASE + '/admin/settings/migration', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);
    const f3 = await shot(page, 'ayarlar-05-migration');
    record(S, 'Migration ekranı (yalnız görüntüleme)', 'INFO', (await page.locator('body').innerText()).replace(/\s+/g, ' ').slice(60, 300), f3);
  } catch (e) { record(S, 'Ayarlar geri kalanı', 'ERROR', e.message.slice(0, 150)); }

  // G) Cari kartlar
  S = 'cari';
  try {
    await page.goto(BASE + '/admin/accounts', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    const f = await shot(page, 'cari-01-liste');
    record(S, 'Cari kartlar listesi', 'PASS', (await page.locator('table').innerText()).replace(/\s+/g, ' ').slice(0, 250), f);
  } catch (e) { record(S, 'Cari', 'ERROR', e.message.slice(0, 150)); }

  if (errors.length) record('ayarlar', 'HTTP/konsol hataları (tur)', 'WARN', JSON.stringify(errors.slice(0, 8)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
