import { useState, useMemo } from 'react'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Eye, EyeOff, Globe, ShoppingBag, Building2 } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'
import type { PlatformType, SchemaField } from './PlatformTypesPage'
import { CapabilityBadges, CapabilityOverridesEditor, DEFAULT_CAPABILITIES, MARKETPLACE_CAPABILITIES, mergeCapabilities, type CapabilityOverrides, type ChannelCapabilities } from '@/components/channels/ChannelCapabilities'
import { getFieldLabel } from './PlatformTypesPage'

// ── Types ─────────────────────────────────────────────────────────────────────

interface FirmPlatform {
  id: string
  firmId: string
  platformTypeId: string
  platformTypeCode: string
  platformTypeNameI18n: Record<string, string>
  platformTypeIsMarketplace: boolean
  settingsSchema: SchemaField[] | null
  code: string
  nameI18n: Record<string, string>
  priceType: string | null
  priceMultiplier: number | null
  credentials: Record<string, unknown>
  settings: Record<string, unknown>
  isActive: boolean
  createdAt: string
  capabilities?: ChannelCapabilities          // etkin (tip + kanal ezmesi)
  capabilityOverrides?: CapabilityOverrides | null
}

interface FirmPlatformWithFirm extends FirmPlatform {
  firmName: string
}

interface Firm {
  id: string
  code: string
  nameI18n: Record<string, string>
  isMain: boolean
  isActive: boolean
}

const PRICE_TYPES = [
  { value: '', label: '— Yok —' },
  { value: 'manual', label: 'Manuel' },
  { value: 'multiplier', label: 'Çarpan' },
]

function getFirmName(f: Pick<Firm, 'nameI18n' | 'code'>) {
  const n = f.nameI18n
  if (!n) return f.code
  return n['tr'] ?? n[Object.keys(n)[0]] ?? f.code
}

function getPlatformTypeName(pt: Pick<FirmPlatform, 'platformTypeCode' | 'platformTypeNameI18n'>) {
  const n = pt.platformTypeNameI18n
  if (!n) return pt.platformTypeCode
  return n['tr'] ?? n[Object.keys(n)[0]] ?? pt.platformTypeCode
}

function getChannelName(ch: Pick<FirmPlatform, 'code' | 'nameI18n'>) {
  const n = ch.nameI18n
  if (!n) return ch.code
  return n['tr'] ?? n[Object.keys(n)[0]] ?? ch.code
}

// ── Dynamic Field ─────────────────────────────────────────────────────────────

function DynamicField({
  field, value, onChange,
}: {
  field: SchemaField
  value: string
  onChange: (v: string) => void
}) {
  const [showPass, setShowPass] = useState(false)
  const label = getFieldLabel(field)

  if (field.type === 'boolean') {
    return (
      <label className="flex items-center gap-2 cursor-pointer">
        <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
          checked={value === 'true'}
          onChange={e => onChange(e.target.checked ? 'true' : 'false')} />
        <span className="text-sm" style={{ color: 'var(--text)' }}>{label}</span>
        {field.required && <span style={{ color: '#ef4444' }}>*</span>}
      </label>
    )
  }

  const inputType = field.type === 'password' && !showPass ? 'password'
    : field.type === 'number' ? 'number'
    : field.type === 'date' ? 'date'
    : 'text'

  return (
    <div>
      <label className="flbl">
        {label}
        {field.required && <span style={{ color: '#ef4444' }}> *</span>}
        {field.type === 'password' && (
          <span className="ml-1 text-xs font-normal" style={{ color: 'var(--text-s)' }}>(şifreli alan)</span>
        )}
      </label>
      <div className="relative">
        <input
          type={inputType}
          className="inp"
          style={field.type === 'password' ? { paddingRight: 36 } : {}}
          value={value}
          onChange={e => onChange(e.target.value)}
          placeholder={field.type === 'date' ? 'GG.AA.YYYY' : label}
        />
        {field.type === 'password' && (
          <button
            type="button"
            onClick={() => setShowPass(s => !s)}
            className="absolute right-2 top-1/2 -translate-y-1/2 p-1 rounded transition-colors hover:opacity-60"
          >
            {showPass
              ? <EyeOff size={14} style={{ color: 'var(--text-s)' }} />
              : <Eye size={14} style={{ color: 'var(--text-s)' }} />}
          </button>
        )}
      </div>
    </div>
  )
}

// ── Exported Types ────────────────────────────────────────────────────────────

export type { FirmPlatformWithFirm, Firm }

// ── Channel Form ──────────────────────────────────────────────────────────────

interface ChannelFormProps {
  platformTypes: PlatformType[]
  firms: Firm[]
  initialFirmId: string | undefined   // undefined = "Tümü" tab açık, form firma seçer
  target: FirmPlatformWithFirm | null
  onClose: () => void
  onSuccess: () => void
}

export function ChannelForm({ platformTypes, firms, initialFirmId, target, onClose, onSuccess }: ChannelFormProps) {
  const queryClient = useQueryClient()
  const isEdit = !!target

  const [firmId, setFirmId] = useState<string>(
    target?.firmId ?? initialFirmId ?? (firms.length === 1 ? firms[0].id : '')
  )
  const [platformTypeId, setPlatformTypeId] = useState(target?.platformTypeId ?? '')
  const [nameTr, setNameTr] = useState(target?.nameI18n['tr'] ?? '')
  const [code] = useState(target?.code ?? '')
  const [priceType, setPriceType] = useState(target?.priceType ?? '')
  const [multiplier, setMultiplier] = useState(
    target?.priceMultiplier != null ? String(target.priceMultiplier) : ''
  )
  const [isActive, setIsActive] = useState(target?.isActive ?? true)
  // Stok görünürlüğü (şema-dışı, kanal ayarı): stoğu biten ürünleri listede göster + tarih eşiği.
  const [showOutOfStock, setShowOutOfStock] = useState(target?.settings?.['showOutOfStock'] === true)
  const [oosSince, setOosSince] = useState(
    typeof target?.settings?.['outOfStockVisibleSince'] === 'string'
      ? String(target.settings['outOfStockVisibleSince']).slice(0, 10) : ''
  )
  // Ödeme yöntemleri (2026-08-04, şema-dışı kanal ayarı): sitede sunulan yöntemler +
  // kapıda ödeme bedel/limit. Ayar yoksa üç yöntem de açık, 50 TL / 3000 TL varsayılır.
  const TUM_ODEME_YONTEMLERI = [
    { key: 'kart', label: 'Kart ile Öde (Online)' },
    { key: 'kapida-nakit', label: 'Kapıda Nakit Ödeme' },
    { key: 'kapida-kart', label: 'Kapıda Kart ile Ödeme' },
  ]
  const [paymentMethods, setPaymentMethods] = useState<string[]>(() => {
    const kayitli = target?.settings?.['paymentMethods']
    return Array.isArray(kayitli) && kayitli.length
      ? kayitli.filter((y): y is string => typeof y === 'string')
      : TUM_ODEME_YONTEMLERI.map(y => y.key)
  })
  const [codFee, setCodFee] = useState(
    target?.settings?.['codServiceFee'] != null ? String(target.settings['codServiceFee']) : '50')
  const [codMax, setCodMax] = useState(
    target?.settings?.['codMaxOrderTotal'] != null ? String(target.settings['codMaxOrderTotal']) : '3000')
  // Eski sistem (ECSGYE) platform eşlemesi (2026-08-04, GEÇİCİ — sipariş senkronu için):
  // tozlu=1, julude=2, olurbutik=12, mishar=41. Boş = bu kanal eskiye senkronlanmaz.
  const [legacyPlatformId, setLegacyPlatformId] = useState(
    target?.settings?.['legacyPlatformId'] != null ? String(target.settings['legacyPlatformId']) : '')
  // Kargo gönderimi (2026-08-05): kanalın paket bilgisi kargo şirketine gönderilsin mi?
  // Kendi satış kanallarımızda AÇIK; pazaryerlerinde KAPALI (kargoyu pazaryeri yönetir).
  // Ayar yoksa açık kabul edilir (eski sistemdeki kargoGonder varsayılanıyla aynı).
  const [cargoDispatch, setCargoDispatch] = useState(target?.settings?.['cargoDispatchEnabled'] !== false)
  // Müşteri kargo seçimi (2026-09-02, kullanıcı kararı): /teslimat'ta kargo şirketi seçimi.
  // Varsayılan KAPALI — sektör pratiği: taşıyıcı operasyonda otomatik atanır (öncelik + ödeme
  // uygunluğu); açık kanalda müşteri mahalle kurallı listeden seçer (tercih bağlayıcı değil).
  const [customerCargoSelection, setCustomerCargoSelection] = useState(
    target?.settings?.['customerCargoSelection'] === true)
  // F0 (K1): kanal bazlı yetenek ezmeleri — yalnız ezilebilir anahtarlar; diğerleri tip varsayılanı.
  const [capOverrides, setCapOverrides] = useState<CapabilityOverrides>(() => ({ ...(target?.capabilityOverrides ?? {}) }))
  const [fieldValues, setFieldValues] = useState<Record<string, string>>(() => {
    const merged: Record<string, string> = {}
    if (target) {
      for (const [k, v] of Object.entries(target.credentials ?? {})) if (v != null) merged[k] = String(v)
      for (const [k, v] of Object.entries(target.settings ?? {})) if (v != null) merged[k] = String(v)
    }
    return merged
  })

  const selectedPlatformType = platformTypes.find(pt => pt.id === platformTypeId)
  const baseCaps: ChannelCapabilities = selectedPlatformType?.capabilities
    ?? (selectedPlatformType?.isMarketplace ? MARKETPLACE_CAPABILITIES : DEFAULT_CAPABILITIES)
  const schema = selectedPlatformType?.settingsSchema ?? []
  const credFields = schema.filter(f => f.section === 'credentials')
  const settingsFields = schema.filter(f => f.section === 'settings')

  const autoCode = useMemo(
    () => nameTr.trim().toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_|_$/g, ''),
    [nameTr]
  )
  const effectiveCode = isEdit ? code : autoCode

  const mutation = useMutation({
    mutationFn: async () => {
      if (paymentMethods.length === 0) { throw new Error('En az bir ödeme yöntemi seçili olmalı.') }
      const credentials: Record<string, unknown> = {}
      const settings: Record<string, unknown> = {}
      // Şema dışı mevcut anahtarlar korunur (stockControlEnabled, tema/domain vb. —
      // backend Settings/Credentials'ı olduğu gibi değiştirir, merge etmez)
      // Stok görünürlüğü anahtarları burada özel ele alınıyor — genel korumadan hariç tut.
      const ozelSettings = new Set(['showOutOfStock', 'outOfStockVisibleSince', 'paymentMethods', 'codServiceFee', 'codMaxOrderTotal', 'legacyPlatformId', 'cargoDispatchEnabled', 'customerCargoSelection'])
      if (target) {
        const schemaKeys = new Set(schema.map(f => f.key))
        for (const [k, v] of Object.entries(target.credentials ?? {})) if (v != null && !schemaKeys.has(k)) credentials[k] = v
        for (const [k, v] of Object.entries(target.settings ?? {})) if (v != null && !schemaKeys.has(k) && !ozelSettings.has(k)) settings[k] = v
      }
      settings['showOutOfStock'] = showOutOfStock
      if (showOutOfStock && oosSince) settings['outOfStockVisibleSince'] = oosSince
      settings['paymentMethods'] = paymentMethods
      settings['codServiceFee'] = parseFloat(codFee) >= 0 ? parseFloat(codFee) : 50
      settings['codMaxOrderTotal'] = parseFloat(codMax) >= 0 ? parseFloat(codMax) : 3000
      if (legacyPlatformId && parseInt(legacyPlatformId) > 0) settings['legacyPlatformId'] = parseInt(legacyPlatformId)
      settings['cargoDispatchEnabled'] = cargoDispatch
      settings['customerCargoSelection'] = customerCargoSelection
      for (const f of schema) {
        const v = fieldValues[f.key] ?? ''
        if (v) {
          const parsed = f.type === 'number' ? parseFloat(v) : f.type === 'boolean' ? v === 'true' : v
          if (f.section === 'credentials') credentials[f.key] = parsed
          else settings[f.key] = parsed
        }
      }
      const body = {
        platformTypeId: isEdit ? undefined : platformTypeId,
        code: isEdit ? undefined : effectiveCode,
        nameI18n: { tr: nameTr },
        priceType: priceType || null,
        priceMultiplier: priceType === 'multiplier' && multiplier ? parseFloat(multiplier) : null,
        credentials,
        settings,
        isActive,
        capabilityOverrides: capOverrides,
        updateCapabilityOverrides: isEdit,
      }
      if (isEdit) {
        await api.put(`/core/firm-platforms/${target!.id}`, body)
      } else {
        await api.post(`/core/firms/${firmId}/platforms`, body)
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['firm-platforms'] })
      onSuccess()
    },
  })

  const ptOptions = platformTypes.filter(pt => pt.isActive).map(pt => ({
    value: pt.id,
    label: `${pt.nameI18n['tr'] ?? pt.code} ${pt.capabilities?.pushListing ?? pt.isMarketplace ? '(Pazaryeri)' : pt.capabilities?.pullsFromPartnerApi ? '(Dropship bayi)' : '(Kendi kanal)'}`,
  }))

  const firmOptions = firms.map(f => ({
    value: f.id,
    label: getFirmName(f) + (f.isMain ? ' (Ana Firma)' : ''),
  }))

  const needFirmSelect = !isEdit && !initialFirmId && firms.length > 1

  return (
    <div className="space-y-5">
      {/* Firma seçimi — sadece "Tümü" tabından açılınca göster */}
      {needFirmSelect && (
        <div>
          <label className="flbl">Firma <span style={{ color: '#ef4444' }}>*</span></label>
          <SearchableSelect
            value={firmId || null}
            onChange={v => setFirmId(v ?? '')}
            options={firmOptions}
            placeholder="— Firma seçin —"
            hasValue={!!firmId}
          />
        </div>
      )}

      {/* Platform Type */}
      {!isEdit && (
        <div>
          <label className="flbl">Platform Tipi <span style={{ color: '#ef4444' }}>*</span></label>
          <SearchableSelect
            value={platformTypeId || null}
            onChange={v => { setPlatformTypeId(v ?? ''); setFieldValues({}) }}
            options={ptOptions}
            placeholder="— Platform tipi seçin —"
            hasValue={!!platformTypeId}
          />
        </div>
      )}

      {/* Name */}
      <div>
        <label className="flbl">Kanal Adı <span style={{ color: '#ef4444' }}>*</span></label>
        <input className="inp" value={nameTr} onChange={e => setNameTr(e.target.value)}
          placeholder="örn: Trendyol Ana Mağaza" />
      </div>

      {/* Auto code (create only) */}
      {!isEdit && (
        <div>
          <label className="flbl">Kod</label>
          <div className="flex items-center gap-2 px-3 py-2 rounded-xl"
            style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            <code className="text-sm font-mono" style={{ color: effectiveCode ? 'var(--brand)' : 'var(--text-s)' }}>
              {effectiveCode || '—'}
            </code>
          </div>
        </div>
      )}

      {/* Price settings */}
      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="flbl">Fiyat Tipi</label>
          <select className="sel" value={priceType} onChange={e => setPriceType(e.target.value)}>
            {PRICE_TYPES.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
        </div>
        {priceType === 'multiplier' && (
          <div>
            <label className="flbl">Çarpan</label>
            <input type="number" className="inp" value={multiplier}
              onChange={e => setMultiplier(e.target.value)}
              placeholder="örn: 1.10" step="0.01" min="0" />
          </div>
        )}
      </div>

      {/* Credentials section */}
      {credFields.length > 0 && (
        <div className="space-y-4 p-4 rounded-xl" style={{ background: '#fffbeb', border: '1px solid #fde68a' }}>
          <p className="text-xs font-semibold" style={{ color: '#92400e' }}>Kimlik Bilgileri (API)</p>
          {credFields.map(f => (
            <DynamicField key={f.key} field={f}
              value={fieldValues[f.key] ?? ''}
              onChange={v => setFieldValues(prev => ({ ...prev, [f.key]: v }))} />
          ))}
        </div>
      )}

      {/* Settings section */}
      {settingsFields.length > 0 && (
        <div className="space-y-4 p-4 rounded-xl" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
          <p className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>Ayarlar</p>
          {settingsFields.map(f => (
            <DynamicField key={f.key} field={f}
              value={fieldValues[f.key] ?? ''}
              onChange={v => setFieldValues(prev => ({ ...prev, [f.key]: v }))} />
          ))}
        </div>
      )}

      {schema.length === 0 && platformTypeId && (
        <p className="text-xs py-2 text-center rounded-xl"
          style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}>
          Bu platform tipi için alan şeması tanımlı değil. Platform Tipleri sayfasından şema ekleyebilirsiniz.
        </p>
      )}

      {/* Stok görünürlüğü (kanal ayarı) */}
      <div className="space-y-3 p-4 rounded-xl" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
        <p className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>Stok Görünürlüğü</p>
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
            checked={showOutOfStock} onChange={e => setShowOutOfStock(e.target.checked)} />
          <span className="text-sm" style={{ color: 'var(--text)' }}>Stoğu biten ürünleri listede göster</span>
        </label>
        {showOutOfStock && (
          <div>
            <label className="flbl">Yalnız bu tarihten sonra açılanlar (boş = tümü)</label>
            <input type="date" className="inp" value={oosSince} onChange={e => setOosSince(e.target.value)} />
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Stoğu biten ürünlerden yalnız stok kartı bu tarihten sonra açılanlar listelenir.
            </p>
          </div>
        )}
      </div>

      {/* Yetenekler (F0, K1): tip varsayılanı + kanal ezmesi */}
      {platformTypeId && (
        <div className="space-y-3 p-4 rounded-xl" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
          <div className="flex items-center justify-between gap-2 flex-wrap">
            <p className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>Kanal Yetenekleri</p>
            <CapabilityBadges caps={mergeCapabilities(baseCaps, capOverrides)} />
          </div>
          {isEdit ? (
            <CapabilityOverridesEditor base={baseCaps} overrides={capOverrides} onChange={setCapOverrides} />
          ) : (
            <p className="text-xs" style={{ color: 'var(--text-s)' }}>
              Tip varsayılanları uygulanır; kanal bazlı ezmeler kayıt sonrası düzenlenebilir.
            </p>
          )}
        </div>
      )}

      {/* Ödeme yöntemleri (kanal ayarı, 2026-08-04) */}
      <div className="space-y-3 p-4 rounded-xl" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
        <p className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>Ödeme Yöntemleri</p>
        {TUM_ODEME_YONTEMLERI.map(y => (
          <label key={y.key} className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
              checked={paymentMethods.includes(y.key)}
              onChange={e => setPaymentMethods(m => e.target.checked ? [...m, y.key] : m.filter(x => x !== y.key))} />
            <span className="text-sm" style={{ color: 'var(--text)' }}>{y.label}</span>
          </label>
        ))}
        {paymentMethods.length === 0 && (
          <p className="text-xs" style={{ color: '#ef4444' }}>En az bir ödeme yöntemi seçili olmalı.</p>
        )}
        {(paymentMethods.includes('kapida-nakit') || paymentMethods.includes('kapida-kart')) && (
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="flbl">Kapıda ödeme hizmet bedeli (TL)</label>
              <input type="number" min="0" step="0.01" className="inp" value={codFee} onChange={e => setCodFee(e.target.value)} />
            </div>
            <div>
              <label className="flbl">Kapıda ödeme üst sınırı (TL, 0 = sınırsız)</label>
              <input type="number" min="0" step="1" className="inp" value={codMax} onChange={e => setCodMax(e.target.value)} />
            </div>
          </div>
        )}
        <p className="text-xs" style={{ color: 'var(--text-s)' }}>
          Kapalı yöntem sitede hiç gösterilmez; sunucu da bu yöntemle siparişi reddeder. Değişiklik ~1 dk içinde siteye yansır.
        </p>
      </div>

      {/* Kargo gönderimi (kanal ayarı, 2026-08-05) */}
      <div className="space-y-2 p-4 rounded-xl" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
        <p className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>Kargo Gönderimi</p>
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
            checked={cargoDispatch} onChange={e => setCargoDispatch(e.target.checked)} />
          <span className="text-sm" style={{ color: 'var(--text)' }}>Paket bilgileri kargo şirketine gönderilsin</span>
        </label>
        <p className="text-xs" style={{ color: 'var(--text-s)' }}>
          Kendi satış kanallarımızda AÇIK olmalı — paket hazırlandığında kargo şirketine bildirilir.
          Pazaryerlerinde KAPALI: kargo bilgisini pazaryeri kendisi iletir. Kapalıyken bu kanalın
          siparişlerinde kargo gönderisi oluşturulmaz.
        </p>
        <label className="flex items-center gap-2 cursor-pointer pt-1" style={{ borderTop: '1px solid var(--border)' }}>
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
            checked={customerCargoSelection} onChange={e => setCustomerCargoSelection(e.target.checked)} />
          <span className="text-sm" style={{ color: 'var(--text)' }}>Müşteri teslimat adımında kargo şirketi seçebilsin</span>
        </label>
        <p className="text-xs" style={{ color: 'var(--text-s)' }}>
          Varsayılan KAPALI: taşıyıcı "Kargoya Ver" adımında öncelik + ödeme uygunluğuna göre önerilir,
          müşteri seçim görmez. Açılırsa /teslimat'ta mahalle kurallı liste gösterilir (tercih bağlayıcı
          değildir). Değişiklik ~1 dk içinde siteye yansır.
        </p>
      </div>

      {/* Eski sistem eşlemesi (geçici, 2026-08-04) */}
      <div className="space-y-2 p-4 rounded-xl" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
        <p className="text-xs font-semibold" style={{ color: 'var(--text-s)' }}>Eski Sistem (ECSGYE) Eşlemesi</p>
        <div>
          <label className="flbl">Eski platform Id (boş = sipariş senkronu kapalı)</label>
          <input type="number" min="0" className="inp" value={legacyPlatformId}
            onChange={e => setLegacyPlatformId(e.target.value)} placeholder="ör. mishar=41" />
          <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
            tozlu=1 · julude=2 · olurbutik=12 · mishar=41. Bu kanalın siparişleri eski sisteme bu platform Id ile yazılır (geçici köprü).
          </p>
        </div>
      </div>

      {/* Active toggle (edit only) */}
      {isEdit && (
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
            checked={isActive} onChange={e => setIsActive(e.target.checked)} />
          <span className="text-sm" style={{ color: 'var(--text)' }}>Aktif</span>
        </label>
      )}

      {mutation.isError && (
        <p className="text-sm" style={{ color: '#ef4444' }}>
          {(mutation.error as any)?.response?.data?.error ?? 'Hata oluştu. Lütfen tekrar deneyin.'}
        </p>
      )}

      <div className="flex justify-end gap-2 pt-1">
        <Button variant="secondary" onClick={onClose} disabled={mutation.isPending}>İptal</Button>
        <Button
          onClick={() => mutation.mutate()}
          loading={mutation.isPending}
          disabled={
            (!isEdit && (!firmId || !platformTypeId || !nameTr.trim() || !effectiveCode)) ||
            (isEdit && !nameTr.trim())
          }
        >
          {isEdit ? 'Kaydet' : 'Oluştur'}
        </Button>
      </div>
    </div>
  )
}

// ── Channel Card ──────────────────────────────────────────────────────────────

function ChannelCard({
  ch,
  showFirm,
  onClick,
}: {
  ch: FirmPlatformWithFirm
  showFirm: boolean
  onClick: () => void
}) {
  const schema = ch.settingsSchema ?? []
  const credFields = schema.filter(f => f.section === 'credentials')
  const settingsFields = schema.filter(f => f.section === 'settings')
  const creds = ch.credentials ?? {}
  const sets = ch.settings ?? {}
  const filledCreds = credFields.filter(f => creds[f.key])
  const filledSettings = settingsFields.filter(f => sets[f.key])

  return (
    <div
      onClick={onClick}
      className="card cursor-pointer transition-all hover:shadow-md p-0 overflow-hidden"
    >
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3"
        style={{ borderBottom: '1px solid var(--border)' }}>
        <div className="flex items-center gap-2.5 min-w-0">
          <div className="w-8 h-8 shrink-0 rounded-lg flex items-center justify-center"
            style={{
              background: ch.platformTypeIsMarketplace ? '#fef3c7' : 'var(--surface2)',
              border: '1px solid var(--border)',
            }}>
            {ch.platformTypeIsMarketplace
              ? <ShoppingBag size={15} style={{ color: '#d97706' }} />
              : <Globe size={15} style={{ color: 'var(--brand)' }} />}
          </div>
          <div className="min-w-0">
            <p className="text-sm font-semibold truncate" style={{ color: 'var(--text)' }}>
              {getChannelName(ch)}
            </p>
            <p className="text-xs truncate" style={{ color: 'var(--text-s)' }}>
              {getPlatformTypeName(ch)}
            </p>
            {ch.capabilities && <div className="mt-1"><CapabilityBadges caps={ch.capabilities} /></div>}
          </div>
        </div>
        <div className="flex items-center gap-1.5 shrink-0 ml-2">
          <Badge variant={ch.isActive ? 'success' : 'neutral'}>
            {ch.isActive ? 'Aktif' : 'Pasif'}
          </Badge>
        </div>
      </div>

      {/* Body */}
      <div className="px-4 py-3 space-y-2">
        <div className="flex items-center justify-between text-xs">
          <span style={{ color: 'var(--text-s)' }}>Kod</span>
          <code className="font-mono" style={{ color: 'var(--text-m)' }}>{ch.code}</code>
        </div>
        {ch.priceType && (
          <div className="flex items-center justify-between text-xs">
            <span style={{ color: 'var(--text-s)' }}>Fiyat</span>
            <span style={{ color: 'var(--text-m)' }}>
              {ch.priceType === 'multiplier' ? `×${ch.priceMultiplier}` : 'Manuel'}
            </span>
          </div>
        )}

        {/* Fill status */}
        {schema.length > 0 && (
          <div className="pt-1 flex gap-2 flex-wrap text-xs">
            {credFields.length > 0 && (
              <span
                className={cn('flex items-center gap-1 px-2 py-0.5 rounded-full',
                  filledCreds.length === credFields.length ? 'text-green-700' : 'text-amber-700')}
                style={{
                  background: filledCreds.length === credFields.length ? '#f0fdf4' : '#fffbeb',
                  border: '1px solid',
                  borderColor: filledCreds.length === credFields.length ? '#bbf7d0' : '#fde68a',
                }}>
                API {filledCreds.length}/{credFields.length}
              </span>
            )}
            {settingsFields.length > 0 && (
              <span className="flex items-center gap-1 px-2 py-0.5 rounded-full"
                style={{ background: 'var(--surface2)', color: 'var(--text-s)', border: '1px solid var(--border)' }}>
                Ayar {filledSettings.length}/{settingsFields.length}
              </span>
            )}
          </div>
        )}
      </div>

      {/* Firm footer — sadece "Tümü" görünümünde */}
      {showFirm && (
        <div className="px-4 py-2 flex items-center gap-1.5"
          style={{ borderTop: '1px solid var(--border)', background: 'var(--surface2)' }}>
          <Building2 size={11} style={{ color: 'var(--text-s)' }} />
          <span className="text-xs" style={{ color: 'var(--text-s)' }}>{ch.firmName}</span>
        </div>
      )}
    </div>
  )
}

// ── Main Page ─────────────────────────────────────────────────────────────────

export function ChannelsPage() {
  const [selectedFirmId, setSelectedFirmId] = useState<string | 'all'>('all')
  const [modalOpen, setModalOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<FirmPlatformWithFirm | null>(null)

  // Firms
  const { data: firms = [], isLoading: firmsLoading } = useQuery<Firm[]>({
    queryKey: ['firms'],
    queryFn: async () => {
      const { data } = await api.get('/core/firms')
      return data.data ?? []
    },
    staleTime: 10 * 60 * 1000,
  })

  // Channels for each firm in parallel
  const channelQueries = useQueries({
    queries: firms.map(firm => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<FirmPlatformWithFirm[]> => {
        const { data } = await api.get(`/core/firms/${firm.id}/platforms`)
        return (data.data ?? []).map((ch: FirmPlatform) => ({
          ...ch,
          firmId: firm.id,
          firmName: getFirmName(firm),
        }))
      },
      enabled: firms.length > 0,
      staleTime: 2 * 60 * 1000,
    })),
  })

  const { data: platformTypes = [], isLoading: ptLoading } = useQuery<PlatformType[]>({
    queryKey: ['platform-types', false],
    queryFn: async () => {
      const { data } = await api.get('/core/platform-types?activeOnly=false')
      return data.data ?? []
    },
    staleTime: 5 * 60 * 1000,
  })

  const channelsLoading = channelQueries.some(q => q.isLoading)

  // Merge all channels
  const allChannels = useMemo<FirmPlatformWithFirm[]>(() => {
    return channelQueries.flatMap(q => q.data ?? [])
  }, [channelQueries])

  // Filtered channels
  const visibleChannels = useMemo(() => {
    if (selectedFirmId === 'all') return allChannels
    return allChannels.filter(ch => ch.firmId === selectedFirmId)
  }, [allChannels, selectedFirmId])

  // The firm pre-selected when opening create modal
  const createFirmId = selectedFirmId === 'all' ? undefined : selectedFirmId

  function openCreate() { setEditTarget(null); setModalOpen(true) }
  function openEdit(ch: FirmPlatformWithFirm) { setEditTarget(ch); setModalOpen(true) }
  function closeModal() { setModalOpen(false); setEditTarget(null) }

  if (firmsLoading || ptLoading) return <PageSpinner />

  if (firms.length === 0) {
    return (
      <div className="p-6">
        <div className="card py-16 text-center">
          <p className="text-sm" style={{ color: 'var(--text-s)' }}>
            Firma kaydı bulunamadı. Önce Firmalar sayfasından bir firma oluşturun.
          </p>
        </div>
      </div>
    )
  }

  const totalCount = allChannels.length
  const visibleCount = visibleChannels.length

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-start justify-between mb-5">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Satış Kanalları</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Mağaza, pazaryeri ve özel kanal tanımları — API kimlik bilgileri ve sözleşme ayarları
          </p>
        </div>
        <Button size="sm" onClick={openCreate}><Plus size={14} /> Yeni Kanal</Button>
      </div>

      {/* Firma Tabs */}
      <div className="flex items-center gap-1 flex-wrap mb-5">
        {/* "Tümü" pill */}
        <button
          onClick={() => setSelectedFirmId('all')}
          className={cn(
            'flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-sm font-medium transition-all',
            selectedFirmId === 'all'
              ? 'shadow-sm'
              : 'hover:opacity-80',
          )}
          style={
            selectedFirmId === 'all'
              ? { background: 'var(--brand)', color: '#fff' }
              : { background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }
          }
        >
          Tümü
          <span className="text-xs px-1.5 py-0.5 rounded-md font-semibold"
            style={
              selectedFirmId === 'all'
                ? { background: 'rgba(255,255,255,0.25)', color: '#fff' }
                : { background: 'var(--surface)', color: 'var(--text-s)', border: '1px solid var(--border)' }
            }>
            {totalCount}
          </span>
        </button>

        {/* Separator */}
        {firms.length > 0 && (
          <span className="mx-1 h-4" style={{ borderLeft: '1px solid var(--border)' }} />
        )}

        {/* Per-firm pills */}
        {firms.map(firm => {
          const firmChannelCount = allChannels.filter(ch => ch.firmId === firm.id).length
          const isActive = selectedFirmId === firm.id
          return (
            <button
              key={firm.id}
              onClick={() => setSelectedFirmId(firm.id)}
              className={cn(
                'flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-sm font-medium transition-all',
                isActive ? 'shadow-sm' : 'hover:opacity-80',
              )}
              style={
                isActive
                  ? { background: 'var(--surface)', color: 'var(--text)', border: '1px solid var(--brand)' }
                  : { background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }
              }
            >
              {firm.isMain && (
                <span className="w-1.5 h-1.5 rounded-full shrink-0" style={{ background: 'var(--brand)' }} />
              )}
              {getFirmName(firm)}
              <span className="text-xs px-1.5 py-0.5 rounded-md font-semibold"
                style={{ background: 'var(--surface)', color: 'var(--text-s)', border: '1px solid var(--border)' }}>
                {firmChannelCount}
              </span>
            </button>
          )
        })}
      </div>

      {/* Content */}
      {channelsLoading ? (
        <PageSpinner />
      ) : visibleCount === 0 ? (
        <div className="card py-16 text-center">
          <p className="text-sm mb-3" style={{ color: 'var(--text-s)' }}>
            {selectedFirmId === 'all'
              ? 'Henüz satış kanalı tanımlanmamış.'
              : 'Bu firmaya henüz satış kanalı eklenmemiş.'}
          </p>
          <Button size="sm" onClick={openCreate}><Plus size={13} /> Kanal Ekle</Button>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {visibleChannels.map(ch => (
            <ChannelCard
              key={ch.id}
              ch={ch}
              showFirm={selectedFirmId === 'all' && firms.length > 1}
              onClick={() => openEdit(ch)}
            />
          ))}
        </div>
      )}

      {/* Modal */}
      <Modal
        open={modalOpen}
        onClose={closeModal}
        title={editTarget ? `Kanal Düzenle — ${getChannelName(editTarget)}` : 'Yeni Satış Kanalı'}
        size="md"
        footer={null}
      >
        <ChannelForm
          platformTypes={platformTypes}
          firms={firms}
          initialFirmId={createFirmId}
          target={editTarget}
          onClose={closeModal}
          onSuccess={closeModal}
        />
      </Modal>
    </div>
  )
}
