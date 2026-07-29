import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { CheckCircle, Plus } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { PageSpinner } from '@/components/ui/Spinner'

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

  const { data: rows = [], isLoading } = useQuery<SeriesRow[]>({
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
    onError: (err: any) => setError(err?.response?.data?.error ?? 'Kaydedilemedi.'),
  })

  const edit = (r: SeriesRow) => edits[r.firmPlatformId] ?? {
    prefix: r.prefix ?? '',
    padLength: String(r.padLength ?? 6),
    isActive: r.isActive ?? true,
  }

  const setEdit = (id: string, patch: Partial<{ prefix: string; padLength: string; isActive: boolean }>) =>
    setEdits(prev => ({ ...prev, [id]: { ...(prev[id] ?? edit(rows.find(r => r.firmPlatformId === id)!)), ...patch } }))

  if (isLoading) return <PageSpinner />

  return (
    <div className="card overflow-hidden p-0">
      <div className="px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
        <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>{title}</h2>
        <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{hint}</p>
      </div>
      <table className="w-full">
        <thead>
          <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
            {['KANAL', 'ÖNEK', 'DOLGU', 'ÖRNEK', 'SIRADAKİ', 'AKTİF', ''].map(h => (
              <th key={h} className="px-4 py-2.5 text-left text-xs font-semibold tracking-wider"
                style={{ color: 'var(--text-s)' }}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map(r => {
            const e = edit(r)
            const dirty = !!edits[r.firmPlatformId]
            return (
              <tr key={r.firmPlatformId} style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-2.5">
                  <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>
                    {r.channelName ?? r.channelCode}
                  </span>
                  <code className="text-xs ml-2" style={{ color: 'var(--text-s)' }}>{r.channelCode}</code>
                </td>
                <td className="px-4 py-2.5">
                  <input className="inp" style={{ width: 90 }} maxLength={10} value={e.prefix}
                    placeholder="örn. MIS"
                    onChange={ev => setEdit(r.firmPlatformId, { prefix: ev.target.value.toUpperCase() })} />
                </td>
                <td className="px-4 py-2.5">
                  <input className="inp" style={{ width: 64 }} type="number" min={4} max={12} value={e.padLength}
                    onChange={ev => setEdit(r.firmPlatformId, { padLength: ev.target.value })} />
                </td>
                <td className="px-4 py-2.5">
                  <code className="text-xs" style={{ color: 'var(--brand)' }}>
                    {sampleOf(e.prefix, Number(e.padLength) || 6)}
                  </code>
                </td>
                <td className="px-4 py-2.5">
                  <span className="text-sm tabular-nums" style={{ color: 'var(--text-m)' }}>
                    {r.hasSeries ? r.nextValue : '—'}
                  </span>
                </td>
                <td className="px-4 py-2.5">
                  <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
                    checked={e.isActive}
                    onChange={ev => setEdit(r.firmPlatformId, { isActive: ev.target.checked })} />
                </td>
                <td className="px-4 py-2.5 text-right whitespace-nowrap">
                  {savedId === r.firmPlatformId && (
                    <span className="inline-flex items-center gap-1 text-xs mr-2" style={{ color: '#16a34a' }}>
                      <CheckCircle size={12} /> Kaydedildi
                    </span>
                  )}
                  <Button size="sm" variant={dirty ? 'primary' : 'secondary'}
                    disabled={!dirty && r.hasSeries}
                    loading={save.isPending && save.variables === r}
                    onClick={() => save.mutate(r)}>
                    {r.hasSeries ? 'Kaydet' : 'Seri Aç'}
                  </Button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
      {error && <p className="px-4 py-2 text-sm" style={{ color: '#ef4444' }}>{error}</p>}
      <p className="px-4 py-2 text-xs" style={{ borderTop: '1px solid var(--border)', color: 'var(--text-s)' }}>
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

  const { data: ranges = [] } = useQuery<RangeRow[]>({
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
    onError: (err: any) => setError(err?.response?.data?.error ?? 'Aralık eklenemedi.'),
  })

  const toggleActive = useMutation({
    mutationFn: async (r: RangeRow) => {
      await api.put(`/core/cargo-barcode-ranges/${r.id}/active`, { isActive: !r.isActive })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cargo-barcode-ranges', integrationId] }),
  })

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
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['ARALIK', 'KULLANIM', 'DOLULUK', 'DURUM', ''].map(h => (
                <th key={h} className="px-4 py-2.5 text-left text-xs font-semibold tracking-wider"
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {ranges.length === 0 && (
              <tr><td colSpan={5} className="px-4 py-6 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Bu entegrasyona tanımlı aralık yok.
              </td></tr>
            )}
            {ranges.map(r => {
              const pct = r.total > 0 ? Math.round((r.used / r.total) * 100) : 0
              return (
                <tr key={r.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-2.5">
                    <code className="text-sm">{r.rangeStart} – {r.rangeEnd}</code>
                  </td>
                  <td className="px-4 py-2.5 text-sm tabular-nums" style={{ color: 'var(--text-m)' }}>
                    {r.used} / {r.total}
                  </td>
                  <td className="px-4 py-2.5" style={{ minWidth: 160 }}>
                    <div className="flex items-center gap-2">
                      <div className="flex-1 h-2 rounded-full overflow-hidden" style={{ background: 'var(--surface2)' }}>
                        <div className="h-full rounded-full"
                          style={{ width: `${pct}%`, background: pct >= 90 ? '#ef4444' : pct >= 70 ? '#f59e0b' : 'var(--brand)' }} />
                      </div>
                      <span className="text-xs tabular-nums" style={{ color: pct >= 90 ? '#ef4444' : 'var(--text-s)' }}>%{pct}</span>
                    </div>
                  </td>
                  <td className="px-4 py-2.5">
                    {r.exhaustedAt
                      ? <Badge variant="danger">Tükendi</Badge>
                      : <Badge variant={r.isActive ? 'success' : 'neutral'}>{r.isActive ? 'Aktif' : 'Pasif'}</Badge>}
                  </td>
                  <td className="px-4 py-2.5 text-right">
                    {!r.exhaustedAt && (
                      <button className="text-xs px-2 py-1 rounded-lg"
                        style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                        onClick={() => toggleActive.mutate(r)}>
                        {r.isActive ? 'Pasifleştir' : 'Aktifleştir'}
                      </button>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
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
