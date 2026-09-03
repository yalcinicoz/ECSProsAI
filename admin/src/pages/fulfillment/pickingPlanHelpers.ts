import type { BadgeVariant } from '@/components/ui/Badge'

export const PLAN_DURUM: Record<string, [string, BadgeVariant]> = {
  pending: ['Bekliyor', 'warning'],
  picking: ['Toplanıyor', 'info'],
  completed: ['Tamamlandı', 'success'],
  cancelled: ['İptal', 'danger'],
}

export const PLAN_TIP: Record<string, string> = {
  single_item: 'Tek ürünlü',
  bulk: 'Çok ürünlü',
  single: 'Tekli',
  batch: 'Toplu',
  wave: 'Dalga',
}

export function dagitimDurum(assigned: number, total: number): { label: string; variant: BadgeVariant } {
  if (total === 0) return { label: 'Satır yok', variant: 'neutral' }
  if (assigned === 0) return { label: 'Dağıtım yapılmadı', variant: 'danger' }
  if (assigned < total) return { label: `Dağıtım eksik (${assigned}/${total})`, variant: 'warning' }
  return { label: 'Dağıtım tamam', variant: 'success' }
}
