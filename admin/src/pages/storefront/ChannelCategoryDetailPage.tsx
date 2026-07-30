import { useState, useMemo } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Plus, Trash2, RefreshCw, Save, AlertTriangle, CheckCircle, ImageIcon } from 'lucide-react'
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

interface GroupWithShowcase {
  productGroupId: string
  showcaseProductId: string | null
}

interface CategoryDetail {
  id: string
  firmPlatformId: string
  parentId: string | null
  nameI18n: Record<string, string>
  slug: string
  status: string
  fillType: string
  listingMode: string
  filterDef: Record<string, unknown> | null
  sortOrder: number
  displayImageUrl: string | null
  badgeLabel: string | null
  metaTitleI18n: Record<string, string> | null
  metaDescriptionI18n: Record<string, string> | null
  ogImageUrl: string | null
  ogTitleI18n: Record<string, string> | null
  groups: GroupWithShowcase[]
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
  productGroupId: string | null
}

interface ProductGroup {
  id: string
  code: string
  nameI18n: Record<string, string>
}

interface SimpleProduct {
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

const LISTING_MODES = [
  { value: 'color', label: 'Renk (Ana Varyant) Bazlı Liste' },
  { value: 'model', label: 'Model Bazlı Liste' },
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
    listingMode: string
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
      listingMode:    cat.listingMode ?? 'color',
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
        listingMode:      form.listingMode,
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
    onSuccess: async () => {
      // Listeleme tipi (color/model) değişince ürün listesi de tazelenmeli — aksi halde
      // model modunun boş sonucu ekranda kalıp renk moduna dönünce "ürünler geri gelmiyor".
      queryClient.invalidateQueries({ queryKey: ['channel-category-products', id] })
      setProdPage(1)
      // ÖNCE cat refetch'ini BEKLE, SONRA formu sıfırla. Aksi halde form bayat cat ile
      // yeniden başlar (kaydedilen listingMode görünmez; F5'e kadar eski değer kalır).
      await queryClient.invalidateQueries({ queryKey: ['channel-category', id] })
      setFormInited(false)
    },
  })

  // ── Product Groups tab ────────────────────────────────────────────────────

  // Local state for groups (productGroupId → showcaseProductId)
  const [localGroups, setLocalGroups] = useState<GroupWithShowcase[] | null>(null)
  const activeGroups: GroupWithShowcase[] = localGroups ?? cat?.groups ?? []

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
        groups: activeGroups.map(g => ({
          productGroupId:    g.productGroupId,
          showcaseProductId: g.showcaseProductId ?? null,
        })),
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['channel-category', id] })
      setLocalGroups(null)
    },
  })

  // Products for showcase selector (load per group when model mode)
  const [showcaseProductsCache, setShowcaseProductsCache] = useState<Record<string, SimpleProduct[]>>({})

  async function loadGroupProducts(groupId: string) {
    if (showcaseProductsCache[groupId]) return
    try {
      const { data } = await api.get(`/catalog/product-groups/${groupId}/products?activeOnly=false&pageSize=200`)
      const products: SimpleProduct[] = (data.data?.items ?? []).map((p: SimpleProduct) => p)
      setShowcaseProductsCache(prev => ({ ...prev, [groupId]: products }))
    } catch {
      setShowcaseProductsCache(prev => ({ ...prev, [groupId]: [] }))
    }
  }

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
  const isModelMode = (form?.listingMode ?? cat?.listingMode ?? 'color') === 'model'

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
            <Badge variant={isModelMode ? 'info' : 'neutral'}>
              {isModelMode ? 'Model Bazlı' : 'Renk Bazlı'}
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

          {/* Dolum Tipi + Durum + Listeleme Tipi */}
          <div className="grid grid-cols-3 gap-4">
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
            <div>
              <label className="flbl">Listeleme Tipi</label>
              <SearchableSelect
                value={form.listingMode}
                onChange={v => v && setForm(f => f && ({ ...f, listingMode: v }))}
                options={LISTING_MODES}
                hasValue
              />
            </div>
          </div>

          {/* Listeleme modu açıklamaları */}
          {form.listingMode === 'color' && (
            <div className="flex items-start gap-2.5 px-4 py-3 rounded-xl text-sm"
              style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', color: '#15803d' }}>
              <ImageIcon size={16} className="mt-0.5 flex-shrink-0" />
              <span>
                <strong>Renk (Ana Varyant) Bazlı Liste:</strong> Her ürün (renk varyantı) ayrı bir kart olarak listelenir.
                Trendyol gibi moda/tekstil kategorileri için varsayılan moddur.
              </span>
            </div>
          )}
          {form.listingMode === 'model' && (
            <div className="flex items-start gap-2.5 px-4 py-3 rounded-xl text-sm"
              style={{ background: '#eff6ff', border: '1px solid #bfdbfe', color: '#1e40af' }}>
              <ImageIcon size={16} className="mt-0.5 flex-shrink-0" />
              <span>
                <strong>Model Bazlı Liste:</strong> Her ürün grubu tek bir kart olarak görünür.
                "Gruplar" sekmesinde her grup için <strong>vitrin ürünü</strong> seçin — hangi renk/varyant kartı temsil etsin.
                Seçilmezse sistem ilk aktif ürünü otomatik kullanır.
              </span>
            </div>
          )}

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

          {/* Badge + Sıra */}
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
                {isModelMode && <> <strong>Model bazlı listeleme</strong> açık: her grup için vitrin ürünü seçin.</>}
              </p>
              <div className="space-y-2">
                {activeGroups.map(g => {
                  const grp = allGroups.find(ag => ag.id === g.productGroupId)
                  const groupProducts = showcaseProductsCache[g.productGroupId] ?? []
                  const productSelectOptions = groupProducts.map(p => ({
                    value: p.id,
                    label: `${getName(p.nameI18n, p.code)} (${p.code})`,
                  }))

                  return (
                    <div key={g.productGroupId} className="rounded-xl overflow-hidden"
                      style={{ border: '1px solid var(--border)' }}>
                      {/* Grup satırı */}
                      <div className="flex items-center justify-between px-3 py-2.5"
                        style={{ background: 'var(--surface2)' }}>
                        <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>
                          {grp ? getName(grp.nameI18n, grp.code) : g.productGroupId}
                        </span>
                        <button
                          onClick={() => setLocalGroups(activeGroups.filter(ag => ag.productGroupId !== g.productGroupId))}
                          className="text-xs px-2 py-1 rounded-lg"
                          style={{ color: '#ef4444' }}
                        >
                          Kaldır
                        </button>
                      </div>

                      {/* Vitrin ürünü — sadece model modunda */}
                      {isModelMode && (
                        <div className="px-3 py-2.5 flex items-center gap-3"
                          style={{ borderTop: '1px solid var(--border)' }}>
                          <span className="text-xs flex-shrink-0" style={{ color: 'var(--text-s)', minWidth: 90 }}>
                            Vitrin ürünü
                          </span>
                          <div className="flex-1" onClick={() => loadGroupProducts(g.productGroupId)}>
                            <SearchableSelect
                              value={g.showcaseProductId ?? ''}
                              onChange={v => setLocalGroups(
                                activeGroups.map(ag =>
                                  ag.productGroupId === g.productGroupId
                                    ? { ...ag, showcaseProductId: v || null }
                                    : ag
                                )
                              )}
                              options={productSelectOptions}
                              placeholder="Otomatik (ilk aktif ürün)"
                              hasValue={!!g.showcaseProductId}
                            />
                          </div>
                          {g.showcaseProductId && (
                            <button
                              onClick={() => setLocalGroups(
                                activeGroups.map(ag =>
                                  ag.productGroupId === g.productGroupId
                                    ? { ...ag, showcaseProductId: null }
                                    : ag
                                )
                              )}
                              className="text-xs px-2 py-1 rounded-lg flex-shrink-0"
                              style={{ color: 'var(--text-s)' }}
                            >
                              Temizle
                            </button>
                          )}
                        </div>
                      )}
                    </div>
                  )
                })}
                {activeGroups.length === 0 && (
                  <p className="text-sm py-3 text-center" style={{ color: 'var(--text-s)' }}>
                    Henüz grup eklenmedi
                  </p>
                )}
              </div>
            </div>

            {/* Grup ekle */}
            <div>
              <label className="flbl mb-1">Grup Ekle</label>
              <SearchableSelect
                value=""
                onChange={v => {
                  if (v && !activeGroups.find(g => g.productGroupId === v)) {
                    const newGroup: GroupWithShowcase = { productGroupId: v, showcaseProductId: null }
                    setLocalGroups([...activeGroups, newGroup])
                    if (isModelMode) loadGroupProducts(v)
                  }
                }}
                options={groupOptions.filter(o => !activeGroups.find(g => g.productGroupId === o.value))}
                placeholder="Grup seçin…"
                hasValue={false}
              />
            </div>

            {localGroups !== null && (
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
            <p className="text-sm" style={{ color: 'var(--text-s)' }}>
              {prodData?.totalCount ?? 0} {isModelMode ? 'model' : 'ürün'}
            </p>
            <div className="flex items-center gap-2">
              {hasFilter && (
                <Button variant="secondary" onClick={() => syncMutation.mutate()} loading={syncMutation.isPending}>
                  <RefreshCw size={14} /> Sync
                </Button>
              )}
              {!isModelMode && (
                <Button onClick={() => setAddOpen(true)}>
                  <Plus size={14} /> Ürün Ekle
                </Button>
              )}
            </div>
          </div>

          {isModelMode && (
            <div className="flex items-center gap-2 px-3 py-2.5 rounded-xl text-sm"
              style={{ background: '#eff6ff', border: '1px solid #bfdbfe', color: '#1e40af' }}>
              <ImageIcon size={15} className="flex-shrink-0" />
              Model bazlı modda vitrin ürünleri görüntüleniyor. Vitrin ürünlerini "Gruplar" sekmesinden yönetebilirsiniz.
            </div>
          )}

          {syncMutation.isSuccess && (
            <div className="px-3 py-2 rounded-xl text-sm" style={{ background: '#dcfce7', color: '#16a34a' }}>
              Sync tamamlandı — kategoride {prodData?.totalCount ?? 0} {isModelMode ? 'model' : 'ürün'} listelenecek.
            </div>
          )}

          <div className="card overflow-hidden p-0">
            <table className="w-full">
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                  <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Ürün</th>
                  <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>
                    {isModelMode ? 'Grup' : 'Sıra'}
                  </th>
                  <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Tip</th>
                  {!isModelMode && <th className="w-12" />}
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
                    <td className="px-4 py-3 text-center text-sm" style={{ color: 'var(--text-m)' }}>
                      {isModelMode
                        ? (p.productGroupId
                            ? <code className="text-xs">{p.productGroupId.slice(0, 8)}…</code>
                            : '—')
                        : p.sortOrder
                      }
                    </td>
                    <td className="px-4 py-3 text-center">
                      {isModelMode
                        ? <Badge variant="info">Vitrin</Badge>
                        : p.isExcluded
                          ? <Badge variant="neutral">Hariç</Badge>
                          : <Badge variant="info">Dahil</Badge>
                      }
                    </td>
                    {!isModelMode && (
                      <td className="px-4 py-3 text-center">
                        <button
                          onClick={() => removeProductMutation.mutate(p.productId)}
                          className="w-7 h-7 flex items-center justify-center rounded-lg transition-colors hover:bg-red-50"
                          style={{ color: '#ef4444' }}
                        >
                          <Trash2 size={13} />
                        </button>
                      </td>
                    )}
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
