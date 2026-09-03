/**
 * F1 Kanal kapsamı sekmesi (docs/satis-kanali-ortak-kurgu.md §3.1 / §4.1 madde 4).
 * Kanalın "söz konusu ürünler" kümesi: Tümü | Filtre | Karma (filtre + manuel). Filtre = FilterBuilder
 * (kanal kapsamı kriterleri açık), eşleşen sayı önizlemesi, Kaydet (hemen günceller), Kapsamı Güncelle,
 * manuel eklenen / hariç tutulan listeleri (ürün kodu ile ekle).
 */
import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { RefreshCw, Plus, X, Ban } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { FilterBuilder, type FilterDef } from '@/components/catalog/FilterBuilder'
import { apiErrorMessage } from '@/lib/api-error'

interface ScopeProduct { productId: string; code: string; nameI18n: Record<string, string> }
interface ScopeDto {
  firmPlatformId: string
  fillType: 'all' | 'filter' | 'mixed'
  filterDef: FilterDef | null
  syncedAt: string | null
  matchedCount: number | null
  lastSyncError: string | null
  inScopeCount: number
  manualIncluded: ScopeProduct[]
  manualExcluded: ScopeProduct[]
}

const FILL_OPTIONS = [
  { value: 'all', label: 'Tümü', help: 'Görselli tüm katalog ürünleri kapsamdadır (bugünkü davranış). Kanaldan çıkar / durdur kararları ayrıca uygulanır.' },
  { value: 'filter', label: 'Filtre', help: 'Yalnız kayıtlı filtreden geçen ürünler kapsamdadır; filtre değişince / gece taramasında yeniden hesaplanır.' },
  { value: 'mixed', label: 'Karma', help: 'Filtreden geçenler + manuel eklenenler; manuel hariç tutulanlar filtreden geçse de kapsam dışı kalır.' },
] as const

const nameOf = (i18n: Record<string, string>) => i18n?.['tr'] ?? i18n?.[Object.keys(i18n ?? {})[0]] ?? '—'

export function ChannelScopeTab({ channelId }: { channelId: string }) {
  const qc = useQueryClient()
  const { data: scope, isLoading } = useQuery<ScopeDto>({
    queryKey: ['channel-scope', channelId],
    queryFn: async () => (await api.get(`/navigation/channel-products/${channelId}/scope`)).data.data,
    enabled: !!channelId,
  })

  const [fillType, setFillType] = useState<'all' | 'filter' | 'mixed'>('all')
  const [filterDef, setFilterDef] = useState<FilterDef>({})
  const [dirty, setDirty] = useState(false)
  const [preview, setPreview] = useState<{ matchedCount: number; catalogCount: number } | null>(null)
  const [manualCode, setManualCode] = useState('')
  const [manualErr, setManualErr] = useState<string | null>(null)
  const [msg, setMsg] = useState<string | null>(null)
  const [loadedScope, setLoadedScope] = useState<ScopeDto | undefined>()

  if (scope && scope !== loadedScope) {
    setLoadedScope(scope)
    setFillType(scope.fillType)
    setFilterDef(scope.filterDef ?? {})
    setDirty(false)
    setPreview(null)
  }

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['channel-scope', channelId] })
    qc.invalidateQueries({ queryKey: ['channel-products'] })
  }

  const previewMut = useMutation({
    mutationFn: async () => (await api.post(`/navigation/channel-products/${channelId}/scope/preview`, { fillType, filterDef })).data.data,
    onSuccess: d => setPreview(d),
  })
  const saveMut = useMutation({
    mutationFn: async () => (await api.put(`/navigation/channel-products/${channelId}/scope`, { fillType, filterDef: fillType === 'all' ? null : filterDef })).data.data,
    onSuccess: d => { setMsg(fillType === 'all' ? 'Kapsam "Tümü" olarak kaydedildi.' : `Kapsam kaydedildi ve güncellendi: ${d.matched} ürün filtreden geçti.`); invalidate() },
    onError: (e: unknown) => setMsg(apiErrorMessage(e, 'Kaydedilemedi.')),
  })
  const syncMut = useMutation({
    mutationFn: async () => (await api.post(`/navigation/channel-products/${channelId}/scope/sync`)).data.data,
    onSuccess: d => { setMsg(`Kapsam güncellendi: ${d.matched} ürün filtreden geçti.`); invalidate() },
    onError: (e: unknown) => setMsg(apiErrorMessage(e, 'Güncellenemedi.')),
  })
  const manualMut = useMutation({
    mutationFn: async ({ productIds, action }: { productIds: string[]; action: 'include' | 'exclude' | 'clear' }) =>
      (await api.post(`/navigation/channel-products/${channelId}/scope/manual`, { productIds, action })).data.data,
    onSuccess: () => { setManualCode(''); setManualErr(null); invalidate() },
    onError: (e: unknown) => setManualErr(apiErrorMessage(e, 'İşlem başarısız.')),
  })

  // Ürün kodundan Id çözümü (manuel ekle / hariç tut)
  const resolveByCode = async (code: string): Promise<string | null> => {
    const c = code.trim()
    if (!c) return null
    try {
      const { data } = await api.get(`/catalog/products/${encodeURIComponent(c)}`)
      return data?.data?.id ?? null
    } catch { return null }
  }
  const manual = async (action: 'include' | 'exclude') => {
    const id = await resolveByCode(manualCode)
    if (!id) { setManualErr(`"${manualCode}" kodlu ürün bulunamadı.`); return }
    manualMut.mutate({ productIds: [id], action })
  }

  const filterBased = fillType !== 'all'
  const summary = useMemo(() => {
    if (!scope) return null
    if (scope.fillType === 'all') return 'Kapsam: tüm katalog (örtük)'
    return `Kapsamda ${scope.inScopeCount} ürün · son hesaplama: ${scope.syncedAt ? new Date(scope.syncedAt).toLocaleString('tr-TR') : '—'}${scope.matchedCount != null ? ` · filtreden geçen ${scope.matchedCount}` : ''}`
  }, [scope])

  if (isLoading || !scope) return <p className="text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</p>

  return (
    <div className="space-y-4">
      <div className="card space-y-3">
        <div className="flex items-start justify-between gap-3 flex-wrap">
          <div>
            <h2 className="text-base font-semibold" style={{ color: 'var(--text)' }}>Kapsam</h2>
            <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
              Bu kanalda "söz konusu" ürünler. Kapsam dışı ürün sitede/pazaryerinde görünmez; kapsamdaki ürün için kanal kararı
              (kanala al / çıkar / durdur) Ürünler sekmesinden verilir.
            </p>
          </div>
          <div className="flex items-center gap-2">
            {summary && <Badge variant="neutral">{summary}</Badge>}
            {scope.lastSyncError && <Badge variant="danger">Son hesaplama hatası</Badge>}
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          {FILL_OPTIONS.map(o => (
            <button key={o.value} type="button"
              onClick={() => { setFillType(o.value); setDirty(true); setPreview(null) }}
              className="px-3 py-1.5 rounded-lg text-sm font-medium"
              style={{
                background: fillType === o.value ? 'var(--brand)' : 'var(--surface2)',
                color: fillType === o.value ? '#fff' : 'var(--text-m)',
                border: '1px solid var(--border)',
              }}>{o.label}</button>
          ))}
        </div>
        <p className="text-xs" style={{ color: 'var(--text-s)' }}>{FILL_OPTIONS.find(o => o.value === fillType)?.help}</p>
        {scope.lastSyncError && <p className="text-xs" style={{ color: '#ef4444' }}>{scope.lastSyncError}</p>}
      </div>

      {filterBased && (
        <div className="card">
          <FilterBuilder value={filterDef} channelScope onChange={def => { setFilterDef(def); setDirty(true); setPreview(null) }} />
        </div>
      )}

      <div className="card flex flex-wrap items-center gap-2">
        {filterBased && (
          <Button variant="secondary" onClick={() => previewMut.mutate()} loading={previewMut.isPending}>
            Eşleşen sayısını göster
          </Button>
        )}
        {preview && (
          <Badge variant="info">{preview.matchedCount} / {preview.catalogCount} ürün eşleşiyor</Badge>
        )}
        <div className="flex-1" />
        {filterBased && !dirty && (
          <Button variant="secondary" onClick={() => syncMut.mutate()} loading={syncMut.isPending}>
            <RefreshCw size={14} /> Kapsamı Güncelle
          </Button>
        )}
        <Button onClick={() => saveMut.mutate()} loading={saveMut.isPending}
          disabled={!dirty || (filterBased && !Object.values(filterDef).some(value => value != null && (!Array.isArray(value) || value.length > 0)))}>
          Kaydet ve Güncelle
        </Button>
        {msg && <p className="w-full text-xs" style={{ color: 'var(--text-s)' }}>{msg}</p>}
      </div>

      {/* Manuel ekle / hariç tut */}
      <div className="card space-y-3">
        <div>
          <h3 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Manuel kapsam kararları</h3>
          <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
            <b>Kapsama ekle</b>: filtreden geçmese de kapsamda kalır (Karma/Filtre). <b>Hariç tut</b>: filtreden geçse de kalıcı kapsam dışı — yeniden hesaplama geri eklemez.
          </p>
        </div>
        <div className="flex flex-wrap items-end gap-2">
          <div className="min-w-[220px]">
            <label className="flbl mb-1">Ürün kodu</label>
            <input className="inp" value={manualCode} onChange={e => setManualCode(e.target.value)} placeholder="örn. 12345"
              onKeyDown={e => e.key === 'Enter' && manual('include')} />
          </div>
          <Button variant="secondary" onClick={() => manual('include')} loading={manualMut.isPending} disabled={!manualCode.trim()}>
            <Plus size={14} /> Kapsama ekle
          </Button>
          <Button variant="secondary" onClick={() => manual('exclude')} loading={manualMut.isPending} disabled={!manualCode.trim()}>
            <Ban size={14} /> Hariç tut
          </Button>
          {manualErr && <p className="w-full text-xs" style={{ color: '#ef4444' }}>{manualErr}</p>}
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <ManualList title={`Manuel eklenen (${scope.manualIncluded.length})`} items={scope.manualIncluded}
            onRemove={id => manualMut.mutate({ productIds: [id], action: 'clear' })} />
          <ManualList title={`Hariç tutulan (${scope.manualExcluded.length})`} items={scope.manualExcluded}
            onRemove={id => manualMut.mutate({ productIds: [id], action: 'clear' })} />
        </div>
      </div>
    </div>
  )
}

function ManualList({ title, items, onRemove }: { title: string; items: ScopeProduct[]; onRemove: (id: string) => void }) {
  return (
    <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
      <div className="px-3 py-2 text-xs font-semibold" style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>{title}</div>
      {items.length === 0 ? (
        <p className="px-3 py-3 text-xs" style={{ color: 'var(--text-s)' }}>Kayıt yok.</p>
      ) : (
        <ul className="max-h-64 overflow-auto divide-y" style={{ borderColor: 'var(--border)' }}>
          {items.map(p => (
            <li key={p.productId} className="flex items-center justify-between px-3 py-1.5 text-sm">
              <span className="truncate" style={{ color: 'var(--text)' }}>
                {nameOf(p.nameI18n)} <code className="text-xs ml-1" style={{ color: 'var(--text-s)' }}>{p.code}</code>
              </span>
              <button type="button" className="p-1 rounded hover:opacity-70" title="Manuel kararı kaldır" onClick={() => onRemove(p.productId)}>
                <X size={14} style={{ color: 'var(--text-s)' }} />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
