const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';
const S = 'katalog-urun-crud';

(async () => {
  const { browser, page, errors } = await launch();
  await login(page);
  await page.goto(BASE + '/admin/catalog/products/new', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2000);

  // 1) NEGATİF: boş formda buton devre dışı mı
  const disabled = await page.locator('button:has-text("Ürün Kartını Oluştur")').isDisabled();
  const f1 = await shot(page, 'katalog-10-yeni-urun-bos-form');
  record(S, 'NEGATİF: boş formda gönderim engeli', disabled ? 'PASS' : 'FAIL', disabled ? 'Buton disabled — boş kayıt mümkün değil (iyi UX)' : 'Buton AKTİF, boş form gönderilebilir olabilir!', f1);

  // 2) Ad + grup ile oluştur
  await page.locator('input[placeholder*="NK-AM270"]').fill('TEST-PANEL-001');
  await page.locator('input[placeholder*="Nike"]').fill('TEST Panel Testi Ürünü 2026-07-18');
  // Grup seçimi — buton "— Grup seçin —" bir dropdown
  await page.locator('button:has-text("Grup seçin")').click();
  await page.waitForTimeout(800);
  const f2 = await shot(page, 'katalog-11-grup-dropdown');
  // dropdown içinden Bluz'u seç
  const opt = page.locator('[role="option"], li, .dropdown-item, button').filter({ hasText: /^Bluz$/ }).first();
  await opt.click({ timeout: 5000 }).catch(async () => {
    await page.locator('text=Bluz').first().click();
  });
  await page.waitForTimeout(600);
  await page.locator('button:has-text("Ürün Kartını Oluştur")').click();
  await page.waitForTimeout(3500);
  const url = page.url();
  const f3 = await shot(page, 'katalog-12-urun-olusturuldu');
  const created = /\/products\/(?!new)[^/]+$/.test(url);
  record(S, 'TEST ürünü oluşturma (TEST-PANEL-001, grup: Bluz)', created ? 'PASS' : 'FAIL', 'URL: ' + url, f3);

  if (created) {
    // 3) Genel sekmesinde fiyat/açıklama doldur, kaydet
    try {
      await page.locator('label:has-text("Alış Fiyatı")').locator('..').locator('input').fill('100');
    } catch { await page.locator('input').nth(2).fill('100'); }
    const inputs = page.locator('input[type="text"], input[type="number"], input:not([type])');
    // Daha sağlam: label bazlı doldurma
    const fillByLabel = async (labelText, value) => {
      const ok = await page.evaluate(({ labelText, value }) => {
        const labels = [...document.querySelectorAll('label, div')].filter(e => e.childElementCount === 0 && e.textContent.trim().startsWith(labelText));
        for (const l of labels) {
          const cont = l.closest('div')?.parentElement || l.parentElement;
          const inp = cont?.querySelector('input, textarea');
          if (inp) {
            const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set || Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value')?.set;
            setter.call(inp, value);
            inp.dispatchEvent(new Event('input', { bubbles: true }));
            inp.dispatchEvent(new Event('change', { bubbles: true }));
            return true;
          }
        }
        return false;
      }, { labelText, value });
      return ok;
    };
    const r1 = await fillByLabel('Alış Fiyatı', '100');
    const r2 = await fillByLabel('Satış Fiyatı', '199.90');
    const r3 = await fillByLabel('Kısa Açıklama', 'Panel testi için oluşturulmuş üründür.');
    record(S, 'Fiyat/açıklama alanları dolduruldu', (r1 && r2) ? 'PASS' : 'WARN', `alış:${r1} satış:${r2} kısaAçıklama:${r3}`);
    await page.locator('button:has-text("Kaydet")').first().click();
    await page.waitForTimeout(3000);
    const f4 = await shot(page, 'katalog-13-urun-kaydet');
    const after = await page.locator('body').innerText();
    const saved = /kaydedildi|başarı|güncellendi/i.test(after) && !errors.some(e => e.kind === 'http');
    record(S, 'Kaydet + geri bildirim', saved ? 'PASS' : 'WARN', errors.length ? 'HTTP/konsol: ' + JSON.stringify(errors.slice(0,3)) : (saved ? 'Başarı bildirimi görüldü' : 'Başarı bildirimi tespit edilemedi'), f4);

    // 4) Kalıcılık: sayfayı yenile, değerler duruyor mu
    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(3000);
    const vals = await page.evaluate(() => [...document.querySelectorAll('input, textarea')].map(i => i.value).filter(v => v));
    const persisted = vals.some(v => v.includes('199')) && vals.some(v => v === '100');
    const f5 = await shot(page, 'katalog-14-urun-kalicilik');
    record(S, 'Yenileme sonrası kalıcılık (fiyatlar)', persisted ? 'PASS' : 'FAIL', 'Dolu alanlar: ' + JSON.stringify(vals.slice(0, 10)), f5);
  }
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
