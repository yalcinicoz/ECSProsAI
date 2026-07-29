#!/usr/bin/env node
// Mobil API referans istemcisi (2026-07-23) — mobil geliştirici için.
// Attestation → device token → imzalı istek akışının çalışan örneği. Uygulamada
// (Kotlin/Swift) yeniden yazılacak imza mantığının referansıdır; harici bağımlılık yok.
//
// Kullanım:
//   BASE=https://new.ecspros.com BYPASS=<test-secret> node reference-client.mjs
//   (BYPASS yalnız DevBypass'ın açık olduğu ortamda çalışır; prod'da attestation gerçektir.)
//
// Not: BYPASS gerçek Play Integrity'nin YERİNE geçer — sadece test içindir. Uygulamanın
// canlı sürümü bu adımda Play Integrity/App Attest token'ı gönderir; token'dan SONRAKI
// imza/replay akışı prod'da da AYNIDIR.

import crypto from 'node:crypto'

const BASE = process.env.BASE || 'http://localhost:5051'
const BYPASS = process.env.BYPASS || ''
const PLATFORM = process.env.PLATFORM || 'android'
const FIRM_PLATFORM_ID = process.env.FIRM_PLATFORM_ID || 'c900c659-8d0f-4754-9658-aa157ea3072e'

let deviceToken = null
let signingSecret = null

function sha256Hex(buf) {
  return crypto.createHash('sha256').update(buf).digest('hex')
}

// İmza = hex(HMACSHA256(base64decode(secret), "METOD\n/path?query\nts\nnonce\nsha256hex(body)"))
function imzaBasliklari(method, pathQuery, bodyBuf) {
  const ts = Math.floor(Date.now() / 1000).toString()
  const nonce = crypto.randomUUID().replace(/-/g, '')
  const veri = [method.toUpperCase(), pathQuery, ts, nonce, sha256Hex(bodyBuf || Buffer.alloc(0))].join('\n')
  const imza = crypto.createHmac('sha256', Buffer.from(signingSecret, 'base64')).update(veri).digest('hex')
  return { 'X-Timestamp': ts, 'X-Nonce': nonce, 'X-Signature': imza }
}

// Device token taşıyan her isteğe imza başlıklarını otomatik ekler.
async function apiIstek(method, path, { body = null, token = deviceToken, imzala = true } = {}) {
  const bodyBuf = body != null ? Buffer.from(JSON.stringify(body)) : null
  const pathQuery = path // path zaten ?query içermeli
  const headers = { 'X-Client-Platform': PLATFORM }
  if (body != null) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`
  if (token === deviceToken && imzala) Object.assign(headers, imzaBasliklari(method, pathQuery, bodyBuf))
  const res = await fetch(BASE + path, { method, headers, body: bodyBuf })
  const text = await res.text()
  let json; try { json = JSON.parse(text) } catch { json = text }
  return { status: res.status, json }
}

async function attest() {
  const ch = await (await fetch(`${BASE}/api/store/device/challenge`)).json()
  const challenge = ch.data.challenge
  const attestation = BYPASS || 'PLAY_INTEGRITY_TOKEN_BURAYA' // prod: gerçek integrity token
  const res = await fetch(`${BASE}/api/store/device/attest`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ platform: PLATFORM, attestation, challenge }),
  })
  const d = await res.json()
  if (!d.success) throw new Error(`Attest başarısız (HTTP ${res.status}): ${d.error}`)
  deviceToken = d.data.deviceToken
  signingSecret = d.data.signingSecret
  console.log(`✓ Device token alındı (bitiş: ${d.data.expiresAt})`)
}

async function main() {
  console.log(`Hedef: ${BASE} | platform: ${PLATFORM} | bypass: ${BYPASS ? 'AÇIK' : 'yok'}`)
  await attest()

  // 1) Anonim veri: ürün araması (imzalı device token'la)
  const urunler = await apiIstek('GET',
    `/api/store/catalog/products?firmPlatformId=${FIRM_PLATFORM_ID}&search=g%C3%B6mlek&page=1&pageSize=4`)
  console.log(`✓ Ürün araması: HTTP ${urunler.status}, sonuç: ${urunler.json?.data ? 'geldi' : urunler.json?.error}`)

  // 2) Bootstrap
  const boot = await apiIstek('GET', `/api/store/bootstrap?code=mishar`)
  console.log(`✓ Bootstrap: HTTP ${boot.status}, kanal: ${boot.json?.data?.code}`)

  // 3) Üye girişi (imzalı) — dönen üye JWT'si sonraki isteklerde device token YERİNE kullanılır
  if (process.env.EMAIL && process.env.PASSWORD) {
    const login = await apiIstek('POST', '/api/store/auth/login',
      { body: { email: process.env.EMAIL, password: process.env.PASSWORD } })
    if (login.json?.data?.accessToken) {
      const memberToken = login.json.data.accessToken
      console.log(`✓ Üye girişi: HTTP ${login.status}`)
      // Üye token'lı istek: imza GEREKMEZ (imzala:false), Authorization yeter
      const me = await apiIstek('GET', '/api/store/auth/me', { token: memberToken, imzala: false })
      console.log(`✓ /auth/me: HTTP ${me.status}, üye: ${me.json?.data?.email}`)
    } else {
      console.log(`✗ Üye girişi: HTTP ${login.status} — ${login.json?.error}`)
    }
  } else {
    console.log('ℹ Üye girişi atlandı (EMAIL/PASSWORD env verilmedi)')
  }
}

main().catch(e => { console.error('HATA:', e.message); process.exit(1) })
