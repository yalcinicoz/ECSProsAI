import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { formatDate } from '@/lib/i18n'
import { Button } from '@/components/ui/Button'
import { DURUM_AD, durumRozet, type SupplierOrder } from './OrdersPage'

/* Sipariş detayı (2026-08-11): kalemler + teslimat + kargo bildirimi (yalnız kargo modu
 * 'satıcı gönderir' ise) + satıcı fatura bilgisi. Kargo bildirimi partner API ile AYNI
 * zincirden geçer — paket başına tek bildirim, sipariş tamamlanınca durum kendiliğinden döner. */

const hataMetni = (e: unknown, varsayilan: string) => {
  const d = (e as { response?: { data?: { error?: string; errors?: { message: string }[] } } })?.response?.data
  return d?.error ?? d?.errors?.map(x => x.message).join(' ') ?? varsayilan
}

export function OrderDetailPage() {
  const { orderNumber } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { data: order, isLoading } = useQuery<SupplierOrder>({
    queryKey: ['supplier-order', orderNumber],
    queryFn: async () => (await api.get(`/supplier/orders/${encodeURIComponent(orderNumber!)}`)).data.data,
  })
  const { data: settings } = useQuery<{ cargoMode: string }>({
    queryKey: ['supplier-settings'],
    queryFn: async () => (await api.get('/supplier/account/settings')).data.data,
  })

  const [carrier, setCarrier] = useState('')
  const [tracking, setTracking] = useState('')
  const [trackingUrl, setTrackingUrl] = useState('')
  const [invoiceNo, setInvoiceNo] = useState('')
  const [invoiceUrl, setInvoiceUrl] = useState('')

  const kargoBildir = useMutation({
    mutationFn: async () =>
      (await api.post(`/supplier/orders/${encodeURIComponent(orderNumber!)}/shipment`,
        { carrierName: carrier, trackingNumber: tracking, trackingUrl: trackingUrl || null })).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['supplier-order', orderNumber] }),
  })
  const faturaKaydet = useMutation({
    mutationFn: async () =>
      (await api.put(`/supplier/orders/${encodeURIComponent(orderNumber!)}/invoice`,
        { invoiceNumber: invoiceNo, invoiceUrl: invoiceUrl || null })).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['supplier-order', orderNumber] }),
  })

  if (isLoading) return <div className="py-8 text-center opacity-60">Yükleniyor…</div>
  if (!order) return <div className="py-8 text-center opacity-60">Sipariş bulunamadı.</div>

  const kargolanabilir = ['confirmed', 'processing'].includes(order.status)
  const toplam = order.items.reduce((t, i) => t + i.total, 0)

  return (
    <div className="max-w-4xl">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <h1 className="text-lg font-bold">{order.orderNumber}</h1>
          <span className={durumRozet(order.status)}>{DURUM_AD[order.status] ?? order.status}</span>
        </div>
        <Button variant="secondary" onClick={() => navigate('/orders')}>Siparişlerim</Button>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 mb-4">
        <div className="card p-5">
          <div className="text-xs font-semibold uppercase tracking-wider mb-2 opacity-70">Teslimat</div>
          <div className="text-sm font-medium">{order.shipping.recipientName}</div>
          <div className="text-sm mt-1">{order.shipping.addressLine}</div>
          <div className="text-sm opacity-80">
            {[order.shipping.districtName, order.shipping.cityName, order.shipping.postalCode].filter(Boolean).join(' / ')}
          </div>
          {order.shipping.deliveryNotes && <div className="text-xs mt-2 opacity-70">Not: {order.shipping.deliveryNotes}</div>}
        </div>
        <div className="card p-5">
          <div className="text-xs font-semibold uppercase tracking-wider mb-2 opacity-70">Özet</div>
          <div className="text-sm">Sipariş tarihi: {formatDate(order.createdAt)}</div>
          <div className="text-sm">Kalem: {order.items.length} — Toplam: <strong>{toplam.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} {order.currencyCode}</strong></div>
          <div className="text-sm mt-1">Paketler: {order.packages.length > 0
            ? order.packages.map(p => `${p.packageNumber} (${p.status})`).join(', ')
            : 'henüz paket yok'}</div>
        </div>
      </div>

      <div className="card tbl-wrap overflow-x-auto mb-4">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs opacity-70">
              <th className="py-2 px-3">Ürün</th>
              <th className="py-2 px-3">SKU</th>
              <th className="py-2 px-3 text-right">Adet</th>
              <th className="py-2 px-3 text-right">Birim</th>
              <th className="py-2 px-3 text-right">İndirim</th>
              <th className="py-2 px-3 text-right">Tutar</th>
            </tr>
          </thead>
          <tbody>
            {order.items.map(i => (
              <tr key={i.orderItemId} className="border-t">
                <td className="py-2 px-3">{i.productName}{i.variantInfo && <span className="opacity-60"> — {i.variantInfo}</span>}</td>
                <td className="py-2 px-3"><code className="text-xs">{i.sku}</code></td>
                <td className="py-2 px-3 text-right">{i.quantity}</td>
                <td className="py-2 px-3 text-right">{i.unitPrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 })}</td>
                <td className="py-2 px-3 text-right">{i.discountAmount.toLocaleString('tr-TR', { minimumFractionDigits: 2 })}</td>
                <td className="py-2 px-3 text-right font-medium">{i.total.toLocaleString('tr-TR', { minimumFractionDigits: 2 })}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="card p-5">
          <div className="text-xs font-semibold uppercase tracking-wider mb-2 opacity-70">Kargo Bildirimi</div>
          {settings?.cargoMode !== 'seller_ships' ? (
            <p className="text-sm opacity-70">
              Kargo modunuz "platform gönderir" — gönderimi biz yapıyoruz, takip bilgisi girmenize gerek yok.
              Modu Hesabım sayfasından değiştirebilirsiniz.
            </p>
          ) : !kargolanabilir ? (
            <p className="text-sm opacity-70">Bu durumda kargo bildirilemez (sipariş onaylı/işlemde olmalı ya da zaten kargolandı).</p>
          ) : (
            <div className="grid gap-2">
              <input className="inp w-full" placeholder="Taşıyıcı (örn. Aras Kargo)" value={carrier} onChange={e => setCarrier(e.target.value)} />
              <input className="inp w-full" placeholder="Takip numarası" value={tracking} onChange={e => setTracking(e.target.value)} />
              <input className="inp w-full" placeholder="Takip linki (isteğe bağlı)" value={trackingUrl} onChange={e => setTrackingUrl(e.target.value)} />
              <div className="flex items-center gap-3">
                <Button onClick={() => kargoBildir.mutate()} disabled={kargoBildir.isPending || !carrier.trim() || !tracking.trim()}>
                  {kargoBildir.isPending ? 'Bildiriliyor…' : 'Kargoladım'}
                </Button>
                {kargoBildir.isSuccess && <span className="text-xs text-green-600">Bildirildi ✓</span>}
                {kargoBildir.isError && <span className="text-xs text-red-600">{hataMetni(kargoBildir.error, 'Bildirilemedi')}</span>}
              </div>
            </div>
          )}
        </div>

        <div className="card p-5">
          <div className="text-xs font-semibold uppercase tracking-wider mb-2 opacity-70">Fatura Bilgisi</div>
          <p className="text-xs opacity-70 mb-2">Kestiğiniz faturanın numarasını (ve varsa görüntü linkini) girin — paketinize kaydedilir.</p>
          <div className="grid gap-2">
            <input className="inp w-full" placeholder="Fatura numarası" value={invoiceNo} onChange={e => setInvoiceNo(e.target.value)} />
            <input className="inp w-full" placeholder="Fatura görüntü linki (isteğe bağlı)" value={invoiceUrl} onChange={e => setInvoiceUrl(e.target.value)} />
            <div className="flex items-center gap-3">
              <Button variant="secondary" onClick={() => faturaKaydet.mutate()} disabled={faturaKaydet.isPending || !invoiceNo.trim()}>
                {faturaKaydet.isPending ? 'Kaydediliyor…' : 'Faturayı Kaydet'}
              </Button>
              {faturaKaydet.isSuccess && <span className="text-xs text-green-600">Kaydedildi ✓</span>}
              {faturaKaydet.isError && <span className="text-xs text-red-600">{hataMetni(faturaKaydet.error, 'Kaydedilemedi')}</span>}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
