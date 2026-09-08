// DataGrid F5 doğrulaması — kaydedilmiş görünümler (kaydet/uygula/varsayılan otomatik uygula/sil), mobil kompakt görünüm (liste, genişletme, Detay, tablo geçişi).
// Canlı /admin dist'i, API sahte (route; tercihler bellekte tutulur) — kimlik gerekmez. Kullanım: CHROME_PATH=... node grid-f5-check.mjs
import { chromium } from 'playwright-core'

const BASE = process.env.BASE || 'https://www.misharitalia.com'
const ORDERS = Array.from({ length: 30 }, (_, i) => ({
  id: `00000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`, orderNumber: `MIS${1000 + i}`, memberId: null,
  status: i % 2 ? 'pending' : 'shipped', paymentStatus: i % 3 ? 'paid' : 'unpaid', grandTotal: 100 + i * 10, currencyCode: 'TRY',
  createdAt: new Date(Date.now() - i * 3600_000).toISOString(), recipientName: 'Ayşe Yılmaz', paymentMethod: 'kart',
}))
let prefs = {}          // sunucu tercih sözlüğü (bellek)
const puts = []

const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH, args: ['--no-sandbox'] })
const sonuc = []
const ok = (ad, kosul, ek = '') => { sonuc.push(`${kosul ? '✓' : '✗'} ${ad}${ek ? ' — ' + ek : ''}`); if (!kosul) process.exitCode = 1 }

async function acilis(viewport) {
  const ctx = await browser.newContext({ viewport, ignoreHTTPSErrors: true })
  await ctx.addInitScript(() => {
    localStorage.setItem('ecspros-auth', JSON.stringify({ state: { accessToken: 'x', refreshToken: 'y', isAuthenticated: true,
      user: { id: 'u-test', email: 'test@x', fullName: 'Test', permissions: ['*'], mustChangePassword: false } }, version: 0 }))
    localStorage.setItem('access_token', 'x'); localStorage.setItem('refresh_token', 'y')
  })
  await ctx.route('**/hubs/**', r => r.abort())
  await ctx.route('**/api/**', async r => {
    const u = new URL(r.request().url()); const p = u.pathname; const m = r.request().method()
    if (p.endsWith('/api/auth/me')) return r.fulfill({ json: { success: true, data: { userId: 'u-test', permissions: ['*'] } } })
    if (p.endsWith('/api/iam/users/me/preferences') && m === 'GET') return r.fulfill({ json: { success: true, data: prefs } })
    if (p.endsWith('/api/iam/users/me/preferences') && m === 'PUT') { const b = JSON.parse(r.request().postData() || '{}'); puts.push(b); if (b.value == null) delete prefs[b.key]; else prefs[b.key] = b.value; return r.fulfill({ json: { success: true } }) }
    if (p.endsWith('/api/orders/status-counts')) return r.fulfill({ json: { success: true, data: { pending: 15, confirmed: 0, processing: 0, shipped: 15 } } })
    if (p.endsWith('/api/orders')) {
      let all = ORDERS
      const st = u.searchParams.get('statuses'); if (st) { const s = st.split(','); all = all.filter(o => s.includes(o.status)) }
      const paid = u.searchParams.get('f.paid'); if (paid) all = all.filter(o => (o.paymentStatus === 'paid') === paid.endsWith('true'))
      const page = +(u.searchParams.get('page') || 1), ps = +(u.searchParams.get('pageSize') || 20)
      return r.fulfill({ json: { success: true, data: { items: all.slice((page - 1) * ps, page * ps), totalCount: all.length, page, pageSize: ps, totalPages: Math.ceil(all.length / ps) } } })
    }
    return r.fulfill({ json: { success: true, data: { items: [], totalCount: 0, page: 1, pageSize: 1, totalPages: 0 } } })
  })
  const page = await ctx.newPage()
  page.on('pageerror', e => sonuc.push(`✗ sayfa hatası: ${e.message}`))
  page.on('dialog', d => d.accept())
  return { ctx, page }
}

// ── Masaüstü: görünümler ──
{
  const { ctx, page } = await acilis({ width: 1280, height: 800 })
  await page.goto(`${BASE}/admin/orders?tab=pending&f.paid=eq:true`, { waitUntil: 'networkidle' })
  await page.waitForSelector('[data-grid-id="orders"] tbody tr')
  // kolon tercihi: ÖDEME gizle → görünümde saklanmalı
  await page.click('button:has-text("Kolonlar")'); await page.click('[role="menu"] label:has-text("ÖDEME") input'); await page.keyboard.press('Escape'); await page.mouse.click(5, 5)
  await page.click('button[aria-label="Görünüm"]')
  ok('görünüm menüsü boş başlar', (await page.locator('[role="menu"]').innerText()).includes('Henüz görünüm yok'))
  await page.click('[role="menuitem"]:has-text("Geçerli durumu görünüm olarak kaydet")')
  await page.fill('input[aria-label="Görünüm adı"]', 'Bekleyen ödenmiş')
  await page.keyboard.press('Enter')
  await page.waitForTimeout(400)
  const put1 = puts[0]
  ok('PUT /iam/users/me/preferences key=grids.orders', put1?.key === 'grids.orders', JSON.stringify(put1?.key))
  const v = put1?.value?.views?.[0]
  ok('görünüm: URL parametreleri (tab + f.paid) kaydedildi', v?.params?.tab === 'pending' && v?.params?.['f.paid'] === 'eq:true', JSON.stringify(v?.params))
  ok('görünüm: kolon tercihi (ÖDEME gizli) kaydedildi', Array.isArray(v?.prefs?.manualHidden) && v.prefs.manualHidden.includes('paymentStatus'), JSON.stringify(v?.prefs))
  ok('düğmede aktif görünüm adı', (await page.locator('button[aria-label="Görünüm"]').innerText()).includes('Bekleyen ödenmiş'))
  // filtreleri temizle → görünüm pasif → menüden uygula → URL geri gelir
  await page.click('text=Tümünü temizle'); await page.waitForTimeout(300)
  ok('filtre temizlenince görünüm pasif', !(await page.locator('button[aria-label="Görünüm"]').innerText()).includes('Bekleyen'))
  await page.click('button[aria-label="Görünüm"]'); await page.click('[role="menuitem"]:has-text("Bekleyen ödenmiş")'); await page.waitForTimeout(400)
  ok('görünüm uygulandı: URL tab=pending + f.paid', page.url().includes('tab=pending') && decodeURIComponent(page.url()).includes('f.paid=eq:true'), page.url())
  ok('görünüm uygulandı: çip görünür', await page.locator('.badge', { hasText: 'Ödemesi alınan: Evet' }).count() === 1)
  ok('görünüm uygulandı: ÖDEME gizli', !(await page.locator('[data-grid-id="orders"] thead th').allInnerTexts()).includes('ÖDEME'))
  // varsayılan yap → temiz girişte otomatik uygulanır
  await page.click('button[aria-label="Görünüm"]'); await page.click('button[aria-label="Varsayılan yap"]'); await page.waitForTimeout(300); await page.keyboard.press('Escape')
  ok('varsayılan PUT edildi', puts.at(-1)?.value?.defaultViewId === v?.id)
  await page.goto(`${BASE}/admin/orders`, { waitUntil: 'networkidle' }); await page.waitForTimeout(600)
  ok('temiz girişte varsayılan görünüm otomatik uygulandı (URL replace)', page.url().includes('tab=pending') && decodeURIComponent(page.url()).includes('f.paid=eq:true'), page.url())
  ok('otomatik uygulamada çipler görünür (gizli filtre yok)', await page.locator('[aria-label="Aktif filtreler"] .badge').count() >= 1)
  // paylaşılan link (parametreli giriş) varsayılanı EZMEZ
  await page.goto(`${BASE}/admin/orders?tab=shipped`, { waitUntil: 'networkidle' }); await page.waitForTimeout(500)
  ok('parametreli girişte varsayılan uygulanmaz', page.url().includes('tab=shipped') && !page.url().includes('f.paid'), page.url())
  // sil
  await page.click('button[aria-label="Görünüm"]'); await page.click('button[aria-label^="Görünümü sil"]'); await page.waitForTimeout(300)
  ok('görünüm silindi', puts.at(-1)?.value?.views?.length === 0)
  await ctx.close()
}

// ── Mobil: kompakt görünüm ──
{
  const { ctx, page } = await acilis({ width: 390, height: 844 })
  await page.goto(`${BASE}/admin/orders`, { waitUntil: 'networkidle' })
  await page.waitForSelector('[data-grid-id="orders"] [role="list"]')
  const items = page.locator('[data-grid-id="orders"] [role="listitem"]')
  ok('mobil: kompakt liste render (20 öğe)', await items.count() === 20, String(await items.count()))
  const first = items.first()
  ok('mobil: başlık sipariş no + sağda tutar', (await first.innerText()).includes('MIS1000') && (await first.innerText()).includes('₺'))
  await first.locator('button[aria-expanded]').click()
  ok('mobil: dokununca detay satırları açılır', await first.locator('text=Detay →').count() === 1 && (await first.innerText()).includes('MÜŞTERİ'))
  await page.click('button[aria-label="Tablo görünümüne geç"]'); await page.waitForTimeout(300)
  ok('mobil: Tablo geçişi → tablo render', await page.locator('[data-grid-id="orders"] table').count() === 1)
  ok('mobil: tercih localStorage (mobileView=table)', JSON.parse(await page.evaluate(() => localStorage.getItem('ecspros-grid:orders:u-test') || '{}')).mobileView === 'table')
  await page.click('button[aria-label="Kompakt görünüme geç"]'); await page.waitForTimeout(300)
  ok('mobil: Kompakt geri', await page.locator('[data-grid-id="orders"] [role="list"]').count() === 1)
  ok('mobil: gövde yatay taşmıyor', await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
  await ctx.close()
}

await browser.close()
console.log(sonuc.join('\n'))
console.log(`\n${sonuc.filter(s => s.startsWith('✓')).length}/${sonuc.length} geçti`)
