import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { PermissionGuard } from '@/components/ui/PermissionGuard'
import type { Warehouse } from './WarehousesPage'
import { getWarehouseName } from './WarehousesPage'

const PERM = 'inventory.manage'

export const TRANSFER_TYPES = [
  { value: 'internal',      label: 'İç Transfer' },
  { value: 'replenishment', label: 'İkmal' },
  { value: 'return',        label: 'İade' },
  { value: 'adjustment',    label: 'Düzeltme' },
]

export const STATUS_MAP: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' | 'danger' }> = {
  draft:      { label: 'Taslak',     variant: 'neutral' },
  pending:    { label: 'Bekliyor',   variant: 'warning' },
  picking:    { label: 'Toplama',    variant: 'warning' },
  picked:     { label: 'Toplandı',   variant: 'warning' },
  in_transit: { label: 'Yolda',      variant: 'warning' },
  delivered:  { label: 'Teslim',     variant: 'success' },
  completed:  { label: 'Tamamlandı', variant: 'success' },
  cancelled:  { label: 'İptal',      variant: 'danger' },
}

// Status transition map
export const TRANSITIONS: Record<string, { status: string; label: string }[]> = {
  draft:      [{ status: 'pending',    label: 'Onayla' }, { status: 'cancelled', label: 'İptal Et' }],
  pending:    [{ status: 'picking',    label: 'Toplamaya Başla' }, { status: 'cancelled', label: 'İptal Et' }],
  picking:    [{ status: 'picked',     label: 'Toplama Tamamlandı' }, { status: 'cancelled', label: 'İptal Et' }],
  picked:     [{ status: 'in_transit', label: 'Kargoya Ver' }],
  in_transit: [{ status: 'delivered',  label: 'Teslim Alındı' }],
  delivered:  [{ status: 'completed',  label: 'Tamamla' }],
}

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

  const [statusFilter, setStatusFilter] = useState('')
  const [page, setPage] = useState(1)
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

  const { data: transfersData, isLoading: tLoading } = useQuery<PagedResult<TransferSummary>>({
    queryKey: ['transfers', statusFilter, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (statusFilter) params.set('status', statusFilter)
      const { data } = await api.get(`/inventory/transfers?${params}`)
      return data.data
    },
  })

  const transfers = transfersData?.items ?? []
  const totalCount = transfersData?.totalCount ?? 0
  const totalPages = Math.ceil(totalCount / 20)

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

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Transferler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{totalCount} kayıt</p>
        </div>
        <div className="flex items-center gap-3">
          <select className="inp text-sm py-1.5 px-3 h-auto" value={statusFilter}
            onChange={e => { setStatusFilter(e.target.value); setPage(1) }}
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

      {/* Table */}
      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'KAYNAK', 'HEDEF', 'TİP', 'KALEM', 'DURUM', 'TARİH', ''].map(h => (
                <th key={h} className={`px-4 py-3 text-xs font-semibold ${h === '' ? 'w-20' : 'text-left'}`}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {tLoading && (
              <tr><td colSpan={8} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Yükleniyor...
              </td></tr>
            )}
            {!tLoading && transfers.length === 0 && (
              <tr><td colSpan={8} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Transfer bulunamadı.
              </td></tr>
            )}
            {transfers.map(t => {
              const st = STATUS_MAP[t.status] ?? { label: t.status, variant: 'neutral' as const }
              return (
                <tr key={t.id}
                  onClick={() => navigate(`/inventory/transfers/${t.id}`)}
                  className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                  style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-3">
                    <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{t.code}</code>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm" style={{ color: 'var(--text-m)' }}>{t.fromWarehouseCode}</span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm" style={{ color: 'var(--text-m)' }}>{t.toWarehouseCode}</span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm" style={{ color: 'var(--text-s)' }}>
                      {TRANSFER_TYPES.find(tt => tt.value === t.transferType)?.label ?? t.transferType}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{t.itemCount}</span>
                  </td>
                  <td className="px-4 py-3">
                    <Badge variant={st.variant}>{st.label}</Badge>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>
                      {new Date(t.createdAt).toLocaleDateString('tr-TR')}
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
