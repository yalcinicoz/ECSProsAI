import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { BarChart3, ArrowRight, ShieldCheck } from 'lucide-react'
import api from '@/api/client'
import { useAuthStore } from '@/store/auth'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'

type Row = { id: string } & Record<string, string | number | null>
type ReportId = 'stocks'
const reports = {
  stocks: { title: 'Stok Durumu', description: 'Ürünlerin depo, rezerve ve kullanılabilir miktarlarını karşılaştırın.', permission: 'inventory.view', endpoint: '/inventory/stocks/admin-list', export: '/inventory/stocks/admin-list/export', sort: 'available', note: 'Anlık stok durumudur; geçmiş tarihli stok veya maliyet raporu değildir. Başlamak için ürün kodu, adı veya barkod arayın.' },
} as const

const display = (v: Row[string]) => v == null ? '—' : typeof v === 'number' ? v.toLocaleString('tr-TR', { maximumFractionDigits: 2 }) : v
const text = (key: string, header: string, valueKey = key, sortable = false): GridColumn<Row> => ({ key, header, sortable, cell: row => display(row[valueKey]) })
function columns(): GridColumn<Row>[] {
  return [
    { ...text('productCode', 'Ürün kodu'), lockVisible: true, filter: { type: 'text', field: 'product', ops: ['contains'], label: 'Ürün' } },
    text('productName', 'Ürün adı'),
    text('options', 'Varyant'),
    text('warehouse', 'Depo', 'warehouseName', true),
    ...[['quantity', 'Stok', 'quantity'], ['reserved', 'Rezerve', 'reservedQuantity'], ['available', 'Kullanılabilir', 'availableQuantity']].map(([key, label, field]): GridColumn<Row> => ({ ...text(key, label, field, true), align: 'right', filter: { type: 'number' } })),
  ]
}

export function ReportsPage({ reportId }: { reportId: ReportId }) {
  const hasPermission = useAuthStore(s => s.hasPermission)
  const report = reports[reportId]
  if (!hasPermission(report.permission)) return <div className="p-6" role="alert">Bu raporu görüntüleme yetkiniz yok.</div>
  return <ReportContent key={reportId} reportId={reportId} />
}

function ReportContent({ reportId }: { reportId: ReportId }) {
  const hasPermission = useAuthStore(s => s.hasPermission)
  const userId = useAuthStore(s => s.user?.id)
  const report = reports[reportId]
  const grid = useGridState(`report-${reportId}`, { defaultPageSize: 25, defaultSort: report.sort, defaultDir: 'desc' })
  const enabled = !!grid.state.search.trim() || grid.state.filters.some(f => f.field === 'product' && f.value.trim())
  const { data, isLoading, isFetching, error } = useQuery<{ items: Row[]; totalCount: number }>({
    queryKey: ['fixed-report', userId, reportId, ...grid.queryKey],
    queryFn: async ({ signal }) => (await api.get(`${report.endpoint}?${grid.toParams()}`, { signal })).data.data,
    enabled,
  })
  return <div className="p-4 md:p-6 space-y-5">
    <header className="rounded-2xl border p-5 md:p-7" style={{ borderColor: 'var(--border)', background: 'var(--surface)' }}>
      <div className="flex items-center gap-3"><BarChart3 size={28} style={{ color: 'var(--brand)' }} /><div><h1 className="text-xl font-bold">Raporlar</h1><p className="text-sm mt-1" style={{ color: 'var(--text-s)' }}>İşletmenizin verileri, tek yerde.</p></div></div>
      <nav aria-label="Sabit raporlar" className="grid gap-3 md:grid-cols-3 mt-5">
        {Object.entries(reports).filter(([, r]) => hasPermission(r.permission)).map(([id, r]) => <Link key={id} to={`/reports/${id}`} aria-current={id === reportId ? 'page' : undefined} className="rounded-xl border p-4 hover:shadow-sm focus-visible:outline-2 focus-visible:outline-offset-2" style={{ borderColor: id === reportId ? 'var(--brand)' : 'var(--border)', background: id === reportId ? 'var(--surface2)' : 'var(--surface)' }}><span className="flex justify-between font-semibold text-sm">{r.title}<ArrowRight size={16} /></span><span className="block mt-2 text-xs leading-5" style={{ color: 'var(--text-s)' }}>{r.description}</span></Link>)}
      </nav>
    </header>
    <section aria-label={report.title} className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2"><h2 className="text-lg font-semibold">{report.title}</h2><span className="text-xs flex items-center gap-1" style={{ color: 'var(--text-s)' }}><ShieldCheck size={14} /> Salt okunur · Yetkiniz kapsamındaki veriler</span></div>
      <p className="text-sm" style={{ color: 'var(--text-s)' }}>{report.note}</p>
      <p aria-live="polite" className="text-sm font-medium">{!enabled ? 'Ürün araması bekleniyor' : error ? 'Rapor alınamadı' : data ? `${data.totalCount.toLocaleString('tr-TR')} kayıt${grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}` : 'Rapor hazırlanıyor…'}</p>
      <DataGrid<Row> gridId={`report-${reportId}`} grid={grid} columns={columns()} advancedFilters
        rows={enabled ? data?.items ?? [] : []} totalCount={enabled ? data?.totalCount ?? 0 : 0} loading={isLoading} fetching={isFetching}
        error={error ? errText(error) : null} search={{ placeholder: 'Ürün kodu, adı veya barkod…' }}
        empty={enabled ? 'Bu filtrelerle kayıt bulunamadı.' : 'Stok raporu için ürün arayın.'} minWidth={850}
        export={enabled && !error ? { endpoint: report.export, fallbackFileName: `${reportId}-raporu.xlsx` } : undefined} />
    </section>
  </div>
}
