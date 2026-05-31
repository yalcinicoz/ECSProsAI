import { ChevronLeft, ChevronRight } from 'lucide-react'

interface PaginationProps {
  page: number
  totalPages: number
  totalCount: number
  pageSize: number
  onChange: (page: number) => void
}

export function Pagination({ page, totalPages, totalCount, pageSize, onChange }: PaginationProps) {
  if (totalPages <= 1) return null
  const from = (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)

  return (
    <div className="flex items-center justify-between px-4 py-3 border-t" style={{ borderColor: 'var(--border)' }}>
      <span className="text-xs" style={{ color: 'var(--text-s)' }}>
        {from}–{to} / {totalCount} kayıt
      </span>
      <div className="flex items-center gap-1">
        <button
          onClick={() => onChange(page - 1)}
          disabled={page === 1}
          className="w-8 h-8 flex items-center justify-center rounded-lg disabled:opacity-40 hover:bg-[var(--surface2)] transition-colors"
          style={{ color: 'var(--text-m)' }}
        >
          <ChevronLeft size={15} />
        </button>
        {Array.from({ length: Math.min(totalPages, 7) }, (_, i) => {
          const p = totalPages <= 7 ? i + 1 : i < 3 ? i + 1 : i === 3 ? page : i > 3 ? totalPages - (6 - i) : page
          return (
            <button
              key={p}
              onClick={() => onChange(p)}
              className="w-8 h-8 flex items-center justify-center rounded-lg text-sm font-medium transition-colors"
              style={
                p === page
                  ? { background: 'var(--brand)', color: '#fff' }
                  : { color: 'var(--text-m)' }
              }
              onMouseOver={(e) => { if (p !== page) (e.currentTarget.style.background = 'var(--surface2)') }}
              onMouseOut={(e) => { if (p !== page) (e.currentTarget.style.background = '') }}
            >
              {p}
            </button>
          )
        })}
        <button
          onClick={() => onChange(page + 1)}
          disabled={page === totalPages}
          className="w-8 h-8 flex items-center justify-center rounded-lg disabled:opacity-40 hover:bg-[var(--surface2)] transition-colors"
          style={{ color: 'var(--text-m)' }}
        >
          <ChevronRight size={15} />
        </button>
      </div>
    </div>
  )
}
