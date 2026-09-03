import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { cn } from '@/lib/utils'
import { ORDER_STATUS_MAP, PAYMENT_METHOD_MAP, PAYMENT_STATUS_MAP } from './orderConstants'

// 2026-08-04 (kullanıcı kararı): filtre yöntem değil tahsilat durumu —
// alınan = paid; alınmayan = paid dışı her durum (unpaid/pending/failed/partial)
const PAYMENT_COLLECTED_FILTER = [
  { value: '', label: 'Ödeme: Tümü' },
  { value: 'true', label: 'Ödemesi Alınan' },
  { value: 'false', label: 'Ödemesi Alınmayan' },
]

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

function daysAgoIso(days: number): string {
  const d = new Date()
  d.setDate(d.getDate() - days)
  return d.toISOString().slice(0, 10)
}

export function OrdersPage() {
  const navigate = useNavigate()

  const [tabKey, setTabKey] = useState('active')
  const [search, setSearch] = useState('')          // input değeri
  const [appliedSearch, setAppliedSearch] = useState('')
  const [paymentCollected, setPaymentCollected] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [page, setPage] = useState(1)

  const tab = TABS.find(t => t.key === tabKey) ?? TABS[0]

  // Sayaçlar — eski binary'de endpoint yoksa (restart öncesi) sessizce gizlenir
  const { data: counts } = useQuery<Record<string, number>>({
    queryKey: ['order-status-counts'],
    queryFn: async () => {
      const { data } = await api.get('/orders/status-counts')
      return data.data
    },
    refetchInterval: 60_000,
    retry: false,
  })
  const activeTotal = counts
    ? Object.values(counts).reduce((a, b) => a + b, 0)
    : undefined

  const { data: ordersData, isLoading } = useQuery<PagedResult<OrderSummary>>({
    queryKey: ['orders', tabKey, appliedSearch, fromDate, toDate, paymentCollected, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (tab.statuses) params.set('statuses', tab.statuses)
      if (appliedSearch) params.set('search', appliedSearch)
      if (paymentCollected) params.set('paymentCollected', paymentCollected)
      if (fromDate) params.set('from', new Date(`${fromDate}T00:00:00`).toISOString())
      if (toDate) {
        const end = new Date(`${toDate}T00:00:00`)
        end.setDate(end.getDate() + 1) // exclusive üst sınır: seçilen günün sonu
        params.set('to', end.toISOString())
      }
      const { data } = await api.get(`/orders?${params}`)
      return data.data
    },
  })

  const orders = ordersData?.items ?? []
  const totalCount = ordersData?.totalCount ?? 0
  const totalPages = Math.ceil(totalCount / 20)

  function switchTab(t: OrderTab) {
    setTabKey(t.key)
    setPage(1)
    // Büyük listeler sınırsız taranmasın: tarih boşsa son 30 güne çek
    if (t.heavy && !fromDate) setFromDate(daysAgoIso(30))
  }

  function applySearch() {
    setAppliedSearch(search.trim())
    setPage(1)
  }

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Siparişler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {tab.heavy && fromDate ? `${totalCount} kayıt (seçili tarih aralığında)` : `${totalCount} kayıt`}
          </p>
        </div>
      </div>

      {/* Durum sekmeleri */}
      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => {
          const count = t.key === 'active' ? activeTotal : t.counted ? counts?.[t.counted] : undefined
          return (
            <button key={t.key} className={cn('stab', tabKey === t.key && 'active')}
              onClick={() => switchTab(t)}>
              {t.label}
              {count !== undefined && (
                <span className="ml-1.5 text-xs px-1.5 py-0.5 rounded-full"
                  style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>{count}</span>
              )}
            </button>
          )
        })}
      </div>

      {/* Arama + tarih filtreleri */}
      <div className="flex flex-wrap items-center gap-2 mb-4">
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 260 }}
          placeholder="Sipariş no veya alıcı adı ara…"
          value={search}
          onChange={e => setSearch(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') applySearch() }} />
        <button onClick={applySearch}
          className="px-3 py-1.5 rounded-lg text-sm"
          style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Ara</button>
        <select className="inp text-sm py-1.5 px-2 h-auto w-auto" value={paymentCollected}
          onChange={e => { setPaymentCollected(e.target.value); setPage(1) }}>
          {PAYMENT_COLLECTED_FILTER.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
        <div className="flex items-center gap-1 ml-2">
          <input type="date" className="inp text-sm py-1.5 px-2 h-auto" value={fromDate}
            onChange={e => { setFromDate(e.target.value); setPage(1) }} />
          <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>
          <input type="date" className="inp text-sm py-1.5 px-2 h-auto" value={toDate}
            onChange={e => { setToDate(e.target.value); setPage(1) }} />
          {(fromDate || toDate) && (
            <button onClick={() => { setFromDate(''); setToDate(''); setPage(1) }}
              className="text-xs px-2 py-1" style={{ color: 'var(--text-s)' }}>Temizle</button>
          )}
        </div>
      </div>

      {/* Table */}
      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['SİPARİŞ NO', 'MÜŞTERİ', 'TUTAR', 'ÖDEME', 'DURUM', 'TARİH', ''].map(h => (
                <th key={h} className={`px-4 py-3 text-xs font-semibold ${h === '' ? 'w-20' : 'text-left'}`}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Yükleniyor...
              </td></tr>
            )}
            {!isLoading && orders.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Sipariş bulunamadı.
              </td></tr>
            )}
            {orders.map(o => {
              const st = ORDER_STATUS_MAP[o.status] ?? { label: o.status, variant: 'neutral' as const }
              return (
                <tr key={o.id}
                  onClick={() => navigate(`/orders/${o.id}`)}
                  className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                  style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-3">
                    <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{o.orderNumber}</code>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm" style={{ color: 'var(--text-m)' }}>{o.recipientName ?? '—'}</span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>
                      {o.grandTotal.toLocaleString('tr-TR', { minimumFractionDigits: 2 })}
                      {' '}{o.currencyCode === 'TRY' ? '₺' : o.currencyCode}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm" style={{ color: 'var(--text)' }}>
                      {o.paymentMethod ? (PAYMENT_METHOD_MAP[o.paymentMethod] ?? o.paymentMethod) : '—'}
                    </span>
                    <div className="text-xs" style={{ color: 'var(--text-s)' }}>
                      {PAYMENT_STATUS_MAP[o.paymentStatus] ?? o.paymentStatus}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <Badge variant={st.variant}>{st.label}</Badge>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>
                      {new Date(o.createdAt).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' })}
                    </span>
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

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 mt-4">
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>
            ← Önceki
          </button>
          <span className="text-sm" style={{ color: 'var(--text-s)' }}>{page} / {totalPages}</span>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>
            Sonraki →
          </button>
        </div>
      )}
    </div>
  )
}
