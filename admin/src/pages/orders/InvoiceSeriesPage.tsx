import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { CheckCircle, Plus } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { INVOICE_TYPE_MAP } from './orderConstants'
import type { InvoiceSeries } from './InvoicesPage'

// FE2 (docs/fatura-entegrasyon-plani.md §2.7): seri yönetimi + kanal yuvaları.
// Kural: seri tekil ve TİPLİ; kanal yuvasına yalnız aynı tipteki seri bağlanır;
// kullanılan seri, yerine geçecek (aynı firma + aynı tip) seri verilmeden pasife alınamaz.

const TYPES = ['e_archive', 'e_invoice', 'export'] as const
const SEND_METHOD_MAP: Record<string, string> = {
  manual: 'Yalnız kayıt',
  integrator_api: 'Entegratör API',
  erp: 'ERP gönderir',
  marketplace: 'Pazaryeri keser',
}

interface FirmRow { id: string; code: string; nameI18n: Record<string, string> }

interface ChannelBinding { invoiceType: string; seriesId: string; serial: string; seriesName: string | null; seriesActive: boolean }
interface ChannelSettings {
  firmPlatformId: string
  firmId: string
  channelCode: string
  channelName: string
  channelActive: boolean
  sendMethod: string
  bindings: ChannelBinding[]
  missingTypes: string[]
  warnings: string[]
}

const trName = (i18n: Record<string, string> | null | undefined, fallback: string) =>
  i18n?.['tr'] ?? (i18n ? i18n[Object.keys(i18n)[0]] : undefined) ?? fallback

function apiErrorMessage(error: unknown, fallback: string): string {
  const e = error as { response?: { data?: { error?: string } } }
  return e?.response?.data?.error ?? fallback
}

const fmtDate = (iso: string | null | undefined) => iso ? new Date(iso).toLocaleDateString('tr-TR') : '—'

// ── Yeni seri ─────────────────────────────────────────────────────────────────

function NewSeriesModal({ firms, defaultFirmId, onClose }: { firms: FirmRow[]; defaultFirmId: string; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [firmId, setFirmId] = useState(defaultFirmId)
  const [serial, setSerial] = useState('')
  const [invoiceType, setInvoiceType] = useState<string>('e_archive')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [error, setError] = useState('')

  const create = useMutation({
    mutationFn: async () => {
      await api.post('/orders/invoice-series', { firmId, serial, invoiceType, name: name || null, description: description || null })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['invoice-series'] })
      queryClient.invalidateQueries({ queryKey: ['invoice-series-active'] })
      onClose()
    },
    onError: (e: unknown) => setError(apiErrorMessage(e, 'Seri oluşturulamadı.')),
  })

  return (
    <Modal open onClose={onClose} title="Yeni Fatura Serisi"
      footer={<>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => create.mutate()} loading={create.isPending} disabled={!firmId || serial.length !== 3}>Seri Ekle</Button>
      </>}>
      <div className="space-y-3">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Firma <span className="text-red-500">*</span></label>
            <select className="inp" value={firmId} onChange={e => setFirmId(e.target.value)}>
              <option value="">Firma seçin</option>
              {firms.map(f => <option key={f.id} value={f.id}>{trName(f.nameI18n, f.code)}</option>)}
            </select>
          </div>
          <div>
            <label className="flbl">Ad</label>
            <input className="inp" value={name} onChange={e => setName(e.target.value)} placeholder="ör. Ana e-Arşiv" />
          </div>
          <div>
            <label className="flbl">Seri (3 harf) <span className="text-red-500">*</span></label>
            <input className="inp font-mono" value={serial} maxLength={3} placeholder="MSH"
              onChange={e => setSerial(e.target.value.toUpperCase().replace(/[^A-Z]/g, ''))} />
          </div>
          <div>
            <label className="flbl">Tip <span className="text-red-500">*</span></label>
            <select className="inp" value={invoiceType} onChange={e => setInvoiceType(e.target.value)}>
              {TYPES.map(t => <option key={t} value={t}>{INVOICE_TYPE_MAP[t]}</option>)}
            </select>
          </div>
        </div>
        <div>
          <label className="flbl">Açıklama</label>
          <input className="inp" value={description} onChange={e => setDescription(e.target.value)} placeholder="isteğe bağlı" />
        </div>
        <p className="text-xs" style={{ color: 'var(--text-s)' }}>
          Aynı harfler bir firmada tipten bağımsız yalnız bir kez tanımlanabilir; tip sonradan değiştirilemez.
          Numara örneği: <code>{serial || 'MSH'}{new Date().getFullYear()}000000001</code>
        </p>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
    </Modal>
  )
}

// ── Düzenle ───────────────────────────────────────────────────────────────────

function EditSeriesModal({ series, usedBy, onClose }: { series: InvoiceSeries; usedBy: ChannelSettings[]; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(series.name ?? '')
  const [description, setDescription] = useState(series.description ?? '')
  const [error, setError] = useState('')

  const save = useMutation({
    mutationFn: async () => {
      await api.put(`/orders/invoice-series/${series.id}`, {
        name: name || null, description: description || null, integrationContractId: series.integrationContractId ?? null,
      })
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['invoice-series'] }); onClose() },
    onError: (e: unknown) => setError(apiErrorMessage(e, 'Kaydedilemedi.')),
  })

  return (
    <Modal open onClose={onClose} title={`${series.serial} · ${INVOICE_TYPE_MAP[series.invoiceType] ?? series.invoiceType}`}
      footer={<>
        <Button variant="secondary" onClick={onClose}>Kapat</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending}>Kaydet</Button>
      </>}>
      <div className="space-y-3">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Ad</label>
            <input className="inp" value={name} onChange={e => setName(e.target.value)} />
          </div>
          <div>
            <label className="flbl">Entegratör Sözleşmesi</label>
            <input className="inp" value={series.integrationContractName ?? '— (FE3 ile bağlanacak)'} disabled />
          </div>
        </div>
        <div>
          <label className="flbl">Açıklama</label>
          <input className="inp" value={description} onChange={e => setDescription(e.target.value)} />
        </div>
        <div className="rounded-lg p-3 text-xs space-y-1" style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>
          <div>Son numara: <b>{series.lastYear ? `${series.serial}${series.lastYear}${String(series.lastSequence).padStart(9, '0')}` : 'henüz kesilmedi'}</b>
            {series.lastInvoiceDate && <> · son fatura tarihi {fmtDate(series.lastInvoiceDate)}</>}</div>
          <div>Kullanan kanallar: {usedBy.length === 0 ? 'yok' : usedBy.map(c => c.channelName || c.channelCode).join(', ')}</div>
          {series.retiredAt && <div>Pasife alındı: {fmtDate(series.retiredAt)}</div>}
        </div>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
    </Modal>
  )
}

// ── Pasife al ─────────────────────────────────────────────────────────────────

function DeactivateModal({ series, usedBy, candidates, onClose }: {
  series: InvoiceSeries; usedBy: ChannelSettings[]; candidates: InvoiceSeries[]; onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [replacementId, setReplacementId] = useState('')
  const [error, setError] = useState('')

  const deactivate = useMutation({
    mutationFn: async () => {
      await api.post(`/orders/invoice-series/${series.id}/deactivate`, { replacementSeriesId: replacementId || null })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['invoice-series'] })
      queryClient.invalidateQueries({ queryKey: ['channel-invoice-settings'] })
      onClose()
    },
    onError: (e: unknown) => setError(apiErrorMessage(e, 'Pasife alınamadı.')),
  })

  const needsReplacement = usedBy.length > 0

  return (
    <Modal open onClose={onClose} title={`${series.serial} serisini pasife al`}
      footer={<>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button variant="danger" onClick={() => deactivate.mutate()} loading={deactivate.isPending}
          disabled={needsReplacement && !replacementId}>Pasife Al</Button>
      </>}>
      <div className="space-y-3 text-sm" style={{ color: 'var(--text)' }}>
        <p>Pasif seri numara üretmez; geçmiş faturalar seriye bağlı kalır. Seri silinmez.</p>
        {needsReplacement ? (
          <>
            <div className="rounded-lg p-3 text-xs" style={{ background: '#fffbeb', color: '#92400e', border: '1px solid #fde68a' }}>
              Bu seri {usedBy.length} satış kanalının <b>{INVOICE_TYPE_MAP[series.invoiceType]}</b> yuvasında kullanılıyor:
              {' '}{usedBy.map(c => c.channelName || c.channelCode).join(', ')}.
              Kanal serisiz kalamaz; yerine geçecek seri seçin — bağlar tek işlemde taşınır.
            </div>
            <div>
              <label className="flbl">Yerine geçecek seri (aynı firma, aynı tip, aktif)</label>
              <select className="inp" value={replacementId} onChange={e => setReplacementId(e.target.value)}>
                <option value="">Seri seçin</option>
                {candidates.map(c => <option key={c.id} value={c.id}>{c.serial}{c.name ? ` · ${c.name}` : ''}</option>)}
              </select>
              {candidates.length === 0 && (
                <p className="text-xs mt-1 text-red-500">Bu firmada aynı tipte başka aktif seri yok — önce yeni seri tanımlayın.</p>
              )}
            </div>
          </>
        ) : (
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>Seriyi kullanan kanal yok; doğrudan pasife alınabilir.</p>
        )}
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
    </Modal>
  )
}

// ── Kanal yuvaları ────────────────────────────────────────────────────────────

function ChannelSlotsCard({ channels, series, firms }: { channels: ChannelSettings[]; series: InvoiceSeries[]; firms: FirmRow[] }) {
  const queryClient = useQueryClient()
  type Edit = { sendMethod: string; e_archive: string; e_invoice: string; export: string }
  const [edits, setEdits] = useState<Record<string, Edit>>({})
  const [savedId, setSavedId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const firmName = (id: string) => { const f = firms.find(x => x.id === id); return f ? trName(f.nameI18n, f.code) : '—' }
  const base = (c: ChannelSettings): Edit => ({
    sendMethod: c.sendMethod,
    e_archive: c.bindings.find(b => b.invoiceType === 'e_archive')?.seriesId ?? '',
    e_invoice: c.bindings.find(b => b.invoiceType === 'e_invoice')?.seriesId ?? '',
    export: c.bindings.find(b => b.invoiceType === 'export')?.seriesId ?? '',
  })
  const edit = (c: ChannelSettings) => edits[c.firmPlatformId] ?? base(c)
  const setEdit = (c: ChannelSettings, patch: Partial<Edit>) =>
    setEdits(prev => ({ ...prev, [c.firmPlatformId]: { ...(prev[c.firmPlatformId] ?? base(c)), ...patch } }))

  const save = useMutation({
    mutationFn: async (c: ChannelSettings) => {
      const e = edit(c)
      await api.put(`/orders/invoice-settings/channels/${c.firmPlatformId}`, {
        sendMethod: e.sendMethod,
        eArchiveSeriesId: e.e_archive || null,
        eInvoiceSeriesId: e.e_invoice || null,
        exportSeriesId: e.export || null,
      })
      return c.firmPlatformId
    },
    onSuccess: (id) => {
      queryClient.invalidateQueries({ queryKey: ['channel-invoice-settings'] })
      queryClient.invalidateQueries({ queryKey: ['invoice-series'] })
      setEdits(prev => { const p = { ...prev }; delete p[id]; return p })
      setError(null); setSavedId(id); setTimeout(() => setSavedId(null), 2000)
    },
    onError: (err: unknown) => setError(apiErrorMessage(err, 'Kaydedilemedi.')),
  })

  // Yuva seçici yalnız kanalın firmasındaki, aktif, AYNI TİPTEKİ serileri listeler (plan §2.3)
  const options = (c: ChannelSettings, type: string) =>
    series.filter(s => s.firmId === c.firmId && s.invoiceType === type && s.isActive)

  return (
    <div className="card overflow-hidden p-0">
      <div className="px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
        <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Kanal Yuvaları</h2>
        <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
          Her aktif satış kanalı üç tipte de seriye bağlı olmalı. Yuvaya yalnız kanalın firmasındaki aynı tipteki aktif seriler seçilebilir.
        </p>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KANAL', 'FİRMA', 'GÖNDERİM', 'E-ARŞİV', 'E-FATURA', 'İHRACAT', 'DURUM', ''].map(h => (
                <th key={h} className="px-3 py-2.5 text-left text-xs font-semibold tracking-wider" style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {channels.map(c => {
              const e = edit(c)
              const dirty = !!edits[c.firmPlatformId]
              return (
                <tr key={c.firmPlatformId} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-3 py-2.5 whitespace-nowrap">
                    <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{c.channelName || c.channelCode}</span>
                    <code className="text-xs ml-2" style={{ color: 'var(--text-s)' }}>{c.channelCode}</code>
                    {!c.channelActive && <Badge variant="neutral" className="ml-2">Pasif kanal</Badge>}
                  </td>
                  <td className="px-3 py-2.5 text-sm whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{firmName(c.firmId)}</td>
                  <td className="px-3 py-2.5">
                    <select className="inp" style={{ minWidth: 150 }} value={e.sendMethod} onChange={ev => setEdit(c, { sendMethod: ev.target.value })}>
                      {Object.entries(SEND_METHOD_MAP).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                    </select>
                  </td>
                  {TYPES.map(t => {
                    const opts = options(c, t)
                    return (
                      <td key={t} className="px-3 py-2.5">
                        <select className="inp" style={{ minWidth: 130 }} value={e[t]} onChange={ev => setEdit(c, { [t]: ev.target.value } as Partial<Edit>)}>
                          <option value="">— yok —</option>
                          {opts.map(s => <option key={s.id} value={s.id}>{s.serial}{s.name ? ` · ${s.name}` : ''}</option>)}
                        </select>
                        {opts.length === 0 && <p className="text-[11px] mt-0.5" style={{ color: 'var(--text-s)' }}>bu tipte seri yok</p>}
                      </td>
                    )
                  })}
                  <td className="px-3 py-2.5">
                    {c.warnings.length === 0
                      ? <Badge variant="success">Tam</Badge>
                      : <div className="flex flex-col gap-1">{c.warnings.map((w, i) => <Badge key={i} variant="danger">{w}</Badge>)}</div>}
                  </td>
                  <td className="px-3 py-2.5 text-right whitespace-nowrap">
                    {savedId === c.firmPlatformId && (
                      <span className="inline-flex items-center gap-1 text-xs mr-2" style={{ color: '#16a34a' }}><CheckCircle size={12} /> Kaydedildi</span>
                    )}
                    <Button size="sm" variant={dirty ? 'primary' : 'secondary'} disabled={!dirty}
                      loading={save.isPending && save.variables === c} onClick={() => save.mutate(c)}>Kaydet</Button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      {error && <p className="px-4 py-2 text-sm" style={{ color: '#ef4444' }}>{error}</p>}
    </div>
  )
}

// ── Sayfa ─────────────────────────────────────────────────────────────────────

export function InvoiceSeriesPage() {
  const queryClient = useQueryClient()
  const [firmFilter, setFirmFilter] = useState('')
  const [typeFilter, setTypeFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState<'active' | 'passive' | ''>('active')
  const [q, setQ] = useState('')
  const [newOpen, setNewOpen] = useState(false)
  const [editing, setEditing] = useState<InvoiceSeries | null>(null)
  const [deactivating, setDeactivating] = useState<InvoiceSeries | null>(null)
  const [error, setError] = useState('')

  const { data: firms = [] } = useQuery<FirmRow[]>({
    queryKey: ['firms-for-invoice-series'],
    queryFn: async () => (await api.get('/core/firms?activeOnly=false')).data.data ?? [],
  })
  const { data: series = [], isLoading } = useQuery<InvoiceSeries[]>({
    queryKey: ['invoice-series'],
    queryFn: async () => (await api.get('/orders/invoice-series?activeOnly=false')).data.data ?? [],
  })
  const { data: channels = [] } = useQuery<ChannelSettings[]>({
    queryKey: ['channel-invoice-settings'],
    queryFn: async () => (await api.get('/orders/invoice-settings/channels')).data.data ?? [],
  })

  const activate = useMutation({
    mutationFn: async (id: string) => { await api.post(`/orders/invoice-series/${id}/activate`, {}) },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['invoice-series'] }); setError('') },
    onError: (e: unknown) => setError(apiErrorMessage(e, 'Aktifleştirilemedi.')),
  })

  const firmName = (id: string) => { const f = firms.find(x => x.id === id); return f ? trName(f.nameI18n, f.code) : '—' }
  const usedBy = (s: InvoiceSeries) => channels.filter(c => c.bindings.some(b => b.seriesId === s.id))

  const rows = useMemo(() => series.filter(s =>
    (!firmFilter || s.firmId === firmFilter) &&
    (!typeFilter || s.invoiceType === typeFilter) &&
    (!statusFilter || (statusFilter === 'active' ? s.isActive : !s.isActive)) &&
    (!q || s.serial.includes(q.toUpperCase()) || (s.name ?? '').toLowerCase().includes(q.toLowerCase()))
  ), [series, firmFilter, typeFilter, statusFilter, q])

  const unboundActiveChannels = channels.filter(c => c.channelActive && c.missingTypes.length > 0).length

  if (isLoading) return <PageSpinner />

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Fatura Serileri</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {series.filter(s => s.isActive).length} aktif seri · {unboundActiveChannels > 0
              ? <span style={{ color: '#dc2626' }}>{unboundActiveChannels} aktif kanalda eksik yuva var</span>
              : 'tüm aktif kanallar üç tipte de bağlı'}
          </p>
        </div>
        <Button size="sm" onClick={() => setNewOpen(true)}><Plus size={14} /> Yeni Seri</Button>
      </div>

      <div className="card overflow-hidden p-0">
        <div className="flex flex-wrap gap-2 px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
          <select className="inp" style={{ width: 200 }} value={firmFilter} onChange={e => setFirmFilter(e.target.value)}>
            <option value="">Tüm firmalar</option>
            {firms.map(f => <option key={f.id} value={f.id}>{trName(f.nameI18n, f.code)}</option>)}
          </select>
          <select className="inp" style={{ width: 140 }} value={typeFilter} onChange={e => setTypeFilter(e.target.value)}>
            <option value="">Tüm tipler</option>
            {TYPES.map(t => <option key={t} value={t}>{INVOICE_TYPE_MAP[t]}</option>)}
          </select>
          <select className="inp" style={{ width: 120 }} value={statusFilter} onChange={e => setStatusFilter(e.target.value as 'active' | 'passive' | '')}>
            <option value="active">Aktif</option>
            <option value="passive">Pasif</option>
            <option value="">Tümü</option>
          </select>
          <input className="inp" style={{ width: 160 }} value={q} onChange={e => setQ(e.target.value)} placeholder="Seri / ad ara" />
        </div>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                {['FİRMA', 'SERİ', 'TİP', 'AD', 'KULLANAN KANALLAR', 'SON NUMARA', 'SON FATURA', 'DURUM', ''].map(h => (
                  <th key={h} className="px-3 py-2.5 text-left text-xs font-semibold tracking-wider" style={{ color: 'var(--text-s)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map(s => {
                const used = usedBy(s)
                return (
                  <tr key={s.id} className="cursor-pointer hover:bg-[var(--surface2)]" style={{ borderBottom: '1px solid var(--border)' }}
                    onClick={() => setEditing(s)}>
                    <td className="px-3 py-2.5 text-sm whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{firmName(s.firmId)}</td>
                    <td className="px-3 py-2.5 font-mono font-semibold" style={{ color: 'var(--text)' }}>{s.serial}</td>
                    <td className="px-3 py-2.5"><Badge variant="info">{INVOICE_TYPE_MAP[s.invoiceType] ?? s.invoiceType}</Badge></td>
                    <td className="px-3 py-2.5 text-sm" style={{ color: 'var(--text)' }}>
                      {s.name ?? '—'}
                      {s.description && <span className="block text-xs" style={{ color: 'var(--text-s)' }}>{s.description}</span>}
                    </td>
                    <td className="px-3 py-2.5 text-xs" style={{ color: 'var(--text-m)' }}>
                      {used.length === 0 ? <span style={{ color: 'var(--text-s)' }}>—</span>
                        : used.map(c => <code key={c.firmPlatformId} className="mr-1">{c.channelCode}</code>)}
                    </td>
                    <td className="px-3 py-2.5 font-mono text-xs tabular-nums" style={{ color: 'var(--text-m)' }}>
                      {s.lastYear ? `${s.serial}${s.lastYear}${String(s.lastSequence).padStart(9, '0')}` : '—'}
                    </td>
                    <td className="px-3 py-2.5 text-xs whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{fmtDate(s.lastInvoiceDate)}</td>
                    <td className="px-3 py-2.5">
                      {s.isActive ? <Badge variant="success">Aktif</Badge> : <Badge variant="neutral">Pasif</Badge>}
                    </td>
                    <td className="px-3 py-2.5 text-right whitespace-nowrap" onClick={e => e.stopPropagation()}>
                      {s.isActive
                        ? <Button size="sm" variant="ghost" onClick={() => setDeactivating(s)}>Pasife Al</Button>
                        : <Button size="sm" variant="ghost" onClick={() => activate.mutate(s.id)} loading={activate.isPending && activate.variables === s.id}>Aktifleştir</Button>}
                    </td>
                  </tr>
                )
              })}
              {rows.length === 0 && (
                <tr><td colSpan={9} className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Filtreye uyan seri yok.</td></tr>
              )}
            </tbody>
          </table>
        </div>
        {error && <p className="px-4 py-2 text-sm" style={{ color: '#ef4444' }}>{error}</p>}
        <p className="px-4 py-2 text-xs" style={{ borderTop: '1px solid var(--border)', color: 'var(--text-s)' }}>
          Bir seri = bir üç harfli ön ek = bir tip = bir numara akışı. Numaralar her yıl 1'den başlar, iptal edilen fatura numarayı tüketir. Seri silinmez, pasife alınır.
        </p>
      </div>

      <ChannelSlotsCard channels={channels} series={series} firms={firms} />

      {newOpen && <NewSeriesModal firms={firms} defaultFirmId={firmFilter} onClose={() => setNewOpen(false)} />}
      {editing && <EditSeriesModal series={editing} usedBy={usedBy(editing)} onClose={() => setEditing(null)} />}
      {deactivating && (
        <DeactivateModal series={deactivating} usedBy={usedBy(deactivating)}
          candidates={series.filter(c => c.id !== deactivating.id && c.firmId === deactivating.firmId && c.invoiceType === deactivating.invoiceType && c.isActive)}
          onClose={() => setDeactivating(null)} />
      )}
    </div>
  )
}
