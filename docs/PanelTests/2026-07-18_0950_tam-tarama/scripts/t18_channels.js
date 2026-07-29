const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';
const S = 'ayarlar';
(async () => {
  const { browser, page, errors } = await launch();
  await login(page);
  await page.goto(BASE + '/admin/settings/channels', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);
  const f0 = await shot(page, 'ayarlar-06-kanallar-liste');
  const btn = page.locator('button:has-text("Düzenle"), a:has-text("Düzenle")').nth(1);
  if (await btn.count()) {
    await btn.click();
    await page.waitForTimeout(2500);
    const f = await shot(page, 'ayarlar-07-kanal-duzenle');
    const kTxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    const labels = await page.evaluate(() => [...document.querySelectorAll('label')].map(l => l.textContent.trim()).filter(t => t).slice(0, 30));
    record(S, 'Kanal düzenleme formu', labels.length ? 'PASS' : 'WARN', labels.join(' | ').slice(0, 450), f);
    const outStock = /stok|tüken/i.test(kTxt);
    record(S, 'Kanalda stok görünürlük ayarı (showOutOfStock)', outStock ? 'PASS' : 'WARN', (kTxt.match(/[^.]*[Ss]tok[^.]*\./) || ['bulunamadı'])[0].slice(0, 200));
  } else {
    record(S, 'Kanal düzenleme', 'WARN', 'Düzenle butonu bulunamadı', f0);
  }
  // Toplu resim yükleme ekranı görünümü
  await page.goto(BASE + '/admin/catalog/bulk-images', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2000);
  const f2 = await shot(page, 'katalog-27-toplu-resim');
  record('katalog', 'Toplu resim yükleme ekranı', 'INFO', (await page.locator('body').innerText()).replace(/\s+/g, ' ').slice(60, 300), f2);
  if (errors.length) record(S, 'HTTP/konsol hataları', 'WARN', JSON.stringify(errors.slice(0, 6)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
