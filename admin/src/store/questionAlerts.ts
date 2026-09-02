// Satıcıya Soru Sor — cevap bekleyen soru sayacı (2026-09-02). QuestionAlerts bileşeni
// doldurur (60 sn poll + SignalR anlık olay); Sidebar rozeti ve Dashboard kartı okur.
import { create } from 'zustand'

interface QuestionAlertState {
  pendingCount: number
  setPendingCount: (n: number) => void
}

export const useQuestionAlertStore = create<QuestionAlertState>((set) => ({
  pendingCount: 0,
  setPendingCount: (n) => set({ pendingCount: n }),
}))
