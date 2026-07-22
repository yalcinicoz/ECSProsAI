// H7 kapanış regresyonu — Faz H yüzeylerinin uçtan uca dumanı (2026-07-22).
// Eski faz suite'leri /tmp scratchpad'lerdeydi ve kayboldu; bu suite KALICI (tools/).
//
// Çalıştırma:
//   BASE=http://localhost:5051 CHROME=~/.cache/ms-playwright/chromium-1228/chrome-linux64/chrome \
//   node tools/misharix-sync/h7-regression.mjs
// Gerekli: npm i playwright-core (suite dizininde ya da NODE_PATH ile erişilebilir).
// Test verisi (dev DB): üye h10test@test.local/H10Test123!; koleksiyon ShareCode b804133807.

import { createRequire } from 'module';
import os from 'os';
import path from 'path';
import { fileURLToPath } from 'url';

const require = createRequire(import.meta.url);
let chromium;
try { ({ chromium } = require('playwright-core')); }
catch { ({ chromium } = require(path.join(process.env.PWMODUL || '', 'node_modules', 'playwright-core'))); }

const BASE = process.env.BASE || 'http://localhost:5051';
const CHROME = process.env.CHROME
  || path.join(os.homedir(), '.cache/ms-playwright/chromium-1228/chrome-linux64/chrome');
const SHOTS = path.join(path.dirname(fileURLToPath(import.meta.url)), 'shots');

let gecen = 0, kalan = 0;
const ok = (m) => { gecen++; console.log('OK  :', m); };
const fail = (m) => { kalan++; process.exitCode = 1; console.error('FAIL:', m); };
const kontrol = (kosul, m) => (kosul ? ok(m) : fail(m));

const browser = await chromium.launch({ headless: true, executablePath: CHROME,
  args: ['--no-sandbox', '--ignore-certificate-errors'] });

// ── 1. Desktop çekirdek ──────────────────────────────────────────
{
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, ignoreHTTPSErrors: true });
  const p = await ctx.newPage();

  await p.goto(BASE + '/', { waitUntil: 'domcontentloaded', timeout: 30000 });
  await p.waitForTimeout(2500);
  kontrol(await p.locator('.ms-ana-navigasyon-arama-input').first().isVisible(), 'ana sayfa: nav + arama');
  kontrol((await p.locator('.ms-urun-karti').count()) > 0, 'ana sayfa: vitrin ürün kartları');
  await p.screenshot({ path: path.join(SHOTS, 'h7-home-desktop.png') });

  // Kategori listesi + karttan detaya
  await p.goto(BASE + '/kadin-bot', { waitUntil: 'domcontentloaded' });
  await p.waitForTimeout(2000);
  const kartSayisi = await p.locator('.ms-urun-karti').count();
  kontrol(kartSayisi > 3, `liste: ${kartSayisi} kart`);
  // Kart linki galeri katmanının altında (tıklamayı site.js yönetir) — href ile gidilir
  const detayHref = await p.locator('.ms-urun-karti a[href]').first().getAttribute('href');
  await p.goto(BASE + detayHref, { waitUntil: 'domcontentloaded' });
  await p.waitForTimeout(2000);
  kontrol((await p.locator('h1').count()) > 0, 'detay: sayfa açıldı ' + p.url().slice(-40));

  // H9: değerlendirmeler sayfası
  const d = await p.request.get(BASE + '/urun-degerlendirmeleri/P-00019511');
  kontrol(d.status() === 200, 'H9: /urun-degerlendirmeleri/P-00019511 → 200');

  // H3: görsel arama endpoint yanıt sözleşmesi (servis erişilebilirse results döner; 502 kabul — dış servis)
  const g = await p.request.post(BASE + '/gorsel-arama');
  kontrol(g.status() === 400, 'H3: dosyasız /gorsel-arama → 400');

  // H10-1: public koleksiyon + olmayan kod
  const k1 = await p.request.get(BASE + '/koleksiyon/b804133807');
  kontrol(k1.status() === 200, 'H10: /koleksiyon/{shareCode} → 200');
  const k2 = await p.request.get(BASE + '/koleksiyon/yok999x');
  kontrol(k2.status() === 404, 'H10: olmayan koleksiyon → 404');

  await ctx.close();
}

// ── 2. Üye oturumu (statü bloğu + çıkış davranışı) ───────────────
{
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const p = await ctx.newPage();
  const r = await p.request.post(BASE + '/api/store/auth/login',
    { data: { email: 'h10test@test.local', password: 'H10Test123!' } });
  if (r.status() !== 200) fail('üye login başarısız (test verisi eksik olabilir)');
  else {
    const v = (await r.json()).data;
    await ctx.addCookies([{ name: 'ecspros_member', value: v.accessToken, url: BASE }]);
    await p.goto(BASE + '/uyelik-bilgilerim', { waitUntil: 'domcontentloaded' });
    await p.evaluate((t) => { localStorage.setItem('ms_token', t.a); localStorage.setItem('ms_refresh', t.r); },
      { a: v.accessToken, r: v.refreshToken });
    await p.reload({ waitUntil: 'domcontentloaded' });
    await p.waitForTimeout(1500);
    kontrol(await p.locator('text=Statünüz').first().isVisible().catch(() => false), 'H10-4: yan menü statü bloğu');
    const basliklar = await p.request.get(BASE + '/uyelik-bilgilerim',
      { headers: { Cookie: 'ecspros_member=' + v.accessToken } });
    kontrol((basliklar.headers()['cache-control'] || '').includes('no-store'), 'hesabım: Cache-Control no-store');
    // Çıkış → köke dönüş (oturum düzeltmesi)
    await p.evaluate(() => document.querySelector('[data-ms-hesap-cikis]')?.click());
    await p.waitForTimeout(2500);
    kontrol(new URL(p.url()).pathname === '/', 'çıkış: üye-özel sayfadan ana sayfaya yönlendi');
  }
  await ctx.close();
}

// ── 3. Mobil (alt bar + renk paneli mükerrersiz) ─────────────────
{
  const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const p = await ctx.newPage();
  await p.goto(BASE + '/', { waitUntil: 'domcontentloaded' });
  await p.waitForTimeout(2000);
  kontrol(await p.locator('.ms-mobil-alt-bar').first().isVisible().catch(() => false), 'H4: mobil alt bar');
  await p.screenshot({ path: path.join(SHOTS, 'h7-home-mobil.png') });

  await p.goto(BASE + '/kadin-bot', { waitUntil: 'domcontentloaded' });
  await p.waitForTimeout(2000);
  const sonuc = await p.evaluate(() => {
    const roz = document.querySelector('.ms-urun-karti .ms-urun-renk-rozet');
    if (!roz) return null;
    const kart = roz.closest('.ms-urun-karti');
    kart.classList.add('ms-urun-renk-tooltip-acik');
    const a = [...kart.querySelectorAll('.ms-urun-renk-tooltip-liste a')];
    return { toplam: a.length, gorunen: a.filter((x) => getComputedStyle(x).display !== 'none').length,
             rozet: parseInt(roz.textContent.trim(), 10) };
  });
  if (sonuc) kontrol(sonuc.gorunen === sonuc.rozet && sonuc.gorunen < sonuc.toplam,
    `mobil renk paneli: ${sonuc.gorunen}/${sonuc.toplam} görünen (rozet ${sonuc.rozet}) — mükerrer yok`);
  await p.screenshot({ path: path.join(SHOTS, 'h7-liste-mobil.png') });
  await ctx.close();
}

await browser.close();
console.log(`\nSONUÇ: ${gecen} geçti, ${kalan} kaldı — ${kalan === 0 ? 'TEMİZ ✓' : 'BAŞARISIZ ✗'}`);
