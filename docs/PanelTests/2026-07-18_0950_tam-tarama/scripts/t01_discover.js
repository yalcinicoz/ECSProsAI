const { launch, record, shot, login, BASE } = require('./lib');

(async () => {
  const { browser, page, errors } = await launch();
  const S = 'giris';

  // 1) Negatif: yanlış şifre
  try {
    await page.goto(BASE + '/admin/login', { waitUntil: 'networkidle' });
    const f = await shot(page, '01-login-sayfasi');
    record(S, 'Login sayfası yükleniyor', 'PASS', 'URL: ' + page.url(), f);
    await page.locator('input[type="text"]').first().fill('admin');
    await page.locator('input[type="password"]').first().fill('YanlisSifre1!');
    await page.locator('button[type="submit"], button:has-text("Giriş")').first().click();
    await page.waitForTimeout(2500);
    const stillLogin = page.url().includes('/login');
    const bodyText = (await page.locator('body').innerText()).slice(0, 2000);
    const hasError = /hatalı|geçersiz|yanlış|invalid|error|başarısız/i.test(bodyText);
    const f2 = await shot(page, '02-login-yanlis-sifre');
    record(S, 'Yanlış şifreyle giriş reddi', stillLogin ? 'PASS' : 'FAIL',
      (stillLogin ? 'Login sayfasında kaldı. ' : 'YÖNLENDİRİLDİ! ') + (hasError ? 'Hata mesajı görünüyor.' : 'DİKKAT: kullanıcıya hata mesajı görünmüyor olabilir.'), f2);
  } catch (e) { record(S, 'Yanlış şifre testi', 'ERROR', e.message.slice(0,200)); }

  // 2) Doğru giriş
  try {
    await login(page);
    const f = await shot(page, '03-login-basarili-ilk-ekran');
    record(S, 'Doğru bilgilerle giriş', 'PASS', 'Giriş sonrası URL: ' + page.url(), f);
  } catch (e) {
    record(S, 'Doğru bilgilerle giriş', 'FAIL', e.message.slice(0,200));
    await browser.close(); process.exit(1);
  }

  // 3) Sidebar envanteri — sidebar'ı aç (hover ya da genişlet) ve tüm linkleri çek
  try {
    // Sidebar genellikle fixed; tüm <a href^="/admin"> linklerini topla
    const links = await page.evaluate(() => {
      const out = [];
      document.querySelectorAll('a[href]').forEach(a => {
        const href = a.getAttribute('href');
        if (href && href.startsWith('/admin') && !href.includes('logout')) {
          out.push({ href, text: (a.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 60) });
        }
      });
      return out;
    });
    const uniq = [...new Map(links.map(l => [l.href, l])).values()];
    require('fs').writeFileSync('nav-links.json', JSON.stringify(uniq, null, 2));
    record(S, 'Sidebar link envanteri', 'PASS', uniq.length + ' benzersiz link bulundu (nav-links.json)');
    console.log(JSON.stringify(uniq, null, 1));
  } catch (e) { record(S, 'Sidebar link envanteri', 'ERROR', e.message.slice(0,200)); }

  if (errors.length) record(S, 'Konsol/HTTP hataları', 'WARN', JSON.stringify(errors.slice(0, 10)));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
