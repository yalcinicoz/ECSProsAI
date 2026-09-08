import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Plus, Package } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { errText } from '@/components/ui/DataTable.utils'
import { DataGrid, useGridState, type GridColumn, type GridFilterField } from '@/components/grid'

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

  const named = { activeOnly: String(activeOnly), sort: 'newest' }

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
    { key: 'group', header: 'GRUP', filters: [{ field: 'sourceType', label: 'Kaynak', type: 'enum', multiple: true, options: SOURCE_TYPE_OPTIONS }], priority: 2, cell: (item) => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{groupMap.get(item.productGroupId) ?? '—'}</span> },
    { key: 'variantCount', header: 'VARYANT', priority: 3, align: 'center', sortable: true, filter: { type: 'number', label: 'Varyant sayısı' }, cell: (item) => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{item.variantCount}</span> },
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

      <DataGrid<ProductListItem>
        gridId="products"
        views
        grid={grid}
        columns={columns}
        extraFilters={extraFilters}
        search={{ placeholder: 'Ürün adı, kod, tedarikçi ürün kodu…' }}
        rows={items}
        totalCount={totalCount}
        loading={isLoading}
        fetching={isFetching}
        error={error ? errText(error) : null}
        onRowClick={(item) => navigate(`/catalog/products/${item.code}`)}
        empty={grid.state.search ? `"${grid.state.search}" için ürün bulunamadı` : 'Henüz ürün eklenmemiş'}
        minWidth={640}
        export={{ endpoint: '/catalog/products/export', named: () => ({ activeOnly: String(activeOnly) }), fallbackFileName: 'urunler.xlsx' }}
      />
    </div>
  )
}
