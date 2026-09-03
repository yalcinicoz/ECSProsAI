export const ORDER_STATUS_MAP: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' | 'danger' }> = {
  pending: { label: 'Bekleyen', variant: 'warning' },
  confirmed: { label: 'Onaylı', variant: 'warning' },
  processing: { label: 'İşlemde', variant: 'warning' },
  shipped: { label: 'Kargoda', variant: 'success' },
  delivered: { label: 'Teslim', variant: 'success' },
  cancelled: { label: 'İptal', variant: 'danger' },
  returned: { label: 'İade', variant: 'danger' },
}

export const PAYMENT_STATUS_MAP: Record<string, string> = {
  pending: 'Bekliyor',
  unpaid: 'Ödenmedi',
  paid: 'Ödendi',
  partial: 'Kısmi',
  refunded: 'İade Edildi',
  failed: 'Başarısız',
}

export const PAYMENT_METHOD_MAP: Record<string, string> = {
  'kart': 'Kart (Online)',
  'kapida-nakit': 'Kapıda Nakit',
  'kapida-kart': 'Kapıda Kart',
}

export const INVOICE_STATUS_MAP: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' | 'danger' }> = {
  created: { label: 'Oluşturuldu', variant: 'success' },
  cancelled: { label: 'İptal', variant: 'danger' },
}

export const INVOICE_TYPE_MAP: Record<string, string> = {
  e_archive: 'e-Arşiv',
  e_invoice: 'e-Fatura',
  export: 'İhracat',
}

export const RETURN_STATUS_MAP: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' | 'danger' }> = {
  requested: { label: 'Talep Edildi', variant: 'warning' },
  approved: { label: 'Onaylandı', variant: 'warning' },
  received: { label: 'Teslim Alındı', variant: 'success' },
  refunded: { label: 'Geri Ödendi', variant: 'success' },
  rejected: { label: 'Reddedildi', variant: 'danger' },
}
