/**
 * T2 Mal Kabul parti detayı: başlık (düzenlenebilir) + durum aksiyonları + gevşek SA bağları (bağla/çöz)
 * + kaba evrak kalemleri + fatura bağı. Hiçbir alan ayrıştırmayı kısıtlamaz (İ2/İ3).
 */
import { useEffect, useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Plus, Trash2, X } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'
import { PO_STATUS, useSuppliers } from './PurchaseOrdersPage'
import { RB_STATUS, useWarehouses, whName } from './ReceiptsPage'

interface ItemDto { id: string; descriptionText: string; quantity: number | null; unitPrice: number | null }
interface LinkedPo { id: string; code: string; status: string; orderDate: string; itemCount: number; totalQuantity: number; totalAmount: number }
interface DetailDto {
  id: string; code: string; supplierId: string; warehouseId: string; receivedAt: string
  packageCount: number | null; deliveryNoteNumber: string | null; supplierInvoiceId: string | null
  status: string; notes: string | null; items: ItemDto[]; purchaseOrders: LinkedPo[]
}
interface InvoiceOpt { id: string; invoiceNumber: string; invoiceDate: string; grandTotal: number }

const NEXT: Record<string, { to: string; label: string; variant?: 'secondary' }[]> = {
  received: [{ to: 'sorting', label: 'Ayrıştırmaya Başla' }],
  sorting: [{ to: 'completed', label: 'Tamamla' }, { to: 'received', label: 'Geri Al', variant: 'secondary' }],
  completed: [{ to: 'sorting', label: 'Geri Aç', variant: 'secondary' }],
}
const tl = (n: number) => n.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const num = (s: string) => { const v = parseFloat(s.replace(/\./g, '').replace(',', '.')); return isNaN(v) ? parseFloat(s.replace(',', '.')) : v }

export function ReceiptDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { data: suppliers = [] } = useSuppliers()
  const { data: warehouses = [] } = useWarehouses()
  const [err, setErr] = useState<string | null>(null)

  const { data: b, isLoading } = useQuery<DetailDto>({
    queryKey: ['receipt-batch', id],
    queryFn: async () => (await api.get(`/procurement/receipts/${id}`)).data.data,
    enabled: !!id,
  })
  const invalidate = () => { qc.invalidateQueries({ queryKey: ['receipt-batch', id] }); qc.invalidateQueries({ queryKey: ['receipt-batches'] }) }
  const onErr = (e: any) => setErr(e?.response?.data?.error ?? 'İşlem başarısız.')

  // Tedarikçinin faturaları (bağ için) + açık SA'ları
  const { data: invoices = [] } = useQuery<InvoiceOpt[]>({
    queryKey: ['supplier-invoices-of', b?.supplierId],
    queryFn: async () => (await api.get(`/finance/supplier-invoices?currentAccountId=${b!.supplierId}&pageSize=100`)).data.data?.items ?? [],
    enabled: !!b?.supplierId,
  })
  const { data: supplierPos = [] } = useQuery<{ id: string; code: string; status: string }[]>({
    queryKey: ['pos-of-supplier', b?.supplierId],
    queryFn: async () => (await api.get(`/procurement/purchase-orders?supplierId=${b!.supplierId}&pageSize=100`)).data.data?.items ?? [],
    enabled: !!b?.supplierId,
  })

  const statusMut = useMutation({
    mutationFn: async (to: string) => api.post(`/procurement/receipts/${id}/status`, { status: to }),
    onSuccess: () => { setErr(null); invalidate() }, onError: onErr,
  })
  const headerMut = useMutation({
    mutationFn: async (body: any) => api.put(`/procurement/receipts/${id}`, body),
    onSuccess: () => { setErr(null); invalidate() }, onError: onErr,
  })
  const itemsMut = useMutation({
    mutationFn: async (items: any[]) => api.post(`/procurement/receipts/${id}/items`, { items }),
    onSuccess: () => { setErr(null); setNewItem({ desc: '', qty: '', price: '' }); invalidate() }, onError: onErr,
  })
  const delItemMut = useMutation({
    mutationFn: async (itemId: string) => api.delete(`/procurement/receipts/${id}/items/${itemId}`),
    onSuccess: () => { setErr(null); invalidate() }, onError: onErr,
  })
  const poMut = useMutation({
    mutationFn: async (v: { purchaseOrderIds: string[]; action: 'link' | 'unlink' }) =>
      api.post(`/procurement/receipts/${id}/purchase-orders`, v),
    onSuccess: () => { setErr(null); setPoToLink(''); invalidate(); qc.invalidateQueries({ queryKey: ['purchase-orders'] }) }, onError: onErr,
  })

  const [hdr, setHdr] = useState({ packageCount: '', deliveryNoteNumber: '', notes: '' })
  useEffect(() => { if (b) setHdr({ packageCount: b.packageCount?.toString() ?? '', deliveryNoteNumber: b.deliveryNoteNumber ?? '', notes: b.notes ?? '' }) }, [b])
  const [newItem, setNewItem] = useState({ desc: '', qty: '', price: '' })
  const [poToLink, setPoToLink] = useState('')

  if (isLoading || !b) return <PageSpinner />
  const st = RB_STATUS[b.status] ?? { label: b.status, variant: 'neutral' as const }
  const editable = b.status !== 'completed'
  const supplier = suppliers.find(s => s.id === b.supplierId)
  const linkedIds = new Set(b.purchaseOrders.map(p => p.id))
  const linkable = supplierPos.filter(p => !linkedIds.has(p.id) && p.status !== 'cancelled')
  const invoice = invoices.find(i => i.id === b.supplierInvoiceId)

  const saveHeader = () => headerMut.mutate({
    packageCount: hdr.packageCount ? parseInt(hdr.packageCount) : null,
    deliveryNoteNumber: hdr.deliveryNoteNumber || null,
    supplierInvoiceId: b.supplierInvoiceId, notes: hdr.notes || null,
  })

  return (
    <div className="p-6">
      <button className="flex items-center gap-1.5 text-sm mb-4" style={{ color: 'var(--text-s)' }}
        onClick={() => navigate('/procurement/receipts')}><ArrowLeft size={15} /> Mal Kabul</button>

      {/* Başlık */}
      <div className="card mb-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-lg font-bold font-mono" style={{ color: 'var(--text)' }}>{b.code}</h1>
              <Badge variant={st.variant}>{st.label}</Badge>
            </div>
            <p className="text-sm mt-1" style={{ color: 'var(--text-m)' }}>
              {supplier?.title ?? '—'} · {whName(warehouses.find(w => w.id === b.warehouseId))} · {new Date(b.receivedAt).toLocaleString('tr-TR')}
            </p>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            {b.status !== 'completed' && (
              <Button size="sm" onClick={() => { sessionStorage.setItem('sorting.batchId', b.id); navigate('/procurement/sorting') }}>
                Sayım / Teslim →
              </Button>
            )}
            {(NEXT[b.status] ?? []).map(a => (
              <Button key={a.to} size="sm" variant={a.variant ?? undefined} loading={statusMut.isPending}
                onClick={() => statusMut.mutate(a.to)}>{a.label}</Button>
            ))}
          </div>
        </div>
        {editable && (
          <div className="flex flex-wrap items-end gap-3 mt-3 pt-3" style={{ borderTop: '1px solid var(--border)' }}>
            <div className="w-28">
              <label className="flbl mb-1">Koli</label>
              <input type="number" min="0" className="inp" value={hdr.packageCount} onChange={e => setHdr(h => ({ ...h, packageCount: e.target.value }))} />
            </div>
            <div className="w-44">
              <label className="flbl mb-1">İrsaliye no</label>
              <input className="inp" value={hdr.deliveryNoteNumber} onChange={e => setHdr(h => ({ ...h, deliveryNoteNumber: e.target.value }))} />
            </div>
            <div className="flex-1 min-w-[200px]">
              <label className="flbl mb-1">Not</label>
              <input className="inp" value={hdr.notes} onChange={e => setHdr(h => ({ ...h, notes: e.target.value }))} />
            </div>
            <Button size="sm" variant="secondary" loading={headerMut.isPending} onClick={saveHeader}>Kaydet</Button>
          </div>
        )}
        {err && <p className="text-sm mt-2" style={{ color: '#ef4444' }}>{err}</p>}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* SA bağları */}
        <div className="card">
          <h2 className="text-sm font-semibold mb-1" style={{ color: 'var(--text)' }}>Bağlı Satın Almalar</h2>
          <p className="text-xs mb-3" style={{ color: 'var(--text-s)' }}>
            Gevşek bilgi bağı: "bu partide şu SA'lar var". Kalem eşleşmesi zorlanmaz; birden çok SA tek partide gelebilir.
          </p>
          {b.purchaseOrders.length === 0 && <p className="text-sm mb-2" style={{ color: 'var(--text-s)' }}>Bağlı satın alma yok.</p>}
          <ul className="space-y-1.5 mb-3">
            {b.purchaseOrders.map(p => {
              const pst = PO_STATUS[p.status] ?? { label: p.status, variant: 'neutral' as const }
              return (
                <li key={p.id} className="flex items-center justify-between gap-2 text-sm rounded-lg px-3 py-2" style={{ background: 'var(--surface2)' }}>
                  <span>
                    <Link to={`/procurement/purchase-orders/${p.id}`} className="font-mono text-xs underline" style={{ color: 'var(--brand)' }}>{p.code}</Link>
                    <Badge variant={pst.variant} className="ml-2">{pst.label}</Badge>
                    <span className="text-xs ml-2" style={{ color: 'var(--text-s)' }}>{p.itemCount} kalem · {p.totalQuantity} adet · {tl(p.totalAmount)} ₺</span>
                  </span>
                  <button className="p-1 rounded hover:opacity-70" title="Bağı çöz"
                    onClick={() => poMut.mutate({ purchaseOrderIds: [p.id], action: 'unlink' })}>
                    <X size={14} style={{ color: 'var(--text-s)' }} />
                  </button>
                </li>
              )
            })}
          </ul>
          <div className="flex items-end gap-2">
            <div className="flex-1">
              <label className="flbl mb-1">Satın alma bağla</label>
              <SearchableSelect value={poToLink} onChange={v => setPoToLink(v ?? '')}
                options={linkable.map(p => ({ value: p.id, label: `${p.code} (${PO_STATUS[p.status]?.label ?? p.status})` }))}
                placeholder={linkable.length ? 'SA seçin…' : 'Bağlanabilir SA yok'} hasValue={!!poToLink} />
            </div>
            <Button size="sm" variant="secondary" disabled={!poToLink} loading={poMut.isPending}
              onClick={() => poMut.mutate({ purchaseOrderIds: [poToLink], action: 'link' })}><Plus size={14} /> Bağla</Button>
          </div>
        </div>

        {/* Fatura bağı */}
        <div className="card">
          <h2 className="text-sm font-semibold mb-1" style={{ color: 'var(--text)' }}>Tedarikçi Faturası</h2>
          <p className="text-xs mb-3" style={{ color: 'var(--text-s)' }}>
            Opsiyonel gevşek bağ — dönemsel mutabakatta "fatura tutarı" sütununu besler.
          </p>
          {invoice ? (
            <div className="flex items-center justify-between text-sm rounded-lg px-3 py-2 mb-2" style={{ background: 'var(--surface2)' }}>
              <span style={{ color: 'var(--text)' }}>{invoice.invoiceNumber} · {new Date(invoice.invoiceDate).toLocaleDateString('tr-TR')} · <b>{tl(invoice.grandTotal)} ₺</b></span>
              {editable && (
                <button className="p-1 rounded hover:opacity-70" title="Bağı çöz"
                  onClick={() => headerMut.mutate({ packageCount: b.packageCount, deliveryNoteNumber: b.deliveryNoteNumber, supplierInvoiceId: null, notes: b.notes })}>
                  <X size={14} style={{ color: 'var(--text-s)' }} />
                </button>
              )}
            </div>
          ) : (
            <p className="text-sm mb-2" style={{ color: 'var(--text-s)' }}>Bağlı fatura yok.</p>
          )}
          {editable && !invoice && (
            <SearchableSelect value="" onChange={v => v && headerMut.mutate({ packageCount: b.packageCount, deliveryNoteNumber: b.deliveryNoteNumber, supplierInvoiceId: v, notes: b.notes })}
              options={invoices.map(i => ({ value: i.id, label: `${i.invoiceNumber} — ${tl(i.grandTotal)} ₺` }))}
              placeholder={invoices.length ? 'Fatura seçin…' : 'Bu tedarikçiye kayıtlı fatura yok'} hasValue={false} />
          )}
        </div>
      </div>

      {/* Kaba evrak kalemleri */}
      <div className="card p-0 overflow-x-auto mt-4">
        <div className="px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
          <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Evrak Kalemleri (kaba)</h2>
          <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
            Teslim evrakında yazan haliyle ("t-shirt, 1000 adet, 15 TL"). Opsiyoneldir; ayrıştırmayı kısıtlamaz.
          </p>
        </div>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs" style={{ color: 'var(--text-s)', background: 'var(--surface2)' }}>
              {['AÇIKLAMA', 'ADET', 'BİRİM FİYAT', 'TUTAR', ''].map(h => <th key={h} className="px-4 py-2.5 font-semibold">{h}</th>)}
            </tr>
          </thead>
          <tbody>
            {b.items.map(it => (
              <tr key={it.id} style={{ borderTop: '1px solid var(--border)' }}>
                <td className="px-4 py-2" style={{ color: 'var(--text)' }}>{it.descriptionText}</td>
                <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{it.quantity ?? '—'}</td>
                <td className="px-4 py-2 whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{it.unitPrice != null ? `${tl(it.unitPrice)} ₺` : '—'}</td>
                <td className="px-4 py-2 whitespace-nowrap" style={{ color: 'var(--text)' }}>{it.quantity != null && it.unitPrice != null ? `${tl(it.quantity * it.unitPrice)} ₺` : '—'}</td>
                <td className="px-4 py-2 text-right">
                  {editable && (
                    <button className="p-1 rounded hover:opacity-70" title="Kalemi sil" onClick={() => delItemMut.mutate(it.id)}>
                      <Trash2 size={14} style={{ color: 'var(--text-s)' }} />
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {b.items.length === 0 && (
              <tr><td colSpan={5} className="px-4 py-6 text-center text-sm" style={{ color: 'var(--text-s)' }}>Evrak kalemi yok (zorunlu değil).</td></tr>
            )}
            {editable && (
              <tr style={{ borderTop: '1px solid var(--border)', background: 'var(--surface2)' }}>
                <td className="px-2 py-2"><input className="inp" placeholder='örn. "t-shirt"' value={newItem.desc} onChange={e => setNewItem(r => ({ ...r, desc: e.target.value }))} /></td>
                <td className="px-2 py-2"><input className="inp w-24" placeholder="Adet" value={newItem.qty} onChange={e => setNewItem(r => ({ ...r, qty: e.target.value }))} /></td>
                <td className="px-2 py-2"><input className="inp w-28" placeholder="Fiyat" value={newItem.price} onChange={e => setNewItem(r => ({ ...r, price: e.target.value }))} /></td>
                <td colSpan={2} className="px-2 py-2">
                  <Button size="sm" loading={itemsMut.isPending} disabled={!newItem.desc.trim()}
                    onClick={() => itemsMut.mutate([{ descriptionText: newItem.desc, quantity: newItem.qty ? num(newItem.qty) : null, unitPrice: newItem.price ? num(newItem.price) : null }])}>
                    <Plus size={14} /> Ekle
                  </Button>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
