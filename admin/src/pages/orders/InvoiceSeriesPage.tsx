import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { CheckCircle, Plus } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText as gridErrText } from '@/components/ui/DataTable.utils'
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
// FE3: firma bazlı e-fatura entegratör sözleşmesi (kimlik değerleri gelmez)
interface ContractRow { id: string; firmId: string; serviceCode: string; name: string | null; isActive: boolean; status: string; testMode: boolean; firmWide: boolean }
const contractLabel = (c: ContractRow) => `${c.name ?? c.serviceCode} (${c.serviceCode}${c.testMode ? ' · test' : ''})`

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

function NewSeriesModal({ firms, contracts, defaultFirmId, onClose }: { firms: FirmRow[]; contracts: ContractRow[]; defaultFirmId: string; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [firmId, setFirmId] = useState(defaultFirmId)
  const [serial, setSerial] = useState('')
  const [invoiceType, setInvoiceType] = useState<string>('e_archive')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [contractId, setContractId] = useState('')
  const [error, setError] = useState('')
  const firmContracts = contracts.filter(c => c.firmId === firmId && c.firmWide && c.isActive)

  const create = useMutation({
    mutationFn: async () => {
      await api.post('/orders/invoice-series', {
        firmId, serial, invoiceType, name: name || null, description: description || null, integrationContractId: contractId || null,
      })
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
        <Button onClick={() => create.mutate()} loading={create.isPending} disabled={!firmId || serial.length !== 3 || !contractId}>Seri Ekle</Button>
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
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Entegratör sözleşmesi <span className="text-red-500">*</span></label>
            <select className="inp" value={contractId} onChange={e => setContractId(e.target.value)} disabled={!firmId}>
              <option value="">Sözleşme seçin</option>
              {firmContracts.map(c => <option key={c.id} value={c.id}>{contractLabel(c)}</option>)}
            </select>
            {firmId && firmContracts.length === 0 && (
              <p className="text-xs mt-1 text-red-500">Bu firmada aktif e-fatura entegratör sözleşmesi yok — Ayarlar → Firmalar → firma → Entegrasyonlar'dan "einvoice" tipli servis ekleyin.</p>
            )}
          </div>
          <div>
            <label className="flbl">Açıklama</label>
            <input className="inp" value={description} onChange={e => setDescription(e.target.value)} placeholder="isteğe bağlı" />
          </div>
        </div>
        <p className="text-xs" style={{ color: 'var(--text-s)' }}>
          Her seri bir entegratör sözleşmesine aittir. Aynı harfler bir firmada tipten bağımsız yalnız bir kez tanımlanabilir; tip sonradan değiştirilemez.
          Numara örneği: <code>{serial || 'MSH'}{new Date().getFullYear()}000000001</code>
        </p>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
    </Modal>
  )
}

// ── Düzenle ───────────────────────────────────────────────────────────────────

interface GapRow { year: string; expectedLast: number; recordedCount: number; cancelledCount: number; missingSequences: number[]; missingTotal: number; lastInvoiceDate: string | null }

function EditSeriesModal({ series, usedBy, contracts, onClose }: { series: InvoiceSeries; usedBy: ChannelSettings[]; contracts: ContractRow[]; onClose: () => void }) {
  const queryClient = useQueryClient()
  // FE1: boşluk denetimi (sayaç ↔ kayıtlı numaralar)
  const { data: gaps = [] } = useQuery<GapRow[]>({
    queryKey: ['invoice-series-gaps', series.id],
    queryFn: async () => (await api.get(`/orders/invoice-series/${series.id}/gaps`)).data.data ?? [],
  })
  const [name, setName] = useState(series.name ?? '')
  const [description, setDescription] = useState(series.description ?? '')
  const [contractId, setContractId] = useState(series.integrationContractId ?? '')
  const [error, setError] = useState('')
  const firmContracts = contracts.filter(c => c.firmId === series.firmId && c.firmWide && (c.isActive || c.id === series.integrationContractId))

  const save = useMutation({
    mutationFn: async () => {
      await api.put(`/orders/invoice-series/${series.id}`, {
        name: name || null, description: description || null, integrationContractId: contractId || null,
      })
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['invoice-series'] }); onClose() },
    onError: (e: unknown) => setError(apiErrorMessage(e, 'Kaydedilemedi.')),
  })

  return (
    <Modal open onClose={onClose} title={`${series.serial} · ${INVOICE_TYPE_MAP[series.invoiceType] ?? series.invoiceType}`}
      footer={<>
        <Button variant="secondary" onClick={onClose}>Kapat</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!contractId}>Kaydet</Button>
      </>}>
      <div className="space-y-3">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Ad</label>
            <input className="inp" value={name} onChange={e => setName(e.target.value)} />
          </div>
          <div>
            <label className="flbl">Entegratör sözleşmesi <span className="text-red-500">*</span></label>
            <select className="inp" value={contractId} onChange={e => setContractId(e.target.value)}>
              <option value="">Sözleşme seçin</option>
              {firmContracts.map(c => <option key={c.id} value={c.id}>{contractLabel(c)}</option>)}
            </select>
            {firmContracts.length === 0 && <p className="text-xs mt-1 text-red-500">Bu firmada e-fatura sözleşmesi yok — Ayarlar → Firmalar → Entegrasyonlar.</p>}
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
        <div>
          <p className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>BOŞLUK DENETİMİ</p>
          {gaps.length === 0
            ? <p className="text-xs" style={{ color: 'var(--text-s)' }}>Bu seriden henüz numara üretilmedi.</p>
            : gaps.map(g => (
              <div key={g.year} className="flex flex-wrap items-center gap-2 text-xs py-1" style={{ borderTop: '1px solid var(--border)', color: 'var(--text-m)' }}>
                <b style={{ color: 'var(--text)' }}>{g.year}</b>
                <span>son sıra {g.expectedLast}</span>
                <span>· kayıtlı {g.recordedCount}</span>
                {g.cancelledCount > 0 && <span>· iptal {g.cancelledCount}</span>}
                {g.missingTotal === 0
                  ? <Badge variant="success">boşluk yok</Badge>
                  : <Badge variant="danger">{g.missingTotal} eksik: {g.missingSequences.slice(0, 20).join(', ')}{g.missingTotal > 20 ? '…' : ''}</Badge>}
              </div>
            ))}
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
  // DataGrid (2026-09-09, tur 11): sunucu filtre/sıralama/arama (InvoiceSeriesGrid.Schema) + Excel + görünümler.
  // ★ Ayrı uç: /orders/invoice-series TAM liste döner ve AŞAĞIDA hâlâ gerekiyor — kanal yuvası seçicileri,
  // "pasife alırken yerine geçecek seri" adayları ve kullanan-kanal kodları tam listeden çizilir;
  // yalnız SATIRLAR sayfalı /orders/invoice-series/grid ucundan gelir.
  const [sp] = useSearchParams()
  const firmFilter = sp.get('firmId') ?? ''
  const typeFilter = sp.get('invoiceType') ?? ''
  const statusFilter = sp.get('durum') ?? 'active'
  const grid = useGridState('invoice-series', { defaultPageSize: 50, defaultSort: 'invoiceType', defaultDir: 'asc' })
  const setNamed = (k: string, v: string) => grid.mutate(n => { if (v) n.set(k, v); else n.delete(k) })
  const named = () => ({
    firmId: firmFilter || undefined,
    invoiceType: typeFilter || undefined,
    durum: statusFilter === 'active' ? undefined : statusFilter,   // sunucu varsayılanı 'active'
  })
  const [newOpen, setNewOpen] = useState(false)
  const [editing, setEditing] = useState<InvoiceSeries | null>(null)
  const [deactivating, setDeactivating] = useState<InvoiceSeries | null>(null)
  const [error, setError] = useState('')

  const { data: firms = [] } = useQuery<FirmRow[]>({
    queryKey: ['firms-for-invoice-series'],
    queryFn: async () => (await api.get('/core/firms?activeOnly=false')).data.data ?? [],
  })
  // Tam liste: seçiciler + kullanan-kanal kodları + pasife alma adayları (sayfalanmaz).
  const { data: series = [] } = useQuery<InvoiceSeries[]>({
    queryKey: ['invoice-series'],
    queryFn: async () => (await api.get('/orders/invoice-series?activeOnly=false')).data.data ?? [],
  })
  const { data, isLoading, isFetching, error: listError } = useQuery<{ items: InvoiceSeries[]; totalCount: number }>({
    queryKey: ['invoice-series-grid', firmFilter, typeFilter, statusFilter, ...grid.queryKey],
    queryFn: async () => (await api.get(`/orders/invoice-series/grid?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const { data: contracts = [] } = useQuery<ContractRow[]>({
    queryKey: ['einvoice-contracts'],
    queryFn: async () => (await api.get('/orders/invoice-series/contracts')).data.data ?? [],
  })
  const { data: channels = [] } = useQuery<ChannelSettings[]>({
    queryKey: ['channel-invoice-settings'],
    queryFn: async () => (await api.get('/orders/invoice-settings/channels')).data.data ?? [],
  })

  const activate = useMutation({
    mutationFn: async (id: string) => { await api.post(`/orders/invoice-series/${id}/activate`, {}) },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['invoice-series'] })        // tam liste (seçiciler)
      queryClient.invalidateQueries({ queryKey: ['invoice-series-grid'] })   // bu ekranın sayfalı listesi
      setError('')
    },
    onError: (e: unknown) => setError(apiErrorMessage(e, 'Aktifleştirilemedi.')),
  })

  const firmName = (id: string) => { const f = firms.find(x => x.id === id); return f ? trName(f.nameI18n, f.code) : '—' }
  const usedBy = (s: InvoiceSeries) => channels.filter(c => c.bindings.some(b => b.seriesId === s.id))

  const rows = data?.items ?? []

  const unboundActiveChannels = channels.filter(c => c.channelActive && c.missingTypes.length > 0).length

  const columns: GridColumn<InvoiceSeries>[] = [
    { key: 'serial', header: 'SERİ', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 110,
      filter: { type: 'text', label: 'Seri', ops: ['startswith', 'contains', 'eq'] },
      cell: s => <span className="font-mono font-semibold" style={{ color: 'var(--text)' }}>{s.serial}</span> },
    { key: 'firma', header: 'FİRMA', priority: 1, minWidth: 160,
      // Firma adı Core modülünde çözülür → sıralanamaz; süzgeç üstteki firma seçicisidir (firmId).
      cell: s => <span className="text-sm whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{firmName(s.firmId)}</span> },
    { key: 'invoiceType', header: 'TİP', priority: 1, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Tip',
        options: TYPES.map(t => ({ value: t, label: INVOICE_TYPE_MAP[t] ?? t })) },
      cell: s => <Badge variant="info">{INVOICE_TYPE_MAP[s.invoiceType] ?? s.invoiceType}</Badge> },
    { key: 'ad', header: 'AD', priority: 2, sortable: true, minWidth: 200,
      filter: { type: 'text', label: 'Ad' },
      filters: [{ field: 'aciklama', label: 'Açıklama', type: 'text' }],
      cell: s => <div>
        <span className="text-sm" style={{ color: 'var(--text)' }}>{s.name ?? '—'}</span>
        {s.description && <span className="block text-xs" style={{ color: 'var(--text-s)' }}>{s.description}</span>}
      </div> },
    { key: 'sozlesme', header: 'SÖZLEŞME', priority: 2, minWidth: 150,
      // Sözleşme adı firma entegrasyonlarından gelir → sıralanamaz; süzgeç "sözleşmesi var mı" bayrağı.
      filter: { type: 'boolean', label: 'Sözleşmesi var', field: 'sozlesmeVar' },
      cell: s => <span className="text-xs" style={{ color: 'var(--text-m)' }}>
        {s.integrationContractId ? (s.integrationContractName ?? 'bağlı') : <Badge variant="danger">sözleşmesiz</Badge>}
      </span> },
    { key: 'kanalSayisi', header: 'KULLANAN KANALLAR', priority: 2, sortable: true, minWidth: 170,
      filter: { type: 'number', label: 'Bağlı kanal sayısı' },
      filters: [{ field: 'kanalaBagli', label: 'Bir kanala bağlı', type: 'boolean' }],
      cell: s => {
        const used = usedBy(s)
        return <span className="text-xs" style={{ color: 'var(--text-m)' }}>
          {used.length === 0
            ? <span style={{ color: 'var(--text-s)' }}>{s.channelCount > 0 ? `${s.channelCount} kanal` : '—'}</span>
            : used.map(c => <code key={c.firmPlatformId} className="mr-1">{c.channelCode}</code>)}
        </span>
      } },
    { key: 'sonSira', header: 'SON NUMARA', priority: 3, sortable: true, minWidth: 150,
      filter: { type: 'number', label: 'Son sıra' },
      filters: [{ field: 'kullanilmis', label: 'Numara tüketmiş', type: 'boolean' }],
      cell: s => <span className="font-mono text-xs tabular-nums" style={{ color: 'var(--text-m)' }}>
        {s.lastYear ? `${s.serial}${s.lastYear}${String(s.lastSequence).padStart(9, '0')}` : '—'}</span> },
    { key: 'sonFatura', header: 'SON FATURA', priority: 3, sortable: true,
      filter: { type: 'date', label: 'Son fatura tarihi' },
      cell: s => <span className="text-xs whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{fmtDate(s.lastInvoiceDate)}</span> },
    { key: 'aktif', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      cell: s => s.isActive ? <Badge variant="success">Aktif</Badge> : <Badge variant="neutral">Pasif</Badge> },
    { key: 'islem', header: '', priority: 2, align: 'right', exportable: false, stopRowClick: true, minWidth: 120,
      cell: s => s.isActive
        ? <Button size="sm" variant="ghost" onClick={() => setDeactivating(s)}>Pasife Al</Button>
        : <Button size="sm" variant="ghost" onClick={() => activate.mutate(s.id)}
            loading={activate.isPending && activate.variables === s.id}>Aktifleştir</Button> },
  ]

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

      <DataGrid<InvoiceSeries>
        gridId="invoice-series"
        views
        grid={grid}
        columns={columns}
        rows={rows}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? gridErrText(listError) : (error || null)}
        onRowClick={s => setEditing(s)}
        empty="Ölçütlere uyan seri yok."
        search={{ placeholder: 'Seri, ad veya açıklama ara…' }}
        minWidth={1280}
        pageSizes={[50, 100, 200]}
        filterLeading={
          <>
            <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={firmFilter} aria-label="Firma"
              onChange={e => setNamed('firmId', e.target.value)}>
              <option value="">Tüm firmalar</option>
              {firms.map(f => <option key={f.id} value={f.id}>{trName(f.nameI18n, f.code)}</option>)}
            </select>
            <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={typeFilter} aria-label="Tip"
              onChange={e => setNamed('invoiceType', e.target.value)}>
              <option value="">Tüm tipler</option>
              {TYPES.map(t => <option key={t} value={t}>{INVOICE_TYPE_MAP[t]}</option>)}
            </select>
            <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={statusFilter} aria-label="Durum"
              onChange={e => setNamed('durum', e.target.value)}>
              <option value="active">Aktif</option>
              <option value="passive">Pasif</option>
              <option value="">Tümü</option>
            </select>
          </>
        }
        export={{ endpoint: '/orders/invoice-series/export', named, fallbackFileName: 'fatura-serileri.xlsx' }}
        compact={{
          title: s => s.serial,
          subtitle: s => `${INVOICE_TYPE_MAP[s.invoiceType] ?? s.invoiceType}${s.name ? ` · ${s.name}` : ''}`,
          right: s => firmName(s.firmId),
          badge: s => s.isActive ? <Badge variant="success">Aktif</Badge> : <Badge variant="neutral">Pasif</Badge>,
        }}
      />

      <p className="text-xs" style={{ color: 'var(--text-s)' }}>
        Bir seri = bir üç harfli ön ek = bir tip = bir numara akışı. Numaralar her yıl 1'den başlar,
        iptal edilen fatura numarayı tüketir. Seri silinmez, pasife alınır.
      </p>

      <ChannelSlotsCard channels={channels} series={series} firms={firms} />

      {newOpen && <NewSeriesModal firms={firms} contracts={contracts} defaultFirmId={firmFilter} onClose={() => setNewOpen(false)} />}
      {editing && <EditSeriesModal series={editing} usedBy={usedBy(editing)} contracts={contracts} onClose={() => setEditing(null)} />}
      {deactivating && (
        <DeactivateModal series={deactivating} usedBy={usedBy(deactivating)}
          candidates={series.filter(c => c.id !== deactivating.id && c.firmId === deactivating.firmId && c.invoiceType === deactivating.invoiceType && c.isActive)}
          onClose={() => setDeactivating(null)} />
      )}
    </div>
  )
}
