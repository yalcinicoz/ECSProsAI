import { useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { cn } from '@/lib/utils'
import { DataGrid, useGridState, quickDateRange, type GridColumn, type GridFilterField } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { ORDER_STATUS_MAP, PAYMENT_METHOD_MAP, PAYMENT_STATUS_MAP } from './orderConstants'

// Siparişler — DataGrid UX pilotu (docs/datagrid-standardi-plani.md F2, K5). Durum sekmeleri (sayaçlı) korunur; sayaçlar listeyle
// aynı filtre parametrelerini alır (durum hariç). Filtre/arama/sıralama/sayfa URL'de; kolon tercihleri localStorage'da.
// Sunucu beyaz listesi: OrderGrid.Schema (Order.Application) — filter alanları ve sort anahtarları oradakilerle birebir.

// Aktif küme küçük kalır (partial index) — sayaç yalnız bunlarda; Teslim/İptal/Tümü
// milyonlara ulaşacağından sayaçsız + son-30-gün varsayılanıyla açılır (P1a kararı, K19)
const ACTIVE_STATUSES = 'pending,confirmed,processing,shipped'

interface OrderTab {
  key: string
  label: string
  statuses: string   // virgüllü; '' = tümü
  counted?: string   // sayaç gösterilecekse tekil durum kodu
  heavy?: boolean    // büyük liste: varsayılan son-30-gün
}

const TABS: OrderTab[] = [
  { key: 'active',     label: 'Aktif',    statuses: ACTIVE_STATUSES },
  { key: 'pending',    label: 'Bekleyen', statuses: 'pending',    counted: 'pending' },
  { key: 'confirmed',  label: 'Onaylı',   statuses: 'confirmed',  counted: 'confirmed' },
  { key: 'processing', label: 'İşlemde',  statuses: 'processing', counted: 'processing' },
  { key: 'shipped',    label: 'Kargoda',  statuses: 'shipped',    counted: 'shipped' },
  { key: 'delivered',  label: 'Teslim',   statuses: 'delivered',  heavy: true },
  { key: 'cancelled',  label: 'İptal',    statuses: 'cancelled',  heavy: true },
  { key: 'all',        label: 'Tümü',     statuses: '',           heavy: true },
]

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
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

const enumOpts = (m: Record<string, string>) => Object.entries(m).map(([value, label]) => ({ value, label }))

// Hızlı filtreler çubukta; diğer alanlar ilgili sütun başlığının filtre penceresinde (kullanıcı kararı 2026-09-08)
const EXTRA_FILTERS: GridFilterField[] = [
  { key: 'paid', label: 'Ödemesi alınan', type: 'boolean', quick: true },
]
const PAYMENT_FILTERS = [
  { field: 'paymentStatus', label: 'Ödeme durumu', type: 'enum' as const, multiple: true, options: enumOpts(PAYMENT_STATUS_MAP) },
  { field: 'paymentMethod', label: 'Ödeme yöntemi', type: 'enum' as const, multiple: true, options: [...enumOpts(PAYMENT_METHOD_MAP), { value: 'none', label: 'Yöntemsiz (eski kayıt)' }] },
]

export function OrdersPage() {
  const navigate = useNavigate()
  const grid = useGridState('orders', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })
  const [sp] = useSearchParams()
  const tabKey = sp.get('tab') ?? 'active'
  const tab = TABS.find(t => t.key === tabKey) ?? TABS[0]

  // Sayaçlar — listeyle aynı filtreler (durum sekmesi hariç); eski binary'de endpoint yoksa sessizce gizlenir
  const countParams = (() => { const p = grid.toParams(); p.delete('page'); p.delete('pageSize'); p.delete('sort'); p.delete('dir'); return p.toString() })()
  const { data: counts } = useQuery<Record<string, number>>({
    queryKey: ['order-status-counts', countParams],
    queryFn: async () => (await api.get(`/orders/status-counts?${countParams}`)).data.data,
    refetchInterval: 60_000,
    retry: false,
    placeholderData: prev => prev,
  })
  const activeTotal = counts ? Object.values(counts).reduce((a, b) => a + b, 0) : undefined

  const { data: ordersData, isLoading, isFetching, error: ordersError } = useQuery<PagedResult<OrderSummary>>({
    queryKey: ['orders', tab.key, ...grid.queryKey],
    queryFn: async () => (await api.get(`/orders?${grid.toParams({ statuses: tab.statuses || undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const orders = ordersData?.items ?? []
  const totalCount = ordersData?.totalCount ?? 0

  function switchTab(t: OrderTab) {
    grid.mutate(n => {
      if (t.key === 'active') n.delete('tab'); else n.set('tab', t.key)
      // Büyük listeler sınırsız taranmasın: tarih filtresi yoksa son 30 güne çek (çipte görünür, kaldırılabilir)
      if (t.heavy && !n.has('f.createdAt')) { n.set('f.createdAt', `between:${quickDateRange('last30')}`); n.set('fq.createdAt', 'last30') }
    })
  }

  const columns: GridColumn<OrderSummary>[] = [
    { key: 'orderNumber', header: 'SİPARİŞ NO', frozen: true, lockVisible: true, sortable: true, minWidth: 130,
      filter: { type: 'text', label: 'Sipariş no', ops: ['startswith', 'contains', 'eq'] },
      filters: [{ field: 'externalOrderNumber', label: 'Dış sipariş no', type: 'text' }, { field: 'cargo', label: 'Kargo', type: 'text' }],
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

      {/* Durum sekmeleri — hızlı filtre; sayaçlar diğer aktif filtrelerle tutarlı */}
      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => {
          const count = t.key === 'active' ? activeTotal : t.counted ? counts?.[t.counted] : undefined
          return (
            <button key={t.key} className={cn('stab', tab.key === t.key && 'active')} onClick={() => switchTab(t)}>
              {t.label}
              {count !== undefined && (
                <span className="ml-1.5 text-xs px-1.5 py-0.5 rounded-full" style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>{count.toLocaleString('tr-TR')}</span>
              )}
            </button>
          )
        })}
      </div>

      <DataGrid<OrderSummary>
        gridId="orders"
        grid={grid}
        columns={columns}
        extraFilters={EXTRA_FILTERS}
        search={{ placeholder: 'Sipariş no, dış sipariş no, alıcı adı, telefon…' }}
        rows={orders}
        totalCount={totalCount}
        loading={isLoading}
        fetching={isFetching}
        error={ordersError ? errText(ordersError) : null}
        onRowClick={o => navigate(`/orders/${o.id}`)}
        empty="Sipariş bulunamadı."
        minWidth={820}
        export={{ endpoint: '/orders/export', named: () => ({ statuses: tab.statuses || undefined }), fallbackFileName: 'siparisler.xlsx' }}
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
