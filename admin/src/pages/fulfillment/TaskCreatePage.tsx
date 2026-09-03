import { useEffect, useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { DataTable } from '@/components/ui/DataTable'
import { errText, tarihSaat } from '@/components/ui/DataTable.utils'
import { PLAN_TIP } from './pickingPlanHelpers'

// ── API tipleri ──────────────────────────────────────────────────────────────
interface CandidatePreview {
  orderId: string
  orderNumber: string
  firmPlatformId: string
  createdAt: string
  totalQuantity: number
  cargoIntegrationId?: string | null
  cargoName?: string | null
  shippingCityId: string
  warehouseIds: string[]
  karmaDepolu: boolean
}

interface Candidates {
  toplamSiparis: number
  tekUrunlu: number
  cokUrunlu: number
  karmaDepoluHaricTutulan: number
  onizleme: CandidatePreview[]
}

interface CreatedTask { planId: string; planNumber: string; planType: string; orderCount: number; lineCount: number }

interface Warehouse { id: string; code: string; nameI18n?: Record<string, string> }
interface FirmPlatform { id: string; firmId: string; nameI18n: Record<string, string> }
interface GeoItem { id: string; nameI18n?: Record<string, string>; name?: string }

const geoAd = (g: GeoItem) => g.nameI18n?.['tr'] ?? g.name ?? '—'

export function TaskCreatePage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  // ── Filtre state ──
  const [kanallar, setKanallar] = useState<string[]>([])
  const [depoId, setDepoId] = useState<string | null>(null)
  const [adetMode, setAdetMode] = useState<'all' | 'single' | 'multi'>('all')
  const [minItems, setMinItems] = useState('2')
  const [maxItems, setMaxItems] = useState('')
  const [kargoId, setKargoId] = useState<string | null>(null)
  const [sehirId, setSehirId] = useState<string | null>(null)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')

  // ── Görev tipleri ──

  const [error, setError] = useState('')
  const [created, setCreated] = useState<CreatedTask[] | null>(null)

  // ── Yardımcı listeler ──
  const { data: warehouses = [] } = useQuery<Warehouse[]>({
    queryKey: ['warehouses'],
    queryFn: async () => (await api.get('/inventory/warehouses')).data.data,
  })

  const { data: firms = [] } = useQuery<{ id: string; nameI18n: Record<string, string> }[]>({
    queryKey: ['firms'],
    queryFn: async () => (await api.get('/core/firms')).data.data,
  })
  const { data: firmPlatforms = [] } = useQuery<FirmPlatform[]>({
    queryKey: ['all-firm-platforms', firms.map(f => f.id).join(',')],
    queryFn: async () => {
      const all: FirmPlatform[] = []
      for (const f of firms) {
        const { data } = await api.get(`/core/firms/${f.id}/platforms`)
        for (const p of data.data ?? []) all.push({ ...p, firmId: f.id })
      }
      return all
    },
    enabled: firms.length > 0,
  })
  const platformAd = (id: string) => firmPlatforms.find(p => p.id === id)?.nameI18n?.['tr'] ?? '—'

  // Şehir listesi (adres formlarının kullandığı store geo uçları)
  const { data: ulkeler = [] } = useQuery<{ id: string; code: string }[]>({
    queryKey: ['geo-countries'],
    queryFn: async () => (await api.get('/store/geo/countries')).data.data,
    retry: false,
  })
  const ulkeId = ulkeler.find(u => u.code === 'TR')?.id ?? ulkeler[0]?.id
  const { data: iller = [] } = useQuery<GeoItem[]>({
    queryKey: ['geo-cities', ulkeId],
    queryFn: async () => (await api.get(`/store/geo/cities?countryId=${ulkeId}`)).data.data,
    enabled: !!ulkeId,
    retry: false,
  })

  // ── Debounce'lu candidates sorgusu ──
  const paramStr = useMemo(() => {
    const p = new URLSearchParams()
    kanallar.forEach(k => p.append('firmPlatformIds', k))
    if (depoId) p.set('warehouseId', depoId)
    if (adetMode === 'single') { p.set('minItems', '1'); p.set('maxItems', '1') }
    if (adetMode === 'multi') {
      p.set('minItems', String(Math.max(2, parseInt(minItems) || 2)))
      if (maxItems && parseInt(maxItems) >= 2) p.set('maxItems', String(parseInt(maxItems)))
    }
    if (kargoId) p.set('cargoIntegrationId', kargoId)
    if (sehirId) p.set('shippingCityId', sehirId)
    if (from) p.set('from', from)
    if (to) p.set('to', `${to}T23:59:59`)
    return p.toString()
  }, [kanallar, depoId, adetMode, minItems, maxItems, kargoId, sehirId, from, to])

  const [debouncedParams, setDebouncedParams] = useState(paramStr)
  useEffect(() => {
    const t = setTimeout(() => setDebouncedParams(paramStr), 400)
    return () => clearTimeout(t)
  }, [paramStr])

  const { data: aday, isFetching } = useQuery<Candidates>({
    queryKey: ['task-candidates', debouncedParams],
    queryFn: async () => (await api.get(`/fulfillment/task-candidates?${debouncedParams}`)).data.data,
    placeholderData: prev => prev,
  })

  // Önceki filtre sonuçları React Query önbelleğinde kaldığı sürece kargo seçeneklerini
  // birlikte göster; böylece seçili kargo filtresi diğer seçenekleri listeden düşürmez.
  const kargoOpts: Record<string, string> = {}
  for (const [, candidates] of queryClient.getQueriesData<Candidates>({ queryKey: ['task-candidates'] })) {
    for (const order of candidates?.onizleme ?? []) {
      if (order.cargoIntegrationId) kargoOpts[order.cargoIntegrationId] = order.cargoName ?? 'Kargo'
    }
  }

  // ── Görev oluşturma ──
  const olustur = useMutation({
    mutationFn: async () => {
      setError('')
      const body: Record<string, unknown> = {
        firmPlatformIds: kanallar.length > 0 ? kanallar : null,
        warehouseId: depoId || null,
        minItems: adetMode === 'single' ? 1 : adetMode === 'multi' ? Math.max(2, parseInt(minItems) || 2) : null,
        maxItems: adetMode === 'single' ? 1 : adetMode === 'multi' && maxItems ? parseInt(maxItems) : null,
        cargoIntegrationId: kargoId || null,
        shippingCityId: sehirId || null,
        from: from || null,
        to: to ? `${to}T23:59:59` : null,
        // Görev tipleri "Ürün sayısı" filtresinden türetilir (kullanıcı kararı 2026-08-09):
        // Tümü → iki görev birden (otomatik ayrım), Tek → yalnız tek ürünlü, Çok → yalnız çok ürünlü
        createSingleItemTask: adetMode !== 'multi',
        createMultiItemTask: adetMode !== 'single',
      }
      return (await api.post('/fulfillment/tasks', body)).data.data as { tasks: CreatedTask[] }
    },
    onSuccess: d => setCreated(d.tasks),
    onError: (e: unknown) => setError(errText(e)),
  })

  const toggleKanal = (id: string) =>
    setKanallar(prev => (prev.includes(id) ? prev.filter(k => k !== id) : [...prev, id]))

  const rows = (aday?.onizleme ?? []).map(o => ({ ...o, id: o.orderId }))

  return (
    <div className="p-6">
      <div className="flex items-center gap-3 mb-4">
        <button onClick={() => navigate('/fulfillment/picking-plans')} className="text-sm" style={{ color: 'var(--text-s)' }}>←</button>
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Görev Oluşturma</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Filtreye uyan onaylı siparişlerden tek/çok ürünlü toplama görevleri oluşturulur.
          </p>
        </div>
      </div>

      {/* ── Filtre kartı ── */}
      <div className="card p-4 mb-4">
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {/* Kanallar */}
          <div>
            <label className="flbl">Kanallar</label>
            <div className="thin-scroll rounded-xl p-2 overflow-y-auto" style={{ border: '1px solid var(--border)', maxHeight: 132 }}>
              {firmPlatforms.length === 0 && (
                <p className="text-xs px-1 py-2" style={{ color: 'var(--text-s)' }}>Kanal listesi yükleniyor…</p>
              )}
              {firmPlatforms.map(p => (
                <label key={p.id} className="flex items-center gap-2 px-1 py-1 text-sm cursor-pointer" style={{ color: 'var(--text)' }}>
                  <input type="checkbox" checked={kanallar.includes(p.id)} onChange={() => toggleKanal(p.id)} />
                  {p.nameI18n?.['tr'] ?? p.id}
                </label>
              ))}
            </div>
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Boş bırakılırsa tüm kanallar dahildir.</p>
          </div>

          <div className="space-y-3">
            {/* Depo */}
            <div>
              <label className="flbl">Depo</label>
              <SearchableSelect
                value={depoId}
                onChange={setDepoId}
                clearable
                placeholder="Tüm depolar"
                options={warehouses.map(w => ({ value: w.id, label: w.nameI18n?.['tr'] ?? w.code }))}
              />
              {depoId && (aday?.karmaDepoluHaricTutulan ?? 0) > 0 && (
                <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
                  Karma depolu siparişler bu depo seçiliyken hariç tutulur.
                </p>
              )}
            </div>
            {/* Kargo */}
            <div>
              <label className="flbl">Kargo</label>
              <SearchableSelect
                value={kargoId}
                onChange={setKargoId}
                clearable
                placeholder="Tüm kargolar"
                options={Object.entries(kargoOpts).map(([value, label]) => ({ value, label }))}
              />
            </div>
            {/* Şehir */}
            {iller.length > 0 && (
              <div>
                <label className="flbl">Teslimat Şehri</label>
                <SearchableSelect
                  value={sehirId}
                  onChange={setSehirId}
                  clearable
                  placeholder="Tüm şehirler"
                  options={iller.map(i => ({ value: i.id, label: geoAd(i) }))}
                />
              </div>
            )}
          </div>

          <div className="space-y-3">
            {/* Ürün sayısı */}
            <div>
              <label className="flbl">Ürün Sayısı</label>
              <div className="flex gap-2">
                <select className="inp" value={adetMode} onChange={e => setAdetMode(e.target.value as typeof adetMode)}>
                  <option value="all">Tümü</option>
                  <option value="single">Tek ürünlü</option>
                  <option value="multi">Çok ürünlü</option>
                </select>
                {adetMode === 'multi' && (
                  <>
                    <input type="number" min={2} className="inp w-20" value={minItems}
                      onChange={e => setMinItems(e.target.value)} placeholder="Min" title="En az kalem" />
                    <input type="number" min={2} className="inp w-20" value={maxItems}
                      onChange={e => setMaxItems(e.target.value)} placeholder="Maks" title="En çok kalem (boş = sınırsız)" />
                  </>
                )}
              </div>
            </div>
            {/* Tarih aralığı */}
            <div>
              <label className="flbl">Sipariş Tarihi</label>
              <div className="flex items-center gap-2">
                <input type="date" className="inp" value={from} onChange={e => setFrom(e.target.value)} />
                <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>
                <input type="date" className="inp" value={to} onChange={e => setTo(e.target.value)} />
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* ── Özet şeridi + oluşturma ── */}
      <div className="card p-4 mb-4">
        <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
          <p className="text-sm" style={{ color: 'var(--text)' }}>
            {isFetching ? 'Hesaplanıyor…' : (
              <>
                Eşleşen: <b>{aday?.toplamSiparis ?? 0}</b> sipariş
                {' '}(<b>{aday?.tekUrunlu ?? 0}</b> tek / <b>{aday?.cokUrunlu ?? 0}</b> çok)
                {' '}| Karma depolu (hariç): <b>{aday?.karmaDepoluHaricTutulan ?? 0}</b>
              </>
            )}
          </p>
          <div className="flex flex-wrap items-center gap-4 ml-auto">
            <span className="text-sm" style={{ color: 'var(--text-s)' }}>
              {adetMode === 'single' ? 'Tek ürünlü görev oluşturulacak'
                : adetMode === 'multi' ? 'Çok ürünlü görev oluşturulacak'
                : 'Tek + çok ürünlü görevler ayrı ayrı oluşturulacak'}
            </span>
            <Button
              onClick={() => olustur.mutate()}
              loading={olustur.isPending}
              disabled={(aday?.toplamSiparis ?? 0) === 0}>
              Görev(ler)i Oluştur
            </Button>
          </div>
        </div>
        {error && <p className="text-sm text-red-500 mt-2">{error}</p>}
      </div>

      {/* ── Önizleme tablosu ── */}
      <h2 className="text-sm font-semibold mb-2" style={{ color: 'var(--text)' }}>
        Önizleme {aday && aday.toplamSiparis > aday.onizleme.length ? `(ilk ${aday.onizleme.length} / ${aday.toplamSiparis})` : ''}
      </h2>
      <DataTable<CandidatePreview & { id: string }>
        columns={[
          { header: 'SİPARİŞ NO', cell: o => <code className="text-xs font-mono">{o.orderNumber}</code> },
          { header: 'KANAL', cell: o => platformAd(o.firmPlatformId) },
          { header: 'TARİH', cell: o => tarihSaat(o.createdAt) },
          { header: 'ADET', cell: o => o.totalQuantity },
          { header: 'KARGO', cell: o => o.cargoName ?? '—' },
          { header: '', cell: o => o.karmaDepolu ? <Badge variant="warning">Karma depolu</Badge> : null },
        ]}
        rows={rows}
        loading={isFetching && !aday}
        empty="Filtreye uyan sipariş yok."
        onRowClick={o => navigate(`/orders/${o.orderId}`)}
      />

      {/* ── Başarı modalı ── */}
      <Modal open={!!created} onClose={() => navigate('/fulfillment/picking-plans')} title="Görevler Oluşturuldu">
        <div className="space-y-2">
          {(created ?? []).map(t => (
            <div key={t.planId} className="flex flex-wrap items-center gap-2 text-sm py-1.5"
              style={{ borderBottom: '1px solid var(--border)' }}>
              <code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{t.planNumber}</code>
              <Badge variant={t.planType === 'single_item' ? 'info' : 'neutral'}>
                {PLAN_TIP[t.planType] ?? t.planType}
              </Badge>
              <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>
                {t.orderCount} sipariş · {t.lineCount} satır
              </span>
            </div>
          ))}
        </div>
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          {created?.length === 1 && (
            <Button variant="secondary" onClick={() => navigate(`/fulfillment/tasks/${created[0].planId}`)}>
              Göreve Git
            </Button>
          )}
          <Button onClick={() => navigate('/fulfillment/picking-plans')}>Görev Listesine Git</Button>
        </div>
      </Modal>
    </div>
  )
}
