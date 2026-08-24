/**
 * T1 Satın Alma detayı: başlık + durum aksiyonları + kalem tablosu (satır ekleme/düzenleme/silme)
 * + "Excel'den Yapıştır" (K4): panoya kopyalanan tablo → sütun eşleme → toplu kalem.
 */
import { useMemo, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ClipboardPaste, Plus, Trash2 } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { PO_STATUS, useSuppliers } from './PurchaseOrdersPage'

interface ItemDto { id: string; variantId: string | null; modelText: string | null; colorText: string | null; sizeText: string | null; quantity: number; unitPrice: number; total: number; notes: string | null; sortOrder: number }
interface DetailDto { id: string; code: string; supplierId: string; orderDate: string; expectedDate: string | null; status: string; notes: string | null; totalQuantity: number; totalAmount: number; items: ItemDto[] }

const NEXT: Record<string, { to: string; label: string; variant?: 'secondary' }[]> = {
  draft: [{ to: 'ordered', label: 'Sipariş Verildi' }, { to: 'cancelled', label: 'İptal Et', variant: 'secondary' }],
  ordered: [{ to: 'receiving', label: 'Teslim Alınıyor' }, { to: 'closed', label: 'Kapat' }, { to: 'cancelled', label: 'İptal Et', variant: 'secondary' }],
  receiving: [{ to: 'closed', label: 'Kapat' }, { to: 'ordered', label: 'Siparişe Geri Al', variant: 'secondary' }],
  closed: [{ to: 'receiving', label: 'Geri Aç', variant: 'secondary' }],
  cancelled: [],
}
const COLS = [
  { key: 'model', label: 'Model' }, { key: 'color', label: 'Renk' }, { key: 'size', label: 'Beden' },
  { key: 'quantity', label: 'Adet' }, { key: 'price', label: 'Fiyat' }, { key: 'notes', label: 'Not' }, { key: 'skip', label: '— Yoksay —' },
] as const
const tl = (n: number) => n.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const num = (s: string) => { const v = parseFloat(s.replace(/\./g, '').replace(',', '.')); return isNaN(v) ? parseFloat(s.replace(',', '.')) : v }

export function PurchaseOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { data: suppliers = [] } = useSuppliers()
  const [err, setErr] = useState<string | null>(null)

  const { data: po, isLoading } = useQuery<DetailDto>({
    queryKey: ['purchase-order', id],
    queryFn: async () => (await api.get(`/procurement/purchase-orders/${id}`)).data.data,
    enabled: !!id,
  })
  const invalidate = () => { qc.invalidateQueries({ queryKey: ['purchase-order', id] }); qc.invalidateQueries({ queryKey: ['purchase-orders'] }) }
  const onErr = (e: any) => setErr(e?.response?.data?.error ?? 'İşlem başarısız.')

  const statusMut = useMutation({
    mutationFn: async (to: string) => api.post(`/procurement/purchase-orders/${id}/status`, { status: to }),
    onSuccess: () => { setErr(null); invalidate() }, onError: onErr,
  })
  const itemsMut = useMutation({
    mutationFn: async (items: any[]) => api.post(`/procurement/purchase-orders/${id}/items`, { items }),
    onSuccess: () => { setErr(null); setNewRow({ model: '', color: '', size: '', qty: '', price: '' }); setPasteOpen(false); invalidate() },
    onError: onErr,
  })
  const delMut = useMutation({
    mutationFn: async (itemId: string) => api.delete(`/procurement/purchase-orders/${id}/items/${itemId}`),
    onSuccess: () => { setErr(null); invalidate() }, onError: onErr,
  })

  // satır ekleme formu
  const [newRow, setNewRow] = useState({ model: '', color: '', size: '', qty: '', price: '' })
  // yapıştırma modalı
  const [pasteOpen, setPasteOpen] = useState(false)
  const [pasteText, setPasteText] = useState('')
  const [mapping, setMapping] = useState<string[]>([])
  const parsed = useMemo(() => pasteText.trim().split(/\r?\n/).filter(l => l.trim()).map(l => l.split('\t').map(c => c.trim())), [pasteText])
  const colCount = parsed[0]?.length ?? 0
  const effMapping = useMemo(() => {
    const defaults = ['model', 'color', 'size', 'quantity', 'price', 'notes']
    return Array.from({ length: colCount }, (_, i) => mapping[i] ?? defaults[i] ?? 'skip')
  }, [colCount, mapping])
  const pasteItems = useMemo(() => parsed.map(cells => {
    const it: any = { quantity: 0, unitPrice: 0 }
    cells.forEach((c, i) => {
      const m = effMapping[i]
      if (m === 'model') it.modelText = c
      else if (m === 'color') it.colorText = c
      else if (m === 'size') it.sizeText = c
      else if (m === 'notes') it.notes = c
      else if (m === 'quantity') it.quantity = num(c) || 0
      else if (m === 'price') it.unitPrice = num(c) || 0
    })
    return it
  }), [parsed, effMapping])
  const pasteValid = pasteItems.length > 0 && pasteItems.every(it => it.quantity > 0 && (it.modelText || it.colorText || it.sizeText))

  if (isLoading || !po) return <PageSpinner />
  const st = PO_STATUS[po.status] ?? { label: po.status, variant: 'neutral' as const }
  const editable = po.status !== 'closed' && po.status !== 'cancelled'
  const supplier = suppliers.find(s => s.id === po.supplierId)

  return (
    <div className="p-6">
      <button className="flex items-center gap-1.5 text-sm mb-4" style={{ color: 'var(--text-s)' }}
        onClick={() => navigate('/procurement/purchase-orders')}><ArrowLeft size={15} /> Satın Almalar</button>

      <div className="card mb-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-lg font-bold font-mono" style={{ color: 'var(--text)' }}>{po.code}</h1>
              <Badge variant={st.variant}>{st.label}</Badge>
            </div>
            <p className="text-sm mt-1" style={{ color: 'var(--text-m)' }}>
              {supplier?.title ?? '—'} · {new Date(po.orderDate).toLocaleDateString('tr-TR')}
              {po.expectedDate ? ` · beklenen: ${new Date(po.expectedDate).toLocaleDateString('tr-TR')}` : ''}
            </p>
            {po.notes && <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>{po.notes}</p>}
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-sm mr-2" style={{ color: 'var(--text-m)' }}>
              <b>{po.items.length}</b> kalem · <b>{po.totalQuantity}</b> adet · <b>{tl(po.totalAmount)} ₺</b>
            </span>
            {(NEXT[po.status] ?? []).map(a => (
              <Button key={a.to} size="sm" variant={a.variant ?? undefined} loading={statusMut.isPending}
                onClick={() => statusMut.mutate(a.to)}>{a.label}</Button>
            ))}
          </div>
        </div>
        {err && <p className="text-sm mt-2" style={{ color: '#ef4444' }}>{err}</p>}
      </div>

      <div className="card p-0 overflow-x-auto">
        <div className="flex items-center justify-between px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
          <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Kalemler</h2>
          {editable && (
            <Button size="sm" variant="secondary" onClick={() => { setPasteText(''); setMapping([]); setPasteOpen(true) }}>
              <ClipboardPaste size={14} /> Excel'den Yapıştır
            </Button>
          )}
        </div>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs" style={{ color: 'var(--text-s)', background: 'var(--surface2)' }}>
              {['MODEL', 'RENK', 'BEDEN', 'ADET', 'BİRİM FİYAT', 'TUTAR', 'NOT', ''].map(h => <th key={h} className="px-4 py-2.5 font-semibold">{h}</th>)}
            </tr>
          </thead>
          <tbody>
            {po.items.map(it => (
              <tr key={it.id} style={{ borderTop: '1px solid var(--border)' }}>
                <td className="px-4 py-2" style={{ color: 'var(--text)' }}>{it.modelText ?? '—'}</td>
                <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{it.colorText ?? '—'}</td>
                <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{it.sizeText ?? '—'}</td>
                <td className="px-4 py-2" style={{ color: 'var(--text-m)' }}>{it.quantity}</td>
                <td className="px-4 py-2 whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{tl(it.unitPrice)} ₺</td>
                <td className="px-4 py-2 whitespace-nowrap" style={{ color: 'var(--text)' }}>{tl(it.total)} ₺</td>
                <td className="px-4 py-2 text-xs" style={{ color: 'var(--text-s)' }}>{it.notes ?? ''}</td>
                <td className="px-4 py-2 text-right">
                  {editable && (
                    <button className="p-1 rounded hover:opacity-70" title="Kalemi sil" onClick={() => delMut.mutate(it.id)}>
                      <Trash2 size={14} style={{ color: 'var(--text-s)' }} />
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {po.items.length === 0 && (
              <tr><td colSpan={8} className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Henüz kalem yok — satır ekleyin ya da Excel'den yapıştırın.</td></tr>
            )}
            {editable && (
              <tr style={{ borderTop: '1px solid var(--border)', background: 'var(--surface2)' }}>
                <td className="px-2 py-2"><input className="inp" placeholder="Model" value={newRow.model} onChange={e => setNewRow(r => ({ ...r, model: e.target.value }))} /></td>
                <td className="px-2 py-2"><input className="inp" placeholder="Renk" value={newRow.color} onChange={e => setNewRow(r => ({ ...r, color: e.target.value }))} /></td>
                <td className="px-2 py-2"><input className="inp" placeholder="Beden" value={newRow.size} onChange={e => setNewRow(r => ({ ...r, size: e.target.value }))} /></td>
                <td className="px-2 py-2"><input className="inp w-20" placeholder="Adet" value={newRow.qty} onChange={e => setNewRow(r => ({ ...r, qty: e.target.value }))} /></td>
                <td className="px-2 py-2"><input className="inp w-28" placeholder="Fiyat" value={newRow.price} onChange={e => setNewRow(r => ({ ...r, price: e.target.value }))} /></td>
                <td colSpan={3} className="px-2 py-2">
                  <Button size="sm" loading={itemsMut.isPending}
                    disabled={!(num(newRow.qty) > 0) || !(newRow.model || newRow.color || newRow.size)}
                    onClick={() => itemsMut.mutate([{ modelText: newRow.model || null, colorText: newRow.color || null, sizeText: newRow.size || null, quantity: num(newRow.qty) || 0, unitPrice: num(newRow.price) || 0 }])}>
                    <Plus size={14} /> Ekle
                  </Button>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Modal open={pasteOpen} onClose={() => setPasteOpen(false)} title="Excel'den Yapıştır">
        <div className="space-y-3">
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>
            Excel'de model/renk/beden/adet/fiyat sütunlarını kopyalayıp aşağıya yapıştırın; sütunların ne olduğunu
            başlıklardan eşleyin. Adet zorunludur; model/renk/beden'den en az biri dolu olmalıdır.
          </p>
          <textarea className="inp w-full h-32 font-mono text-xs" value={pasteText} onChange={e => setPasteText(e.target.value)}
            placeholder={'Basic Tshirt\tSiyah\tM\t50\t120,00\nBasic Tshirt\tSiyah\tL\t40\t120,00'} />
          {colCount > 0 && (
            <div className="overflow-x-auto rounded-lg" style={{ border: '1px solid var(--border)' }}>
              <table className="w-full text-xs">
                <thead>
                  <tr style={{ background: 'var(--surface2)' }}>
                    {Array.from({ length: colCount }, (_, i) => (
                      <th key={i} className="px-2 py-1.5">
                        <select className="inp !py-1 text-xs" value={effMapping[i]}
                          onChange={e => setMapping(() => { const n = [...effMapping]; n[i] = e.target.value; return n })}>
                          {COLS.map(c => <option key={c.key} value={c.key}>{c.label}</option>)}
                        </select>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {parsed.slice(0, 5).map((cells, ri) => (
                    <tr key={ri} style={{ borderTop: '1px solid var(--border)' }}>
                      {Array.from({ length: colCount }, (_, ci) => (
                        <td key={ci} className="px-2 py-1" style={{ color: 'var(--text-m)' }}>{cells[ci] ?? ''}</td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
              {parsed.length > 5 && <p className="px-2 py-1 text-xs" style={{ color: 'var(--text-s)' }}>… toplam {parsed.length} satır</p>}
            </div>
          )}
          {colCount > 0 && !pasteValid && <p className="text-xs" style={{ color: '#ef4444' }}>Her satırda adet &gt; 0 ve en az bir kimlik alanı (model/renk/beden) olmalı.</p>}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setPasteOpen(false)}>Vazgeç</Button>
            <Button loading={itemsMut.isPending} disabled={!pasteValid}
              onClick={() => itemsMut.mutate(pasteItems)}>{parsed.length} kalemi ekle</Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}
