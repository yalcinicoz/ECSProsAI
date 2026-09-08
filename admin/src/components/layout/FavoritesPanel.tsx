import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { X, Star, Plus, Search } from 'lucide-react'
import { useUIStore } from '@/store/ui'
import { useAuthStore } from '@/store/auth'
import { useFavoritesStore } from '@/store/favorites'
import { favoriteForRoute, matchesFavorite } from '@/lib/adminFavorites'
import { NAV_SECTIONS, findActiveItem, permittedSections } from './sidebarNavigation'
import api from '@/api/client'

export function FavoritesPanel() {
  const { favsPanelOpen, setFavsPanelOpen } = useUIStore()
  const user = useAuthStore((state) => state.user)
  const hasPermission = useAuthStore((state) => state.hasPermission)
  const { byUser, add, remove, error, panelRequest } = useFavoritesStore()
  const navigate = useNavigate()
  const location = useLocation()
  const panelRef = useRef<HTMLDivElement>(null)
  const searchRef = useRef<HTMLInputElement>(null)
  const [search, setSearch] = useState('')
  const [adding, setAdding] = useState(false)
  const [seenRequest, setSeenRequest] = useState(panelRequest)
  // Üst başlıktan eklenen sayfa eski arama/ekleme ekranının arkasında kalmasın.
  if (seenRequest !== panelRequest) {
    setSeenRequest(panelRequest)
    setAdding(false)
    setSearch('')
  }
  const items = user && Object.hasOwn(byUser, user.id) ? byUser[user.id] : []
  const allowed = (to: string) => {
    const item = findActiveItem(to.split(/[?#]/)[0])
    return Boolean(item && (!item.permission || hasPermission(item.permission)))
  }
  const favorites = items.filter((item) => allowed(item.to))
  const visible = favorites.filter((item) => matchesFavorite(item, search))
  const choices = permittedSections(NAV_SECTIONS, hasPermission)
    .map((section) => ({ ...section, items: section.items.filter((item) => matchesFavorite(item, search)) }))
    .filter((section) => section.items.length > 0)
  const current = favoriteForRoute(location.pathname + location.search + location.hash)
  // Önceki panelin canlı sayaçları korunur; yalnız ilgili favori görünürken sorgulanır.
  const { data: pendingOrders = 0 } = useQuery<number>({
    queryKey: ['fav-pending-orders', user?.id],
    queryFn: async () => (await api.get('/orders?status=pending&pageSize=1')).data.data?.totalCount ?? 0,
    enabled: favsPanelOpen && Boolean(user) && visible.some((item) => item.to.split(/[?#]/)[0] === '/orders'),
    refetchInterval: 60_000, retry: false,
  })
  const { data: stockAlerts = 0 } = useQuery<number>({
    queryKey: ['fav-stock-alerts', user?.id],
    queryFn: async () => (await api.get('/store-notifications/stock-alerts?status=active&pageSize=1')).data.data?.totalCount ?? 0,
    enabled: favsPanelOpen && Boolean(user) && visible.some((item) => item.to.split(/[?#]/)[0] === '/storefront/notifications'),
    refetchInterval: 60_000, retry: false,
  })

  useEffect(() => {
    if (!favsPanelOpen) return
    searchRef.current?.focus()
    function outside(event: MouseEvent) {
      const target = event.target as Node
      if (!panelRef.current?.contains(target) && !document.getElementById('fav-btn')?.contains(target)) {
        setFavsPanelOpen(false)
      }
    }
    function escape(event: KeyboardEvent) {
      if (event.key !== 'Escape') return
      if (adding) { setAdding(false); setSearch('') }
      else { setFavsPanelOpen(false); document.getElementById('fav-btn')?.focus() }
    }
    document.addEventListener('mousedown', outside)
    document.addEventListener('keydown', escape)
    return () => {
      document.removeEventListener('mousedown', outside)
      document.removeEventListener('keydown', escape)
    }
  }, [favsPanelOpen, adding, setFavsPanelOpen])

  function addFavorite(to: string) {
    if (user && allowed(to) && add(user.id, to)) {
      setAdding(false)
      setSearch('')
    }
  }

  return (
    <>
      <button id="fav-btn" type="button" onClick={() => setFavsPanelOpen(!favsPanelOpen)}
        title="Sık Kullanılanlar" aria-label="Sık Kullanılanlar" aria-expanded={favsPanelOpen} aria-controls="favorites-panel">
        <Star size={12} />
      </button>
      <div id="favorites-panel" ref={panelRef} inert={!favsPanelOpen} aria-hidden={!favsPanelOpen}
        aria-label="Sık Kullanılanlar" role="complementary"
        className="fixed top-0 right-0 h-full w-[272px] max-w-full flex flex-col z-[59]"
        style={{ background: 'var(--surface)', borderLeft: '1px solid var(--border)',
          boxShadow: '-8px 0 28px rgba(0,0,0,.14)',
          transform: favsPanelOpen ? 'translateX(0)' : 'translateX(100%)',
          transition: 'transform .28s cubic-bezier(.4,0,.2,1)' }}>
        <div className="flex items-center justify-between px-4 py-4 border-b flex-shrink-0" style={{ borderColor: 'var(--border)' }}>
          <div>
            <h3 className="font-bold text-sm" style={{ color: 'var(--text)' }}>{adding ? 'Kısayol Ekle' : 'Sık Kullanılanlar'}</h3>
            <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>Bu tarayıcıdaki kişisel kısayolların</p>
          </div>
          <button type="button" aria-label="Sık Kullanılanları kapat"
            onClick={() => { setFavsPanelOpen(false); document.getElementById('fav-btn')?.focus() }}
            className="w-8 h-8 flex items-center justify-center rounded-lg focus-visible:outline-2 focus-visible:outline-emerald-500"
            style={{ color: 'var(--text-s)' }}><X size={15} /></button>
        </div>
        <div className="px-3 py-3">
          <div className="relative">
            <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2" style={{ color: 'var(--text-s)' }} />
            <input ref={searchRef} type="search" aria-label={adding ? 'Kısayol ara' : 'Favorilerde ara'}
              placeholder={adding ? 'Menüde kısayol ara…' : 'Favorilerde ara…'} value={search}
              onChange={(event) => setSearch(event.target.value)}
              className="w-full rounded-lg border pl-9 pr-8 py-2 text-sm focus-visible:outline-2 focus-visible:outline-emerald-500"
              style={{ background: 'var(--surface2)', borderColor: 'var(--border)', color: 'var(--text)' }} />
            {search && <button type="button" aria-label="Favori aramasını temizle"
              onClick={() => { setSearch(''); searchRef.current?.focus() }}
              className="absolute right-2 top-1/2 -translate-y-1/2" style={{ color: 'var(--text-m)' }}><X size={14} /></button>}
          </div>
        </div>
        {error && <p role="alert" className="px-4 text-xs text-red-600">{error}</p>}
        <div className="flex-1 overflow-y-auto thin-scroll px-3 py-2">
          {adding ? <>
            {current && allowed(current.to) && <button type="button" disabled={!user} onClick={() => addFavorite(current.to)}
              className="w-full text-left rounded-lg px-2 py-2 text-sm mb-2" style={{ background: 'var(--brand-bg)', color: 'var(--brand)' }}>
              Bulunduğum sayfayı ekle
            </button>}
            {choices.map((section) => <div key={section.id}>
              <p className="text-[10px] font-bold uppercase px-2 py-2" style={{ color: 'var(--text-s)' }}>{section.label}</p>
              {section.items.map((item) => {
                const saved = favorites.some((favorite) => favorite.to === item.to)
                return <button key={item.to} type="button" disabled={saved || !user} onClick={() => addFavorite(item.to)}
                  className="w-full flex items-center gap-2 text-left rounded-lg px-2 py-2 text-sm hover:bg-[var(--surface2)] disabled:opacity-50"
                  style={{ color: 'var(--text-m)' }}>
                  {saved ? <Star size={13} className="text-amber-500" /> : <Plus size={13} />}
                  <span className="flex-1">{item.label}</span>{saved && <span className="text-xs">Eklendi</span>}
                </button>
              })}
            </div>)}
            {choices.length === 0 && <p role="status" className="text-sm px-2" style={{ color: 'var(--text-s)' }}>Eşleşen kısayol bulunamadı.</p>}
          </> : <>
            {visible.map((item) => <div key={item.to} className="flex items-center gap-1">
              <button type="button" title={item.to} onClick={() => { navigate(item.to); setFavsPanelOpen(false) }}
                className="flex-1 min-w-0 flex items-center gap-2 text-left rounded-lg px-2 py-2 text-sm hover:bg-[var(--surface2)]"
                style={{ color: 'var(--text-m)' }}>
                <Star size={13} className="flex-shrink-0 text-amber-400" /><span className="break-words min-w-0">{item.label}</span>
                {item.to.split(/[?#]/)[0] === '/orders' && pendingOrders > 0 && <span title="Bekleyen siparişler"
                  className="ml-auto text-[10px] px-1.5 rounded-full bg-amber-100 text-amber-700">{pendingOrders}</span>}
                {item.to.split(/[?#]/)[0] === '/storefront/notifications' && stockAlerts > 0 && <span title="Stok uyarıları"
                  className="ml-auto text-[10px] px-1.5 rounded-full bg-red-100 text-red-600">{stockAlerts}</span>}
              </button>
              <button type="button" aria-label={item.label + ' favorisini kaldır'} onClick={() => { if (user) remove(user.id, item.to) }}
                className="p-1.5 rounded hover:bg-[var(--surface2)] text-red-500"><X size={13} /></button>
            </div>)}
            {visible.length === 0 && <p role="status" className="text-sm px-2" style={{ color: 'var(--text-s)' }}>
              {search.trim() ? 'Aramanızla eşleşen favori bulunamadı.' : 'Henüz favori eklemediniz. Bir sayfada Favorilere Ekle veya aşağıdaki Kısayol Ekle düğmesini kullanın.'}
            </p>}
          </>}
        </div>
        <div className="px-4 py-3 border-t flex-shrink-0" style={{ borderColor: 'var(--border)' }}>
          <button type="button" onClick={() => { setAdding(!adding); setSearch('') }}
            className="w-full py-2 rounded-xl text-sm font-semibold flex items-center justify-center gap-1.5"
            style={{ background: 'var(--brand-bg)', color: 'var(--brand)', border: '1px solid var(--brand-b)' }}>
            <Plus size={13} /> {adding ? 'Favorilere Dön' : 'Kısayol Ekle'}
          </button>
        </div>
      </div>
    </>
  )
}
