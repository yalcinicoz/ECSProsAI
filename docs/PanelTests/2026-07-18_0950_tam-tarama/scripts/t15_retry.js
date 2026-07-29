const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';

(async () => {
  const { browser, page, errors } = await launch();
  await login(page);

  // A) Fatura serisi: Firma seç + alanlar + Seri Ekle
  let S = 'faturalar';
  try {
    await page.goto(BASE + '/admin/orders/invoices', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);
    await page.locator('button:has-text("Fatura Serileri")').first().click();
    await page.waitForTimeout(1200);
    const firmSel = page.locator('div.fixed select').first();
    const firms = await firmSel.locator('option').allTextContents();
    await firmSel.selectOption({ index: 2 });  // Mişaroğlu?
    await page.locator('div.fixed input').nth(0).fill('TEST Serisi');
    await page.locator('div.fixed input').nth(1).fill('TST');
    await page.waitForTimeout(400);
    const addBtn = page.locator('div.fixed button:has-text("Seri Ekle")');
    const enabled = !(await addBtn.isDisabled());
    if (enabled) { await addBtn.click(); await page.waitForTimeout(2500); }
    const f = await shot(page, 'fatura-03-seri-ekleme');
    const mtxt = (await page.locator('div.fixed').last().innerText().catch(() => '')).replace(/\s+/g, ' ');
    record(S, 'TEST fatura serisi ekleme', /TST|TEST Serisi/.test(mtxt) ? 'PASS' : 'WARN', `firmalar: ${firms.join('/')} | ${mtxt.slice(0, 200)}`, f);
    await page.locator('div.fixed button:has-text("Kapat")').click().catch(() => {});
    await page.waitForTimeout(600);

    // Onaylı siparişe fatura kes
    await page.goto(BASE + '/admin/orders', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    await page.locator('table tbody tr').filter({ hasText: 'Onaylı' }).first().click();
    await page.waitForTimeout(2500);
    await page.locator('button:has-text("Fatura Oluştur")').first().click();
    await page.waitForTimeout(1500);
    const modal = page.locator('div.fixed').last();
    const serSel = modal.locator('select').first();
    if (await serSel.count()) { await serSel.selectOption({ index: 1 }).catch(() => {}); }
    await page.waitForTimeout(400);
    const fM = await shot(page, 'fatura-04-fatura-modal-dolu');
    const createBtn = modal.locator('button').filter({ hasText: /Oluştur/ }).last();
    if (await createBtn.count() && !(await createBtn.isDisabled().catch(() => true))) {
      await createBtn.click(); await page.waitForTimeout(3000);
      const t2 = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
      const f2 = await shot(page, 'fatura-05-fatura-kesildi');
      record(S, 'Fatura oluşturma (seri sonrası)', /Faturalar \([1-9]/.test(t2) ? 'PASS' : 'WARN', (t2.match(/(MSH|TST)\S*|Faturalar.{0,80}/) || [''])[0], f2);
    } else {
      const mtxt2 = (await modal.innerText().catch(() => '')).replace(/\s+/g, ' ');
      record(S, 'Fatura oluşturma (seri sonrası)', 'WARN', 'Oluştur pasif — modal: ' + mtxt2.slice(0, 250), fM);
    }
  } catch (e) { record(S, 'Fatura akışı', 'ERROR', e.message.slice(0, 150)); }

  // B) Vitrin platform dropdown
  S = 'vitrin';
  try {
    await page.goto(BASE + '/admin/storefront/pages', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);
    await page.locator('text=Platform seç').last().click();
    await page.waitForTimeout(1000);
    const f = await shot(page, 'vitrin-02-platform-dropdown');
    const opts = await page.evaluate(() => [...document.querySelectorAll('[role="option"], [role="listbox"] *, .absolute button, .absolute li')].map(e => e.textContent.trim()).filter(t => t && t.length < 60).slice(0, 12));
    record(S, 'Platform dropdown seçenekleri', opts.length ? 'PASS' : 'WARN', opts.join(' | ').slice(0, 250), f);
    const mishar = page.locator('button, li, [role="option"]').filter({ hasText: /Mishar/i }).last();
    await mishar.click({ timeout: 5000 });
    await page.waitForTimeout(3500);
    const f2 = await shot(page, 'vitrin-03-bloklar-mishar');
    const vtxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    record(S, 'Mishar blok listesi', /Yayınla|blok|Blok/i.test(vtxt) ? 'PASS' : 'WARN', vtxt.slice(150, 550), f2);
  } catch (e) { record(S, 'Vitrin platform seçimi', 'ERROR', e.message.slice(0, 150)); }

  // C) Menüler incelemesi
  S = 'menuler';
  try {
    await page.goto(BASE + '/admin/navigation/menus', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    const f = await shot(page, 'menu-02-liste-inceleme');
    const txt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    const sels = await page.locator('select').count();
    const btns = await page.evaluate(() => [...document.querySelectorAll('button')].map(b => b.textContent.trim()).filter(t => t && t.length < 35));
    record(S, 'Menü sayfası durumu', 'INFO', `select:${sels} | butonlar: ${btns.join(' | ').slice(0, 200)} | metin: ${txt.slice(60, 300)}`, f);
    if (sels) {
      await page.locator('select').first().selectOption({ index: 1 }).catch(() => {});
      await page.waitForTimeout(2000);
      const f2 = await shot(page, 'menu-03-platform-secili');
      record(S, 'Platform seçimi sonrası menüler', 'INFO', (await page.locator('body').innerText()).replace(/\s+/g, ' ').slice(60, 300), f2);
    }
  } catch (e) { record(S, 'Menüler', 'ERROR', e.message.slice(0, 150)); }

  if (errors.length) record('vitrin', 'HTTP/konsol hataları (tur2)', 'WARN', JSON.stringify(errors.slice(0, 8)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
