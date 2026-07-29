const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';

(async () => {
  const { browser, page, errors } = await launch();
  await login(page);

  // A) Fatura Serisi tanımla
  let S = 'faturalar';
  try {
    await page.goto(BASE + '/admin/orders/invoices', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    const seriBtn = page.locator('button:has-text("Seri"), a:has-text("Seri")').first();
    if (await seriBtn.count()) {
      await seriBtn.click();
      await page.waitForTimeout(1500);
      const f = await shot(page, 'fatura-01-seriler');
      const mtxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
      record(S, 'Fatura Serileri ekranı', 'PASS', (mtxt.match(/Seri.{0,150}/) || [''])[0], f);
      // Yeni seri ekle: alanları doldur
      const inputs = page.locator('div.fixed input, form input');
      const n = await inputs.count();
      record(S, 'Seri formu alan sayısı', 'INFO', String(n));
      if (n >= 1) {
        // kod/önek/isim alanlarını sırayla doldur
        for (let i = 0; i < Math.min(n, 4); i++) {
          const ph = await inputs.nth(i).getAttribute('placeholder').catch(() => '');
          const type = await inputs.nth(i).getAttribute('type').catch(() => '');
          if (type === 'checkbox') continue;
          if (/say|number|başlangıç/i.test(ph || '') || type === 'number') { await inputs.nth(i).fill('1'); }
          else { await inputs.nth(i).fill(i === 0 ? 'TST' : 'TEST Serisi'); }
        }
        const addBtn = page.locator('div.fixed button, form button').filter({ hasText: /Ekle|Kaydet|Oluştur/ }).last();
        if (await addBtn.count() && !(await addBtn.isDisabled().catch(() => true))) {
          await addBtn.click();
          await page.waitForTimeout(2500);
          const f2 = await shot(page, 'fatura-02-seri-eklendi');
          record(S, 'TEST fatura serisi ekleme', 'INFO', 'Kaydet tıklandı', f2);
        } else record(S, 'TEST fatura serisi ekleme', 'WARN', 'Kaydet butonu pasif/bulunamadı');
      }
    } else record(S, 'Fatura Serileri ekranı', 'WARN', 'Seri butonu/linki bulunamadı');
  } catch (e) { record(S, 'Fatura serileri', 'ERROR', e.message.slice(0, 150)); }

  // B) Vitrin Yönetimi
  S = 'vitrin';
  try {
    await page.goto(BASE + '/admin/storefront/pages', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);
    // Platform seç dropdown
    const psel = page.locator('select').first();
    if (await psel.count()) { await psel.selectOption({ index: 1 }); }
    else { await page.locator('button:has-text("Platform seç"), div:has-text("Platform seç")').last().click(); await page.waitForTimeout(800); await page.locator('[role="option"], li').first().click().catch(() => {}); }
    await page.waitForTimeout(3000);
    const f = await shot(page, 'vitrin-01-bloklar');
    const vtxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    const blockCount = (vtxt.match(/blok/gi) || []).length;
    record(S, 'Platform seçimi + blok listesi', vtxt.includes('Yayınla') || blockCount > 0 ? 'PASS' : 'WARN', vtxt.slice(150, 500), f);
    const btns = await page.evaluate(() => [...document.querySelectorAll('button')].map(b => b.textContent.trim()).filter(t => t && t.length < 35));
    record(S, 'Vitrin aksiyon butonları', 'INFO', btns.join(' | ').slice(0, 400));
  } catch (e) { record(S, 'Vitrin', 'ERROR', e.message.slice(0, 150)); }

  // C) Menüler — ağaç editörü
  S = 'menuler';
  try {
    await page.goto(BASE + '/admin/navigation/menus', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    await page.locator('table tbody tr').first().click();
    await page.waitForTimeout(3000);
    const f = await shot(page, 'menu-01-agac-editoru');
    const mtxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    record(S, 'Menü detay (ağaç editörü)', /menus\/.+/.test(page.url()) ? 'PASS' : 'WARN', mtxt.slice(80, 350), f);
  } catch (e) { record(S, 'Menüler', 'ERROR', e.message.slice(0, 150)); }

  // D) Yorum + koleksiyon moderasyonu
  S = 'moderasyon';
  try {
    await page.goto(BASE + '/admin/storefront/reviews', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    const f = await shot(page, 'moderasyon-01-yorumlar');
    const rtxt = (await page.locator('table').innerText()).replace(/\s+/g, ' ').slice(0, 250);
    record(S, 'Yorum moderasyonu listesi', 'PASS', rtxt, f);
    await page.goto(BASE + '/admin/storefront/collections', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    const f2 = await shot(page, 'moderasyon-02-koleksiyonlar');
    record(S, 'Koleksiyon moderasyonu listesi', 'PASS', (await page.locator('table').innerText()).replace(/\s+/g, ' ').slice(0, 200), f2);
  } catch (e) { record(S, 'Moderasyon', 'ERROR', e.message.slice(0, 150)); }

  if (errors.length) record('vitrin', 'HTTP/konsol hataları (tur)', 'WARN', JSON.stringify(errors.slice(0, 8)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
