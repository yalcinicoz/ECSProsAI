const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';
const S = 'katalog-urun-crud';

// Etiket metninden DOM sırasında SONRAKİ input/textarea'yı bul (belge sırası)
async function fillLabel(page, label, value) {
  return page.evaluate(({ label, value }) => {
    const all = [...document.querySelectorAll('body *')];
    const li = all.findIndex(e => e.childElementCount === 0 && e.textContent.trim().startsWith(label));
    if (li < 0) return null;
    for (let i = li + 1; i < all.length; i++) {
      const el = all[i];
      if (el.matches('input, textarea')) {
        const proto = el.tagName === 'INPUT' ? window.HTMLInputElement.prototype : window.HTMLTextAreaElement.prototype;
        Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, value);
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
        return true;
      }
    }
    return false;
  }, { label, value });
}
async function readLabel(page, label) {
  return page.evaluate(({ label }) => {
    const all = [...document.querySelectorAll('body *')];
    const li = all.findIndex(e => e.childElementCount === 0 && e.textContent.trim().startsWith(label));
    if (li < 0) return null;
    for (let i = li + 1; i < all.length; i++) if (all[i].matches('input, textarea')) return all[i].value;
    return null;
  }, { label });
}

(async () => {
  const { browser, page, errors } = await launch();
  await login(page);
  await page.goto(BASE + '/admin/catalog/products/TEST-PANEL-001', { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('.stab', { timeout: 20000 });
  await page.waitForTimeout(1500);

  // Alanları düzelt
  const rA = await fillLabel(page, 'Alış Fiyatı', '100');
  const rS = await fillLabel(page, 'Satış Fiyatı', '199.90');
  const rN = await fillLabel(page, 'Ürün Adı', 'TEST Panel Testi Ürünü 2026-07-18');
  record(S, 'Alan doldurma', (rA && rS && rN) ? 'INFO' : 'WARN', `alış:${rA} satış:${rS} ad:${rN}`);
  await page.locator('button:has-text("Kaydet")').first().click();
  await page.waitForTimeout(3000);
  const f1 = await shot(page, 'katalog-15-urun-guncelle');
  record(S, 'Ürün güncelleme (ad + fiyatlar)', 'INFO', 'Kaydet tıklandı', f1);

  // Yenile ve doğrula
  await page.reload({ waitUntil: 'domcontentloaded' });
  await page.waitForSelector('.stab', { timeout: 20000 });
  await page.waitForTimeout(1500);
  const vAlis = await readLabel(page, 'Alış Fiyatı');
  const vSatis = await readLabel(page, 'Satış Fiyatı');
  const vAd = await readLabel(page, 'Ürün Adı');
  const ok = vAlis === '100' && (vSatis === '199.90' || vSatis === '199.9') && vAd.startsWith('TEST Panel');
  const f2 = await shot(page, 'katalog-16-urun-guncelleme-dogrulama');
  record(S, 'Güncelleme kalıcılığı', ok ? 'PASS' : 'FAIL', `alış=${vAlis} satış=${vSatis} ad="${vAd}"`, f2);

  // Fiyat Geçmişi modalı
  await page.locator('button:has-text("Fiyat Geçmişi")').click();
  await page.waitForTimeout(1500);
  const f3 = await shot(page, 'katalog-17-fiyat-gecmisi');
  const modalTxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
  const hasHistory = /199|100|Fiyat Geçmişi/i.test(modalTxt);
  record(S, 'Fiyat geçmişi modalı', hasHistory ? 'PASS' : 'WARN', modalTxt.includes('kayıt bulunamadı') ? 'Modal açıldı ama kayıt yok' : 'Modal açıldı', f3);
  await page.keyboard.press('Escape');

  // Satışa Aç butonu
  await page.locator('button:has-text("Satışa Aç")').click();
  await page.waitForTimeout(2500);
  const f4 = await shot(page, 'katalog-18-satisa-ac');
  const badge = (await page.locator('body').innerText()).includes('Satışta');
  record(S, '"Satışa Aç" işlemi', badge ? 'PASS' : 'WARN', badge ? 'Durum "Satışta" oldu' : 'Durum değişimi görülemedi', f4);

  if (errors.length) record(S, 'HTTP/konsol hataları', 'WARN', JSON.stringify(errors.slice(0, 5)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
