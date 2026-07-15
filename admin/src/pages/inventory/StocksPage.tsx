import { useState, useRef } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { Pagination } from '@/components/ui/Pagination'
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

const PAGE_SIZE = 30

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
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')     // uygulanmış arama (Ara/Enter ile)
  const [page, setPage] = useState(1)
  // İkincil filtre seçimleri (arama facet'lerinden)
  const [variantId, setVariantId] = useState('')
  const [sectionId, setSectionId] = useState('')
  const [binId, setBinId] = useState('')

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
  const { data: stockPage, isLoading: sLoading } = useQuery<StockPage>({
    queryKey: ['stocks-admin', search, warehouseId, availableOnly, variantId, sectionId, binId, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) })
      if (search) params.set('search', search)
      if (warehouseId) params.set('warehouseId', warehouseId)
      if (availableOnly) params.set('availableOnly', 'true')
      if (variantId) params.set('variantId', variantId)
      if (sectionId) params.set('sectionId', sectionId)
      if (binId) params.set('binId', binId)
      const { data } = await api.get(`/inventory/stocks/admin-list?${params}`)
      return data.data
    },
    enabled: filtreVar,
  })
  const stocks = stockPage?.items ?? []
  const totalCount = stockPage?.totalCount ?? 0
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))

  // İkincil filtre seçenekleri — yalnız arama varken. Mevcut seçimler de gönderilir:
  // her boyut diğer seçimlerle daraltılmış hesaplanır (sayaçlar listeyle tutarlı kalır).
  const { data: facets } = useQuery<StockFacets>({
    queryKey: ['stocks-facets', search, warehouseId, availableOnly, variantId, sectionId, binId],
    queryFn: async () => {
      const params = new URLSearchParams({ search })
      if (warehouseId) params.set('warehouseId', warehouseId)
      if (availableOnly) params.set('availableOnly', 'true')
      if (variantId) params.set('variantId', variantId)
      if (sectionId) params.set('sectionId', sectionId)
      if (binId) params.set('binId', binId)
      const { data } = await api.get(`/inventory/stocks/admin-list/facets?${params}`)
      return data.data
    },
    enabled: !!search,
    placeholderData: (prev) => prev,   // seçim değişince seçenekler yenilenirken çubuk titremesin
  })

  const resetSecondary = () => { setVariantId(''); setSectionId(''); setBinId('') }
  const applySearch = () => { setPage(1); resetSecondary(); setSearch(searchInput.trim()) }

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

  if (wLoading) return <PageSpinner />

  const isFormValid = form.variantId && form.warehouseId && form.quantityDelta !== 0

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Stok</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {filtreVar ? `${totalCount} kayıt` : 'Listelemek için ürün arayın veya depo seçin'}
          </p>
        </div>
        <div className="flex items-center gap-3 flex-wrap">
          <div className="flex gap-1">
            <input
              className="inp text-sm py-1.5 px-3 h-auto"
              style={{ minWidth: 220 }}
              value={searchInput}
              onChange={e => setSearchInput(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && applySearch()}
              placeholder="Ürün kodu / adı / barkod ara…"
            />
            <Button size="sm" variant="secondary" onClick={applySearch}><Search size={14} /></Button>
          </div>
          <div className="flex items-center gap-1 rounded-xl p-1" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            {[false, true].map(v => (
              <button key={String(v)}
                onClick={() => { setPage(1); setAvailableOnly(v) }}
                className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all',
                  availableOnly === v ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
                style={availableOnly === v ? { color: 'var(--text)' } : {}}>
                {v ? 'Mevcut' : 'Tümü'}
              </button>
            ))}
          </div>
          <select className="inp text-sm py-1.5 px-3 h-auto" value={warehouseId}
            onChange={e => { setPage(1); setSectionId(''); setBinId(''); setWarehouseId(e.target.value) }}
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

      {/* İkincil filtre — arama sonucundan türetilen varyant/kısım/raf seçenekleri */}
      {!!search && facets && (facets.variants.length > 0 || facets.sections.length > 0) && (
        <div className="card mb-4 flex items-end gap-3 flex-wrap">
          <div>
            <label className="flbl mb-1">Varyant</label>
            <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 220 }}
              value={variantId}
              onChange={e => { setPage(1); setVariantId(e.target.value) }}>
              <option value="">Tümü ({facets.variants.length})</option>
              {facets.variants.map(v => (
                <option key={v.id} value={v.id}>{v.label} — {v.count} kayıt</option>
              ))}
            </select>
          </div>
          <div>
            <label className="flbl mb-1">Kısım</label>
            <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 170 }}
              value={sectionId}
              onChange={e => { setPage(1); setBinId(''); setSectionId(e.target.value) }}>
              <option value="">Tümü ({sectionOptions.length})</option>
              {sectionOptions.map(s => (
                <option key={s.id} value={s.id}>{s.label} — {s.count} kayıt</option>
              ))}
            </select>
          </div>
          <div>
            <label className="flbl mb-1">Raf</label>
            <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 150 }}
              value={binId}
              onChange={e => { setPage(1); setBinId(e.target.value) }}>
              <option value="">Tümü ({binOptions.length})</option>
              {binOptions.map(b => (
                <option key={b.id} value={b.id}>{b.label} — {b.count} kayıt</option>
              ))}
            </select>
          </div>
          {(variantId || sectionId || binId) && (
            <button className="text-xs underline pb-2" style={{ color: 'var(--text-s)' }}
              onClick={() => { setPage(1); resetSecondary() }}>
              İkincil filtreyi temizle
            </button>
          )}
        </div>
      )}

      {/* Table */}
      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['ÜRÜN', 'DEPO', 'KISIM', 'RAF', 'STOK', 'REZ.', 'MEVCUT'].map(h => (
                <th key={h} className="px-4 py-3 text-xs font-semibold text-left"
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {!filtreVar && (
              <tr><td colSpan={7} className="px-4 py-14 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Ürün kodu/adı/barkod arayın veya bir depo seçin — sonuçlar burada listelenir.
              </td></tr>
            )}
            {filtreVar && sLoading && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Yükleniyor...
              </td></tr>
            )}
            {filtreVar && !sLoading && stocks.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Stok kaydı bulunamadı.
              </td></tr>
            )}
            {stocks.map(s => (
              <tr key={s.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-2">
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
                  </div>
                </td>
                <td className="px-4 py-2">
                  <span className="text-sm" style={{ color: 'var(--text)' }}>{s.warehouseName}</span>
                </td>
                <td className="px-4 py-2">
                  <span className="text-sm" style={{ color: 'var(--text-m)' }}>{s.sectionName ?? '—'}</span>
                </td>
                <td className="px-4 py-2">
                  {s.binCode
                    ? <code className="text-xs font-mono" style={{ color: 'var(--text-m)' }}>{s.binCode}</code>
                    : <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>}
                </td>
                <td className="px-4 py-2">
                  <span className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{s.quantity}</span>
                </td>
                <td className="px-4 py-2">
                  <span className="text-sm" style={{ color: s.reservedQuantity > 0 ? 'var(--brand)' : 'var(--text-s)' }}>
                    {s.reservedQuantity}
                  </span>
                </td>
                <td className="px-4 py-2">
                  <span className={cn('text-sm font-medium',
                    s.availableQuantity <= 0 ? 'text-red-500' : s.availableQuantity <= 5 ? 'text-yellow-600' : '')}>
                    {s.availableQuantity}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {filtreVar && (
          <Pagination page={page} totalPages={totalPages} totalCount={totalCount} pageSize={PAGE_SIZE} onChange={setPage} />
        )}
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
