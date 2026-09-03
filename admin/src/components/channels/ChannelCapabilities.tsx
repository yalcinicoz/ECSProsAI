/**
 * Satış kanalı yetenek seti (docs/satis-kanali-ortak-kurgu.md §2.1, K1).
 * Kanal tipi varsayılanı taşır; kanal yalnız OVERRIDABLE alanları ezebilir.
 * Backend: Shared.Contracts/Channels/ChannelCapabilities.cs ile birebir anahtarlar (camelCase).
 */
import { Badge } from '@/components/ui/Badge'
import {
  CAPABILITY_META,
  DEFAULT_CAPABILITIES,
  MARKETPLACE_CAPABILITIES,
  OVERRIDABLE_KEYS,
  isOverridable,
  type CapabilityOverrides,
  type ChannelCapabilities,
} from './channelCapabilitiesModel'

export type { CapabilityOverrides, ChannelCapabilities } from './channelCapabilitiesModel'

const metaOf = (key: keyof ChannelCapabilities) => CAPABILITY_META.find(m => m.key === key)!
const selectLabel = (key: keyof ChannelCapabilities, value: string) => {
  const m = metaOf(key)
  return m.kind === 'select' ? (m.options.find(o => o.value === value)?.label ?? value) : value
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
    if (k === 'minStock') {
      if (typeof v === 'number') next.minStock = v
      else delete next.minStock
    } else if (k === 'thirdPartySellerProducts') {
      if (typeof v === 'boolean') next.thirdPartySellerProducts = v
      else delete next.thirdPartySellerProducts
    } else if (k === 'externalSupplyProducts') {
      if (typeof v === 'boolean') next.externalSupplyProducts = v
      else delete next.externalSupplyProducts
    } else {
      if (typeof v === 'boolean') next.autoPublish = v
      else delete next.autoPublish
    }
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
