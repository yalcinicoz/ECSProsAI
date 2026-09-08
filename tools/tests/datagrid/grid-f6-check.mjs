// DataGrid "tüm sütunlarda filtre" düzeneği (2026-09-08): 17 DataGrid sayfasında başlığı olan her <th>'de HeaderFilterButton bulunmalı
// (boş başlıklı aksiyon/detay kolonları hariç). Masaüstü 1280; API sahte, kimlik gerekmez. Kullanım: CHROME_PATH=... node grid-f6-check.mjs
import { chromium } from 'playwright-core'

const BASE = process.env.BASE || 'https://www.misharitalia.com'
const PAGES = [
  '/admin/catalog/products', '/admin/crm/members', '/admin/crm/tickets', '/admin/inventory/stocks?search=abc',
  '/admin/orders', '/admin/orders/invoices', '/admin/orders/returns', '/admin/orders/quotes', '/admin/orders/gift-cards',
  '/admin/promotion/campaigns', '/admin/settings/users', '/admin/settings/audit-logs', '/admin/integrations/logs',
  '/admin/fulfillment/picking-plans', '/admin/pos/sales', '/admin/finance/supplier-invoices', '/admin/marketing/tracking?tab=outbox',
]
const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH, args: ['--no-sandbox'] })
const sonuc = []
const ok = (ad, kosul, ek = '') => { sonuc.push(`${kosul ? '✓' : '✗'} ${ad}${ek ? ' — ' + ek : ''}`); if (!kosul) process.exitCode = 1 }
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 }, ignoreHTTPSErrors: true })
await ctx.addInitScript(() => {
  localStorage.setItem('ecspros-auth', JSON.stringify({ state: { accessToken: 'x', refreshToken: 'y', isAuthenticated: true,
    user: { id: 'u-test', email: 'test@x', fullName: 'Test', permissions: ['*'], mustChangePassword: false } }, version: 0 }))
  localStorage.setItem('access_token', 'x'); localStorage.setItem('refresh_token', 'y')
})
await ctx.route('**/hubs/**', r => r.abort())
await ctx.route('**/api/**', async r => {
  const u = new URL(r.request().url()); const p = u.pathname
  if (p.endsWith('/api/auth/me')) return r.fulfill({ json: { success: true, data: { userId: 'u-test', email: 'test@x', fullName: 'Test', permissions: ['*'] } } })
  if (u.searchParams.has('page') || /\/api\/(iam\/users|product-questions|crm\/tickets\/notifications|store-notifications)/.test(p))
    return r.fulfill({ json: { success: true, data: { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0, counters: [], statusCounts: {} } } })
  if (p.endsWith('/facets')) return r.fulfill({ json: { success: true, data: { variants: [], warehouses: [], sections: [], bins: [] } } })
  if (/crm\/tickets\/settings/.test(p)) return r.fulfill({ json: { success: true, data: { statuses: [], subjects: [], types: [] } } })
  if (/firm-platforms|channels/.test(p)) return r.fulfill({ json: { success: true, data: [{ id: 'fp1', code: 'web', name: 'Web', nameI18n: { tr: 'Web' }, isActive: true }] } })
  return r.fulfill({ json: { success: true, data: [] } })
})
for (const path of PAGES) {
  const page = await ctx.newPage()
  await page.goto(BASE + path, { waitUntil: 'networkidle' }).catch(() => {})
  await page.waitForTimeout(600)
  // Tüm kolonları aç (gizli olanların başlığı DOM'da olmasın diye değil — sadece görünür başlıkları ölçüyoruz; priority ile gizlenenler masaüstünde açık)
  const info = await page.evaluate(() => {
    const ths = [...document.querySelectorAll('table thead th[data-key]')]
    const named = ths.filter(th => (th.textContent || '').trim().length > 0)
    const eksik = named.filter(th => !th.querySelector('button')).map(th => (th.textContent || '').trim())
    return { toplam: named.length, eksik }
  })
  ok(`${path}: ${info.toplam} başlıklı kolon, hepsinde filtre`, info.toplam > 0 && info.eksik.length === 0, info.eksik.join(', ') || (info.toplam === 0 ? 'grid yok' : ''))
  await page.close()
}
await browser.close()
console.log(sonuc.join('\n'))
console.log(`\n${sonuc.filter(s => s.startsWith('✓')).length}/${sonuc.length} geçti`)
