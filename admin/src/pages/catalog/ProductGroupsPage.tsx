import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Plus, FolderOpen } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { PermissionGuard, ReadOnlyBadge } from '@/components/ui/PermissionGuard'

import { CreateProductGroupModal } from './CreateProductGroupModal'

const PLATFORM_PERM = 'catalog.platform.manage'

// ── Types ────────────────────────────────────────────────────────────────────

export interface ProductGroupAttribute {
  id: string
  attributeTypeId: string
  attributeTypeCode: string
  attributeTypeNameI18n: Record<string, string>
  isVariant: boolean
  isRequired: boolean
  isPrimaryAxis: boolean
  sortOrder: number
  defaultAttributeValueId?: string | null
}

export interface ProductGroupAxisSubAttribute {
  id: string
  axisAttributeTypeId: string
  axisAttributeTypeCode: string
  axisAttributeTypeNameI18n: Record<string, string>
  subAttributeTypeId: string
  subAttributeTypeCode: string
  subAttributeTypeNameI18n: Record<string, string>
  isRequired: boolean
  sortOrder: number
}

export interface ProductGroup {
  id: string
  code: string
  nameI18n: Record<string, string>
  isActive: boolean
  sortOrder: number
  hasProducts: boolean
  attributes: ProductGroupAttribute[]
  axisSubAttributes?: ProductGroupAxisSubAttribute[]
}

// ── Component ─────────────────────────────────────────────────────────────────

/** DataGrid satırı — /catalog/product-groups/grid (özellik/eksen ayrıntısı liste ekranında gerekmez). */
interface ProductGroupRow {
  id: string
  code: string
  nameI18n: Record<string, string>
  isActive: boolean
  sortOrder: number
  attributeCount: number
  variantCount: number
  productCount: number
  hasProducts: boolean
  createdAt: string
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

export function ProductGroupsPage() {
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (ProductGroupGrid.Schema) + Excel + görünümler.
  // ★ Ayrı uç: /catalog/product-groups TÜM grupları döner ve dropdown kaynağıdır; liste ekranı
  // sayfalı /product-groups/grid ucunu kullanır (bkz. ProductGroupGrid açıklaması).
  const navigate = useNavigate()
  const [sp] = useSearchParams()
  const activeOnly = sp.get('activeOnly') === 'true'
  const grid = useGridState('product-groups', { defaultPageSize: 20, defaultSort: 'sortOrder', defaultDir: 'asc' })
  const [createOpen, setCreateOpen] = useState(false)

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<ProductGroupRow>>({
    queryKey: ['product-groups-grid', activeOnly, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/catalog/product-groups/grid?${grid.toParams({ activeOnly: activeOnly ? 'true' : undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const rows = data?.items ?? []
  const ad = (g: ProductGroupRow) => g.nameI18n?.['tr'] ?? Object.values(g.nameI18n ?? {})[0] ?? g.code

  const columns: GridColumn<ProductGroupRow>[] = [
    { key: 'name', header: 'AD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 220,
      filter: { type: 'text', label: 'Ad' },
      cell: g => <div className="flex items-center gap-2">
        <FolderOpen size={14} style={{ color: 'var(--brand)', flexShrink: 0 }} />
        <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{ad(g)}</span>
      </div> },
    { key: 'code', header: 'KOD', priority: 1, sortable: true, filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      cell: g => <code className="text-xs px-2 py-0.5 rounded-md font-mono"
        style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>{g.code}</code> },
    { key: 'attributeCount', header: 'ÖZELLİK', priority: 2, align: 'center', sortable: true, filter: { type: 'number', label: 'Özellik sayısı' },
      cell: g => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{g.attributeCount}</span> },
    { key: 'variantCount', header: 'VARYANT', priority: 2, align: 'center', sortable: true, filter: { type: 'number', label: 'Varyant ekseni' },
      cell: g => g.variantCount > 0
        ? <Badge variant="default">{g.variantCount} eksen</Badge>
        : <span className="text-sm" style={{ color: 'var(--text-s)' }}>—</span> },
    { key: 'productCount', header: 'ÜRÜN', priority: 2, align: 'center', sortable: true, filter: { type: 'number', label: 'Ürün sayısı' },
      filters: [{ field: 'hasProducts', label: 'Ürünü olan', type: 'boolean' }],
      cell: g => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{g.productCount}</span> },
    { key: 'sortOrder', header: 'SIRA', priority: 3, align: 'center', sortable: true, filter: { type: 'number', label: 'Sıra' },
      cell: g => <span className="text-sm" style={{ color: 'var(--text-s)' }}>{g.sortOrder}</span> },
    { key: 'isActive', header: 'DURUM', priority: 1, lockVisible: true, align: 'center', sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [{ field: 'createdAt', label: 'Oluşturma', type: 'date' }],
      cell: g => <Badge variant={g.isActive ? 'success' : 'neutral'}>{g.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'detail', header: '', priority: 3, align: 'right', exportable: false,
      cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Ürün Grupları</h1>
            <PermissionGuard permission={PLATFORM_PERM} fallback={<ReadOnlyBadge />} />
          </div>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}
          </p>
        </div>
        <PermissionGuard permission={PLATFORM_PERM}>
          <Button onClick={() => setCreateOpen(true)}><Plus size={14} /> Yeni Grup</Button>
        </PermissionGuard>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', !activeOnly && 'active')} onClick={() => grid.mutate(n => n.delete('activeOnly'))}>Tümü</button>
        <button className={cn('stab', activeOnly && 'active')} onClick={() => grid.mutate(n => n.set('activeOnly', 'true'))}>Aktif</button>
      </div>

      <DataGrid<ProductGroupRow>
        gridId="product-groups"
        views
        grid={grid}
        columns={columns}
        rows={rows}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={g => navigate(`/catalog/product-groups/${g.id}`)}
        empty="Ürün grubu bulunamadı."
        search={{ placeholder: 'Grup adı veya koduyla ara…' }}
        minWidth={860}
        export={{ endpoint: '/catalog/product-groups/export', named: () => ({ activeOnly: activeOnly ? 'true' : undefined }), fallbackFileName: 'urun-gruplari.xlsx' }}
        compact={{
          title: g => ad(g),
          subtitle: g => `${g.code} · ${g.attributeCount} özellik${g.variantCount > 0 ? ` · ${g.variantCount} eksen` : ''}`,
          right: g => `${g.productCount} ürün`,
          badge: g => <Badge variant={g.isActive ? 'success' : 'neutral'}>{g.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />

      {createOpen && (
        <CreateProductGroupModal
          onClose={() => setCreateOpen(false)}
          onCreated={(group) => {
            setCreateOpen(false)
            navigate(`/catalog/product-groups/${group.id}`)
          }}
        />
      )}
    </div>
  )
}
