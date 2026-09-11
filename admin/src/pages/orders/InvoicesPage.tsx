import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { cn } from '@/lib/utils'
import { DataGrid, useGridState, type GridColumn, type GridFilterField } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { INVOICE_STATUS_MAP, INVOICE_TYPE_MAP, INVOICE_SOURCE_MAP } from './orderConstants'

// 2026-09-11 (kullanıcı isteği): satır tıklama popup'ı KALDIRILDI; liste kolonları sipariş no / fatura tarihi / oluşturma /
// ETTN / VKN-TCKN / tip / para birimi / toplam / ödenecek / vergi matrahı / vergi toplamı / entegratör-ERP-pazaryeri gönderimi;
// en sağda sabit "Görüntüle" (PDF) + "URL" (kopyalanabilir küçük popup). Fatura iptali sipariş detayına taşındı.

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
  orderNumber?: string
  ettn?: string | null
  recipientTaxNumber?: string | null
  currencyCode?: string
  subtotal?: number
  totalDiscount?: number
  totalTax?: number
  integratorSentAt?: string | null
  erpStatus?: string
  erpSentAt?: string | null
  erpReference?: string | null
  externalDocumentId?: string | null
  sendMethod?: string | null
  integratorInvoiceUrl?: string | null
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

const ERP_STATUS: Record<string, string> = {
  not_applicable: 'Gönderim yok', '': 'Gönderim yok', pending: 'Bekliyor', sent: 'Gönderildi', acknowledged: 'ERP kesti', error: 'Hata',
}
const fmtD = (iso: string | null | undefined) => iso ? new Date(iso).toLocaleDateString('tr-TR') : '—'
const para = (n: number | undefined, cur?: string) =>
  (n ?? 0).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ' + (cur === 'TRY' || !cur ? '₺' : cur)

// Panel bearer'lı: PDF'i blob olarak alıp yeni sekmede aç (window.open doğrudan token taşımaz).
async function pdfAc(inv: InvoiceSummary, onErr: (m: string) => void) {
  if (!inv.hasIntegratorPdf) { window.open(`/yazdir/fatura/${inv.id}`, '_blank'); return }
  try {
    const res = await api.get(`/orders/invoices/${inv.id}/pdf`, { responseType: 'blob' })
    const url = URL.createObjectURL(new Blob([res.data], { type: 'application/pdf' }))
    window.open(url, '_blank')
    setTimeout(() => URL.revokeObjectURL(url), 60_000)
  } catch (e) {
    onErr(errText(e))
  }
}

// ── "URL göster" popup'ı: fatura adresleri + kopyala ────────────────────────
function UrlPopup({ inv, onClose }: { inv: InvoiceSummary; onClose: () => void }) {
  const [kopyalandi, setKopyalandi] = useState('')
  const origin = window.location.origin
  const satirlar = [
    inv.integratorInvoiceUrl ? { ad: 'Entegratör PDF adresi', url: inv.integratorInvoiceUrl } : null,
    inv.hasIntegratorPdf ? { ad: 'Panel PDF (proxy, yetkili)', url: `${origin}/api/orders/invoices/${inv.id}/pdf` } : null,
    inv.status !== 'cancelled' ? { ad: 'Yazdırma sayfası', url: `${origin}/yazdir/fatura/${inv.id}` } : null,
  ].filter((x): x is { ad: string; url: string } => !!x)
  const kopyala = async (url: string) => {
    try { await navigator.clipboard.writeText(url); setKopyalandi(url); setTimeout(() => setKopyalandi(''), 1500) }
    catch { window.prompt('Kopyalamak için seçin:', url) }
  }
  return (
    <Modal open onClose={onClose} title={`Fatura ${inv.invoiceNumber} — adresler`} size="md">
      <div className="space-y-3">
        {satirlar.map(r => (
          <div key={r.url}>
            <div className="text-xs font-semibold mb-1" style={{ color: 'var(--text-s)' }}>{r.ad}</div>
            <div className="flex items-center gap-2">
              <input className="inp font-mono text-xs flex-1" readOnly value={r.url} onFocus={e => e.currentTarget.select()} />
              <Button size="sm" variant="secondary" onClick={() => kopyala(r.url)}>{kopyalandi === r.url ? 'Kopyalandı ✓' : 'Kopyala'}</Button>
            </div>
          </div>
        ))}
        {satirlar.length === 0 && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Bu fatura için adres yok.</p>}
        {!inv.hasIntegratorPdf && inv.status !== 'cancelled' && (
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>Entegratör PDF'i henüz kayıtlı değil; "Görüntüle" yazdırma sayfasını açar.</p>
        )}
      </div>
      <div className="flex justify-end mt-4 pt-3" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" size="sm" onClick={onClose}>Kapat</Button>
      </div>
    </Modal>
  )
}

// ── Fatura listesi — DataGrid F4 (docs/datagrid-standardi-plani.md): sekme ?tab= (created varsayılan), filtre/arama/sıralama URL'de ──
const INVOICE_EXTRA_FILTERS: GridFilterField[] = [
  { key: 'invoiceType', label: 'Tip', type: 'enum', multiple: true, quick: true, options: Object.entries(INVOICE_TYPE_MAP).map(([value, label]) => ({ value, label })) },
]

export function InvoicesPage() {
  const grid = useGridState('invoices', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })
  const [sp] = useSearchParams()
  const tab = sp.get('tab') ?? 'created'          // created | cancelled | '' (all) | queue
  const [urlInv, setUrlInv] = useState<InvoiceSummary | null>(null)
  const [pdfErr, setPdfErr] = useState('')

  const { data, isLoading, isFetching, error } = useQuery<PagedResult<InvoiceSummary>>({
    queryKey: ['invoices', tab, ...grid.queryKey],
    queryFn: async () => (await api.get(`/orders/invoices?${grid.toParams({ status: tab && tab !== 'all' ? tab : undefined })}`)).data.data,
    enabled: tab !== 'queue',
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const invoices = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const switchTab = (key: string) => grid.mutate(n => { if (key === 'created') n.delete('tab'); else n.set('tab', key) })
  const xs = { color: 'var(--text-s)' } as const

  const columns: GridColumn<InvoiceSummary>[] = [
    { key: 'invoiceNumber', header: 'FATURA NO', filters: [{ field: 'externalDocumentId', label: 'Dış belge no', type: 'text' }, { field: 'numberSource', label: 'Numara kaynağı', type: 'enum', multiple: true, options: Object.entries(INVOICE_SOURCE_MAP).map(([value, label]) => ({ value, label })) }], frozen: true, lockVisible: true, sortable: true, minWidth: 150,
      cell: inv => <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{inv.invoiceNumber}{inv.numberSource && inv.numberSource !== 'internal' && <Badge variant="neutral" className="ml-2">{INVOICE_SOURCE_MAP[inv.numberSource] ?? inv.numberSource}</Badge>}</code> },
    { key: 'orderNumber', header: 'SİPARİŞ NO', sortable: true, priority: 1, filter: { type: 'text', label: 'Sipariş no' }, stopRowClick: true,
      cell: inv => <Link to={`/orders/${inv.orderId}`} className="text-xs font-mono underline" style={{ color: 'var(--brand)' }}>{inv.orderNumber || '—'}</Link> },
    { key: 'invoiceDate', header: 'FATURA TARİHİ', sortable: true, priority: 1, filter: { type: 'date', label: 'Fatura tarihi', quick: true },
      cell: inv => <span className="text-xs" style={xs}>{fmtD(inv.invoiceDate)}</span> },
    { key: 'createdAt', header: 'OLUŞTURMA', sortable: true, priority: 3, filter: { type: 'date', label: 'Kayıt tarihi' },
      cell: inv => <span className="text-xs" style={xs}>{fmtDt(inv.createdAt)}</span> },
    { key: 'ettn', header: 'ETTN', priority: 3, minWidth: 200,
      cell: inv => inv.ettn ? <code className="text-xs font-mono" style={{ color: 'var(--text-m)' }}>{inv.ettn}</code> : <span className="text-xs" style={xs}>—</span> },
    { key: 'taxNumber', header: 'VKN / TCKN', priority: 2, sortable: false, filter: { type: 'text', label: 'Vergi no / TCKN', ops: ['contains', 'startswith'] },
      cell: inv => <span className="text-xs font-mono" style={{ color: 'var(--text-m)' }}>{inv.recipientTaxNumber || '—'}</span> },
    { key: 'recipient', header: 'ALICI', sortable: true, priority: 2, defaultVisible: false, filter: { type: 'text', label: 'Alıcı' },
      cell: inv => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{inv.recipientName}</span> },
    { key: 'invoiceType', header: 'FATURA TİPİ', sortable: true, priority: 2, filter: { type: 'enum', multiple: true, label: 'Tip', options: Object.entries(INVOICE_TYPE_MAP).map(([value, label]) => ({ value, label })) },
      cell: inv => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{INVOICE_TYPE_MAP[inv.invoiceType] ?? inv.invoiceType}</span> },
    { key: 'currency', header: 'PARA BİRİMİ', sortable: true, priority: 3, align: 'center', filter: { type: 'text', label: 'Para birimi' },
      cell: inv => <span className="text-xs" style={xs}>{inv.currencyCode || 'TRY'}</span> },
    { key: 'subtotal', header: 'TOPLAM TUTAR', sortable: true, align: 'right', priority: 2, filter: { type: 'number', label: 'Toplam tutar' },
      cell: inv => <span className="text-sm tabular-nums" style={{ color: 'var(--text)' }}>{para(inv.subtotal, inv.currencyCode)}</span> },
    { key: 'total', header: 'ÖDENECEK', sortable: true, align: 'right', priority: 1, filter: { type: 'number', label: 'Ödenecek tutar' },
      cell: inv => <span className="text-sm font-medium tabular-nums" style={{ color: 'var(--text)' }}>{para(inv.grandTotal, inv.currencyCode)}</span> },
    { key: 'taxBase', header: 'VERGİ MATRAHI', sortable: true, align: 'right', priority: 3, filter: { type: 'number', label: 'Vergi matrahı' },
      cell: inv => <span className="text-sm tabular-nums" style={{ color: 'var(--text-m)' }}>{para((inv.subtotal ?? 0) - (inv.totalDiscount ?? 0), inv.currencyCode)}</span> },
    { key: 'totalTax', header: 'VERGİ TOPLAMI', sortable: true, align: 'right', priority: 3, filter: { type: 'number', label: 'Vergi toplamı' },
      cell: inv => <span className="text-sm tabular-nums" style={{ color: 'var(--text-m)' }}>{para(inv.totalTax, inv.currencyCode)}</span> },
    { key: 'integratorStatus', header: 'ENTEGRATÖR', sortable: true, priority: 2, filter: { type: 'enum', multiple: true, label: 'Entegratör durumu', options: Object.entries(INTEGRATOR_STATUS).map(([value, label]) => ({ value, label })) },
      cell: inv => (
        <div className="text-xs leading-tight">
          <div style={{ color: 'var(--text)' }}>{INTEGRATOR_STATUS[inv.integratorStatus] ?? inv.integratorStatus}{inv.hasIntegratorPdf ? ' · PDF ✓' : ''}</div>
          {inv.integratorSentAt && <div style={xs}>{fmtDt(inv.integratorSentAt)}</div>}
        </div>) },
    { key: 'erpStatus', header: 'ERP', sortable: true, priority: 3, filter: { type: 'enum', multiple: true, label: 'ERP durumu', options: Object.entries(ERP_STATUS).filter(([v]) => v !== '').map(([value, label]) => ({ value, label })) },
      cell: inv => (
        <div className="text-xs leading-tight">
          <div style={{ color: 'var(--text)' }}>{ERP_STATUS[inv.erpStatus ?? ''] ?? inv.erpStatus}{inv.erpReference ? ` · ${inv.erpReference}` : ''}</div>
          {inv.erpSentAt && <div style={xs}>{fmtDt(inv.erpSentAt)}</div>}
        </div>) },
    { key: 'marketplace', header: 'PAZARYERİ', priority: 3,
      cell: inv => inv.numberSource === 'marketplace'
        ? <div className="text-xs leading-tight"><div style={{ color: 'var(--text)' }}>{inv.externalSource || 'Pazaryeri'} kesti</div>{inv.externalDocumentId && <div style={xs}>{inv.externalDocumentId}</div>}</div>
        : <span className="text-xs" style={xs}>—</span> },
    { key: 'status', header: 'DURUM', lockVisible: true, sortable: true, priority: 1,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(INVOICE_STATUS_MAP).map(([value, v]) => ({ value, label: v.label })) },
      cell: inv => { const st = INVOICE_STATUS_MAP[inv.status] ?? { label: inv.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> } },
    { key: 'actions', header: '', priority: 1, align: 'right', exportable: false, lockVisible: true, frozenRight: true, stopRowClick: true,
      cell: inv => (
        <div className="flex items-center justify-end gap-1 whitespace-nowrap">
          <Button size="sm" variant="secondary" onClick={() => void pdfAc(inv, setPdfErr)}>Görüntüle</Button>
          <Button size="sm" variant="ghost" onClick={() => setUrlInv(inv)}>URL</Button>
        </div>) },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Faturalar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{tab === 'queue' ? 'Gönderim kuyruğu' : `${totalCount.toLocaleString('tr-TR')} kayıt${grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}`}</p>
        </div>
        <Link to="/orders/invoice-series"><Button size="sm" variant="secondary">Fatura Serileri</Button></Link>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => {
          const key = t.key === '' ? 'all' : t.key
          return (
            <button key={key} className={cn('stab', tab === key && 'active')} onClick={() => switchTab(key)}>{t.label}</button>
          )
        })}
      </div>

      {pdfErr && <div className="mb-3 px-3 py-2 rounded text-sm" style={{ background: 'var(--danger-bg,#fef2f2)', color: '#b91c1c' }}>{pdfErr}</div>}

      {tab === 'queue' ? <DispatchQueue /> : (
        <DataGrid<InvoiceSummary>
          gridId="invoices"
          views
          grid={grid}
          columns={columns}
          extraFilters={INVOICE_EXTRA_FILTERS}
          search={{ placeholder: 'Fatura no, alıcı, vergi no, dış belge no…' }}
          rows={invoices}
          totalCount={totalCount}
          loading={isLoading}
          fetching={isFetching}
          error={error ? errText(error) : null}
          empty={'Fatura bulunamadı. Fatura, sipariş detayındaki "Fatura Oluştur" ile kesilir.'}
          minWidth={1400}
          export={{ endpoint: '/orders/invoices/export', named: () => ({ status: tab && tab !== 'all' ? tab : undefined }), fallbackFileName: 'faturalar.xlsx' }}
          compact={{
            title: inv => inv.invoiceNumber,
            subtitle: inv => `${inv.orderNumber ?? ''} · ${inv.recipientTaxNumber ?? inv.recipientName} · ${fmtD(inv.invoiceDate)}`,
            right: inv => para(inv.grandTotal, inv.currencyCode),
            badge: inv => { const st = INVOICE_STATUS_MAP[inv.status] ?? { label: inv.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> },
          }}
        />
      )}

      {urlInv && <UrlPopup inv={urlInv} onClose={() => setUrlInv(null)} />}
    </div>
  )
}
