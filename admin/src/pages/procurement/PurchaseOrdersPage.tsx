/**
 * T1 Tedarik — Satın Almalar listesi (docs/urun-tedarik-is-akisi.md §3).
 * HAFİF kayıt katmanı: hiçbir akışı kilitlemez (İ2); kapanış elle (İ3/İ4).
 */
import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { PO_STATUS, apiErrorMessage, useSuppliers } from './procurementHelpers'

interface PoRow {
  id: string; code: string; supplierId: string; orderDate: string; expectedDate: string | null
  status: string; itemCount: number; totalQuantity: number; totalAmount: number; notes: string | null
}
interface Paged { items: PoRow[]; totalCount: number; page: number; pageSize: number }

const tl = (n: number) => n.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export function PurchaseOrdersPage() {
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (PurchaseOrderGrid.Schema) + Excel + görünümler.
  // Tedarikçi ADI Accounts modülünde → o kolon sıralanamaz; tedarikçi adlandırılmış süzgeçle filtrelenir.
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [sp] = useSearchParams()
  const status = sp.get('status') ?? ''
  const supplierId = sp.get('supplierId') ?? ''
  const grid = useGridState('purchase-orders', { defaultPageSize: 20, defaultSort: 'code', defaultDir: 'desc' })
  const setNamed = (k: string, v: string) => grid.mutate(n => { if (v) n.set(k, v); else n.delete(k) })
  const [createOpen, setCreateOpen] = useState(false)
  const [newSupplier, setNewSupplier] = useState('')
  const [newNotes, setNewNotes] = useState('')

  const { data: suppliers = [] } = useSuppliers()
  const supplierName = (id: string) => suppliers.find(s => s.id === id)?.title ?? '—'

  const named = () => ({ status: status || undefined, supplierId: supplierId || undefined })

  const { data, isLoading, isFetching, error: listError } = useQuery<Paged>({
    queryKey: ['purchase-orders', status, supplierId, ...grid.queryKey],
    queryFn: async () => (await api.get(`/procurement/purchase-orders?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const createMut = useMutation({
    mutationFn: async () => (await api.post('/procurement/purchase-orders', { supplierId: newSupplier, notes: newNotes || null })).data.data,
    onSuccess: (d: { id: string }) => { qc.invalidateQueries({ queryKey: ['purchase-orders'] }); setCreateOpen(false); navigate(`/procurement/purchase-orders/${d.id}`) },
  })

  const rows = data?.items ?? []

  const columns: GridColumn<PoRow>[] = [
    { key: 'code', header: 'KOD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 150,
      filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      filters: [{ field: 'notes', label: 'Not', type: 'text' }],
      cell: r => <span className="font-mono text-xs" style={{ color: 'var(--text)' }}>{r.code}</span> },
    // Tedarikçi adı Accounts modülünde çözülüyor → sıralama şemada YOK (bilinçli); süzgeç supplierId ile.
    { key: 'supplier', header: 'TEDARİKÇİ', priority: 1, frozen: true, minWidth: 200,
      filter: { type: 'enum', label: 'Tedarikçi', field: 'supplierId', options: suppliers.map(s => ({ value: s.id, label: s.title })) },
      cell: r => <span style={{ color: 'var(--text)' }}>{supplierName(r.supplierId)}</span> },
    { key: 'orderDate', header: 'TARİH', priority: 2, sortable: true, filter: { type: 'date', label: 'Sipariş tarihi', quick: true },
      cell: r => <span className="whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{new Date(r.orderDate).toLocaleDateString('tr-TR')}</span> },
    { key: 'expectedDate', header: 'BEKLENEN', priority: 2, sortable: true, filter: { type: 'date', label: 'Beklenen tarih' },
      filters: [{ field: 'overdue', label: 'Tarihi geçmiş (kapanmamış)', type: 'boolean' }],
      cell: r => <span className="whitespace-nowrap" style={{ color: 'var(--text-m)' }}>
        {r.expectedDate ? new Date(r.expectedDate).toLocaleDateString('tr-TR') : '—'}</span> },
    { key: 'itemCount', header: 'KALEM', priority: 2, align: 'right', sortable: true, filter: { type: 'number', label: 'Kalem sayısı' },
      cell: r => <span style={{ color: 'var(--text-m)' }}>{r.itemCount}</span> },
    { key: 'totalQuantity', header: 'ADET', priority: 2, align: 'right', sortable: true, filter: { type: 'number', label: 'Toplam adet' },
      cell: r => <span style={{ color: 'var(--text-m)' }}>{r.totalQuantity}</span> },
    { key: 'totalAmount', header: 'TUTAR', priority: 1, align: 'right', sortable: true, filter: { type: 'number', label: 'Toplam tutar' },
      cell: r => <span className="whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{tl(r.totalAmount)} ₺</span> },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(PO_STATUS).map(([value, v]) => ({ value, label: v.label })) },
      filters: [{ field: 'open', label: 'Açık (kapanmamış)', type: 'boolean' }, { field: 'createdAt', label: 'Oluşturma', type: 'date' }],
      cell: r => { const st = PO_STATUS[r.status] ?? { label: r.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> } },
  ]

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

      <DataGrid<PoRow>
        gridId="purchase-orders"
        views
        grid={grid}
        columns={columns}
        rows={rows}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={r => navigate(`/procurement/purchase-orders/${r.id}`)}
        empty="Henüz satın alma kaydı yok."
        search={{ placeholder: 'SA-… ya da model adı' }}
        minWidth={1040}
        filterLeading={
          <div style={{ minWidth: 200 }}>
            <SearchableSelect value={supplierId} onChange={v => setNamed('supplierId', v ?? '')}
              options={[{ value: '', label: 'Tedarikçi: Tümü' }, ...suppliers.map(s => ({ value: s.id, label: s.title }))]}
              placeholder="Tedarikçi: Tümü" hasValue={!!supplierId} />
          </div>
        }
        export={{ endpoint: '/procurement/purchase-orders/export', named, fallbackFileName: 'satin-alma.xlsx' }}
        compact={{
          title: r => r.code,
          subtitle: r => `${supplierName(r.supplierId)} · ${new Date(r.orderDate).toLocaleDateString('tr-TR')}`,
          right: r => `${tl(r.totalAmount)} ₺`,
          badge: r => { const st = PO_STATUS[r.status] ?? { label: r.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> },
        }}
      />

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
          {createMut.isError && <p className="text-sm" style={{ color: '#ef4444' }}>{apiErrorMessage(createMut.error, 'Oluşturulamadı.')}</p>}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={() => createMut.mutate()} loading={createMut.isPending} disabled={!newSupplier}>Oluştur</Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}
