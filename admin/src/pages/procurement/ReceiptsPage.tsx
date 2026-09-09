/**
 * T2 Tedarik — Mal Kabul partileri (docs/urun-tedarik-is-akisi.md §2.2).
 * Parti = "koli geldi" kaydı: kalemsiz açılır (İ2), ayrıştırma hemen başlayabilir.
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
import { RB_STATUS, apiErrorMessage, useSuppliers, useWarehouses, whName } from './procurementHelpers'

interface RbRow {
  id: string; code: string; supplierId: string; warehouseId: string; receivedAt: string
  packageCount: number | null; deliveryNoteNumber: string | null; status: string
  itemCount: number; linkedPoCount: number; hasInvoice: boolean; notes: string | null
}
interface Paged { items: RbRow[]; totalCount: number; page: number; pageSize: number }

export function ReceiptsPage() {
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (ReceiptBatchGrid.Schema) + Excel + görünümler.
  // Tedarikçi/depo ADLARI başka modüllerde çözülüyor → o kolonlar sıralanamaz; süzgeçleri kimlikle.
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [sp] = useSearchParams()
  const status = sp.get('status') ?? ''
  const supplierId = sp.get('supplierId') ?? ''
  const grid = useGridState('receipts', { defaultPageSize: 20, defaultSort: 'code', defaultDir: 'desc' })
  const setNamed = (k: string, v: string) => grid.mutate(n => { if (v) n.set(k, v); else n.delete(k) })
  const [createOpen, setCreateOpen] = useState(false)
  const [form, setForm] = useState({ supplierId: '', warehouseId: '', packageCount: '', deliveryNoteNumber: '', notes: '' })

  const { data: suppliers = [] } = useSuppliers()
  const { data: warehouses = [] } = useWarehouses()
  const supplierName = (id: string) => suppliers.find(s => s.id === id)?.title ?? '—'

  const named = () => ({ status: status || undefined, supplierId: supplierId || undefined })

  const { data, isLoading, isFetching, error: listError } = useQuery<Paged>({
    queryKey: ['receipt-batches', status, supplierId, ...grid.queryKey],
    queryFn: async () => (await api.get(`/procurement/receipts?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
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

  const columns: GridColumn<RbRow>[] = [
    { key: 'code', header: 'KOD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 150,
      filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      filters: [{ field: 'notes', label: 'Not', type: 'text' }],
      cell: r => <span className="font-mono text-xs" style={{ color: 'var(--text)' }}>{r.code}</span> },
    // Tedarikçi adı Accounts modülünde → sıralama YOK; süzgeç adlandırılmış supplierId ile.
    { key: 'supplier', header: 'TEDARİKÇİ', priority: 1, frozen: true, minWidth: 200,
      filter: { type: 'enum', label: 'Tedarikçi', field: 'supplierId', options: suppliers.map(s => ({ value: s.id, label: s.title })) },
      cell: r => <span style={{ color: 'var(--text)' }}>{supplierName(r.supplierId)}</span> },
    // Depo adı Inventory modülünde → sıralama YOK; süzgeç warehouseId ile.
    { key: 'warehouse', header: 'DEPO', priority: 2, minWidth: 160,
      filter: { type: 'enum', label: 'Depo', field: 'warehouseId', options: warehouses.map(w => ({ value: w.id, label: whName(w) })) },
      cell: r => <span style={{ color: 'var(--text-m)' }}>{whName(warehouses.find(w => w.id === r.warehouseId))}</span> },
    { key: 'receivedAt', header: 'TARİH', priority: 1, sortable: true, filter: { type: 'date', label: 'Teslim tarihi', quick: true },
      cell: r => <span className="whitespace-nowrap" style={{ color: 'var(--text-m)' }}>{new Date(r.receivedAt).toLocaleDateString('tr-TR')}</span> },
    { key: 'packageCount', header: 'KOLİ', priority: 2, align: 'right', sortable: true, filter: { type: 'number', label: 'Koli sayısı' },
      filters: [{ field: 'itemCount', label: 'Kalem sayısı', type: 'number' }],
      cell: r => <span style={{ color: 'var(--text-m)' }}>{r.packageCount ?? '—'}</span> },
    { key: 'deliveryNoteNumber', header: 'İRSALİYE', priority: 2, sortable: true, filter: { type: 'text', label: 'İrsaliye no' },
      cell: r => <span style={{ color: 'var(--text-m)' }}>{r.deliveryNoteNumber ?? '—'}</span> },
    { key: 'linkedPoCount', header: 'SA BAĞI', priority: 3, align: 'right', sortable: true, filter: { type: 'number', label: 'Bağlı satın alma' },
      cell: r => <span style={{ color: 'var(--text-m)' }}>{r.linkedPoCount > 0 ? `${r.linkedPoCount} SA` : '—'}</span> },
    { key: 'hasInvoice', header: 'FATURA', priority: 3, align: 'center', sortable: true, filter: { type: 'boolean', label: 'Faturalı' },
      cell: r => <span style={{ color: 'var(--text-m)' }}>{r.hasInvoice ? '✓' : '—'}</span> },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(RB_STATUS).map(([value, v]) => ({ value, label: v.label })) },
      filters: [{ field: 'open', label: 'Açık (tamamlanmamış)', type: 'boolean' }],
      cell: r => { const st = RB_STATUS[r.status] ?? { label: r.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> } },
  ]

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

      <DataGrid<RbRow>
        gridId="receipts"
        views
        grid={grid}
        columns={columns}
        rows={rows}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={r => navigate(`/procurement/receipts/${r.id}`)}
        empty="Henüz mal kabul partisi yok."
        search={{ placeholder: 'MK-… ya da irsaliye no' }}
        minWidth={1120}
        filterLeading={
          <div style={{ minWidth: 200 }}>
            <SearchableSelect value={supplierId} onChange={v => setNamed('supplierId', v ?? '')}
              options={[{ value: '', label: 'Tedarikçi: Tümü' }, ...suppliers.map(s => ({ value: s.id, label: s.title }))]}
              placeholder="Tedarikçi: Tümü" hasValue={!!supplierId} />
          </div>
        }
        export={{ endpoint: '/procurement/receipts/export', named, fallbackFileName: 'mal-kabul.xlsx' }}
        compact={{
          title: r => r.code,
          subtitle: r => `${supplierName(r.supplierId)} · ${new Date(r.receivedAt).toLocaleDateString('tr-TR')}`,
          right: r => r.packageCount ? `${r.packageCount} koli` : '',
          badge: r => { const st = RB_STATUS[r.status] ?? { label: r.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> },
        }}
      />

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
          {createMut.isError && <p className="text-sm" style={{ color: '#ef4444' }}>{apiErrorMessage(createMut.error, 'Oluşturulamadı.')}</p>}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={() => createMut.mutate()} loading={createMut.isPending} disabled={!form.supplierId || !form.warehouseId}>Oluştur</Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}
