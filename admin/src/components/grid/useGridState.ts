import { useCallback, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuthStore } from '@/store/auth'
import { GRID_MAX_PAGE_SIZE, type GridFilterValue, type GridPrefs, type GridState } from './types'

// Tek durum kaynağı (plan §2.1, K3):
//  • URL'de: page, search, sort, dir, f.<alan>=<op>:<değer>, fq.<alan>=<hızlı seçim etiketi> → geri/ileri + paylaşılabilir link,
//    sidebar linkleri parametresiz olduğundan listeye ilk giriş her zaman temiz [E14].
//  • localStorage'da (kullanıcıya özel anahtar): kolon sırası/görünürlüğü, sayfa boyu, frozen tercihi.

export interface GridStateOptions {
  defaultPageSize?: number
  defaultSort?: string | null
  defaultDir?: 'asc' | 'desc'
}

const KNOWN_OPS = new Set(['contains', 'eq', 'startswith', 'in', 'between', 'gt', 'gte', 'lt', 'lte'])

function parseFilters(sp: URLSearchParams): GridFilterValue[] {
  const out: GridFilterValue[] = []
  sp.forEach((raw, key) => {
    if (!key.startsWith('f.') || key.length <= 2 || !raw) return
    const field = key.slice(2)
    const idx = raw.indexOf(':')
    let op = 'auto', value = raw
    if (idx > 0 && idx <= 12 && KNOWN_OPS.has(raw.slice(0, idx).toLowerCase())) { op = raw.slice(0, idx).toLowerCase(); value = raw.slice(idx + 1) }
    const quick = sp.get(`fq.${field}`) ?? undefined
    out.push({ field, op, value, quick })
  })
  return out
}

export function prefsKey(gridId: string, userId: string | null | undefined) {
  return `ecspros-grid:${gridId}:${userId ?? 'anon'}`
}

function readPrefs(key: string): GridPrefs {
  try {
    const raw = localStorage.getItem(key)
    return raw ? (JSON.parse(raw) as GridPrefs) : {}
  } catch { return {} }
}

export function useGridState(gridId: string, opts: GridStateOptions = {}) {
  const defaultPageSize = opts.defaultPageSize ?? 50
  const [sp, setSp] = useSearchParams()
  const userId = useAuthStore(s => s.user?.id)
  const key = prefsKey(gridId, userId)
  // kullanıcı değişince (anahtar değişir) tercihler render sırasında yeniden okunur — effect içinde setState yok
  const [box, setBox] = useState<{ key: string; prefs: GridPrefs }>(() => ({ key, prefs: readPrefs(key) }))
  if (box.key !== key) setBox({ key, prefs: readPrefs(key) })
  const prefs = box.key === key ? box.prefs : readPrefs(key)

  const setPrefs = useCallback((patch: Partial<GridPrefs> | ((p: GridPrefs) => GridPrefs)) => {
    setBox(prev => {
      const next = typeof patch === 'function' ? patch(prev.prefs) : { ...prev.prefs, ...patch }
      try { localStorage.setItem(prev.key, JSON.stringify(next)) } catch { /* kota/gizli mod: tercih yalnız oturumda kalır */ }
      return { key: prev.key, prefs: next }
    })
  }, [])

  const resetPrefs = useCallback(() => {
    setBox(prev => {
      try { localStorage.removeItem(prev.key) } catch { /* yok say */ }
      return { key: prev.key, prefs: {} }
    })
  }, [])

  const state: GridState = useMemo(() => {
    const dirRaw = sp.get('dir')
    const sort = sp.get('sort') || opts.defaultSort || null
    const dir: 'asc' | 'desc' | null = dirRaw === 'asc' || dirRaw === 'desc' ? dirRaw : sort ? (opts.defaultDir ?? 'desc') : null
    const ps = prefs.pageSize && prefs.pageSize > 0 ? Math.min(prefs.pageSize, GRID_MAX_PAGE_SIZE) : defaultPageSize
    return {
      page: Math.max(1, parseInt(sp.get('page') ?? '1', 10) || 1),
      pageSize: ps,
      search: sp.get('search') ?? '',
      sort, dir,
      filters: parseFilters(sp),
    }
  }, [sp, prefs.pageSize, defaultPageSize, opts.defaultSort, opts.defaultDir])

  /** URL'yi güncelle; page dışındaki her değişiklik sayfayı 1'e alır. replace: yazarken geçmiş kirlenmesin (debounce'lu arama). */
  const update = useCallback((fn: (next: URLSearchParams) => void, o: { keepPage?: boolean; replace?: boolean } = {}) => {
    setSp(prev => {
      const next = new URLSearchParams(prev)
      fn(next)
      if (!o.keepPage) next.delete('page')
      return next
    }, { replace: o.replace ?? false })
  }, [setSp])

  const setPage = useCallback((page: number) => update(n => { if (page > 1) n.set('page', String(page)); else n.delete('page') }, { keepPage: true }), [update])
  const setSearch = useCallback((s: string, o?: { replace?: boolean }) => update(n => { if (s.trim()) n.set('search', s.trim()); else n.delete('search') }, o), [update])
  /** Aynı anahtara tekrar tıklama yönü çevirir; üçüncü tıklama sıralamayı kaldırır. */
  const toggleSort = useCallback((k: string) => update(n => {
    const cur = n.get('sort'); const curDir = n.get('dir')
    if (cur !== k) { n.set('sort', k); n.set('dir', 'asc') }
    else if (curDir !== 'desc') n.set('dir', 'desc')
    else { n.delete('sort'); n.delete('dir') }
  }), [update])
  const setSort = useCallback((k: string | null, dir: 'asc' | 'desc' = 'asc') => update(n => {
    if (!k) { n.delete('sort'); n.delete('dir') } else { n.set('sort', k); n.set('dir', dir) }
  }), [update])
  const setFilter = useCallback((f: GridFilterValue) => update(n => {
    if (!f.value) { n.delete(`f.${f.field}`); n.delete(`fq.${f.field}`); return }
    n.set(`f.${f.field}`, f.op && f.op !== 'auto' ? `${f.op}:${f.value}` : f.value)
    if (f.quick) n.set(`fq.${f.field}`, f.quick); else n.delete(`fq.${f.field}`)
  }), [update])
  const removeFilter = useCallback((field: string) => update(n => { n.delete(`f.${field}`); n.delete(`fq.${field}`) }), [update])
  const clearFilters = useCallback((o: { keepSearch?: boolean } = {}) => update(n => {
    Array.from(n.keys()).forEach(k => { if (k.startsWith('f.') || k.startsWith('fq.')) n.delete(k) })
    if (!o.keepSearch) n.delete('search')
  }), [update])
  const setPageSize = useCallback((n: number) => { setPrefs({ pageSize: n }); setPage(1) }, [setPrefs, setPage])

  /** API parametreleri (sunucu GridRequestParser sözleşmesi) */
  const toParams = useCallback((extra?: Record<string, string | undefined>) => {
    const p = new URLSearchParams()
    p.set('page', String(state.page)); p.set('pageSize', String(state.pageSize))
    if (state.search) p.set('search', state.search)
    if (state.sort) { p.set('sort', state.sort); p.set('dir', state.dir ?? 'asc') }
    for (const f of state.filters) p.set(`f.${f.field}`, f.op && f.op !== 'auto' ? `${f.op}:${f.value}` : f.value)
    if (extra) for (const [k, v] of Object.entries(extra)) if (v) p.set(k, v)
    return p
  }, [state])

  const activeFilterCount = state.filters.length
  /** react-query anahtarı için kararlı özet */
  const queryKey = useMemo(() => [gridId, state.page, state.pageSize, state.search, state.sort, state.dir, state.filters.map(f => `${f.field}=${f.op}:${f.value}`).join('&')], [gridId, state])

  /** Toplu URL değişikliği (aynı tikte birden çok setX çağrısı birbirini ezer — react-router updater kapanıştaki parametreleri görür). */
  const mutate = update

  return { state, prefs, setPrefs, resetPrefs, setPage, setPageSize, setSearch, toggleSort, setSort, setFilter, removeFilter, clearFilters, mutate, toParams, activeFilterCount, queryKey }
}

export type GridStateApi = ReturnType<typeof useGridState>
