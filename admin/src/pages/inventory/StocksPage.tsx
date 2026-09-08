import { useState, useRef } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { Search } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { PermissionGuard } from '@/components/ui/PermissionGuard'
import { cn } from '@/lib/utils'
import { DataGrid, useGridState, type GridColumn, type GridFilterField } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import type { Warehouse } from './WarehousesPage'
import { getWarehouseName } from './warehouseHelpers'

const PERM = 'inventory.manage'

const MOVEMENT_TYPES = [
  { value: 'purchase',    label: 'Satın Alma' },
  { value: 'sale',        label: 'Satış' },
  { value: 'adjustment',  label: 'Düzeltme' },
  { value: 'return',      label: 'İade' },
  { value: 'transfer_in', label: 'Transfer Giriş' },
  { value: 'transfer_out',label: 'Transfer Çıkış' },
]

// Admin stok satırı: sayfalı + ürün/depo/kısım/raf bilgisiyle zenginleştirilmiş
// (eski /inventory/stocks 165K satırı sayfasız döndürüp tarayıcıyı donduruyordu).
interface StockAdminRow {
  id: string
  variantId: string
  productCode: string
  productName: string
  options: string | null       // "Beden: M, Renk: Beyaz"
  imageUrl: string | null
  warehouseName: string
  sectionName: string | null
  binCode: string | null
  quantity: number
  reservedQuantity: number
  availableQuantity: number
}

interface StockPage {
  items: StockAdminRow[]
  totalCount: number
  page: number
  pageSize: number
}

// İkincil filtre: arama sonucundan türetilen seçenekler (varyant / depo / kısım / raf)
interface FacetOption {
  id: string
  label: string
  count: number
  parentId: string | null   // kısım→depo, raf→kısım (kademeli daraltma)
}
interface StockFacets {
  variants: FacetOption[]
  warehouses: FacetOption[]
  sections: FacetOption[]
  bins: FacetOption[]
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

// DataGrid F4 (2026-09-08): arama (global search), depo ve ikincil facet seçimleri (variantId/sectionId/binId) adlandırılmış URL
// parametreleri; "Mevcut" anahtarı f.inStock boolean grid filtresi; sıralama miktar/rezerve/mevcut; kolon tercihleri localStorage'da.
const NAMED_KEYS = ['warehouseId', 'variantId', 'sectionId', 'binId'] as const

const EXTRA_FILTERS: GridFilterField[] = [
  { key: 'inStock', label: 'Mevcut stok', type: 'boolean', quick: true },
  { key: 'quantity', label: 'Stok', type: 'number' },
  { key: 'reserved', label: 'Rezerve', type: 'number' },
  { key: 'available', label: 'Mevcut', type: 'number' },
  { key: 'stockType', label: 'Stok tipi', type: 'enum', options: [{ value: 'physical', label: 'Fiziksel' }, { value: 'virtual', label: 'Sanal' }] },
  { key: 'updatedAt', label: 'Güncellenme', type: 'date' },
]

export function StocksPage() {
  const queryClient = useQueryClient()
  const grid = useGridState('stocks', { defaultPageSize: 30 })
  const [sp] = useSearchParams()
  const get = (k: string) => sp.get(k) ?? ''
  const setNamed = (k: string, v: string, also?: (n: URLSearchParams) => void) => grid.mutate(n => { if (v) n.set(k, v); else n.delete(k); also?.(n) })
  const named = () => Object.fromEntries(NAMED_KEYS.map(k => [k, get(k) || undefined]))
  const warehouseId = get('warehouseId'), variantId = get('variantId'), sectionId = get('sectionId'), binId = get('binId')
  const search = grid.state.search

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

  // Sayfa BOŞ açılır: filtre (arama veya depo) girilmeden sorgu atılmaz — 165K satırı
  // topluca çekmek tarayıcıyı donduruyordu.
  const filtreVar = !!(search || warehouseId)
  const { data: stockPage, isLoading: sLoading, isFetching, error } = useQuery<StockPage>({
    queryKey: ['stocks-admin', ...grid.queryKey, ...NAMED_KEYS.map(k => get(k))],
    queryFn: async () => (await api.get(`/inventory/stocks/admin-list?${grid.toParams(named())}`)).data.data,
    enabled: filtreVar,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const totalCount = stockPage?.totalCount ?? 0

  // İkincil filtre seçenekleri — yalnız arama varken. Mevcut seçimler + grid filtreleri de gönderilir:
  // her boyut diğer seçimlerle daraltılmış hesaplanır (sayaçlar listeyle tutarlı kalır).
  const facetParams = (() => { const p = grid.toParams(named()); p.delete('page'); p.delete('pageSize'); p.delete('sort'); p.delete('dir'); return p.toString() })()
  const { data: facets } = useQuery<StockFacets>({
    queryKey: ['stocks-facets', facetParams],
    queryFn: async () => (await api.get(`/inventory/stocks/admin-list/facets?${facetParams}`)).data.data,
    enabled: !!search,
    placeholderData: (prev) => prev,   // seçim değişince seçenekler yenilenirken çubuk titremesin
  })

  const resetSecondary = (n: URLSearchParams) => { n.delete('variantId'); n.delete('sectionId'); n.delete('binId') }

  // Kademeli daraltma: depo seçiliyse kısımlar o depoya, kısım seçiliyse raflar o kısma süzülür.
  const sectionOptions = (facets?.sections ?? []).filter(s => !warehouseId || s.parentId === warehouseId)
  const binOptions = (facets?.bins ?? []).filter(b => !sectionId || b.parentId === sectionId)

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
      queryClient.invalidateQueries({ queryKey: ['stocks-admin'] })
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

  const columns: GridColumn<StockAdminRow>[] = [
    { key: 'productCode', header: 'ÜRÜN', frozen: true, lockVisible: true, minWidth: 260,
      cell: s => (
        <div className="flex items-center gap-2">
          {s.imageUrl
            ? <img src={s.imageUrl} alt="" className="w-9 h-9 rounded object-cover shrink-0" style={{ background: 'var(--surface2)' }} />
            : <div className="w-9 h-9 rounded shrink-0" style={{ background: 'var(--surface2)' }} />}
          <div className="min-w-0">
            <div className="text-sm truncate" style={{ color: 'var(--text)' }}>{s.productName}</div>
            <div className="text-xs" style={{ color: 'var(--text-s)' }}>
              <code className="font-mono">{s.productCode}</code>
              {s.options ? <span> · {s.options}</span> : null}
            </div>
          </div>
        </div>) },
    { key: 'warehouse', header: 'DEPO', priority: 1, lockVisible: true, cell: s => <span className="text-sm" style={{ color: 'var(--text)' }}>{s.warehouseName}</span> },
    { key: 'section', header: 'KISIM', priority: 2, cell: s => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{s.sectionName ?? '—'}</span> },
    { key: 'bin', header: 'RAF', priority: 2, cell: s => s.binCode
      ? <code className="text-xs font-mono" style={{ color: 'var(--text-m)' }}>{s.binCode}</code>
      : <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span> },
    { key: 'quantity', header: 'STOK', sortable: true, align: 'right', priority: 1, cell: s => <span className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{s.quantity}</span> },
    { key: 'reserved', header: 'REZ.', sortable: true, align: 'right', priority: 2,
      cell: s => <span className="text-sm" style={{ color: s.reservedQuantity > 0 ? 'var(--brand)' : 'var(--text-s)' }}>{s.reservedQuantity}</span> },
    { key: 'available', header: 'MEVCUT', sortable: true, align: 'right', priority: 1,
      cell: s => <span className={cn('text-sm font-medium', s.availableQuantity <= 0 ? 'text-red-500' : s.availableQuantity <= 5 ? 'text-yellow-600' : '')}>{s.availableQuantity}</span> },
  ]

  if (wLoading) return <PageSpinner />

  const isFormValid = form.variantId && form.warehouseId && form.quantityDelta !== 0

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Stok</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {filtreVar ? `${totalCount.toLocaleString('tr-TR')} kayıt` : 'Listelemek için ürün arayın veya depo seçin'}
          </p>
        </div>
        <PermissionGuard permission={PERM}>
          <Button size="sm" onClick={openAdjust}>+ Stok Hareketi</Button>
        </PermissionGuard>
      </div>

      <DataGrid<StockAdminRow>
        gridId="stocks"
        grid={grid}
        columns={columns}
        extraFilters={EXTRA_FILTERS}
        search={{ placeholder: 'Ürün kodu / adı / barkod ara…' }}
        filterLeading={
          <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={warehouseId} aria-label="Depo"
            onChange={e => setNamed('warehouseId', e.target.value, n => { n.delete('sectionId'); n.delete('binId') })}>
            <option value="">Tüm Depolar</option>
            {warehouses.map(w => <option key={w.id} value={w.id}>{getWarehouseName(w)}</option>)}
          </select>
        }
        toolbarBelow={!!search && facets && ((facets.variants?.length ?? 0) > 0 || (facets.sections?.length ?? 0) > 0) ? (
          // İkincil filtre — arama sonucundan türetilen varyant/kısım/raf seçenekleri
          <div className="card2 p-3 flex items-end gap-3 flex-wrap">
            <div>
              <label className="flbl mb-1">Varyant</label>
              <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 220 }} value={variantId} onChange={e => setNamed('variantId', e.target.value)}>
                <option value="">Tümü ({facets.variants.length})</option>
                {facets.variants.map(v => <option key={v.id} value={v.id}>{v.label} — {v.count} kayıt</option>)}
              </select>
            </div>
            <div>
              <label className="flbl mb-1">Kısım</label>
              <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 170 }} value={sectionId} onChange={e => setNamed('sectionId', e.target.value, n => n.delete('binId'))}>
                <option value="">Tümü ({sectionOptions.length})</option>
                {sectionOptions.map(s => <option key={s.id} value={s.id}>{s.label} — {s.count} kayıt</option>)}
              </select>
            </div>
            <div>
              <label className="flbl mb-1">Raf</label>
              <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 150 }} value={binId} onChange={e => setNamed('binId', e.target.value)}>
                <option value="">Tümü ({binOptions.length})</option>
                {binOptions.map(b => <option key={b.id} value={b.id}>{b.label} — {b.count} kayıt</option>)}
              </select>
            </div>
            {(variantId || sectionId || binId) && (
              <button className="text-xs underline pb-2" style={{ color: 'var(--text-s)' }} onClick={() => grid.mutate(resetSecondary)}>
                İkincil filtreyi temizle
              </button>
            )}
          </div>
        ) : undefined}
        rows={filtreVar ? (stockPage?.items ?? []) : []}
        totalCount={filtreVar ? totalCount : 0}
        loading={filtreVar && sLoading}
        fetching={filtreVar && isFetching}
        error={error ? errText(error) : null}
        empty={filtreVar ? 'Stok kaydı bulunamadı.' : 'Ürün kodu/adı/barkod arayın veya bir depo seçin — sonuçlar burada listelenir.'}
        minWidth={760}
        export={{ endpoint: '/inventory/stocks/admin-list/export', named, fallbackFileName: 'stok.xlsx' }}
      />

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
