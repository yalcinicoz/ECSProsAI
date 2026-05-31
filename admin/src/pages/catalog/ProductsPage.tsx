import { useState, useMemo, useRef } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Search, Plus, ChevronRight, Package } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { PageSpinner } from '@/components/ui/Spinner'

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

// ── Component ─────────────────────────────────────────────────────────────────

const PAGE_SIZE = 20

export function ProductsPage() {
  const navigate = useNavigate()

  const [search, setSearch]               = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [activeOnly, setActiveOnly]       = useState(false)
  const [page, setPage]                   = useState(1)
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  function handleSearch(val: string) {
    setSearch(val)
    if (debounceRef.current) clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(() => {
      setDebouncedSearch(val)
      setPage(1)
    }, 300)
  }

  const { data: result, isLoading } = useQuery<PagedResult>({
    queryKey: ['products', debouncedSearch, activeOnly, page, PAGE_SIZE],
    queryFn: async () => {
      const params = new URLSearchParams({
        page: String(page),
        pageSize: String(PAGE_SIZE),
        activeOnly: String(activeOnly),
      })
      if (debouncedSearch) params.set('search', debouncedSearch)
      const { data } = await api.get(`/catalog/products?${params}`)
      return data.data
    },
    placeholderData: (prev) => prev,
  })

  const { data: groups = [] } = useQuery<ProductGroup[]>({
    queryKey: ['product-groups', false],
    queryFn: async () => {
      const { data } = await api.get('/catalog/product-groups?activeOnly=false')
      return data.data
    },
    staleTime: 5 * 60 * 1000,
  })

  const groupMap = useMemo(() => {
    const m = new Map<string, string>()
    groups.forEach((g) => m.set(g.id, getName(g)))
    return m
  }, [groups])

  const items      = result?.items ?? []
  const totalCount = result?.totalCount ?? 0
  const totalPages = result?.totalPages ?? 1

  // Pagination window: show at most 5 pages around current
  const pageNums = useMemo(() => {
    const half = 2
    let start = Math.max(1, page - half)
    let end   = Math.min(totalPages, start + 4)
    start = Math.max(1, end - 4)
    return Array.from({ length: end - start + 1 }, (_, i) => start + i)
  }, [page, totalPages])

  if (isLoading && !result) return <PageSpinner />

  return (
    <div className="p-6">
      {/* ── Page header ── */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Ürünler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {result ? `${totalCount} ürün` : '…'}
          </p>
        </div>

        <div className="flex items-center gap-3">
          {/* Active filter toggle */}
          <div
            className="flex items-center gap-1 rounded-xl p-1"
            style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}
          >
            <button
              onClick={() => { setActiveOnly(false); setPage(1) }}
              className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all', !activeOnly ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
              style={!activeOnly ? { color: 'var(--text)' } : {}}
            >Tümü</button>
            <button
              onClick={() => { setActiveOnly(true); setPage(1) }}
              className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all', activeOnly ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
              style={activeOnly ? { color: 'var(--text)' } : {}}
            >Aktif</button>
          </div>

          <Button onClick={() => navigate('/catalog/products/new')}>
            <Plus size={14} /> Yeni Ürün
          </Button>
        </div>
      </div>

      {/* ── Table card ── */}
      <div className="card overflow-hidden p-0">
        {/* Search bar */}
        <div
          className="flex items-center gap-3 px-5 py-3.5 border-b"
          style={{ borderColor: 'var(--border)', background: 'var(--surface2)' }}
        >
          <div className="relative max-w-xs w-full">
            <Search
              size={13}
              className="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
              style={{ color: 'var(--text-s)' }}
            />
            <input
              type="text"
              value={search}
              onChange={(e) => handleSearch(e.target.value)}
              placeholder="Ürün adı, kod…"
              className="inp pl-8"
              style={{ fontSize: '13px' }}
            />
          </div>
          {isLoading && (
            <span className="text-xs" style={{ color: 'var(--text-s)' }}>Yükleniyor…</span>
          )}
        </div>

        {/* Table */}
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Ürün</th>
              <th className="text-left px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Grup</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Varyant</th>
              <th className="text-center px-4 py-3 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Durum</th>
              <th className="w-8 px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {items.length === 0 && !isLoading && (
              <tr>
                <td colSpan={5} className="px-4 py-12 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                  {debouncedSearch ? `"${debouncedSearch}" için ürün bulunamadı` : 'Henüz ürün eklenmemiş'}
                </td>
              </tr>
            )}
            {items.map((item) => (
              <tr
                key={item.id}
                onClick={() => navigate(`/catalog/products/${item.code}`)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}
              >
                {/* Ürün */}
                <td className="px-4 py-3.5">
                  <div className="flex items-center gap-3">
                    <div
                      className="w-9 h-9 rounded-xl flex-shrink-0 flex items-center justify-center"
                      style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}
                    >
                      <Package size={14} />
                    </div>
                    <div>
                      <div className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{getName(item)}</div>
                      <code className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{item.code}</code>
                    </div>
                  </div>
                </td>

                {/* Grup */}
                <td className="px-4 py-3.5">
                  <span className="text-sm" style={{ color: 'var(--text-m)' }}>
                    {groupMap.get(item.productGroupId) ?? '—'}
                  </span>
                </td>

                {/* Varyant */}
                <td className="px-4 py-3.5 text-center">
                  <span className="text-sm" style={{ color: 'var(--text-m)' }}>{item.variantCount}</span>
                </td>

                {/* Durum */}
                <td className="px-4 py-3.5 text-center">
                  <Badge variant={item.isActive ? 'success' : 'neutral'}>
                    {item.isActive ? 'Aktif' : 'Pasif'}
                  </Badge>
                </td>

                <td className="px-4 py-3.5">
                  <ChevronRight size={14} style={{ color: 'var(--text-s)' }} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {/* Pagination */}
        {totalPages > 1 && (
          <div
            className="flex items-center justify-between px-4 py-3 border-t"
            style={{ borderColor: 'var(--border)', background: 'var(--surface2)' }}
          >
            <span className="text-sm" style={{ color: 'var(--text-m)' }}>
              <span className="font-medium" style={{ color: 'var(--text)' }}>
                {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, totalCount)}
              </span>{' '}
              / {totalCount}
            </span>

            <div className="flex items-center gap-1">
              <button
                disabled={page === 1}
                onClick={() => setPage((p) => p - 1)}
                className="w-8 h-8 flex items-center justify-center rounded-xl text-xs"
                style={{
                  border: '1px solid var(--border)',
                  color: 'var(--text-s)',
                  opacity: page === 1 ? 0.4 : 1,
                  cursor: page === 1 ? 'not-allowed' : 'pointer',
                }}
              >‹</button>

              {pageNums.map((p) => (
                <button
                  key={p}
                  onClick={() => setPage(p)}
                  className="w-8 h-8 flex items-center justify-center rounded-xl text-sm"
                  style={
                    p === page
                      ? { background: 'var(--brand)', color: '#fff', fontWeight: 600 }
                      : { color: 'var(--text-m)' }
                  }
                >
                  {p}
                </button>
              ))}

              <button
                disabled={page === totalPages}
                onClick={() => setPage((p) => p + 1)}
                className="w-8 h-8 flex items-center justify-center rounded-xl text-xs"
                style={{
                  border: '1px solid var(--border)',
                  color: 'var(--text-s)',
                  opacity: page === totalPages ? 0.4 : 1,
                  cursor: page === totalPages ? 'not-allowed' : 'pointer',
                }}
              >›</button>
            </div>

            <span className="text-xs mob-hide" style={{ color: 'var(--text-s)' }}>
              Sayfa {page} / {totalPages}
            </span>
          </div>
        )}
      </div>
    </div>
  )
}
