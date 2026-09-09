import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { PermissionGuard } from '@/components/ui/PermissionGuard'
import type { Warehouse } from './WarehousesPage'
import { getWarehouseName } from './warehouseHelpers'
import { STATUS_MAP, TRANSFER_TYPES } from './transferConstants'

const PERM = 'inventory.manage'

export interface TransferSummary {
  id: string
  code: string
  fromWarehouseId: string
  fromWarehouseCode: string
  toWarehouseId: string
  toWarehouseCode: string
  transferType: string
  status: string
  itemCount: number
  requestedAt: string
  createdAt: string
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

type CreateForm = {
  fromWarehouseId: string
  toWarehouseId: string
  transferType: string
  notes: string
}

export function TransfersPage() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (TransferGrid.Schema) + Excel + görünümler.
  const [sp] = useSearchParams()
  const statusFilter = sp.get('status') ?? ''
  const grid = useGridState('transfers', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })
  const [createOpen, setCreateOpen] = useState(false)

  const [form, setForm] = useState<CreateForm>({
    fromWarehouseId: '', toWarehouseId: '', transferType: 'internal', notes: '',
  })

  const { data: warehouses = [], isLoading: wLoading } = useQuery<Warehouse[]>({
    queryKey: ['warehouses', false],
    queryFn: async () => {
      const { data } = await api.get('/inventory/warehouses?activeOnly=false')
      return data.data
    },
  })

  const { data: transfersData, isLoading: tLoading, isFetching, error: listError } = useQuery<PagedResult<TransferSummary>>({
    queryKey: ['transfers', statusFilter, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/inventory/transfers?${grid.toParams({ status: statusFilter || undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const transfers = transfersData?.items ?? []
  const totalCount = transfersData?.totalCount ?? 0

  const createMutation = useMutation({
    mutationFn: async () => {
      const { data } = await api.post('/inventory/transfers', {
        fromWarehouseId: form.fromWarehouseId,
        toWarehouseId: form.toWarehouseId,
        transferType: form.transferType,
        notes: form.notes || null,
        items: [],
      })
      return data.data.id as string
    },
    onSuccess: (id) => {
      queryClient.invalidateQueries({ queryKey: ['transfers'] })
      setCreateOpen(false)
      resetForm()
      navigate(`/inventory/transfers/${id}`)
    },
  })

  function resetForm() {
    setForm({ fromWarehouseId: '', toWarehouseId: '', transferType: 'internal', notes: '' })
  }

  if (wLoading) return <PageSpinner />

  const isCreateValid = form.fromWarehouseId && form.toWarehouseId
    && form.fromWarehouseId !== form.toWarehouseId

  const columns: GridColumn<TransferSummary>[] = [
    { key: 'code', header: 'KOD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 130,
      filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      filters: [{ field: 'notes', label: 'Not', type: 'text' }],
      cell: t => <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{t.code}</code> },
    { key: 'fromWarehouse', header: 'KAYNAK', priority: 1, sortable: true,
      filter: { type: 'enum', label: 'Kaynak depo', field: 'fromWarehouseId', options: warehouses.map(w => ({ value: w.id, label: getWarehouseName(w) })) },
      cell: t => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{t.fromWarehouseCode}</span> },
    { key: 'toWarehouse', header: 'HEDEF', priority: 1, sortable: true,
      filter: { type: 'enum', label: 'Hedef depo', field: 'toWarehouseId', options: warehouses.map(w => ({ value: w.id, label: getWarehouseName(w) })) },
      cell: t => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{t.toWarehouseCode}</span> },
    { key: 'transferType', header: 'TİP', priority: 2, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Tip', options: TRANSFER_TYPES.map(tt => ({ value: tt.value, label: tt.label })) },
      cell: t => <span className="text-sm" style={{ color: 'var(--text-s)' }}>
        {TRANSFER_TYPES.find(tt => tt.value === t.transferType)?.label ?? t.transferType}</span> },
    { key: 'itemCount', header: 'KALEM', priority: 2, align: 'center', sortable: true, filter: { type: 'number', label: 'Kalem sayısı' },
      cell: t => <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{t.itemCount}</span> },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(STATUS_MAP).map(([value, st]) => ({ value, label: st.label })) },
      filters: [{ field: 'open', label: 'Açık transfer', type: 'boolean' }],
      cell: t => { const st = STATUS_MAP[t.status] ?? { label: t.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> } },
    { key: 'createdAt', header: 'TARİH', priority: 2, sortable: true, filter: { type: 'date', label: 'Oluşturma', quick: true },
      filters: [{ field: 'requestedAt', label: 'Talep tarihi', type: 'date' }],
      cell: t => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{new Date(t.createdAt).toLocaleDateString('tr-TR')}</span> },
    { key: 'detail', header: '', priority: 3, align: 'right', exportable: false,
      cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Transferler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{totalCount.toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}</p>
        </div>
        <div className="flex items-center gap-3">
          <select className="inp text-sm py-1.5 px-3 h-auto" value={statusFilter} aria-label="Durum"
            onChange={e => grid.mutate(n => { if (e.target.value) n.set('status', e.target.value); else n.delete('status') })}
            style={{ minWidth: 140 }}>
            <option value="">Tüm Durumlar</option>
            {Object.entries(STATUS_MAP).map(([v, s]) => (
              <option key={v} value={v}>{s.label}</option>
            ))}
          </select>
          <PermissionGuard permission={PERM}>
            <Button size="sm" onClick={() => { resetForm(); setCreateOpen(true) }}>+ Yeni Transfer</Button>
          </PermissionGuard>
        </div>
      </div>

      <DataGrid<TransferSummary>
        gridId="transfers"
        views
        grid={grid}
        columns={columns}
        rows={transfers}
        totalCount={totalCount}
        loading={tLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={t => navigate(`/inventory/transfers/${t.id}`)}
        empty="Transfer bulunamadı."
        search={{ placeholder: 'Transfer kodu veya notta ara…' }}
        minWidth={920}
        export={{ endpoint: '/inventory/transfers/export', named: () => ({ status: statusFilter || undefined }), fallbackFileName: 'transferler.xlsx' }}
        compact={{
          title: t => t.code,
          subtitle: t => `${t.fromWarehouseCode} → ${t.toWarehouseCode}`,
          right: t => `${t.itemCount} kalem`,
          badge: t => { const st = STATUS_MAP[t.status] ?? { label: t.status, variant: 'neutral' as const }; return <Badge variant={st.variant}>{st.label}</Badge> },
        }}
      />

      {/* Create Modal — sadece ana bilgiler */}
      <Modal open={createOpen} onClose={() => { setCreateOpen(false); resetForm() }} title="Yeni Transfer Talebi">
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Kaynak Depo <span className="text-red-500">*</span></label>
              <select className="inp" value={form.fromWarehouseId}
                onChange={e => setForm(f => ({ ...f, fromWarehouseId: e.target.value }))}>
                <option value="">Depo seçin</option>
                {warehouses.map(w => (
                  <option key={w.id} value={w.id}>{getWarehouseName(w)}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="flbl">Hedef Depo <span className="text-red-500">*</span></label>
              <select className="inp" value={form.toWarehouseId}
                onChange={e => setForm(f => ({ ...f, toWarehouseId: e.target.value }))}>
                <option value="">Depo seçin</option>
                {warehouses.filter(w => w.id !== form.fromWarehouseId).map(w => (
                  <option key={w.id} value={w.id}>{getWarehouseName(w)}</option>
                ))}
              </select>
            </div>
          </div>

          <div>
            <label className="flbl">Transfer Tipi</label>
            <select className="inp" value={form.transferType}
              onChange={e => setForm(f => ({ ...f, transferType: e.target.value }))}>
              {TRANSFER_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
            </select>
          </div>

          <div>
            <label className="flbl">Not <span className="text-xs" style={{ color: 'var(--text-s)' }}>(isteğe bağlı)</span></label>
            <textarea className="ta" rows={2} value={form.notes}
              onChange={e => setForm(f => ({ ...f, notes: e.target.value }))}
              placeholder="Transfer açıklaması" />
          </div>

          <p className="text-xs rounded-xl px-3 py-2" style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>
            Transfer oluşturulduktan sonra detay sayfasından ürün kalemleri ekleyebilirsiniz.
          </p>
        </div>

        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => { setCreateOpen(false); resetForm() }}>İptal</Button>
          <Button onClick={() => createMutation.mutate()} loading={createMutation.isPending}
            disabled={!isCreateValid}>
            Oluştur
          </Button>
        </div>
      </Modal>
    </div>
  )
}
