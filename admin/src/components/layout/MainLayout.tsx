import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { Header } from './Header'
import { FavoritesPanel } from './FavoritesPanel'
import { CommandPalette } from './CommandPalette'
import { useUIStore } from '@/store/ui'
import { cn } from '@/lib/utils'

export function MainLayout() {
  const { sidebarCollapsed, sidebarMobileOpen, setSidebarMobileOpen } = useUIStore()

  return (
    <div className="min-h-dvh" style={{ background: 'var(--bg)' }}>

      {/* ── Desktop sidebar (fixed) ── */}
      <div
        className={cn(
          'hidden md:flex flex-col fixed top-0 left-0 h-full z-50',
          'transition-all duration-[280ms] ease-[cubic-bezier(.4,0,.2,1)]',
          sidebarCollapsed ? 'w-[60px]' : 'w-[248px]',
        )}
      >
        <Sidebar />
      </div>

      {/* ── Mobile sidebar (overlay, slides from left) ── */}
      {sidebarMobileOpen && (
        <div
          className="fixed inset-0 z-40 md:hidden"
          style={{ background: 'rgba(15,23,42,.5)', backdropFilter: 'blur(2px)' }}
          onClick={() => setSidebarMobileOpen(false)}
        >
          <div
            className="absolute left-0 top-0 h-full w-[248px] flex flex-col"
            onClick={(e) => e.stopPropagation()}
          >
            <Sidebar onMobileClose={() => setSidebarMobileOpen(false)} />
          </div>
        </div>
      )}

      {/* ── Main content (shifts right on desktop per sidebar width) ── */}
      <div
        className={cn(
          'flex flex-col min-h-dvh',
          'transition-all duration-[280ms] ease-[cubic-bezier(.4,0,.2,1)]',
          // Mobile: no margin (sidebar overlays)
          // Desktop: margin = sidebar width
          sidebarCollapsed ? 'md:ml-[60px]' : 'md:ml-[248px]',
        )}
      >
        <Header onMobileMenuOpen={() => setSidebarMobileOpen(true)} />
        <main className="flex-1 flex flex-col overflow-hidden">
          <Outlet />
        </main>
      </div>

      {/* ── Global overlays ── */}
      <FavoritesPanel />
      <CommandPalette />
    </div>
  )
}
