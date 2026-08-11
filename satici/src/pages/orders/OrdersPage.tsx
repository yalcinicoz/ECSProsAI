import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { formatDate } from '@/lib/i18n'

/* Siparişlerim (2026-08-11) — partner GET /orders ile aynı görünüm: yalnız satıcının
 * kalemleri, müşteriden yalnız ad-soyad + teslimat adresi (K2). */

export interface SupplierOrder {
  orderNumber: string
  status: string
  paymentStatus: string
  currencyCode: string
  createdAt: string
  updatedAt: string | null
  shipping: { recipientName: string; addressLine: string; cityName: string | null; districtName: string | null; postalCode: string | null; deliveryNotes: string | null }
  items: { orderItemId: string; sku: string; productName: string; variantInfo: string; quantity: number; unitPrice: number; discountAmount: number; total: number }[]
  packages: { packageNumber: string; status: string; packedAt: string | null }[]
}

export const DURUM_AD: Record<string, string> = {
  confirmed: 'Onaylandı', processing: 'Hazırlanıyor', shipped: 'Kargoda',
  delivered: 'Teslim edildi', cancelled: 'İptal',
}
export const durumRozet = (s: string) =>
  s === 'cancelled' ? 'badge br' : s === 'delivered' ? 'badge bg' : 'badge ba'

const STATUS_OPTIONS = [
  { value: '', label: 'Tüm Durumlar' },
  { value: 'confirmed', label: 'Onaylandı' },
  { value: 'processing', label: 'Hazırlanıyor' },
  { value: 'shipped', label: 'Kargoda' },
  { value: 'delivered', label: 'Teslim edildi' },
  { value: 'cancelled', label: 'İptal' },
]

export function OrdersPage() {
  const navigate = useNavigate()
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 20

  const { data, isLoading } = useQuery({
    queryKey: ['supplier-orders', status, page],
    queryFn: async () => {
      const { data } = await api.get('/supplier/orders', {
        params: { status: status || undefined, page, pageSize },
      })
      return data.data as { items: SupplierOrder[]; totalCount: number }
    },
  })

  const orders = data?.items ?? []
  const totalPages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / pageSize))

  return (
    <div>
      <div className="flex flex-wrap items-center gap-3 mb-4">
        <h1 className="text-lg font-bold flex-1">Siparişlerim</h1>
        <select className="inp !w-44" value={status} onChange={e => { setStatus(e.target.value); setPage(1) }}>
          {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      </div>

      <div className="card tbl-wrap overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs opacity-70">
              <th className="py-2 px-3">Sipariş No</th>
              <th className="py-2 px-3">Tarih</th>
              <th className="py-2 px-3">Alıcı</th>
              <th className="py-2 px-3">Kalem</th>
              <th className="py-2 px-3 text-right">Tutar</th>
              <th className="py-2 px-3">Paket</th>
              <th className="py-2 px-3">Durum</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <tr><td colSpan={7} className="py-8 text-center opacity-60">Yükleniyor…</td></tr>}
            {!isLoading && orders.length === 0 && (
              <tr><td colSpan={7} className="py-8 text-center opacity-60">Sipariş yok.</td></tr>
            )}
            {orders.map(o => (
              <tr key={o.orderNumber} className="cursor-pointer hover:bg-black/5 border-t"
                onClick={() => navigate(`/orders/${encodeURIComponent(o.orderNumber)}`)}>
                <td className="py-2.5 px-3 font-medium">{o.orderNumber}</td>
                <td className="py-2.5 px-3 whitespace-nowrap">{formatDate(o.createdAt)}</td>
                <td className="py-2.5 px-3">{o.shipping.recipientName}</td>
                <td className="py-2.5 px-3">{o.items.length}</td>
                <td className="py-2.5 px-3 text-right whitespace-nowrap">
                  {o.items.reduce((t, i) => t + i.total, 0).toLocaleString('tr-TR', { minimumFractionDigits: 2 })} TL
                </td>
                <td className="py-2.5 px-3">{o.packages.map(p => p.packageNumber).join(', ') || '—'}</td>
                <td className="py-2.5 px-3"><span className={durumRozet(o.status)}>{DURUM_AD[o.status] ?? o.status}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-between mt-4 text-sm">
        <span className="opacity-70">Toplam {data?.totalCount ?? 0} sipariş</span>
        <div className="flex gap-2">
          <button className="px-3 py-1.5 rounded-lg border disabled:opacity-40" style={{ borderColor: 'var(--border)' }} disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Önceki</button>
          <span className="py-1.5">{page} / {totalPages}</span>
          <button className="px-3 py-1.5 rounded-lg border disabled:opacity-40" style={{ borderColor: 'var(--border)' }} disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Sonraki</button>
        </div>
      </div>
    </div>
  )
}
