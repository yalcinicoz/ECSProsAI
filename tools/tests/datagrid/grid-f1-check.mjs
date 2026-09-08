// DataGrid F1 görsel/işlev doğrulaması — canlı /admin dist'i, API sahte (route) verisiyle; kimlik bilgisi gerekmez.
import { chromium } from 'playwright-core'
import { mkdirSync } from 'node:fs'

const BASE = process.env.BASE || 'https://www.misharitalia.com'
const OUT = process.env.OUT || './shots'
mkdirSync(OUT, { recursive: true })

const USERS = Array.from({ length: 60 }, (_, i) => ({
  id: `00000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`,
  username: `kullanici${i + 1}`, email: `kullanici${i + 1}@ornek-firma-uzun-alanadi.com.tr`,
  firstName: ['Ayşe', 'Mehmet', 'Zeynep', 'Ali'][i % 4], lastName: ['Yılmaz', 'Demirtaş', 'Kaya', 'Öztürk'][i % 4],
  department: ['Depo', 'Muhasebe', 'Müşteri Hizmetleri'][i % 3], jobTitle: 'Uzman', phone: i % 3 ? `05${String(300000000 + i)}` : null,
  isActive: i % 5 !== 0, lastLoginAt: new Date(Date.now() - i * 3600_000).toISOString(),
  roles: i % 2 ? ['super_admin', 'catalog_editor', 'order_operator'] : ['viewer'],
}))

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
    if (p.endsWith('/api/auth/me')) return r.fulfill({ json: { success: true, data: { userId: 'u-test', email: 'test@x', fullName: 'Test Kullanıcı', permissions: ['*'] } } })
    if (p.endsWith('/api/iam/users')) {
      const page = +(u.searchParams.get('page') || 1), ps = +(u.searchParams.get('pageSize') || 20)
      const q = (u.searchParams.get('search') || '').toLowerCase()
      const all = q ? USERS.filter(x => x.username.includes(q) || x.email.includes(q)) : USERS
      const items = all.slice((page - 1) * ps, page * ps)
      return r.fulfill({ json: { success: true, data: { items, totalCount: all.length, page, pageSize: ps, totalPages: Math.ceil(all.length / ps) } } })
    }
    if (p.endsWith('/api/iam/roles')) return r.fulfill({ json: { success: true, data: [] } })
    return r.fulfill({ json: { success: true, data: { items: [], totalCount: 0, page: 1, pageSize: 1, totalPages: 0 } } })
  })
  const page = await ctx.newPage()
  page.on('pageerror', e => sonuc.push(`✗ [${adi}] sayfa hatası: ${e.message}`))
  return { ctx, page }
}

// ── Masaüstü ──
{
  const { ctx, page } = await acilis({ width: 1280, height: 720 }, 'desktop')
  await page.goto(`${BASE}/admin/settings/users`, { waitUntil: 'networkidle' })
  await page.waitForSelector('[data-grid-id="users"] tbody tr')
  const rows = await page.locator('[data-grid-id="users"] tbody tr').count()
  ok('desktop: 20 satır (varsayılan sayfa boyu 20)', rows === 20, `satır=${rows}`)
  const ths = await page.locator('[data-grid-id="users"] thead th').allInnerTexts()
  ok('desktop: tüm priority kolonları görünür (dep/ünvan defaultVisible=false gizli)', ths.length === 8, ths.join('|'))
  const frozen = await page.locator('[data-grid-id="users"] thead th.grid-frozen').count()
  ok('desktop: kullanıcı adı frozen', frozen === 1, `frozen=${frozen}`)
  // sayfa boyu → 100 (tercih localStorage'a yazılır, URL'de yok)
  await page.selectOption('[data-grid-id="users"] select[aria-label="Sayfa boyu"]', '100')
  await page.waitForFunction(() => document.querySelectorAll('[data-grid-id="users"] tbody tr').length === 60)
  const pref = await page.evaluate(() => JSON.parse(localStorage.getItem('ecspros-grid:users:u-test') || '{}'))
  ok('sayfa boyu tercihi localStorage\'da (kullanıcıya özel anahtar)', pref.pageSize === 100, JSON.stringify(pref))
  ok('URL\'de pageSize yok', !page.url().includes('pageSize'), page.url())
  // dar pencere: yatay kaydırma + ghost scrollbar
  await page.setViewportSize({ width: 1024, height: 500 })
  await page.evaluate(() => { const el = document.querySelector('[data-grid-id="users"] .grid-scroll'); el.style.maxWidth = '700px' })
  await page.waitForTimeout(400)
  const scrollable = await page.evaluate(() => { const el = document.querySelector('[data-grid-id="users"] .grid-scroll'); return el.scrollWidth > el.clientWidth })
  ok('dar alanda tablo yatay kaydırılabilir', scrollable)
  const pos0 = await page.getAttribute('[data-grid-id="users"] .grid-scroll-wrap', 'data-scroll')
  ok('kenar ipucu: başlangıçta sağ gölge (data-scroll=start)', pos0 === 'start', pos0)
  await page.evaluate(() => window.scrollTo(0, 300))
  await page.waitForTimeout(400)
  const ghost = page.locator('.grid-ghost-scroll')
  ok('tablo ortasındayken ghost scrollbar görünür', await ghost.count() === 1 && await ghost.isVisible())
  const ghostCount = await ghost.count()
  ok('tek ghost bar', ghostCount <= 1, `adet=${ghostCount}`)
  await page.evaluate(() => { document.querySelector('.grid-ghost-scroll').scrollLeft = 250 })
  await page.waitForTimeout(300)
  const realLeft = await page.evaluate(() => document.querySelector('[data-grid-id="users"] .grid-scroll').scrollLeft)
  ok('ghost → gerçek senkron', Math.abs(realLeft - 250) <= 2, `real=${realLeft}`)
  await page.evaluate(() => { document.querySelector('[data-grid-id="users"] .grid-scroll').scrollLeft = 120 })
  await page.waitForTimeout(300)
  const ghostLeft = await page.evaluate(() => document.querySelector('.grid-ghost-scroll').scrollLeft)
  ok('gerçek → ghost senkron', Math.abs(ghostLeft - 120) <= 2, `ghost=${ghostLeft}`)
  const posMid = await page.getAttribute('[data-grid-id="users"] .grid-scroll-wrap', 'data-scroll')
  ok('ortada iki gölge (data-scroll=middle)', posMid === 'middle', posMid)
  const frozenLeft = await page.evaluate(() => getComputedStyle(document.querySelector('[data-grid-id="users"] tbody td.grid-frozen')).position)
  ok('frozen hücre sticky', frozenLeft === 'sticky', frozenLeft)
  await page.screenshot({ path: `${OUT}/f1-desktop-ghost.png` })
  // tablonun altına gelince ghost kaybolur
  await page.evaluate(() => document.querySelector('[data-grid-id="users"] .grid-scroll').scrollIntoView({ block: 'end' }))
  await page.waitForTimeout(500)
  ok('gerçek scrollbar görünürken ghost yok', await page.locator('.grid-ghost-scroll').count() === 0)
  // Kolonlar menüsü: e-postayı gizle, telefonu yukarı taşı
  await page.setViewportSize({ width: 1280, height: 720 })
  await page.evaluate(() => window.scrollTo(0, 0))
  await page.click('button:has-text("Kolonlar")')
  await page.click('[role="menu"] label:has-text("E-POSTA") input')
  await page.waitForTimeout(200)
  const ths2 = await page.locator('[data-grid-id="users"] thead th').allInnerTexts()
  ok('Kolonlar: E-POSTA gizlendi', !ths2.some(t => t.includes('E-POSTA')), ths2.join('|'))
  const lockDisabled = await page.locator('[role="menu"] label:has-text("KULLANICI ADI") input').isDisabled()
  ok('kritik kolon gizlenemez (disabled)', lockDisabled)
  await page.locator('[role="menu"] div:has(label:has-text("TELEFON")) button[aria-label="Yukarı taşı"]').first().click()
  await page.waitForTimeout(200)
  const ths3 = await page.locator('[data-grid-id="users"] thead th').allInnerTexts()
  ok('Kolonlar: TELEFON yukarı taşındı', ths3.indexOf('TELEFON') < ths3.indexOf('AD SOYAD') || ths3.indexOf('TELEFON') === 2, ths3.join('|'))
  await page.keyboard.press('Escape')
  await page.screenshot({ path: `${OUT}/f1-desktop.png` })
  // satır tıklama → modal; arama URL'de
  await page.click('[data-grid-id="users"] tbody tr >> nth=0')
  ok('satır tıklama → düzenleme modalı', await page.locator('text=Kaydet').count() > 0)
  await page.keyboard.press('Escape')
  await page.fill('input[placeholder*="Ad, e-posta"]', 'kullanici1')
  await page.keyboard.press('Enter')
  await page.waitForTimeout(500)
  ok('arama URL\'de (?search=)', page.url().includes('search=kullanici1'), page.url())
  const rowsQ = await page.locator('[data-grid-id="users"] tbody tr').count()
  ok('arama sonucu filtreli (11 kayıt: kullanici1, 10-19)', rowsQ === 11, `satır=${rowsQ}`)
  ok('sayaçta (filtreli)', (await page.locator('[data-grid-id="users"] .card').innerText()).includes('(filtreli)'))
  await page.goBack(); await page.waitForTimeout(400)
  ok('geri tuşu aramayı kaldırır', !page.url().includes('search='), page.url())
  await ctx.close()
}

// ── Tablet ──
{
  const { ctx, page } = await acilis({ width: 900, height: 700 }, 'tablet')
  await page.goto(`${BASE}/admin/settings/users`, { waitUntil: 'networkidle' })
  await page.waitForSelector('[data-grid-id="users"] tbody tr')
  const ths = await page.locator('[data-grid-id="users"] thead th').allInnerTexts()
  ok('tablet: priority 3 kolonlar gizli (TELEFON/SON GİRİŞ/düzenle yok)', !ths.includes('TELEFON') && !ths.includes('SON GİRİŞ'), ths.join('|'))
  // kullanıcı telefonu açıkça açar → mobilde de kalmalı [E5]
  await page.click('button:has-text("Kolonlar")')
  await page.click('[role="menu"] label:has-text("TELEFON") input')
  await page.keyboard.press('Escape')
  await page.waitForTimeout(200)
  ok('tablet: TELEFON manuel açıldı', (await page.locator('[data-grid-id="users"] thead th').allInnerTexts()).includes('TELEFON'))
  await page.screenshot({ path: `${OUT}/f1-tablet.png` })
  await page.setViewportSize({ width: 390, height: 844 })
  await page.waitForTimeout(400)
  const thsM = await page.locator('[data-grid-id="users"] thead th').allInnerTexts()
  ok('mobil: manuel açılan TELEFON korunur, E-POSTA (p2) gizli', thsM.includes('TELEFON') && !thsM.includes('E-POSTA'), thsM.join('|'))
  ok('mobil: frozen yok', await page.locator('[data-grid-id="users"] th.grid-frozen').count() === 0)
  ok('mobil: ghost yok (doğal kaydırma)', await page.locator('.grid-ghost-scroll').count() === 0)
  const scrollableM = await page.evaluate(() => { const el = document.querySelector('[data-grid-id="users"] .grid-scroll'); return el.scrollWidth > el.clientWidth })
  ok('mobil: tablo yatay kaydırılabilir (tüm kolonlara erişim)', scrollableM)
  const bodyScroll = await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)
  ok('mobil: sayfa gövdesi yatay taşmıyor', bodyScroll)
  await page.screenshot({ path: `${OUT}/f1-mobil.png` })
  await ctx.close()
}

await browser.close()
console.log(sonuc.join('\n'))
console.log(`\n${sonuc.filter(s => s.startsWith('✓')).length}/${sonuc.length} geçti`)
