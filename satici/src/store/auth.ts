import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import api from '@/api/client'

export interface SupplierUser {
  id: string
  email: string
  fullName: string
  lastLoginAt: string | null
}

export interface SupplierAccount {
  id: string
  code: string
  title: string
  supplierKind: string
  currency: string
  isActive: boolean
  contactName: string | null
  email: string | null
  phone: string | null
}

interface AuthState {
  user: SupplierUser | null
  account: SupplierAccount | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  fetchMe: () => Promise<void>
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      account: null,
      isAuthenticated: false,

      login: async (email, password) => {
        const { data } = await api.post('/supplier/auth/login', { email, password })
        const { accessToken, refreshToken } = data.data
        localStorage.setItem('supplier_access_token', accessToken)
        localStorage.setItem('supplier_refresh_token', refreshToken)
        set({ isAuthenticated: true })
        // Kullanıcı + cari kart özeti — panel introspection
        const meRes = await api.get('/supplier/me')
        const me = meRes.data.data
        set({ user: me.user, account: me.account })
      },

      logout: async () => {
        const refreshToken = localStorage.getItem('supplier_refresh_token')
        try {
          if (refreshToken) await api.post('/supplier/auth/logout', { refreshToken })
        } catch { /* oturum sunucuda kapatılamasa da yerelde temizlenir */ }
        localStorage.clear()
        set({ user: null, account: null, isAuthenticated: false })
      },

      fetchMe: async () => {
        const { data } = await api.get('/supplier/me')
        set({ user: data.data.user, account: data.data.account })
      },
    }),
    {
      name: 'ecspros-satici-auth',
      partialize: (state) => ({
        user: state.user,
        account: state.account,
        isAuthenticated: state.isAuthenticated,
      }),
    },
  ),
)
