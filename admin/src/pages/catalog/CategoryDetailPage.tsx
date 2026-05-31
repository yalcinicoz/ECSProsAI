import { useState, useMemo } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Plus, Trash2, RefreshCw, Save } from 'lucide-react'
import { cn } from '@/lib/utils'
import { FilterBuilder, type FilterDef } from '@/components/catalog/FilterBuilder'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'

// ── Types ─────────────────────────────────────────────────────────────────────

interface FilterPresetInfo {
  id: string
  code: string
  nameI18n: Record<string, string>
  description: string | null
  filterDef: Record<string, unknown>
}

interface CategoryDetail {
  id: string
  code: string
  nameI18n: Record<string, string>
  parentId: string | null
  fillType: string
  filterPresetId: string | null
  filterPreset: FilterPresetInfo | null
  filterRules: Record<string, unknown> | null
  isActive: boolean
  sortOrder: number
}

interface CategoryProductItem {
  productId: string
  code: string
  nameI18n: Record<string, string>
  mainImageUrl: string | null
  basePrice: number
  isActive: boolean
  sortOrder: number
  isPinned: boolean
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

const FILL_TYPES: Record<string, { label: string; color: string }> = {
  manual: { label: 'Manuel',  color: '#6366f1' },
  filter: { label: 'Filtre',  color: '#0ea5e9' },
  mixed:  { label: 'Karma',   color: '#8b5cf6' },
}

const TABS = ['Genel', 'Ürünler'] as const
type Tab = typeof TABS[number]

function getName(i18n: Record<string, string>, code?: string): string {
  return i18n['tr'] ?? i18n[Object.keys(i18n)[0]] ?? code ?? ''
}

// ── Main Component ────────────────────────────────────────────────────────────

export function CategoryDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [] } = useLanguages()

  const [activeTab, setActiveTab] = useState<Tab>('Genel')

  // ── Category fetch ────────────────────────────────────────────────────────

  const { data: cat, isLoading } = useQuery<CategoryDetail>({
    queryKey: ['category', id],
    queryFn: async () => {
      const { data } = await api.get(`/catalog/categories/${id}`)
      return data.data
    },
    enabled: !!id,
  })

  // ── General tab state ─────────────────────────────────────────────────────

  const [genForm, setGenForm] = useState<{
    nameI18n: Record<string, string>
    fillType: string
    filterPresetId: string | null
    filterDef: FilterDef
    isActive: boolean
    sortOrder: number
  } | null>(null)

  const [formInited, setFormInited] = useState(false)
  if (cat && !formInited) {
    setFormInited(true)
    setGenForm({
      nameI18n:       { ...cat.nameI18n },
      fillType:       cat.fillType,
      filterPresetId: cat.filterPresetId,
      filterDef:      (cat.filterRules ?? {}) as FilterDef,
      isActive:       cat.isActive,
      sortOrder:      cat.sortOrder,
    })
  }

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!genForm) return
      const filterRules = genForm.fillType !== 'manual' && Object.keys(genForm.filterDef).length > 0
        ? genForm.filterDef
        : null
      await api.put(`/catalog/categories/${id}`, {
        nameI18n:       genForm.nameI18n,
        parentId:       cat?.parentId ?? null,
        fillType:       genForm.fillType,
        filterPresetId: genForm.filterPresetId ?? null,
        filterRules,
        isActive:       genForm.isActive,
        sortOrder:      genForm.sortOrder,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['category', id] })
      setFormInited(false)
    },
  })

  // ── Filter presets list (for selector) ───────────────────────────────────

  const { data: filterPresets = [] } = useQuery<FilterPresetInfo[]>({
    queryKey: ['filter-presets-active'],
    queryFn: async () => { const { data } = await api.get('/catalog/filter-presets?activeOnly=true'); return data.data },
  })

  const selectedPreset = filterPresets.find(fp => fp.id === genForm?.filterPresetId)
    ?? (cat?.filterPreset ?? null)

  // ── Products tab ─────────────────────────────────────────────────────────

  const [prodPage, setProdPage] = useState(1)
  const { data: prodData, isLoading: prodLoading, refetch: refetchProds } =
    useQuery<PagedResult<CategoryProductItem>>({
      queryKey: ['category-products', id, prodPage],
      queryFn: async () => {
        const { data } = await api.get(`/catalog/categories/${id}/products?page=${prodPage}&pageSize=20`)
        return data.data
      },
      enabled: !!id && activeTab === 'Ürünler',
    })

  // Add product modal
  const [addOpen, setAddOpen] = useState(false)
  const [addProductId, setAddProductId] = useState('')
  const [addSortOrder, setAddSortOrder] = useState(0)
  const [addIsPinned, setAddIsPinned] = useState(false)

  const { data: allProducts = [] } = useQuery<{ id: string; code: string; nameI18n: Record<string, string> }[]>({
    queryKey: ['products-simple'],
    queryFn: async () => {
      const { data } = await api.get('/catalog/products?activeOnly=false&pageSize=500')
      return data.data?.items ?? []
    },
    enabled: addOpen,
  })

  const productOptions = useMemo(
    () => allProducts.map((p) => ({ value: p.id, label: `${getName(p.nameI18n, p.code)} (${p.code})` })),
    [allProducts],
  )

  const addMutation = useMutation({
    mutationFn: async () => {
      await api.post(`/catalog/categories/${id}/products`, {
        productId: addProductId,
        sortOrder: addSortOrder,
        isPinned:  addIsPinned,
      })
    },
    onSuccess: () => {
      setAddOpen(false)
      setAddProductId('')
      setAddSortOrder(0)
      setAddIsPinned(false)
      refetchProds()
    },
  })

  const removeMutation = useMutation({
    mutationFn: async (productId: string) => {
      await api.delete(`/catalog/categories/${id}/products/${productId}`)
    },
    onSuccess: () => refetchProds(),
  })

  // Sync mutation
  const syncMutation = useMutation({
    mutationFn: async () => {
      const { data } = await api.post(`/catalog/categories/${id}/sync`)
      return data.data?.addedCount as number
    },
    onSuccess: () => refetchProds(),
  })

  // ── Helpers ───────────────────────────────────────────────────────────────

  const sourceLang = languages.find((l) => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'
  const nameFields = useMemo(() => [{ key: 'name', labels: FL.categoryName, required: true }], [])

  if (isLoading || !cat || !genForm) return <PageSpinner />

  const fillInfo = FILL_TYPES[genForm.fillType] ?? FILL_TYPES.manual
  const hasFilterSupport = genForm.fillType === 'filter' || genForm.fillType === 'mixed'

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center gap-3 mb-6">
        <button
          onClick={() => navigate('/catalog/categories')}
          className="w-8 h-8 flex items-center justify-center rounded-xl transition-colors"
          style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}
        >
          <ArrowLeft size={15} />
        </button>
        <div className="flex-1 min-w-0">
          <h1 className="text-xl font-bold truncate" style={{ color: 'var(--text)' }}>
            {getName(cat.nameI18n, cat.code)}
          </h1>
          <div className="flex items-center gap-2 mt-0.5">
            <code className="text-xs" style={{ color: 'var(--text-s)' }}>{cat.code}</code>
            <span
              className="text-xs px-1.5 py-0.5 rounded-full font-medium"
              style={{ background: `${fillInfo.color}22`, color: fillInfo.color }}
            >
              {fillInfo.label}
            </span>
            <Badge variant={cat.isActive ? 'success' : 'neutral'}>
              {cat.isActive ? 'Aktif' : 'Pasif'}
            </Badge>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="tab-scroll mb-6">
        {TABS.map((tab) => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={cn('stab', activeTab === tab && 'active')}
          >
            {tab}
          </button>
        ))}
      </div>

      {/* ── Genel Tab ─────────────────────────────────────────────────────── */}
      {activeTab === 'Genel' && (
        <div className="card space-y-6">
          {/* Ad — I18n */}
          <div>
            <label className="flbl mb-2">Ad</label>
            <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
              <I18nField
                sourceLang={sourceLang}
                languages={languages}
                fields={nameFields}
                values={buildI18nValues(genForm.nameI18n, languages)}
                onChange={(lang, _key, value) =>
                  setGenForm((f) => f && ({ ...f, nameI18n: { ...f.nameI18n, [lang]: value } }))
                }
              />
            </div>
          </div>

          {/* Dolum Tipi */}
          <div>
            <label className="flbl">Dolum Tipi</label>
            <SearchableSelect
              value={genForm.fillType}
              onChange={(v) => v && setGenForm((f) => f && ({ ...f, fillType: v }))}
              options={Object.entries(FILL_TYPES).map(([v, { label }]) => ({ value: v, label }))}
              hasValue
            />
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Manuel: ürünler el ile eklenir · Filtre: kural tabanlı otomatik · Karma: her ikisi
            </p>
          </div>

          {/* Filtre — Preset seçici + görsel builder */}
          {hasFilterSupport && (
            <div className="space-y-4">
              {/* Kayıtlı filtre seçici */}
              <div>
                <div className="flex items-center justify-between mb-1">
                  <label className="flbl">Kayıtlı Filtreden Seç</label>
                  {genForm.filterPresetId && (
                    <button className="text-xs" style={{ color: '#ef4444' }}
                      onClick={() => setGenForm(f => f && ({ ...f, filterPresetId: null }))}>
                      Filtreyi kaldır
                    </button>
                  )}
                </div>
                <SearchableSelect
                  value={genForm.filterPresetId}
                  onChange={(v) => setGenForm(f => f && ({ ...f, filterPresetId: v ?? null }))}
                  options={filterPresets.map(fp => ({
                    value: fp.id,
                    label: fp.nameI18n['tr'] ?? fp.nameI18n[Object.keys(fp.nameI18n)[0]] ?? fp.code,
                  }))}
                  placeholder="Kayıtlı filtreyi seç…"
                  hasValue={!!genForm.filterPresetId}
                />
                {selectedPreset && (
                  <div className="mt-2 p-2.5 rounded-xl text-xs"
                    style={{ background: 'var(--brand-bg)', border: '1px solid var(--brand)' }}>
                    <span className="font-semibold" style={{ color: 'var(--brand)' }}>
                      {selectedPreset.nameI18n['tr'] ?? selectedPreset.code}
                    </span>
                    {selectedPreset.description && (
                      <span style={{ color: 'var(--text-m)' }}> · {selectedPreset.description}</span>
                    )}
                  </div>
                )}
              </div>

              {/* Ayraç */}
              <div className="flex items-center gap-2" style={{ color: 'var(--text-s)' }}>
                <div style={{ flex: 1, height: 1, background: 'var(--border)' }} />
                <span className="text-xs">{genForm.filterPresetId ? 'ek özel kurallar (opsiyonel)' : 'filtre kurallarını seç'}</span>
                <div style={{ flex: 1, height: 1, background: 'var(--border)' }} />
              </div>

              {/* Görsel filtre oluşturucu */}
              <FilterBuilder
                value={genForm.filterDef}
                onChange={(def) => setGenForm(f => f && ({ ...f, filterDef: def }))}
              />

              <p className="text-xs" style={{ color: 'var(--text-s)' }}>
                Ürünler sekmesinden "Sync Çalıştır" ile CategoryProducts tablosu güncellenir.
              </p>
            </div>
          )}

          {/* Sıra + Durum */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="flbl">Sıra</label>
              <IntegerInput
                value={genForm.sortOrder}
                onChange={(v) => setGenForm((f) => f && ({ ...f, sortOrder: v ?? 0 }))}
              />
            </div>
            <div>
              <label className="flbl">Durum</label>
              <div className="flex items-center gap-3 mt-2">
                <button
                  onClick={() => setGenForm((f) => f && ({ ...f, isActive: !f.isActive }))}
                  className={cn(
                    'relative w-10 h-5 rounded-full transition-colors',
                    genForm.isActive ? 'bg-[var(--brand)]' : 'bg-[var(--border)]',
                  )}
                >
                  <span className={cn(
                    'absolute top-0.5 w-4 h-4 rounded-full bg-white transition-transform shadow-sm',
                    genForm.isActive ? 'translate-x-5' : 'translate-x-0.5',
                  )} />
                </button>
                <span className="text-sm" style={{ color: genForm.isActive ? 'var(--brand)' : 'var(--text-s)' }}>
                  {genForm.isActive ? 'Aktif' : 'Pasif'}
                </span>
              </div>
            </div>
          </div>

          <div className="flex justify-end pt-2" style={{ borderTop: '1px solid var(--border)' }}>
            <Button onClick={() => saveMutation.mutate()} loading={saveMutation.isPending}>
              <Save size={14} /> Kaydet
            </Button>
          </div>
        </div>
      )}

      {/* ── Ürünler Tab ───────────────────────────────────────────────────── */}
      {activeTab === 'Ürünler' && (
        <div className="space-y-4">
          {/* Toolbar */}
          <div className="flex items-center justify-between">
            <p className="text-sm" style={{ color: 'var(--text-s)' }}>
              {prodData?.totalCount ?? 0} ürün
            </p>
            <div className="flex items-center gap-2">
              {hasFilterSupport && (
                <Button
                  variant="secondary"
                  onClick={() => syncMutation.mutate()}
                  loading={syncMutation.isPending}
                  title="FilterRules'u çalıştır, listeyi güncelle"
                >
                  <RefreshCw size={14} />
                  Sync Çalıştır
                </Button>
              )}
              <Button onClick={() => setAddOpen(true)}>
                <Plus size={14} /> Ürün Ekle
              </Button>
            </div>
          </div>

          {syncMutation.isSuccess && (
            <div
              className="flex items-center gap-2 px-3 py-2 rounded-xl text-sm"
              style={{ background: '#dcfce7', color: '#16a34a' }}
            >
              Sync tamamlandı — {syncMutation.data} yeni ürün eklendi.
            </div>
          )}

          {/* Product list */}
          <div className="card overflow-hidden p-0">
            <table className="w-full">
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                  <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Ürün</th>
                  <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Sıra</th>
                  <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Sabit</th>
                  <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Fiyat</th>
                  <th className="w-12" />
                </tr>
              </thead>
              <tbody>
                {prodLoading && (
                  <tr><td colSpan={5} className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</td></tr>
                )}
                {!prodLoading && !prodData?.items.length && (
                  <tr><td colSpan={5} className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Henüz ürün yok</td></tr>
                )}
                {prodData?.items.map((p) => (
                  <tr key={p.productId} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        {p.mainImageUrl
                          ? <img src={p.mainImageUrl} className="w-8 h-8 rounded-lg object-cover flex-shrink-0" />
                          : <div className="w-8 h-8 rounded-lg flex-shrink-0" style={{ background: 'var(--surface2)' }} />
                        }
                        <div>
                          <div className="text-sm font-medium" style={{ color: 'var(--text)' }}>
                            {getName(p.nameI18n, p.code)}
                          </div>
                          <code className="text-xs" style={{ color: 'var(--text-s)' }}>{p.code}</code>
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-center text-sm" style={{ color: 'var(--text-m)' }}>{p.sortOrder}</td>
                    <td className="px-4 py-3 text-center">
                      {p.isPinned && <Badge variant="info">Sabit</Badge>}
                    </td>
                    <td className="px-4 py-3 text-center text-sm font-medium" style={{ color: 'var(--text)' }}>
                      {p.basePrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺
                    </td>
                    <td className="px-4 py-3 text-center">
                      <button
                        onClick={() => removeMutation.mutate(p.productId)}
                        disabled={removeMutation.isPending}
                        className="w-7 h-7 flex items-center justify-center rounded-lg transition-colors hover:bg-red-50"
                        style={{ color: '#ef4444' }}
                        title="Kategoriden çıkar"
                      >
                        <Trash2 size={13} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {/* Pagination */}
            {(prodData?.totalCount ?? 0) > 20 && (
              <div className="flex items-center justify-center gap-2 p-4" style={{ borderTop: '1px solid var(--border)' }}>
                <Button variant="secondary" disabled={prodPage <= 1} onClick={() => setProdPage((p) => p - 1)} size="sm">←</Button>
                <span className="text-sm" style={{ color: 'var(--text-s)' }}>Sayfa {prodPage}</span>
                <Button variant="secondary" disabled={(prodData?.items.length ?? 0) < 20} onClick={() => setProdPage((p) => p + 1)} size="sm">→</Button>
              </div>
            )}
          </div>
        </div>
      )}

      {/* ── Add Product Modal ─────────────────────────────────────────────── */}
      <Modal
        open={addOpen}
        onClose={() => setAddOpen(false)}
        title="Ürün Ekle"
        footer={
          <>
            <Button variant="secondary" onClick={() => setAddOpen(false)}>İptal</Button>
            <Button onClick={() => addMutation.mutate()} loading={addMutation.isPending} disabled={!addProductId}>
              Ekle
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="flbl">Ürün</label>
            <SearchableSelect
              value={addProductId}
              onChange={(v) => v && setAddProductId(v)}
              options={productOptions}
              placeholder="Ürün seçin…"
              hasValue={!!addProductId}
            />
          </div>
          <div>
            <label className="flbl">Sıra</label>
            <IntegerInput value={addSortOrder} onChange={(v) => setAddSortOrder(v ?? 0)} />
          </div>
          <div className="flex items-center gap-3">
            <input
              type="checkbox"
              id="isPinned"
              checked={addIsPinned}
              onChange={(e) => setAddIsPinned(e.target.checked)}
              className="w-4 h-4 rounded"
            />
            <label htmlFor="isPinned" className="text-sm" style={{ color: 'var(--text-m)' }}>
              Sabit (sync işleminde kaldırılmaz)
            </label>
          </div>
        </div>
      </Modal>
    </div>
  )
}
