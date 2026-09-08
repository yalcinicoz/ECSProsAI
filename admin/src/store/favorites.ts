import { create } from 'zustand'
import { FAVORITES_KEY, favoriteForRoute, readFavorites } from '@/lib/adminFavorites'
import type { UserFavorites } from '@/lib/adminFavorites'

interface FavoritesState {
  byUser: UserFavorites
  error: string | null
  panelRequest: number
  add: (userId: string, to: string) => boolean
  remove: (userId: string, to: string) => void
}

export const useFavoritesStore = create<FavoritesState>((set) => {
  const save = (byUser: UserFavorites) => {
    try {
      localStorage.setItem(FAVORITES_KEY, JSON.stringify(byUser))
      set((state) => ({ byUser, error: null, panelRequest: state.panelRequest + 1 }))
      return true
    } catch {
      set({ error: 'Favoriler kaydedilemedi. Tarayıcının depolama iznini ve boş alanını kontrol edin.' })
      return false
    }
  }
  return {
    byUser: readFavorites(), error: null, panelRequest: 0,
    add: (userId, to) => {
      const favorite = favoriteForRoute(to)
      if (!userId || !favorite) return false
      const byUser = readFavorites()
      const items = Object.hasOwn(byUser, userId) ? byUser[userId] : []
      if (items.some((item) => item.to === favorite.to)) {
        set((state) => ({ byUser, error: null, panelRequest: state.panelRequest + 1 })); return true
      }
      if (items.length >= 100) { set({ error: 'En fazla 100 kısayol ekleyebilirsiniz.' }); return false }
      return save({ ...byUser, [userId]: [...items, favorite] })
    },
    remove: (userId, to) => {
      if (!userId) return
      const byUser = readFavorites()
      const items = Object.hasOwn(byUser, userId) ? byUser[userId] : []
      save({ ...byUser, [userId]: items.filter((item) => item.to !== to) })
    },
  }
})
