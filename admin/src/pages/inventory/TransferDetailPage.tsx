import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ChevronLeft, Plus } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { PageSpinner } from '@/components/ui/Spinner'
import { PermissionGuard } from '@/components/ui/PermissionGuard'
import { TRANSFER_TYPES, STATUS_MAP, TRANSITIONS } from './TransfersPage'

const PERM = 'inventory.manage'

interface TransferItem {
  id: string
  variantId: string
  requestedQuantity: number
  pickedQuantity: number
  deliveredQuantity: number
  fromLocationId: string | null
  toLocationId: string | null
  status: string
}

interface TransferDetail {
  id: string
  code: string
  fromWarehouseId: string
  fromWarehouseCode: string
  toWarehouseId: string
  toWarehouseCode: string
  transferType: string
  status: string
  requestedBy: string
  requestedAt: string
  notes: string | null
  items: TransferItem[]
  createdAt: string
}

type NewItemForm = { variantId: string; requestedQuantity: number; fromLocationId: string; toLocationId: string }

const emptyItem = (): NewItemForm => ({ variantId: '', requestedQuantity: 1, fromLocationId: '', toLocationId: '' })

export function TransferDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [newItem, setNewItem] = useState<NewItemForm>(emptyItem())
  const [addError, setAddError] = useState('')

  const { data: transfer, isLoading } = useQuery<TransferDetail>({
    queryKey: ['transfer', id],
    queryFn: async () => {
      const { data } = await api.get(`/inventory/transfers/${id}`)
      return data.data
    },
    enabled: !!id,
  })

  const addItemMutation = useMutation({
    mutationFn: async (item: NewItemForm) => {
      await api.post(`/inventory/transfers/${id}/items`, {
        variantId: item.variantId,
        requestedQuantity: item.requestedQuantity,
        fromLocationId: item.fromLocationId || null,
        toLocationId: item.toLocationId || null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['transfer', id] })
      queryClient.invalidateQueries({ queryKey: ['transfers'] })
      setNewItem(emptyItem())
      setAddError('')
    },
    onError: (err: any) => {
      setAddError(err?.response?.data?.error ?? 'Kalem eklenemedi.')
    },
  })

  const statusMutation = useMutation({
    mutationFn: async ({ status, notes }: { status: string; notes?: string }) => {
      await api.patch(`/inventory/transfers/${id}/status`, { status, notes: notes ?? null })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['transfer', id] })
      queryClient.invalidateQueries({ queryKey: ['transfers'] })
    },
  })

  function handleAddItem() {
    if (!newItem.variantId.trim()) { setAddError('Varyant ID gereklidir.'); return }
    if (newItem.requestedQuantity < 1) { setAddError('Miktar en az 1 olmalıdır.'); return }
    setAddError('')
    addItemMutation.mutate(newItem)
  }

  if (isLoading) return <PageSpinner />
  if (!transfer) return (
    <div className="p-6 text-center" style={{ color: 'var(--text-s)' }}>Transfer bulunamadı.</div>
  )

  const st = STATUS_MAP[transfer.status] ?? { label: transfer.status, variant: 'neutral' as const }
  const canAddItems = transfer.status === 'draft'
  const transitions = TRANSITIONS[transfer.status] ?? []

  return (
    <div className="p-6 max-w-4xl mx-auto">
      {/* Back + Header */}
      <div className="flex items-center gap-3 mb-6">
        <button
          onClick={() => navigate('/inventory/transfers')}
          className="flex items-center gap-1 text-sm transition-colors hover:opacity-80"
          style={{ color: 'var(--text-s)' }}>
          <ChevronLeft size={16} />
          Transferler
        </button>
        <span style={{ color: 'var(--border)' }}>/</span>
        <code className="text-sm font-mono font-semibold" style={{ color: 'var(--text)' }}>{transfer.code}</code>
        <Badge variant={st.variant}>{st.label}</Badge>
      </div>

      {/* Info card */}
      <div className="card p-5 mb-5">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
          <div>
            <p className="text-xs mb-1 font-medium" style={{ color: 'var(--text-s)' }}>KAYNAK DEPO</p>
            <p className="font-medium" style={{ color: 'var(--text)' }}>{transfer.fromWarehouseCode}</p>
          </div>
          <div>
            <p className="text-xs mb-1 font-medium" style={{ color: 'var(--text-s)' }}>HEDEF DEPO</p>
            <p className="font-medium" style={{ color: 'var(--text)' }}>{transfer.toWarehouseCode}</p>
          </div>
          <div>
            <p className="text-xs mb-1 font-medium" style={{ color: 'var(--text-s)' }}>TİP</p>
            <p style={{ color: 'var(--text-m)' }}>
              {TRANSFER_TYPES.find(t => t.value === transfer.transferType)?.label ?? transfer.transferType}
            </p>
          </div>
          <div>
            <p className="text-xs mb-1 font-medium" style={{ color: 'var(--text-s)' }}>TARİH</p>
            <p style={{ color: 'var(--text-m)' }}>
              {new Date(transfer.createdAt).toLocaleDateString('tr-TR')}
            </p>
          </div>
        </div>
        {transfer.notes && (
          <p className="mt-4 text-sm p-3 rounded-xl" style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>
            {transfer.notes}
          </p>
        )}

        {/* Status transitions */}
        {transitions.length > 0 && (
          <PermissionGuard permission={PERM}>
            <div className="mt-4 pt-4 flex items-center gap-3 flex-wrap" style={{ borderTop: '1px solid var(--border)' }}>
              <span className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>İŞLEMLER:</span>
              {transitions.map(t => (
                <Button
                  key={t.status}
                  size="sm"
                  variant={t.status === 'cancelled' ? 'secondary' : 'primary'}
                  loading={statusMutation.isPending}
                  onClick={() => statusMutation.mutate({ status: t.status })}>
                  {t.label}
                </Button>
              ))}
            </div>
          </PermissionGuard>
        )}
      </div>

      {/* Items */}
      <div className="card overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4" style={{ borderBottom: '1px solid var(--border)' }}>
          <div>
            <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Kalemler</h2>
            <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{transfer.items.length} ürün</p>
          </div>
        </div>

        {/* Items table */}
        {transfer.items.length > 0 ? (
          <table className="w-full text-sm">
            <thead>
              <tr style={{ background: 'var(--surface2)', borderBottom: '1px solid var(--border)' }}>
                <th className="px-4 py-2.5 text-xs font-semibold text-left" style={{ color: 'var(--text-s)' }}>VARYant ID</th>
                <th className="px-4 py-2.5 text-xs font-semibold text-center" style={{ color: 'var(--text-s)' }}>İSTENEN</th>
                <th className="px-4 py-2.5 text-xs font-semibold text-center" style={{ color: 'var(--text-s)' }}>TOPLANAN</th>
                <th className="px-4 py-2.5 text-xs font-semibold text-center" style={{ color: 'var(--text-s)' }}>TESLİM</th>
                <th className="px-4 py-2.5 text-xs font-semibold text-left" style={{ color: 'var(--text-s)' }}>DURUM</th>
              </tr>
            </thead>
            <tbody>
              {transfer.items.map(item => (
                <tr key={item.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-3">
                    <code className="text-xs font-mono" style={{ color: 'var(--text-m)' }}>
                      {item.variantId}
                    </code>
                  </td>
                  <td className="px-4 py-3 text-center font-medium" style={{ color: 'var(--text)' }}>
                    {item.requestedQuantity}
                  </td>
                  <td className="px-4 py-3 text-center" style={{ color: 'var(--text-m)' }}>
                    {item.pickedQuantity}
                  </td>
                  <td className="px-4 py-3 text-center" style={{ color: 'var(--text-m)' }}>
                    {item.deliveredQuantity}
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>{item.status}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <div className="px-5 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>
            {canAddItems ? 'Henüz kalem eklenmemiş. Aşağıdan ekleyin.' : 'Kalem bulunmuyor.'}
          </div>
        )}

        {/* Add item row — only in draft */}
        {canAddItems && (
          <PermissionGuard permission={PERM}>
            <div className="p-4" style={{ borderTop: '1px solid var(--border)', background: 'var(--surface2)' }}>
              <p className="text-xs font-semibold mb-3" style={{ color: 'var(--text-s)' }}>YENİ KALEM EKLE</p>
              <div className="flex gap-2 items-end flex-wrap">
                <div className="flex-1 min-w-[200px]">
                  <label className="block text-xs mb-1" style={{ color: 'var(--text-s)' }}>Varyant ID <span className="text-red-500">*</span></label>
                  <input
                    className="inp text-xs py-1.5"
                    value={newItem.variantId}
                    onChange={e => setNewItem(i => ({ ...i, variantId: e.target.value }))}
                    placeholder="UUID"
                  />
                </div>
                <div style={{ width: 90 }}>
                  <label className="block text-xs mb-1" style={{ color: 'var(--text-s)' }}>Miktar <span className="text-red-500">*</span></label>
                  <input
                    type="number"
                    min={1}
                    className="inp text-xs py-1.5"
                    value={newItem.requestedQuantity}
                    onChange={e => setNewItem(i => ({ ...i, requestedQuantity: parseInt(e.target.value) || 1 }))}
                  />
                </div>
                <div className="flex-1 min-w-[160px]">
                  <label className="block text-xs mb-1" style={{ color: 'var(--text-s)' }}>Kaynak Lokasyon <span className="text-xs opacity-60">(isteğe bağlı)</span></label>
                  <input
                    className="inp text-xs py-1.5"
                    value={newItem.fromLocationId}
                    onChange={e => setNewItem(i => ({ ...i, fromLocationId: e.target.value }))}
                    placeholder="Lokasyon UUID"
                  />
                </div>
                <div className="flex-1 min-w-[160px]">
                  <label className="block text-xs mb-1" style={{ color: 'var(--text-s)' }}>Hedef Lokasyon <span className="text-xs opacity-60">(isteğe bağlı)</span></label>
                  <input
                    className="inp text-xs py-1.5"
                    value={newItem.toLocationId}
                    onChange={e => setNewItem(i => ({ ...i, toLocationId: e.target.value }))}
                    placeholder="Lokasyon UUID"
                  />
                </div>
                <button
                  onClick={handleAddItem}
                  disabled={addItemMutation.isPending || !newItem.variantId.trim()}
                  className="flex items-center gap-1 px-3 py-2 rounded-xl text-xs font-medium disabled:opacity-40 transition-colors"
                  style={{ background: 'var(--brand)', color: '#fff' }}>
                  <Plus size={12} /> Ekle
                </button>
              </div>
              {addError && (
                <p className="text-xs mt-2 text-red-500">{addError}</p>
              )}
            </div>
          </PermissionGuard>
        )}
      </div>
    </div>
  )
}
