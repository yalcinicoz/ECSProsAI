const { launch, record, shot, login } = require('./lib');
const BASE = 'https://51.178.208.59';

async function rowClickTest(page, S, url, waitSel, expectUrlPart, shotName) {
  await page.goto(BASE + url, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector(waitSel, { timeout: 25000 });
  await page.waitForTimeout(800);
  const rowTxt = (await page.locator('table tbody tr').first().innerText().catch(() => '')).replace(/\s+/g, ' ').slice(0, 100);
  await page.locator('table tbody tr').first().click();
  await page.waitForTimeout(2500);
  const f = await shot(page, shotName);
  const ok = expectUrlPart ? page.url().includes(expectUrlPart) : true;
  record(S, `${url} satır tıklama`, ok ? 'PASS' : 'WARN', `ilk satır: ${rowTxt} → ${page.url()}`, f);
  return ok;
}

(async () => {
  const { browser, page, errors } = await launch();
  await login(page);

  // A) Siparişler
  let S = 'siparisler';
  try {
    await page.goto(BASE + '/admin/orders', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    const rows = await page.locator('table tbody tr').count();
    const listTxt = (await page.locator('table').innerText()).replace(/\s+/g, ' ').slice(0, 400);
    const f0 = await shot(page, 'siparis-01-liste');
    record(S, 'Sipariş listesi', rows > 0 ? 'PASS' : 'WARN', `${rows} satır | ${listTxt.slice(0, 250)}`, f0);
    // Durum filtresi dene
    const sel = page.locator('select').first();
    if (await sel.count()) {
      const opts = await sel.locator('option').allTextContents();
      record(S, 'Durum filtresi seçenekleri', 'INFO', opts.join(', ').slice(0, 200));
    }
    // Satır tıklama → detay
    await page.locator('table tbody tr').first().click();
    await page.waitForTimeout(2500);
    const f1 = await shot(page, 'siparis-02-detay');
    record(S, 'Sipariş detayı', /\/orders\/.+/.test(page.url()) ? 'PASS' : 'FAIL', page.url(), f1);
    const dTxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    const hasParts = ['Kalem', 'Toplam', 'Durum'].filter(k => dTxt.includes(k));
    const btns = await page.evaluate(() => [...document.querySelectorAll('button')].map(b => b.textContent.trim()).filter(t => t && t.length < 30));
    record(S, 'Detay içerik + aksiyonlar', hasParts.length >= 2 ? 'PASS' : 'WARN', `bulunan: ${hasParts.join('/')} | butonlar: ${btns.join(' | ').slice(0, 250)}`);
  } catch (e) { record(S, 'Siparişler', 'ERROR', e.message.slice(0, 150)); }

  // B) İadeler + Faturalar
  try { await rowClickTest(page, 'iadeler', '/admin/orders/returns', 'table tbody tr', '', 'siparis-03-iadeler'); } catch (e) { record('iadeler', 'sayfa', 'ERROR', e.message.slice(0, 120)); }
  try { await rowClickTest(page, 'faturalar', '/admin/orders/invoices', 'table tbody tr', '', 'siparis-04-faturalar'); } catch (e) { record('faturalar', 'sayfa', 'ERROR', e.message.slice(0, 120)); }

  // C) Üyeler
  S = 'crm';
  try {
    await page.goto(BASE + '/admin/crm/members', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('table tbody tr', { timeout: 25000 });
    const search = page.locator('input').first();
    await search.fill('test');
    await page.waitForTimeout(2000);
    const rows = await page.locator('table tbody tr').count();
    const first = (await page.locator('table tbody tr').first().innerText().catch(() => '')).replace(/\s+/g, ' ').slice(0, 90);
    record(S, 'Üye arama ("test")', 'INFO', `${rows} satır | ilk: ${first}`);
    await search.fill('');
    await page.waitForTimeout(1500);
    await page.locator('table tbody tr').first().click();
    await page.waitForTimeout(2500);
    const f = await shot(page, 'crm-01-uye-detay');
    record(S, 'Üye detayı', /members\/.+/.test(page.url()) ? 'PASS' : 'FAIL', page.url(), f);
    const uTxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    record(S, 'Üye detay içeriği', 'INFO', uTxt.slice(80, 400));
  } catch (e) { record(S, 'Üyeler', 'ERROR', e.message.slice(0, 150)); }
  try { await rowClickTest(page, 'crm', '/admin/crm/member-groups', 'table tbody tr', '', 'crm-02-uye-gruplari'); } catch (e) { record('crm', 'üye grupları', 'ERROR', e.message.slice(0, 120)); }

  // D) İletişim mesajları + bülten + bildirimler
  try { await rowClickTest(page, 'iletisim', '/admin/storefront/contact-messages', 'table tbody tr', '', 'iletisim-01-mesajlar'); } catch (e) { record('iletisim', 'mesajlar', 'ERROR', e.message.slice(0, 120)); }
  try {
    await page.goto(BASE + '/admin/storefront/newsletter', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);
    const f = await shot(page, 'iletisim-02-bulten');
    record('iletisim', 'Bülten aboneleri', 'PASS', '', f);
    await page.goto(BASE + '/admin/storefront/notifications', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    const nTxt = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
    const f2 = await shot(page, 'iletisim-03-bildirimler');
    record('iletisim', 'Bildirim izleme', 'INFO', nTxt.slice(60, 350), f2);
  } catch (e) { record('iletisim', 'bülten/bildirim', 'ERROR', e.message.slice(0, 120)); }

  if (errors.length) record('siparisler', 'HTTP/konsol hataları (tur)', 'WARN', JSON.stringify(errors.slice(0, 8)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
