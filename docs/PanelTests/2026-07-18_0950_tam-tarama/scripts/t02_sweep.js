const { launch, record, shot, login } = require('./lib');
const fs = require('fs');
const BASE = 'https://51.178.208.59';

(async () => {
  const { browser, page, errors } = await launch();
  await login(page);
  const links = JSON.parse(fs.readFileSync('nav-links.json', 'utf8'));
  const S = 'sayfa-taramasi';
  const pageMeta = [];

  for (let i = 0; i < links.length; i++) {
    const l = links[i];
    const slug = l.href.replace('/admin', '').replace(/^\//, '').replace(/\//g, '-') || 'dashboard';
    const name = `sweep-${String(i).padStart(2, '0')}-${slug}`;
    errors.length = 0;
    const t0 = Date.now();
    try {
      await page.goto(BASE + l.href, { waitUntil: 'networkidle', timeout: 30000 });
      // spinner kaybolana kadar bekle (maks 12 sn)
      try { await page.waitForFunction(() => !document.querySelector('.animate-spin, [class*="spinner"]'), { timeout: 12000 }); } catch {}
      await page.waitForTimeout(400);
      const ms = Date.now() - t0;
      const meta = await page.evaluate(() => {
        const h = document.querySelector('h1, h2, [class*="page-title"]');
        const tables = document.querySelectorAll('table');
        let rows = 0; tables.forEach(t => rows += t.querySelectorAll('tbody tr').length);
        const bodyText = document.body.innerText;
        const isEmpty = bodyText.replace(/\s+/g, ' ').length < 200;
        const hasPlaceholder = /yakında|placeholder|geliştirme aşamasında|coming soon|hazırlanıyor/i.test(bodyText);
        const inputs = document.querySelectorAll('input, select, textarea').length;
        const buttons = [...document.querySelectorAll('button')].map(b => b.textContent.trim()).filter(t => t && t.length < 30).slice(0, 15);
        return { title: h ? h.textContent.trim() : '', tables: tables.length, rows, isEmpty, hasPlaceholder, inputs, buttons, textLen: bodyText.length };
      });
      const f = await shot(page, name);
      const errNote = errors.length ? ` | HATALAR: ${JSON.stringify(errors.slice(0,3))}` : '';
      const status = errors.some(e => e.kind === 'http' && e.status >= 500) ? 'FAIL' : (meta.hasPlaceholder ? 'WARN' : (errors.length ? 'WARN' : 'PASS'));
      record(S, l.href, status, `${ms}ms | başlık:"${meta.title}" | tablo:${meta.tables} satır:${meta.rows} form-alanı:${meta.inputs}${meta.hasPlaceholder ? ' | PLACEHOLDER' : ''}${errNote}`, f);
      pageMeta.push({ href: l.href, ...meta, ms, errors: [...errors], shot: f });
    } catch (e) {
      const f = await shot(page, name + '-err').catch(() => '');
      record(S, l.href, 'ERROR', e.message.slice(0, 150), f);
      pageMeta.push({ href: l.href, error: e.message.slice(0, 150) });
    }
  }
  fs.writeFileSync('page-meta.json', JSON.stringify(pageMeta, null, 2));
  await browser.close();
})().catch(e => { console.error('FATAL:', e); process.exit(1); });
