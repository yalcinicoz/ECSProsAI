// DataGrid kolon bazlı sabitleme düzeneği (2026-09-08): varsayılan kritik kolonlar sabit; kullanıcı Kolonlar menüsünden istediği kolonu
// sabitler / kritik kolonu sabitlikten çıkarır; sabitler sola alınır; tercih localStorage'da kalıcı; Varsayılana dön geri alır; mobilde raptiye yok.
// Kullanıcılar sayfası, API sahte. Kullanım: CHROME_PATH=... node grid-f8-check.mjs
import { chromium } from 'playwright-core'

const BASE = process.env.BASE || 'https://www.misharitalia.com'
const USERS = Array.from({ length: 30 }, (_, i) => ({
  id: `00000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`, username: `kullanici${i + 1}`, email: `kullanici${i + 1}@ornek.com`,
  firstName: 'Ayşe', lastName: 'Yılmaz', department: 'Depo', jobTitle: 'Uzman', phone: `05${String(300000000 + i)}`, isActive: true,
  lastLoginAt: new Date().toISOString(), roles: ['viewer'],
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
    if (p.endsWith('/api/iam/users')) return r.fulfill({ json: { success: true, data: { items: USERS.slice(0, 20), totalCount: 30, page: 1, pageSize: 20, totalPages: 2 } } })
    if (p.endsWith('/api/iam/roles')) return r.fulfill({ json: { success: true, data: [] } })
    return r.fulfill({ json: { success: true, data: { items: [], totalCount: 0, page: 1, pageSize: 1, totalPages: 0 } } })
  })
  const page = await ctx.newPage()
  const errs = []; page.on('pageerror', e => errs.push(e.message))
  await page.goto(`${BASE}/admin/settings/users`, { waitUntil: 'networkidle' })
  await page.waitForSelector('[data-grid-id="users"] tbody tr')
  return { ctx, page, errs }
}
const frozenHeads = (page) => page.locator('[data-grid-id="users"] thead th.grid-frozen').allInnerTexts()
const headOrder = (page) => page.locator('[data-grid-id="users"] thead th').allInnerTexts()
const openMenu = async (page) => { await page.click('button:has-text("Kolonlar")'); await page.waitForSelector('[role="menu"]') }

// ── masaüstü ──
{
  const { ctx, page, errs } = await acilis({ width: 1280, height: 800 })
  ok('varsayılan: yalnız kritik kolon (KULLANICI ADI) sabit', JSON.stringify(await frozenHeads(page)) === JSON.stringify(['KULLANICI ADI']), (await frozenHeads(page)).join('|'))
  await openMenu(page)
  ok('menüde her satırda raptiye', await page.locator('[role="menu"] button[data-pin]').count() >= 8)
  ok('eski "Kritik kolonları sabit tut" anahtarı yok', await page.locator('[role="menu"] label:has-text("Kritik kolonları sabit tut")').count() === 0)
  ok('kritik kolon raptiyesi basılı', await page.getAttribute('[role="menu"] button[data-pin="username"]', 'aria-pressed') === 'true')
  // E-POSTA'yı sabitle → iki sabit, e-posta sola alınır (kullanıcı adından sonra)
  await page.click('[role="menu"] button[data-pin="email"]'); await page.waitForTimeout(300)
  let fh = await frozenHeads(page); let ho = await headOrder(page)
  ok('E-POSTA sabitlenince 2 sabit kolon', JSON.stringify(fh) === JSON.stringify(['KULLANICI ADI', 'E-POSTA']), fh.join('|'))
  ok('sabit kolonlar solda (sıra: kullanıcı adı, e-posta, ...)', ho[0] === 'KULLANICI ADI' && ho[1] === 'E-POSTA', ho.join('|'))
  ok('sabit hücre sticky + left ofseti', await page.evaluate(() => { const t = document.querySelector('[data-grid-id="users"] tbody tr'); const c = t.querySelectorAll('td.grid-frozen'); return c.length === 2 && getComputedStyle(c[1]).position === 'sticky' && parseFloat(getComputedStyle(c[1]).left) > 0 }))
  // kritik kolonu sabitlikten çıkar
  await page.click('[role="menu"] button[data-pin="username"]'); await page.waitForTimeout(300)
  fh = await frozenHeads(page); ho = await headOrder(page)
  ok('kritik kolon sabitlikten çıkarılabilir → yalnız E-POSTA sabit', JSON.stringify(fh) === JSON.stringify(['E-POSTA']), fh.join('|'))
  ok('E-POSTA en sola geçti', ho[0] === 'E-POSTA', ho.join('|'))
  ok('kritik kolon yine gizlenemez (kilit)', await page.locator('[role="menu"] label:has-text("KULLANICI ADI") input').isDisabled())
  // kalıcılık: yenile
  await page.reload({ waitUntil: 'networkidle' }); await page.waitForSelector('[data-grid-id="users"] tbody tr')
  fh = await frozenHeads(page)
  ok('yenilemeden sonra tercih kalıcı (E-POSTA sabit)', JSON.stringify(fh) === JSON.stringify(['E-POSTA']), fh.join('|'))
  // olumsuz: çok geniş küme → bütçe (%40) aşanlar serbest, sayfa bozulmaz
  await openMenu(page)
  for (const k of ['name', 'phone', 'roles', 'lastLoginAt']) { await page.click(`[role="menu"] button[data-pin="${k}"]`); await page.waitForTimeout(120) }
  await page.waitForTimeout(300)
  const budget = await page.evaluate(() => { const box = document.querySelector('[data-grid-id="users"] .grid-scroll').clientWidth; const w = [...document.querySelectorAll('[data-grid-id="users"] thead th.grid-frozen')].reduce((a, t) => a + t.getBoundingClientRect().width, 0); return { box, w, count: document.querySelectorAll('[data-grid-id="users"] thead th.grid-frozen').length, pinned: document.querySelectorAll('[role="menu"] button[data-pin][aria-pressed="true"]').length, sigmiyor: [...document.querySelectorAll('[role="menu"] label span')].filter(s => s.textContent.includes('(sığmıyor)')).length } })
  ok('5 kolon sabitlenince genişlik bütçesi (%40) aşılmaz, aşanlar serbest', budget.w <= budget.box * 0.4 + 1 && budget.count < budget.pinned, JSON.stringify(budget))
  ok('sığmayanlar menüde işaretli', budget.sigmiyor >= 1, JSON.stringify(budget))
  // varsayılana dön
  await page.click('[role="menu"] button:has-text("Varsayılana dön")'); await page.waitForTimeout(300)
  fh = await frozenHeads(page)
  ok('Varsayılana dön → yalnız kritik kolon sabit', JSON.stringify(fh) === JSON.stringify(['KULLANICI ADI']), fh.join('|'))
  ok('sayfa hatası yok', errs.length === 0, errs.join(' | ').slice(0, 200))
  await ctx.close()
}
// ── mobil ──
{
  const { ctx, page, errs } = await acilis({ width: 390, height: 844 })
  await page.click('button[aria-haspopup="menu"]:has(svg)'); await page.waitForSelector('[role="menu"]')
  ok('mobil: raptiye yok (frozen kapalı)', await page.locator('[role="menu"] button[data-pin]').count() === 0)
  ok('mobil: frozen yok', await page.locator('[data-grid-id="users"] th.grid-frozen').count() === 0)
  ok('mobil: sayfa hatası yok', errs.length === 0)
  await ctx.close()
}
await browser.close()
console.log(sonuc.join('\n'))
console.log(`\n${sonuc.filter(s => s.startsWith('✓')).length}/${sonuc.length} geçti`)
