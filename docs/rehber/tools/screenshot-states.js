// Rehber: sekme/modal/bölüm durum görselleri (screenshot.js'in tamamlayıcısı) — aynı env değişkenleri.
import { chromium } from 'playwright-core';
import fs from 'node:fs'; import path from 'node:path'; import { fileURLToPath } from 'node:url';
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const IMG = path.resolve(__dirname, '../img');
const BASE = (process.env.REHBER_BASE || 'https://admin-telemania.ecspros.com').replace(/\/$/, '');
const EXE = process.env.CHROME || process.env.HOME + '/.cache/ms-playwright/chromium-1234/chrome-linux64/chrome';
let sharp = null; try { sharp = (await import('sharp')).default; } catch { }
const args = ['--no-sandbox']; if (process.env.REHBER_RESOLVE) args.push('--host-resolver-rules=MAP ' + process.env.REHBER_RESOLVE.replace(':', ' '));
const br = await chromium.launch({ executablePath: EXE, headless: true, args });
const ctx = await br.newContext({ viewport: { width: 1440, height: 900 }, ignoreHTTPSErrors: true, locale: 'tr-TR' });
const page = await ctx.newPage(); page.setDefaultTimeout(15000);
async function kaydet(slug, opts = {}) {
  const png = path.join(IMG, slug + '.png');
  await page.screenshot({ path: png, fullPage: !opts.viewport });
  if (sharp) { await sharp(png).webp({ quality: 82 }).toFile(path.join(IMG, slug + '.webp')); fs.unlinkSync(png); }
}
const apiGet = (u) => page.evaluate(async (u) => { const t = localStorage.getItem('access_token'); const r = await fetch(u, { headers: { Authorization: 'Bearer ' + t } }); return r.ok ? r.json() : null; }, u);
const first = (j) => { const d = j && j.data !== undefined ? j.data : j; if (Array.isArray(d)) return d[0]; if (d && d.items) return d.items[0]; return d; };
const rapor = [];
async function adim(slug, fn) { try { await fn(); await page.waitForTimeout(700); rapor.push('OK   ' + slug); } catch (e) { rapor.push('FAIL ' + slug + '  ' + String(e.message).split('\n')[0].slice(0, 90)); } }

// Giriş sayfası (oturumsuz)
await adim('login', async () => { await page.goto(BASE + '/admin/login', { waitUntil: 'networkidle' }); await page.waitForTimeout(500); await kaydet('login', { viewport: true }); });
// Giriş
await page.fill('input[type="text"], input[type="email"]', process.env.REHBER_USER); await page.fill('input[type="password"]', process.env.REHBER_PASS);
await Promise.all([page.waitForURL(u => !u.pathname.endsWith('/login')).catch(() => { }), page.keyboard.press('Enter')]); await page.waitForTimeout(800);

// Ürün detay sekmeleri
const urun = first(await apiGet('/api/catalog/products?page=1&pageSize=1'));
const urunKod = urun?.code;
for (const [sekme, slug] of [['Özellikler', 'ozellikler-sekmesi'], ['Varyantlar', 'varyantlar-sekmesi'], ['Alt Özellikler', 'alt-ozellikler-sekmesi'], ['Stok', 'stok-sekmesi'], ['Satış Kanalları', 'satis-kanallari-sekmesi'], ['Görseller', 'gorseller-sekmesi'], ['Etiketler', 'etiketler-sekmesi'], ['SEO', 'seo-sekmesi']]) {
  await adim('catalog-products-detay--' + slug, async () => {
    if (!urunKod) throw new Error('ürün yok');
    await page.goto(BASE + '/admin/catalog/products/' + encodeURIComponent(urunKod), { waitUntil: 'networkidle' });
    await page.locator('.stab', { hasText: sekme }).first().click(); await page.waitForTimeout(900);
    await kaydet('catalog-products-detay--' + slug);
  });
}
// Görseller sekmesinde video bölümü varsa aynı görseli kullan
await adim('catalog-products-detay--videolar-sekmesi', async () => { if (!fs.existsSync(path.join(IMG, 'catalog-products-detay--gorseller-sekmesi.webp'))) throw new Error('görseller yok'); fs.copyFileSync(path.join(IMG, 'catalog-products-detay--gorseller-sekmesi.webp'), path.join(IMG, 'catalog-products-detay--videolar-sekmesi.webp')); });
// Varyant ekle modalı
await adim('catalog-products-detay--varyant-ekle-modal', async () => {
  await page.goto(BASE + '/admin/catalog/products/' + encodeURIComponent(urunKod), { waitUntil: 'networkidle' });
  await page.locator('.stab', { hasText: 'Varyantlar' }).first().click(); await page.waitForTimeout(600);
  await page.locator('button', { hasText: /Varyant Ekle|Yeni Varyant/ }).first().click(); await page.waitForTimeout(600);
  await kaydet('catalog-products-detay--varyant-ekle-modal', { viewport: true });
});
// Talepler: yeni talep modalı
await adim('requests--yeni-talep-modal', async () => { await page.goto(BASE + '/admin/requests', { waitUntil: 'networkidle' }); await page.locator('button', { hasText: 'Yeni Talep' }).first().click(); await page.waitForTimeout(500); await kaydet('requests--yeni-talep-modal', { viewport: true }); });
// Talep detayı durum modalı
await adim('requests-detay--durum-modal', async () => {
  const t = first(await apiGet('/api/requests?page=1&pageSize=1')); if (!t) throw new Error('talep yok');
  await page.goto(BASE + '/admin/requests/' + t.id, { waitUntil: 'networkidle' });
  await page.locator('button', { hasText: /Durum/ }).first().click(); await page.waitForTimeout(500); await kaydet('requests-detay--durum-modal', { viewport: true });
});
// Kullanıcı düzenle modalı (ilk satır)
await adim('settings-users--duzenle-modal', async () => { await page.goto(BASE + '/admin/settings/users', { waitUntil: 'networkidle' }); const b = page.locator('table tbody tr').first().locator('button').first(); await b.click({ force: true }); await page.waitForTimeout(500); await kaydet('settings-users--duzenle-modal', { viewport: true }); });
// POS satış detay modalı
await adim('pos-sales--detay-modal', async () => { await page.goto(BASE + '/admin/pos/sales', { waitUntil: 'networkidle' }); const r = page.locator('table tbody tr').first(); if (!(await r.count())) throw new Error('satış yok'); await r.click({ force: true }); await page.waitForTimeout(600); await kaydet('pos-sales--detay-modal', { viewport: true }); });
// Firma detayı — Entegrasyonlar bölümü
await adim('settings-firms-detay--entegrasyonlar', async () => {
  const f = first(await apiGet('/api/core/firms')); await page.goto(BASE + '/admin/settings/firms/' + f.id, { waitUntil: 'networkidle' });
  const h = page.locator('h2, h3', { hasText: 'Entegrasyon' }).first(); await h.scrollIntoViewIfNeeded(); await page.waitForTimeout(400); await kaydet('settings-firms-detay--entegrasyonlar', { viewport: true });
});
// Sipariş detayı — Paketler bölümü
await adim('orders-detay--paketler', async () => {
  const o = first(await apiGet('/api/orders?page=1&pageSize=1')); await page.goto(BASE + '/admin/orders/' + o.id, { waitUntil: 'networkidle' });
  const h = page.locator('h2, h3, h4', { hasText: 'Paketler' }).first(); await h.scrollIntoViewIfNeeded(); await page.waitForTimeout(400); await kaydet('orders-detay--paketler', { viewport: true });
});
// Pazaryeri mağaza — ürün gönderimi sekmesi
await adim('marketplaces-detay--urun-gonderimi', async () => {
  const ov = await apiGet('/api/marketplaces/overview'); const d = ov?.data ?? ov; const store = Array.isArray(d) ? (d.find(x => x.id) || (d[0]?.stores || [])[0]) : (d?.stores || [])[0];
  if (!store) throw new Error('mağaza yok'); await page.goto(BASE + '/admin/marketplaces/' + store.id + '?tab=urunler', { waitUntil: 'networkidle' }); await page.waitForTimeout(800); await kaydet('marketplaces-detay--urun-gonderimi');
});
// Fulfillment operasyon ekranları (plan id)
const plan = first(await apiGet('/api/fulfillment/picking-plans?page=1&pageSize=1'));
for (const [slug, yol] of [['fulfillment-fast-lane-detay', '/fulfillment/fast-lane/'], ['fulfillment-sorting-detay', '/fulfillment/sorting/'], ['fulfillment-sorting-wall-detay', '/fulfillment/sorting-wall/']])
  await adim(slug, async () => { if (!plan) throw new Error('plan yok'); await page.goto(BASE + '/admin' + yol + plan.id, { waitUntil: 'networkidle' }); await page.waitForTimeout(900); await kaydet(slug); });
await adim('fulfillment-desk-detay', async () => { const d = first(await apiGet('/api/fulfillment/desks')); if (!d) throw new Error('masa yok'); await page.goto(BASE + '/admin/fulfillment/desk/' + d.id, { waitUntil: 'networkidle' }); await page.waitForTimeout(900); await kaydet('fulfillment-desk-detay'); });

await br.close(); console.log(rapor.join('\n'));
