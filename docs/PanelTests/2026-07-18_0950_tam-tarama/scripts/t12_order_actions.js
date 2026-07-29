const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';

(async () => {
  const { browser, page, errors } = await launch();
  await login(page);

  // A) Bekleyen siparişi ONAYLA (test verisi — stok rezervasyonu tetikler)
  let S = 'siparis-aksiyonlari';
  try {
    await page.goto(BASE + '/admin/orders', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    await page.locator('table tbody tr').filter({ hasText: 'Bekleyen' }).first().click();
    await page.waitForTimeout(2500);
    const orderUrl = page.url();
    await page.locator('button:has-text("Onayla")').first().click();
    await page.waitForTimeout(1200);
    // olası onay modalı
    const modal = page.locator('div.fixed').last();
    if (await page.locator('div.fixed.inset-0').count()) {
      const fM = await shot(page, 'siparis-05-onay-modali');
      await modal.locator('button').filter({ hasText: /Onayla|Evet/ }).last().click().catch(() => {});
    }
    await page.waitForTimeout(3000);
    const f = await shot(page, 'siparis-06-onaylandi');
    const txt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    const confirmed = /Onaylandı|confirmed|Onaylı/i.test(txt);
    record(S, 'Sipariş onaylama', confirmed ? 'PASS' : 'WARN', confirmed ? 'Durum onaylandı (stok rezervasyonu tetiklenmiş olmalı)' : 'Durum metni doğrulanamadı: ' + txt.slice(100, 260), f);

    // B) Fatura oluştur
    await page.locator('button:has-text("Fatura Oluştur")').first().click();
    await page.waitForTimeout(1500);
    if (await page.locator('div.fixed.inset-0').count()) {
      const fM = await shot(page, 'siparis-07-fatura-modal');
      await page.locator('div.fixed button').filter({ hasText: /Oluştur|Kes/ }).last().click().catch(() => {});
    }
    await page.waitForTimeout(3000);
    const f2 = await shot(page, 'siparis-08-fatura-sonrasi');
    const txt2 = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    record(S, 'Fatura oluşturma', /INV|Fatura/i.test(txt2) ? 'INFO' : 'WARN', (txt2.match(/Fatura.{0,120}/) || [''])[0], f2);
  } catch (e) { record(S, 'Sipariş aksiyonları', 'ERROR', e.message.slice(0, 150)); }

  // C) Kampanyalar
  S = 'kampanyalar';
  try {
    await page.goto(BASE + '/admin/promotion/campaigns', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    const first = (await page.locator('table tbody tr').first().innerText()).replace(/\s+/g, ' ').slice(0, 100);
    const f = await shot(page, 'promo-01-kampanyalar');
    record(S, 'Kampanya listesi', 'PASS', 'ilk: ' + first, f);
    // Yeni kampanya butonu var mı, form alanları neler
    const newBtn = page.locator('button:has-text("Yeni"), a:has-text("Yeni")').first();
    if (await newBtn.count()) {
      await newBtn.click();
      await page.waitForTimeout(2000);
      const f2 = await shot(page, 'promo-02-yeni-kampanya-formu');
      const fields = await page.evaluate(() => [...document.querySelectorAll('label')].map(l => l.textContent.trim()).filter(t => t).slice(0, 25));
      record(S, 'Yeni kampanya formu', 'INFO', fields.join(' | ').slice(0, 400), f2);
      // kapat/geri — kaydetmeden çık
      await page.goBack().catch(() => {});
    }
  } catch (e) { record(S, 'Kampanyalar', 'ERROR', e.message.slice(0, 150)); }

  // D) Kuponlar
  S = 'kuponlar';
  try {
    await page.goto(BASE + '/admin/promotion/coupons', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    const first = (await page.locator('table tbody tr').first().innerText()).replace(/\s+/g, ' ').slice(0, 110);
    const f = await shot(page, 'promo-03-kuponlar');
    record(S, 'Kupon listesi', 'PASS', 'ilk: ' + first, f);
  } catch (e) { record(S, 'Kuponlar', 'ERROR', e.message.slice(0, 150)); }

  // E) CMS sayfaları
  S = 'cms';
  try {
    await page.goto(BASE + '/admin/cms/pages', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    const rows = await page.locator('table tbody tr').count();
    await page.locator('table tbody tr').first().click();
    await page.waitForTimeout(2500);
    const f = await shot(page, 'cms-01-sayfa-detay');
    record(S, `CMS sayfa listesi (${rows}) + satır tıklama`, /pages\/.+/.test(page.url()) ? 'PASS' : 'WARN', page.url(), f);
    const fields = await page.evaluate(() => [...document.querySelectorAll('label')].map(l => l.textContent.trim()).filter(t => t).slice(0, 20));
    record(S, 'CMS sayfa formu alanları', 'INFO', fields.join(' | ').slice(0, 350));
  } catch (e) { record(S, 'CMS', 'ERROR', e.message.slice(0, 150)); }

  if (errors.length) record('siparis-aksiyonlari', 'HTTP/konsol hataları (tur)', 'WARN', JSON.stringify(errors.slice(0, 8)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
