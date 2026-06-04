import { useState } from 'react'
import { NavLink, Link } from 'react-router-dom'
import { cn } from '@/lib/utils'
import { useUIStore } from '@/store/ui'
import { useAuthStore } from '@/store/auth'
import { Search, X } from 'lucide-react'

// ── Nav structure (matches option-h) ──────────────────────────────────────────

interface NavItem {
  label: string
  to: string
  icon: string        // CSS class suffix or svg path — we'll use a small icon map
  badge?: number
}
interface NavSection {
  label: string
  items: NavItem[]
}

// Using inline SVG paths for compact icons (matches FA icons used in option-h)
const ICON: Record<string, React.ReactNode> = {
  gauge:         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M12 2a10 10 0 1 0 10 10"/><path d="M12 12 8.5 8.5"/><circle cx="12" cy="12" r="1"/><path d="M16.51 17.35a8 8 0 0 0 1.49-8.35"/></svg>,
  box:           <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/></svg>,
  sliders:       <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><line x1="4" x2="4" y1="21" y2="14"/><line x1="4" x2="4" y1="10" y2="3"/><line x1="12" x2="12" y1="21" y2="12"/><line x1="12" x2="12" y1="8" y2="3"/><line x1="20" x2="20" y1="21" y2="16"/><line x1="20" x2="20" y1="12" y2="3"/><line x1="2" x2="6" y1="14" y2="14"/><line x1="10" x2="14" y1="8" y2="8"/><line x1="18" x2="22" y1="16" y2="16"/></svg>,
  layers:        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="m12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83Z"/><path d="m22 17.65-9.17 4.16a2 2 0 0 1-1.66 0L2 17.65"/><path d="m22 12.65-9.17 4.16a2 2 0 0 1-1.66 0L2 12.65"/></svg>,
  sitemap:       <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect width="6" height="4" x="9" y="14" rx="1"/><rect width="6" height="4" x="2" y="14" rx="1"/><rect width="6" height="4" x="16" y="14" rx="1"/><rect width="6" height="4" x="9" y="6" rx="1"/><path d="M5 10v4"/><path d="M12 10v4"/><path d="M19 10v4"/><path d="M5 10H19"/></svg>,
  shoppingbag:   <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z"/><line x1="3" x2="21" y1="6" y2="6"/><path d="M16 10a4 4 0 0 1-8 0"/></svg>,
  rotateccw:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/></svg>,
  filetext:      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"/><path d="M14 2v4a2 2 0 0 0 2 2h4"/><line x1="10" x2="16" y1="9" y2="9"/><line x1="10" x2="16" y1="13" y2="13"/><line x1="10" x2="14" y1="17" y2="17"/></svg>,
  handshake:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="m11 17 2 2a1 1 0 1 0 3-3"/><path d="m14 14 2.5 2.5a1 1 0 1 0 3-3l-3.88-3.88a3 3 0 0 0-4.24 0l-.88.88a1 1 0 1 1-3-3l2.81-2.81a5.79 5.79 0 0 1 7.06-.87l.47.28a2 2 0 0 0 1.42.25L21 4"/><path d="m21 3 1 11h-2"/><path d="M3 3 2 14l6.5 6.5a1 1 0 1 0 3-3"/><path d="M3 4h8"/></svg>,
  users:         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>,
  usersround:    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M18 21a8 8 0 0 0-16 0"/><circle cx="10" cy="8" r="5"/><path d="M22 20c0-3.37-2-6.5-4-8a5 5 0 0 0-.45-8.3"/></svg>,
  warehouse:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M22 8.35V20a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V8.35A2 2 0 0 1 3.26 6.5l8-3.2a2 2 0 0 1 1.48 0l8 3.2A2 2 0 0 1 22 8.35Z"/><path d="M6 18h12"/><path d="M6 14h12"/><rect width="8" height="6" x="8" y="18" rx="1"/></svg>,
  boxes:         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M2.97 12.92A2 2 0 0 0 2 14.63v3.24a2 2 0 0 0 .97 1.71l3 1.8a2 2 0 0 0 2.06 0L12 19v-5.5l-5-3-4.03 2.42Z"/><path d="m7 16.5-4.74-2.85"/><path d="m7 16.5 5-3"/><path d="M7 16.5v5.17"/><path d="M12 13.5V19l3.97 2.38a2 2 0 0 0 2.06 0l3-1.8a2 2 0 0 0 .97-1.71v-3.24a2 2 0 0 0-.97-1.71L17 10.5l-5 3Z"/><path d="m17 16.5-5-3"/><path d="m17 16.5 4.74-2.85"/><path d="M17 16.5v5.17"/><path d="M7.97 4.42A2 2 0 0 0 7 6.13v4.37l5 3 5-3V6.13a2 2 0 0 0-.97-1.71l-3-1.8a2 2 0 0 0-2.06 0l-3 1.8Z"/><path d="M12 8 7.26 5.15"/><path d="m12 8 4.74-2.85"/><path d="M12 13.5V8"/></svg>,
  refreshcw:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/><path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"/><path d="M8 16H3v5"/></svg>,
  percent:       <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><line x1="19" x2="5" y1="5" y2="19"/><circle cx="6.5" cy="6.5" r="2.5"/><circle cx="17.5" cy="17.5" r="2.5"/></svg>,
  ticket:        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z"/></svg>,
  gift:          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect x="3" y="8" width="18" height="4" rx="1"/><path d="M12 8v13"/><path d="M19 12v7a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2v-7"/><path d="M7.5 8a2.5 2.5 0 0 1 0-5A4.8 8 0 0 1 12 8a4.8 8 0 0 1 4.5-5 2.5 2.5 0 0 1 0 5"/></svg>,
  creditcard:    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect width="20" height="14" x="2" y="5" rx="2"/><line x1="2" x2="22" y1="10" y2="10"/></svg>,
  plug:          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M12 22v-5"/><path d="M9 8V2"/><path d="M15 8V2"/><path d="M18 8H6a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-6a2 2 0 0 0-2-2Z"/></svg>,
  settings:      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></svg>,
  clipboard:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect width="8" height="4" x="8" y="2" rx="1"/><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><line x1="12" x2="12" y1="11" y2="17"/><line x1="9" x2="15" y1="14" y2="14"/></svg>,
  monitor:       <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect width="20" height="14" x="2" y="3" rx="2"/><line x1="8" x2="16" y1="21" y2="21"/><line x1="12" x2="12" y1="17" y2="21"/></svg>,
  building2:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18Z"/><path d="M6 12H4a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h2"/><path d="M18 9h2a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2h-2"/><path d="M10 6h4"/><path d="M10 10h4"/><path d="M10 14h4"/><path d="M10 18h4"/></svg>,
  globe:         <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><circle cx="12" cy="12" r="10"/><path d="M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20"/><path d="M2 12h20"/></svg>,
  languages:     <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><path d="m5 8 6 6"/><path d="m4 14 6-6 2-3"/><path d="M2 5h12"/><path d="M7 2h1"/><path d="m22 22-5-10-5 10"/><path d="M14 18h6"/></svg>,
  images:        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><rect width="18" height="18" x="3" y="3" rx="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21"/></svg>,
  palette:       <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-[15px] h-[15px]"><circle cx="13.5" cy="6.5" r=".5" fill="currentColor"/><circle cx="17.5" cy="10.5" r=".5" fill="currentColor"/><circle cx="8.5" cy="7.5" r=".5" fill="currentColor"/><circle cx="6.5" cy="12.5" r=".5" fill="currentColor"/><path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.554C21.965 6.012 17.461 2 12 2z"/></svg>,
}

const NAV_SECTIONS: NavSection[] = [
  { label: 'Genel', items: [
    { label: 'Dashboard', to: '/', icon: 'gauge' },
  ]},
  { label: 'Katalog', items: [
    { label: 'Ürün Kartları',      to: '/catalog/products',        icon: 'box' },
    { label: 'Toplu Resim Yükleme',to: '/catalog/bulk-images',     icon: 'images' },
    { label: 'Özellik Tipleri',    to: '/catalog/attribute-types', icon: 'sliders' },
    { label: 'Filtre Renkleri',    to: '/catalog/filter-colors',             icon: 'palette' },
    { label: 'Ürün Grupları',      to: '/catalog/product-groups',            icon: 'layers' },
    { label: 'Kategoriler',        to: '/catalog/categories',                icon: 'sitemap' },
    { label: 'Kanal Kategorileri', to: '/storefront/channel-categories',     icon: 'layout' },
    { label: 'Katalog Ayarları',   to: '/catalog/settings',                  icon: 'settings' },
  ]},
  { label: 'Satış', items: [
    { label: 'Siparişler', to: '/orders',           icon: 'shoppingbag' },
    { label: 'İadeler',    to: '/orders/returns',   icon: 'rotateccw' },
    { label: 'Faturalar',  to: '/orders/invoices',  icon: 'filetext' },
    { label: 'Teklifler',  to: '/orders/quotes',    icon: 'handshake' },
  ]},
  { label: 'Cari', items: [
    { label: 'Cari Kartlar', to: '/accounts',        icon: 'users' },
    { label: 'Cari Grupları', to: '/accounts/groups', icon: 'usersround' },
  ]},
  { label: 'Müşteriler', items: [
    { label: 'Üyeler', to: '/crm/members',       icon: 'users' },
    { label: 'Gruplar', to: '/crm/member-groups', icon: 'usersround' },
  ]},
  { label: 'Stok', items: [
    { label: 'Depolar',          to: '/inventory/warehouses', icon: 'warehouse' },
    { label: 'Stok Takibi',      to: '/inventory/stocks',     icon: 'boxes' },
    { label: 'Stok Hareketleri', to: '/inventory/transfers',  icon: 'refreshcw' },
  ]},
  { label: 'Pazarlama', items: [
    { label: 'Kampanyalar',  to: '/promotion/campaigns',  icon: 'percent' },
    { label: 'Hediye Kartı', to: '/orders/gift-cards',    icon: 'gift' },
  ]},
  { label: 'İçerik', items: [
    { label: 'Menüler',   to: '/navigation/menus', icon: 'sitemap' },
    { label: 'Sayfalar',  to: '/cms/pages', icon: 'filetext' },
  ]},
  { label: 'Sistem', items: [
    { label: 'POS',           to: '/pos/sales',             icon: 'creditcard' },
    { label: 'Entegrasyonlar',to: '/integrations/logs',     icon: 'plug' },
    { label: 'Finans',        to: '/finance/suppliers',     icon: 'clipboard' },
    { label: 'Fulfillment',   to: '/fulfillment/picking-plans', icon: 'boxes' },
    { label: 'Firmalar',        to: '/settings/firms',          icon: 'building2' },
    { label: 'Platform Tipleri',to: '/settings/platform-types', icon: 'globe' },
    { label: 'Satış Kanalları', to: '/settings/channels',       icon: 'shoppingbag' },
    { label: 'Çeviriler',       to: '/settings/translations',   icon: 'languages' },
    { label: 'Ayarlar',         to: '/settings/users',          icon: 'settings' },
  ]},
]

// ── Component ─────────────────────────────────────────────────────────────────

interface SidebarProps {
  onMobileClose?: () => void
}

export function Sidebar({ onMobileClose }: SidebarProps) {
  const { sidebarCollapsed, toggleSidebar } = useUIStore()
  const { user, logout } = useAuthStore()
  const [search, setSearch] = useState('')

  // Filter menu items when search is active
  const filtered = search.trim()
    ? NAV_SECTIONS.map((s) => ({
        ...s,
        items: s.items.filter((i) => i.label.toLowerCase().includes(search.toLowerCase())),
      })).filter((s) => s.items.length > 0)
    : NAV_SECTIONS

  // Initials for user avatar
  const initials = user?.fullName
    ?.split(' ')
    .slice(0, 2)
    .map((n) => n[0])
    .join('')
    .toUpperCase() ?? 'AD'

  return (
    <aside
      className={cn(
        'flex flex-col h-full overflow-hidden white-space-nowrap',
        'transition-all duration-[280ms] ease-[cubic-bezier(.4,0,.2,1)]',
        sidebarCollapsed ? 'w-[60px]' : 'w-[248px]',
      )}
      style={{ background: 'var(--sidebar)' }}
    >
      {/* ── Logo + toggle ── */}
      <div
        className="flex items-center justify-between px-4 py-4 flex-shrink-0"
        style={{ borderBottom: '1px solid rgba(255,255,255,.1)' }}
      >
        <Link to="/" className="flex items-center gap-2.5 min-w-0 hover:opacity-80 transition-opacity">
          <div
            className="w-8 h-8 rounded-xl flex items-center justify-center flex-shrink-0"
            style={{ background: 'var(--brand)' }}
          >
            <span className="text-white font-black text-sm">E</span>
          </div>
          {!sidebarCollapsed && (
            <span className="text-white font-bold text-base tracking-tight truncate">ECSPros</span>
          )}
        </Link>
        <div className="flex items-center gap-1 flex-shrink-0">
          {onMobileClose && (
            <button
              type="button"
              onClick={onMobileClose}
              className="md:hidden w-7 h-7 flex items-center justify-center rounded-lg hover:bg-white/10 transition-colors flex-shrink-0"
              style={{ color: 'rgba(255,255,255,.4)' }}
            >
              <X size={16} />
            </button>
          )}
          {!sidebarCollapsed && (
            <button
              type="button"
              onClick={toggleSidebar}
              className="hidden md:flex w-7 h-7 items-center justify-center rounded-lg hover:bg-white/10 transition-colors flex-shrink-0"
              style={{ color: 'rgba(255,255,255,.3)' }}
              title="Daralt"
            >
              {/* bars-staggered icon */}
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
                <line x1="4" x2="20" y1="6" y2="6"/><line x1="4" x2="14" y1="12" y2="12"/><line x1="4" x2="17" y1="18" y2="18"/>
              </svg>
            </button>
          )}
        </div>
      </div>

      {/* ── Menu search (only when expanded) ── */}
      {!sidebarCollapsed && (
        <div className="px-3 pt-3 pb-1 flex-shrink-0">
          <div className="relative">
            <Search
              size={12}
              className="absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
              style={{ color: 'rgba(255,255,255,.25)' }}
            />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Menüde ara…"
              className="w-full pl-8 pr-3 py-2 text-sm rounded-lg outline-none"
              style={{
                background: 'rgba(255,255,255,.07)',
                border: '1px solid rgba(255,255,255,.1)',
                color: 'rgba(255,255,255,.75)',
              }}
            />
          </div>
        </div>
      )}

      {/* ── Nav ── */}
      <nav className="flex-1 overflow-y-auto thin-scroll px-3 py-2 space-y-0.5">
        {filtered.map((section) => (
          <div key={section.label}>
            {!sidebarCollapsed && (
              <div className="nav-section-lbl">{section.label}</div>
            )}
            {sidebarCollapsed && <div className="h-2" />}
            {section.items.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end
                title={sidebarCollapsed ? item.label : undefined}
                className={({ isActive }) =>
                  cn('nav-lnk', isActive && 'active', sidebarCollapsed && 'justify-center px-0 py-2')
                }
              >
                <span className="ni flex-shrink-0">{ICON[item.icon]}</span>
                {!sidebarCollapsed && <span className="truncate">{item.label}</span>}
                {!sidebarCollapsed && item.badge != null && (
                  <span
                    className="ml-auto text-[10px] font-bold px-1.5 py-0.5 rounded-full flex-shrink-0"
                    style={{ background: 'rgba(52,211,153,.2)', color: '#34d399' }}
                  >
                    {item.badge}
                  </span>
                )}
              </NavLink>
            ))}
          </div>
        ))}
      </nav>

      {/* ── User ── */}
      <div
        className="flex items-center gap-3 px-3 py-3 flex-shrink-0"
        style={{ borderTop: '1px solid rgba(255,255,255,.1)' }}
      >
        <div
          className="w-8 h-8 rounded-full flex items-center justify-center text-white text-xs font-bold flex-shrink-0"
          style={{ background: 'var(--brand)' }}
        >
          {initials}
        </div>
        {!sidebarCollapsed && (
          <>
            <div className="min-w-0 flex-1">
              <div className="text-white text-sm font-medium truncate">{user?.fullName}</div>
              <div className="text-xs truncate" style={{ color: 'rgba(255,255,255,.35)' }}>
                {user?.email}
              </div>
            </div>
            <button
              type="button"
              onClick={logout}
              className="flex-shrink-0 w-7 h-7 flex items-center justify-center rounded-lg hover:bg-white/10 transition-colors"
              style={{ color: 'rgba(255,255,255,.35)' }}
              title="Çıkış"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
                <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" x2="9" y1="12" y2="12"/>
              </svg>
            </button>
          </>
        )}
      </div>
    </aside>
  )
}
