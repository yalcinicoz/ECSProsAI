// DataGrid F3 doğrulaması — Excel export düğmesi: gövde (aynı filtre modeli + kolon listesi + named), indirme, 400 tavan ve 429 limit mesajları.
// Canlı /admin dist'i, API sahte (route) — kimlik gerekmez. Kullanım: CHROME_PATH=... node grid-f3-check.mjs
import { chromium } from 'playwright-core'

const BASE = process.env.BASE || 'https://www.misharitalia.com'
const ORDERS = Array.from({ length: 30 }, (_, i) => ({
  id: `00000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`, orderNumber: `MIS${1000 + i}`, memberId: null,
  status: 'shipped', paymentStatus: 'paid', grandTotal: 100 + i, currencyCode: 'TRY', createdAt: new Date(Date.now() - i * 3600_000).toISOString(),
  recipientName: 'Ayşe Yılmaz', paymentMethod: 'kart',
}))
const exportCalls = []
let exportMode = 'ok'

const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH, args: ['--no-sandbox'] })
const sonuc = []
const ok = (ad, kosul, ek = '') => { sonuc.push(`${kosul ? '✓' : '✗'} ${ad}${ek ? ' — ' + ek : ''}`); if (!kosul) process.exitCode = 1 }

const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 }, ignoreHTTPSErrors: true, acceptDownloads: true })
await ctx.addInitScript(() => {
  localStorage.setItem('ecspros-auth', JSON.stringify({ state: { accessToken: 'x', refreshToken: 'y', isAuthenticated: true,
    user: { id: 'u-test', email: 'test@x', fullName: 'Test Kullanıcı', permissions: ['*'], mustChangePassword: false } }, version: 0 }))
  localStorage.setItem('access_token', 'x'); localStorage.setItem('refresh_token', 'y')
})
await ctx.route('**/hubs/**', r => r.abort())
await ctx.route('**/api/**', async r => {
  const u = new URL(r.request().url()); const p = u.pathname
  if (p.endsWith('/api/auth/me')) return r.fulfill({ json: { success: true, data: { userId: 'u-test', email: 'test@x', fullName: 'Test', permissions: ['*'] } } })
  if (p.endsWith('/api/orders/export') && r.request().method() === 'POST') {
    exportCalls.push(JSON.parse(r.request().postData() || '{}'))
    if (exportMode === '429') return r.fulfill({ status: 429, contentType: 'application/json; charset=utf-8', body: JSON.stringify({ success: false, error: 'Dakikada en fazla 5 dışa aktarma yapılabilir. Lütfen biraz bekleyin.' }) })
    if (exportMode === '400') return r.fulfill({ status: 400, contentType: 'application/json; charset=utf-8', body: JSON.stringify({ success: false, error: 'Sonuç 123.456 satır; dışa aktarma sınırı 100.000. Filtreyi daraltın.' }) })
    return r.fulfill({ status: 200, contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      headers: { 'content-disposition': 'attachment; filename=siparisler-2026-09-08-1530.xlsx; filename*=UTF-8\'\'siparisler-2026-09-08-1530.xlsx' },
      body: Buffer.from('PKsahte-xlsx') })
  }
  if (p.endsWith('/api/orders/status-counts')) return r.fulfill({ json: { success: true, data: { pending: 0, confirmed: 0, processing: 0, shipped: 30 } } })
  if (p.endsWith('/api/orders')) {
    const page = +(u.searchParams.get('page') || 1), ps = +(u.searchParams.get('pageSize') || 20)
    return r.fulfill({ json: { success: true, data: { items: ORDERS.slice((page - 1) * ps, page * ps), totalCount: ORDERS.length, page, pageSize: ps, totalPages: 2 } } })
  }
  return r.fulfill({ json: { success: true, data: { items: [], totalCount: 0, page: 1, pageSize: 1, totalPages: 0 } } })
})
const page = await ctx.newPage()
page.on('pageerror', e => sonuc.push(`✗ sayfa hatası: ${e.message}`))

await page.goto(`${BASE}/admin/orders?tab=shipped&f.paid=eq:true&f.createdAt=between:2026-09-01,2026-09-08&fq.createdAt=custom&search=ayse&sort=total&dir=asc`, { waitUntil: 'networkidle' })
await page.waitForSelector('[data-grid-id="orders"] tbody tr')

// Kolonlar'dan ÖDEME'yi gizle → görünür export kümesi 5 kolon (orderNumber, customer, total, status, createdAt)
await page.click('button:has-text("Kolonlar")')
await page.click('[role="menu"] label:has-text("ÖDEME") input')
await page.keyboard.press('Escape'); await page.mouse.click(5, 5)

// 1) Yalnız görünür kolonlar → indirme
const dlPromise = page.waitForEvent('download', { timeout: 10_000 })
await page.click('button:has-text("Excel\'e aktar")')
await page.click('[role="menuitem"]:has-text("Yalnız görünür kolonlar")')
const dl = await dlPromise
ok('indirme tetiklendi, dosya adı Content-Disposition\'dan', dl.suggestedFilename() === 'siparisler-2026-09-08-1530.xlsx', dl.suggestedFilename())
const b1 = exportCalls[0]
ok('gövde: search/sort/dir aynı GridState', b1.search === 'ayse' && b1.sort === 'total' && b1.dir === 'asc', JSON.stringify(b1))
ok('gövde: filtreler (paid eq true, createdAt between)', b1.filters.length === 2 && b1.filters.some(f => f.field === 'paid' && f.op === 'eq' && f.value === 'true') && b1.filters.some(f => f.field === 'createdAt' && f.op === 'between'))
ok('gövde: görünür kolonlar (ÖDEME hariç, detay hariç)', JSON.stringify(b1.columns) === JSON.stringify(['orderNumber', 'customer', 'total', 'status', 'createdAt']), JSON.stringify(b1.columns))
ok('gövde: named.statuses = sekme', b1.named?.statuses === 'shipped', JSON.stringify(b1.named))
ok('gövde: sayfa/sayfa boyu YOK', b1.page === undefined && b1.pageSize === undefined)

// 2) Tüm kolonlar → columns boş (sunucu tümü)
const dl2 = page.waitForEvent('download', { timeout: 10_000 })
await page.click('button:has-text("Excel\'e aktar")')
await page.click('[role="menuitem"]:has-text("Tüm kolonlar")')
await dl2
ok('Tüm kolonlar → columns []', Array.isArray(exportCalls[1].columns) && exportCalls[1].columns.length === 0, JSON.stringify(exportCalls[1].columns))

// 3) 429 → mesaj
exportMode = '429'
await page.click('button:has-text("Excel\'e aktar")')
await page.click('[role="menuitem"]:has-text("Tüm kolonlar")')
await page.waitForSelector('[role="alert"]')
ok('429 → limit mesajı gösterildi', (await page.locator('[role="alert"]').innerText()).includes('Dakikada en fazla 5'))
await page.click('[role="alert"] button:has-text("kapat")')

// 4) 400 tavan → mesaj
exportMode = '400'
await page.click('button:has-text("Excel\'e aktar")')
await page.click('[role="menuitem"]:has-text("Yalnız görünür kolonlar")')
await page.waitForSelector('[role="alert"]')
ok('400 → tavan mesajı gösterildi', (await page.locator('[role="alert"]').innerText()).includes('100.000'))
ok('düğme yeniden etkin', await page.locator('button:has-text("Excel\'e aktar")').isEnabled())
ok('toplam 4 export isteği', exportCalls.length === 4)

await ctx.close(); await browser.close()
console.log(sonuc.join('\n'))
console.log(`\n${sonuc.filter(s => s.startsWith('✓')).length}/${sonuc.length} geçti`)
