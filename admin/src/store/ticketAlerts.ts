// Müşteri İlişkileri — bekleyen (görülmemiş ya da kayda girilmemiş) bildirim sayacı (2026-09-07).
// QuestionAlerts katmanı doldurur (60 sn poll + SignalR TicketNotification); Header çanı ve Sidebar rozeti okur.
import { create } from 'zustand'

interface TicketAlertState {
  pendingCount: number
  setPendingCount: (n: number) => void
}

export const useTicketAlertStore = create<TicketAlertState>((set) => ({
  pendingCount: 0,
  setPendingCount: (n) => set({ pendingCount: n }),
}))
