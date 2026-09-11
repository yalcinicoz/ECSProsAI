import { useQuery } from '@tanstack/react-query'
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { DataGrid, useGridState, type GridColumn, type GridFilterField } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { ORDER_STATUS_MAP, PAYMENT_METHOD_MAP, PAYMENT_STATUS_MAP } from './orderConstants'

// Siparişler: sütun ve gelişmiş filtreler aynı URL durumunu kullanır.
export interface OrderSummary {
  id: string
  orderNumber: string
  memberId: string | null
  status: string
  paymentStatus: string
  grandTotal: number
  currencyCode: string
  createdAt: string
  recipientName?: string
  paymentMethod?: string | null
  requestedCargoName?: string | null   // FAZ 15.2c: müşterinin kargo tercihi
  customerNote?: string | null         // FAZ 15.4h: müşteri notu (CustomerNotes.note)
  internalNotes?: string | null        // FAZ 15.4h: iç not
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

const enumOpts = (m: Record<string, string>) => Object.entries(m).map(([value, label]) => ({ value, label }))

// Ek alanlar gelişmiş filtrelerde; sütun filtreleri de aynı ortak durumu kullanır.
const EXTRA_FILTERS: GridFilterField[] = [
  { key: 'paid', label: 'Ödemesi alınan', type: 'boolean' },
  { key: 'barcode', label: 'Barkod', type: 'text', ops: ['eq'] },
  { key: 'productCode', label: 'Ürün kodu', type: 'text', ops: ['eq'] },
  { key: 'hasNotes', label: 'Notu olan', type: 'boolean' },
]
const PAYMENT_FILTERS = [
  { field: 'paymentStatus', label: 'Ödeme durumu', type: 'enum' as const, multiple: true, options: enumOpts(PAYMENT_STATUS_MAP) },
  { field: 'paymentMethod', label: 'Ödeme yöntemi', type: 'enum' as const, multiple: true, options: [...enumOpts(PAYMENT_METHOD_MAP), { value: 'none', label: 'Yöntemsiz (eski kayıt)' }] },
]

export function OrdersPage() {
  const [params] = useSearchParams()
  const legacyTab = params.get('tab')
  if (legacyTab !== null) {
    const next = new URLSearchParams(params)
    next.delete('tab')
    const status = legacyTab === 'active' ? 'pending,confirmed,processing,shipped'
      : Object.hasOwn(ORDER_STATUS_MAP, legacyTab) ? legacyTab : ''
    if (status && !next.has('f.status')) next.set('f.status', `in:${status}`)
    return <Navigate replace to={{ search: next.toString() ? `?${next}` : '' }} />
  }
  return <OrdersGrid />
}

function OrdersGrid() {
  const navigate = useNavigate()
  const grid = useGridState('orders', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })
  const { data: ordersData, isLoading, isFetching, error: ordersError } = useQuery<PagedResult<OrderSummary>>({
    queryKey: ['orders', ...grid.queryKey],
    queryFn: async () => (await api.get(`/orders?${grid.toParams()}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const orders = ordersData?.items ?? []
  const totalCount = ordersData?.totalCount ?? 0

  const columns: GridColumn<OrderSummary>[] = [
    { key: 'orderNumber', header: 'SİPARİŞ NO', frozen: true, lockVisible: true, sortable: true, minWidth: 130,
      filter: { type: 'text', label: 'Sipariş no', ops: ['startswith', 'contains', 'eq'] },
      // FAZ 15.2c (2026-09-10, eski "Üründen Sipariş Sorgula"): ürün kodu / adı kalem filtresi (sunucuda OrderGrid.ApplyProductFilter)
      filters: [{ field: 'externalOrderNumber', label: 'Dış sipariş no', type: 'text' }, { field: 'product', label: 'Ürün kodu / adı (kalem)', type: 'text', ops: ['contains', 'startswith', 'eq'] }],
      cell: o => <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{o.orderNumber}</code> },
    { key: 'customer', header: 'MÜŞTERİ', frozen: true, sortable: true, priority: 1, filter: { type: 'text', label: 'Müşteri' },
      filters: [{ field: 'phone', label: 'Telefon', type: 'text', ops: ['contains', 'startswith'] }],
      cell: o => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{o.recipientName ?? '—'}</span> },
    { key: 'total', header: 'TUTAR', sortable: true, align: 'right', priority: 1, filter: { type: 'number', label: 'Tutar' },
      cell: o => <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>
        {o.grandTotal.toLocaleString('tr-TR', { minimumFractionDigits: 2 })}{' '}{o.currencyCode === 'TRY' ? '₺' : o.currencyCode}</span> },
    { key: 'paymentStatus', header: 'ÖDEME', sortable: true, priority: 2, filters: PAYMENT_FILTERS,
      cell: o => <>
        <span className="text-sm" style={{ color: 'var(--text)' }}>{o.paymentMethod ? (PAYMENT_METHOD_MAP[o.paymentMethod] ?? o.paymentMethod) : '—'}</span>
        <div className="text-xs" style={{ color: 'var(--text-s)' }}>{PAYMENT_STATUS_MAP[o.paymentStatus] ?? o.paymentStatus}</div>
      </> },
    { key: 'status', header: 'DURUM', lockVisible: true, sortable: true, priority: 1,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(ORDER_STATUS_MAP).map(([value, v]) => ({ value, label: v.label })) },
      cell: o => { const st = ORDER_STATUS_MAP[o.status] ?? { label: o.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> } },
    // FAZ 15.2c (eski "Kargo Firmaları Sipariş"): kargo kolonu + filtresi (teslimat adımındaki tercih; kargoya veriş bunu varsayılan alır)
    { key: 'cargo', header: 'KARGO', sortable: true, priority: 3, filter: { type: 'text', label: 'Kargo' },
      cell: o => <span className="text-xs" style={{ color: 'var(--text-m)' }}>{o.requestedCargoName ?? '—'}</span> },
    // FAZ 15.4h: müşteri notu + iç not (varsayılan gizli; "Notu olan" hızlı filtresiyle tarih aralıklı not listesi)
    { key: 'note', header: 'NOTLAR', priority: 3, defaultVisible: false, minWidth: 220, filter: { type: 'text', label: 'Müşteri notu' },
      filters: [{ field: 'internalNote', label: 'İç not', type: 'text' }],
      cell: o => (o.customerNote || o.internalNotes)
        ? <div className="text-xs" style={{ color: 'var(--text-m)' }}>
            {o.customerNote && <div title={o.customerNote}><span style={{ color: 'var(--text-s)' }}>Müşteri: </span>{o.customerNote.length > 90 ? o.customerNote.slice(0, 90) + '…' : o.customerNote}</div>}
            {o.internalNotes && <div title={o.internalNotes}><span style={{ color: 'var(--text-s)' }}>İç: </span>{o.internalNotes.length > 90 ? o.internalNotes.slice(0, 90) + '…' : o.internalNotes}</div>}
          </div>
        : <span style={{ color: 'var(--text-s)' }}>—</span> },
    { key: 'createdAt', header: 'TARİH', sortable: true, priority: 2, filter: { type: 'date', label: 'Tarih', quick: true },
      cell: o => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{new Date(o.createdAt).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' })}</span> },
    { key: 'detail', header: '', priority: 3, align: 'right', exportable: false, cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Siparişler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {totalCount.toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}
          </p>
        </div>
      </div>

      <DataGrid<OrderSummary>
        gridId="orders"
        grid={grid}
        columns={columns}
        extraFilters={EXTRA_FILTERS}
        advancedFilters
        search={{ placeholder: 'Sipariş no, dış sipariş no, alıcı adı, telefon…' }}
        rows={orders}
        totalCount={totalCount}
        loading={isLoading}
        fetching={isFetching}
        error={ordersError ? errText(ordersError) : null}
        onRowClick={o => navigate(`/orders/${o.id}`)}
        empty="Sipariş bulunamadı."
        minWidth={820}
        export={{ endpoint: '/orders/export', fallbackFileName: 'siparisler.xlsx' }}
        views
        compact={{
          title: o => o.orderNumber,
          subtitle: o => `${o.recipientName ?? '—'} · ${new Date(o.createdAt).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' })}`,
          right: o => `${o.grandTotal.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ${o.currencyCode === 'TRY' ? '₺' : o.currencyCode}`,
          badge: o => { const st = ORDER_STATUS_MAP[o.status] ?? { label: o.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> },
        }}
      />
    </div>
  )
}
