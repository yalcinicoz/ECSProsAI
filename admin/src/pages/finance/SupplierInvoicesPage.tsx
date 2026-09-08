import { useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText, para } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'

interface SupplierInvoice {
  id: string
  currentAccountId: string
  invoiceNumber: string
  invoiceDate: string
  dueDate?: string
  grandTotal: number
  status: string
  itemCount: number
  createdAt: string
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

const DURUM: Record<string, [string, BadgeVariant]> = {
  draft:     ['Taslak', 'neutral'],
  open:      ['Açık', 'info'],
  partial:   ['Kısmi Ödendi', 'warning'],
  paid:      ['Ödendi', 'success'],
  cancelled: ['İptal', 'danger'],
}

export function SupplierInvoicesPage() {
  const [sp] = useSearchParams()
  const tab = sp.get('tab') ?? ''
  // DataGrid: sunucu filtre/sıralama/arama (SupplierInvoiceGrid.Schema) + Excel export; sekme ?tab= (durum adlandırılmış parametre)
  const grid = useGridState('supplier-invoices', { defaultPageSize: 20, defaultSort: 'invoiceDate', defaultDir: 'desc' })
  const switchTab = (v: string) => grid.mutate(n => { if (v) n.set('tab', v); else n.delete('tab') })

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<SupplierInvoice>>({
    queryKey: ['supplier-invoices', tab, ...grid.queryKey],
    queryFn: async () => (await api.get(`/finance/supplier-invoices?${grid.toParams({ status: tab || undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const invoices = data?.items ?? []
  const columns: GridColumn<SupplierInvoice>[] = [
    { key: 'invoiceNumber', header: 'FATURA NO', priority: 1, lockVisible: true, frozen: true, sortable: true, filter: { type: 'text', label: 'Fatura no', ops: ['startswith', 'contains', 'eq'] },
      filters: [{ field: 'notes', label: 'Not', type: 'text' }],
      cell: f => <code className="text-xs font-mono">{f.invoiceNumber}</code> },
    { key: 'invoiceDate', header: 'TARİH', priority: 1, sortable: true, filter: { type: 'date', label: 'Fatura tarihi', quick: true },
      filters: [{ field: 'createdAt', label: 'Kayıt tarihi', type: 'date' }],
      cell: f => new Date(f.invoiceDate).toLocaleDateString('tr-TR') },
    { key: 'dueDate', header: 'VADE', priority: 2, sortable: true, filter: { type: 'date', label: 'Vade' }, cell: f => (f.dueDate ? new Date(f.dueDate).toLocaleDateString('tr-TR') : '—') },
    { key: 'itemCount', header: 'KALEM', priority: 3, sortable: true, align: 'right', filter: { type: 'number', label: 'Kalem sayısı' }, cell: f => f.itemCount },
    { key: 'total', header: 'TUTAR', priority: 1, sortable: true, align: 'right', filter: { type: 'number', label: 'Tutar' }, cell: f => <span className="font-medium">{para(f.grandTotal)}</span> },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(DURUM).map(([value, [label]]) => ({ value, label })) },
      cell: f => { const [l, v] = DURUM[f.status] ?? [f.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{l}</Badge> } },
  ]

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Tedarikçi Faturaları</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''} — faturalar tedarikçi teslimat akışından oluşur
        </p>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {[['', 'Tümü'], ['open', 'Açık'], ['paid', 'Ödendi']].map(([v, l]) => (
          <button key={v} className={cn('stab', tab === v && 'active')}
            onClick={() => switchTab(v)}>{l}</button>
        ))}
      </div>

      <DataGrid<SupplierInvoice>
        gridId="supplier-invoices"
        grid={grid}
        columns={columns}
        rows={invoices}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        empty="Tedarikçi faturası yok."
        search={{ placeholder: 'Fatura no / not ara…' }}
        export={{ endpoint: '/finance/supplier-invoices/export', named: () => ({ status: tab || undefined }), fallbackFileName: 'tedarikci-faturalari.xlsx' }}
        views
      />
    </div>
  )
}
