import { useState } from 'react'
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
  const [tab, setTab] = useState('')
  // DataGrid F4 mekanik göç: sayfa/sayfa boyu grid durumunda; uç sort/filtre desteklemez
  const grid = useGridState('supplier-invoices', { defaultPageSize: 20 })

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<SupplierInvoice>>({
    queryKey: ['supplier-invoices', tab, ...grid.queryKey],
    queryFn: async () => (await api.get(`/finance/supplier-invoices?${grid.toParams({ status: tab || undefined })}`)).data.data,
    placeholderData: prev => prev,
  })

  const invoices = data?.items ?? []
  const columns: GridColumn<SupplierInvoice>[] = [
    { key: 'invoiceNumber', header: 'FATURA NO', priority: 1, lockVisible: true, frozen: true, cell: f => <code className="text-xs font-mono">{f.invoiceNumber}</code> },
    { key: 'invoiceDate', header: 'TARİH', priority: 1, cell: f => new Date(f.invoiceDate).toLocaleDateString('tr-TR') },
    { key: 'dueDate', header: 'VADE', priority: 2, cell: f => (f.dueDate ? new Date(f.dueDate).toLocaleDateString('tr-TR') : '—') },
    { key: 'itemCount', header: 'KALEM', priority: 3, cell: f => f.itemCount },
    { key: 'total', header: 'TUTAR', priority: 1, cell: f => <span className="font-medium">{para(f.grandTotal)}</span> },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, cell: f => { const [l, v] = DURUM[f.status] ?? [f.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{l}</Badge> } },
  ]

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Tedarikçi Faturaları</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {data?.totalCount ?? 0} kayıt — faturalar tedarikçi teslimat akışından oluşur
        </p>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {[['', 'Tümü'], ['open', 'Açık'], ['paid', 'Ödendi']].map(([v, l]) => (
          <button key={v} className={cn('stab', tab === v && 'active')}
            onClick={() => { setTab(v); grid.setPage(1) }}>{l}</button>
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
      />
    </div>
  )
}
