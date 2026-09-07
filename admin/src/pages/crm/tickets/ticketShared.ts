// Müşteri İlişkileri (talep/şikayet) — ortak tipler ve yardımcılar (plan docs/crm-musteri-iliskileri-plani.md v2)
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'

export interface TicketStatus { id: string; code: string; name: string; color: string; sortOrder: number; isHidden: boolean; isResolved: boolean; exemptFromDuplicateCheck: boolean; isDefault: boolean }
export interface TicketSubject { id: string; name: string; type: 'complaint' | 'request'; sortOrder: number; isActive: boolean; requiredFields: string[] }
export interface TicketSettings { statuses: TicketStatus[]; subjects: TicketSubject[] }

export interface TicketListItem {
  id: string; trackingNo: number; type: string; subjectName: string; statusCode: string; statusName: string; statusColor: string
  customerName: string; customerPhone: string; callerName: string; callerPhone: string
  orderNumber: string | null; orderId: string | null; memberId: string | null; firmPlatformId: string | null
  createdByName: string; createdAt: string; updatedByName: string | null; lastActivityAt: string; activityCount: number
  readByMe: boolean; taggedMe: boolean; isHidden: boolean
}
export interface TicketCounter { statusCode: string; statusName: string; color: string; count: number }
export interface TicketPage { items: TicketListItem[]; totalCount: number; page: number; pageSize: number; counters: TicketCounter[] }

export interface TicketRead { userId: string; userName: string; readAt: string }
export interface TicketActivity {
  id: string; bodyHtml: string; attachments: string[]; userId: string | null; userName: string; createdAt: string
  statusName: string; previousStatusName: string | null; statusChanged: boolean; isResolvedStatus: boolean
  taggedUserId: string | null; taggedUserName: string | null; reads: TicketRead[]
}
export interface TicketNotificationTrace { userId: string; kind: string; message: string; sentAt: string; seenAt: string | null; openedAt: string | null }
export interface TicketBrief { id: string; trackingNo: number; subjectName: string; statusName: string; statusColor: string; createdAt: string; createdByName: string }
export interface TicketDetail {
  id: string; trackingNo: number; type: string; subjectId: string; subjectName: string; statusId: string; statusCode: string; statusName: string; statusColor: string; isResolved: boolean
  memberId: string | null; legacyMemberId: number | null; customerName: string; customerPhone: string; callerName: string; callerPhone: string
  orderId: string | null; orderNumber: string | null; firmPlatformId: string | null
  bodyHtml: string; attachments: string[]
  createdByUserId: string | null; createdByName: string; createdAt: string; updatedByName: string | null; updatedAt: string | null
  lastActivityAt: string; isHidden: boolean; legacyId: number | null
  activities: TicketActivity[]; notifications: TicketNotificationTrace[]; previousTickets: TicketBrief[]
}
export interface TicketNotification { id: string; ticketId: string; trackingNo: number; kind: string; message: string; createdAt: string; seenAt: string | null; openedAt: string | null }
export interface OrderCandidate {
  orderId: string; orderNumber: string; firmPlatformId: string; platformName: string; createdAt: string; grandTotal: number; status: string
  memberId: string | null; legacyMemberId: number | null; customerName: string; customerPhone: string
}
export interface AdminUser { id: string; fullName: string; isActive?: boolean }

export const TYPE_LABEL: Record<string, string> = { complaint: 'Şikayet', request: 'Talep' }
export const FIELD_LABEL: Record<string, string> = { orderNumber: 'Sipariş No', callerName: 'Arayan Ad Soyad', callerPhone: 'Arayan Telefon', body: 'İçerik', image: 'Görsel (ek)' }
export const KIND_LABEL: Record<string, string> = { created_by: 'Kaydı açan', tagged: 'Etiketlendi', tagged_followup: 'Etiketli (takip)', participant: 'İşlem yapan' }

export function apiErrorMessage(error: unknown, fallback: string): string {
  if (typeof error !== 'object' || error === null || !('response' in error)) return fallback
  const response = (error as { response?: { data?: { error?: string } } }).response
  return typeof response?.data?.error === 'string' ? response.data.error : fallback
}
export function fmtTarih(s: string | null | undefined) {
  if (!s) return '—'
  const d = new Date(s)
  return d.toLocaleDateString('tr-TR') + ' ' + d.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
}
export function fmtTelefon(p: string | null | undefined) {
  if (!p) return ''
  return p.length === 10 ? `0${p.slice(0, 3)} ${p.slice(3, 6)} ${p.slice(6, 8)} ${p.slice(8)}` : p
}

export function useTicketSettings(includeInactive = false) {
  return useQuery<TicketSettings>({
    queryKey: ['ticket-settings', includeInactive],
    queryFn: async () => (await api.get(`/crm/tickets/settings?includeInactive=${includeInactive}`)).data.data,
    staleTime: 5 * 60 * 1000,
  })
}
export function useAdminUsers() {
  return useQuery<AdminUser[]>({
    queryKey: ['admin-users-active'],
    queryFn: async () => ((await api.get('/iam/users?pageSize=200&activeOnly=true')).data.data?.items ?? []) as AdminUser[],
    staleTime: 5 * 60 * 1000,
  })
}
/** Tüm firmaların platformları (kanal adı için) — cmsPageShared.useFirmPlatforms ile aynı yol. */
export function usePlatformNames() {
  const { data: firms = [] } = useQuery<{ id: string }[]>({ queryKey: ['firms'], queryFn: async () => (await api.get('/core/firms')).data.data })
  return useQuery<Record<string, string>>({
    queryKey: ['platform-names', firms.map((f) => f.id).join(',')],
    queryFn: async () => {
      const m: Record<string, string> = {}
      for (const f of firms) {
        const { data } = await api.get(`/core/firms/${f.id}/platforms`)
        for (const p of data.data ?? []) m[p.id] = p.nameI18n?.tr ?? p.code ?? p.id
      }
      return m
    },
    enabled: firms.length > 0,
    staleTime: 10 * 60 * 1000,
  })
}
/** Yükleme: /crm/tickets/media — Requests deseni. */
export async function uploadTicketFile(f: File): Promise<string> {
  const form = new FormData()
  form.append('file', f)
  const res = await api.post('/crm/tickets/media', form, { headers: { 'Content-Type': 'multipart/form-data' } })
  return res.data.data.url as string
}
