/**
 * T1 Tedarik — Satın Almalar listesi (docs/urun-tedarik-is-akisi.md §3).
 * HAFİF kayıt katmanı: hiçbir akışı kilitlemez (İ2); kapanış elle (İ3/İ4).
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

export interface SupplierOpt { id: string; title: string; code: string }

export const PO_STATUS: Record<string, { label: string; variant: 'success' | 'info' | 'warning' | 'danger' | 'neutral' }> = {
  draft:     { label: 'Taslak',        variant: 'neutral' },
  ordered:   { label: 'Sipariş Verildi', variant: 'info' },
  receiving: { label: 'Teslim Alınıyor', variant: 'warning' },
  closed:    { label: 'Kapandı',       variant: 'success' },
  cancelled: { label: 'İptal',         variant: 'danger' },
}

interface PoRow {
  id: string; code: string; supplierId: string; orderDate: string; expectedDate: string | null
  status: string; itemCount: number; totalQuantity: number; totalAmount: number; notes: string | null
}
interface Paged { items: PoRow[]; totalCount: number; page: number; pageSize: number }

export function useSuppliers() {
  return useQuery<SupplierOpt[]>({
    queryKey: ['suppliers-simple'],
    queryFn: async () => {
      const { data } = await api.get('/accounts?accountType=supplier&isActive=true&pageSize=500')
      const items = data.data?.items ?? data.data ?? []
      return items.map((a: any) => ({ id: a.id, title: a.title, code: a.code }))
    },
    staleTime: 60_000,
  })
}

const PAGE_SIZE = 20
const tl = (n: number) => n.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export function PurchaseOrdersPage() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [status, setStatus] = useState('')
  const [supplierId, setSupplierId] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [createOpen, setCreateOpen] = useState(false)
  const [newSupplier, setNewSupplier] = useState('')
  const [newNotes, setNewNotes] = useState('')

  const { data: suppliers = [] } = useSuppliers()
  const supplierName = (id: string) => suppliers.find(s => s.id === id)?.title ?? '—'

  const { data, isLoading } = useQuery<Paged>({
    queryKey: ['purchase-orders', status, supplierId, search, page],
    queryFn: async () => {
      const p = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) })
      if (status) p.set('status', status)
      if (supplierId) p.set('supplierId', supplierId)
      if (search) p.set('search', search)
      return (await api.get(`/procurement/purchase-orders?${p}`)).data.data
    },
  })

  const createMut = useMutation({
    mutationFn: async () => (await api.post('/procurement/purchase-orders', { supplierId: newSupplier, notes: newNotes || null })).data.data,
    onSuccess: (d: { id: string }) => { qc.invalidateQueries({ queryKey: ['purchase-orders'] }); setCreateOpen(false); navigate(`/procurement/purchase-orders/${d.id}`) },
  })

  const rows = data?.items ?? []
  const totalPages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / PAGE_SIZE))
  if (isLoading && !data) return <PageSpinner />

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Satın Almalar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Tedarikçilere verilen satın alma listeleri (model/renk/beden/adet/fiyat). Kayıt katmanıdır:
            mal kabul ve ayrıştırma bu kayıtlar olmadan da yürür; kapanış elle verilir.
          </p>
        </div>
        <Button size="sm" onClick={() => { setNewSupplier(''); setNewNotes(''); setCreateOpen(true) }}><Plus size={14} /> Yeni Satın Alma</Button>
      </div>

      <div className="card mb-4 flex flex-wrap items-end gap-3">
        <div className="flex-1 min-w-[220px]">
          <label className="flbl mb-1.5">Ara (kod / model)</label>
          <div className="flex gap-2">
            <input className="inp flex-1" value={searchInput} onChange={e => setSearchInput(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && (setPage(1), setSearch(searchInput.trim()))} placeholder="SA-… ya da model adı" />
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
            {Object.entries(PO_STATUS).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
          </select>
        </div>
      </div>

      <div className="card p-0 overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'TEDARİKÇİ', 'TARİH', 'BEKLENEN', 'KALEM', 'ADET', 'TUTAR', 'DURUM'].map(h =>
                <th key={h} className="px-4 py-3 font-semibold">{h}</th>)}
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && (
              <tr><td colSpan={8} className="px-4 py-10 text-center" style={{ color: 'var(--text-s)' }}>
                {search || status || supplierId ? 'Filtreye uyan kayıt yok.' : 'Henüz satın alma kaydı yok.'}
              </td></tr>
            )}
            {rows.map(r => {
              const st = PO_STATUS[r.status] ?? { label: r.status, variant: 'neutral' as const }
              return (
                <tr key={r.id} className="cursor-pointer hover:opacity-90" style={{ borderBottom: '1px solid var(--border)' }}
                  onClick={() => navigate(`/procurement/purchase-orders/${r.id}`)}>
                  <td className="px-4 py-2.5 font-mono text-xs" style={{ color: 'var(--text)' }}>{r.code}</td>
                  <td className="px-4 py-2.5" style={{ color: 'var(--text)' }}>{supplierName(r.supplierId)}</td>
                  <td className="px-4 py-2.5 whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{new Date(r.orderDate).toLocaleDateString('tr-TR')}</td>
                  <td className="px-4 py-2.5 whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{r.expectedDate ? new Date(r.expectedDate).toLocaleDateString('tr-TR') : '—'}</td>
                  <td className="px-4 py-2.5" style={{ color: 'var(--text-m)' }}>{r.itemCount}</td>
                  <td className="px-4 py-2.5" style={{ color: 'var(--text-m)' }}>{r.totalQuantity}</td>
                  <td className="px-4 py-2.5 whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{tl(r.totalAmount)} ₺</td>
                  <td className="px-4 py-2.5"><Badge variant={st.variant}>{st.label}</Badge></td>
                </tr>
              )
            })}
          </tbody>
        </table>
        <Pagination page={page} totalPages={totalPages} totalCount={data?.totalCount ?? 0} pageSize={PAGE_SIZE} onChange={setPage} />
      </div>

      <Modal open={createOpen} onClose={() => setCreateOpen(false)} title="Yeni Satın Alma">
        <div className="space-y-4">
          <div>
            <label className="flbl mb-1.5">Tedarikçi</label>
            <SearchableSelect value={newSupplier} onChange={v => setNewSupplier(v ?? '')}
              options={suppliers.map(s => ({ value: s.id, label: `${s.title} (${s.code})` }))}
              placeholder="Tedarikçi seçin…" hasValue={!!newSupplier} />
          </div>
          <div>
            <label className="flbl mb-1.5">Not (opsiyonel)</label>
            <input className="inp" value={newNotes} onChange={e => setNewNotes(e.target.value)} placeholder="örn. yaz sezonu ilk parti" />
          </div>
          {createMut.isError && <p className="text-sm" style={{ color: '#ef4444' }}>{(createMut.error as any)?.response?.data?.error ?? 'Oluşturulamadı.'}</p>}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={() => createMut.mutate()} loading={createMut.isPending} disabled={!newSupplier}>Oluştur</Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}
