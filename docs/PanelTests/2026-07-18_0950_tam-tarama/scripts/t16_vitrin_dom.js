const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';
const S = 'vitrin';
(async () => {
  const { browser, page, errors } = await launch();
  await login(page);
  await page.goto(BASE + '/admin/storefront/pages', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2500);
  // "Platform seç" öğesinin DOM yapısı
  const dom = await page.evaluate(() => {
    const el = [...document.querySelectorAll('*')].find(e => e.childElementCount === 0 && e.textContent.trim() === 'Platform seç');
    if (!el) return 'bulunamadı';
    let cur = el, out = [];
    for (let i = 0; i < 4 && cur; i++) { out.push(cur.outerHTML.slice(0, 300)); cur = cur.parentElement; }
    return out.join('\n---\n');
  });
  console.log('DOM:\n' + dom.slice(0, 1500));
  // Kutunun tamamına tıkla
  const box = page.locator('div,button').filter({ hasText: /^Platform seç$/ }).last();
  await box.click({ force: true });
  await page.waitForTimeout(1200);
  const after = await page.evaluate(() => document.body.innerHTML.length);
  const f = await shot(page, 'vitrin-04-dropdown-tiklama');
  // listbox var mı
  const items = await page.evaluate(() => [...document.querySelectorAll('div,li,button')].map(e => e.textContent.trim()).filter(t => /Julude|Mishar|Tozlu/i.test(t) && t.length < 80).slice(0, 8));
  record(S, 'Dropdown tıklama sonrası platform öğeleri', items.length ? 'PASS' : 'FAIL', JSON.stringify(items).slice(0, 300), f);
  if (items.length) {
    await page.locator('div,li,button').filter({ hasText: /^Mishar/i }).last().click({ force: true }).catch(() => {});
    await page.waitForTimeout(3500);
    const f2 = await shot(page, 'vitrin-05-mishar-secili');
    const vtxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    record(S, 'Mishar seçimi sonrası bloklar', /Yayınla|Taslak|blok/i.test(vtxt.slice(150)) ? 'PASS' : 'WARN', vtxt.slice(150, 600), f2);
    const btns = await page.evaluate(() => [...document.querySelectorAll('button')].map(b => b.textContent.trim()).filter(t => t && t.length < 35));
    record(S, 'Blok aksiyonları', 'INFO', btns.join(' | ').slice(0, 400));
  }
  if (errors.length) record(S, 'HTTP/konsol hataları', 'WARN', JSON.stringify(errors.slice(0, 6)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
