// DataGrid F4 duman düzeneği — 7 göç edilmiş sayfa: grid render, FilterBar/Kolonlar/Excel düğmeleri, sayfa hatası yok,
// liste isteğinde page/pageSize + (varsa) sort parametreleri, mobil gövde taşması yok. API sahte (route), kimlik gerekmez.
import { chromium } from 'playwright-core'

const BASE = process.env.BASE || 'https://www.misharitalia.com'
const PAGES = [
  { path: '/admin/catalog/products', gridId: 'products', list: '/api/catalog/products' },
  { path: '/admin/crm/members', gridId: 'members', list: '/api/crm/members' },
  { path: '/admin/crm/tickets', gridId: 'tickets', list: '/api/crm/tickets' },
  { path: '/admin/inventory/stocks?search=abc', gridId: 'stocks', list: '/api/inventory/stocks/admin-list' },   // liste arama/depo seçilince yüklenir (tasarım)
  { path: '/admin/orders/invoices', gridId: 'invoices', list: '/api/orders/invoices' },
  { path: '/admin/orders/returns', gridId: 'returns', list: '/api/orders/returns' },
  { path: '/admin/promotion/campaigns', gridId: 'campaigns', list: '/api/promotion/campaigns' },
  // F4 kalan: eski DataTable+Pager sayfaları (backend grid desteği yok → sıralama/filtre/export beklenmez)
  { path: '/admin/settings/audit-logs', gridId: 'audit-logs', list: '/api/iam/audit-logs', basic: true },
  { path: '/admin/integrations/logs', gridId: 'integration-logs', list: '/api/integrations/logs', basic: true },
  { path: '/admin/fulfillment/picking-plans', gridId: 'picking-plans', list: '/api/fulfillment/picking-plans', basic: true },
  { path: '/admin/pos/sales', gridId: 'pos-sales', list: '/api/pos/sales', basic: true },
  { path: '/admin/orders/quotes', gridId: 'quotes', list: '/api/orders/quotes', basic: true },
  { path: '/admin/finance/supplier-invoices', gridId: 'supplier-invoices', list: '/api/finance/supplier-invoices', basic: true },
  { path: '/admin/orders/gift-cards', gridId: 'gift-cards', list: '/api/orders/gift-cards', basic: true },
]
const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH, args: ['--no-sandbox'] })
const sonuc = []
const ok = (ad, kosul, ek = '') => { sonuc.push(`${kosul ? '✓' : '✗'} ${ad}${ek ? ' — ' + ek : ''}`); if (!kosul) process.exitCode = 1 }

for (const viewport of [{ width: 1280, height: 800 }, { width: 390, height: 844 }]) {
  const mob = viewport.width < 768
  for (const pg of PAGES) {
    const calls = []
    const ctx = await browser.newContext({ viewport, ignoreHTTPSErrors: true })
    await ctx.addInitScript(() => {
      localStorage.setItem('ecspros-auth', JSON.stringify({ state: { accessToken: 'x', refreshToken: 'y', isAuthenticated: true,
        user: { id: 'u-test', email: 'test@x', fullName: 'Test', permissions: ['*'], mustChangePassword: false } }, version: 0 }))
      localStorage.setItem('access_token', 'x'); localStorage.setItem('refresh_token', 'y')
    })
    await ctx.route('**/hubs/**', r => r.abort())
    await ctx.route('**/api/**', async r => {
      const u = new URL(r.request().url()); const p = u.pathname
      calls.push(p + u.search)
      if (p.endsWith('/api/auth/me')) return r.fulfill({ json: { success: true, data: { userId: 'u-test', email: 'test@x', fullName: 'Test', permissions: ['*'] } } })
      if (p.endsWith(pg.list) && u.searchParams.has('page'))
        return r.fulfill({ json: { success: true, data: { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0, counters: [], statusCounts: {} } } })
      if (p.endsWith('/facets')) return r.fulfill({ json: { success: true, data: { variants: [], warehouses: [], sections: [], bins: [] } } })
      // diğer yardımcı uçlar: pageSize isteyenler sayfalı şekil (items), diğerleri boş dizi
      if (/\/api\/(iam\/users|product-questions|crm\/tickets\/notifications|store-notifications)/.test(p)) return r.fulfill({ json: { success: true, data: { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 } } })
      return r.fulfill({ json: { success: true, data: [] } })
    })
    const page = await ctx.newPage()
    const errs = []
    page.on('pageerror', e => errs.push(e.message)); page.on('console', m => { const t = m.text(); if (m.type() === 'error' && !/negotiation|ERR_FAILED|hubs|WebSocket|SignalR/i.test(t)) errs.push('console: ' + t.slice(0, 160)) })
    await page.goto(`${BASE}${pg.path}`, { waitUntil: 'networkidle' })
    const grid = page.locator(`[data-grid-id="${pg.gridId}"]`)
    const tag = `${mob ? 'mobil' : 'desktop'} ${pg.gridId}`
    ok(`${tag}: grid render`, await grid.count() === 1)
    ok(`${tag}: sayfa hatası yok`, errs.length === 0, errs.join(' | ').slice(0, 200))
    const listCall = calls.find(c => c.startsWith(pg.list + '?'))
    ok(`${tag}: liste isteği page/pageSize ile`, !!listCall && listCall.includes('page=') && listCall.includes('pageSize='), listCall ?? 'istek yok')
    ok(`${tag}: Kolonlar menüsü`, await page.locator('button[aria-haspopup="menu"]:has(svg)', { hasText: mob ? '' : 'Kolonlar' }).count() >= 1)
    if (!pg.basic) {
      ok(`${tag}: Excel düğmesi`, await page.locator('button:has-text("Excel")').count() + (mob ? await page.locator('button[aria-haspopup="menu"]').count() : 0) >= 1)
      ok(`${tag}: FilterBar (arama veya filtre)`, await page.locator('input[aria-label="Ara"], button:has-text("Filtreler"), button:has-text("Gelişmiş"), select').count() >= 1)
    }
    const txt = await grid.count() ? await grid.innerText() : ''
    ok(`${tag}: boş durum metni`, /bulunamadı|yok|kayıt|eklenmemiş|arayın/i.test(txt))
    if (mob) ok(`${tag}: gövde yatay taşmıyor`, await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
    await ctx.close()
  }
}
await browser.close()
console.log(sonuc.join('\n'))
console.log(`\n${sonuc.filter(s => s.startsWith('✓')).length}/${sonuc.length} geçti`)
