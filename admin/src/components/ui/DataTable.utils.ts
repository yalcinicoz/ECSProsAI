import { apiErrorMessage } from '@/lib/api-error'

export function errText(error: unknown) {
  return apiErrorMessage(error, 'İşlem başarısız oldu.')
}

export const tarih = (value?: string | null) => value ? new Date(value).toLocaleDateString('tr-TR') : '—'
export const tarihSaat = (value?: string | null) => value ? new Date(value).toLocaleString('tr-TR') : '—'
export const para = (amount: number, currency = '₺') =>
  `${amount.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ${currency}`
export const i18nAd = (values?: Record<string, string> | null) => values?.['tr'] ?? Object.values(values ?? {})[0] ?? '—'
