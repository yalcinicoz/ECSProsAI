import { cn } from '@/lib/utils'

// Liste ekranlarının ortak tablo iskeleti — CouponsPage kalıbının paylaşılan hali.

export interface Column<T> {
  header: string
  cell: (row: T) => React.ReactNode
  className?: string
}

export function DataTable<T extends { id: string }>({
  columns, rows, loading, empty, onRowClick,
}: {
  columns: Column<T>[]
  rows: T[]
  loading?: boolean
  empty?: string
  onRowClick?: (row: T) => void
}) {
  return (
    <div className="card overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {columns.map(c => (
                <th key={c.header}
                  className={cn('px-4 py-3 text-xs font-semibold text-left whitespace-nowrap', c.className)}
                  style={{ color: 'var(--text-s)' }}>{c.header}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {loading && (
              <tr><td colSpan={columns.length} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!loading && rows.length === 0 && (
              <tr><td colSpan={columns.length} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>{empty ?? 'Kayıt bulunamadı.'}</td></tr>
            )}
            {!loading && rows.map(r => (
              <tr key={r.id} onClick={onRowClick ? () => onRowClick(r) : undefined}
                className={cn('transition-colors', onRowClick && 'cursor-pointer hover:bg-[var(--surface2)]')}
                style={{ borderBottom: '1px solid var(--border)' }}>
                {columns.map(c => (
                  <td key={c.header} className={cn('px-4 py-3 text-sm', c.className)} style={{ color: 'var(--text)' }}>{c.cell(r)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

// Pager kaldırıldı (2026-09-08, DataGrid F4): sayfalı listeler DataGrid/GridPagination, ham tablolar Pagination kullanır.
