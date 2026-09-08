import { findActiveItem } from '@/components/layout/sidebarNavigation'

export const FAVORITES_KEY = 'ecspros-admin-favorites-v1'
export interface AdminFavorite { label: string; to: string }
export type UserFavorites = Record<string, AdminFavorite[]>

export function favoriteForRoute(to: string): AdminFavorite | null {
  // Yalnız admin-relative uygulama yolları; dış URL ve scheme kabul edilmez.
  if (to.length > 2048 || !to.startsWith('/') || to.startsWith('//') || to.includes('\\') || [...to].some((character) => character.charCodeAt(0) <= 32)) return null
  const url = new URL(to, 'https://admin.invalid')
  if (url.origin !== 'https://admin.invalid') return null
  const item = findActiveItem(url.pathname)
  if (!item) return null
  const suffix = url.pathname !== item.to ? url.pathname.split('/').filter(Boolean).at(-1) : ''
  return { label: suffix ? `${item.label} · ${suffix}` : item.label, to: url.pathname + url.search + url.hash }
}

export function readFavorites(): UserFavorites {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(FAVORITES_KEY) ?? '{}')
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return {}
    return Object.fromEntries(Object.entries(parsed).filter(([id]) => !['__proto__', 'constructor', 'prototype'].includes(id)).map(([id, value]) => {
      const items: AdminFavorite[] = []
      if (Array.isArray(value)) for (const entry of value) {
        if (!entry || typeof entry.to !== 'string' || entry.to.length > 2048) continue
        const favorite = favoriteForRoute(entry.to)
        if (!favorite || items.some((item) => item.to === favorite.to)) continue
        items.push(favorite) // Güncel menü adını kullan; saklı HTML/etikete güvenme.
      }
      return [id, items.slice(0, 100)]
    }))
  } catch { return {} }
}

/** Token/oturum temizliği aynı kalır; yalnız doğrulanmış kullanıcı favorileri korunur. */
export function clearSessionStoragePreservingFavorites() {
  const favorites = readFavorites()
  // DataGrid kişisel tablo tercihleri (ecspros-grid:<gridId>:<userId>) de korunur — kullanıcıya özel anahtarlar, sır içermez.
  const gridPrefs: [string, string][] = []
  try {
    for (let i = 0; i < localStorage.length; i++) {
      const k = localStorage.key(i)
      if (k && k.startsWith('ecspros-grid:')) gridPrefs.push([k, localStorage.getItem(k) ?? ''])
    }
  } catch { /* okunamazsa korunmaz */ }
  localStorage.clear()
  try {
    if (Object.keys(favorites).length) localStorage.setItem(FAVORITES_KEY, JSON.stringify(favorites))
    for (const [k, v] of gridPrefs) localStorage.setItem(k, v)
  } catch { /* Oturum temizliği storage kota/izin hatasında da tamamlanmıştır. */ }
}

export function matchesFavorite(favorite: AdminFavorite, query: string): boolean {
  const normalize = (s: string) => s.trim().toLocaleLowerCase('tr').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/ı/g, 'i').replace(/\s+/g, ' ')
  return normalize(favorite.label + ' ' + favorite.to).includes(normalize(query))
}
