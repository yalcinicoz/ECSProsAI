// Admin paneli ekran görüntüsü toplayıcı (rehber görselleri) — kullanım:
//   REHBER_BASE=https://admin-telemania.ecspros.com REHBER_USER=... REHBER_PASS=... node screenshot.js [slug-filtre]
//   ya da REHBER_TOKEN=<access_token> REHBER_REFRESH=<refresh_token> (tarayıcı localStorage'ından) ile şifresiz.
// Çıktı: docs/rehber/img/<slug>.webp (sharp ile PNG→WebP). Slug kuralı YAZIM-KILAVUZU.md'dedir.
// Detay sayfaları (:id/:code) için liste sayfasına gidilip ilk satıra tıklanır (genel kalıp: satır → detay).
import { chromium } from 'playwright-core';
import fs from 'node:fs'; import path from 'node:path'; import { fileURLToPath } from 'node:url';
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const IMG = path.resolve(__dirname, '../img'); fs.mkdirSync(IMG, { recursive: true });
const BASE = (process.env.REHBER_BASE || 'https://admin-telemania.ecspros.com').replace(/\/$/, '');
const EXE = process.env.CHROME || process.env.HOME + '/.cache/ms-playwright/chromium-1234/chrome-linux64/chrome';
const FILTRE = process.argv[2] || null;
let sharp = null; try { sharp = (await import('sharp')).default; } catch { }

// [slug, yol, seçenekler] — yol ":detay" içeriyorsa liste → ilk satır tıklanır
const ROTALAR = [
  ['dashboard', '/'], ['requests', '/requests'], ['requests-detay', '/requests/:detay'],
  ['catalog-products', '/catalog/products'], ['catalog-products-new', '/catalog/products/new'], ['catalog-products-detay', '/catalog/products/:detay'],
  ['catalog-product-submissions', '/catalog/product-submissions'], ['catalog-product-submissions-detay', '/catalog/product-submissions/:detay'],
  ['catalog-bulk-images', '/catalog/bulk-images'], ['catalog-attribute-types', '/catalog/attribute-types'], ['catalog-attribute-types-detay', '/catalog/attribute-types/:detay'],
  ['catalog-product-groups', '/catalog/product-groups'], ['catalog-product-groups-detay', '/catalog/product-groups/:detay'], ['catalog-settings', '/catalog/settings'],
  ['storefront-channel-categories', '/storefront/channel-categories'], ['storefront-channel-categories-detay', '/storefront/channel-categories/:detay'],
  ['storefront-menu-placement', '/storefront/menu-placement'], ['storefront-product-card', '/storefront/product-card'], ['storefront-tracking-consent', '/storefront/tracking-consent'],
  ['storefront-channel-products', '/storefront/channel-products'], ['storefront-collections', '/storefront/collections'], ['storefront-reviews', '/storefront/reviews'],
  ['storefront-pages', '/storefront/pages'], ['storefront-pages-detay', '/storefront/pages/:detay'], ['storefront-pages-history', '/storefront/pages/history'], ['storefront-contact-messages', '/storefront/contact-messages'],
  ['orders', '/orders'], ['orders-detay', '/orders/:detay'], ['orders-returns', '/orders/returns'], ['orders-returns-detay', '/orders/returns/:detay'],
  ['orders-invoices', '/orders/invoices'], ['orders-quotes', '/orders/quotes'], ['orders-gift-cards', '/orders/gift-cards'], ['orders-number-series', '/orders/number-series'], ['orders-cargo-zones', '/orders/cargo-zones'],
  ['fulfillment-picking-plans', '/fulfillment/picking-plans'], ['fulfillment-tasks-new', '/fulfillment/tasks/new'], ['fulfillment-tasks-detay', '/fulfillment/picking-plans/:detay'],
  ['fulfillment-my-picking', '/fulfillment/my-picking'], ['fulfillment-desks', '/fulfillment/desks'], ['fulfillment-packing-stations', '/fulfillment/packing-stations'], ['fulfillment-cargo-reroute', '/fulfillment/cargo-reroute'],
  ['marketplaces', '/marketplaces'], ['marketplaces-detay', '/marketplaces/:detay'], ['marketplaces-eslestirme', '/marketplaces/eslestirme'], ['commission', '/commission'],
  ['accounts', '/accounts'], ['accounts-new', '/accounts/new'], ['accounts-detay', '/accounts/:detay'], ['accounts-groups', '/accounts/groups'],
  ['crm-members', '/crm/members'], ['crm-members-detay', '/crm/members/:detay'], ['crm-member-groups', '/crm/member-groups'],
  ['inventory-warehouses', '/inventory/warehouses'], ['inventory-warehouses-detay', '/inventory/warehouses/:detay'], ['inventory-stocks', '/inventory/stocks'], ['inventory-transfers', '/inventory/transfers'], ['inventory-transfers-detay', '/inventory/transfers/:detay'],
  ['promotion-campaigns', '/promotion/campaigns'], ['promotion-campaigns-detay', '/promotion/campaigns/:detay'], ['promotion-campaign-types', '/promotion/campaign-types'], ['promotion-coupons', '/promotion/coupons'],
  ['storefront-notifications', '/storefront/notifications'], ['storefront-newsletter', '/storefront/newsletter'], ['marketing-tracking', '/marketing/tracking'],
  ['cms-pages', '/cms/pages'], ['cms-pages-detay', '/cms/pages/:detay'],
  ['pos-sales', '/pos/sales'], ['pos-registers', '/pos/registers'], ['integrations-logs', '/integrations/logs'], ['finance-supplier-invoices', '/finance/supplier-invoices'],
  ['settings-firms', '/settings/firms'], ['settings-firms-detay', '/settings/firms/:detay'], ['settings-channels', '/settings/channels'], ['settings-platform-types', '/settings/platform-types'],
  ['settings-integration-services', '/settings/integration-services'], ['settings-notification-templates', '/settings/notification-templates'], ['settings-translations', '/settings/translations'],
  ['settings-languages', '/settings/languages'], ['settings-lookup-types', '/settings/lookup-types'], ['settings-migration', '/settings/migration'],
  ['settings-users', '/settings/users'], ['settings-roles', '/settings/roles'], ['settings-audit-logs', '/settings/audit-logs'],
];

const br = await chromium.launch({ executablePath: EXE, headless: true, args: ['--no-sandbox'] });
const ctx = await br.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1, ignoreHTTPSErrors: true, locale: 'tr-TR' });
const page = await ctx.newPage();
page.setDefaultTimeout(20000);

// Giriş
await page.goto(BASE + '/admin/login', { waitUntil: 'domcontentloaded' });
if (process.env.REHBER_TOKEN) {
  await page.evaluate(([a, r]) => { localStorage.setItem('access_token', a); if (r) localStorage.setItem('refresh_token', r); }, [process.env.REHBER_TOKEN, process.env.REHBER_REFRESH || '']);
} else if (process.env.REHBER_USER) {
  await page.fill('input[name="username"], input[type="text"], input[type="email"]', process.env.REHBER_USER);
  await page.fill('input[type="password"]', process.env.REHBER_PASS || '');
  await Promise.all([page.waitForURL(u => !u.pathname.endsWith('/login'), { timeout: 20000 }).catch(() => { }), page.keyboard.press('Enter')]);
} else { console.error('REHBER_USER/REHBER_PASS ya da REHBER_TOKEN verin.'); process.exit(2); }
await page.goto(BASE + '/admin/', { waitUntil: 'networkidle' });
if (page.url().includes('/login')) { console.error('Giriş başarısız (hâlâ /login).'); process.exit(3); }
// Sidebar'ı açık tut (varsa daraltılmış durumu sıfırla)
await page.evaluate(() => { try { localStorage.removeItem('sidebarCollapsed'); } catch { } });

let ok = 0, fail = 0; const rapor = [];
for (const [slug, yol] of ROTALAR) {
  if (FILTRE && !slug.includes(FILTRE)) continue;
  try {
    if (yol.includes(':detay')) {
      const listeYolu = yol.replace(/\/:detay$/, '');
      await page.goto(BASE + '/admin' + listeYolu, { waitUntil: 'networkidle' });
      await page.waitForTimeout(600);
      const satir = page.locator('table tbody tr, [data-row], .kart a, a[href*="' + listeYolu + '/"]').first();
      if (!(await satir.count())) throw new Error('liste satırı yok');
      const href = await satir.evaluate(el => el.tagName === 'A' ? el.getAttribute('href') : (el.querySelector('a') || {}).getAttribute?.('href') || null).catch(() => null);
      await Promise.all([page.waitForURL(u => u.pathname !== '/admin' + listeYolu && u.pathname.startsWith('/admin' + listeYolu.replace(/\/[^/]+$/, '')), { timeout: 8000 }).catch(() => { }), satir.click({ force: true })]);
      if (page.url().endsWith(listeYolu) && href) await page.goto(BASE + (href.startsWith('/admin') ? href : '/admin' + href), { waitUntil: 'networkidle' });
      if (page.url().endsWith(listeYolu)) throw new Error('detaya geçilemedi');
    } else {
      await page.goto(BASE + '/admin' + yol, { waitUntil: 'networkidle' });
    }
    await page.waitForTimeout(900);
    const png = path.join(IMG, slug + '.png');
    await page.screenshot({ path: png, fullPage: true });
    if (sharp) { await sharp(png).webp({ quality: 82 }).toFile(path.join(IMG, slug + '.webp')); fs.unlinkSync(png); }
    ok++; rapor.push('OK   ' + slug + '  ' + page.url().replace(BASE, ''));
  } catch (e) { fail++; rapor.push('FAIL ' + slug + '  ' + String(e.message).split('\n')[0].slice(0, 100)); }
}
await br.close();
console.log(rapor.join('\n')); console.log(`\n${ok} başarılı, ${fail} başarısız → ${IMG}`);
