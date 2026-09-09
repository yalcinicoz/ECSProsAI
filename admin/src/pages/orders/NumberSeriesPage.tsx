import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { CheckCircle, Plus } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { DataGrid, useGridState, useLocalGrid, type GridColumn } from '@/components/grid'
import { errText as gridErrText } from '@/components/ui/DataTable.utils'

function apiErrorMessage(error: unknown, fallback: string): string {
  if (typeof error !== 'object' || error === null || !('response' in error)) return fallback
  const response = error.response
  if (typeof response !== 'object' || response === null || !('data' in response)) return fallback
  const data = response.data
  if (typeof data !== 'object' || data === null || !('error' in data)) return fallback
  return typeof data.error === 'string' ? data.error : fallback
}

// ── Types ─────────────────────────────────────────────────────────────────────

interface SeriesRow {
  firmPlatformId: string
  channelCode: string
  channelName: string | null
  hasSeries: boolean
  prefix: string | null
  padLength: number | null
  nextValue: number | null
  isActive: boolean | null
}

interface FirmRow { id: string; code: string; nameI18n: Record<string, string> }

interface CargoIntegrationRow {
  id: string
  serviceCode: string
  serviceNameI18n: Record<string, string>
  name: string | null
  isActive: boolean
}

interface RangeRow {
  id: string
  firmPlatformIntegrationId: string
  rangeStart: number
  rangeEnd: number
  nextValue: number
  isActive: boolean
  exhaustedAt: string | null
  total: number
  used: number
}

const trName = (i18n: Record<string, string> | null, fallback: string) =>
  i18n?.['tr'] ?? (i18n ? i18n[Object.keys(i18n)[0]] : undefined) ?? fallback

// ── Seri tablosu (sipariş / paket ortak) ──────────────────────────────────────

function SeriesTable({ title, hint, endpoint, queryKey, sampleOf }: {
  title: string
  hint: string
  endpoint: string           // GET listesi + PUT {endpoint}/{firmPlatformId}
  queryKey: string
  sampleOf: (prefix: string, pad: number) => string
}) {
  const queryClient = useQueryClient()
  const [edits, setEdits] = useState<Record<string, { prefix: string; padLength: string; isActive: boolean }>>({})
  const [savedId, setSavedId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  // DataGrid (2026-09-09, tur 12) — YEREL değerlendirme (useLocalGrid): bu tablo bir LİSTE değil
  // FORM'dur, satır kümesi "kanal başına bir satır"dır ve sunucuda sayfalanmaz (serisiz kanallar da
  // görünmek zorunda: kaynak sorgu core_firm_platforms LEFT JOIN seri). Sayfalamak formu bölerdi;
  // buna karşılık her sütunda sıralama/süzgeç ve mobil Kompakt görünüm yerelde sağlanır.
  const grid = useGridState(queryKey, { defaultPageSize: 50, defaultSort: 'channel', defaultDir: 'asc' })
  const { data: rows = [], isLoading, isFetching, error: listError } = useQuery<SeriesRow[]>({
    queryKey: [queryKey],
    queryFn: async () => (await api.get(endpoint)).data.data ?? [],
  })

  const save = useMutation({
    mutationFn: async (r: SeriesRow) => {
      const e = edits[r.firmPlatformId]
      await api.put(`${endpoint}/${r.firmPlatformId}`, {
        prefix: e?.prefix ?? r.prefix ?? '',
        padLength: e?.padLength ? Number(e.padLength) : (r.padLength ?? 6),
        isActive: e?.isActive ?? r.isActive ?? true,
      })
      return r.firmPlatformId
    },
    onSuccess: (id) => {
      queryClient.invalidateQueries({ queryKey: [queryKey] })
      setEdits(prev => { const p = { ...prev }; delete p[id]; return p })
      setError(null)
      setSavedId(id)
      setTimeout(() => setSavedId(null), 2000)
    },
    onError: (err: unknown) => setError(apiErrorMessage(err, 'Kaydedilemedi.')),
  })

  const edit = (r: SeriesRow) => edits[r.firmPlatformId] ?? {
    prefix: r.prefix ?? '',
    padLength: String(r.padLength ?? 6),
    isActive: r.isActive ?? true,
  }

  const setEdit = (id: string, patch: Partial<{ prefix: string; padLength: string; isActive: boolean }>) =>
    setEdits(prev => ({ ...prev, [id]: { ...(prev[id] ?? edit(rows.find(r => r.firmPlatformId === id)!)), ...patch } }))

  const yerel = useLocalGrid(rows, grid, {
    values: {
      channel: r => r.channelName ?? r.channelCode,
      channelCode: r => r.channelCode,
      prefix: r => r.prefix ?? '',
      padLength: r => r.padLength ?? 0,
      nextValue: r => Number(r.nextValue ?? 0),
      hasSeries: r => r.hasSeries,
      isActive: r => r.isActive ?? false,
    },
    search: r => `${r.channelName ?? ''} ${r.channelCode} ${r.prefix ?? ''}`,
  })

  const columns: GridColumn<SeriesRow>[] = [
    { key: 'channel', header: 'KANAL', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 220,
      filter: { type: 'text', label: 'Kanal' },
      filters: [{ field: 'channelCode', label: 'Kanal kodu', type: 'text' }],
      cell: r => <span>
        <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{r.channelName ?? r.channelCode}</span>
        <code className="text-xs ml-2" style={{ color: 'var(--text-s)' }}>{r.channelCode}</code>
      </span> },
    { key: 'prefix', header: 'ÖNEK', priority: 1, sortable: true, stopRowClick: true, minWidth: 110,
      filter: { type: 'text', label: 'Önek' },
      cell: r => <input className="inp" style={{ width: 90 }} maxLength={10} value={edit(r).prefix}
        placeholder="örn. MIS"
        onChange={ev => setEdit(r.firmPlatformId, { prefix: ev.target.value.toUpperCase() })} /> },
    { key: 'padLength', header: 'DOLGU', priority: 2, sortable: true, stopRowClick: true,
      filter: { type: 'number', label: 'Dolgu' },
      cell: r => <input className="inp" style={{ width: 64 }} type="number" min={4} max={12} value={edit(r).padLength}
        onChange={ev => setEdit(r.firmPlatformId, { padLength: ev.target.value })} /> },
    { key: 'ornek', header: 'ÖRNEK', priority: 2, minWidth: 130,
      cell: r => <code className="text-xs" style={{ color: 'var(--brand)' }}>
        {sampleOf(edit(r).prefix, Number(edit(r).padLength) || 6)}</code> },
    { key: 'nextValue', header: 'SIRADAKİ', priority: 2, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Sıradaki değer' },
      filters: [{ field: 'hasSeries', label: 'Serisi açılmış', type: 'boolean' }],
      cell: r => <span className="text-sm tabular-nums" style={{ color: 'var(--text-m)' }}>
        {r.hasSeries ? r.nextValue : '—'}</span> },
    { key: 'isActive', header: 'AKTİF', priority: 1, align: 'center', sortable: true, stopRowClick: true,
      filter: { type: 'boolean', label: 'Aktif' },
      cell: r => <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
        checked={edit(r).isActive}
        onChange={ev => setEdit(r.firmPlatformId, { isActive: ev.target.checked })} /> },
    { key: 'islem', header: '', priority: 1, align: 'right', exportable: false, stopRowClick: true, minWidth: 150,
      cell: r => <span className="whitespace-nowrap">
        {savedId === r.firmPlatformId && (
          <span className="inline-flex items-center gap-1 text-xs mr-2" style={{ color: '#16a34a' }}>
            <CheckCircle size={12} /> Kaydedildi
          </span>
        )}
        <Button size="sm" variant={edits[r.firmPlatformId] ? 'primary' : 'secondary'}
          disabled={!edits[r.firmPlatformId] && r.hasSeries}
          loading={save.isPending && save.variables === r}
          onClick={() => save.mutate(r)}>
          {r.hasSeries ? 'Kaydet' : 'Seri Aç'}
        </Button>
      </span> },
  ]

  return (
    <div>
      <div className="mb-2">
        <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>{title}</h2>
        <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{hint}</p>
      </div>
      <DataGrid<SeriesRow>
        gridId={queryKey}
        views
        grid={grid}
        columns={columns}
        rows={yerel.rows}
        totalCount={yerel.totalCount}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? gridErrText(listError) : (error || null)}
        rowKey={r => r.firmPlatformId}
        empty="Ölçütlere uyan kanal yok."
        search={{ placeholder: 'Kanal adı, kodu veya önek ara…' }}
        minWidth={1020}
        pageSizes={[50, 100, 200]}
        compact={{
          title: r => r.channelName ?? r.channelCode,
          subtitle: r => r.hasSeries
            ? `${r.prefix ?? ''} · dolgu ${r.padLength ?? 6} · sıradaki ${r.nextValue}`
            : 'seri açılmamış',
          right: r => sampleOf(edit(r).prefix, Number(edit(r).padLength) || 6),
          badge: r => <Badge variant={r.isActive ? 'success' : 'neutral'}>{r.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />
      <p className="px-1 py-2 text-xs" style={{ color: 'var(--text-s)' }}>
        Sıradaki değer elle değiştirilemez; kullanılan numaralar iptalde bile havuza geri dönmez.
      </p>
    </div>
  )
}

// ── Kargo barkod aralıkları ───────────────────────────────────────────────────

function CargoRangesCard() {
  const queryClient = useQueryClient()
  const [firmId, setFirmId] = useState('')
  const [integrationId, setIntegrationId] = useState('')
  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  const [error, setError] = useState<string | null>(null)

  const { data: firms = [] } = useQuery<FirmRow[]>({
    queryKey: ['firms-for-ranges'],
    queryFn: async () => (await api.get('/core/firms?activeOnly=false')).data.data ?? [],
  })

  const { data: integrations = [] } = useQuery<CargoIntegrationRow[]>({
    queryKey: ['cargo-integrations', firmId],
    enabled: !!firmId,
    queryFn: async () =>
      (await api.get(`/core/firms/${firmId}/integrations?serviceType=cargo`)).data.data ?? [],
  })

  // Aralıklar entegrasyon başına birkaç satırdır ve tam gelir (sunucuda sayfalanmaz) → yerel grid.
  const rangeGrid = useGridState('cargo-barcode-ranges', { defaultPageSize: 50, defaultSort: 'rangeStart', defaultDir: 'asc' })
  const { data: ranges = [], isLoading: rangesLoading, isFetching: rangesFetching } = useQuery<RangeRow[]>({
    queryKey: ['cargo-barcode-ranges', integrationId],
    enabled: !!integrationId,
    queryFn: async () =>
      (await api.get(`/core/cargo-barcode-ranges?firmPlatformIntegrationId=${integrationId}`)).data.data ?? [],
  })

  const createRange = useMutation({
    mutationFn: async () => {
      await api.post('/core/cargo-barcode-ranges', {
        firmPlatformIntegrationId: integrationId,
        rangeStart: Number(start),
        rangeEnd: Number(end),
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['cargo-barcode-ranges', integrationId] })
      setStart(''); setEnd(''); setError(null)
    },
    onError: (err: unknown) => setError(apiErrorMessage(err, 'Aralık eklenemedi.')),
  })

  const toggleActive = useMutation({
    mutationFn: async (r: RangeRow) => {
      await api.put(`/core/cargo-barcode-ranges/${r.id}/active`, { isActive: !r.isActive })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cargo-barcode-ranges', integrationId] }),
  })

  const doluluk = (r: RangeRow) => r.total > 0 ? Math.round((r.used / r.total) * 100) : 0

  const yerelAralik = useLocalGrid(ranges, rangeGrid, {
    values: {
      rangeStart: r => r.rangeStart,
      rangeEnd: r => r.rangeEnd,
      used: r => r.used,
      total: r => r.total,
      doluluk: r => doluluk(r),
      isActive: r => r.isActive,
      tukendi: r => !!r.exhaustedAt,
    },
    search: r => `${r.rangeStart} ${r.rangeEnd}`,
  })

  const rangeColumns: GridColumn<RangeRow>[] = [
    { key: 'rangeStart', header: 'ARALIK', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 180,
      filter: { type: 'number', label: 'Başlangıç' },
      filters: [{ field: 'rangeEnd', label: 'Bitiş', type: 'number' }],
      cell: r => <code className="text-sm">{r.rangeStart} – {r.rangeEnd}</code> },
    { key: 'used', header: 'KULLANIM', priority: 1, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Kullanılan' },
      filters: [{ field: 'total', label: 'Toplam', type: 'number' }],
      cell: r => <span className="text-sm tabular-nums" style={{ color: 'var(--text-m)' }}>
        {r.used} / {r.total}</span> },
    { key: 'doluluk', header: 'DOLULUK', priority: 1, sortable: true, minWidth: 180,
      filter: { type: 'number', label: 'Doluluk (%)' },
      cell: r => {
        const pct = doluluk(r)
        return <div className="flex items-center gap-2">
          <div className="flex-1 h-2 rounded-full overflow-hidden" style={{ background: 'var(--surface2)' }}>
            <div className="h-full rounded-full"
              style={{ width: `${pct}%`, background: pct >= 90 ? '#ef4444' : pct >= 70 ? '#f59e0b' : 'var(--brand)' }} />
          </div>
          <span className="text-xs tabular-nums" style={{ color: pct >= 90 ? '#ef4444' : 'var(--text-s)' }}>%{pct}</span>
        </div>
      } },
    { key: 'isActive', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [{ field: 'tukendi', label: 'Tükenmiş', type: 'boolean' }],
      cell: r => r.exhaustedAt
        ? <Badge variant="danger">Tükendi</Badge>
        : <Badge variant={r.isActive ? 'success' : 'neutral'}>{r.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'islem', header: '', priority: 2, align: 'right', exportable: false, stopRowClick: true, minWidth: 120,
      cell: r => !r.exhaustedAt
        ? <button className="text-xs px-2 py-1 rounded-lg"
            style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
            onClick={() => toggleActive.mutate(r)}>
            {r.isActive ? 'Pasifleştir' : 'Aktifleştir'}
          </button>
        : null },
  ]

  return (
    <div className="card overflow-hidden p-0">
      <div className="px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
        <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Kargo Barkod Aralıkları</h2>
        <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
          Tahsisli aralık (range) stratejili taşıyıcılar için — örn. PTT'nin verdiği barkod aralıkları.
          Aralık sınırları ve sayaç sonradan değiştirilemez; tahsis edilen barkod havuza geri dönmez.
        </p>
      </div>

      <div className="px-4 py-3 flex flex-wrap items-end gap-3" style={{ borderBottom: '1px solid var(--border)' }}>
        <div>
          <label className="flbl">Firma</label>
          <select className="sel" style={{ width: 200 }} value={firmId}
            onChange={e => { setFirmId(e.target.value); setIntegrationId('') }}>
            <option value="">— seçin</option>
            {firms.map(f => <option key={f.id} value={f.id}>{trName(f.nameI18n, f.code)}</option>)}
          </select>
        </div>
        <div>
          <label className="flbl">Kargo Entegrasyonu</label>
          <select className="sel" style={{ width: 220 }} value={integrationId} disabled={!firmId}
            onChange={e => setIntegrationId(e.target.value)}>
            <option value="">— seçin</option>
            {integrations.map(i => (
              <option key={i.id} value={i.id}>
                {trName(i.serviceNameI18n, i.serviceCode)}{i.name ? ` (${i.name})` : ''}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className="flbl">Başlangıç</label>
          <input className="inp" style={{ width: 130 }} type="number" value={start}
            onChange={e => setStart(e.target.value)} disabled={!integrationId} />
        </div>
        <div>
          <label className="flbl">Bitiş</label>
          <input className="inp" style={{ width: 130 }} type="number" value={end}
            onChange={e => setEnd(e.target.value)} disabled={!integrationId} />
        </div>
        <Button size="sm" disabled={!integrationId || !start || !end}
          loading={createRange.isPending} onClick={() => createRange.mutate()}>
          <Plus size={14} /> Aralık Ekle
        </Button>
      </div>

      {error && <p className="px-4 py-2 text-sm" style={{ color: '#ef4444' }}>{error}</p>}

      {integrationId && (
        <div className="px-4 pb-4">
          <DataGrid<RangeRow>
            gridId="cargo-barcode-ranges"
            grid={rangeGrid}
            columns={rangeColumns}
            rows={yerelAralik.rows}
            totalCount={yerelAralik.totalCount}
            loading={rangesLoading}
            fetching={rangesFetching}
            rowKey={r => r.id}
            empty="Bu entegrasyona tanımlı aralık yok."
            search={{ placeholder: 'Aralık ara…' }}
            minWidth={860}
            pageSizes={[50, 100]}
            compact={{
              title: r => `${r.rangeStart} – ${r.rangeEnd}`,
              subtitle: r => `${r.used} / ${r.total} kullanıldı`,
              right: r => `%${r.total > 0 ? Math.round((r.used / r.total) * 100) : 0}`,
              badge: r => r.exhaustedAt
                ? <Badge variant="danger">Tükendi</Badge>
                : <Badge variant={r.isActive ? 'success' : 'neutral'}>{r.isActive ? 'Aktif' : 'Pasif'}</Badge>,
            }}
          />
        </div>
      )}
    </div>
  )
}

// ── Sayfa ─────────────────────────────────────────────────────────────────────

export function NumberSeriesPage() {
  return (
    <div className="p-6 space-y-6">
      <div>
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Numara Serileri</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Kanala özel sipariş ve paket numarası serileri + kargo barkod aralıkları.
          Pazaryeri siparişlerinde seri kullanılmaz; pazaryerinin numarası aynen saklanır.
        </p>
      </div>

      <SeriesTable
        title="Sipariş Numarası Serileri"
        hint="Her satış kanalının kendi serisi vardır; numara = önek + soldan sıfır dolgulu sayaç."
        endpoint="/orders/number-series"
        queryKey="order-number-series"
        sampleOf={(p, pad) => p + '1'.padStart(pad, '0')}
      />

      <SeriesTable
        title="Paket Numarası Serileri"
        hint="Paket numarası siparişten bağımsız, kanala özel seriden üretilir (~6 hane önerilir)."
        endpoint="/fulfillment/package-number-series"
        queryKey="package-number-series"
        sampleOf={(p, pad) => p + '1'.padStart(pad, '0')}
      />

      <CargoRangesCard />
    </div>
  )
}
