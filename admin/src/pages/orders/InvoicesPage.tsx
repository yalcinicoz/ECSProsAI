import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { cn } from '@/lib/utils'
import { INVOICE_STATUS_MAP, INVOICE_TYPE_MAP, INVOICE_SOURCE_MAP } from './orderConstants'

const TABS = [
  { key: 'created',   label: 'Oluşturulan' },
  { key: 'cancelled', label: 'İptal Edilen' },
  { key: '',          label: 'Tümü' },
  { key: 'queue',     label: 'Gönderim Kuyruğu' },
]

// FE4: gönderim kuyruğu
interface DispatchRow {
  id: string; invoiceId: string; invoiceNumber: string; invoiceType: string; orderId: string
  action: string; status: string; providerCode: string | null; attempt: number; maxAttempts: number
  nextAttemptAt: string | null; lastAttemptAt: string | null; completedAt: string | null; lastError: string | null
  responseSnapshot: Record<string, unknown> | null; createdAt: string
}
const DISPATCH_STATUS: Record<string, { label: string; variant: 'default' | 'success' | 'warning' | 'danger' | 'info' | 'neutral' }> = {
  pending: { label: 'Bekliyor', variant: 'info' },
  done:    { label: 'Tamamlandı', variant: 'success' },
  dead:    { label: 'Ölü mektup', variant: 'danger' },
  blocked: { label: 'Engelli', variant: 'warning' },
}
const DISPATCH_ACTION: Record<string, string> = { send: 'Gönder', cancel: 'İptal bildir', status: 'Durum sor' }
const INTEGRATOR_STATUS: Record<string, string> = {
  not_applicable: 'Gönderim yok', pending: 'Bekliyor', queued: 'Kuyrukta', retrying: 'Tekrar denenecek',
  sent: 'Gönderildi', accepted: 'Kabul edildi', rejected: 'Reddedildi', error: 'Hata', blocked: 'Engelli',
  cancel_queued: 'İptal kuyrukta', cancelled: 'İptal bildirildi',
}
const fmtDt = (iso: string | null | undefined) => iso ? new Date(iso).toLocaleString('tr-TR') : '—'

function DispatchQueue() {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState('pending')
  const [error, setError] = useState('')
  const { data, isLoading } = useQuery<PagedResult<DispatchRow>>({
    queryKey: ['invoice-dispatches', status],
    queryFn: async () => (await api.get(`/orders/invoice-dispatches?status=${status}&pageSize=100`)).data.data,
    refetchInterval: 30_000,
  })
  const retry = useMutation({
    mutationFn: async (id: string) => { await api.post(`/orders/invoice-dispatches/${id}/retry`, {}) },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['invoice-dispatches'] }); setError('') },
    onError: (e: unknown) => setError((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Tekrar denenemedi.'),
  })
  const rows = data?.items ?? []
  return (
    <div className="card overflow-hidden p-0">
      <div className="flex flex-wrap items-center gap-2 px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
        {Object.entries(DISPATCH_STATUS).map(([k, v]) => (
          <button key={k} className={cn('stab', status === k && 'active')} onClick={() => setStatus(k)}>{v.label}</button>
        ))}
        <button className={cn('stab', status === '' && 'active')} onClick={() => setStatus('')}>Tümü</button>
        <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>{data?.totalCount ?? 0} iş · worker kapalıysa kuyruk birikir</span>
      </div>
      <table className="w-full">
        <thead>
          <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
            {['FATURA', 'İŞLEM', 'SAĞLAYICI', 'DENEME', 'SONRAKİ', 'DURUM', 'SON HATA', ''].map(h => (
              <th key={h} className="px-3 py-2.5 text-left text-xs font-semibold tracking-wider" style={{ color: 'var(--text-s)' }}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map(d => {
            const st = DISPATCH_STATUS[d.status] ?? { label: d.status, variant: 'neutral' as const }
            return (
              <tr key={d.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-3 py-2.5">
                  <Link to={`/orders/${d.orderId}`} className="text-xs font-mono underline" style={{ color: 'var(--brand)' }}>{d.invoiceNumber}</Link>
                  <span className="text-xs ml-2" style={{ color: 'var(--text-s)' }}>{INVOICE_TYPE_MAP[d.invoiceType] ?? d.invoiceType}</span>
                </td>
                <td className="px-3 py-2.5 text-xs" style={{ color: 'var(--text)' }}>{DISPATCH_ACTION[d.action] ?? d.action}</td>
                <td className="px-3 py-2.5 text-xs" style={{ color: 'var(--text-m)' }}>{d.providerCode ?? '—'}</td>
                <td className="px-3 py-2.5 text-xs tabular-nums" style={{ color: 'var(--text-m)' }}>{d.attempt}/{d.maxAttempts}</td>
                <td className="px-3 py-2.5 text-xs" style={{ color: 'var(--text-m)' }}>{d.status === 'pending' ? fmtDt(d.nextAttemptAt) : fmtDt(d.completedAt ?? d.lastAttemptAt)}</td>
                <td className="px-3 py-2.5"><Badge variant={st.variant}>{st.label}</Badge></td>
                <td className="px-3 py-2.5 text-xs max-w-md" style={{ color: '#b91c1c' }}>{d.lastError ?? ''}</td>
                <td className="px-3 py-2.5 text-right whitespace-nowrap">
                  {(d.status === 'dead' || d.status === 'blocked') && (
                    <Button size="sm" variant="secondary" onClick={() => retry.mutate(d.id)} loading={retry.isPending && retry.variables === d.id}>Tekrar Dene</Button>
                  )}
                </td>
              </tr>
            )
          })}
          {!isLoading && rows.length === 0 && (
            <tr><td colSpan={8} className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Bu durumda iş yok.</td></tr>
          )}
        </tbody>
      </table>
      {error && <p className="px-4 py-2 text-sm" style={{ color: '#ef4444' }}>{error}</p>}
    </div>
  )
}

export interface InvoiceSummary {
  id: string
  orderId: string
  invoiceNumber: string
  invoiceType: string
  invoiceDate: string
  recipientName: string
  grandTotal: number
  status: string
  integratorStatus: string
  createdAt: string
  hasIntegratorPdf?: boolean
  numberSource?: string
  externalSource?: string | null
}

// FE0 (2026-09-06): seri tekil ve TİPLİ — bkz. docs/fatura-entegrasyon-plani.md §2.2
export interface InvoiceSeries {
  id: string
  firmId: string
  serial: string
  invoiceType: 'e_archive' | 'e_invoice' | 'export' | string
  name?: string | null
  description?: string | null
  integrationContractId?: string | null
  integrationContractName?: string | null
  isActive: boolean
  retiredAt?: string | null
  channelCount: number
  lastYear?: string | null
  lastSequence: number
  lastInvoiceDate?: string | null
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

// ── Fatura detay modalı (liste verisinden; PDF URL girişi + iptal) ────────────
function InvoiceModal({ invoice, onClose }: { invoice: InvoiceSummary; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [pdfUrl, setPdfUrl] = useState('')
  const [error, setError] = useState('')

  function invalidate() {
    queryClient.invalidateQueries({ queryKey: ['invoices'] })
    queryClient.invalidateQueries({ queryKey: ['order-invoices', invoice.orderId] })
  }
  function onErr(e: unknown) {
    const err = e as { response?: { data?: { error?: string } } }
    setError(err.response?.data?.error ?? 'İşlem başarısız oldu.')
  }

  const saveUrl = useMutation({
    mutationFn: async () => {
      await api.patch(`/orders/invoices/${invoice.id}/integrator-url`, { integratorInvoiceUrl: pdfUrl.trim() || null })
    },
    onSuccess: () => { invalidate(); onClose() },
    onError: onErr,
  })
  const { data: dispatches } = useQuery<PagedResult<DispatchRow>>({
    queryKey: ['invoice-dispatches', 'inv', invoice.id],
    queryFn: async () => (await api.get(`/orders/invoice-dispatches?invoiceId=${invoice.id}&pageSize=20`)).data.data,
  })
  const [resendError, setResendError] = useState('')
  const resend = useMutation({
    mutationFn: async () => { await api.post(`/orders/invoices/${invoice.id}/resend`, {}) },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['invoice-dispatches'] }); queryClient.invalidateQueries({ queryKey: ['invoices'] }); setResendError('') },
    onError: (e: unknown) => setResendError((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Gönderilemedi.'),
  })
  const cancel = useMutation({
    mutationFn: async () => { await api.post(`/orders/invoices/${invoice.id}/cancel`, {}) },
    onSuccess: () => { invalidate(); onClose() },
    onError: onErr,
  })

  const st = INVOICE_STATUS_MAP[invoice.status] ?? { label: invoice.status, variant: 'neutral' as const }

  return (
    <Modal open onClose={onClose} title={`Fatura ${invoice.invoiceNumber}`}>
      <div className="space-y-1 text-sm">
        <div className="flex items-center gap-2 mb-2">
          <Badge variant={st.variant}>{st.label}</Badge>
          <span className="text-xs" style={{ color: 'var(--text-s)' }}>
            {INVOICE_TYPE_MAP[invoice.invoiceType] ?? invoice.invoiceType} · {new Date(invoice.invoiceDate).toLocaleDateString('tr-TR')}
          </span>
        </div>
        <p style={{ color: 'var(--text)' }}>Alıcı: {invoice.recipientName}</p>
        <p style={{ color: 'var(--text)' }}>
          Tutar: <b>{invoice.grandTotal.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺</b>
        </p>
        <p style={{ color: 'var(--text-m)' }}>
          Sipariş: <Link to={`/orders/${invoice.orderId}`} className="underline" style={{ color: 'var(--brand)' }} onClick={onClose}>görüntüle</Link>
        </p>
        <p className="text-xs" style={{ color: 'var(--text-s)' }}>
          Entegratör PDF: {invoice.hasIntegratorPdf ? 'kayıtlı ✓ (müşteri "Faturayı Görüntüle" butonunu görür)' : 'kayıtlı değil'}
        </p>
      </div>

      <div className="mt-4 pt-3" style={{ borderTop: '1px solid var(--border)' }}>
        <div className="rounded-lg p-3 mb-3 text-xs" style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>
          <div className="flex items-center gap-2 mb-1">
            <span className="font-semibold" style={{ color: 'var(--text)' }}>Entegratör durumu:</span>
            <Badge variant="neutral">{INTEGRATOR_STATUS[invoice.integratorStatus] ?? invoice.integratorStatus}</Badge>
            {invoice.status !== 'cancelled' && (invoice.numberSource ?? 'internal') === 'internal' && !['sent', 'accepted', 'queued', 'pending', 'retrying'].includes(invoice.integratorStatus) && (
              <Button size="sm" variant="ghost" onClick={() => resend.mutate()} loading={resend.isPending}>Entegratöre Gönder</Button>
            )}
          </div>
          {(dispatches?.items ?? []).map(d => {
            const st = DISPATCH_STATUS[d.status] ?? { label: d.status, variant: 'neutral' as const }
            return (
              <div key={d.id} className="flex flex-wrap items-center gap-2 py-1" style={{ borderTop: '1px solid var(--border)' }}>
                <span>{fmtDt(d.createdAt)}</span>
                <span>{DISPATCH_ACTION[d.action] ?? d.action}</span>
                <Badge variant={st.variant}>{st.label}</Badge>
                <span>{d.attempt}/{d.maxAttempts}</span>
                {d.lastError && <span style={{ color: '#b91c1c' }}>{d.lastError}</span>}
              </div>
            )
          })}
          {(dispatches?.items ?? []).length === 0 && <div>Gönderim işi yok.</div>}
          {resendError && <div style={{ color: '#ef4444' }}>{resendError}</div>}
        </div>
        <label className="flbl">Entegratör PDF Adresi (https)</label>
        <input className="inp" value={pdfUrl} onChange={e => setPdfUrl(e.target.value)}
          placeholder="https://.../earchive/....pdf" />
        <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
          Adres müşteriye inmez; site sunucusu üzerinden (proxy) görüntülenir. Boş kaydetmek mevcut adresi siler.
        </p>
      </div>
      {error && <p className="text-sm mt-2 text-red-500">{error}</p>}
      <div className="flex justify-between gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        {invoice.status === 'created' ? (
          <Button size="sm" variant="danger" onClick={() => cancel.mutate()} loading={cancel.isPending}>Faturayı İptal Et</Button>
        ) : <span />}
        <div className="flex gap-2">
          <Button variant="secondary" onClick={onClose}>Kapat</Button>
          <Button onClick={() => saveUrl.mutate()} loading={saveUrl.isPending}>PDF Adresini Kaydet</Button>
        </div>
      </div>
    </Modal>
  )
}

// ── Fatura listesi ────────────────────────────────────────────────────────────
export function InvoicesPage() {
  const [tab, setTab] = useState('created')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<InvoiceSummary | null>(null)

  const { data, isLoading } = useQuery<PagedResult<InvoiceSummary>>({
    queryKey: ['invoices', tab, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (tab) params.set('status', tab)
      return (await api.get(`/orders/invoices?${params}`)).data.data
    },
    enabled: tab !== 'queue',
  })

  const invoices = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const totalPages = Math.ceil(totalCount / 20)

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Faturalar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{totalCount} kayıt</p>
        </div>
        <Link to="/orders/invoice-series"><Button size="sm" variant="secondary">Fatura Serileri</Button></Link>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => (
          <button key={t.key} className={cn('stab', tab === t.key && 'active')}
            onClick={() => { setTab(t.key); setPage(1) }}>{t.label}</button>
        ))}
      </div>

      {tab === 'queue' ? <DispatchQueue /> : (<>
      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['FATURA NO', 'TİP', 'ALICI', 'TUTAR', 'PDF', 'DURUM', 'TARİH', ''].map(h => (
                <th key={h} className={`px-4 py-3 text-xs font-semibold ${h === '' ? 'w-20' : 'text-left'}`}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={8} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && invoices.length === 0 && (
              <tr><td colSpan={8} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Fatura bulunamadı. Fatura, sipariş detayındaki "Fatura Oluştur" ile kesilir.
              </td></tr>
            )}
            {invoices.map(inv => {
              const st = INVOICE_STATUS_MAP[inv.status] ?? { label: inv.status, variant: 'neutral' as const }
              return (
                <tr key={inv.id} onClick={() => setSelected(inv)}
                  className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                  style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-3">
                    <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{inv.invoiceNumber}{inv.numberSource && inv.numberSource !== 'internal' && <Badge variant="neutral" className="ml-2">{INVOICE_SOURCE_MAP[inv.numberSource] ?? inv.numberSource}</Badge>}</code>
                  </td>
                  <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>
                    {INVOICE_TYPE_MAP[inv.invoiceType] ?? inv.invoiceType}
                  </td>
                  <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{inv.recipientName}</td>
                  <td className="px-4 py-3 text-sm font-medium" style={{ color: 'var(--text)' }}>
                    {inv.grandTotal.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺
                  </td>
                  <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                    {inv.hasIntegratorPdf ? '✓' : '—'}
                  </td>
                  <td className="px-4 py-3"><Badge variant={st.variant}>{st.label}</Badge></td>
                  <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                    {new Date(inv.invoiceDate).toLocaleDateString('tr-TR')}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 mt-4">
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>← Önceki</button>
          <span className="text-sm" style={{ color: 'var(--text-s)' }}>{page} / {totalPages}</span>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Sonraki →</button>
        </div>
      )}
      </>)}

      {selected && <InvoiceModal invoice={selected} onClose={() => setSelected(null)} />}
    </div>
  )
}
