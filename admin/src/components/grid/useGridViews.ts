import { useCallback, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import type { GridStateApi } from './useGridState'
import type { GridPrefs } from './types'

// Kaydedilmiş görünümler (plan §2.9, K7 — kişisel): iam.Users.Preferences jsonb, anahtar `grids.<gridId>`.
// Görünüm = URL parametreleri (filtreler, arama, sıralama, sekme… — page hariç) + kolon tercihleri (sıra/görünürlük/sayfa boyu/frozen).
// Varsayılan görünüm: listeye TEMİZ girişte (URL'de grid parametresi yokken) bir kez uygulanır; filtreleri çipte görünür.

export interface GridView {
  id: string
  name: string
  params: Record<string, string>
  prefs: Pick<GridPrefs, 'order' | 'manualVisible' | 'manualHidden' | 'pageSize' | 'frozen'>
  createdAt: string
}

interface GridViewsDoc { views: GridView[]; defaultViewId?: string | null }

const PREFS_QK = ['my-preferences']
const GRID_PARAM_RE = /^(search|sort|dir|tab|status|f\.|fq\.)/

function currentParams(sp: URLSearchParams): Record<string, string> {
  const out: Record<string, string> = {}
  sp.forEach((v, k) => { if (k !== 'page' && v) out[k] = v })
  return out
}
function hasGridParams(sp: URLSearchParams) {
  let any = false; sp.forEach((_, k) => { if (GRID_PARAM_RE.test(k)) any = true }); return any
}
function sameParams(a: Record<string, string>, b: Record<string, string>) {
  const ka = Object.keys(a).sort(), kb = Object.keys(b).sort()
  return ka.length === kb.length && ka.every((k, i) => k === kb[i] && a[k] === b[k])
}

export function useGridViews(gridId: string, grid: GridStateApi, searchParams: URLSearchParams, opts: { enabled?: boolean } = {}) {
  const enabled = opts.enabled ?? true
  const qc = useQueryClient()
  const key = `grids.${gridId}`
  const { data: allPrefs, isLoading } = useQuery<Record<string, unknown>>({
    queryKey: PREFS_QK,
    queryFn: async () => (await api.get('/iam/users/me/preferences')).data.data ?? {},
    staleTime: 5 * 60_000,
    retry: false,
    enabled,   // görünüm özelliği kapalı grid'ler tercih isteği atmaz
  })
  const doc = useMemo<GridViewsDoc>(() => {
    const d = allPrefs?.[key] as Partial<GridViewsDoc> | undefined
    return { views: Array.isArray(d?.views) ? d!.views! : [], defaultViewId: d?.defaultViewId ?? null }
  }, [allPrefs, key])

  const write = useMutation({
    mutationFn: async (next: GridViewsDoc) => { await api.put('/iam/users/me/preferences', { key, value: next }) ; return next },
    onSuccess: (next) => qc.setQueryData<Record<string, unknown>>(PREFS_QK, prev => ({ ...(prev ?? {}), [key]: next })),
  })

  const snapshot = useCallback((): Omit<GridView, 'id' | 'name' | 'createdAt'> => ({
    params: currentParams(searchParams),
    prefs: { order: grid.prefs.order, manualVisible: grid.prefs.manualVisible, manualHidden: grid.prefs.manualHidden, pageSize: grid.prefs.pageSize, frozen: grid.prefs.frozen },
  }), [searchParams, grid.prefs])

  const save = useCallback(async (name: string) => {
    const v: GridView = { id: crypto.randomUUID(), name: name.trim(), createdAt: new Date().toISOString(), ...snapshot() }
    await write.mutateAsync({ ...doc, views: [...doc.views, v] })
    return v
  }, [doc, snapshot, write])

  const update = useCallback(async (id: string) => {
    await write.mutateAsync({ ...doc, views: doc.views.map(v => v.id === id ? { ...v, ...snapshot() } : v) })
  }, [doc, snapshot, write])

  const remove = useCallback(async (id: string) => {
    await write.mutateAsync({ views: doc.views.filter(v => v.id !== id), defaultViewId: doc.defaultViewId === id ? null : doc.defaultViewId })
  }, [doc, write])

  const setDefault = useCallback(async (id: string | null) => { await write.mutateAsync({ ...doc, defaultViewId: id }) }, [doc, write])

  const apply = useCallback((v: GridView, o: { replace?: boolean } = {}) => {
    grid.setPrefs(p => ({ ...p, ...v.prefs }))
    grid.mutate(n => {
      Array.from(n.keys()).forEach(k => n.delete(k))
      for (const [k, val] of Object.entries(v.params)) n.set(k, val)
    }, { replace: o.replace })
  }, [grid])

  const activeView = useMemo(() => {
    const cur = currentParams(searchParams)
    return doc.views.find(v => sameParams(v.params, cur)) ?? null
  }, [doc.views, searchParams])

  // varsayılan görünüm: temiz girişte bir kez (mount başına) — durum, ref değil (render sırasında ref okunmaz)
  const [appliedOnce, setAppliedOnce] = useState(false)
  const defaultView = doc.views.find(v => v.id === doc.defaultViewId) ?? null
  const shouldApplyDefault = !appliedOnce && !isLoading && !!defaultView && !hasGridParams(searchParams)
  const applyDefaultOnce = useCallback(() => {
    if (!shouldApplyDefault || !defaultView) return
    setAppliedOnce(true)
    apply(defaultView, { replace: true })
  }, [shouldApplyDefault, defaultView, apply])

  return { views: doc.views, defaultViewId: doc.defaultViewId ?? null, isLoading, saving: write.isPending, activeView,
    save, update, remove, setDefault, apply, shouldApplyDefault, applyDefaultOnce }
}

export type GridViewsApi = ReturnType<typeof useGridViews>
