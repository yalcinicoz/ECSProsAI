import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'
import { Bell, Moon, Sun, Star, Search, Menu } from 'lucide-react'
import { useUIStore } from '@/store/ui'
import { cn } from '@/lib/utils'

// Breadcrumb map: pathname → label
const BREADCRUMB: Record<string, string> = {
  '/':                              'Dashboard',
  '/catalog/products':              'Ürün Kartları',
  '/catalog/attribute-types':      'Özellik Tipleri',
  '/catalog/product-groups':       'Ürün Grupları',
  '/catalog/categories':           'Kategoriler',
  '/orders':                       'Siparişler',
  '/orders/returns':               'İadeler',
  '/orders/invoices':              'Faturalar',
  '/orders/quotes':                'Teklifler',
  '/orders/gift-cards':            'Hediye Kartları',
  '/crm/members':                  'Üyeler',
  '/crm/member-groups':            'Üye Grupları',
  '/inventory/warehouses':         'Depolar',
  '/inventory/stocks':             'Stok Takibi',
  '/inventory/transfers':          'Stok Hareketleri',
  '/promotion/campaigns':          'Kampanyalar',
  '/pos/sales':                    'POS Satışları',
  '/integrations/logs':            'Entegrasyon Logları',
  '/cms/pages':                    'CMS Sayfaları',
  '/finance/suppliers':            'Tedarikçiler',
  '/fulfillment/picking-plans':    'Picking Planları',
  '/settings/users':               'Kullanıcılar',
  '/settings/roles':               'Roller',
  '/settings/audit-logs':          'Denetim Logları',
  '/settings/firms':               'Firmalar',
  '/settings/languages':           'Diller',
  '/settings/lookup-types':        'Lookup Tipleri',
}

interface HeaderProps {
  onMobileMenuOpen: () => void
}

export function Header({ onMobileMenuOpen }: HeaderProps) {
  const location = useLocation()
  const { darkMode, toggleDarkMode, setFavsPanelOpen, setCmdOpen, sidebarCollapsed, toggleSidebar } = useUIStore()

  // Ctrl+K → open command palette
  useEffect(() => {
    function handler(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault()
        setCmdOpen(true)
      }
    }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [setCmdOpen])

  const breadcrumb = BREADCRUMB[location.pathname] ?? ''

  return (
    <header
      className="h-14 flex items-center px-4 gap-3 flex-shrink-0 sticky top-0 z-30"
      style={{ background: 'var(--header)', borderBottom: '1px solid var(--border)', backdropFilter: 'blur(8px)' }}
    >
      {/* Hamburger — mobile: open overlay, desktop: toggle sidebar */}
      <button
        type="button"
        onClick={() => { onMobileMenuOpen() }}
        className="md:hidden w-9 h-9 flex items-center justify-center rounded-xl transition-colors"
        style={{ color: 'var(--text-m)' }}
        onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--surface2)')}
        onMouseLeave={(e) => (e.currentTarget.style.background = '')}
      >
        <Menu size={18} />
      </button>

      {/* Desktop hamburger — always visible, toggles sidebar */}
      <button
        type="button"
        onClick={toggleSidebar}
        className="hidden md:flex w-9 h-9 items-center justify-center rounded-xl transition-colors"
        style={{ color: 'var(--text-m)' }}
        onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--surface2)')}
        onMouseLeave={(e) => (e.currentTarget.style.background = '')}
        title={sidebarCollapsed ? 'Menüyü Aç' : 'Menüyü Kapat'}
      >
        <Menu size={18} />
      </button>

      {/* Breadcrumb */}
      {breadcrumb && (
        <nav className="mob-hide flex items-center text-sm flex-shrink-0">
          <span className="font-semibold" style={{ color: 'var(--brand)' }}>{breadcrumb}</span>
        </nav>
      )}

      {/* Search trigger (Ctrl+K) */}
      <div className="flex-1 max-w-md mx-auto">
        <button
          type="button"
          onClick={() => setCmdOpen(true)}
          className="w-full flex items-center gap-3 rounded-xl px-3 py-2 text-sm text-left transition-all"
          style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text-s)' }}
        >
          <Search size={14} />
          <span className="flex-1 truncate">Ara…</span>
          <span className="flex items-center gap-0.5 mob-hide">
            <kbd className="kbd">Ctrl</kbd><kbd className="kbd">K</kbd>
          </span>
        </button>
      </div>

      <div className="flex items-center gap-1 flex-shrink-0">
        {/* Favorilere Ekle */}
        <button
          type="button"
          onClick={() => setFavsPanelOpen(true)}
          className={cn(
            'mob-hide flex items-center gap-1.5 px-2.5 py-1.5 rounded-xl text-xs font-medium transition-all',
          )}
          style={{ color: 'var(--text-m)', border: '1px solid var(--border)', background: 'var(--surface)' }}
        >
          <Star size={11} className="text-amber-400" />
          <span>Favorilere Ekle</span>
        </button>

        {/* Bildirimler */}
        <button
          type="button"
          className="relative w-9 h-9 flex items-center justify-center rounded-xl transition-colors"
          style={{ color: 'var(--text-m)' }}
          onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--surface2)')}
          onMouseLeave={(e) => (e.currentTarget.style.background = '')}
          title="Bildirimler"
        >
          <Bell size={16} />
          <span className="absolute top-2 right-2 w-1.5 h-1.5 bg-red-500 rounded-full" />
        </button>

        {/* Dark / Light mode */}
        <button
          type="button"
          onClick={toggleDarkMode}
          className="w-9 h-9 flex items-center justify-center rounded-xl transition-colors"
          style={{ color: 'var(--text-m)' }}
          onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--surface2)')}
          onMouseLeave={(e) => (e.currentTarget.style.background = '')}
          title={darkMode ? 'Açık tema' : 'Koyu tema'}
        >
          {darkMode ? <Sun size={16} /> : <Moon size={16} />}
        </button>
      </div>
    </header>
  )
}
