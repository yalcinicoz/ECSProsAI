/**
 * T2 Tedarik — Mal Kabul partileri (docs/urun-tedarik-is-akisi.md §2.2).
 * Parti = "koli geldi" kaydı: kalemsiz açılır (İ2), ayrıştırma hemen başlayabilir.
 */
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Search } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { Pagination } from '@/components/ui/Pagination'
import { PageSpinner } from '@/components/ui/Spinner'
import { useSuppliers } from './PurchaseOrdersPage'

export const RB_STATUS: Record<string, { label: string; variant: 'success' | 'info' | 'warning' | 'neutral' }> = {
  received:  { label: 'Teslim Alındı', variant: 'info' },
  sorting:   { label: 'Ayrıştırılıyor', variant: 'warning' },
  completed: { label: 'Tamamlandı', variant: 'success' },
}

export interface WarehouseOpt { id: string; nameI18n: Record<string, string>; code: string }
export function useWarehouses() {
  return useQuery<WarehouseOpt[]>({
    queryKey: ['warehouses-simple'],
    queryFn: async () => {
      const { data } = await api.get('/inventory/warehouses')
      return (data.data ?? []).filter((w: any) => w.isActive !== false)
    },
    staleTime: 60_000,
  })
}
export const whName = (w?: WarehouseOpt) => w ? (w.nameI18n?.['tr'] ?? w.code) : '—'

interface RbRow {
  id: string; code: string; supplierId: string; warehouseId: string; receivedAt: string
  packageCount: number | null; deliveryNoteNumber: string | null; status: string
  itemCount: number; linkedPoCount: number; hasInvoice: boolean; notes: string | null
}
interface Paged { items: RbRow[]; totalCount: number; page: number; pageSize: number }
const PAGE_SIZE = 20

export function ReceiptsPage() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [status, setStatus] = useState('')
  const [supplierId, setSupplierId] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [createOpen, setCreateOpen] = useState(false)
  const [form, setForm] = useState({ supplierId: '', warehouseId: '', packageCount: '', deliveryNoteNumber: '', notes: '' })

  const { data: suppliers = [] } = useSuppliers()
  const { data: warehouses = [] } = useWarehouses()
  const supplierName = (id: string) => suppliers.find(s => s.id === id)?.title ?? '—'

  const { data, isLoading } = useQuery<Paged>({
    queryKey: ['receipt-batches', status, supplierId, search, page],
    queryFn: async () => {
      const p = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) })
      if (status) p.set('status', status)
      if (supplierId) p.set('supplierId', supplierId)
      if (search) p.set('search', search)
      return (await api.get(`/procurement/receipts?${p}`)).data.data
    },
  })

  const createMut = useMutation({
    mutationFn: async () => (await api.post('/procurement/receipts', {
      supplierId: form.supplierId, warehouseId: form.warehouseId,
      packageCount: form.packageCount ? parseInt(form.packageCount) : null,
      deliveryNoteNumber: form.deliveryNoteNumber || null, notes: form.notes || null,
    })).data.data,
    onSuccess: (d: { id: string }) => { qc.invalidateQueries({ queryKey: ['receipt-batches'] }); setCreateOpen(false); navigate(`/procurement/receipts/${d.id}`) },
  })

  const rows = data?.items ?? []
  const totalPages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / PAGE_SIZE))
  if (isLoading && !data) return <PageSpinner />

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Mal Kabul</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Gelen koliler parti olarak kaydedilir — kalem bilgisi zorunlu değildir; ayrıştırma hemen başlayabilir.
            Evrak kalemleri ve satın alma bağları yalnız dönemsel mutabakat raporuna girdidir.
          </p>
        </div>
        <Button size="sm" onClick={() => { setForm({ supplierId: '', warehouseId: warehouses[0]?.id ?? '', packageCount: '', deliveryNoteNumber: '', notes: '' }); setCreateOpen(true) }}>
          <Plus size={14} /> Yeni Parti
        </Button>
      </div>

      <div className="card mb-4 flex flex-wrap items-end gap-3">
        <div className="flex-1 min-w-[220px]">
          <label className="flbl mb-1.5">Ara (kod / irsaliye no)</label>
          <div className="flex gap-2">
            <input className="inp flex-1" value={searchInput} onChange={e => setSearchInput(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && (setPage(1), setSearch(searchInput.trim()))} placeholder="MK-… ya da irsaliye no" />
            <Button variant="secondary" onClick={() => { setPage(1); setSearch(searchInput.trim()) }}><Search size={14} /> Ara</Button>
          </div>
        </div>
        <div className="min-w-[220px]">
          <label className="flbl mb-1.5">Tedarikçi</label>
          <SearchableSelect value={supplierId} onChange={v => { setPage(1); setSupplierId(v ?? '') }}
            options={[{ value: '', label: 'Tümü' }, ...suppliers.map(s => ({ value: s.id, label: s.title }))]}
            placeholder="Tümü" hasValue={!!supplierId} />
        </div>
        <div className="min-w-[170px]">
          <label className="flbl mb-1.5">Durum</label>
          <select className="inp" value={status} onChange={e => { setPage(1); setStatus(e.target.value) }}>
            <option value="">Tümü</option>
            {Object.entries(RB_STATUS).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
          </select>
        </div>
      </div>

      <div className="card p-0 overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'TEDARİKÇİ', 'DEPO', 'TARİH', 'KOLİ', 'İRSALİYE', 'SA BAĞI', 'FATURA', 'DURUM'].map(h =>
                <th key={h} className="px-4 py-3 font-semibold">{h}</th>)}
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && (
              <tr><td colSpan={9} className="px-4 py-10 text-center" style={{ color: 'var(--text-s)' }}>
                {search || status || supplierId ? 'Filtreye uyan kayıt yok.' : 'Henüz mal kabul partisi yok.'}
              </td></tr>
            )}
            {rows.map(r => {
              const st = RB_STATUS[r.status] ?? { label: r.status, variant: 'neutral' as const }
              return (
                <tr key={r.id} className="cursor-pointer hover:opacity-90" style={{ borderBottom: '1px solid var(--border)' }}
                  onClick={() => navigate(`/procurement/receipts/${r.id}`)}>
                  <td className="px-4 py-2.5 font-mono text-xs" style={{ color: 'var(--text)' }}>{r.code}</td>
                  <td className="px-4 py-2.5" style={{ color: 'var(--text)' }}>{supplierName(r.supplierId)}</td>
                  <td className="px-4 py-2.5" style={{ color: 'var(--text-m)' }}>{whName(warehouses.find(w => w.id === r.warehouseId))}</td>
                  <td className="px-4 py-2.5 whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{new Date(r.receivedAt).toLocaleDateString('tr-TR')}</td>
                  <td className="px-4 py-2.5" style={{ color: 'var(--text-m)' }}>{r.packageCount ?? '—'}</td>
                  <td className="px-4 py-2.5" style={{ color: 'var(--text-m)' }}>{r.deliveryNoteNumber ?? '—'}</td>
                  <td className="px-4 py-2.5" style={{ color: 'var(--text-m)' }}>{r.linkedPoCount > 0 ? `${r.linkedPoCount} SA` : '—'}</td>
                  <td className="px-4 py-2.5" style={{ color: 'var(--text-m)' }}>{r.hasInvoice ? '✓' : '—'}</td>
                  <td className="px-4 py-2.5"><Badge variant={st.variant}>{st.label}</Badge></td>
                </tr>
              )
            })}
          </tbody>
        </table>
        <Pagination page={page} totalPages={totalPages} totalCount={data?.totalCount ?? 0} pageSize={PAGE_SIZE} onChange={setPage} />
      </div>

      <Modal open={createOpen} onClose={() => setCreateOpen(false)} title="Yeni Mal Kabul Partisi">
        <div className="space-y-4">
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>
            Yalnız tedarikçi ve depo zorunludur — koli geldi, kayıt açılır; ayrıntılar sonra eklenebilir.
          </p>
          <div>
            <label className="flbl mb-1.5">Tedarikçi</label>
            <SearchableSelect value={form.supplierId} onChange={v => setForm(f => ({ ...f, supplierId: v ?? '' }))}
              options={suppliers.map(s => ({ value: s.id, label: `${s.title} (${s.code})` }))} placeholder="Tedarikçi seçin…" hasValue={!!form.supplierId} />
          </div>
          <div>
            <label className="flbl mb-1.5">Depo</label>
            <SearchableSelect value={form.warehouseId} onChange={v => setForm(f => ({ ...f, warehouseId: v ?? '' }))}
              options={warehouses.map(w => ({ value: w.id, label: whName(w) }))} placeholder="Depo seçin…" hasValue={!!form.warehouseId} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl mb-1.5">Koli sayısı (ops.)</label>
              <input type="number" min="0" className="inp" value={form.packageCount} onChange={e => setForm(f => ({ ...f, packageCount: e.target.value }))} />
            </div>
            <div>
              <label className="flbl mb-1.5">İrsaliye no (ops.)</label>
              <input className="inp" value={form.deliveryNoteNumber} onChange={e => setForm(f => ({ ...f, deliveryNoteNumber: e.target.value }))} />
            </div>
          </div>
          <div>
            <label className="flbl mb-1.5">Not (ops.)</label>
            <input className="inp" value={form.notes} onChange={e => setForm(f => ({ ...f, notes: e.target.value }))} />
          </div>
          {createMut.isError && <p className="text-sm" style={{ color: '#ef4444' }}>{(createMut.error as any)?.response?.data?.error ?? 'Oluşturulamadı.'}</p>}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={() => createMut.mutate()} loading={createMut.isPending} disabled={!form.supplierId || !form.warehouseId}>Oluştur</Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}
