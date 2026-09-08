import { ChevronLeft, ChevronRight } from 'lucide-react'
import { GRID_PAGE_SIZES } from './types'

interface Props {
  page: number
  pageSize: number
  totalCount: number
  filtered?: boolean
  onPage: (p: number) => void
  onPageSize: (n: number) => void
  pageSizes?: number[]
}

/** Mevcut Pagination'ın DataGrid sürümü: sayfa boyu seçimi + "x–y / toplam (filtreli)" + numaralı düğmeler; tek sayfada da görünür. */
export function GridPagination({ page, pageSize, totalCount, filtered, onPage, onPageSize, pageSizes = GRID_PAGE_SIZES }: Props) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)
  const sizes = pageSizes.includes(pageSize) ? pageSizes : [...pageSizes, pageSize].sort((a, b) => a - b)
  const nums = Array.from({ length: Math.min(totalPages, 7) }, (_, i) =>
    totalPages <= 7 ? i + 1 : i < 3 ? i + 1 : i === 3 ? Math.min(Math.max(page, 4), totalPages - 3) : totalPages - (6 - i))
  const uniq = Array.from(new Set(nums))

  return (
    <div className="flex flex-wrap items-center justify-between gap-2 px-3 py-2" style={{ borderTop: '1px solid var(--border)' }}>
      <div className="flex items-center gap-2 text-xs" style={{ color: 'var(--text-s)' }}>
        <span>{from}–{to} / {totalCount.toLocaleString('tr-TR')} kayıt{filtered ? ' (filtreli)' : ''}</span>
        <span className="mob-hide">· sayfa {page}/{totalPages}</span>
        <label className="flex items-center gap-1 whitespace-nowrap">
          <span className="mob-hide">Sayfa boyu</span>
          <select className="inp text-xs py-0.5 px-1.5 h-auto" value={pageSize} onChange={e => onPageSize(Number(e.target.value))} aria-label="Sayfa boyu">
            {sizes.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </label>
      </div>
      {totalPages > 1 && (
        <div className="flex items-center gap-1">
          <button onClick={() => onPage(page - 1)} disabled={page <= 1} aria-label="Önceki sayfa"
            className="w-8 h-8 flex items-center justify-center rounded-lg disabled:opacity-40 hover:bg-[var(--surface2)] transition-colors" style={{ color: 'var(--text-m)' }}>
            <ChevronLeft size={15} />
          </button>
          {uniq.map(p => (
            <button key={p} onClick={() => onPage(p)} aria-current={p === page ? 'page' : undefined}
              className="w-8 h-8 flex items-center justify-center rounded-lg text-sm font-medium transition-colors hover:bg-[var(--surface2)]"
              style={p === page ? { background: 'var(--brand)', color: '#fff' } : { color: 'var(--text-m)' }}>{p}</button>
          ))}
          <button onClick={() => onPage(page + 1)} disabled={page >= totalPages} aria-label="Sonraki sayfa"
            className="w-8 h-8 flex items-center justify-center rounded-lg disabled:opacity-40 hover:bg-[var(--surface2)] transition-colors" style={{ color: 'var(--text-m)' }}>
            <ChevronRight size={15} />
          </button>
        </div>
      )}
    </div>
  )
}
