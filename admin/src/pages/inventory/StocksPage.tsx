import { useState, useRef } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { PermissionGuard } from '@/components/ui/PermissionGuard'
import { cn } from '@/lib/utils'
import type { Warehouse } from './WarehousesPage'
import { getWarehouseName } from './WarehousesPage'

const PERM = 'inventory.manage'

const MOVEMENT_TYPES = [
  { value: 'purchase',    label: 'Satın Alma' },
  { value: 'sale',        label: 'Satış' },
  { value: 'adjustment',  label: 'Düzeltme' },
  { value: 'return',      label: 'İade' },
  { value: 'transfer_in', label: 'Transfer Giriş' },
  { value: 'transfer_out',label: 'Transfer Çıkış' },
]

interface Stock {
  id: string
  variantId: string
  warehouseId: string
  stockType: string
  quantity: number
  reservedQuantity: number
  availableQuantity: number
}

interface VariantInfo {
  id: string
  barcode: string
  sku: string
  productCode: string
  productName: string
  attributeSummary?: string
}

type AdjustForm = {
  variantId: string
  warehouseId: string
  quantityDelta: number
  movementType: string
  notes: string
}

export function StocksPage() {
  const queryClient = useQueryClient()

  const [warehouseId, setWarehouseId] = useState<string>('')
  const [availableOnly, setAvailableOnly] = useState(false)
  const [adjustOpen, setAdjustOpen] = useState(false)

  const [barcodeInput, setBarcodeInput] = useState('')
  const [variantLookup, setVariantLookup] = useState<VariantInfo | null>(null)
  const [lookupError, setLookupError] = useState('')
  const [lookupLoading, setLookupLoading] = useState(false)
  const barcodeRef = useRef<HTMLInputElement>(null)

  const [form, setForm] = useState<AdjustForm>({
    variantId: '', warehouseId: '', quantityDelta: 1, movementType: 'adjustment', notes: '',
  })

  const { data: warehouses = [], isLoading: wLoading } = useQuery<Warehouse[]>({
    queryKey: ['warehouses', false],
    queryFn: async () => {
      const { data } = await api.get('/inventory/warehouses?activeOnly=false')
      return data.data
    },
  })

  const { data: stocks = [], isLoading: sLoading } = useQuery<Stock[]>({
    queryKey: ['stocks', warehouseId, availableOnly],
    queryFn: async () => {
      const params = new URLSearchParams()
      if (warehouseId) params.set('warehouseId', warehouseId)
      if (availableOnly) params.set('availableOnly', 'true')
      const { data } = await api.get(`/inventory/stocks?${params}`)
      return data.data
    },
  })

  const adjustMutation = useMutation({
    mutationFn: async () => {
      await api.post('/inventory/stocks/adjust', {
        variantId: form.variantId,
        warehouseId: form.warehouseId,
        quantityDelta: form.quantityDelta,
        movementType: form.movementType,
        notes: form.notes || null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['stocks'] })
      setAdjustOpen(false)
      resetAdjustForm()
    },
  })

  function resetAdjustForm() {
    setBarcodeInput('')
    setVariantLookup(null)
    setLookupError('')
    setForm({ variantId: '', warehouseId: '', quantityDelta: 1, movementType: 'adjustment', notes: '' })
  }

  async function lookupBarcode() {
    if (!barcodeInput.trim()) return
    setLookupLoading(true)
    setLookupError('')
    setVariantLookup(null)
    try {
      const { data } = await api.get(`/catalog/variants/by-barcode/${encodeURIComponent(barcodeInput.trim())}`)
      const v = data.data
      setVariantLookup(v)
      setForm(f => ({ ...f, variantId: v.id }))
    } catch {
      setLookupError('Barkod bulunamadı.')
    } finally {
      setLookupLoading(false)
    }
  }

  function openAdjust() {
    resetAdjustForm()
    setAdjustOpen(true)
    setTimeout(() => barcodeRef.current?.focus(), 100)
  }

  const warehouseMap = Object.fromEntries(warehouses.map(w => [w.id, w]))

  if (wLoading) return <PageSpinner />

  const isFormValid = form.variantId && form.warehouseId && form.quantityDelta !== 0

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Stok</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{stocks.length} kayıt</p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1 rounded-xl p-1" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            {[false, true].map(v => (
              <button key={String(v)}
                onClick={() => setAvailableOnly(v)}
                className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all',
                  availableOnly === v ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
                style={availableOnly === v ? { color: 'var(--text)' } : {}}>
                {v ? 'Mevcut' : 'Tümü'}
              </button>
            ))}
          </div>
          <select className="inp text-sm py-1.5 px-3 h-auto" value={warehouseId}
            onChange={e => setWarehouseId(e.target.value)}
            style={{ minWidth: 160 }}>
            <option value="">Tüm Depolar</option>
            {warehouses.map(w => (
              <option key={w.id} value={w.id}>{getWarehouseName(w)}</option>
            ))}
          </select>
          <PermissionGuard permission={PERM}>
            <Button size="sm" onClick={openAdjust}>+ Stok Hareketi</Button>
          </PermissionGuard>
        </div>
      </div>

      {/* Table */}
      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['VARYANT ID', 'DEPO', 'TİP', 'STOK', 'REZ.', 'MEVCUT'].map(h => (
                <th key={h} className="px-4 py-3 text-xs font-semibold text-left"
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {sLoading && (
              <tr><td colSpan={6} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Yükleniyor...
              </td></tr>
            )}
            {!sLoading && stocks.length === 0 && (
              <tr><td colSpan={6} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Stok kaydı bulunamadı.
              </td></tr>
            )}
            {stocks.map(s => (
              <tr key={s.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3">
                  <code className="text-xs font-mono" style={{ color: 'var(--text-m)' }}>
                    {s.variantId.slice(0, 8)}…
                  </code>
                </td>
                <td className="px-4 py-3">
                  <span className="text-sm" style={{ color: 'var(--text)' }}>
                    {warehouseMap[s.warehouseId] ? getWarehouseName(warehouseMap[s.warehouseId]) : s.warehouseId.slice(0, 8)}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <span className="text-xs px-2 py-0.5 rounded-full"
                    style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
                    {s.stockType}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <span className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{s.quantity}</span>
                </td>
                <td className="px-4 py-3">
                  <span className="text-sm" style={{ color: s.reservedQuantity > 0 ? 'var(--brand)' : 'var(--text-s)' }}>
                    {s.reservedQuantity}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <span className={cn('text-sm font-medium',
                    s.availableQuantity <= 0 ? 'text-red-500' : s.availableQuantity <= 5 ? 'text-yellow-600' : '')}>
                    {s.availableQuantity <= 0
                      ? <span className="text-red-500">{s.availableQuantity}</span>
                      : s.availableQuantity}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Adjust Modal */}
      <Modal open={adjustOpen} onClose={() => { setAdjustOpen(false); resetAdjustForm() }} title="Stok Hareketi">
        <div className="space-y-4">
          {/* Barcode lookup */}
          <div>
            <label className="flbl">Ürün Barkodu</label>
            <div className="flex gap-2">
              <input
                ref={barcodeRef}
                className="inp flex-1"
                value={barcodeInput}
                onChange={e => setBarcodeInput(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && lookupBarcode()}
                placeholder="Barkod okut veya yaz" />
              <button
                onClick={lookupBarcode}
                disabled={lookupLoading}
                className="px-3 py-2 rounded-xl text-sm font-medium transition-colors"
                style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}>
                <Search size={14} />
              </button>
            </div>
            {lookupError && <p className="text-xs text-red-500 mt-1">{lookupError}</p>}
          </div>

          {variantLookup && (
            <div className="p-3 rounded-xl text-sm" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
              <p className="font-medium" style={{ color: 'var(--text)' }}>{variantLookup.productName}</p>
              <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
                SKU: {variantLookup.sku}
                {variantLookup.attributeSummary && ` · ${variantLookup.attributeSummary}`}
              </p>
            </div>
          )}

          <div>
            <label className="flbl">Depo <span className="text-red-500">*</span></label>
            <select className="inp" value={form.warehouseId}
              onChange={e => setForm(f => ({ ...f, warehouseId: e.target.value }))}>
              <option value="">Depo seçin</option>
              {warehouses.map(w => (
                <option key={w.id} value={w.id}>{getWarehouseName(w)}</option>
              ))}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Hareket Tipi</label>
              <select className="inp" value={form.movementType}
                onChange={e => setForm(f => ({ ...f, movementType: e.target.value }))}>
                {MOVEMENT_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </div>
            <div>
              <label className="flbl">
                Miktar
                <span className="ml-1 text-xs" style={{ color: 'var(--text-s)' }}>
                  (çıkış için eksi)
                </span>
              </label>
              <input
                type="number"
                className="inp"
                value={form.quantityDelta}
                onChange={e => setForm(f => ({ ...f, quantityDelta: parseInt(e.target.value) || 0 }))} />
            </div>
          </div>

          <div>
            <label className="flbl">Not <span className="text-xs" style={{ color: 'var(--text-s)' }}>(isteğe bağlı)</span></label>
            <textarea className="ta" rows={2} value={form.notes}
              onChange={e => setForm(f => ({ ...f, notes: e.target.value }))}
              placeholder="Hareket açıklaması" />
          </div>
        </div>

        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => { setAdjustOpen(false); resetAdjustForm() }}>İptal</Button>
          <Button onClick={() => adjustMutation.mutate()} loading={adjustMutation.isPending}
            disabled={!isFormValid}>
            Kaydet
          </Button>
        </div>
      </Modal>
    </div>
  )
}
