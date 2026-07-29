const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';

(async () => {
  const { browser, page, errors } = await launch();
  await login(page);

  // A) Varyant Ekle akışı (TEST ürününde)
  let S = 'katalog-varyant';
  try {
    await page.goto(BASE + '/admin/catalog/products/TEST-PANEL-001', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.stab', { timeout: 20000 });
    await page.locator('.stab').filter({ hasText: 'Varyant' }).first().click();
    await page.waitForTimeout(1200);
    await page.locator('button:has-text("Varyant Ekle")').first().click();
    await page.waitForTimeout(1500);
    const f = await shot(page, 'katalog-22-varyant-ekle-modal');
    const modalTxt = (await page.locator('div.fixed').last().innerText().catch(() => '')).replace(/\s+/g, ' ').slice(0, 400);
    record(S, 'Varyant Ekle modalı', modalTxt ? 'PASS' : 'WARN', modalTxt, f);
    // Eksen seçimleri var mı? select'leri dene
    const selects = await page.locator('div.fixed select').count();
    const modalInputs = await page.locator('div.fixed input').count();
    record(S, 'Varyant modal alanları', 'INFO', `select:${selects} input:${modalInputs}`);
    await page.keyboard.press('Escape');
    await page.waitForTimeout(500);
    const escWorked = !(await page.locator('div.fixed.inset-0').count());
    record(S, 'UX: varyant modal ESC', escWorked ? 'PASS' : 'WARN', escWorked ? 'ESC kapatıyor' : 'ESC kapatmıyor');
  } catch (e) { record(S, 'Varyant ekle akışı', 'ERROR', e.message.slice(0, 150)); }

  // B) Özellik Tipleri: satır tıklama → detay
  S = 'katalog-ozellik-tipleri';
  try {
    await page.goto(BASE + '/admin/catalog/attribute-types', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    const rowTxt = (await page.locator('table tbody tr').first().innerText()).replace(/\s+/g, ' ').slice(0, 80);
    await page.locator('table tbody tr').first().click();
    await page.waitForTimeout(2500);
    const f = await shot(page, 'katalog-23-ozellik-tipi-detay');
    record(S, 'Satır tıklama → detay', page.url().includes('attribute-types/') ? 'PASS' : 'FAIL', `İlk satır: ${rowTxt} → ${page.url()}`, f);
  } catch (e) { record(S, 'Özellik tipleri', 'ERROR', e.message.slice(0, 150)); }

  // C) Ürün Grupları: satır tıklama → detay
  S = 'katalog-urun-gruplari';
  try {
    await page.goto(BASE + '/admin/catalog/product-groups', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    await page.locator('table tbody tr').filter({ hasText: 'Bluz' }).first().click();
    await page.waitForTimeout(2500);
    const f = await shot(page, 'katalog-24-urun-grubu-detay');
    record(S, 'Grup detayı (Bluz)', page.url().includes('product-groups/') ? 'PASS' : 'FAIL', page.url(), f);
    const gTxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    const hasAxis = /eksen|varyant|dataType|veri tipi/i.test(gTxt);
    record(S, 'Grup şeması içeriği', hasAxis ? 'PASS' : 'WARN', (gTxt.match(/(Özellik|Şema|Eksen).{0,150}/) || ['içerik bulunamadı'])[0]);
  } catch (e) { record(S, 'Ürün grupları', 'ERROR', e.message.slice(0, 150)); }

  // D) Kanal Kategorileri: kanal seç
  S = 'katalog-kanal-kategorileri';
  try {
    await page.goto(BASE + '/admin/storefront/channel-categories', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);
    const sel = page.locator('select').first();
    if (await sel.count()) {
      const opts = await sel.locator('option').allTextContents();
      await sel.selectOption({ index: 1 });
      record(S, 'Kanal seçimi', 'PASS', 'Seçenekler: ' + opts.join(', ').slice(0, 120));
    } else {
      await page.locator('button:has-text("Kanal seçin")').first().click();
      await page.waitForTimeout(800);
      await page.locator('div.fixed button, [role="option"]').filter({ hasText: /mishar/i }).first().click().catch(async () => { await page.locator('text=/Mishar/i').first().click(); });
      record(S, 'Kanal seçimi (dropdown)', 'PASS', 'Mishar seçildi');
    }
    await page.waitForTimeout(2500);
    const f = await shot(page, 'katalog-25-kanal-kategorileri');
    const catTxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ').slice(0, 300);
    record(S, 'Kategori ağacı görünümü', 'INFO', catTxt.slice(100, 300), f);
  } catch (e) { record(S, 'Kanal kategorileri', 'ERROR', e.message.slice(0, 150)); }

  // E) Katalog Ayarları
  S = 'katalog-ayarlar';
  try {
    await page.goto(BASE + '/admin/catalog/settings', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    const f = await shot(page, 'katalog-26-katalog-ayarlari');
    const txt = (await page.locator('body').innerText()).replace(/\s+/g, ' ').slice(0, 350);
    record(S, 'Katalog ayarları sayfası', 'INFO', txt.slice(60, 350), f);
  } catch (e) { record(S, 'Katalog ayarları', 'ERROR', e.message.slice(0, 150)); }

  if (errors.length) record('katalog', 'HTTP/konsol hataları (tur)', 'WARN', JSON.stringify(errors.slice(0, 6)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
