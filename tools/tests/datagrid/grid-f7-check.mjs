// DataGrid sticky chrome düzeneği (2026-09-08): kolon başlıkları üstte (uygulama başlığının altında), sayfalama altta sabit kopya;
// ghost scrollbar sayfalamanın üstüne çıkar; yatay kaydırma başlık kopyasıyla eşlenir; kopyadaki sıralama tıklaması çalışır.
// Siparişler sayfası, 120 sahte sipariş, pageSize=100. Kullanım: CHROME_PATH=... node grid-f7-check.mjs
import { chromium } from 'playwright-core'

const BASE = process.env.BASE || 'https://www.misharitalia.com'
const ORDERS = Array.from({ length: 120 }, (_, i) => ({
  id: `00000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`, orderNumber: `MIS${String(1000 + i)}`,
  memberId: null, status: ['pending', 'confirmed', 'processing', 'shipped'][i % 4], paymentStatus: i % 3 ? 'paid' : 'unpaid',
  grandTotal: 100 + i * 37.5, currencyCode: 'TRY', createdAt: new Date(Date.now() - i * 5 * 3600_000).toISOString(),
  recipientName: ['Ayşe Yılmaz', 'Mehmet Demir', 'Zeynep Kaya'][i % 3], paymentMethod: ['kart', 'kapida-nakit', null][i % 3],
}))
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
    const u = new URL(r.request().url()); const p = u.pathname
    if (p.endsWith('/api/auth/me')) return r.fulfill({ json: { success: true, data: { userId: 'u-test', email: 'test@x', fullName: 'Test', permissions: ['*'] } } })
    if (p.endsWith('/api/orders/status-counts')) return r.fulfill({ json: { success: true, data: { pending: 30, confirmed: 30, processing: 30, shipped: 30 } } })
    if (p.endsWith('/api/orders')) {
      const ps = Number(u.searchParams.get('pageSize') || 20); const pg = Number(u.searchParams.get('page') || 1)
      return r.fulfill({ json: { success: true, data: { items: ORDERS.slice((pg - 1) * ps, pg * ps), totalCount: ORDERS.length, page: pg, pageSize: ps } } })
    }
    return r.fulfill({ json: { success: true, data: [] } })
  })
  const page = await ctx.newPage()
  const errs = []
  page.on('pageerror', e => errs.push(e.message))
  const calls = []
  page.on('request', r => { if (r.url().includes('/api/')) calls.push(r.url().replace(BASE, '')) })
  await page.goto(`${BASE}/admin/orders?pageSize=100`, { waitUntil: 'networkidle' })
  try { await page.waitForSelector('[data-grid-id="orders"] tbody tr, [data-grid-id="orders"] .grid-compact-row', { timeout: 10000 }) }
  catch { console.log('TANI', viewport.width, JSON.stringify({ calls, errs, txt: (await page.locator('body').innerText()).slice(0, 400) })) }
  return { ctx, page, errs }
}

const gh = '.grid-ghost-header', gp = '.grid-ghost-pagination[data-bottom-bar]', gs = '.grid-ghost-scroll'
const pagBelow = (page) => page.evaluate(() => { const els = [...document.querySelectorAll('[data-grid-id="orders"] .card > div')]; const p = els[els.length - 1]; return p.getBoundingClientRect().top >= window.innerHeight - 1 })
const click = async (page, sel) => { try { await page.locator(sel).click({ timeout: 3000 }); return true } catch { return false } }
for (const [adi, viewport] of [['tablet900', { width: 900, height: 700 }], ['desktop', { width: 1280, height: 800 }], ['mobil', { width: 390, height: 844 }]]) {
  const { ctx, page, errs } = await acilis(viewport)
  const tag = adi
  // en üstte: ghost başlık yok; ghost sayfalama yalnız gerçek sayfalama fold altındaysa (tasarım: hemen sabitlenir)
  ok(`${tag}: başta ghost başlık yok, sayfalama fold durumuna göre`, await page.locator(gh).count() === 0 && (await page.locator(gp).count()) === (await pagBelow(page) ? 1 : 0))
  await page.evaluate(() => window.scrollTo(0, 700)); await page.waitForTimeout(400)
  if (adi === 'mobil') {
    // mobil kompakt görünüm: tablo başlığı yok → ghost başlık beklenmez; ghost sayfalama çalışır
    ok(`${tag}: kompakt görünümde ghost başlık yok`, await page.locator(gh).count() === 0)
    await page.evaluate(() => window.scrollTo(0, 200)); await page.waitForTimeout(400)
    const below = await pagBelow(page)
    ok(`${tag}: ghost sayfalama yalnız gerçek sayfalama fold altındayken (${below ? 'altta' : 'görünür'})`, (await page.locator(gp).count()) === (below ? 1 : 0))
    if (below) {
      ok(`${tag}: ghost sayfalamadan sayfa 2`, await click(page, `${gp} button:has-text("2")`) && (await page.waitForTimeout(300), /page=2/.test(page.url())))
    }
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight)); await page.waitForTimeout(400)
    ok(`${tag}: en altta ghost sayfalama kaybolur`, await page.locator(gp).count() === 0)
    ok(`${tag}: gövde yatay taşmıyor`, await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
    ok(`${tag}: sayfa hatası yok`, errs.length === 0, errs.join(' | ').slice(0, 200))
    await ctx.close(); continue
  }
  ok(`${tag}: kaydırınca ghost başlık var`, await page.locator(gh).count() === 1)
  const geo = await page.evaluate(() => {
    const g = document.querySelector('.grid-ghost-header'); const hdr = document.querySelector('[data-top-bar]')
    const real = document.querySelector('[data-grid-id="orders"] .grid-scroll'); if (!g || !hdr || !real) return null
    const gr = g.getBoundingClientRect(), hr = hdr.getBoundingClientRect(), rr = real.getBoundingClientRect()
    const gths = [...g.querySelectorAll('th')].map(t => t.textContent.trim()); const rths = [...real.querySelectorAll('thead th')].map(t => t.textContent.trim())
    const gw = [...g.querySelectorAll('th')].map(t => Math.round(t.getBoundingClientRect().width)); const rw = [...real.querySelectorAll('thead th')].map(t => Math.round(t.getBoundingClientRect().width))
    return { top: Math.round(gr.top), hdrBottom: Math.round(hr.bottom), left: Math.round(gr.left), realLeft: Math.round(rr.left), width: Math.round(gr.width), realWidth: Math.round(rr.width), gths, rths, gw, rw }
  })
  ok(`${tag}: ghost başlık uygulama başlığının hemen altında`, !!geo && Math.abs(geo.top - geo.hdrBottom) <= 1, JSON.stringify({ top: geo?.top, hdr: geo?.hdrBottom }))
  ok(`${tag}: ghost başlık grid ile hizalı (sol/genişlik)`, !!geo && geo.left === geo.realLeft && geo.width === geo.realWidth)
  ok(`${tag}: kolon metinleri aynı`, !!geo && JSON.stringify(geo.gths) === JSON.stringify(geo.rths), JSON.stringify(geo?.gths))
  ok(`${tag}: kolon genişlikleri aynı (±1)`, !!geo && geo.gw.length === geo.rw.length && geo.gw.every((w, i) => Math.abs(w - geo.rw[i]) <= 1), JSON.stringify([geo?.gw, geo?.rw]))
  ok(`${tag}: ghost sayfalama altta`, await pagBelow(page) && await page.locator(gp).count() === 1 && await page.evaluate(() => Math.round(document.querySelector('.grid-ghost-pagination').getBoundingClientRect().bottom) === window.innerHeight))
  const overflow = await page.evaluate(() => { const r = document.querySelector('[data-grid-id="orders"] .grid-scroll'); return r.scrollWidth > r.clientWidth + 1 })
  if (overflow && adi !== 'mobil') {
    ok(`${tag}: ghost scrollbar sayfalamanın üstünde`, await page.evaluate(() => {
      const s = document.querySelector('.grid-ghost-scroll'), p = document.querySelector('.grid-ghost-pagination'); if (!s || !p) return false
      return Math.abs(s.getBoundingClientRect().bottom - p.getBoundingClientRect().top) <= 1
    }))
    await page.evaluate(() => { document.querySelector('[data-grid-id="orders"] .grid-scroll').scrollLeft = 120 }); await page.waitForTimeout(150)
    const sl = await page.evaluate(() => ({ real: document.querySelector('[data-grid-id="orders"] .grid-scroll').scrollLeft, ghost: document.querySelector('.grid-ghost-header').scrollLeft }))
    ok(`${tag}: yatay kaydırma ghost başlığa yansır`, sl.real > 0 && Math.abs(sl.real - sl.ghost) <= 1, JSON.stringify(sl))
    ok(`${tag}: ghost başlıkta sabit kolon sticky`, await page.evaluate(() => { const f = document.querySelector('.grid-ghost-header th.grid-frozen'); return !!f && getComputedStyle(f).position === 'sticky' && f.getBoundingClientRect().left === document.querySelector('.grid-ghost-header').getBoundingClientRect().left }))
  } else ok(`${tag}: yatay taşma yok (ghost scrollbar beklenmez)`, (await page.locator(gs).count()) === 0)
  // ghost başlıkta sıralama tıklaması
  ok(`${tag}: ghost başlıktan sıralama URL'ye yazıldı`, await click(page, `${gh} th:has-text("TUTAR")`) && (await page.waitForTimeout(300), /sort=total/.test(page.url())), page.url())
  // ghost sayfalamadan sayfa 2
  ok(`${tag}: ghost sayfalamadan sayfa 2`, await click(page, `${gp} button:has-text("2")`) && (await page.waitForTimeout(300), /page=2/.test(page.url())))
  await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight)); await page.waitForTimeout(400)
  ok(`${tag}: en altta ghost sayfalama kaybolur`, await page.locator(gp).count() === 0)
  await page.evaluate(() => window.scrollTo(0, 0)); await page.waitForTimeout(400)
  ok(`${tag}: en üstte ghost başlık kaybolur`, await page.locator(gh).count() === 0)
  ok(`${tag}: sayfa hatası yok`, errs.length === 0, errs.join(' | ').slice(0, 200))
  await ctx.close()
}
await browser.close()
console.log(sonuc.join('\n'))
console.log(`\n${sonuc.filter(s => s.startsWith('✓')).length}/${sonuc.length} geçti`)
