import { useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { X, Star, Clock, Plus } from 'lucide-react'
import { useUIStore } from '@/store/ui'

const FAVE_PAGES = [
  { label: 'Bekleyen Siparişler', to: '/orders',    badge: '14', badgeColor: 'bg-amber-100 text-amber-700' },
  { label: 'Stok Uyarıları',      to: '/inventory/stocks', badge: '47', badgeColor: 'bg-red-100 text-red-600' },
  { label: 'Yeni Sipariş',        to: '/orders',    badge: null, badgeColor: '' },
]

const RECENT_PAGES = [
  { label: 'Özellik Tipleri',  to: '/catalog/attribute-types' },
  { label: 'Ürün Grupları',    to: '/catalog/product-groups' },
]

export function FavoritesPanel() {
  const { favsPanelOpen, setFavsPanelOpen } = useUIStore()
  const navigate = useNavigate()
  const panelRef = useRef<HTMLDivElement>(null)

  // Close on outside click
  useEffect(() => {
    if (!favsPanelOpen) return
    function handler(e: MouseEvent) {
      if (panelRef.current && !panelRef.current.contains(e.target as Node)) {
        const btn = document.getElementById('fav-btn')
        if (btn && btn.contains(e.target as Node)) return
        setFavsPanelOpen(false)
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [favsPanelOpen, setFavsPanelOpen])

  // Close on Escape
  useEffect(() => {
    if (!favsPanelOpen) return
    function handler(e: KeyboardEvent) { if (e.key === 'Escape') setFavsPanelOpen(false) }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [favsPanelOpen, setFavsPanelOpen])

  function go(to: string) {
    navigate(to)
    setFavsPanelOpen(false)
  }

  return (
    <>
      {/* ── Trigger tab ── */}
      <button
        id="fav-btn"
        onClick={() => setFavsPanelOpen(!favsPanelOpen)}
        title="Sık Kullanılanlar"
        aria-label="Sık Kullanılanlar"
      >
        <Star size={12} />
      </button>

      {/* ── Sliding panel ── */}
      <div
        ref={panelRef}
        className="fixed top-0 right-0 h-full w-[272px] flex flex-col z-[59]"
        style={{
          background: 'var(--surface)',
          borderLeft: '1px solid var(--border)',
          boxShadow: '-8px 0 28px rgba(0,0,0,.14)',
          transform: favsPanelOpen ? 'translateX(0)' : 'translateX(100%)',
          transition: 'transform .28s cubic-bezier(.4,0,.2,1)',
        }}
      >
        {/* Header */}
        <div
          className="flex items-center justify-between px-4 py-4 border-b flex-shrink-0"
          style={{ borderColor: 'var(--border)' }}
        >
          <div>
            <h3 className="font-bold text-sm" style={{ color: 'var(--text)' }}>Sık Kullanılanlar</h3>
            <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>Hızlı erişim kısayolları</p>
          </div>
          <button
            type="button"
            onClick={() => setFavsPanelOpen(false)}
            className="w-8 h-8 flex items-center justify-center rounded-lg transition-colors"
            style={{ color: 'var(--text-s)' }}
            onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--surface2)')}
            onMouseLeave={(e) => (e.currentTarget.style.background = '')}
          >
            <X size={15} />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto thin-scroll px-3 py-2">
          <div
            className="text-[10px] font-bold uppercase tracking-wider px-2 py-1.5"
            style={{ color: 'var(--text-s)' }}
          >
            Sayfalar
          </div>
          {FAVE_PAGES.map((p) => (
            <button
              key={p.label}
              type="button"
              onClick={() => go(p.to)}
              className="nav-lnk w-full text-left"
              style={{ color: 'var(--text-m)' }}
            >
              <Star size={13} className="ni flex-shrink-0 text-amber-400" />
              <span className="flex-1">{p.label}</span>
              {p.badge && (
                <span className={`text-[9px] font-bold px-1.5 rounded-full ${p.badgeColor}`}>
                  {p.badge}
                </span>
              )}
            </button>
          ))}

          <div
            className="text-[10px] font-bold uppercase tracking-wider px-2 py-1.5 mt-2"
            style={{ color: 'var(--text-s)' }}
          >
            Son Ziyaret
          </div>
          {RECENT_PAGES.map((p) => (
            <button
              key={p.label}
              type="button"
              onClick={() => go(p.to)}
              className="nav-lnk w-full text-left"
              style={{ color: 'var(--text-m)' }}
            >
              <Clock size={13} className="ni flex-shrink-0" style={{ color: 'rgba(255,255,255,.28)' }} />
              <span>{p.label}</span>
            </button>
          ))}
        </div>

        {/* Footer */}
        <div
          className="px-4 py-3 border-t flex-shrink-0"
          style={{ borderColor: 'var(--border)' }}
        >
          <button
            type="button"
            className="w-full py-2 rounded-xl text-sm font-semibold flex items-center justify-center gap-1.5"
            style={{ background: 'var(--brand-bg)', color: 'var(--brand)', border: '1px solid var(--brand-b)' }}
          >
            <Plus size={13} /> Kısayol Ekle
          </button>
        </div>
      </div>
    </>
  )
}
