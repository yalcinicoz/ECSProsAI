export interface ChannelCapabilities {
  pushListing: boolean
  externalTaxonomy: boolean
  readinessLevel: 'light' | 'light_price' | 'full'
  priceSource: 'channel_price_type' | 'channel_price_list' | 'channel_price_readback'
  saleStopWindow: boolean
  remoteDeactivate: boolean
  thirdPartySellerProducts: boolean
  externalSupplyProducts: boolean
  orderDirection: 'internal' | 'partner_push' | 'pull'
  minStock: number
  autoPublish: boolean
  pullsFromPartnerApi: boolean
}

export type CapabilityOverrides = Partial<Pick<ChannelCapabilities,
  'thirdPartySellerProducts' | 'externalSupplyProducts' | 'autoPublish' | 'minStock'>>

export const OVERRIDABLE_KEYS = ['thirdPartySellerProducts', 'externalSupplyProducts', 'autoPublish', 'minStock'] as const

export const DEFAULT_CAPABILITIES: ChannelCapabilities = {
  pushListing: false,
  externalTaxonomy: false,
  readinessLevel: 'light',
  priceSource: 'channel_price_type',
  saleStopWindow: true,
  remoteDeactivate: false,
  thirdPartySellerProducts: false,
  externalSupplyProducts: false,
  orderDirection: 'internal',
  minStock: 1,
  autoPublish: true,
  pullsFromPartnerApi: false,
}

/** Pazaryeri varsayılanı (Trendyol/Amazon…) — "Pazaryeri kanalı" hızlı seçimi için. */
export const MARKETPLACE_CAPABILITIES: ChannelCapabilities = {
  ...DEFAULT_CAPABILITIES,
  pushListing: true,
  externalTaxonomy: true,
  readinessLevel: 'full',
  priceSource: 'channel_price_readback',
  remoteDeactivate: true,
  orderDirection: 'pull',
}

export type CapabilityMeta =
  | { key: keyof ChannelCapabilities; label: string; help: string; kind: 'bool' }
  | { key: keyof ChannelCapabilities; label: string; help: string; kind: 'number'; min: number }
  | { key: keyof ChannelCapabilities; label: string; help: string; kind: 'select'; options: { value: string; label: string }[] }

export const CAPABILITY_META: CapabilityMeta[] = [
  { key: 'pushListing', label: 'Ürün dışarı gönderilir (pazaryeri)', help: 'Ürünler batch/adaptörle karşı tarafa yüklenir. Açıksa bu tip "Pazaryeri" sayılır.', kind: 'bool' },
  { key: 'externalTaxonomy', label: 'Dış kategori/özellik eşlemesi gerekir', help: 'Ürün grubu → dış kategori, özellik/değer eşlemeleri bu kanal için zorunludur.', kind: 'bool' },
  { key: 'readinessLevel', label: 'Hazırlık denetimi', help: 'Ürünün bu kanalda listelenebilmesi için yapılan ön kontrol seviyesi.', kind: 'select',
    options: [{ value: 'light', label: 'Hafif (görsel, fiyat, satış açık)' }, { value: 'light_price', label: 'Hafif + kanal fiyatı var' }, { value: 'full', label: 'Tam (eşleme + zorunlu özellik)' }] },
  { key: 'priceSource', label: 'Fiyat kaynağı', help: 'Kanal fiyatının nereden geldiği.', kind: 'select',
    options: [{ value: 'channel_price_type', label: 'Kanal fiyat tipi' }, { value: 'channel_price_list', label: 'Bayi fiyat listesi' }, { value: 'channel_price_readback', label: 'Kanal fiyatı + pazaryeri geri okuma' }] },
  { key: 'saleStopWindow', label: 'Satış durdurma penceresi', help: 'Ürünün satışı tarih aralığıyla geçici durdurulabilir.', kind: 'bool' },
  { key: 'remoteDeactivate', label: 'Listeden düşürme (deactivate) batch\'i', help: 'Pazaryerinde yüklü ürün uzaktan pasife alınabilir.', kind: 'bool' },
  { key: 'thirdPartySellerProducts', label: 'Üçüncü taraf satıcı ürünleri', help: 'Satıcı panelinden gelen (bizim olmayan) ürünler bu kanalın kapsamına girebilir.', kind: 'bool' },
  { key: 'externalSupplyProducts', label: 'Dış tedarik kaynağı ürünleri', help: 'Dropship tedarik kaynaklarından (dış API/Excel) gelen ürünler kapsama girebilir.', kind: 'bool' },
  { key: 'orderDirection', label: 'Sipariş yönü', help: 'Siparişin bu kanalda nasıl oluştuğu.', kind: 'select',
    options: [{ value: 'internal', label: 'İçeride oluşur (site/POS)' }, { value: 'partner_push', label: 'Bayi Partner API ile gönderir' }, { value: 'pull', label: 'Pazaryerinden çekilir' }] },
  { key: 'minStock', label: 'Stok eşiği (minStock)', help: 'Kanala verilen adet = max(0, net stok − eşik + 1). Eşik 3 ise net 3 adet → 1 verilir.', kind: 'number', min: 0 },
  { key: 'autoPublish', label: 'Kapsama giren ürün otomatik kanalda', help: 'Kapalıysa ürün kapsama girse de personel "Kanala al" demeden satışa açılmaz.', kind: 'bool' },
  { key: 'pullsFromPartnerApi', label: 'Karşı taraf bizim Partner API\'mizi kullanır', help: 'Dropship bayi ürün/stok/fiyatı bizim API\'mizden çeker.', kind: 'bool' },
]

export function isOverridable(key: string): key is (typeof OVERRIDABLE_KEYS)[number] {
  return (OVERRIDABLE_KEYS as readonly string[]).includes(key)
}

export function mergeCapabilities(base: ChannelCapabilities, overrides?: CapabilityOverrides | null): ChannelCapabilities {
  const merged = { ...base }
  if (!overrides) return merged

  if (typeof overrides.thirdPartySellerProducts === 'boolean') merged.thirdPartySellerProducts = overrides.thirdPartySellerProducts
  if (typeof overrides.externalSupplyProducts === 'boolean') merged.externalSupplyProducts = overrides.externalSupplyProducts
  if (typeof overrides.autoPublish === 'boolean') merged.autoPublish = overrides.autoPublish
  if (typeof overrides.minStock === 'number') merged.minStock = overrides.minStock
  return merged
}
