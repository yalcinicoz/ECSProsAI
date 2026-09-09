import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import api from '@/api/client'
import { clearSessionStoragePreservingFavorites } from '@/lib/adminFavorites'

export interface AuthUser {
  id: string
  email: string
  fullName: string
  permissions: string[]
  mustChangePassword: boolean
  /** K5: süper admin sistem bayrağı — tüm yetki kontrollerini geçer (permission değildir). */
  isSuperAdmin?: boolean
}

interface AuthState {
  user: AuthUser | null
  accessToken: string | null
  refreshToken: string | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
  fetchMe: () => Promise<void>
  hasPermission: (permission: string) => boolean
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      accessToken: null,
      refreshToken: null,
      isAuthenticated: false,

      login: async (email, password) => {
        const { data } = await api.post('/auth/login', { username: email, password })
        const { accessToken, refreshToken } = data.data
        localStorage.setItem('access_token', accessToken)
        localStorage.setItem('refresh_token', refreshToken)
        set({ accessToken, refreshToken, isAuthenticated: true })
        // Permissions JWT'den okunur — /auth/me ile çek
        const meRes = await api.get('/auth/me')
        const me = meRes.data.data
        set({
          user: {
            id: me.userId,
            email: me.email,
            fullName: me.fullName,
            permissions: me.permissions ?? [],
            mustChangePassword: me.mustChangePassword ?? false,
            isSuperAdmin: me.isSuperAdmin === true,
          },
        })
      },

      logout: () => {
        clearSessionStoragePreservingFavorites()
        set({ user: null, accessToken: null, refreshToken: null, isAuthenticated: false })
      },

      fetchMe: async () => {
        const { data } = await api.get('/auth/me')
        const me = data.data
        set({
          user: {
            id: me.userId,
            email: me.email,
            fullName: me.fullName,
            permissions: me.permissions ?? [],
            mustChangePassword: me.mustChangePassword ?? false,
          },
        })
      },

      hasPermission: (permission) => {
        const { user } = get()
        if (!user) return false
        // K5 (2026-09-09): süper admin permission değil, kullanıcı üzerinde sistem bayrağıdır;
        // tüm kontrolleri geçer. ("*" permission'ı hiç var olmadı — eski ölü kontrol kaldırıldı.)
        if (user.isSuperAdmin) return true
        return user.permissions.includes(permission)
      },
    }),
    {
      name: 'ecspros-auth',
      partialize: (state) => ({
        accessToken: state.accessToken,
        refreshToken: state.refreshToken,
        user: state.user,
        isAuthenticated: state.isAuthenticated,
      }),
    },
  ),
)
