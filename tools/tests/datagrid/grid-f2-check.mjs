// DataGrid F2 doğrulaması — Siparişler pilotu: sekmeler+sayaç tutarlılığı, FilterBar (arama debounce, hızlı/gelişmiş, çipler,
// tarih hızlı seçimi, çoklu enum, sayı, boolean), sıralama başlıkları, URL/geri tuşu, mobil "Filtreler (n)" + bottom sheet.
// Canlı /admin dist'i, API sahte (route) — kimlik gerekmez. Kullanım: CHROME_PATH=... OUT=./shots node grid-f2-check.mjs
import { chromium } from 'playwright-core'
import { mkdirSync } from 'node:fs'

const BASE = process.env.BASE || 'https://www.misharitalia.com'
const OUT = process.env.OUT || './shots'
mkdirSync(OUT, { recursive: true })

const STATUSES = ['pending', 'confirmed', 'processing', 'shipped', 'delivered', 'cancelled']
const ORDERS = Array.from({ length: 120 }, (_, i) => ({
  id: `00000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`, orderNumber: `MIS${String(1000 + i)}`,
  memberId: null, status: STATUSES[i % 6], paymentStatus: i % 3 ? 'paid' : 'unpaid', grandTotal: 100 + i * 37.5, currencyCode: 'TRY',
  createdAt: new Date(Date.now() - i * 5 * 3600_000).toISOString(), recipientName: ['Ayşe Yılmaz', 'Mehmet Demir', 'Zeynep Kaya'][i % 3],
  paymentMethod: ['kart', 'kapida-nakit', null][i % 3],
}))
const calls = []

function applyFilters(list, u) {
  let out = list
  const st = u.searchParams.get('statuses'); if (st) { const s = st.split(','); out = out.filter(o => s.includes(o.status)) }
  const q = (u.searchParams.get('search') || '').toLowerCase(); if (q) out = out.filter(o => o.orderNumber.toLowerCase().includes(q) || o.recipientName.toLowerCase().includes(q))
  for (const [k, v] of u.searchParams) {
    if (!k.startsWith('f.')) continue
    const field = k.slice(2); const [op, val] = v.includes(':') ? [v.slice(0, v.indexOf(':')), v.slice(v.indexOf(':') + 1)] : ['auto', v]
    if (field === 'paid') out = out.filter(o => (o.paymentStatus === 'paid') === (val === 'true'))
    if (field === 'total' && op === 'gt') out = out.filter(o => o.grandTotal > +val)
    if (field === 'paymentStatus') { const s = val.split(','); out = out.filter(o => s.includes(o.paymentStatus)) }
    if (field === 'customer') out = out.filter(o => o.recipientName.toLowerCase().includes(val.toLowerCase()))
    if (field === 'createdAt' && op === 'between') { const [a, b] = val.split(','); out = out.filter(o => o.createdAt.slice(0, 10) >= a && o.createdAt.slice(0, 10) <= b) }
  }
  const sort = u.searchParams.get('sort'); const dir = u.searchParams.get('dir') === 'asc' ? 1 : -1
  if (sort === 'total') out = [...out].sort((a, b) => (a.grandTotal - b.grandTotal) * dir)
  else if (sort === 'createdAt' || !sort) out = [...out].sort((a, b) => (a.createdAt < b.createdAt ? -1 : 1) * dir)
  return out
}

const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH, args: ['--no-sandbox'] })
const sonuc = []
const ok = (ad, kosul, ek = '') => { sonuc.push(`${kosul ? '✓' : '✗'} ${ad}${ek ? ' — ' + ek : ''}`); if (!kosul) process.exitCode = 1 }

async function acilis(viewport, adi) {
  const ctx = await browser.newContext({ viewport, ignoreHTTPSErrors: true })
  await ctx.addInitScript(() => {
    localStorage.setItem('ecspros-auth', JSON.stringify({ state: { accessToken: 'x', refreshToken: 'y', isAuthenticated: true,
      user: { id: 'u-test', email: 'test@x', fullName: 'Test Kullanıcı', permissions: ['*'], mustChangePassword: false } }, version: 0 }))
    localStorage.setItem('access_token', 'x'); localStorage.setItem('refresh_token', 'y')
  })
  await ctx.route('**/hubs/**', r => r.abort())
  await ctx.route('**/api/**', async r => {
    const u = new URL(r.request().url()); const p = u.pathname
    calls.push(p + u.search)
    if (p.endsWith('/api/auth/me')) return r.fulfill({ json: { success: true, data: { userId: 'u-test', email: 'test@x', fullName: 'Test Kullanıcı', permissions: ['*'] } } })
    if (p.endsWith('/api/orders/status-counts')) {
      const base = applyFilters(ORDERS, u)
      const d = {}; for (const s of ['pending', 'confirmed', 'processing', 'shipped']) d[s] = base.filter(o => o.status === s).length
      return r.fulfill({ json: { success: true, data: d } })
    }
    if (p.endsWith('/api/orders')) {
      if (u.searchParams.get('f.hackerField')) return r.fulfill({ status: 400, json: { success: false, error: 'Geçersiz filtre alanı: hackerField' } })
      const all = applyFilters(ORDERS, u)
      const page = +(u.searchParams.get('page') || 1), ps = +(u.searchParams.get('pageSize') || 20)
      return r.fulfill({ json: { success: true, data: { items: all.slice((page - 1) * ps, page * ps), totalCount: all.length, page, pageSize: ps, totalPages: Math.ceil(all.length / ps) } } })
    }
    return r.fulfill({ json: { success: true, data: { items: [], totalCount: 0, page: 1, pageSize: 1, totalPages: 0 } } })
  })
  const page = await ctx.newPage()
  page.on('pageerror', e => sonuc.push(`✗ [${adi}] sayfa hatası: ${e.message}`))
  return { ctx, page }
}

const lastCall = (path) => [...calls].reverse().find(c => c.startsWith(path) && !c.includes('status-counts')) || ''
const rowsOf = (page) => page.locator('[data-grid-id="orders"] tbody tr')

// ── Masaüstü ──
{
  const { ctx, page } = await acilis({ width: 1280, height: 800 }, 'desktop')
  await page.goto(`${BASE}/admin/orders`, { waitUntil: 'networkidle' })
  await page.waitForSelector('[data-grid-id="orders"] tbody tr')
  ok('aktif sekme varsayılan: statuses=pending,confirmed,processing,shipped', lastCall('/api/orders?').includes('statuses=pending%2Cconfirmed%2Cprocessing%2Cshipped') || lastCall('/api/orders?').includes('statuses=pending,confirmed'), lastCall('/api/orders?'))
  ok('varsayılan sıralama başlığı: TARİH azalan (aria-sort)', await page.locator('th[aria-sort="descending"]').innerText() === 'TARİH')
  const frozen = await page.locator('[data-grid-id="orders"] thead th.grid-frozen').allInnerTexts()
  ok('frozen: SİPARİŞ NO + MÜŞTERİ', frozen.length === 2 && frozen[0].includes('SİPARİŞ NO') && frozen[1].includes('MÜŞTERİ'), frozen.join('|'))
  // hızlı filtre: tarih Son 7 gün → URL f.createdAt + fq, çip "Tarih: Son 7 gün"
  await page.selectOption('select[aria-label="Tarih"]', 'last7')
  await page.waitForTimeout(400)
  ok('tarih hızlı seçimi URL\'de (f.createdAt=between + fq.createdAt=last7)', page.url().includes('f.createdAt=between') && page.url().includes('fq.createdAt=last7'), page.url())
  ok('çip: Tarih: Son 7 gün', await page.locator('[aria-label="Aktif filtreler"] .badge', { hasText: 'Tarih: Son 7 gün' }).count() === 1)
  // boolean hızlı: Ödemesi alınan = Evet
  await page.selectOption('select[aria-label="Ödemesi alınan"]', 'true')
  await page.waitForTimeout(400)
  ok('boolean filtre API\'ye f.paid=eq:true', lastCall('/api/orders?').includes('f.paid=eq%3Atrue'), lastCall('/api/orders?'))
  // sayaçlar aynı filtrelerle
  const cnt = [...calls].reverse().find(c => c.includes('status-counts'))
  ok('sayaç ucu listeyle aynı filtreleri alır (f.paid + f.createdAt, page yok)', cnt.includes('f.paid=eq%3Atrue') && cnt.includes('f.createdAt') && !cnt.includes('page='), cnt)
  // sütun başlığı filtreleri (2026-09-08 kararı): ÖDEME başlığı → ödeme durumu çoklu seçim; TUTAR başlığı → tutar > 1000
  await page.click('button[aria-label="ÖDEME filtresi"]')
  ok('başlık filtresi penceresi açıldı', await page.locator('[role="dialog"][aria-label="ÖDEME filtresi"]').count() === 1)
  await page.click('[role="dialog"] button[aria-label="Ödeme durumu"]')
  await page.click('[role="listbox"] label:has-text("Ödendi") input')
  await page.click('[role="listbox"] label:has-text("Ödenmedi") input')
  await page.click('[role="dialog"] button:has-text("Tamam")')
  await page.waitForTimeout(400)
  ok('pencere kapandı', await page.locator('[role="dialog"][aria-label="ÖDEME filtresi"]').count() === 0)
  ok('çoklu enum: f.paymentStatus=in:paid,unpaid', decodeURIComponent(page.url()).includes('f.paymentStatus=in:paid,unpaid'), page.url())
  ok('çip: Ödeme durumu: Ödendi, Ödenmedi', await page.locator('.badge', { hasText: 'Ödeme durumu: Ödendi, Ödenmedi' }).count() === 1)
  await page.click('button[aria-label="TUTAR filtresi"]')
  await page.selectOption('[role="dialog"] select[aria-label="Tutar operatör"]', 'gt')
  await page.fill('[role="dialog"] input[aria-label="Tutar"]', '1000'); await page.keyboard.press('Enter')
  await page.click('[role="dialog"] button:has-text("Tamam")')
  await page.waitForTimeout(400)
  ok('sayı filtresi f.total=gt:1000', decodeURIComponent(page.url()).includes('f.total=gt:1000'), page.url())
  ok('aktif başlık ikonu marka renginde (ÖDEME)', await page.locator('button[aria-label="ÖDEME filtresi"] svg[fill="currentColor"]').count() === 1)
  ok('4 çip', await page.locator('[aria-label="Aktif filtreler"] .badge').count() === 4)
  ok('Gelişmiş paneli yok', await page.locator('button:has-text("Gelişmiş")').count() === 0)
  const totalsOk = await rowsOf(page).evaluateAll(trs => trs.every(tr => parseFloat(tr.children[2].textContent.replace(/\./g, '').replace(',', '.')) > 1000))
  ok('sonuçlar tutar > 1000', totalsOk)
  await page.screenshot({ path: `${OUT}/f2-desktop-filters.png` })
  // çipten tek tek kaldırma
  await page.click('button[aria-label^="Filtreyi kaldır: Tarih"]')
  await page.waitForTimeout(300)
  ok('çip kaldırınca tarih filtresi URL\'den gitti', !page.url().includes('f.createdAt'), page.url())
  // sıralama: TUTAR başlığı → asc → desc
  await page.click('th:has-text("TUTAR")'); await page.waitForTimeout(400)
  ok('TUTAR tıkla → sort=total&dir=asc', page.url().includes('sort=total') && page.url().includes('dir=asc'), page.url())
  const asc = await rowsOf(page).evaluateAll(trs => trs.map(tr => parseFloat(tr.children[2].textContent.replace(/\./g, '').replace(',', '.'))))
  ok('satırlar artan', asc.every((v, i) => i === 0 || v >= asc[i - 1]))
  await page.click('th:has-text("TUTAR")'); await page.waitForTimeout(300)
  ok('ikinci tık → desc', page.url().includes('dir=desc'))
  // global arama debounce (yazınca 400 ms sonra, Enter'sız)
  await page.fill('input[aria-label="Ara"]', 'MIS10')
  await page.waitForTimeout(150)
  ok('debounce: 150 ms\'de henüz istek yok', !lastCall('/api/orders?').includes('search=MIS10'))
  await page.waitForTimeout(500)
  ok('debounce: 400 ms sonra istek gitti', lastCall('/api/orders?').includes('search=MIS10'), lastCall('/api/orders?'))
  ok('çip: Arama', await page.locator('.badge', { hasText: 'Arama: "MIS10"' }).count() === 1)
  // Tümünü temizle
  await page.click('text=Tümünü temizle')
  await page.waitForTimeout(300)
  ok('Tümünü temizle: f.* ve search yok (sort ve tab kalır)', !page.url().includes('f.') && !page.url().includes('search=') && page.url().includes('sort=total'), page.url())
  // sekme: Teslim (heavy) → son 30 gün otomatik + tab param
  await page.click('button.stab:has-text("Teslim")'); await page.waitForTimeout(400)
  ok('Teslim sekmesi: tab=delivered + fq.createdAt=last30 (tek URL güncellemesi)', page.url().includes('tab=delivered') && page.url().includes('fq.createdAt=last30'), page.url())
  ok('çip: Tarih: Son 30 gün', await page.locator('.badge', { hasText: 'Tarih: Son 30 gün' }).count() === 1)
  ok('liste statuses=delivered', lastCall('/api/orders?').includes('statuses=delivered'))
  await page.goBack(); await page.waitForTimeout(400)
  ok('geri tuşu: aktif sekmeye döner', !page.url().includes('tab=delivered'), page.url())
  // sunucu 400 (beyaz liste) → hata satırı, çökme yok
  await page.goto(`${BASE}/admin/orders?f.hackerField=eq:1`, { waitUntil: 'networkidle' })
  await page.waitForTimeout(500)
  ok('bilinmeyen filtre 400 → sayfa çökmedi, tablo boş/uyarı', await page.locator('[data-grid-id="orders"]').count() === 1)
  await ctx.close()
}

// ── Mobil ──
{
  const { ctx, page } = await acilis({ width: 390, height: 844 }, 'mobil')
  await page.goto(`${BASE}/admin/orders?f.paid=eq:true&f.createdAt=between:2026-09-01,2026-09-08&fq.createdAt=custom`, { waitUntil: 'networkidle' })
  await page.waitForSelector('[data-grid-id="orders"]')
  const btn = page.locator('button:has-text("Filtreler")')
  ok('mobil: "Filtreler" düğmesi rozet 2', (await btn.innerText()).replace(/\s+/g, ' ').includes('2'), await btn.innerText())
  ok('mobil: bottom sheet kapalıyken çipler görünür', await page.locator('[aria-label="Aktif filtreler"] .badge').count() === 2)
  ok('mobil: özel tarih çipi gg.aa.yyyy', await page.locator('.badge', { hasText: '01.09.2026 – 08.09.2026' }).count() === 1)
  await btn.click()
  ok('mobil: bottom sheet açıldı', await page.locator('[role="dialog"]').count() === 1)
  await page.selectOption('[role="dialog"] select[aria-label="Ödemesi alınan"]', 'false')
  await page.click('[role="dialog"] button:has-text("Uygula")')
  await page.waitForTimeout(400)
  ok('mobil: sheet\'ten filtre değişti (f.paid=eq:false)', decodeURIComponent(page.url()).includes('f.paid=eq:false'), page.url())
  // F5: mobilde varsayılan kompakt görünüm → tabloya geçip kolonları kontrol et
  ok('mobil: varsayılan kompakt liste', await page.locator('[data-grid-id="orders"] [role="list"]').count() === 1)
  await page.click('button[aria-label="Tablo görünümüne geç"]'); await page.waitForTimeout(300)
  const thsM = await page.locator('[data-grid-id="orders"] thead th').allInnerTexts()
  ok('mobil: p1 kolonlar (SİPARİŞ NO, MÜŞTERİ, TUTAR, DURUM)', thsM.length === 4, thsM.join('|'))
  ok('mobil: gövde yatay taşmıyor', await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
  await page.screenshot({ path: `${OUT}/f2-mobil.png` })
  await ctx.close()
}

await browser.close()
console.log(sonuc.join('\n'))
console.log(`\n${sonuc.filter(s => s.startsWith('✓')).length}/${sonuc.length} geçti`)
