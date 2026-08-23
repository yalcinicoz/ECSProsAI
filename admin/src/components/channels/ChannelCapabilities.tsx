/**
 * Satış kanalı yetenek seti (docs/satis-kanali-ortak-kurgu.md §2.1, K1).
 * Kanal tipi varsayılanı taşır; kanal yalnız OVERRIDABLE alanları ezebilir.
 * Backend: Shared.Contracts/Channels/ChannelCapabilities.cs ile birebir anahtarlar (camelCase).
 */
import { Badge } from '@/components/ui/Badge'

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

type Meta =
  | { key: keyof ChannelCapabilities; label: string; help: string; kind: 'bool' }
  | { key: keyof ChannelCapabilities; label: string; help: string; kind: 'number'; min: number }
  | { key: keyof ChannelCapabilities; label: string; help: string; kind: 'select'; options: { value: string; label: string }[] }

export const CAPABILITY_META: Meta[] = [
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

const metaOf = (key: keyof ChannelCapabilities) => CAPABILITY_META.find(m => m.key === key)!
const selectLabel = (key: keyof ChannelCapabilities, value: string) => {
  const m = metaOf(key)
  return m.kind === 'select' ? (m.options.find(o => o.value === value)?.label ?? value) : value
}

export function isOverridable(key: string): key is (typeof OVERRIDABLE_KEYS)[number] {
  return (OVERRIDABLE_KEYS as readonly string[]).includes(key)
}

export function mergeCapabilities(base: ChannelCapabilities, overrides?: CapabilityOverrides | null): ChannelCapabilities {
  const out = { ...base }
  if (!overrides) return out
  for (const k of OVERRIDABLE_KEYS) {
    const v = overrides[k]
    if (v === undefined || v === null) continue
    ;(out as any)[k] = v
  }
  return out
}

/** Yetenek rozetleri — liste/kart/başlık için kısa özet. */
export function CapabilityBadges({ caps, compact = false }: { caps: ChannelCapabilities | null | undefined; compact?: boolean }) {
  if (!caps) return null
  const items: { label: string; variant: 'info' | 'success' | 'warning' | 'neutral' }[] = []
  items.push(caps.pushListing ? { label: 'Pazaryeri', variant: 'info' } : { label: caps.pullsFromPartnerApi ? 'Dropship bayi' : 'Kendi kanal', variant: 'neutral' })
  if (caps.externalTaxonomy) items.push({ label: 'Eşleme', variant: 'warning' })
  if (!compact) {
    if (caps.thirdPartySellerProducts) items.push({ label: 'Satıcı ürünleri', variant: 'success' })
    if (caps.externalSupplyProducts) items.push({ label: 'Dış kaynak', variant: 'success' })
    if (caps.minStock > 1) items.push({ label: `Stok eşiği ${caps.minStock}`, variant: 'neutral' })
    if (!caps.autoPublish) items.push({ label: 'Elle kanala al', variant: 'neutral' })
  }
  return (
    <span className="inline-flex flex-wrap gap-1 align-middle">
      {items.map(i => <Badge key={i.label} variant={i.variant}>{i.label}</Badge>)}
    </span>
  )
}

/** Tip düzeyi tam editör (Platform Tipleri sayfası). */
export function CapabilitiesEditor({ value, onChange }: { value: ChannelCapabilities; onChange: (v: ChannelCapabilities) => void }) {
  const set = (key: keyof ChannelCapabilities, v: unknown) => onChange({ ...value, [key]: v } as ChannelCapabilities)
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-2">
        <button type="button" className="text-xs px-2 py-1 rounded-lg"
          style={{ border: '1px solid var(--border)', background: 'var(--surface2)', color: 'var(--text-m)' }}
          onClick={() => onChange({ ...MARKETPLACE_CAPABILITIES })}>Pazaryeri şablonu</button>
        <button type="button" className="text-xs px-2 py-1 rounded-lg"
          style={{ border: '1px solid var(--border)', background: 'var(--surface2)', color: 'var(--text-m)' }}
          onClick={() => onChange({ ...DEFAULT_CAPABILITIES })}>Kendi kanal şablonu</button>
        <button type="button" className="text-xs px-2 py-1 rounded-lg"
          style={{ border: '1px solid var(--border)', background: 'var(--surface2)', color: 'var(--text-m)' }}
          onClick={() => onChange({ ...DEFAULT_CAPABILITIES, readinessLevel: 'light_price', priceSource: 'channel_price_list', orderDirection: 'partner_push', minStock: 3, pullsFromPartnerApi: true })}>Dropship bayi şablonu</button>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-3">
        {CAPABILITY_META.map(m => (
          <div key={m.key} className="flex flex-col gap-1">
            {m.kind === 'bool' ? (
              <label className="flex items-start gap-2 cursor-pointer">
                <input type="checkbox" className="mt-0.5 w-4 h-4 rounded accent-[var(--brand)]"
                  checked={Boolean(value[m.key])} onChange={e => set(m.key, e.target.checked)} />
                <span>
                  <span className="text-sm block" style={{ color: 'var(--text)' }}>{m.label}</span>
                  <span className="text-xs block" style={{ color: 'var(--text-s)' }}>{m.help}</span>
                </span>
              </label>
            ) : m.kind === 'number' ? (
              <div>
                <label className="flbl">{m.label}</label>
                <input type="number" min={m.min} className="inp" value={String(value[m.key] ?? 0)}
                  onChange={e => set(m.key, Math.max(m.min, parseInt(e.target.value || '0', 10) || 0))} />
                <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>{m.help}</p>
              </div>
            ) : (
              <div>
                <label className="flbl">{m.label}</label>
                <select className="sel" value={String(value[m.key])} onChange={e => set(m.key, e.target.value)}>
                  {m.options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                </select>
                <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>{m.help}</p>
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}

/**
 * Kanal düzeyi ezme editörü (kanal formu). Yalnız OVERRIDABLE_KEYS; her alan "Tip varsayılanı"
 * ya da açık değer. Diğer yetenekler salt okunur özet olarak gösterilir.
 */
export function CapabilityOverridesEditor({ base, overrides, onChange }: {
  base: ChannelCapabilities
  overrides: CapabilityOverrides
  onChange: (o: CapabilityOverrides) => void
}) {
  const setKey = (k: (typeof OVERRIDABLE_KEYS)[number], v: unknown) => {
    const next: CapabilityOverrides = { ...overrides }
    if (v === undefined) delete (next as any)[k]
    else (next as any)[k] = v
    onChange(next)
  }
  const readOnly = CAPABILITY_META.filter(m => !isOverridable(m.key))
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-1.5 text-xs" style={{ color: 'var(--text-s)' }}>
        {readOnly.map(m => (
          <span key={m.key} className="px-2 py-0.5 rounded-full" style={{ border: '1px solid var(--border)', background: 'var(--surface)' }}
            title={m.help}>
            {m.label}: <b style={{ color: 'var(--text-m)' }}>
              {m.kind === 'bool' ? (base[m.key] ? 'Evet' : 'Hayır') : m.kind === 'select' ? selectLabel(m.key, String(base[m.key])) : String(base[m.key])}
            </b>
          </span>
        ))}
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        {OVERRIDABLE_KEYS.map(k => {
          const m = metaOf(k)
          const ov = overrides[k]
          const isDefault = ov === undefined || ov === null
          if (m.kind === 'number') {
            return (
              <div key={k}>
                <label className="flbl">{m.label}</label>
                <div className="flex items-center gap-2">
                  <select className="sel" value={isDefault ? 'default' : 'custom'}
                    onChange={e => setKey(k, e.target.value === 'default' ? undefined : (base[k] as number))}>
                    <option value="default">Tip varsayılanı ({String(base[k])})</option>
                    <option value="custom">Bu kanala özel</option>
                  </select>
                  {!isDefault && (
                    <input type="number" min={m.min} className="inp w-24" value={String(ov)}
                      onChange={e => setKey(k, Math.max(m.min, parseInt(e.target.value || '0', 10) || 0))} />
                  )}
                </div>
                <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>{m.help}</p>
              </div>
            )
          }
          return (
            <div key={k}>
              <label className="flbl">{m.label}</label>
              <select className="sel" value={isDefault ? 'default' : ov ? 'true' : 'false'}
                onChange={e => setKey(k, e.target.value === 'default' ? undefined : e.target.value === 'true')}>
                <option value="default">Tip varsayılanı ({base[k] ? 'Evet' : 'Hayır'})</option>
                <option value="true">Evet</option>
                <option value="false">Hayır</option>
              </select>
              <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>{m.help}</p>
            </div>
          )
        })}
      </div>
    </div>
  )
}
