import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import api from '@/api/client'

export interface AuthUser {
  id: string
  email: string
  fullName: string
  permissions: string[]
  mustChangePassword: boolean
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
          },
        })
      },

      logout: () => {
        localStorage.clear()
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
        return user.permissions.includes(permission) || user.permissions.includes('*')
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
