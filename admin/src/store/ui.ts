import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface UIState {
  sidebarCollapsed: boolean   // true = icon-only (60px), false = expanded (248px)
  sidebarMobileOpen: boolean
  darkMode: boolean
  favsPanelOpen: boolean
  cmdOpen: boolean
  toggleSidebar: () => void
  setSidebarMobileOpen: (open: boolean) => void
  toggleDarkMode: () => void
  setFavsPanelOpen: (open: boolean) => void
  setCmdOpen: (open: boolean) => void
}

function applyDark(dark: boolean) {
  if (dark) document.documentElement.classList.add('dark')
  else document.documentElement.classList.remove('dark')
}

export const useUIStore = create<UIState>()(
  persist(
    (set, get) => ({
      sidebarCollapsed: true,
      sidebarMobileOpen: false,
      darkMode: false,
      favsPanelOpen: false,
      cmdOpen: false,

      toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
      setSidebarMobileOpen: (open) => set({ sidebarMobileOpen: open }),
      toggleDarkMode: () => {
        const next = !get().darkMode
        applyDark(next)
        set({ darkMode: next })
      },
      setFavsPanelOpen: (open) => set({ favsPanelOpen: open }),
      setCmdOpen: (open) => set({ cmdOpen: open }),
    }),
    {
      name: 'ecspros-ui',
      partialize: (s) => ({ sidebarCollapsed: s.sidebarCollapsed, darkMode: s.darkMode }),
      onRehydrateStorage: () => (state) => {
        if (state?.darkMode) applyDark(true)
      },
    },
  ),
)
