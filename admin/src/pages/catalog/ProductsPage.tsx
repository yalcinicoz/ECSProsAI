import { useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Plus, Package } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { errText } from '@/components/ui/DataTable.utils'
import { DataGrid, FilterAccordion, useGridState, type GridColumn, type GridFilterField } from '@/components/grid'

// Ürünler — DataGrid göçü (docs/datagrid-standardi-plani.md F4). Tümü/Satışta anahtarı `?tab=` ile URL'de (activeOnly named parametresi
// korunur); filtre/arama/sıralama/sayfa URL'de, kolon tercihleri localStorage'da. Sunucu beyaz listesi: ProductGrid.Schema.

// ── Types ─────────────────────────────────────────────────────────────────────

interface ProductListItem {
  id: string
  code: string
  nameI18n: Record<string, string>
  productGroupId: string
  isActive: boolean
  variantCount: number
  // 2026-09-11 kapsamlı filtre kolonları (mv_product_stats, 5 dk tazelik)
  imageState?: 'none' | 'partial' | 'full' | string
  imageCount?: number
  stockQuantity?: number
  stockAvailable?: number
}

interface PagedResult {
  items: ProductListItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

interface ProductGroup {
  id: string
  code: string
  nameI18n: Record<string, string>
}

function getName(item: { nameI18n: Record<string, string>; code: string }): string {
  return item.nameI18n['tr'] ?? item.nameI18n[Object.keys(item.nameI18n)[0]] ?? item.code
}

const SOURCE_TYPE_OPTIONS = [
  { value: 'own', label: 'Kendi' },
  { value: 'seller', label: 'Satıcı' },
  { value: 'supply', label: 'Dış tedarik' },
]
// Görsel durumu RENK bazlı (grubun birincil ekseni): Var = tüm renklerde, Kısmi = bazı renklerde, Yok = hiç görsel yok
const IMAGE_STATE_OPTIONS = [
  { value: 'full', label: 'Var' },
  { value: 'partial', label: 'Kısmi (bazı renklerinde var)' },
  { value: 'none', label: 'Yok' },
]
const IMAGE_STATE_BADGE: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' | 'danger' }> = {
  full: { label: 'Var', variant: 'success' }, partial: { label: 'Kısmi', variant: 'warning' }, none: { label: 'Yok', variant: 'danger' },
}

// ── Component ─────────────────────────────────────────────────────────────────

export function ProductsPage() {
  const navigate = useNavigate()
  const grid = useGridState('products', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })
  const [sp] = useSearchParams()
  const activeOnly = sp.get('tab') === 'active'

  const { data: groups = [] } = useQuery<ProductGroup[]>({
    queryKey: ['product-groups', false],
    queryFn: async () => (await api.get('/catalog/product-groups?activeOnly=false')).data.data,
    staleTime: 5 * 60 * 1000,
  })

  const groupMap = useMemo(() => {
    const m = new Map<string, string>()
    groups.forEach((g) => m.set(g.id, getName(g)))
    return m
  }, [groups])

  const groupOptions = useMemo(() => groups.map(g => ({ value: g.id, label: getName(g) })).sort((a, b) => a.label.localeCompare(b.label, 'tr')), [groups])

  const named = { activeOnly: String(activeOnly) }   // sıralama grid'den (varsayılan createdAt desc = eski 'newest'); named sort artık gönderilmez (2026-09-08: 'Geçersiz sıralama alanı: newest' düzeltmesi)

  const { data: result, isLoading, isFetching, error } = useQuery<PagedResult>({
    queryKey: ['products', activeOnly, ...grid.queryKey],
    queryFn: async () => (await api.get(`/catalog/products?${grid.toParams(named)}`)).data.data,
    placeholderData: (prev) => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const items = result?.items ?? []
  const totalCount = result?.totalCount ?? 0

  const extraFilters: GridFilterField[] = useMemo(() => [
    { key: 'productGroupId', label: 'Ürün grubu', type: 'enum', options: groupOptions, quick: true },
  ], [groupOptions])

  // Kapsamlı (gelişmiş) filtre — eski panel /urun/urun-yonetim'in filtre setinin karşılığı (2026-09-11):
  // kod/ad/tedarikçi kodu/barkod, grup/kaynak, satış durumu, görsel durumu (var/kısmi/yok), stok min-maks (fiziksel + satılabilir),
  // fiyat, varyant sayısı, görsel sayısı, oluşturma ve son görsel tarihi. Görsel/stok değerleri 5 dk'da bir yenilenir.
  const advancedFields: GridFilterField[] = useMemo(() => [
    { key: 'code', label: 'Ürün kodu', type: 'text' },
    { key: 'name', label: 'Ürün adı', type: 'text' },
    { key: 'supplierProductCode', label: 'Tedarikçi ürün kodu', type: 'text' },
    { key: 'barcode', label: 'Varyant barkodu', type: 'text', ops: ['eq', 'contains'] },
    { key: 'productGroupId', label: 'Ürün grubu', type: 'enum', options: groupOptions },
    { key: 'sourceType', label: 'Kaynak', type: 'enum', multiple: true, options: SOURCE_TYPE_OPTIONS },
    { key: 'isSaleOpen', label: 'Satışta', type: 'boolean' },
    { key: 'imageState', label: 'Görsel durumu', type: 'enum', options: IMAGE_STATE_OPTIONS },
    { key: 'stock', label: 'Stok adedi (fiziksel)', type: 'number' },
    { key: 'stockAvailable', label: 'Satılabilir stok', type: 'number' },
    { key: 'basePrice', label: 'Liste fiyatı', type: 'number' },
    { key: 'variantCount', label: 'Varyant sayısı', type: 'number' },
    { key: 'imageCount', label: 'Görsel sayısı', type: 'number' },
    { key: 'createdAt', label: 'Oluşturma tarihi', type: 'date' },
    { key: 'lastImageAt', label: 'Son görsel tarihi', type: 'date' },
  ], [groupOptions])

  // Görsel/stok istatistiklerinin tazeliği + "Şimdi yenile"
  const queryClient = useQueryClient()
  const { data: statsInfo } = useQuery<{ refreshedAt: string | null; intervalMinutes: number }>({
    queryKey: ['product-stats-info'],
    queryFn: async () => (await api.get('/catalog/products/stats')).data.data,
    staleTime: 60_000,
  })
  const refreshStats = useMutation({
    mutationFn: async () => (await api.post('/catalog/products/stats/refresh', {})).data.data as { refreshed: boolean },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['product-stats-info'] }); queryClient.invalidateQueries({ queryKey: ['products'] }) },
  })
  const statsNote = (
    <span>
      Görsel/stok bilgisi her {statsInfo?.intervalMinutes ?? 5} dk'da bir yenilenir
      {statsInfo?.refreshedAt ? ` · son: ${new Date(statsInfo.refreshedAt).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}` : ''}.
      {' '}<button type="button" className="underline" style={{ color: 'var(--brand)' }} disabled={refreshStats.isPending}
        onClick={() => refreshStats.mutate()}>{refreshStats.isPending ? 'Yenileniyor…' : 'Şimdi yenile'}</button>
    </span>
  )

  const columns: GridColumn<ProductListItem>[] = [
    { key: 'code', header: 'ÜRÜN', filters: [{ field: 'name', label: 'Ürün adı', type: 'text' }, { field: 'supplierProductCode', label: 'Tedarikçi ürün kodu', type: 'text' }, { field: 'basePrice', label: 'Liste fiyatı', type: 'number' }, { field: 'taxRate', label: 'KDV %', type: 'number' }, { field: 'createdAt', label: 'Oluşturma', type: 'date' }], frozen: true, lockVisible: true, sortable: true, minWidth: 220, filter: { type: 'text', label: 'Ürün kodu' },
      cell: (item) => (
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-xl flex-shrink-0 flex items-center justify-center" style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}>
            <Package size={14} />
          </div>
          <div>
            <div className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{getName(item)}</div>
            <code className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{item.code}</code>
          </div>
        </div>
      ) },
    { key: 'group', header: 'GRUP', sortable: true, filter: { type: 'enum', label: 'Grup', field: 'productGroupId', options: groupOptions }, filters: [{ field: 'sourceType', label: 'Kaynak', type: 'enum', multiple: true, options: SOURCE_TYPE_OPTIONS }], priority: 2, cell: (item) => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{groupMap.get(item.productGroupId) ?? '—'}</span> },
    { key: 'variantCount', header: 'VARYANT', priority: 3, align: 'center', sortable: true, filter: { type: 'number', label: 'Varyant sayısı' }, cell: (item) => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{item.variantCount}</span> },
    { key: 'imageState', header: 'GÖRSEL', priority: 2, align: 'center', sortable: true, filter: { type: 'enum', label: 'Görsel durumu', options: IMAGE_STATE_OPTIONS }, filters: [{ field: 'imageCount', label: 'Görsel sayısı', type: 'number' }, { field: 'lastImageAt', label: 'Son görsel tarihi', type: 'date' }],
      cell: (item) => { const b = IMAGE_STATE_BADGE[item.imageState ?? 'none'] ?? IMAGE_STATE_BADGE.none; return <span className="inline-flex items-center gap-1"><Badge variant={b.variant}>{b.label}</Badge>{(item.imageCount ?? 0) > 0 && <span className="text-xs" style={{ color: 'var(--text-s)' }}>{item.imageCount}</span>}</span> } },
    { key: 'stock', header: 'STOK', priority: 2, align: 'right', sortable: true, filter: { type: 'number', label: 'Stok adedi (fiziksel)' }, filters: [{ field: 'stockAvailable', label: 'Satılabilir stok', type: 'number' }],
      cell: (item) => <span className="text-sm tabular-nums" style={{ color: (item.stockQuantity ?? 0) > 0 ? 'var(--text)' : 'var(--text-s)' }} title={`Satılabilir: ${item.stockAvailable ?? 0}`}>{item.stockQuantity ?? 0}</span> },
    { key: 'isSaleOpen', header: 'DURUM', priority: 1, align: 'center', sortable: true, lockVisible: true, filter: { type: 'boolean', label: 'Satışta' },
      cell: (item) => <Badge variant={item.isActive ? 'success' : 'neutral'}>{item.isActive ? 'Satışta' : 'Satış Kapalı'}</Badge> },
    { key: 'detail', header: '', priority: 3, align: 'right', exportable: false, cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      {/* ── Page header ── */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Ürünler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {result ? `${totalCount.toLocaleString('tr-TR')} ürün${grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}` : '…'}
          </p>
        </div>

        <div className="flex items-center gap-3">
          {/* Tümü / Satışta anahtarı — URL ?tab=active */}
          <div className="flex items-center gap-1 rounded-xl p-1" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            <button
              onClick={() => grid.mutate(n => n.delete('tab'))}
              className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all', !activeOnly ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
              style={!activeOnly ? { color: 'var(--text)' } : {}}
            >Tümü</button>
            <button
              onClick={() => grid.mutate(n => n.set('tab', 'active'))}
              className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all', activeOnly ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
              style={activeOnly ? { color: 'var(--text)' } : {}}
            >Satışta</button>
          </div>

          <Button onClick={() => navigate('/catalog/products/new')}>
            <Plus size={14} /> Yeni Ürün
          </Button>
        </div>
      </div>

      {/* 2026-09-11 (kullanıcı): filtre, eski panel /urun/urun-yonetim gibi listenin ÜSTÜNDE tam satır akordeon — başlangıçta kapalı */}
      <FilterAccordion grid={grid} fields={advancedFields} storageKey="grid:products:adv" title="Filtrele" note={statsNote} />

      <DataGrid<ProductListItem>
        gridId="products"
        views
        grid={grid}
        columns={columns}
        extraFilters={extraFilters}
        advancedFilters={{ fields: advancedFields, layout: 'external' }}
        search={{ placeholder: 'Ürün adı, kod, tedarikçi ürün kodu…' }}
        rows={items}
        totalCount={totalCount}
        loading={isLoading}
        fetching={isFetching}
        error={error ? errText(error) : null}
        onRowClick={(item) => navigate(`/catalog/products/${item.code}`)}
        empty={grid.state.search ? `"${grid.state.search}" için ürün bulunamadı` : 'Henüz ürün eklenmemiş'}
        minWidth={860}
        export={{ endpoint: '/catalog/products/export', named: () => ({ activeOnly: String(activeOnly) }), fallbackFileName: 'urunler.xlsx' }}
        compact={{
          title: item => item.code,
          subtitle: item => getName(item),
          right: item => `${item.variantCount} varyant`,
          badge: item => <Badge variant={item.isActive ? 'success' : 'neutral'}>{item.isActive ? 'Satışta' : 'Kapalı'}</Badge>,
        }}
      />
    </div>
  )
}
