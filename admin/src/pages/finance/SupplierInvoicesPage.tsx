import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { DataTable, Pager, para } from '@/components/ui/DataTable'
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
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery<PagedResult<SupplierInvoice>>({
    queryKey: ['supplier-invoices', tab, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (tab) params.set('status', tab)
      return (await api.get(`/finance/supplier-invoices?${params}`)).data.data
    },
  })

  const invoices = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 20)

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
            onClick={() => { setTab(v); setPage(1) }}>{l}</button>
        ))}
      </div>

      <DataTable<SupplierInvoice>
        columns={[
          { header: 'FATURA NO', cell: f => <code className="text-xs font-mono">{f.invoiceNumber}</code> },
          { header: 'TARİH', cell: f => new Date(f.invoiceDate).toLocaleDateString('tr-TR') },
          { header: 'VADE', cell: f => (f.dueDate ? new Date(f.dueDate).toLocaleDateString('tr-TR') : '—') },
          { header: 'KALEM', cell: f => f.itemCount },
          { header: 'TUTAR', cell: f => <span className="font-medium">{para(f.grandTotal)}</span> },
          { header: 'DURUM', cell: f => { const [l, v] = DURUM[f.status] ?? [f.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{l}</Badge> } },
        ]}
        rows={invoices}
        loading={isLoading}
        empty="Tedarikçi faturası yok."
      />

      <Pager page={page} totalPages={totalPages} onChange={setPage} />
    </div>
  )
}
