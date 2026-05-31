import { useState, useEffect, useRef, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, Package, ShoppingBag, User, Loader } from 'lucide-react'
import { useUIStore } from '@/store/ui'
import { cn } from '@/lib/utils'
import api from '@/api/client'

// ── Types ─────────────────────────────────────────────────────────────────────

type Scope = 'Tümü' | 'Ürünler' | 'Siparişler' | 'Müşteriler'
const SCOPES: Scope[] = ['Tümü', 'Ürünler', 'Siparişler', 'Müşteriler']

interface SearchResult {
  id: string
  type: 'product' | 'order' | 'member'
  title: string
  sub: string
  to: string
}

// ── API helpers ───────────────────────────────────────────────────────────────

async function searchProducts(q: string): Promise<SearchResult[]> {
  const { data } = await api.get(`/catalog/products?search=${encodeURIComponent(q)}&pageSize=5&activeOnly=false`)
  return (data.data?.items ?? []).map((p: any) => ({
    id: p.id,
    type: 'product',
    title: p.nameI18n?.tr ?? p.nameI18n?.[Object.keys(p.nameI18n ?? {})[0]] ?? p.code,
    sub: `${p.code} · ${p.variantCount} varyant`,
    to: `/catalog/products/${p.code}`,
  }))
}

async function searchOrders(q: string): Promise<SearchResult[]> {
  const { data } = await api.get(`/orders?search=${encodeURIComponent(q)}&pageSize=5`)
  return (data.data?.items ?? []).map((o: any) => ({
    id: o.id,
    type: 'order',
    title: `#${o.orderNumber}`,
    sub: `${o.grandTotal?.toLocaleString('tr-TR')} ₺ · ${o.status}`,
    to: `/orders/${o.id}`,
  }))
}

async function searchMembers(q: string): Promise<SearchResult[]> {
  const { data } = await api.get(`/crm/members?search=${encodeURIComponent(q)}&pageSize=5`)
  return (data.data?.items ?? []).map((m: any) => ({
    id: m.id,
    type: 'member',
    title: `${m.firstName} ${m.lastName}`,
    sub: m.email ?? m.phone ?? '—',
    to: `/crm/members/${m.id}`,
  }))
}

// ── Icon & color per type ─────────────────────────────────────────────────────

const TYPE_META: Record<SearchResult['type'], { icon: React.ReactNode; bg: string; color: string; badge: string }> = {
  product: { icon: <Package size={14} />,     bg: 'var(--brand-bg)', color: 'var(--brand)',  badge: 'Ürün' },
  order:   { icon: <ShoppingBag size={14} />, bg: '#f0fdf4',         color: '#16a34a',       badge: 'Sipariş' },
  member:  { icon: <User size={14} />,        bg: '#f3e8ff',         color: '#9333ea',       badge: 'Müşteri' },
}

const BADGE_STYLE: Record<SearchResult['type'], React.CSSProperties> = {
  product: { background: '#eff6ff', color: '#1e40af', border: '1px solid #bfdbfe' },
  order:   { background: '#f0fdf4', color: '#166534', border: '1px solid #bbf7d0' },
  member:  { background: '#f3e8ff', color: '#7e22ce', border: '1px solid #e9d5ff' },
}

// ── Component ─────────────────────────────────────────────────────────────────

export function CommandPalette() {
  const { cmdOpen, setCmdOpen } = useUIStore()
  const navigate = useNavigate()

  const [query, setQuery]             = useState('')
  const [scope, setScope]             = useState<Scope>('Tümü')
  const [results, setResults]         = useState<SearchResult[]>([])
  const [loading, setLoading]         = useState(false)
  const [highlighted, setHighlighted] = useState(0)

  const inputRef   = useRef<HTMLInputElement>(null)
  const debounceId = useRef<ReturnType<typeof setTimeout> | null>(null)

  // Reset state on open
  useEffect(() => {
    if (cmdOpen) {
      setQuery('')
      setScope('Tümü')
      setResults([])
      setHighlighted(0)
      requestAnimationFrame(() => inputRef.current?.focus())
    }
  }, [cmdOpen])

  // Debounced API search
  const runSearch = useCallback(async (q: string, sc: Scope) => {
    if (!q.trim()) { setResults([]); setLoading(false); return }

    setLoading(true)
    try {
      const fetchers: Promise<SearchResult[]>[] = []
      if (sc === 'Tümü' || sc === 'Ürünler')   fetchers.push(searchProducts(q))
      if (sc === 'Tümü' || sc === 'Siparişler') fetchers.push(searchOrders(q))
      if (sc === 'Tümü' || sc === 'Müşteriler') fetchers.push(searchMembers(q))

      const settled = await Promise.allSettled(fetchers)
      const merged = settled.flatMap((r) => r.status === 'fulfilled' ? r.value : [])
      setResults(merged)
      setHighlighted(0)
    } finally {
      setLoading(false)
    }
  }, [])

  function handleQueryChange(val: string) {
    setQuery(val)
    if (debounceId.current) clearTimeout(debounceId.current)
    debounceId.current = setTimeout(() => runSearch(val, scope), 300)
  }

  function handleScopeChange(sc: Scope) {
    setScope(sc)
    if (debounceId.current) clearTimeout(debounceId.current)
    debounceId.current = setTimeout(() => runSearch(query, sc), 0)
  }

  function go(to: string) {
    navigate(to)
    setCmdOpen(false)
  }

  function handleKeyDown(e: React.KeyboardEvent) {
    if (e.key === 'Escape') { setCmdOpen(false); return }
    if (e.key === 'ArrowDown') { e.preventDefault(); setHighlighted((h) => Math.min(h + 1, results.length - 1)) }
    if (e.key === 'ArrowUp')   { e.preventDefault(); setHighlighted((h) => Math.max(h - 1, 0)) }
    if (e.key === 'Enter' && results[highlighted]) go(results[highlighted].to)
  }

  if (!cmdOpen) return null

  const isEmpty = query.trim() === ''
  const noResults = !isEmpty && !loading && results.length === 0

  return (
    <div
      className="fixed inset-0 z-[9999] flex items-start justify-center pt-[60px] px-4"
      style={{ background: 'rgba(15,23,42,.65)', backdropFilter: 'blur(6px)' }}
      onMouseDown={(e) => { if (e.target === e.currentTarget) setCmdOpen(false) }}
    >
      <div
        className="w-full max-w-[580px] rounded-2xl overflow-hidden shadow-2xl"
        style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}
        onKeyDown={handleKeyDown}
      >
        {/* ── Search input ── */}
        <div
          className="flex items-center gap-3 px-5 border-b"
          style={{ borderColor: 'var(--border)' }}
        >
          {loading
            ? <Loader size={16} className="animate-spin flex-shrink-0" style={{ color: 'var(--brand)' }} />
            : <Search size={16} className="flex-shrink-0" style={{ color: 'var(--text-s)' }} />
          }
          <input
            ref={inputRef}
            value={query}
            onChange={(e) => handleQueryChange(e.target.value)}
            placeholder="Sipariş, ürün, müşteri ara…"
            className="w-full py-4 text-base border-none outline-none"
            style={{ background: 'transparent', color: 'var(--text)' }}
          />
          <kbd className="kbd flex-shrink-0">ESC</kbd>
        </div>

        {/* ── Scope tabs ── */}
        <div
          className="flex border-b"
          style={{ background: 'var(--surface2)', borderColor: 'var(--border)' }}
        >
          {SCOPES.map((s) => (
            <button
              key={s}
              type="button"
              onClick={() => handleScopeChange(s)}
              className={cn(
                'px-3 py-2 text-xs font-medium whitespace-nowrap border-b-2 transition-all',
                scope === s ? 'border-[var(--brand)]' : 'border-transparent',
              )}
              style={{ color: scope === s ? 'var(--brand)' : 'var(--text-s)' }}
            >
              {s}
            </button>
          ))}
        </div>

        {/* ── Results ── */}
        <div className="max-h-72 overflow-y-auto thin-scroll">

          {/* Empty state */}
          {isEmpty && (
            <div className="px-4 py-6 text-center text-sm" style={{ color: 'var(--text-s)' }}>
              Aramak istediğiniz sipariş, ürün veya müşteriyi yazın
            </div>
          )}

          {/* Loading skeleton */}
          {loading && (
            <div className="px-4 py-3 space-y-2">
              {[1, 2, 3].map((i) => (
                <div key={i} className="flex items-center gap-3 animate-pulse">
                  <div className="w-8 h-8 rounded-lg flex-shrink-0" style={{ background: 'var(--surface2)' }} />
                  <div className="flex-1 space-y-1.5">
                    <div className="h-3 rounded" style={{ background: 'var(--surface2)', width: '60%' }} />
                    <div className="h-2.5 rounded" style={{ background: 'var(--surface2)', width: '40%' }} />
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* No results */}
          {noResults && (
            <div className="px-4 py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>
              "<span style={{ color: 'var(--text)' }}>{query}</span>" için sonuç bulunamadı
            </div>
          )}

          {/* Results list */}
          {!loading && results.length > 0 && results.map((r, i) => {
            const meta = TYPE_META[r.type]
            return (
              <div
                key={`${r.type}-${r.id}`}
                className={cn('cm-res', i === highlighted && 'cm-sel')}
                onMouseEnter={() => setHighlighted(i)}
                onMouseDown={() => go(r.to)}
              >
                <div
                  className="cm-ico flex-shrink-0"
                  style={{ background: meta.bg, color: meta.color }}
                >
                  {meta.icon}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="text-sm font-semibold truncate" style={{ color: 'var(--text)' }}>{r.title}</div>
                  <div className="text-xs truncate" style={{ color: 'var(--text-s)' }}>{r.sub}</div>
                </div>
                <span className="badge flex-shrink-0 text-[11px]" style={BADGE_STYLE[r.type]}>
                  {meta.badge}
                </span>
              </div>
            )
          })}
        </div>

        {/* ── Footer ── */}
        <div
          className="flex items-center justify-between border-t px-4 py-2.5"
          style={{ background: 'var(--surface2)', borderColor: 'var(--border)' }}
        >
          <div className="flex gap-3 text-[11px] mob-hide" style={{ color: 'var(--text-s)' }}>
            <span><kbd className="kbd">↑↓</kbd> Gezin</span>
            <span><kbd className="kbd">↵</kbd> Git</span>
            <span><kbd className="kbd">Esc</kbd> Kapat</span>
          </div>
          <span className="text-[11px]" style={{ color: 'var(--text-s)' }}>
            {!isEmpty && !loading && `${results.length} sonuç`}
          </span>
        </div>
      </div>
    </div>
  )
}
