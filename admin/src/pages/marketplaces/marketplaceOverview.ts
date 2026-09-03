import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'

export interface MarketplaceStore {
  id: string
  firmId: string
  firmCode: string
  firmNameI18n: Record<string, string>
  platformTypeId: string
  platformTypeCode: string
  platformTypeNameI18n: Record<string, string>
  code: string
  nameI18n: Record<string, string>
  isActive: boolean
  hasCredentials: boolean
  integrationId: string | null
  serviceCode: string | null
  uploadedListings: number
  pendingListings: number
  failedListings: number
  deactivatedListings: number
  toUploadProducts: number
  openOrders: number
  todayOrders: number
  lastSyncAt: string | null
  openIssues: number
}

export const MP_BRAND: Record<string, { bg: string; label: string }> = {
  trendyol: { bg: '#f27a1a', label: 'TY' },
  hepsiburada: { bg: '#ff6000', label: 'HB' },
  n11: { bg: '#7b2d8b', label: 'n11' },
  amazon: { bg: '#232f3e', label: 'AMZ' },
  ciceksepeti: { bg: '#e5468a', label: 'ÇS' },
  pazarama: { bg: '#00a8a8', label: 'PZ' },
}

export function pickTr(i18n: Record<string, string> | undefined, fallback = '') {
  if (!i18n) return fallback
  return i18n['tr'] ?? i18n[Object.keys(i18n)[0]] ?? fallback
}

export type StoreHealth = { level: 'ok' | 'warn' | 'err' | 'off'; text: string }

export function storeHealth(s: MarketplaceStore): StoreHealth {
  if (!s.isActive) return { level: 'off', text: 'Mağaza pasif — senkron durduruldu' }
  if (s.failedListings > 0)
    return { level: 'err', text: `${s.failedListings} üründe senkron hatası` }
  if (s.openIssues > 0)
    return { level: 'warn', text: `${s.openIssues} açık sorun — Sorunlar sekmesine bakın` }
  if (!s.integrationId)
    return { level: 'warn', text: 'Bağlantı kurulmadı — pazaryeri sözleşmesi/API bilgisi eksik' }
  if (!s.lastSyncAt) return { level: 'ok', text: 'Bağlantı hazır — henüz senkron yapılmadı' }
  return { level: 'ok', text: `Bağlantı sağlıklı · son senkron ${timeAgo(s.lastSyncAt)}` }
}

export function timeAgo(iso: string) {
  const dk = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60000))
  if (dk < 1) return 'az önce'
  if (dk < 60) return `${dk} dk önce`
  const saat = Math.round(dk / 60)
  if (saat < 24) return `${saat} saat önce`
  return new Date(iso).toLocaleDateString('tr-TR')
}

export function daysSince(value: string | null | undefined): number | null {
  return value ? Math.floor((Date.now() - new Date(value).getTime()) / 86400000) : null
}

export const HEALTH_COLOR: Record<StoreHealth['level'], string> = {
  ok: 'var(--brand)',
  warn: '#f59e0b',
  err: '#ef4444',
  off: 'var(--text-s)',
}

export function useMarketplaceOverview() {
  return useQuery<MarketplaceStore[]>({
    queryKey: ['marketplaces-overview'],
    queryFn: async () => {
      const { data } = await api.get('/marketplaces/overview')
      return data.data ?? []
    },
    staleTime: 60 * 1000,
  })
}
