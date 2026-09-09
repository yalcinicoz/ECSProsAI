import { useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'

const SUBMISSION_STATUS_MAP: Record<string, { label: string; variant: BadgeVariant }> = {
  pending:  { label: 'Bekliyor',    variant: 'warning' },
  approved: { label: 'Onaylandı',   variant: 'success' },
  rejected: { label: 'Reddedildi',  variant: 'danger' },
}

interface SubmissionListItem {
  id: string
  supplierId: string
  supplierProductCode: string
  groupCode: string
  name: Record<string, string>
  variantCount: number
  status: string
  productCode: string | null
  reviewNote: string | null
  submittedAt: string
  reviewedAt: string | null
}

const TABS = [
  { key: 'pending', label: 'Bekleyen' },
  { key: 'approved', label: 'Onaylı' },
  { key: 'rejected', label: 'Reddedilen' },
  { key: '', label: 'Tümü' },
]

function nm(i: SubmissionListItem): string {
  return i.name?.['tr'] ?? i.name?.[Object.keys(i.name ?? {})[0]] ?? '—'
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

export function ProductSubmissionsPage() {
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (ProductSubmissionGrid.Schema) + Excel + görünümler.
  const navigate = useNavigate()
  const [sp] = useSearchParams()
  const tab = sp.get('status') ?? 'pending'
  const grid = useGridState('product-submissions', { defaultPageSize: 20, defaultSort: 'submittedAt', defaultDir: 'desc' })

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<SubmissionListItem>>({
    queryKey: ['product-submissions', tab, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/catalog/product-submissions?${grid.toParams({ status: tab || undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const items = data?.items ?? []

  const columns: GridColumn<SubmissionListItem>[] = [
    { key: 'supplierProductCode', header: 'ÜRÜN KODU', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 160,
      filter: { type: 'text', label: 'Tedarikçi ürün kodu', ops: ['startswith', 'contains', 'eq'] },
      cell: i => <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{i.supplierProductCode}</span> },
    { key: 'name', header: 'AD', priority: 1, frozen: true, sortable: true, minWidth: 220,
      filter: { type: 'text', label: 'Ad' },
      cell: i => <span className="text-sm" style={{ color: 'var(--text)' }}>{nm(i)}</span> },
    { key: 'groupCode', header: 'GRUP', priority: 2, sortable: true, filter: { type: 'text', label: 'Grup kodu' },
      cell: i => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{i.groupCode}</span> },
    { key: 'variantCount', header: 'VARYANT', priority: 2, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Varyant sayısı' },
      cell: i => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{i.variantCount}</span> },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(SUBMISSION_STATUS_MAP).map(([value, v]) => ({ value, label: v.label })) },
      filters: [{ field: 'reviewNote', label: 'İnceleme notu', type: 'text' }, { field: 'reviewed', label: 'İncelenmiş', type: 'boolean' }],
      cell: i => { const st = SUBMISSION_STATUS_MAP[i.status] ?? { label: i.status, variant: 'neutral' as BadgeVariant }; return <Badge variant={st.variant}>{st.label}</Badge> } },
    { key: 'submittedAt', header: 'GÖNDERİM', priority: 2, sortable: true, filter: { type: 'date', label: 'Gönderim', quick: true },
      filters: [{ field: 'reviewedAt', label: 'İnceleme tarihi', type: 'date' }],
      cell: i => <span className="text-sm" style={{ color: 'var(--text-s)' }}>{new Date(i.submittedAt).toLocaleString('tr-TR')}</span> },
    { key: 'productCode', header: 'ÜRÜN', priority: 2, sortable: true, filter: { type: 'text', label: 'Ürün kodu' },
      filters: [{ field: 'hasProduct', label: 'Ürüne dönüşmüş', type: 'boolean' }],
      cell: i => <span className="text-sm" style={{ color: 'var(--text-s)' }}>{i.productCode ?? '—'}</span> },
  ]

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Tedarikçi Gönderimleri</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''} · Partner API'den gelen ürün kartı gönderimleri
        </p>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => (
          <button key={t.key} className={cn('stab', tab === t.key && 'active')}
            onClick={() => grid.mutate(n => { if (t.key) n.set('status', t.key); else n.delete('status') })}>{t.label}</button>
        ))}
      </div>

      <DataGrid<SubmissionListItem>
        gridId="product-submissions"
        views
        grid={grid}
        columns={columns}
        rows={items}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={i => navigate(`/catalog/product-submissions/${i.id}`)}
        empty="Bu durumda gönderim yok."
        search={{ placeholder: 'Ürün kodu, ad veya grup kodu ara…' }}
        minWidth={960}
        export={{ endpoint: '/catalog/product-submissions/export', named: () => ({ status: tab || undefined }), fallbackFileName: 'urun-gonderimleri.xlsx' }}
        compact={{
          title: i => i.supplierProductCode,
          subtitle: i => `${nm(i)} · ${i.groupCode}`,
          right: i => `${i.variantCount} varyant`,
          badge: i => { const st = SUBMISSION_STATUS_MAP[i.status] ?? { label: i.status, variant: 'neutral' as BadgeVariant }; return <Badge variant={st.variant}>{st.label}</Badge> },
        }}
      />
    </div>
  )
}
