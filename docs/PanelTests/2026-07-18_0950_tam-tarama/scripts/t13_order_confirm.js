const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';
const S = 'siparis-aksiyonlari';
(async () => {
  const { browser, page, errors } = await launch();
  await login(page);
  await page.goto(BASE + '/admin/orders', { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('table tbody tr', { timeout: 25000 });
  await page.locator('table tbody tr').filter({ hasText: 'Bekleyen' }).first().click();
  await page.waitForTimeout(2500);
  const orderNo = (await page.locator('h1, h2').first().innerText().catch(() => '')).trim();

  await page.locator('button:has-text("Onayla")').first().click();
  await page.waitForTimeout(1000);
  const sel = page.locator('div.fixed select').first();
  const opts = await sel.locator('option').allTextContents();
  record(S, 'Onay modalı depo seçenekleri', 'INFO', opts.join(', '));
  await sel.selectOption({ index: 1 });
  await page.waitForTimeout(500);
  await page.locator('div.fixed button:has-text("Onayla")').last().click();
  await page.waitForTimeout(3500);
  const f = await shot(page, 'siparis-09-onaylandi');
  const txt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
  const ok = /Onayland|Confirmed|İşleme Al/i.test(txt);
  record(S, `Sipariş onaylama (${orderNo})`, ok ? 'PASS' : 'WARN', (txt.match(/ORD-\S+ \S+/) || [''])[0] + ' | butonlar: ' + (txt.match(/İşleme Al|Kargoya Ver|İptal/g) || []).join(','), f);

  // Fatura oluştur
  const invBtn = page.locator('button:has-text("Fatura Oluştur")').first();
  if (await invBtn.count()) {
    await invBtn.click();
    await page.waitForTimeout(1500);
    if (await page.locator('div.fixed.inset-0').count()) {
      const fM = await shot(page, 'siparis-10-fatura-modal');
      const mtxt = (await page.locator('div.fixed').last().innerText()).replace(/\s+/g, ' ').slice(0, 200);
      record(S, 'Fatura modalı', 'INFO', mtxt, fM);
      const btn = page.locator('div.fixed button').filter({ hasText: /Oluştur|Kes|Onayla/ }).last();
      if (await btn.count() && !(await btn.isDisabled())) { await btn.click(); await page.waitForTimeout(3000); }
    }
    const f2 = await shot(page, 'siparis-11-fatura-sonrasi');
    const t2 = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    record(S, 'Fatura oluşturma', /Faturalar \([1-9]/.test(t2) ? 'PASS' : 'WARN', (t2.match(/Faturalar.{0,100}/) || [''])[0], f2);
  } else {
    record(S, 'Fatura oluşturma', 'WARN', 'Fatura Oluştur butonu bulunamadı (onay sonrası)');
  }
  if (errors.length) record(S, 'HTTP/konsol hataları', 'WARN', JSON.stringify(errors.slice(0, 6)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
