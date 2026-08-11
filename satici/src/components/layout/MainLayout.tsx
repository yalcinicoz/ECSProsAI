import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import {
  Home, LogOut, Menu, Store, Package, Boxes, ShoppingCart, Wallet, Building2,
} from 'lucide-react'
import { useAuthStore } from '@/store/auth'

/** S3 menüsü — alt fazlar geldikçe maddeler aktifleşir; gelmeyenler "Yakında". */
const NAV_ITEMS = [
  { to: '/', icon: Home, label: 'Panel Özeti', end: true, soon: false },
  { to: '/products', icon: Package, label: 'Ürünlerim' },
  { to: '/orders', icon: ShoppingCart, label: 'Siparişlerim' },
  { to: '/finance', icon: Wallet, label: 'Mali Durum' },
  { to: '/campaigns', icon: Boxes, label: 'Kampanyalar' },
  { to: '/account', icon: Building2, label: 'Hesabım' },
]

function Sidebar({ onMobileClose }: { onMobileClose?: () => void }) {
  const account = useAuthStore((s) => s.account)
  return (
    <aside className="h-full flex flex-col" style={{ background: 'var(--sidebar)' }}>
      {/* Logo */}
      <div className="px-4 py-5 flex items-center gap-2.5">
        <div
          className="w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0"
          style={{ background: 'rgba(52,211,153,.15)' }}
        >
          <Store size={16} color="#34d399" />
        </div>
        <div className="min-w-0">
          <div className="text-white text-sm font-bold tracking-tight truncate">Satıcı Paneli</div>
          <div className="text-[11px] truncate" style={{ color: 'rgba(255,255,255,.4)' }}>
            {account?.title ?? 'ECSPros'}
          </div>
        </div>
      </div>

      {/* Nav */}
      <nav className="flex-1 px-2.5 py-2 space-y-0.5 overflow-y-auto">
        {NAV_ITEMS.map((item) =>
          item.soon ? (
            <span key={item.to} className="nav-lnk opacity-40 cursor-default">
              <item.icon size={15} />
              {item.label}
              <span className="ml-auto text-[9px] font-bold uppercase tracking-wider" style={{ color: 'rgba(255,255,255,.3)' }}>
                Yakında
              </span>
            </span>
          ) : (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) => `nav-lnk${isActive ? ' active' : ''}`}
              onClick={onMobileClose}
            >
              <item.icon size={15} />
              {item.label}
            </NavLink>
          ),
        )}
      </nav>

      {/* Cari kodu */}
      {account && (
        <div className="px-4 py-3 text-[11px]" style={{ color: 'rgba(255,255,255,.35)' }}>
          Cari: {account.code}
        </div>
      )}
    </aside>
  )
}

export function MainLayout() {
  const navigate = useNavigate()
  const [mobileOpen, setMobileOpen] = useState(false)
  const user = useAuthStore((s) => s.user)
  const logout = useAuthStore((s) => s.logout)

  async function handleLogout() {
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="min-h-dvh" style={{ background: 'var(--bg)' }}>
      {/* ── Desktop sidebar (fixed) ── */}
      <div className="hidden md:flex flex-col fixed top-0 left-0 h-full z-50 w-[248px]">
        <Sidebar />
      </div>

      {/* ── Mobile sidebar (overlay) ── */}
      {mobileOpen && (
        <div
          className="fixed inset-0 z-40 md:hidden"
          style={{ background: 'rgba(15,23,42,.5)', backdropFilter: 'blur(2px)' }}
          onClick={() => setMobileOpen(false)}
        >
          <div
            className="absolute left-0 top-0 h-full w-[248px] flex flex-col"
            onClick={(e) => e.stopPropagation()}
          >
            <Sidebar onMobileClose={() => setMobileOpen(false)} />
          </div>
        </div>
      )}

      {/* ── Main content ── */}
      <div className="flex flex-col min-h-dvh md:ml-[248px]">
        {/* Header */}
        <header
          className="flex items-center justify-between gap-3 px-4 md:px-6 h-14 border-b sticky top-0 z-30"
          style={{ background: 'var(--header)', borderColor: 'var(--border)', backdropFilter: 'blur(8px)' }}
        >
          <button
            className="md:hidden p-2 -ml-2 rounded-lg"
            style={{ color: 'var(--text-m)' }}
            onClick={() => setMobileOpen(true)}
            aria-label="Menü"
          >
            <Menu size={18} />
          </button>
          <div className="flex-1" />
          <div className="flex items-center gap-3">
            <span className="text-sm font-medium mob-hide" style={{ color: 'var(--text-m)' }}>
              {user?.fullName}
            </span>
            <button
              className="flex items-center gap-1.5 text-sm px-3 py-1.5 rounded-lg border transition-colors hover:bg-[var(--surface2)]"
              style={{ color: 'var(--text-m)', borderColor: 'var(--border)' }}
              onClick={handleLogout}
            >
              <LogOut size={14} />
              Çıkış
            </button>
          </div>
        </header>

        <main className="flex-1 flex flex-col overflow-hidden">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
