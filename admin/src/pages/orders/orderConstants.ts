export const ORDER_STATUS_MAP: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' | 'danger' }> = {
  pending: { label: 'Bekleyen', variant: 'warning' },
  confirmed: { label: 'Onaylı', variant: 'warning' },
  processing: { label: 'İşlemde', variant: 'warning' },
  shipped: { label: 'Kargoda', variant: 'success' },
  delivered: { label: 'Teslim', variant: 'success' },
  cancelled: { label: 'İptal', variant: 'danger' },
  // İade planı R5 (2026-09-10): `returned` YALNIZ teslimatsız iadedir (müşteri iadesi sipariş durumunu değiştirmez)
  returned: { label: 'Teslimatsız İade', variant: 'danger' },
}

export const PAYMENT_STATUS_MAP: Record<string, string> = {
  pending: 'Bekliyor',
  unpaid: 'Ödenmedi',
  paid: 'Ödendi',
  partial: 'Kısmi',
  underpaid: 'Eksik Ödeme',
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
  closed: { label: 'Tamamlandı (geri ödeme yok)', variant: 'neutral' },   // İade planı §2.3
  rejected: { label: 'Reddedildi', variant: 'danger' },
}

// İade planı (2026-09-10) — metinler DurumEtiketleri.Panel ile birebir (IadeTipi / GeriOdemeDurumu / GeriOdemeYokNedeni)
export const RETURN_TYPE_MAP: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' | 'danger' | 'info' }> = {
  undelivered: { label: 'Teslimatsız İade', variant: 'danger' },
  customer: { label: 'Müşteri İadesi', variant: 'info' },
}

export const REFUND_STATUS_MAP: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' | 'danger' }> = {
  pending: { label: 'Bekliyor', variant: 'warning' },
  completed: { label: 'Tamamlandı', variant: 'success' },
  not_applicable: { label: 'Geri ödeme yok', variant: 'neutral' },
}

export const REFUND_NA_REASON_MAP: Record<string, string> = {
  cod_not_collected: 'Kapıda ödeme — tahsilat yapılmadı',
  marketplace: 'Pazaryeri siparişi — iadeyi pazaryeri yapar',
  unpaid: 'Müşteriden tahsilat yok',
  already_refunded: 'Tahsil edilen tutarın tamamı zaten iade edildi',
}

export const REFUND_METHOD_MAP: Record<string, string> = {
  card_refund: 'Karta iade',
  bank_transfer: 'Havale/EFT',
  wallet: 'Cüzdan',
  original_payment: 'Orijinal ödeme',
  cash: 'Nakit',
  none: '— (geri ödeme yok)',
}

// FE1: fatura numarasının kaynağı
export const INVOICE_SOURCE_MAP: Record<string, string> = {
  internal: 'Bizim seri',
  erp: 'ERP',
  marketplace: 'Pazaryeri',
  integrator: 'Entegratör',
}
