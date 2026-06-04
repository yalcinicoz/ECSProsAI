import { useState, useMemo } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Plus, Trash2, RefreshCw, Save, AlertTriangle, CheckCircle } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { FilterBuilder, type FilterDef } from '@/components/catalog/FilterBuilder'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'

interface CoverageDto {
  assignedGroupCount: number
  coveredGroupCount: number
  uncoveredGroupIds: string[]
}

interface CategoryDetail {
  id: string
  firmPlatformId: string
  parentId: string | null
  nameI18n: Record<string, string>
  slug: string
  status: string
  fillType: string
  filterDef: Record<string, unknown> | null
  sortOrder: number
  displayImageUrl: string | null
  badgeLabel: string | null
  metaTitleI18n: Record<string, string> | null
  metaDescriptionI18n: Record<string, string> | null
  ogImageUrl: string | null
  ogTitleI18n: Record<string, string> | null
  productGroupIds: string[]
  coverage: CoverageDto
}

interface ProductItem {
  productId: string
  code: string
  nameI18n: Record<string, string>
  mainImageUrl: string | null
  basePrice: number
  isActive: boolean
  sortOrder: number
  isExcluded: boolean
}

interface ProductGroup {
  id: string
  code: string
  nameI18n: Record<string, string>
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

const FILL_TYPES = [
  { value: 'manual', label: 'Manuel — ürünler elle eklenir' },
  { value: 'filter', label: 'Filtre — kural tabanlı otomatik' },
  { value: 'mixed',  label: 'Karma — filtre + sabitler' },
]

const STATUS_OPTIONS = [
  { value: 'draft',     label: 'Taslak' },
  { value: 'published', label: 'Yayında' },
  { value: 'archived',  label: 'Arşiv' },
]

const TABS = ['Genel', 'Gruplar', 'Ürünler', 'SEO'] as const
type Tab = typeof TABS[number]

function getName(i18n: Record<string, string>, fallback = ''): string {
  return i18n?.['tr'] ?? i18n?.[Object.keys(i18n ?? {})[0]] ?? fallback
}

export function ChannelCategoryDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [] } = useLanguages()

  const [activeTab, setActiveTab] = useState<Tab>('Genel')

  const { data: cat, isLoading } = useQuery<CategoryDetail>({
    queryKey: ['channel-category', id],
    queryFn: async () => {
      const { data } = await api.get(`/navigation/channel-categories/${id}`)
      return data.data
    },
    enabled: !!id,
  })

  // ── General form ──────────────────────────────────────────────────────────

  const [form, setForm] = useState<{
    nameI18n: Record<string, string>
    slug: string
    status: string
    fillType: string
    filterDef: FilterDef
    sortOrder: number
    displayImageUrl: string
    badgeLabel: string
  } | null>(null)

  const [formInited, setFormInited] = useState(false)
  if (cat && !formInited) {
    setFormInited(true)
    setForm({
      nameI18n:       { ...cat.nameI18n },
      slug:           cat.slug,
      status:         cat.status,
      fillType:       cat.fillType,
      filterDef:      (cat.filterDef ?? {}) as FilterDef,
      sortOrder:      cat.sortOrder,
      displayImageUrl: cat.displayImageUrl ?? '',
      badgeLabel:     cat.badgeLabel ?? '',
    })
  }

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!form) return
      await api.put(`/navigation/channel-categories/${id}`, {
        parentId:         cat?.parentId ?? null,
        nameI18n:         form.nameI18n,
        slug:             form.slug,
        status:           form.status,
        fillType:         form.fillType,
        filterDef:        form.fillType !== 'manual' ? form.filterDef : null,
        sortOrder:        form.sortOrder,
        displayImageUrl:  form.displayImageUrl || null,
        badgeLabel:       form.badgeLabel || null,
        metaTitleI18n:    cat?.metaTitleI18n ?? null,
        metaDescriptionI18n: cat?.metaDescriptionI18n ?? null,
        ogImageUrl:       cat?.ogImageUrl ?? null,
        ogTitleI18n:      cat?.ogTitleI18n ?? null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['channel-category', id] })
      setFormInited(false)
    },
  })

  // ── Product Groups tab ────────────────────────────────────────────────────

  const [selectedGroupIds, setSelectedGroupIds] = useState<string[] | null>(null)
  const groupIds = selectedGroupIds ?? cat?.productGroupIds ?? []

  const { data: allGroups = [] } = useQuery<ProductGroup[]>({
    queryKey: ['product-groups-simple'],
    queryFn: async () => {
      const { data } = await api.get('/catalog/product-groups')
      return data.data ?? []
    },
    enabled: activeTab === 'Gruplar',
  })

  const groupOptions = allGroups.map(g => ({
    value: g.id,
    label: `${getName(g.nameI18n)} (${g.code})`,
  }))

  const saveGroupsMutation = useMutation({
    mutationFn: async () => {
      await api.put(`/navigation/channel-categories/${id}/groups`, {
        productGroupIds: groupIds,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['channel-category', id] })
      setSelectedGroupIds(null)
    },
  })

  // ── Products tab ──────────────────────────────────────────────────────────

  const [prodPage, setProdPage] = useState(1)
  const { data: prodData, isLoading: prodLoading, refetch: refetchProds } =
    useQuery<PagedResult<ProductItem>>({
      queryKey: ['channel-category-products', id, prodPage],
      queryFn: async () => {
        const { data } = await api.get(
          `/navigation/channel-categories/${id}/products?page=${prodPage}&pageSize=20`
        )
        return data.data
      },
      enabled: !!id && activeTab === 'Ürünler',
    })

  const [addOpen, setAddOpen] = useState(false)
  const [addProductId, setAddProductId] = useState('')
  const [addSortOrder, setAddSortOrder] = useState(0)
  const [addIsExcluded, setAddIsExcluded] = useState(false)

  const { data: allProducts = [] } = useQuery<{ id: string; code: string; nameI18n: Record<string, string> }[]>({
    queryKey: ['products-simple'],
    queryFn: async () => {
      const { data } = await api.get('/catalog/products?activeOnly=false&pageSize=500')
      return data.data?.items ?? []
    },
    enabled: addOpen,
  })

  const productOptions = useMemo(
    () => allProducts.map(p => ({ value: p.id, label: `${getName(p.nameI18n, p.code)} (${p.code})` })),
    [allProducts],
  )

  const addProductMutation = useMutation({
    mutationFn: async () => {
      await api.post(`/navigation/channel-categories/${id}/products`, {
        productId:  addProductId,
        sortOrder:  addSortOrder,
        isExcluded: addIsExcluded,
      })
    },
    onSuccess: () => {
      setAddOpen(false)
      setAddProductId('')
      setAddSortOrder(0)
      setAddIsExcluded(false)
      refetchProds()
    },
  })

  const removeProductMutation = useMutation({
    mutationFn: async (productId: string) =>
      api.delete(`/navigation/channel-categories/${id}/products/${productId}`),
    onSuccess: () => refetchProds(),
  })

  const syncMutation = useMutation({
    mutationFn: async () => {
      const { data } = await api.post(`/navigation/channel-categories/${id}/sync`)
      return data.data?.addedCount as number
    },
    onSuccess: () => refetchProds(),
  })

  const sourceLang = languages.find(l => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'
  const nameFields = useMemo(() => [{ key: 'name', labels: FL.categoryName, required: true }], [])
  const hasFilter = form?.fillType === 'filter' || form?.fillType === 'mixed'

  if (isLoading || !cat || !form) return <PageSpinner />

  const coverage = cat.coverage
  const isCovered = coverage.coveredGroupCount >= coverage.assignedGroupCount && coverage.assignedGroupCount > 0

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center gap-3 mb-6">
        <button
          onClick={() => navigate('/storefront/channel-categories')}
          className="w-8 h-8 flex items-center justify-center rounded-xl transition-colors"
          style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}
        >
          <ArrowLeft size={15} />
        </button>
        <div className="flex-1 min-w-0">
          <h1 className="text-xl font-bold truncate" style={{ color: 'var(--text)' }}>
            {getName(cat.nameI18n, cat.slug)}
          </h1>
          <div className="flex items-center gap-2 mt-0.5">
            <code className="text-xs" style={{ color: 'var(--text-s)' }}>/{cat.slug}</code>
            <Badge variant={cat.status === 'published' ? 'success' : cat.status === 'draft' ? 'warning' : 'neutral'}>
              {cat.status === 'published' ? 'Yayında' : cat.status === 'draft' ? 'Taslak' : 'Arşiv'}
            </Badge>
          </div>
        </div>
        {/* Coverage badge */}
        <div className="flex items-center gap-1.5 text-xs px-2.5 py-1.5 rounded-xl"
          style={{
            background: isCovered ? '#dcfce7' : '#fef9c3',
            color: isCovered ? '#16a34a' : '#854d0e',
          }}>
          {isCovered
            ? <><CheckCircle size={12} /> Kapsam tam</>
            : <><AlertTriangle size={12} /> {coverage.uncoveredGroupIds.length} grup kapsam dışı</>
          }
        </div>
      </div>

      {/* Tabs */}
      <div className="tab-scroll mb-6">
        {TABS.map(tab => (
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
          {/* Ad */}
          <div>
            <label className="flbl mb-2">Ad</label>
            <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
              <I18nField
                sourceLang={sourceLang}
                languages={languages}
                fields={nameFields}
                values={buildI18nValues(form.nameI18n, languages)}
                onChange={(lang, _key, value) =>
                  setForm(f => f && ({ ...f, nameI18n: { ...f.nameI18n, [lang]: value } }))
                }
              />
            </div>
          </div>

          {/* URL */}
          <div>
            <label className="flbl">URL</label>
            <div className="flex items-center rounded-xl overflow-hidden"
              style={{ border: '1px solid var(--border)', background: 'var(--surface2)' }}>
              <span className="px-3 text-sm select-none" style={{ color: 'var(--text-s)', borderRight: '1px solid var(--border)' }}>/</span>
              <input
                type="text"
                value={form.slug}
                onChange={e => setForm(f => f && ({ ...f, slug: e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '') }))}
                className="flex-1 px-3 py-2 text-sm font-mono bg-transparent outline-none"
                style={{ color: 'var(--text)' }}
              />
            </div>
          </div>

          {/* Dolum Tipi + Durum */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="flbl">Dolum Tipi</label>
              <SearchableSelect
                value={form.fillType}
                onChange={v => v && setForm(f => f && ({ ...f, fillType: v }))}
                options={FILL_TYPES}
                hasValue
              />
            </div>
            <div>
              <label className="flbl">Durum</label>
              <SearchableSelect
                value={form.status}
                onChange={v => v && setForm(f => f && ({ ...f, status: v }))}
                options={STATUS_OPTIONS}
                hasValue
              />
            </div>
          </div>

          {/* Filtre tanımı */}
          {hasFilter && (
            <div>
              <label className="flbl mb-2">Filtre Tanımı</label>
              <FilterBuilder
                value={form.filterDef}
                onChange={filterDef => setForm(f => f && ({ ...f, filterDef }))}
              />
            </div>
          )}

          {/* Badge + Görsel */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="flbl">Badge Etiketi</label>
              <input
                type="text"
                value={form.badgeLabel}
                onChange={e => setForm(f => f && ({ ...f, badgeLabel: e.target.value }))}
                className="w-full px-3 py-2 rounded-xl text-sm"
                style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}
                placeholder="Yeni, İndirim…"
              />
            </div>
            <div>
              <label className="flbl">Sıra</label>
              <IntegerInput
                value={form.sortOrder}
                onChange={v => setForm(f => f && ({ ...f, sortOrder: v ?? 0 }))}
              />
            </div>
          </div>

          <div>
            <label className="flbl">Görsel URL</label>
            <input
              type="text"
              value={form.displayImageUrl}
              onChange={e => setForm(f => f && ({ ...f, displayImageUrl: e.target.value }))}
              className="w-full px-3 py-2 rounded-xl text-sm"
              style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text)' }}
              placeholder="https://…"
            />
          </div>

          <div className="flex justify-end pt-2" style={{ borderTop: '1px solid var(--border)' }}>
            <Button onClick={() => saveMutation.mutate()} loading={saveMutation.isPending}>
              <Save size={14} /> Kaydet
            </Button>
          </div>
        </div>
      )}

      {/* ── Gruplar Tab ───────────────────────────────────────────────────── */}
      {activeTab === 'Gruplar' && (
        <div className="space-y-4">
          <div className="card space-y-4">
            <div>
              <label className="flbl mb-1">Sorumlu Ürün Grupları</label>
              <p className="text-xs mb-3" style={{ color: 'var(--text-s)' }}>
                Bu kategorinin ürünlerini göstermekten sorumlu olduğu gruplar — coverage kontrolü buradan hesaplanır.
              </p>
              <div className="space-y-2">
                {groupIds.map(gid => {
                  const grp = allGroups.find(g => g.id === gid)
                  return (
                    <div key={gid} className="flex items-center justify-between px-3 py-2 rounded-xl"
                      style={{ background: 'var(--surface2)' }}>
                      <span className="text-sm" style={{ color: 'var(--text)' }}>
                        {grp ? getName(grp.nameI18n, grp.code) : gid}
                      </span>
                      <button
                        onClick={() => setSelectedGroupIds(groupIds.filter(id => id !== gid))}
                        className="text-xs px-2 py-1 rounded-lg"
                        style={{ color: '#ef4444' }}
                      >
                        Kaldır
                      </button>
                    </div>
                  )
                })}
              </div>
            </div>

            {/* Grup ekle */}
            <div>
              <label className="flbl mb-1">Grup Ekle</label>
              <SearchableSelect
                value=""
                onChange={v => {
                  if (v && !groupIds.includes(v))
                    setSelectedGroupIds([...groupIds, v])
                }}
                options={groupOptions.filter(o => !groupIds.includes(o.value))}
                placeholder="Grup seçin…"
                hasValue={false}
              />
            </div>

            {selectedGroupIds !== null && (
              <div className="flex justify-end pt-2" style={{ borderTop: '1px solid var(--border)' }}>
                <Button onClick={() => saveGroupsMutation.mutate()} loading={saveGroupsMutation.isPending}>
                  <Save size={14} /> Kaydet
                </Button>
              </div>
            )}
          </div>

          {/* Coverage özeti */}
          <div className="card">
            <h3 className="text-sm font-semibold mb-3" style={{ color: 'var(--text)' }}>Kanal Kapsam Özeti</h3>
            <div className="grid grid-cols-3 gap-4 text-center">
              <div>
                <div className="text-2xl font-bold" style={{ color: 'var(--text)' }}>{coverage.assignedGroupCount}</div>
                <div className="text-xs" style={{ color: 'var(--text-s)' }}>Kanalda Aktif Grup</div>
              </div>
              <div>
                <div className="text-2xl font-bold" style={{ color: '#16a34a' }}>{coverage.coveredGroupCount}</div>
                <div className="text-xs" style={{ color: 'var(--text-s)' }}>Kapsanan</div>
              </div>
              <div>
                <div className="text-2xl font-bold" style={{ color: coverage.uncoveredGroupIds.length > 0 ? '#f59e0b' : '#16a34a' }}>
                  {coverage.uncoveredGroupIds.length}
                </div>
                <div className="text-xs" style={{ color: 'var(--text-s)' }}>Kapsam Dışı</div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ── Ürünler Tab ───────────────────────────────────────────────────── */}
      {activeTab === 'Ürünler' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <p className="text-sm" style={{ color: 'var(--text-s)' }}>{prodData?.totalCount ?? 0} ürün</p>
            <div className="flex items-center gap-2">
              {hasFilter && (
                <Button variant="secondary" onClick={() => syncMutation.mutate()} loading={syncMutation.isPending}>
                  <RefreshCw size={14} /> Sync
                </Button>
              )}
              <Button onClick={() => setAddOpen(true)}>
                <Plus size={14} /> Ürün Ekle
              </Button>
            </div>
          </div>

          {syncMutation.isSuccess && (
            <div className="px-3 py-2 rounded-xl text-sm" style={{ background: '#dcfce7', color: '#16a34a' }}>
              Sync tamamlandı — {syncMutation.data} yeni ürün eklendi.
            </div>
          )}

          <div className="card overflow-hidden p-0">
            <table className="w-full">
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                  <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Ürün</th>
                  <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Sıra</th>
                  <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Tip</th>
                  <th className="w-12" />
                </tr>
              </thead>
              <tbody>
                {prodLoading && (
                  <tr><td colSpan={4} className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</td></tr>
                )}
                {!prodLoading && !prodData?.items.length && (
                  <tr><td colSpan={4} className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Henüz ürün yok</td></tr>
                )}
                {prodData?.items.map(p => (
                  <tr key={p.productId} style={{ borderBottom: '1px solid var(--border)' }}
                    className={cn(p.isExcluded && 'opacity-50')}>
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
                      {p.isExcluded
                        ? <Badge variant="neutral">Hariç</Badge>
                        : <Badge variant="info">Dahil</Badge>
                      }
                    </td>
                    <td className="px-4 py-3 text-center">
                      <button
                        onClick={() => removeProductMutation.mutate(p.productId)}
                        className="w-7 h-7 flex items-center justify-center rounded-lg transition-colors hover:bg-red-50"
                        style={{ color: '#ef4444' }}
                      >
                        <Trash2 size={13} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {(prodData?.totalCount ?? 0) > 20 && (
              <div className="flex items-center justify-center gap-2 p-4" style={{ borderTop: '1px solid var(--border)' }}>
                <Button variant="secondary" disabled={prodPage <= 1} onClick={() => setProdPage(p => p - 1)} size="sm">←</Button>
                <span className="text-sm" style={{ color: 'var(--text-s)' }}>Sayfa {prodPage}</span>
                <Button variant="secondary" disabled={(prodData?.items.length ?? 0) < 20} onClick={() => setProdPage(p => p + 1)} size="sm">→</Button>
              </div>
            )}
          </div>
        </div>
      )}

      {/* ── SEO Tab ───────────────────────────────────────────────────────── */}
      {activeTab === 'SEO' && (
        <div className="card space-y-4">
          <p className="text-sm" style={{ color: 'var(--text-s)' }}>SEO alanları yakında eklenecek.</p>
        </div>
      )}

      {/* Add Product Modal */}
      <Modal
        open={addOpen}
        onClose={() => setAddOpen(false)}
        title="Ürün Ekle"
        footer={
          <>
            <Button variant="secondary" onClick={() => setAddOpen(false)}>İptal</Button>
            <Button onClick={() => addProductMutation.mutate()} loading={addProductMutation.isPending} disabled={!addProductId}>
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
              onChange={v => v && setAddProductId(v)}
              options={productOptions}
              placeholder="Ürün seçin…"
              hasValue={!!addProductId}
            />
          </div>
          <div>
            <label className="flbl">Sıra</label>
            <IntegerInput value={addSortOrder} onChange={v => setAddSortOrder(v ?? 0)} />
          </div>
          <div className="flex items-center gap-3">
            <input
              type="checkbox"
              id="isExcluded"
              checked={addIsExcluded}
              onChange={e => setAddIsExcluded(e.target.checked)}
              className="w-4 h-4 rounded"
            />
            <label htmlFor="isExcluded" className="text-sm" style={{ color: 'var(--text-m)' }}>
              Hariç tut (filtre sonucundan çıkar)
            </label>
          </div>
        </div>
      </Modal>
    </div>
  )
}
