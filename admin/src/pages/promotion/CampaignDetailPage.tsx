import { useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { cn } from '@/lib/utils'
import { FilterBuilder, type FilterDef } from '@/components/catalog/FilterBuilder'

// ── Tipler ──────────────────────────────────────────────────────────────────
interface SchemaFieldOption { value: string; labelI18n: Record<string, string> }
interface SchemaFieldCondition { field: string; equals?: string; notEquals?: string }
interface CampaignSchemaField {
  key: string; labelI18n: Record<string, string>; type: string; required: boolean
  unit?: string | null; min?: number | null; max?: number | null; default?: unknown
  options?: SchemaFieldOption[] | null; visibleWhen?: SchemaFieldCondition | null
  helpI18n?: Record<string, string> | null
}
interface CampaignType {
  id: string; code: string; nameI18n: Record<string, string>
  scope: string; requiresProducts: boolean; productPriceDisplay: boolean
  isStackable: boolean; isActive: boolean; settingsSchema?: CampaignSchemaField[] | null
}
interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel { id: string; code?: string; nameI18n?: Record<string, string>; firmId?: string; firmName?: string }
interface ProductSimple { id: string; code: string; nameI18n: Record<string, string> }

const tr = (m?: Record<string, string> | null) => m?.['tr'] ?? Object.values(m ?? {})[0] ?? ''
const TABS = ['Genel', 'Parametreler', 'Ürün Kapsamı'] as const
type Tab = typeof TABS[number]
const FILL_OPTS = [
  { value: 'all', label: 'Tüm ürünler' },
  { value: 'manual', label: 'Manuel — ürünler elle eklenir' },
  { value: 'filter', label: 'Filtre — kural tabanlı' },
  { value: 'mixed', label: 'Karma — filtre + manuel' },
]

function errText(e: unknown) {
  return (e as { response?: { data?: { error?: string } } }).response?.data?.error ?? 'İşlem başarısız oldu.'
}

// Koşullu görünürlük
function fieldVisible(f: CampaignSchemaField, settings: Record<string, unknown>): boolean {
  const c = f.visibleWhen
  if (!c) return true
  const cur = settings[c.field] == null ? '' : String(settings[c.field])
  if (c.equals != null) return cur === c.equals
  if (c.notEquals != null) return cur !== c.notEquals
  return true
}

export function CampaignDetailPage() {
  const { id } = useParams<{ id: string }>()
  const isNew = !id || id === 'new'
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<Tab>('Genel')
  const [err, setErr] = useState('')

  // Form state
  const [firmPlatformId, setFirmPlatformId] = useState('')
  const [typeId, setTypeId] = useState('')
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [badge, setBadge] = useState('')
  const [starts, setStarts] = useState(new Date().toISOString().slice(0, 10))
  const [ends, setEnds] = useState('')
  const [priority, setPriority] = useState(0)
  const [isActive, setIsActive] = useState(true)
  const [settings, setSettings] = useState<Record<string, unknown>>({})
  const [fillType, setFillType] = useState('all')
  const [filterDef, setFilterDef] = useState<FilterDef>({})
  const [manualIds, setManualIds] = useState<string[]>([])
  const [loaded, setLoaded] = useState(false)

  // Tipler
  const { data: types = [] } = useQuery<CampaignType[]>({
    queryKey: ['campaign-types', 'all'],
    queryFn: async () => (await api.get('/promotion/campaign-types?activeOnly=false')).data.data,
  })
  const selType = types.find(t => t.id === typeId)

  // Platformlar (firma → platform, düzleştir)
  const { data: firms = [] } = useQuery<Firm[]>({
    queryKey: ['firms'], queryFn: async () => (await api.get('/core/firms')).data.data ?? [],
  })
  const platformQueries = useQueries({
    queries: firms.map(firm => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<Channel[]> => {
        const { data } = await api.get(`/core/firms/${firm.id}/platforms`)
        return (data.data ?? []).map((ch: Channel) => ({ ...ch, firmId: firm.id, firmName: tr(firm.nameI18n) }))
      },
      enabled: firms.length > 0,
    })),
  })
  const channels: Channel[] = platformQueries.flatMap(q => q.data ?? [])

  // Mevcut kampanya (düzenleme)
  useQuery({
    queryKey: ['campaign-detail', id],
    enabled: !isNew && !loaded,
    queryFn: async () => {
      const { data } = await api.get(`/promotion/campaigns/${id}`)
      const c = data.data
      setFirmPlatformId(c.firmPlatformId); setTypeId(c.campaignTypeId); setCode(c.code)
      setName(tr(c.nameI18n)); setDescription(tr(c.descriptionI18n)); setBadge(c.badgeLabel ?? '')
      setStarts((c.startsAt ?? '').slice(0, 10)); setEnds(c.endsAt ? c.endsAt.slice(0, 10) : '')
      setPriority(c.priority); setIsActive(c.isActive); setSettings(c.settings ?? {})
      setFillType(c.fillType ?? 'all'); setFilterDef((c.filterDef ?? {}) as FilterDef)
      setManualIds(c.manualProductIds ?? []); setLoaded(true)
      return c
    },
  })

  // Ürün havuzu (manuel ekleme)
  const showProductScope = selType?.requiresProducts ?? false
  const { data: allProducts = [] } = useQuery<ProductSimple[]>({
    queryKey: ['products-simple'],
    queryFn: async () => (await api.get('/catalog/products?activeOnly=false&pageSize=500')).data.data?.items ?? [],
    enabled: tab === 'Ürün Kapsamı' && (fillType === 'manual' || fillType === 'mixed'),
  })
  const [addProdId, setAddProdId] = useState('')
  const manualProducts = useMemo(
    () => manualIds.map(pid => allProducts.find(p => p.id === pid)).filter(Boolean) as ProductSimple[],
    [manualIds, allProducts])

  const save = useMutation({
    mutationFn: async () => {
      const body = {
        firmPlatformId, campaignTypeId: typeId, code,
        nameI18n: { tr: name }, descriptionI18n: description ? { tr: description } : null,
        badgeLabel: badge || null, startsAt: starts, endsAt: ends || null,
        priority, isActive, settings,
        fillType: showProductScope ? fillType : 'all',
        filterDef: (fillType === 'filter' || fillType === 'mixed') ? filterDef : null,
        manualProductIds: manualIds, excludedProductIds: [] as string[],
      }
      if (isNew) return (await api.post('/promotion/campaigns', body)).data.data.id as string
      await api.put(`/promotion/campaigns/${id}`, body); return id!
    },
    onSuccess: (cid) => {
      queryClient.invalidateQueries({ queryKey: ['campaigns'] })
      navigate(`/promotion/campaigns`)
      void cid
    },
    onError: (e) => setErr(errText(e)),
  })

  const copy = useMutation({
    mutationFn: async () => {
      const newCode = prompt('Yeni kampanya kodu:', `${code}-KOPYA`)
      if (!newCode) return null
      return (await api.post(`/promotion/campaigns/${id}/copy`, { newCode, targetFirmPlatformId: null })).data.data.id as string
    },
    onSuccess: (cid) => { if (cid) { queryClient.invalidateQueries({ queryKey: ['campaigns'] }); navigate(`/promotion/campaigns/${cid}`) } },
    onError: (e) => setErr(errText(e)),
  })

  const setField = (k: string, v: unknown) => setSettings(s => ({ ...s, [k]: v }))

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>
            {isNew ? 'Yeni Kampanya' : `Kampanya: ${code}`}
          </h1>
          {selType && <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{tr(selType.nameI18n)}</p>}
        </div>
        <div className="flex gap-2">
          {!isNew && <Button size="sm" variant="secondary" onClick={() => copy.mutate()}>Kopyala</Button>}
          <Button size="sm" onClick={() => { setErr(''); save.mutate() }} disabled={save.isPending}>
            {save.isPending ? 'Kaydediliyor...' : 'Kaydet'}
          </Button>
        </div>
      </div>

      {err && <div className="mb-3 px-3 py-2 rounded text-sm" style={{ background: 'var(--danger-bg,#fef2f2)', color: '#b91c1c' }}>{err}</div>}

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.filter(t => t !== 'Ürün Kapsamı' || showProductScope).map(t => (
          <button key={t} className={cn('stab', tab === t && 'active')} onClick={() => setTab(t)}>{t}</button>
        ))}
      </div>

      {/* GENEL */}
      {tab === 'Genel' && (
        <div className="card p-4 space-y-3 max-w-2xl">
          <div>
            <label className="flbl">Platform <span className="text-red-500">*</span></label>
            <select className="inp" value={firmPlatformId} onChange={e => setFirmPlatformId(e.target.value)}>
              <option value="">Platform seçin</option>
              {channels.map(c => <option key={c.id} value={c.id}>{tr(c.nameI18n) || c.code} ({c.firmName})</option>)}
            </select>
          </div>
          <div>
            <label className="flbl">Kampanya Tipi <span className="text-red-500">*</span></label>
            <select className="inp" value={typeId} onChange={e => { setTypeId(e.target.value); setSettings({}) }}>
              <option value="">Tip seçin</option>
              {types.map(t => <option key={t.id} value={t.id}>{tr(t.nameI18n)}</option>)}
            </select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div><label className="flbl">Kod <span className="text-red-500">*</span></label>
              <input className="inp" value={code} onChange={e => setCode(e.target.value)} disabled={!isNew} /></div>
            <div><label className="flbl">Ad <span className="text-red-500">*</span></label>
              <input className="inp" value={name} onChange={e => setName(e.target.value)} /></div>
          </div>
          <div><label className="flbl">Açıklama</label>
            <input className="inp" value={description} onChange={e => setDescription(e.target.value)} /></div>
          <div className="grid grid-cols-2 gap-3">
            <div><label className="flbl">Etiket/Rozet</label>
              <input className="inp" value={badge} onChange={e => setBadge(e.target.value)} placeholder="ör. Süper Fırsat" /></div>
            <div><label className="flbl">Öncelik</label>
              <input className="inp" type="number" value={priority} onChange={e => setPriority(Number(e.target.value))} /></div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div><label className="flbl">Başlangıç</label>
              <input className="inp" type="date" value={starts} onChange={e => setStarts(e.target.value)} /></div>
            <div><label className="flbl">Bitiş (boş = süresiz)</label>
              <input className="inp" type="date" value={ends} onChange={e => setEnds(e.target.value)} /></div>
          </div>
          <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
            <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} /> Aktif
          </label>
        </div>
      )}

      {/* PARAMETRELER — SettingsSchema'dan üretilir */}
      {tab === 'Parametreler' && (
        <div className="card p-4 space-y-3 max-w-2xl">
          {!selType && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Önce Genel sekmesinden kampanya tipi seçin.</p>}
          {selType && (selType.settingsSchema ?? []).filter(f => fieldVisible(f, settings)).map(f => (
            <div key={f.key}>
              <label className="flbl">{tr(f.labelI18n)}{f.unit ? ` (${f.unit})` : ''}{f.required && <span className="text-red-500"> *</span>}</label>
              {f.type === 'boolean' ? (
                <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
                  <input type="checkbox" checked={!!settings[f.key]} onChange={e => setField(f.key, e.target.checked)} /> Evet
                </label>
              ) : f.type === 'select' ? (
                <select className="inp" value={String(settings[f.key] ?? f.default ?? '')} onChange={e => setField(f.key, e.target.value)}>
                  <option value="">Seçin</option>
                  {(f.options ?? []).map(o => <option key={o.value} value={o.value}>{tr(o.labelI18n)}</option>)}
                </select>
              ) : (
                <input className="inp" type="number" value={String(settings[f.key] ?? '')}
                  onChange={e => setField(f.key, e.target.value === '' ? '' : Number(e.target.value))} />
              )}
              {tr(f.helpI18n) && <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{tr(f.helpI18n)}</p>}
            </div>
          ))}
        </div>
      )}

      {/* ÜRÜN KAPSAMI */}
      {tab === 'Ürün Kapsamı' && showProductScope && (
        <div className="card p-4 space-y-4 max-w-3xl">
          <div className="max-w-md">
            <label className="flbl">Doldurma tipi</label>
            <select className="inp" value={fillType} onChange={e => setFillType(e.target.value)}>
              {FILL_OPTS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>

          {(fillType === 'filter' || fillType === 'mixed') && (
            <div>
              <div className="text-xs font-semibold mb-2" style={{ color: 'var(--text-s)' }}>FİLTRE KURALLARI</div>
              <FilterBuilder value={filterDef} onChange={(def) => setFilterDef(def)} />
            </div>
          )}

          {(fillType === 'manual' || fillType === 'mixed') && (
            <div>
              <div className="text-xs font-semibold mb-2" style={{ color: 'var(--text-s)' }}>MANUEL ÜRÜNLER ({manualIds.length})</div>
              <div className="flex gap-2 mb-2">
                <select className="inp" value={addProdId} onChange={e => setAddProdId(e.target.value)}>
                  <option value="">Ürün seç…</option>
                  {allProducts.filter(p => !manualIds.includes(p.id)).map(p => (
                    <option key={p.id} value={p.id}>{tr(p.nameI18n)} ({p.code})</option>
                  ))}
                </select>
                <Button size="sm" variant="secondary" onClick={() => { if (addProdId) { setManualIds(ids => [...ids, addProdId]); setAddProdId('') } }}>Ekle</Button>
              </div>
              <div className="space-y-1">
                {manualProducts.map(p => (
                  <div key={p.id} className="flex items-center justify-between px-3 py-1.5 rounded text-sm"
                    style={{ background: 'var(--surface2)', color: 'var(--text)' }}>
                    <span>{tr(p.nameI18n)} <code className="text-xs" style={{ color: 'var(--text-s)' }}>{p.code}</code></span>
                    <button className="text-xs" style={{ color: '#b91c1c' }} onClick={() => setManualIds(ids => ids.filter(x => x !== p.id))}>Kaldır</button>
                  </div>
                ))}
              </div>
            </div>
          )}
          {fillType === 'all' && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Kampanya tüm ürünlere uygulanır.</p>}
        </div>
      )}
    </div>
  )
}
