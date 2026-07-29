const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';
const S = 'katalog-urun-crud';
(async () => {
  const { browser, page, errors } = await launch();
  await login(page);
  await page.goto(BASE + '/admin/catalog/products/TEST-PANEL-001', { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('.stab', { timeout: 20000 });
  await page.waitForTimeout(1200);

  await page.locator('button:has-text("Fiyat Geçmişi")').click();
  await page.waitForTimeout(1000);
  await page.keyboard.press('Escape');
  await page.waitForTimeout(800);
  const escClosed = !(await page.locator('div.fixed.inset-0').count());
  record(S, 'UX: Fiyat geçmişi ESC ile kapanma', escClosed ? 'PASS' : 'WARN', escClosed ? 'ESC kapatıyor' : 'ESC kapatmıyor — X/backdrop gerekiyor');
  if (!escClosed) {
    const closeBtn = page.locator('div.fixed.inset-0 button').first();
    await closeBtn.click().catch(() => page.mouse.click(200, 450));
    await page.waitForTimeout(800);
    const closed = !(await page.locator('div.fixed.inset-0').count());
    record(S, 'Fiyat geçmişi kapatma (X/backdrop)', closed ? 'PASS' : 'FAIL', closed ? 'Kapandı' : 'Kapanamadı');
    if (!closed) { await page.reload({ waitUntil: 'domcontentloaded' }); await page.waitForSelector('.stab'); await page.waitForTimeout(1200); }
  }

  await page.locator('button:has-text("Satışa Aç")').click();
  await page.waitForTimeout(2500);
  const f = await shot(page, 'katalog-18-satisa-ac');
  const body = await page.locator('body').innerText();
  record(S, '"Satışa Aç" işlemi', /Satışta/.test(body) ? 'PASS' : 'WARN', /Satışta/.test(body) ? 'Durum "Satışta"' : 'Durum değişmedi', f);

  await page.locator('.stab').filter({ hasText: 'Varyant' }).first().click();
  await page.waitForTimeout(1500);
  const f2 = await shot(page, 'katalog-19-test-urun-varyantlar');
  const vTxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
  record(S, 'Varyantsız üründe varyant sekmesi', 'INFO', (vTxt.match(/Varyantlar.{0,220}/) || [''])[0], f2);

  if (errors.length) record(S, 'HTTP/konsol hataları', 'WARN', JSON.stringify(errors.slice(0, 5)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
