export const TRANSFER_TYPES = [
  { value: 'internal', label: 'İç Transfer' },
  { value: 'replenishment', label: 'İkmal' },
  { value: 'return', label: 'İade' },
  { value: 'adjustment', label: 'Düzeltme' },
]

export const STATUS_MAP: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' | 'danger' }> = {
  draft: { label: 'Taslak', variant: 'neutral' },
  pending: { label: 'Bekliyor', variant: 'warning' },
  picking: { label: 'Toplama', variant: 'warning' },
  picked: { label: 'Toplandı', variant: 'warning' },
  in_transit: { label: 'Yolda', variant: 'warning' },
  delivered: { label: 'Teslim', variant: 'success' },
  completed: { label: 'Tamamlandı', variant: 'success' },
  cancelled: { label: 'İptal', variant: 'danger' },
}

export const TRANSITIONS: Record<string, { status: string; label: string }[]> = {
  draft: [{ status: 'pending', label: 'Onayla' }, { status: 'cancelled', label: 'İptal Et' }],
  pending: [{ status: 'picking', label: 'Toplamaya Başla' }, { status: 'cancelled', label: 'İptal Et' }],
  picking: [{ status: 'picked', label: 'Toplama Tamamlandı' }, { status: 'cancelled', label: 'İptal Et' }],
  picked: [{ status: 'in_transit', label: 'Kargoya Ver' }],
  in_transit: [{ status: 'delivered', label: 'Teslim Alındı' }],
  delivered: [{ status: 'completed', label: 'Tamamla' }],
}
