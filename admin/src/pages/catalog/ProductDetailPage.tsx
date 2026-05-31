import { useState, useMemo, useEffect, useCallback } from 'react'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { ChevronRight, Save, EyeOff, Trash2, CheckCircle, AlertCircle, Info, Settings2 } from 'lucide-react'
import api from '@/api/client'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'
import { useLanguages } from '@/hooks/useLanguages'
import { ProductImagesTab } from './ProductImagesTab'

// ── Types ─────────────────────────────────────────────────────────────────────

interface VariantImage {
  id: string
  imageUrl: string
  sortOrder: number
  isMain: boolean
}

interface VariantAttribute {
  attributeTypeId: string
  attributeTypeCode: string
  attributeTypeNameI18n: Record<string, string>
  attributeValueId: string
  attributeValueNameI18n: Record<string, string>
}

interface Variant {
  id: string
  sku: string
  barcode: string | null
  basePrice: number
  baseCost: number | null
  isActive: boolean
  variantAttributes: VariantAttribute[]
  images: VariantImage[]
}

interface ProductAttribute {
  id: string
  attributeTypeId: string
  attributeTypeCode: string
  attributeTypeNameI18n: Record<string, string>
  attributeValueId: string | null
  attributeValueNameI18n: Record<string, string> | null
  customValue: string | null
}

interface SubAttributeSchemaItem {
  subAttributeTypeId: string
  subAttributeTypeCode: string
  subAttributeTypeNameI18n: Record<string, string>
  isRequired: boolean
}

interface AxisSubAttributeSchema {
  axisAttributeTypeId: string
  axisAttributeTypeCode: string
  axisAttributeTypeNameI18n: Record<string, string>
  subAttributes: SubAttributeSchemaItem[]
}

interface ProductAxisSubAttributeValue {
  attributeValueId: string
  subAttributeTypeId: string
  value: string
}

interface ProductDetail {
  id: string
  code: string
  nameI18n: Record<string, string>
  shortDescriptionI18n: Record<string, string> | null
  descriptionI18n: Record<string, string> | null
  productGroupId: string
  isActive: boolean
  basePrice: number
  baseCost: number | null
  taxRate: number
  supplierId: string | null
  supplierProductCode: string | null
  createdAt: string
  updatedAt: string | null
  attributes: ProductAttribute[]
  variants: Variant[]
  tags: string[]
  slug: string | null
  metaTitleI18n: Record<string, string> | null
  metaDescriptionI18n: Record<string, string> | null
  metaKeywordsI18n: Record<string, string> | null
  axisSubAttributeSchema: AxisSubAttributeSchema[]
  axisSubAttributeValues: ProductAxisSubAttributeValue[]
}

interface ProductGroupAttribute {
  id: string
  attributeTypeId: string
  attributeTypeCode: string
  attributeTypeNameI18n: Record<string, string>
  isVariant: boolean
  isRequired: boolean
  isPrimaryAxis: boolean
  sortOrder: number
}

interface ProductGroup {
  id: string
  code: string
  nameI18n: Record<string, string>
  attributes: ProductGroupAttribute[]
}

interface StockDto {
  id: string
  variantId: string
  warehouseId: string
  stockType: string
  quantity: number
  reservedQuantity: number
  availableQuantity: number
}

interface Warehouse {
  id: string
  code: string
  nameI18n: Record<string, string>
}

// ── Helpers ───────────────────────────────────────────────────────────────────

// client-side EAN-13 (fallback — used only if API unavailable)
function toEan13(n: number): string {
  const digits = String(n).padStart(12, '0').slice(-12)
  let sum = 0
  for (let i = 0; i < 12; i++) sum += parseInt(digits[i]) * (i % 2 === 0 ? 1 : 3)
  return digits + String((10 - (sum % 10)) % 10)
}

type Tab = 'genel' | 'ozellikler' | 'varyantlar' | 'altOzellikler' | 'stok' | 'kanallar' | 'gorseller' | 'etiketler' | 'seo'

const TABS: { key: Tab; label: string }[] = [
  { key: 'genel',         label: 'Genel' },
  { key: 'ozellikler',    label: 'Özellikler' },
  { key: 'varyantlar',    label: 'Varyantlar' },
  { key: 'altOzellikler', label: 'Alt Özellikler' },
  { key: 'stok',          label: 'Stok' },
  { key: 'kanallar',      label: 'Satış Kanalları' },
  { key: 'gorseller',     label: 'Görseller' },
  { key: 'etiketler',     label: 'Etiketler' },
  { key: 'seo',           label: 'SEO' },
]

const TAX_RATES = [0, 1, 8, 10, 18, 20]

function getName(o: { nameI18n: Record<string, string>; code: string }): string {
  return o.nameI18n['tr'] ?? o.nameI18n[Object.keys(o.nameI18n)[0]] ?? o.code
}

function fmtDate(iso: string) {
  return new Date(iso).toLocaleDateString('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' })
}

// ── Lang Panel (Genel tab multi-lang content) ──────────────────────────────────

interface LangPanelProps {
  lang: string
  sourceLang: string
  isSource: boolean
  nameVal: string
  shortDescVal: string
  descVal: string
  sourceName: string
  sourceShortDesc: string
  sourceDesc: string
  onNameChange: (v: string) => void
  onShortDescChange: (v: string) => void
  onDescChange: (v: string) => void
}

function LangPanel({
  lang, isSource, nameVal, shortDescVal, descVal,
  sourceName, sourceShortDesc, sourceDesc,
  onNameChange, onShortDescChange, onDescChange,
}: LangPanelProps) {
  if (isSource) {
    return (
      <div className="p-4 space-y-4">
        <div>
          <label className="flbl">Ürün Adı</label>
          <input className={cn('inp', nameVal && 'ok')} value={nameVal} onChange={(e) => onNameChange(e.target.value)} />
        </div>
        <div>
          <label className="flbl">Kısa Açıklama</label>
          <textarea className={cn('ta', shortDescVal && 'ok')} value={shortDescVal} onChange={(e) => onShortDescChange(e.target.value)} />
        </div>
        <div>
          <label className="flbl">Açıklama</label>
          <textarea className={cn('ta', descVal && 'ok')} style={{ minHeight: 100 }} value={descVal} onChange={(e) => onDescChange(e.target.value)} />
        </div>
      </div>
    )
  }

  return (
    <div className="p-4 space-y-4">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        <div>
          <label className="flbl" style={{ color: 'var(--text-s)' }}>Ürün Adı</label>
          <div className="src-block">{sourceName || '—'}</div>
        </div>
        <div>
          <label className="flbl">Ürün Adı ({lang.toUpperCase()})</label>
          <input className={cn('inp', nameVal && 'ok')} value={nameVal} onChange={(e) => onNameChange(e.target.value)} placeholder="Çeviri girin…" />
        </div>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        <div>
          <label className="flbl" style={{ color: 'var(--text-s)' }}>Kısa Açıklama</label>
          <div className="src-block">{sourceShortDesc || '—'}</div>
        </div>
        <div>
          <label className="flbl">Kısa Açıklama ({lang.toUpperCase()})</label>
          <textarea className={cn('ta', shortDescVal && 'ok')} value={shortDescVal} onChange={(e) => onShortDescChange(e.target.value)} placeholder="Çeviri girin…" />
        </div>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        <div>
          <label className="flbl" style={{ color: 'var(--text-s)' }}>Açıklama</label>
          <div className="src-block" style={{ minHeight: 70 }}>{sourceDesc || '—'}</div>
        </div>
        <div>
          <label className="flbl">Açıklama ({lang.toUpperCase()})</label>
          <textarea className={cn('ta', descVal && 'ok')} style={{ minHeight: 70 }} value={descVal} onChange={(e) => onDescChange(e.target.value)} placeholder="Çeviri girin…" />
        </div>
      </div>
    </div>
  )
}

// ── Tags Tab ──────────────────────────────────────────────────────────────────

function TagsTab({ product, onSaved }: { product: ProductDetail; onSaved: () => void }) {
  const [tags, setTags] = useState<string[]>(product.tags ?? [])
  const [input, setInput] = useState('')
  const [saveStatus, setSaveStatus] = useState<'idle' | 'ok' | 'err'>('idle')
  const [saving, setSaving] = useState(false)

  const dirty = JSON.stringify(tags.slice().sort()) !== JSON.stringify((product.tags ?? []).slice().sort())

  function addTag(raw: string) {
    const cleaned = raw.trim().toLowerCase().replace(/\s+/g, '-')
    if (!cleaned || tags.includes(cleaned)) { setInput(''); return }
    setTags(prev => [...prev, cleaned])
    setInput('')
  }

  function removeTag(t: string) { setTags(prev => prev.filter(x => x !== t)) }

  function onKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' || e.key === ',') { e.preventDefault(); addTag(input) }
    if (e.key === 'Backspace' && !input && tags.length > 0) removeTag(tags[tags.length - 1])
  }

  async function save() {
    setSaving(true)
    try {
      await api.put(`/catalog/products/${product.id}/tags`, { tags })
      onSaved()
      setSaveStatus('ok')
      setTimeout(() => setSaveStatus('idle'), 2500)
    } catch { setSaveStatus('err') }
    finally { setSaving(false) }
  }

  return (
    <div className="vc max-w-2xl flex flex-col gap-4">
      <div className="card space-y-4">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Ürün Etiketleri</h2>
            <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
              Etiketler arama, filtreleme ve segmentasyonda kullanılır. Enter veya virgül ile ekleyin.
            </p>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            {saveStatus === 'ok' && (
              <span className="flex items-center gap-1 text-xs" style={{ color: '#16a34a' }}>
                <CheckCircle size={12} /> Kaydedildi
              </span>
            )}
            {saveStatus === 'err' && (
              <span className="text-xs" style={{ color: '#dc2626' }}>Hata oluştu</span>
            )}
            <Button size="sm" onClick={save} loading={saving} disabled={!dirty}>
              <Save size={13} /> Kaydet
            </Button>
          </div>
        </div>

        {/* Tag input area */}
        <div
          className="flex flex-wrap gap-1.5 p-2.5 rounded-xl cursor-text min-h-[48px]"
          style={{ border: '1px solid var(--border)', background: 'var(--surface)' }}
          onClick={() => (document.getElementById('tag-input') as HTMLInputElement)?.focus()}
        >
          {tags.map(t => (
            <span key={t}
              className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium"
              style={{ background: 'var(--brand-bg)', color: 'var(--brand)', border: '1px solid var(--brand-b)' }}>
              {t}
              <button onClick={e => { e.stopPropagation(); removeTag(t) }}
                className="hover:opacity-60 transition-opacity ml-0.5">&times;</button>
            </span>
          ))}
          <input
            id="tag-input"
            className="flex-1 bg-transparent outline-none text-sm min-w-[120px]"
            style={{ color: 'var(--text)' }}
            placeholder={tags.length === 0 ? 'Etiket girin, Enter ile ekleyin…' : ''}
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={onKeyDown}
            onBlur={() => { if (input.trim()) addTag(input) }}
          />
        </div>

        <p className="text-xs" style={{ color: 'var(--text-s)' }}>
          {tags.length} etiket &bull; Boşluklar otomatik tire (-) ile değiştirilir
        </p>
      </div>
    </div>
  )
}

// ── SEO Tab ───────────────────────────────────────────────────────────────────

function toSlug(text: string): string {
  const map: Record<string, string> = { ç:'c',Ç:'c',ğ:'g',Ğ:'g',ı:'i',İ:'i',ö:'o',Ö:'o',ş:'s',Ş:'s',ü:'u',Ü:'u' }
  return text.split('').map(c => map[c] ?? c).join('')
    .toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')
}

function SeoTab({ product, languages, onSaved }: {
  product: ProductDetail
  languages: { code: string; name: string; isDefault?: boolean }[]
  onSaved: () => void
}) {
  const defaultLang = languages.find(l => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'
  const [activeLang, setActiveLang] = useState(defaultLang)
  const [slug, setSlug] = useState(product.slug ?? '')
  const [slugManual, setSlugManual] = useState(!!product.slug)
  const [metaTitle, setMetaTitle] = useState<Record<string, string>>(product.metaTitleI18n ?? {})
  const [metaDesc, setMetaDesc] = useState<Record<string, string>>(product.metaDescriptionI18n ?? {})
  const [metaKeywords, setMetaKeywords] = useState<Record<string, string>>(product.metaKeywordsI18n ?? {})
  const [saving, setSaving] = useState(false)
  const [saveStatus, setSaveStatus] = useState<'idle' | 'ok' | 'err'>('idle')

  const autoSlug = toSlug(product.nameI18n?.['tr'] ?? product.nameI18n?.[defaultLang] ?? product.code)
  const effectiveSlug = slugManual ? slug : autoSlug

  const previewTitle = metaTitle[activeLang] || product.nameI18n?.[activeLang] || product.nameI18n?.['tr'] || product.code
  const previewDesc  = metaDesc[activeLang] || product.shortDescriptionI18n?.[activeLang] || product.shortDescriptionI18n?.['tr'] || ''
  const previewUrl   = `siteniz.com/urun/${effectiveSlug}`

  async function save() {
    setSaving(true)
    try {
      await api.put(`/catalog/products/${product.id}/seo`, {
        slug: effectiveSlug || null,
        metaTitleI18n: Object.keys(metaTitle).length ? metaTitle : null,
        metaDescriptionI18n: Object.keys(metaDesc).length ? metaDesc : null,
        metaKeywordsI18n: Object.keys(metaKeywords).length ? metaKeywords : null,
      })
      onSaved()
      setSaveStatus('ok')
      setTimeout(() => setSaveStatus('idle'), 2500)
    } catch { setSaveStatus('err') }
    finally { setSaving(false) }
  }

  return (
    <div className="vc flex detail-cols gap-4">
      {/* LEFT — form */}
      <div className="flex-1 space-y-4 min-w-0">
        {/* URL Slug */}
        <div className="card space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>URL Slug</h2>
            {slugManual && (
              <button onClick={() => { setSlugManual(false); setSlug('') }}
                className="text-xs" style={{ color: 'var(--brand)' }}>
                Otomatiğe döndür
              </button>
            )}
          </div>
          <div className="flex items-center gap-2 rounded-xl overflow-hidden"
            style={{ border: '1px solid var(--border)' }}>
            <span className="px-3 py-2 text-xs shrink-0"
              style={{ background: 'var(--surface2)', color: 'var(--text-s)', borderRight: '1px solid var(--border)' }}>
              /urun/
            </span>
            <input
              className="flex-1 bg-transparent outline-none text-sm px-2 py-2"
              style={{ color: 'var(--text)' }}
              value={slugManual ? slug : autoSlug}
              placeholder={autoSlug}
              onChange={e => { setSlugManual(true); setSlug(e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '')) }}
            />
            {!slugManual && (
              <span className="px-2.5 text-xs shrink-0" style={{ color: 'var(--brand)' }}>Otomatik</span>
            )}
          </div>
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>
            Sadece küçük harf, rakam ve tire. Benzersiz olmalıdır.
          </p>
        </div>

        {/* Meta alanları — dil sekmeli */}
        <div className="card overflow-hidden p-0">
          <div className="flex items-center justify-between px-4 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
            <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Meta Alanları</h2>
            <div className="flex gap-0.5">
              {languages.map(l => (
                <button key={l.code} onClick={() => setActiveLang(l.code)}
                  className={cn('px-2.5 py-0.5 rounded-lg text-xs font-medium transition-all',
                    activeLang === l.code ? 'text-[var(--brand)]' : 'text-[var(--text-s)]')}
                  style={activeLang === l.code
                    ? { background: 'var(--brand-bg)', border: '1px solid var(--brand-b)' }
                    : { border: '1px solid transparent' }}>
                  {l.code.toUpperCase()}
                  {(metaTitle[l.code] || metaDesc[l.code]) && (
                    <span className="ml-1 inline-block w-1.5 h-1.5 rounded-full bg-current opacity-60" />
                  )}
                </button>
              ))}
            </div>
          </div>
          <div className="p-4 space-y-4">
            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="flbl">Meta Başlık</label>
                <span className="text-xs" style={{ color: (metaTitle[activeLang] ?? '').length > 60 ? '#dc2626' : 'var(--text-s)' }}>
                  {(metaTitle[activeLang] ?? '').length}/60
                </span>
              </div>
              <input className="inp" value={metaTitle[activeLang] ?? ''}
                placeholder={product.nameI18n?.[activeLang] ?? product.nameI18n?.['tr'] ?? ''}
                onChange={e => setMetaTitle(p => ({ ...p, [activeLang]: e.target.value }))} />
              <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Boş bırakılırsa ürün adı kullanılır.</p>
            </div>
            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="flbl">Meta Açıklama</label>
                <span className="text-xs" style={{ color: (metaDesc[activeLang] ?? '').length > 160 ? '#dc2626' : 'var(--text-s)' }}>
                  {(metaDesc[activeLang] ?? '').length}/160
                </span>
              </div>
              <textarea className="ta" rows={3} value={metaDesc[activeLang] ?? ''}
                placeholder={product.shortDescriptionI18n?.[activeLang] ?? ''}
                onChange={e => setMetaDesc(p => ({ ...p, [activeLang]: e.target.value }))} />
              <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Boş bırakılırsa kısa açıklama kullanılır.</p>
            </div>
            <div>
              <label className="flbl">Anahtar Kelimeler</label>
              <input className="inp" value={metaKeywords[activeLang] ?? ''}
                placeholder="kelime1, kelime2, kelime3"
                onChange={e => setMetaKeywords(p => ({ ...p, [activeLang]: e.target.value }))} />
              <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Virgülle ayırın.</p>
            </div>
          </div>
        </div>

        {/* Save */}
        <div className="flex items-center justify-end gap-3">
          {saveStatus === 'ok' && (
            <span className="flex items-center gap-1 text-xs" style={{ color: '#16a34a' }}>
              <CheckCircle size={12} /> Kaydedildi
            </span>
          )}
          {saveStatus === 'err' && (
            <span className="text-xs" style={{ color: '#dc2626' }}>
              Hata — slug çakışıyor olabilir
            </span>
          )}
          <Button onClick={save} loading={saving}><Save size={13} /> Kaydet</Button>
        </div>
      </div>

      {/* RIGHT — Google önizleme */}
      <div className="detail-right w-full md:w-80 flex-shrink-0 space-y-4">
        <div className="card overflow-hidden p-0">
          <div className="px-4 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
            <h3 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Google Önizleme</h3>
            <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{activeLang.toUpperCase()} dili</p>
          </div>
          <div className="p-4">
            <div className="rounded-xl p-3 space-y-1" style={{ background: '#fff', border: '1px solid #dadce0' }}>
              <p className="text-xs truncate" style={{ color: '#006621', fontFamily: 'sans-serif' }}>
                {previewUrl}
              </p>
              <p className="text-base leading-snug font-normal"
                style={{ color: '#1a0dab', fontFamily: 'sans-serif', fontSize: 18 }}>
                {previewTitle.slice(0, 60) || 'Ürün Adı'}
              </p>
              <p className="text-xs leading-relaxed line-clamp-2"
                style={{ color: '#545454', fontFamily: 'sans-serif', fontSize: 13 }}>
                {previewDesc.slice(0, 160) || 'Ürün açıklaması burada görünür. Meta açıklama girilmemişse kısa açıklama kullanılır.'}
              </p>
            </div>
            <div className="mt-3 space-y-1">
              <div className="flex justify-between text-xs">
                <span style={{ color: 'var(--text-s)' }}>Başlık uzunluğu</span>
                <span style={{ color: previewTitle.length > 60 ? '#dc2626' : '#16a34a' }}>
                  {previewTitle.length}/60
                </span>
              </div>
              <div className="flex justify-between text-xs">
                <span style={{ color: 'var(--text-s)' }}>Açıklama uzunluğu</span>
                <span style={{ color: previewDesc.length > 160 ? '#dc2626' : '#16a34a' }}>
                  {previewDesc.length}/160
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

// ── Channels Pricing Tab ──────────────────────────────────────────────────────

interface ChannelFirm {
  id: string
  code: string
  nameI18n: Record<string, string>
  isMain: boolean
  isActive: boolean
}

interface ChannelItem {
  id: string
  firmId: string
  firmName: string
  platformTypeCode: string
  platformTypeNameI18n: Record<string, string>
  platformTypeIsMarketplace: boolean
  code: string
  nameI18n: Record<string, string>
  priceType: string | null
  priceMultiplier: number | null
  isActive: boolean
}

interface PricingRecord {
  id: string
  firmPlatformId: string
  variantId: string
  variantSku: string
  priceType: string | null
  priceMultiplier: number | null
  price: number | null
  compareAtPrice: number | null
  isActive: boolean
}

interface PricingRow {
  variantId: string
  sku: string
  attrLabel: string
  basePrice: number
  priceType: string
  priceMultiplier: string
  price: string
  compareAtPrice: string
  isActive: boolean
}

const CH_PRICE_TYPES = [
  { value: '',           label: 'Üründen al' },
  { value: 'manual',     label: 'Manuel' },
  { value: 'multiplier', label: 'Çarpan' },
]

function getChName(o: { nameI18n: Record<string, string>; code: string }) {
  const n = o.nameI18n
  return n['tr'] ?? n[Object.keys(n)[0]] ?? o.code
}

function variantAttrLabel(v: Variant): string {
  if (!v.variantAttributes?.length) return ''
  return v.variantAttributes
    .map(va => va.attributeValueNameI18n?.['tr'] ?? va.attributeValueNameI18n?.[Object.keys(va.attributeValueNameI18n ?? {})[0]] ?? '')
    .filter(Boolean)
    .join(' / ')
}

function ChannelsPricingTab({ product }: { product: ProductDetail }) {
  const queryClient = useQueryClient()
  const [selectedChannelId, setSelectedChannelId] = useState<string | null>(null)
  const [rows, setRows] = useState<Record<string, PricingRow>>({})
  const [savingRows, setSavingRows] = useState<Set<string>>(new Set())
  const [savedRows, setSavedRows] = useState<Set<string>>(new Set())
  const [bulkPrice, setBulkPrice] = useState('')
  const [bulkPriceType, setBulkPriceType] = useState<'manual' | 'multiplier'>('manual')

  // Fetch all firms
  const { data: firms = [], isLoading: firmsLoading } = useQuery<ChannelFirm[]>({
    queryKey: ['firms'],
    queryFn: async () => {
      const { data } = await api.get('/core/firms')
      return data.data ?? []
    },
    staleTime: 10 * 60 * 1000,
  })

  // Fetch channels for each firm in parallel
  const channelQueries = useQueries({
    queries: firms.map(firm => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<ChannelItem[]> => {
        const { data } = await api.get(`/core/firms/${firm.id}/platforms`)
        return (data.data ?? []).map((ch: ChannelItem) => ({
          ...ch,
          firmId: firm.id,
          firmName: getChName(firm),
        }))
      },
      enabled: firms.length > 0,
      staleTime: 5 * 60 * 1000,
    })),
  })

  const allChannels = useMemo<ChannelItem[]>(() => {
    return channelQueries.flatMap(q => q.data ?? []).filter(ch => ch.isActive)
  }, [channelQueries])

  const channelsLoading = firms.length > 0 && channelQueries.some(q => q.isLoading)

  // Auto-select first channel
  useEffect(() => {
    if (!selectedChannelId && allChannels.length > 0) {
      setSelectedChannelId(allChannels[0].id)
    }
  }, [allChannels, selectedChannelId])

  // Fetch pricing for selected channel
  const { data: pricingData = [], isLoading: pricingLoading } = useQuery<PricingRecord[]>({
    queryKey: ['channel-pricing', selectedChannelId, product.id],
    queryFn: async () => {
      const { data } = await api.get(`/catalog/firm-platforms/${selectedChannelId}/products/${product.id}/pricing`)
      return data.data ?? []
    },
    enabled: !!selectedChannelId,
  })

  // Build rows from variants + pricing (left join)
  useEffect(() => {
    if (!selectedChannelId) return
    const next: Record<string, PricingRow> = {}
    for (const v of product.variants) {
      const p = pricingData.find(r => r.variantId === v.id)
      next[v.id] = {
        variantId: v.id,
        sku: v.sku,
        attrLabel: variantAttrLabel(v),
        basePrice: v.basePrice > 0 ? v.basePrice : product.basePrice,
        priceType: p?.priceType ?? '',
        priceMultiplier: p?.priceMultiplier != null ? String(p.priceMultiplier) : '',
        price: p?.price != null ? String(p.price) : '',
        compareAtPrice: p?.compareAtPrice != null ? String(p.compareAtPrice) : '',
        isActive: p?.isActive ?? true,
      }
    }
    setRows(next)
    setSavedRows(new Set())
    setBulkPrice('')
    setBulkPriceType('manual')
  }, [pricingData, selectedChannelId, product.variants])

  function updateRow(variantId: string, patch: Partial<PricingRow>) {
    setRows(prev => ({ ...prev, [variantId]: { ...prev[variantId], ...patch } }))
    setSavedRows(prev => { const s = new Set(prev); s.delete(variantId); return s })
  }

  async function saveRow(variantId: string) {
    if (!selectedChannelId) return
    const row = rows[variantId]
    if (!row) return
    setSavingRows(prev => new Set(prev).add(variantId))
    try {
      await api.put(`/catalog/firm-platforms/${selectedChannelId}/variants/${variantId}/price`, {
        priceType: row.priceType || null,
        priceMultiplier: row.priceType === 'multiplier' && row.priceMultiplier ? parseFloat(row.priceMultiplier) : null,
        price: row.priceType === 'manual' && row.price ? parseFloat(row.price) : null,
        compareAtPrice: row.compareAtPrice ? parseFloat(row.compareAtPrice) : null,
        isActive: row.isActive,
        firmPlatformCode: selectedChannel?.code ?? null,
      })
      queryClient.invalidateQueries({ queryKey: ['channel-pricing', selectedChannelId, product.id] })
      queryClient.invalidateQueries({ queryKey: ['product-price-history', product.id] })
      setSavedRows(prev => new Set(prev).add(variantId))
    } finally {
      setSavingRows(prev => { const s = new Set(prev); s.delete(variantId); return s })
    }
  }

  async function saveAll() {
    const unsaved = Object.keys(rows)
    await Promise.all(unsaved.map(id => saveRow(id)))
  }

  function applyBulkPrice() {
    if (!bulkPrice.trim()) return
    setRows(prev => {
      const next = { ...prev }
      for (const id of Object.keys(next)) {
        next[id] = {
          ...next[id],
          priceType: bulkPriceType,
          price: bulkPriceType === 'manual' ? bulkPrice : next[id].price,
          priceMultiplier: bulkPriceType === 'multiplier' ? bulkPrice : next[id].priceMultiplier,
        }
      }
      return next
    })
    setSavedRows(new Set())
  }

  // Group channels by firm — must be before any early returns
  const channelsByFirm = useMemo(() => {
    const map: Record<string, { firmName: string; channels: ChannelItem[] }> = {}
    for (const ch of allChannels) {
      if (!map[ch.firmId]) map[ch.firmId] = { firmName: ch.firmName, channels: [] }
      map[ch.firmId].channels.push(ch)
    }
    return Object.values(map)
  }, [allChannels])

  if (firmsLoading || channelsLoading) {
    return <div className="p-4 text-sm text-center" style={{ color: 'var(--text-s)' }}>Kanallar yükleniyor…</div>
  }

  if (allChannels.length === 0) {
    return (
      <div className="card py-12 text-center max-w-md">
        <p className="text-sm" style={{ color: 'var(--text-s)' }}>
          Aktif satış kanalı bulunamadı. Önce <strong>Ayarlar → Satış Kanalları</strong>'ndan kanal oluşturun.
        </p>
      </div>
    )
  }

  const selectedChannel = allChannels.find(ch => ch.id === selectedChannelId)
  const rowList = Object.values(rows)
  const hasUnsaved = rowList.some(r => !savedRows.has(r.variantId))

  function effectiveChannelPrice(r: PricingRow, ch: ChannelItem | undefined): number | null {
    if (r.priceType === 'manual' && r.price) return parseFloat(r.price)
    if (r.priceType === 'multiplier' && r.priceMultiplier) return parseFloat(r.priceMultiplier) * r.basePrice
    // no variant override → fall through to channel rule
    if (ch?.priceType === 'multiplier' && ch.priceMultiplier) return ch.priceMultiplier * r.basePrice
    return null
  }

  const lowPriceCount = rowList.filter(r => {
    const p = effectiveChannelPrice(r, selectedChannel)
    return p !== null && p < r.basePrice
  }).length

  return (
    <div className="flex flex-col gap-5">
      {/* Channel selector */}
      <div className="flex items-start gap-4 flex-wrap">
        {channelsByFirm.map(group => (
          <div key={group.firmName} className="flex flex-col gap-1.5">
            <span className="text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>
              {group.firmName}
            </span>
            <div className="flex flex-wrap gap-1.5">
              {group.channels.map(ch => {
                const active = selectedChannelId === ch.id
                return (
                  <button
                    key={ch.id}
                    onClick={() => setSelectedChannelId(ch.id)}
                    className={cn(
                      'flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-sm font-medium transition-all',
                      active ? 'shadow-sm' : 'hover:opacity-80',
                    )}
                    style={
                      active
                        ? { background: 'var(--brand)', color: '#fff' }
                        : { background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }
                    }
                  >
                    {ch.platformTypeIsMarketplace ? '🛒 ' : '🌐 '}
                    {getChName(ch)}
                  </button>
                )
              })}
            </div>
          </div>
        ))}
      </div>

      {/* Pricing table */}
      {selectedChannel && (
        <div className="card overflow-hidden p-0">
          {/* Table header bar */}
          <div className="flex items-center justify-between px-4 py-3"
            style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
            <div className="flex items-center gap-3 flex-wrap">
              <div>
                <span className="text-sm font-semibold" style={{ color: 'var(--text)' }}>
                  {getChName(selectedChannel)}
                </span>
                <span className="ml-2 text-xs" style={{ color: 'var(--text-s)' }}>
                  {getChName({ nameI18n: selectedChannel.platformTypeNameI18n, code: selectedChannel.platformTypeCode })}
                </span>
              </div>
              {/* Channel rule badge */}
              {selectedChannel.priceType === 'multiplier' && selectedChannel.priceMultiplier ? (
                <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium"
                  style={{ background: '#f0fdf4', color: '#15803d', border: '1px solid #bbf7d0' }}>
                  Kanal kuralı: ×{selectedChannel.priceMultiplier} — tüm varyantlara uygulanır
                </span>
              ) : selectedChannel.priceType === 'manual' ? (
                <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full"
                  style={{ background: 'var(--surface)', color: 'var(--text-s)', border: '1px solid var(--border)' }}>
                  Kanal kuralı: Manuel (varyant bazlı)
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full"
                  style={{ background: 'var(--surface)', color: 'var(--text-s)', border: '1px solid var(--border)' }}>
                  Kanal kuralı: Firmadan alınıyor
                </span>
              )}
            </div>
            <Button
              size="sm"
              variant="secondary"
              onClick={saveAll}
              disabled={!hasUnsaved || savingRows.size > 0}
              loading={savingRows.size > 0}
            >
              <Save size={13} /> Tümünü Kaydet
            </Button>
          </div>

          {/* Low-price warning banner */}
          {lowPriceCount > 0 && (
            <div className="flex items-center gap-2 px-4 py-2.5 text-xs"
              style={{ background: '#fffbeb', borderBottom: '1px solid #fde68a', color: '#92400e' }}>
              <AlertCircle size={13} style={{ color: '#d97706', flexShrink: 0 }} />
              <span>
                <strong>{lowPriceCount} varyant</strong> için kanal fiyatı ana fiyatın altında.
                Kaydedebilirsiniz, ancak bu kanalda zarar satışı oluşabilir.
              </span>
            </div>
          )}

          {/* Bulk price bar */}
          <div className="flex items-center gap-2 flex-wrap px-4 py-2.5"
            style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface)' }}>
            <span className="text-xs font-medium shrink-0" style={{ color: 'var(--text-s)' }}>
              Toplu fiyat:
            </span>
            <select
              className="sel text-xs"
              style={{ padding: '4px 8px', width: 110 }}
              value={bulkPriceType}
              onChange={e => setBulkPriceType(e.target.value as 'manual' | 'multiplier')}
            >
              <option value="manual">Manuel</option>
              <option value="multiplier">Çarpan</option>
            </select>
            <div className="flex items-center gap-1">
              {bulkPriceType === 'multiplier' && (
                <span className="text-xs" style={{ color: 'var(--text-s)' }}>×</span>
              )}
              <input
                type="number"
                className="inp text-sm"
                style={{ padding: '4px 10px', width: 110 }}
                placeholder={bulkPriceType === 'multiplier' ? '1.00' : '0.00'}
                step="0.01"
                min="0"
                value={bulkPrice}
                onChange={e => setBulkPrice(e.target.value)}
              />
              {bulkPriceType === 'manual' && (
                <span className="text-xs" style={{ color: 'var(--text-s)' }}>₺</span>
              )}
            </div>
            <button
              onClick={applyBulkPrice}
              disabled={!bulkPrice.trim()}
              className="text-xs px-3 py-1.5 rounded-xl font-medium transition-colors disabled:opacity-40"
              style={{ background: 'var(--brand)', color: '#fff' }}
            >
              Tüm Varyantlara Uygula
            </button>
            <span className="text-xs" style={{ color: 'var(--text-s)' }}>
              — Varyant fiyatlarını ayrıca düzenleyebilirsiniz
            </span>
          </div>

          {pricingLoading ? (
            <div className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
              Fiyatlar yükleniyor…
            </div>
          ) : product.variants.length === 0 ? (
            <div className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
              Bu ürünün varyantı bulunmuyor.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--border)' }}>
                    {['VARYANT', 'ANA FİYAT', 'FİYAT TİPİ', 'KANAL FİYATI', 'LİSTE FİYATI', 'AKTİF', ''].map(h => (
                      <th key={h}
                        className={cn('px-3 py-2.5 text-xs font-semibold tracking-wider text-left', h === '' && 'w-20')}
                        style={{ color: 'var(--text-s)' }}>
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {product.variants.map(v => {
                    const row = rows[v.id]
                    if (!row) return null
                    const saving = savingRows.has(v.id)
                    const saved = savedRows.has(v.id)

                    const chPrice = effectiveChannelPrice(row, selectedChannel)
                    const isLowPrice = chPrice !== null && chPrice < row.basePrice

                    // computed from channel-level multiplier when no variant override
                    const autoChannelPrice =
                      row.priceType === '' && selectedChannel.priceType === 'multiplier' && selectedChannel.priceMultiplier
                        ? selectedChannel.priceMultiplier * row.basePrice
                        : null

                    return (
                      <tr key={v.id}
                        style={{
                          borderBottom: '1px solid var(--border)',
                          background: isLowPrice ? '#fffbeb' : undefined,
                        }}>
                        {/* Variant info */}
                        <td className="px-3 py-2.5">
                          <div className="flex flex-col gap-0.5">
                            <code className="text-xs font-mono" style={{ color: 'var(--text-m)' }}>{v.sku}</code>
                            {row.attrLabel && (
                              <span className="text-xs" style={{ color: 'var(--text-s)' }}>{row.attrLabel}</span>
                            )}
                          </div>
                        </td>
                        {/* Base price (read-only) */}
                        <td className="px-3 py-2.5">
                          <span className="text-sm font-mono" style={{ color: 'var(--text-m)' }}>
                            {row.basePrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺
                          </span>
                        </td>
                        {/* Price type */}
                        <td className="px-3 py-2.5">
                          <select
                            className="sel text-xs"
                            style={{ padding: '4px 8px', minWidth: 110 }}
                            value={row.priceType}
                            onChange={e => updateRow(v.id, { priceType: e.target.value, price: '', priceMultiplier: '' })}
                          >
                            {CH_PRICE_TYPES.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                          </select>
                        </td>
                        {/* Channel price / multiplier */}
                        <td className="px-3 py-2.5">
                          <div className="flex items-center gap-1.5">
                            {row.priceType === 'manual' ? (
                              <input
                                type="number"
                                className="inp text-sm"
                                style={{ padding: '4px 8px', width: 110, borderColor: isLowPrice ? '#fbbf24' : undefined }}
                                placeholder="0.00"
                                step="0.01"
                                min="0"
                                value={row.price}
                                onChange={e => updateRow(v.id, { price: e.target.value })}
                              />
                            ) : row.priceType === 'multiplier' ? (
                              <div className="flex items-center gap-1">
                                <span className="text-xs" style={{ color: 'var(--text-s)' }}>×</span>
                                <input
                                  type="number"
                                  className="inp text-sm"
                                  style={{ padding: '4px 8px', width: 80, borderColor: isLowPrice ? '#fbbf24' : undefined }}
                                  placeholder="1.00"
                                  step="0.01"
                                  min="0"
                                  value={row.priceMultiplier}
                                  onChange={e => updateRow(v.id, { priceMultiplier: e.target.value })}
                                />
                              </div>
                            ) : autoChannelPrice !== null ? (
                              <span className="flex items-center gap-1.5">
                                <span className="text-sm font-mono font-medium" style={{ color: '#15803d' }}>
                                  {autoChannelPrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺
                                </span>
                                <span className="text-xs px-1.5 py-0.5 rounded-md"
                                  style={{ background: '#f0fdf4', color: '#15803d', border: '1px solid #bbf7d0' }}>
                                  ×{selectedChannel.priceMultiplier}
                                </span>
                              </span>
                            ) : (
                              <span className="text-xs italic" style={{ color: 'var(--text-s)' }}>
                                {row.basePrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺
                              </span>
                            )}
                            {isLowPrice && (
                              <span
                                title={`Ana fiyatın (${row.basePrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺) altında`}
                                style={{ display: 'flex', alignItems: 'center' }}>
                                <AlertCircle size={14} style={{ color: '#d97706', flexShrink: 0 }} />
                              </span>
                            )}
                          </div>
                        </td>
                        {/* Compare-at price */}
                        <td className="px-3 py-2.5">
                          <input
                            type="number"
                            className="inp text-sm"
                            style={{ padding: '4px 8px', width: 100 }}
                            placeholder="—"
                            step="0.01"
                            min="0"
                            value={row.compareAtPrice}
                            onChange={e => updateRow(v.id, { compareAtPrice: e.target.value })}
                          />
                        </td>
                        {/* Active */}
                        <td className="px-3 py-2.5">
                          <label className="flex items-center justify-center cursor-pointer">
                            <input
                              type="checkbox"
                              className="w-4 h-4 accent-[var(--brand)]"
                              checked={row.isActive}
                              onChange={e => updateRow(v.id, { isActive: e.target.checked })}
                            />
                          </label>
                        </td>
                        {/* Save per row */}
                        <td className="px-3 py-2.5 text-right">
                          {saved ? (
                            <span className="flex items-center justify-end gap-1 text-xs"
                              style={{ color: '#16a34a' }}>
                              <CheckCircle size={12} /> Kaydedildi
                            </span>
                          ) : (
                            <button
                              onClick={() => saveRow(v.id)}
                              disabled={saving}
                              className="text-xs px-2 py-1 rounded-lg transition-colors disabled:opacity-40"
                              style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                            >
                              {saving ? '…' : 'Kaydet'}
                            </button>
                          )}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// ── Main Component ─────────────────────────────────────────────────────────────

export function ProductDetailPage() {
  const { code } = useParams<{ code: string }>()
  const navigate  = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  const [activeTab,  setActiveTab]  = useState<Tab>('genel')
  const [activeLang, setActiveLang] = useState<string | null>(null)

  // ── Fetch product ──
  const { data: product, isLoading: productLoading } = useQuery<ProductDetail>({
    queryKey: ['product', code],
    queryFn: async () => {
      const { data } = await api.get(`/catalog/products/${code}`)
      return data.data
    },
    enabled: !!code,
  })

  // ── Fetch product groups ──
  const { data: groups = [] } = useQuery<ProductGroup[]>({
    queryKey: ['product-groups', false],
    queryFn: async () => {
      const { data } = await api.get('/catalog/product-groups?activeOnly=false')
      return data.data
    },
    staleTime: 5 * 60 * 1000,
  })

  // ── Fetch suppliers (current accounts of type supplier/both) ──
  const { data: suppliers = [] } = useQuery<{ id: string; code: string; title: string; isActive: boolean }[]>({
    queryKey: ['current-accounts-suppliers'],
    queryFn: async () => {
      const { data } = await api.get('/accounts?accountType=supplier&pageSize=500&isActive=true')
      return data.data?.items ?? []
    },
    staleTime: 5 * 60 * 1000,
  })

  // ── Fetch attribute types (for select dropdowns in Özellikler tab) ──
  const { data: attrTypes = [] } = useQuery<{
    id: string; code: string; nameI18n: Record<string, string>; dataType: string
    values: { id: string; nameI18n: Record<string, string> }[]
  }[]>({
    queryKey: ['attribute-types', false],
    queryFn: async () => {
      const { data } = await api.get('/catalog/attribute-types?activeOnly=false')
      return data.data
    },
    staleTime: 5 * 60 * 1000,
  })

  // ── Fetch price history (lazy — only when modal open) ──
  const [historyOpen, setHistoryOpen] = useState(false)
  const { data: priceHistory = [], isLoading: historyLoading } = useQuery<{
    id: string; source: string; priceField: string
    firmPlatformCode: string | null; variantSku: string | null
    oldValue: number | null; newValue: number | null
    changedAt: string; changedBy: string | null; changedByName: string | null
  }[]>({
    queryKey: ['product-price-history', product?.id],
    enabled: historyOpen && !!product?.id,
    queryFn: async () => {
      const { data } = await api.get(`/catalog/products/${product!.id}/price-history`)
      return data.data ?? []
    },
  })

  // ── Fetch warehouses + stocks (stok tab only) ──
  const { data: warehouses = [] } = useQuery<Warehouse[]>({
    queryKey: ['warehouses'],
    enabled: activeTab === 'stok' || activeTab === 'varyantlar',
    queryFn: async () => {
      const { data } = await api.get('/inventory/warehouses')
      return data.data ?? []
    },
    staleTime: 5 * 60 * 1000,
  })

  const { data: allStocks = [], isLoading: stocksLoading } = useQuery<StockDto[]>({
    queryKey: ['product-stocks', product?.id],
    enabled: (activeTab === 'stok' || activeTab === 'varyantlar') && !!product,
    queryFn: async () => {
      if (!product?.variants?.length) return []
      const settled = await Promise.allSettled(
        product.variants.map((v) =>
          api.get(`/inventory/stocks?variantId=${v.id}`).then((r) => (r.data.data ?? []) as StockDto[])
        )
      )
      return settled.flatMap((r) => (r.status === 'fulfilled' ? r.value : []))
    },
  })

  // ── Form state ──
  const [nameI18n,      setNameI18n]      = useState<Record<string, string>>({})
  const [shortDescI18n, setShortDescI18n] = useState<Record<string, string>>({})
  const [descI18n,      setDescI18n]      = useState<Record<string, string>>({})
  const [basePrice,     setBasePrice]     = useState('0')
  const [baseCost,      setBaseCost]      = useState('')
  const [taxRate,              setTaxRate]             = useState(18)
  const [isActive,             setIsActive]            = useState(true)
  const [supplierId,           setSupplierId]          = useState<string | null>(null)
  const [supplierProductCode,  setSupplierProductCode] = useState('')
  const [saveStatus,           setSaveStatus]          = useState<'idle' | 'ok' | 'err'>('idle')
  const [confirmStatusChange, setConfirmStatusChange] = useState<boolean | null>(null)
  // attributeTypeId → selected attributeValueId
  const [attrForm, setAttrForm] = useState<Record<string, string>>({})

  useEffect(() => {
    if (product) {
      setNameI18n(product.nameI18n)
      setShortDescI18n(product.shortDescriptionI18n ?? {})
      setDescI18n(product.descriptionI18n ?? {})
      setBasePrice(String(product.basePrice))
      setBaseCost(product.baseCost != null ? String(product.baseCost) : '')
      setTaxRate(product.taxRate)
      setIsActive(product.isActive)
      setSupplierId(product.supplierId)
      setSupplierProductCode(product.supplierProductCode ?? '')
      const init: Record<string, string> = {}
      for (const a of product.attributes) {
        if (a.attributeValueId) init[a.attributeTypeId] = a.attributeValueId
      }
      setAttrForm(init)
      // init barcodes
      const bc: Record<string, string> = {}
      for (const v of product.variants) {
        bc[v.id] = v.barcode ?? ''
      }
      setBarcodes(bc)
    }
  }, [product])

  // Set default active lang once languages load
  useEffect(() => {
    if (languages.length > 0 && activeLang === null) {
      const def = languages.find((l) => l.isDefault) ?? languages[0]
      setActiveLang(def.code)
    }
  }, [languages, activeLang])

  // ── Save mutation ──
  const saveMutation = useMutation({
    mutationFn: async () => {
      await api.put(`/catalog/products/${product!.id}`, {
        nameI18n,
        shortDescriptionI18n: shortDescI18n,
        descriptionI18n: descI18n,
        basePrice:  parseFloat(basePrice) || 0,
        baseCost:   baseCost.trim() ? parseFloat(baseCost) : null,
        taxRate,
        isActive,
        supplierId: supplierId || null,
        supplierProductCode: supplierProductCode.trim() || null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', code] })
      queryClient.invalidateQueries({ queryKey: ['products'] })
      setSaveStatus('ok')
      setTimeout(() => setSaveStatus('idle'), 2500)
    },
    onError: () => setSaveStatus('err'),
  })

  const [attrSaveStatus, setAttrSaveStatus] = useState<'idle' | 'ok' | 'err'>('idle')

  const saveAttrMutation = useMutation({
    mutationFn: async () => {
      const group = groups.find((g) => g.id === product!.productGroupId)
      const nonVariantAttrs = (group?.attributes ?? []).filter((a) => !a.isVariant)
      const attributes = nonVariantAttrs.map((a) => ({
        attributeTypeId: a.attributeTypeId,
        attributeValueId: attrForm[a.attributeTypeId] ?? null,
        customValue: null,
      }))
      await api.put(`/catalog/products/${product!.id}/attributes`, { attributes })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', code] })
      setAttrSaveStatus('ok')
      setTimeout(() => setAttrSaveStatus('idle'), 2500)
    },
    onError: () => setAttrSaveStatus('err'),
  })

  // ── Variant builder state ──
  const [variantModalOpen, setVariantModalOpen] = useState(false)
  const [deleteVariantId, setDeleteVariantId] = useState<string | null>(null)
  const [togglingVariant, setTogglingVariant] = useState<string | null>(null)
  const [deleteProductConfirm, setDeleteProductConfirm] = useState(false)
  // axisTypeId → Set of selected valueIds
  const [axisSelections, setAxisSelections] = useState<Record<string, Set<string>>>({})

  // ── Barcode state: variantId → barcode string ──
  const [barcodes, setBarcodes] = useState<Record<string, string>>({})
  // ── Barcode settings modal ──
  const [barcodeSettingsOpen, setBarcodeSettingsOpen] = useState(false)
  const [barcodeSeqInput, setBarcodeSeqInput] = useState('')

  // ── Sub-attribute inline form: axisValueId → { subAttrTypeId → value } ──
  const [subAttrFormValues, setSubAttrFormValues] = useState<Record<string, Record<string, string>>>({})
  const [subAttrSaveStatus, setSubAttrSaveStatus] = useState<'idle' | 'ok' | 'err'>('idle')

  const addVariantsMutation = useMutation({
    mutationFn: async (variants: { attributes: { attributeTypeId: string; attributeValueId: string }[] }[]) => {
      await api.post(`/catalog/products/${product!.id}/variants`, {
        variants: variants.map((v) => ({ sku: null, attributes: v.attributes })),
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', code] })
      setVariantModalOpen(false)
      setAxisSelections({})
    },
  })

  const saveBarcodeMutation = useMutation({
    mutationFn: async ({ variantId, barcode }: { variantId: string; barcode: string }) => {
      await api.put(`/catalog/variants/${variantId}/barcode`, { barcode: barcode || null })
    },
  })

  const { data: catalogSettings = [] } = useQuery<{ key: string; value: string }[]>({
    queryKey: ['catalog-settings'],
    queryFn: async () => {
      const { data } = await api.get('/catalog/settings')
      return data.data
    },
    staleTime: 60 * 1000,
  })

  const updateSettingMutation = useMutation({
    mutationFn: async ({ key, value }: { key: string; value: string }) => {
      await api.put(`/catalog/settings/${key}`, { value })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['catalog-settings'] })
      setBarcodeSettingsOpen(false)
    },
  })

  const currentBarcodeSeq = catalogSettings.find((s) => s.key === 'barcode_sequence')?.value ?? '—'

  const generateBarcodesMutation = useMutation({
    mutationFn: async (count: number) => {
      const { data } = await api.post(`/catalog/barcodes/generate?count=${count}`)
      return data.data as string[]
    },
    onSuccess: (generatedBarcodes) => {
      if (!product) return
      const empty = product.variants.filter((v) => !barcodes[v.id])
      setBarcodes((prev) => {
        const next = { ...prev }
        empty.forEach((v, i) => { if (generatedBarcodes[i]) next[v.id] = generatedBarcodes[i] })
        return next
      })
      // Auto-save each new barcode
      empty.forEach((v, i) => {
        if (generatedBarcodes[i])
          saveBarcodeMutation.mutate({ variantId: v.id, barcode: generatedBarcodes[i] })
      })
    },
  })

  const saveSubAttrsMutation = useMutation({
    mutationFn: async () => {
      const values = Object.entries(subAttrFormValues).flatMap(([attributeValueId, props]) =>
        Object.entries(props).map(([subAttributeTypeId, value]) => ({ attributeValueId, subAttributeTypeId, value }))
      )
      await api.put(`/catalog/products/${product!.id}/axis-sub-attribute-values`, { values })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', code] })
      setSubAttrSaveStatus('ok')
      setTimeout(() => setSubAttrSaveStatus('idle'), 2500)
    },
    onError: () => setSubAttrSaveStatus('err'),
  })

  const deleteVariantMutation = useMutation({
    mutationFn: (variantId: string) => api.delete(`/catalog/variants/${variantId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', product?.code] })
      setDeleteVariantId(null)
    },
  })

  const deleteProductMutation = useMutation({
    mutationFn: () => api.delete(`/catalog/products/${product?.id}`),
    onSuccess: () => {
      setDeleteProductConfirm(false)
      navigate('/catalog/products')
    },
  })

  const toggleVariantStatusMutation = useMutation({
    mutationFn: ({ variantId, isActive }: { variantId: string; isActive: boolean }) =>
      api.patch(`/catalog/variants/${variantId}/status`, { isActive }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', product?.code] })
      setTogglingVariant(null)
    },
    onError: () => setTogglingVariant(null),
  })

  // ── Variant price state ──
  const [variantPrices, setVariantPrices] = useState<Record<string, { basePrice: string; baseCost: string }>>({})
  const [savingVariantPrice, setSavingVariantPrice] = useState<Record<string, boolean>>({})
  const [savedVariantPrice, setSavedVariantPrice] = useState<Record<string, boolean>>({})

  const getVariantPrice = (variantId: string, field: 'basePrice' | 'baseCost', fallback: number) => {
    return variantPrices[variantId]?.[field] ?? String(fallback)
  }

  const setVariantPriceField = (variantId: string, field: 'basePrice' | 'baseCost', value: string) => {
    setVariantPrices((prev) => ({
      ...prev,
      [variantId]: { basePrice: prev[variantId]?.basePrice ?? '', baseCost: prev[variantId]?.baseCost ?? '', [field]: value },
    }))
  }

  const saveVariantPrice = async (variantId: string, currentBasePrice: number, currentBaseCost: number | null) => {
    const prices = variantPrices[variantId]
    const basePrice = prices?.basePrice !== undefined ? parseFloat(prices.basePrice) : currentBasePrice
    const baseCost = prices?.baseCost !== undefined && prices.baseCost !== '' ? parseFloat(prices.baseCost) : (currentBaseCost ?? undefined)
    if (isNaN(basePrice)) return
    setSavingVariantPrice((p) => ({ ...p, [variantId]: true }))
    try {
      await api.put(`/catalog/variants/${variantId}/price`, { basePrice, baseCost })
      queryClient.invalidateQueries({ queryKey: ['product', product?.code] })
      setSavedVariantPrice((p) => ({ ...p, [variantId]: true }))
      setTimeout(() => setSavedVariantPrice((p) => ({ ...p, [variantId]: false })), 2500)
    } finally {
      setSavingVariantPrice((p) => ({ ...p, [variantId]: false }))
    }
  }

  // ── Derived ──
  const sourceLang = languages.find((l) => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'

  const coverImageUrl = useMemo(() => {
    if (!product?.variants) return null
    for (const v of product.variants) {
      const main = v.images?.find((i) => i.isMain)
      if (main) return main.imageUrl
    }
    for (const v of product.variants) {
      if (v.images?.length) return v.images[0].imageUrl
    }
    return null
  }, [product?.variants])

  const groupName = useMemo(() => {
    if (!product) return '—'
    const g = groups.find((g) => g.id === product.productGroupId)
    return g ? getName(g) : '—'
  }, [product, groups])

  const warehouseMap = useMemo(() => {
    const m = new Map<string, string>()
    warehouses.forEach((w) => m.set(w.id, getName(w)))
    return m
  }, [warehouses])

  const variantMap = useMemo(() => {
    const m = new Map<string, Variant>()
    product?.variants.forEach((v) => m.set(v.id, v))
    return m
  }, [product])

  // non-variant group attributes (for Özellikler tab)
  const nonVariantGroupAttrs = useMemo(() => {
    const group = groups.find((g) => g.id === product?.productGroupId)
    return (group?.attributes ?? []).filter((a) => !a.isVariant).sort((a, b) => a.sortOrder - b.sortOrder)
  }, [groups, product])

  // variant axis group attributes — primary axis first
  const variantAxisAttrs = useMemo(() => {
    const group = groups.find((g) => g.id === product?.productGroupId)
    return (group?.attributes ?? [])
      .filter((a) => a.isVariant)
      .sort((a, b) => {
        if (a.isPrimaryAxis !== b.isPrimaryAxis) return a.isPrimaryAxis ? -1 : 1
        return a.sortOrder - b.sortOrder
      })
  }, [groups, product])

  // primary axis attributeTypeId (for images tab and variant display)
  const primaryAxisAttributeTypeId = useMemo(() => {
    return variantAxisAttrs.find((a) => a.isPrimaryAxis)?.attributeTypeId ?? null
  }, [variantAxisAttrs])

  // combinations from axis selections
  const variantCombinations = useMemo(() => {
    const axes = variantAxisAttrs
      .map((a) => ({
        typeId: a.attributeTypeId,
        selected: Array.from(axisSelections[a.attributeTypeId] ?? []),
      }))
      .filter((a) => a.selected.length > 0)

    if (axes.length === 0) return []

    // Cartesian product
    let combos: { attributeTypeId: string; attributeValueId: string }[][] = [[]]
    for (const axis of axes) {
      combos = combos.flatMap((combo) =>
        axis.selected.map((valueId) => [
          ...combo,
          { attributeTypeId: axis.typeId, attributeValueId: valueId },
        ])
      )
    }
    return combos
  }, [variantAxisAttrs, axisSelections])

  // attrType lookup: id → values[]
  const attrTypeMap = useMemo(() => {
    const m = new Map<string, { id: string; nameI18n: Record<string, string> }[]>()
    attrTypes.forEach((t) => m.set(t.id, t.values))
    return m
  }, [attrTypes])

  const hasAxisSubAttrs = (product?.axisSubAttributeSchema?.length ?? 0) > 0

  // sub-attr label helper
  function getSubAttrLabel(s: SubAttributeSchemaItem) {
    return s.subAttributeTypeNameI18n['tr'] ?? s.subAttributeTypeNameI18n[Object.keys(s.subAttributeTypeNameI18n)[0]] ?? s.subAttributeTypeCode
  }

  // unique axis values per axis type used in this product's variants
  // axisTypeId → [{ id, nameI18n }]  (ordered by variant occurrence, deduplicated)
  const usedAxisValues = useMemo(() => {
    const m = new Map<string, Map<string, { id: string; nameI18n: Record<string, string> }>>()
    for (const v of product?.variants ?? []) {
      for (const va of v.variantAttributes) {
        if (!m.has(va.attributeTypeId)) m.set(va.attributeTypeId, new Map())
        m.get(va.attributeTypeId)!.set(va.attributeValueId, { id: va.attributeValueId, nameI18n: va.attributeValueNameI18n })
      }
    }
    return m
  }, [product])

  // Initialize sub-attr form values from product.axisSubAttributeValues
  useEffect(() => {
    if (!product || !hasAxisSubAttrs) return
    const init: Record<string, Record<string, string>> = {}
    for (const v of product.axisSubAttributeValues) {
      if (!init[v.attributeValueId]) init[v.attributeValueId] = {}
      init[v.attributeValueId][v.subAttributeTypeId] = v.value
    }
    setSubAttrFormValues(init)
    setSubAttrSaveStatus('idle')
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [product?.id])

  const stockSummary = useMemo(() => ({
    total:     allStocks.reduce((s, x) => s + x.quantity, 0),
    reserved:  allStocks.reduce((s, x) => s + x.reservedQuantity, 0),
    available: allStocks.reduce((s, x) => s + x.availableQuantity, 0),
  }), [allStocks])

  const productName = product ? (product.nameI18n['tr'] ?? product.nameI18n[Object.keys(product.nameI18n)[0]] ?? product.code) : ''
  const variantCount = product?.variants.length ?? 0

  const handleSetActive = useCallback(async (active: boolean) => {
    if (!product) return
    await api.patch(`/catalog/products/${product.id}/${active ? 'activate' : 'deactivate'}`)
    setIsActive(active)
    queryClient.invalidateQueries({ queryKey: ['product', code] })
  }, [product, code, queryClient])

  if (productLoading || langsLoading) return <PageSpinner />
  if (!product) return (
    <div className="p-8 text-center" style={{ color: 'var(--text-s)' }}>
      Ürün bulunamadı.{' '}
      <button onClick={() => navigate('/catalog/products')} className="underline">
        Listeye dön
      </button>
    </div>
  )

  // ── Tab label with count ──
  const tabLabel = (t: { key: Tab; label: string }) => {
    if (t.key === 'varyantlar') return `${t.label} (${variantCount})`
    return t.label
  }

  return (
    <div className="flex-1 flex flex-col">

      {/* ── Header + Tabs (vht) ── */}
      <div className="vht">
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-center gap-3">
            {/* Thumbnail */}
            <div
              className="w-12 h-12 rounded-2xl flex-shrink-0 overflow-hidden"
              style={{ background: 'var(--surface2)' }}
            >
              {coverImageUrl ? (
                <img
                  src={coverImageUrl}
                  alt={product.nameI18n?.['tr'] ?? product.code}
                  className="w-full h-full object-cover"
                />
              ) : (
                <div className="w-full h-full flex items-center justify-center text-xs" style={{ color: 'var(--text-s)' }}>
                  IMG
                </div>
              )}
            </div>
            <div>
              <div className="flex items-center gap-1.5 text-xs mb-1" style={{ color: 'var(--text-s)' }}>
                <Link to="/catalog/products" className="hover:underline" style={{ color: 'var(--text-s)' }}>
                  Ürünler
                </Link>
                <ChevronRight size={12} />
                <span style={{ color: 'var(--text-m)' }}>{product.code}</span>
              </div>
              <div className="flex items-center flex-wrap gap-2">
                <h1 className="text-lg font-bold" style={{ color: 'var(--text)' }}>{productName}</h1>
                <Badge variant={isActive ? 'success' : 'default'}>
                  {isActive ? 'Aktif' : 'Pasif'}
                </Badge>
              </div>
              <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
                {product.code} · {groupName} · {variantCount} varyant
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2 flex-shrink-0">
            {saveStatus === 'ok' && (
              <span className="flex items-center gap-1.5 text-xs px-3 py-1.5 rounded-xl font-medium"
                style={{ background: '#f0fdf4', color: '#16a34a', border: '1px solid #bbf7d0' }}>
                <CheckCircle size={12} /> Kaydedildi
              </span>
            )}
            {saveStatus === 'err' && (
              <span className="flex items-center gap-1.5 text-xs px-3 py-1.5 rounded-xl font-medium"
                style={{ background: '#fef2f2', color: '#dc2626', border: '1px solid #fecaca' }}>
                <AlertCircle size={12} /> Hata!
              </span>
            )}
            <Button onClick={() => saveMutation.mutate()} loading={saveMutation.isPending}>
              <Save size={13} /> Kaydet
            </Button>
          </div>
        </div>

        {/* Tabs */}
        <div className="tab-scroll mt-4 border-b" style={{ borderColor: 'var(--border)' }}>
          {TABS
            .filter((t) => t.key !== 'altOzellikler' || (product?.axisSubAttributeSchema?.length ?? 0) > 0)
            .map((t) => (
              <button
                key={t.key}
                className={cn('stab', activeTab === t.key && 'active')}
                onClick={() => setActiveTab(t.key)}
              >
                {tabLabel(t)}
              </button>
            ))}
        </div>
      </div>

      {/* ═══════ TAB: GENEL ═══════ */}
      {activeTab === 'genel' && (
        <div className="vc flex detail-cols gap-6">
          {/* LEFT */}
          <div className="flex-1 space-y-4 min-w-0">

            {/* Temel Bilgiler */}
            <div className="card space-y-4">
              <div className="flex items-center justify-between">
                <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Temel Bilgiler</h2>
                <button
                  onClick={() => setHistoryOpen(true)}
                  className="flex items-center gap-1.5 text-xs px-2.5 py-1 rounded-lg transition-colors"
                  style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                >
                  <Info size={12} /> Fiyat Geçmişi
                </button>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="flbl">Ürün Kodu</label>
                  <input className="inp" value={product.code} disabled />
                </div>
                <div>
                  <label className="flbl">Ürün Grubu</label>
                  <input className="inp" value={groupName} disabled />
                </div>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div>
                  <label className="flbl">Alış Fiyatı (₺)</label>
                  <input
                    type="number"
                    className="inp"
                    value={baseCost}
                    onChange={(e) => setBaseCost(e.target.value)}
                    placeholder="0,00"
                    min={0}
                    step="0.01"
                  />
                </div>
                <div>
                  <label className="flbl">Satış Fiyatı (₺)</label>
                  <input
                    type="number"
                    className="inp"
                    value={basePrice}
                    onChange={(e) => setBasePrice(e.target.value)}
                    min={0}
                    step="0.01"
                  />
                  <p className="text-[11px] mt-1 flex items-center gap-1" style={{ color: 'var(--text-s)' }}>
                    <Info size={10} /> Tüm varyantlara baz fiyat olarak yansır
                  </p>
                </div>
                <div>
                  <label className="flbl">KDV (%)</label>
                  <select
                    className="sel"
                    value={taxRate}
                    onChange={(e) => setTaxRate(Number(e.target.value))}
                  >
                    {TAX_RATES.map((r) => (
                      <option key={r} value={r}>%{r}</option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="flbl">Tedarikçi</label>
                  <SearchableSelect
                    value={supplierId}
                    onChange={(v) => setSupplierId(v)}
                    options={suppliers.map((s) => ({ value: s.id, label: `${s.title} (${s.code})` }))}
                    placeholder="— Seçiniz —"
                    hasValue={!!supplierId}
                  />
                </div>
                <div>
                  <label className="flbl">Tedarikçi Ürün Kodu</label>
                  <input
                    className="inp"
                    value={supplierProductCode}
                    onChange={(e) => setSupplierProductCode(e.target.value)}
                    placeholder="Tedarikçinin ürün kodu"
                  />
                </div>
              </div>
            </div>

            {/* Çok Dilli İçerik */}
            <div className="card overflow-hidden">
              <div className="flex items-center justify-between px-4 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
                <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Çok Dilli İçerik</h2>
                <div className="flex flex-wrap items-center gap-1">
                  {languages.map((l) => {
                    const hasName = !!nameI18n[l.code]
                    return (
                      <Badge key={l.code} variant={hasName ? 'success' : 'default'} className="text-[10px]">
                        {hasName ? '✓ ' : ''}{l.code.toUpperCase()}
                      </Badge>
                    )
                  })}
                </div>
              </div>

              {/* Language tabs */}
              <div className="tab-scroll border-b" style={{ borderColor: 'var(--border)', background: 'var(--surface2)' }}>
                {languages.map((l) => (
                  <button
                    key={l.code}
                    className={cn('lang-tab', activeLang === l.code && 'active')}
                    onClick={() => setActiveLang(l.code)}
                  >
                    {l.name}
                  </button>
                ))}
              </div>

              {/* Active lang panel */}
              {languages.map((l) =>
                activeLang === l.code ? (
                  <LangPanel
                    key={l.code}
                    lang={l.code}
                    sourceLang={sourceLang}
                    isSource={l.code === sourceLang}
                    nameVal={nameI18n[l.code] ?? ''}
                    shortDescVal={shortDescI18n[l.code] ?? ''}
                    descVal={descI18n[l.code] ?? ''}
                    sourceName={nameI18n[sourceLang] ?? ''}
                    sourceShortDesc={shortDescI18n[sourceLang] ?? ''}
                    sourceDesc={descI18n[sourceLang] ?? ''}
                    onNameChange={(v) => setNameI18n((p) => ({ ...p, [l.code]: v }))}
                    onShortDescChange={(v) => setShortDescI18n((p) => ({ ...p, [l.code]: v }))}
                    onDescChange={(v) => setDescI18n((p) => ({ ...p, [l.code]: v }))}
                  />
                ) : null
              )}
            </div>
          </div>

          {/* RIGHT sidebar */}
          <div className="detail-right w-full md:w-64 flex-shrink-0 space-y-4">

            {/* Durum */}
            <div className="card overflow-hidden">
              <div className="px-4 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
                <h3 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Durum</h3>
              </div>
              <div className="p-4">
                <Badge variant={isActive ? 'success' : 'danger'}>
                  {isActive ? 'Aktif' : 'Pasif'}
                </Badge>
              </div>
            </div>

            {/* Kayıt Bilgisi */}
            <div className="card overflow-hidden">
              <div className="px-4 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
                <h3 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Kayıt Bilgisi</h3>
              </div>
              <div className="p-4 space-y-2.5">
                <div className="flex justify-between">
                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>Oluşturulma</span>
                  <span className="text-xs font-medium" style={{ color: 'var(--text)' }}>
                    {fmtDate(product.createdAt)}
                  </span>
                </div>
                {product.updatedAt && (
                  <div className="flex justify-between">
                    <span className="text-xs" style={{ color: 'var(--text-s)' }}>Son Güncelleme</span>
                    <span className="text-xs font-medium" style={{ color: 'var(--text)' }}>
                      {fmtDate(product.updatedAt)}
                    </span>
                  </div>
                )}
                <div className="h-px" style={{ background: 'var(--border)' }} />
                <div className="flex justify-between">
                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>Varyant</span>
                  <span className="text-sm font-bold" style={{ color: 'var(--text)' }}>{variantCount} adet</span>
                </div>
              </div>
            </div>

            {/* Tehlikeli Alan */}
            <div className="card overflow-hidden" style={{ borderColor: '#fecaca' }}>
              <div className="px-4 py-3 border-b" style={{ borderColor: '#fecaca' }}>
                <h3 className="text-sm font-bold text-red-500">Tehlikeli Alan</h3>
              </div>
              <div className="p-4 space-y-2">
                <button
                  onClick={() => setConfirmStatusChange(!isActive)}
                  className="w-full text-left flex items-center gap-2 px-3 py-2 rounded-xl text-sm font-medium transition-colors hover:bg-[var(--surface2)]"
                  style={{ border: '1px solid var(--border)', color: 'var(--text-m)' }}
                >
                  <EyeOff size={12} style={{ color: 'var(--text-s)' }} />
                  {isActive ? 'Pasife Al' : 'Aktife Al'}
                </button>
                <button
                  className="w-full text-left flex items-center gap-2 px-3 py-2 rounded-xl text-sm font-medium text-red-500 transition-colors hover:bg-red-50"
                  style={{ border: '1px solid #fecaca' }}
                  onClick={() => setDeleteProductConfirm(true)}
                >
                  <Trash2 size={12} /> Ürünü Sil
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Ürün silme onay modalı */}
      <Modal
        open={deleteProductConfirm}
        onClose={() => setDeleteProductConfirm(false)}
        title="Ürünü Sil"
        size="sm"
        footer={
          <>
            <Button variant="secondary" onClick={() => setDeleteProductConfirm(false)}>İptal</Button>
            <Button
              variant="danger"
              loading={deleteProductMutation.isPending}
              onClick={() => deleteProductMutation.mutate()}
            >
              Kalıcı Olarak Sil
            </Button>
          </>
        }
      >
        <p className="text-sm" style={{ color: 'var(--text-m)' }}>
          <strong>{productName}</strong> ürünü ve tüm varyantları silinecek. Bu işlem geri alınamaz.
        </p>
      </Modal>

      {/* Durum değiştirme onay modalı */}
      <Modal
        open={confirmStatusChange !== null}
        onClose={() => setConfirmStatusChange(null)}
        title={confirmStatusChange ? 'Ürünü Aktife Al' : 'Ürünü Pasife Al'}
        size="sm"
        footer={
          <>
            <Button variant="secondary" onClick={() => setConfirmStatusChange(null)}>İptal</Button>
            <Button
              variant={confirmStatusChange ? 'primary' : 'danger'}
              onClick={() => { const t = confirmStatusChange!; setConfirmStatusChange(null); handleSetActive(t) }}
            >
              {confirmStatusChange ? 'Aktife Al' : 'Pasife Al'}
            </Button>
          </>
        }
      >
        <p className="text-sm" style={{ color: 'var(--text)' }}>
          <strong>{productName}</strong> ürünü {confirmStatusChange ? 'aktife alınacak ve yayınlanacak' : 'pasife alınacak ve yayından kaldırılacak'}. Emin misiniz?
        </p>
      </Modal>

      {/* ═══════ TAB: ÖZELLİKLER ═══════ */}
      {activeTab === 'ozellikler' && (
        <div className="vc flex flex-col gap-4">
          {nonVariantGroupAttrs.length === 0 ? (
            <div
              className="flex items-center gap-2 px-3 py-2.5 rounded-xl text-xs max-w-2xl"
              style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text-s)' }}
            >
              <Info size={13} style={{ color: 'var(--brand)', flexShrink: 0 }} />
              <span>
                Bu ürün grubunda henüz özellik tanımlı değil. <strong style={{ color: 'var(--text-m)' }}>Ürün Grupları</strong> sayfasından özellik ekleyebilirsiniz.
              </span>
            </div>
          ) : (
            <div className="card overflow-hidden max-w-2xl">
              <div className="flex items-center justify-between px-5 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
                <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Ürün Özellikleri</h2>
                <div className="flex items-center gap-2">
                  {attrSaveStatus === 'ok' && (
                    <span className="flex items-center gap-1 text-xs px-2.5 py-1 rounded-lg font-medium"
                      style={{ background: '#f0fdf4', color: '#16a34a', border: '1px solid #bbf7d0' }}>
                      <CheckCircle size={11} /> Kaydedildi
                    </span>
                  )}
                  {attrSaveStatus === 'err' && (
                    <span className="text-xs text-red-500">Hata!</span>
                  )}
                  <Button size="sm" onClick={() => saveAttrMutation.mutate()} loading={saveAttrMutation.isPending}>
                    <Save size={12} /> Kaydet
                  </Button>
                </div>
              </div>
              <div className="p-5 space-y-4">
                {nonVariantGroupAttrs.map((ga) => {
                  const values = attrTypeMap.get(ga.attributeTypeId) ?? []
                  const attrName = ga.attributeTypeNameI18n['tr'] ?? ga.attributeTypeCode
                  const selectedValueId = attrForm[ga.attributeTypeId] ?? ''
                  return (
                    <div key={ga.id}>
                      <label className="flbl">
                        {attrName}
                        {ga.isRequired && <span style={{ color: '#ef4444' }}> *</span>}
                      </label>
                      <select
                        className="sel"
                        value={selectedValueId}
                        onChange={(e) => setAttrForm((f) => ({ ...f, [ga.attributeTypeId]: e.target.value }))}
                      >
                        <option value="">— Seçiniz —</option>
                        {values.map((v) => (
                          <option key={v.id} value={v.id}>
                            {v.nameI18n['tr'] ?? v.nameI18n[Object.keys(v.nameI18n)[0]] ?? v.id}
                          </option>
                        ))}
                      </select>
                    </div>
                  )
                })}
              </div>
            </div>
          )}
        </div>
      )}

      {/* ═══════ TAB: VARYANTLAR ═══════ */}
      {activeTab === 'varyantlar' && (
        <div className="vc flex flex-col">

          <div className="card overflow-hidden max-w-5xl">
            <div className="flex items-center justify-between px-4 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
              <div>
                <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Varyantlar</h2>
                <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
                  {variantCount > 0 ? `${variantCount} varyant` : 'Henüz varyant eklenmedi'}
                </p>
              </div>
              <div className="flex items-center gap-2">
                {variantCount > 0 && (
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => {
                        const emptyCount = (product?.variants ?? []).filter((v) => !barcodes[v.id]).length
                        if (emptyCount > 0) generateBarcodesMutation.mutate(emptyCount)
                      }}
                      disabled={generateBarcodesMutation.isPending}
                      className="flex items-center gap-1.5 px-3 py-1.5 rounded-l-lg text-xs font-semibold transition-colors hover:bg-[var(--surface2)] disabled:opacity-50"
                      style={{ border: '1px solid var(--border)', borderRight: 'none', color: 'var(--text-m)' }}
                    >
                      {generateBarcodesMutation.isPending ? 'Oluşturuluyor…' : 'Barkod Oluştur'}
                    </button>
                    <button
                      onClick={() => { setBarcodeSeqInput(currentBarcodeSeq); setBarcodeSettingsOpen(true) }}
                      title={`Seri: ${currentBarcodeSeq}`}
                      className="flex items-center px-2 py-1.5 rounded-r-lg text-xs transition-colors hover:bg-[var(--surface2)]"
                      style={{ border: '1px solid var(--border)', color: 'var(--text-s)' }}
                    >
                      <Settings2 size={13} />
                    </button>
                  </div>
                )}
                {variantAxisAttrs.length > 0 && (
                  <Button size="sm" onClick={() => {
                    // Pre-populate axisSelections from existing variants
                    const sel: Record<string, Set<string>> = {}
                    for (const v of (product?.variants ?? [])) {
                      for (const va of v.variantAttributes) {
                        if (!sel[va.attributeTypeId]) sel[va.attributeTypeId] = new Set()
                        sel[va.attributeTypeId].add(va.attributeValueId)
                      }
                    }
                    setAxisSelections(sel)
                    setVariantModalOpen(true)
                  }}>
                    + Varyant Ekle
                  </Button>
                )}
              </div>
            </div>

            {variantCount === 0 ? (
              <div className="px-4 py-12 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Bu ürün henüz varyant içermiyor.
              </div>
            ) : (
              <>
                <div className="tbl-wrap">
                  <table className="w-full" style={{ minWidth: 520 }}>
                    <thead>
                      <tr style={{ background: 'var(--surface2)' }}>
                        <th className="px-4 py-3 text-left text-xs font-semibold" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>SKU</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>ÖZELLİKLER</th>
                        <th className="px-4 py-3 text-left text-xs font-semibold mob-hide" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>BARKOD</th>
                        <th className="px-4 py-3 text-right text-xs font-semibold" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>FİYAT (₺)</th>
                        <th className="px-4 py-3 text-right text-xs font-semibold mob-hide" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>STOK</th>
                        <th className="px-4 py-3 text-center text-xs font-semibold" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)', width: 80 }}>DURUM</th>
                        <th className="px-4 py-3 text-center text-xs font-semibold" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)', width: 48 }}></th>
                      </tr>
                    </thead>
                    <tbody>
                      {product.variants.map((v) => {
                        const stock = allStocks.filter((s) => s.variantId === v.id)
                        const avail = stock.reduce((s, x) => s + x.availableQuantity, 0)
                        return (
                          <tr key={v.id} className="trow">
                            <td className="px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
                              <code className="text-xs font-mono" style={{ color: 'var(--text-m)' }}>{v.sku}</code>
                            </td>
                            <td className="px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
                              <div className="flex flex-wrap gap-1">
                                {v.variantAttributes && v.variantAttributes.length > 0 ? (
                                  v.variantAttributes
                                    .slice()
                                    .sort((a, b) => {
                                      if (primaryAxisAttributeTypeId) {
                                        if (a.attributeTypeId === primaryAxisAttributeTypeId) return -1
                                        if (b.attributeTypeId === primaryAxisAttributeTypeId) return 1
                                      }
                                      return 0
                                    })
                                    .map((va) => (
                                      <span
                                        key={va.attributeTypeId}
                                        className="text-xs px-2 py-0.5 rounded-full font-medium"
                                        style={va.attributeTypeId === primaryAxisAttributeTypeId
                                          ? { background: 'var(--brand)', color: '#fff', border: '1px solid var(--brand)' }
                                          : { background: 'var(--brand-bg)', color: 'var(--brand)', border: '1px solid var(--brand-b)' }}
                                      >
                                        {va.attributeValueNameI18n['tr'] ?? va.attributeValueNameI18n[Object.keys(va.attributeValueNameI18n)[0]] ?? '—'}
                                      </span>
                                    ))
                                ) : (
                                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>
                                )}
                              </div>
                            </td>
                            <td className="px-4 py-3 mob-hide" style={{ borderBottom: '1px solid var(--border)' }}>
                              <input
                                className="inp"
                                style={{ padding: '4px 8px', fontSize: 12, fontFamily: 'monospace', width: 150 }}
                                placeholder="Barkod girilmedi"
                                value={barcodes[v.id] ?? ''}
                                onChange={(e) => setBarcodes((b) => ({ ...b, [v.id]: e.target.value }))}
                                onBlur={() => saveBarcodeMutation.mutate({ variantId: v.id, barcode: barcodes[v.id] ?? '' })}
                              />
                            </td>
                            <td className="px-4 py-3 text-right" style={{ borderBottom: '1px solid var(--border)' }}>
                              <div className="flex items-center justify-end gap-1">
                                <input
                                  type="number"
                                  className="inp text-right"
                                  style={{ padding: '4px 8px', fontSize: 13, width: 90 }}
                                  value={getVariantPrice(v.id, 'basePrice', v.basePrice)}
                                  min={0}
                                  step="0.01"
                                  onChange={(e) => setVariantPriceField(v.id, 'basePrice', e.target.value)}
                                  onBlur={() => saveVariantPrice(v.id, v.basePrice, v.baseCost ?? null)}
                                />
                                {savedVariantPrice[v.id] && (
                                  <span style={{ color: 'var(--brand)', fontSize: 11 }}>✓</span>
                                )}
                                {savingVariantPrice[v.id] && (
                                  <span style={{ color: 'var(--text-s)', fontSize: 11 }}>...</span>
                                )}
                              </div>
                            </td>
                            <td className="px-4 py-3 text-right mob-hide" style={{ borderBottom: '1px solid var(--border)' }}>
                              <span className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{avail}</span>
                            </td>
                            <td className="px-4 py-3 text-center" style={{ borderBottom: '1px solid var(--border)' }}>
                              <button
                                className="cursor-pointer"
                                title={v.isActive ? 'Pasif yap' : 'Aktif yap'}
                                disabled={togglingVariant === v.id}
                                onClick={() => {
                                  setTogglingVariant(v.id)
                                  toggleVariantStatusMutation.mutate({ variantId: v.id, isActive: !v.isActive })
                                }}
                              >
                                <Badge variant={v.isActive ? 'success' : 'default'}>
                                  {togglingVariant === v.id ? '...' : (v.isActive ? 'Aktif' : 'Pasif')}
                                </Badge>
                              </button>
                            </td>
                            <td className="px-4 py-3 text-center" style={{ borderBottom: '1px solid var(--border)' }}>
                              <button
                                className="p-1 rounded transition-colors hover:bg-red-50"
                                title="Varyantı sil"
                                onClick={() => setDeleteVariantId(v.id)}
                                style={{ color: 'var(--text-s)' }}
                              >
                                <Trash2 size={14} />
                              </button>
                            </td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
                <div className="px-4 py-3 border-t flex items-center gap-2 text-xs" style={{ borderColor: 'var(--border)', background: 'var(--surface2)', color: 'var(--text-s)' }}>
                  <Info size={12} />
                  <span>
                    Barkod girilmemiş varyantlar için{' '}
                    <strong style={{ color: 'var(--text-m)' }}>Barkod Oluştur</strong>{' '}
                    butonunu kullanın.
                  </span>
                </div>
              </>
            )}
          </div>

          {/* Variant delete confirm modal */}
          <Modal
            open={!!deleteVariantId}
            onClose={() => setDeleteVariantId(null)}
            title="Varyantı Sil"
            size="sm"
            footer={
              <>
                <Button variant="secondary" onClick={() => setDeleteVariantId(null)}>İptal</Button>
                <Button
                  variant="danger"
                  loading={deleteVariantMutation.isPending}
                  onClick={() => deleteVariantId && deleteVariantMutation.mutate(deleteVariantId)}
                >
                  Sil
                </Button>
              </>
            }
          >
            <p className="text-sm" style={{ color: 'var(--text-m)' }}>
              Bu varyant kalıcı olarak silinecek. Devam etmek istiyor musunuz?
            </p>
          </Modal>

          {/* Variant builder modal */}
          <Modal
            open={variantModalOpen}
            onClose={() => setVariantModalOpen(false)}
            title="Varyant Ekle"
            size="lg"
            footer={
              <>
                <Button variant="secondary" onClick={() => setVariantModalOpen(false)}>İptal</Button>
                <Button
                  onClick={() => addVariantsMutation.mutate(variantCombinations.map((c) => ({ attributes: c })))}
                  loading={addVariantsMutation.isPending}
                  disabled={variantCombinations.length === 0}
                >
                  {variantCombinations.length > 0
                    ? `${variantCombinations.length} Kombinasyon Ekle`
                    : 'Kombinasyon Seçin'}
                </Button>
              </>
            }
          >
            <div className="space-y-5">
              {variantAxisAttrs.map((axis) => {
                const values = attrTypeMap.get(axis.attributeTypeId) ?? []
                const selected = axisSelections[axis.attributeTypeId] ?? new Set<string>()
                const axisName = axis.attributeTypeNameI18n['tr'] ?? axis.attributeTypeCode
                return (
                  <div key={axis.id}>
                    <label className="flbl mb-2">
                      {axisName}
                      <span className="ml-1 text-xs font-normal" style={{ color: 'var(--text-s)' }}>
                        ({selected.size} seçili)
                      </span>
                    </label>
                    <div className="flex flex-wrap gap-2">
                      {values.map((v) => {
                        const vName = v.nameI18n['tr'] ?? v.nameI18n[Object.keys(v.nameI18n)[0]] ?? '?'
                        const isSelected = selected.has(v.id)
                        return (
                          <button
                            key={v.id}
                            type="button"
                            onClick={() => {
                              setAxisSelections((prev) => {
                                const next = new Set(prev[axis.attributeTypeId] ?? [])
                                isSelected ? next.delete(v.id) : next.add(v.id)
                                return { ...prev, [axis.attributeTypeId]: next }
                              })
                            }}
                            className="px-3 py-1.5 rounded-lg text-sm font-medium transition-colors"
                            style={isSelected
                              ? { background: 'var(--brand)', color: '#fff', border: '1px solid var(--brand)' }
                              : { background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }
                            }
                          >
                            {vName}
                          </button>
                        )
                      })}
                      {values.length === 0 && (
                        <p className="text-xs" style={{ color: 'var(--text-s)' }}>Bu özellik tipinde değer tanımlı değil.</p>
                      )}
                    </div>
                  </div>
                )
              })}

              {variantCombinations.length > 0 && (
                <div className="rounded-xl p-3" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
                  <p className="text-xs font-semibold mb-2" style={{ color: 'var(--text-m)' }}>
                    Oluşturulacak kombinasyonlar ({variantCombinations.length})
                  </p>
                  <div className="flex flex-wrap gap-1.5">
                    {variantCombinations.map((combo, i) => {
                      const label = combo.map((a) => {
                        const values = attrTypeMap.get(a.attributeTypeId) ?? []
                        const v = values.find((x) => x.id === a.attributeValueId)
                        return v ? (v.nameI18n['tr'] ?? v.nameI18n[Object.keys(v.nameI18n)[0]] ?? '?') : '?'
                      }).join(' / ')
                      return (
                        <span
                          key={i}
                          className="text-xs px-2 py-0.5 rounded-full"
                          style={{ background: 'var(--brand-bg)', color: 'var(--brand)', border: '1px solid var(--brand-b)' }}
                        >
                          {label}
                        </span>
                      )
                    })}
                  </div>
                </div>
              )}

              {addVariantsMutation.isError && (
                <p className="text-sm" style={{ color: '#ef4444' }}>
                  {(addVariantsMutation.error as any)?.response?.data?.error ?? 'Hata oluştu.'}
                </p>
              )}
            </div>
          </Modal>
        </div>
      )}

      {/* Barcode Settings Modal — global (outside tab) */}
      <Modal
        open={barcodeSettingsOpen}
        onClose={() => setBarcodeSettingsOpen(false)}
        title="Barkod Seri Ayarı"
        size="sm"
        footer={
          <>
            <Button variant="secondary" onClick={() => setBarcodeSettingsOpen(false)}>İptal</Button>
            <Button
              onClick={() => updateSettingMutation.mutate({ key: 'barcode_sequence', value: barcodeSeqInput })}
              loading={updateSettingMutation.isPending}
              disabled={!barcodeSeqInput || isNaN(Number(barcodeSeqInput)) || Number(barcodeSeqInput) < 1}
            >
              Kaydet
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <p className="text-sm" style={{ color: 'var(--text-s)' }}>
            Bir sonraki barkod bu sayıdan başlayarak üretilir. Mevcut değer: <strong style={{ color: 'var(--text)' }}>{currentBarcodeSeq}</strong>
          </p>
          <div>
            <label className="flbl">Seri Başlangıç Değeri</label>
            <input
              type="number"
              className="inp"
              min={1}
              value={barcodeSeqInput}
              onChange={(e) => setBarcodeSeqInput(e.target.value)}
              placeholder="örn: 273"
            />
          </div>
          {barcodeSeqInput && !isNaN(Number(barcodeSeqInput)) && Number(barcodeSeqInput) >= 1 && (
            <p className="text-xs" style={{ color: 'var(--text-s)' }}>
              İlk üretilecek EAN-13: <code className="font-mono" style={{ color: 'var(--text-m)' }}>
                {toEan13(Number(barcodeSeqInput))}
              </code>
            </p>
          )}
          {updateSettingMutation.isError && (
            <p className="text-sm" style={{ color: '#ef4444' }}>
              {(updateSettingMutation.error as any)?.response?.data?.error ?? 'Hata oluştu.'}
            </p>
          )}
        </div>
      </Modal>

      {/* ═══════ TAB: ALT ÖZELLİKLER ═══════ */}
      {activeTab === 'altOzellikler' && (
        <div className="vc flex flex-col gap-4">
          {variantCount === 0 ? (
            <div
              className="flex items-center gap-2 px-3 py-2.5 rounded-xl text-xs max-w-2xl"
              style={{ background: 'var(--surface2)', border: '1px solid var(--border)', color: 'var(--text-s)' }}
            >
              <Info size={13} style={{ color: 'var(--brand)', flexShrink: 0 }} />
              <span>Alt özellik değeri girmek için önce varyant oluşturun.</span>
            </div>
          ) : (
            <div className="card p-0 overflow-hidden max-w-5xl">
              <div className="flex items-center justify-between px-4 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
                <div>
                  <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Varyant Alt Özellikleri</h2>
                  <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
                    Eksen değeri başına ölçüsel özellikler — tüm renkler için aynı beden değerleri paylaşılır
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  {subAttrSaveStatus === 'ok' && (
                    <span className="flex items-center gap-1 text-xs px-2.5 py-1 rounded-lg font-medium"
                      style={{ background: '#f0fdf4', color: '#16a34a', border: '1px solid #bbf7d0' }}>
                      <CheckCircle size={11} /> Kaydedildi
                    </span>
                  )}
                  {subAttrSaveStatus === 'err' && (
                    <span className="text-xs" style={{ color: '#ef4444' }}>Hata!</span>
                  )}
                  <Button size="sm" onClick={() => saveSubAttrsMutation.mutate()} loading={saveSubAttrsMutation.isPending}>
                    <Save size={12} /> Kaydet
                  </Button>
                </div>
              </div>

              {(product?.axisSubAttributeSchema ?? []).map((axisSchema, axisIdx) => {
                const axisValues = Array.from(usedAxisValues.get(axisSchema.axisAttributeTypeId)?.values() ?? [])
                if (axisValues.length === 0 || axisSchema.subAttributes.length === 0) return null
                const axisName = axisSchema.axisAttributeTypeNameI18n['tr']
                  ?? axisSchema.axisAttributeTypeNameI18n[Object.keys(axisSchema.axisAttributeTypeNameI18n)[0]]
                  ?? axisSchema.axisAttributeTypeCode
                const isLast = axisIdx === (product?.axisSubAttributeSchema.length ?? 1) - 1
                return (
                  <div key={axisSchema.axisAttributeTypeId}
                    style={{ borderBottom: isLast ? undefined : '1px solid var(--border)' }}>
                    {(product?.axisSubAttributeSchema.length ?? 0) > 1 && (
                      <div className="px-4 py-2 border-b" style={{ background: 'var(--surface2)', borderColor: 'var(--border)' }}>
                        <span className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--text-s)' }}>
                          {axisName}
                        </span>
                      </div>
                    )}
                    <div className="tbl-wrap">
                      <table className="w-full">
                        <thead>
                          <tr style={{ background: 'var(--surface2)', borderBottom: '1px solid var(--border)' }}>
                            <th className="px-4 py-2.5 text-left text-xs font-semibold" style={{ color: 'var(--text-s)', width: 160 }}>
                              ALT ÖZELLİK
                            </th>
                            {axisValues.map((val) => {
                              const valName = val.nameI18n['tr'] ?? val.nameI18n[Object.keys(val.nameI18n)[0]] ?? '—'
                              return (
                                <th key={val.id} className="px-3 py-2.5 text-center text-xs font-semibold" style={{ color: 'var(--text-s)' }}>
                                  <span className="px-2 py-0.5 rounded-full font-semibold"
                                    style={{ background: 'var(--brand)', color: '#fff' }}>
                                    {valName}
                                  </span>
                                </th>
                              )
                            })}
                          </tr>
                        </thead>
                        <tbody>
                          {axisSchema.subAttributes.map((sub) => (
                            <tr key={sub.subAttributeTypeId} style={{ borderBottom: '1px solid var(--border)' }}>
                              <td className="px-4 py-2.5 text-sm font-medium" style={{ color: 'var(--text)' }}>
                                {getSubAttrLabel(sub)}
                                {sub.isRequired && <span className="ml-0.5" style={{ color: '#ef4444' }}>*</span>}
                              </td>
                              {axisValues.map((val) => (
                                <td key={val.id} className="px-3 py-2 text-center">
                                  <input
                                    className="inp text-center"
                                    style={{ padding: '4px 8px', fontSize: 13, width: 90 }}
                                    value={subAttrFormValues[val.id]?.[sub.subAttributeTypeId] ?? ''}
                                    placeholder="—"
                                    onChange={(e) => {
                                      const v = e.target.value
                                      setSubAttrFormValues((prev) => ({
                                        ...prev,
                                        [val.id]: { ...(prev[val.id] ?? {}), [sub.subAttributeTypeId]: v },
                                      }))
                                    }}
                                  />
                                </td>
                              ))}
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>
      )}

      {/* ═══════ TAB: STOK ═══════ */}
      {activeTab === 'stok' && (
        <div className="vc flex flex-col gap-4">
          {/* Özet kartlar */}
          <div className="grid grid-cols-3 gap-3 max-w-lg">
            <div className="card text-center">
              <p className="text-2xl font-bold" style={{ color: 'var(--text)' }}>{stockSummary.total}</p>
              <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Toplam</p>
            </div>
            <div className="card text-center">
              <p className="text-2xl font-bold text-amber-500">{stockSummary.reserved}</p>
              <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Rezerve</p>
            </div>
            <div className="card text-center">
              <p className="text-2xl font-bold" style={{ color: 'var(--brand)' }}>{stockSummary.available}</p>
              <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Kullanılabilir</p>
            </div>
          </div>

          {/* Depo Bazlı Stok */}
          <div className="card overflow-hidden max-w-4xl">
            <div className="flex items-center justify-between px-4 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
              <h2 className="text-sm font-bold" style={{ color: 'var(--text)' }}>Depo Bazlı Stok</h2>
              <Link
                to="/inventory/stocks"
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold"
                style={{ background: 'var(--brand-bg)', color: 'var(--brand)', border: '1px solid var(--brand-b)' }}
              >
                Stok Hareketi
              </Link>
            </div>
            {stocksLoading ? (
              <div className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</div>
            ) : allStocks.length === 0 ? (
              <div className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Stok kaydı bulunamadı.
              </div>
            ) : (
              <div className="tbl-wrap">
                <table className="w-full" style={{ minWidth: 400 }}>
                  <thead>
                    <tr style={{ background: 'var(--surface2)' }}>
                      <th className="px-4 py-3 text-left text-xs font-semibold" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>DEPO</th>
                      <th className="px-4 py-3 text-left text-xs font-semibold" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>VARYANT</th>
                      <th className="px-4 py-3 text-right text-xs font-semibold" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>TOPLAM</th>
                      <th className="px-4 py-3 text-right text-xs font-semibold mob-hide" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>REZERVE</th>
                      <th className="px-4 py-3 text-right text-xs font-semibold" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>KULLANILABİLİR</th>
                    </tr>
                  </thead>
                  <tbody>
                    {allStocks.map((s) => {
                      const variant = variantMap.get(s.variantId)
                      const wName = warehouseMap.get(s.warehouseId) ?? s.warehouseId
                      return (
                        <tr key={s.id} className="trow">
                          <td className="px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
                            <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{wName}</span>
                          </td>
                          <td className="px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
                            <span className="text-xs" style={{ color: 'var(--text-m)' }}>{variant?.sku ?? '—'}</span>
                          </td>
                          <td className="px-4 py-3 text-right" style={{ borderBottom: '1px solid var(--border)' }}>
                            <span className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{s.quantity}</span>
                          </td>
                          <td className="px-4 py-3 text-right mob-hide" style={{ borderBottom: '1px solid var(--border)' }}>
                            <span className={cn('text-sm font-semibold', s.reservedQuantity > 0 ? 'text-amber-500' : '')} style={s.reservedQuantity === 0 ? { color: 'var(--text-s)' } : {}}>
                              {s.reservedQuantity}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-right" style={{ borderBottom: '1px solid var(--border)' }}>
                            <span className="text-sm font-semibold" style={{ color: 'var(--brand)' }}>{s.availableQuantity}</span>
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}

      {/* ═══════ TAB: SATIŞ KANALLARI ═══════ */}
      {activeTab === 'kanallar' && (
        <div className="vc">
          <ChannelsPricingTab product={product} />
        </div>
      )}

      {/* ═══════ TAB: GÖRSELLER ═══════ */}
      {activeTab === 'gorseller' && (
        <ProductImagesTab
          productId={product.id}
          variants={product.variants.map(v => ({
            id: v.id,
            sku: v.sku,
            variantAttributes: v.variantAttributes,
          }))}
          primaryAxisAttributeTypeId={primaryAxisAttributeTypeId}
        />
      )}

      {/* ═══════ TAB: ETİKETLER ═══════ */}
      {activeTab === 'etiketler' && (
        <TagsTab product={product} onSaved={() => queryClient.invalidateQueries({ queryKey: ['product', code] })} />
      )}

      {/* ═══════ TAB: SEO ═══════ */}
      {activeTab === 'seo' && (
        <SeoTab product={product} languages={languages} onSaved={() => queryClient.invalidateQueries({ queryKey: ['product', code] })} />
      )}

      {/* ═══════ FİYAT GEÇMİŞİ SLIDE-OVER ═══════ */}
      {historyOpen && (
        <div className="fixed inset-0 z-50 flex justify-end" style={{ background: 'rgba(0,0,0,0.3)' }}
          onClick={() => setHistoryOpen(false)}>
          <div className="w-full max-w-2xl h-full flex flex-col overflow-hidden shadow-2xl"
            style={{ background: 'var(--surface)' }}
            onClick={e => e.stopPropagation()}>
            {/* Header */}
            <div className="flex items-center justify-between px-5 py-4"
              style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              <div>
                <h2 className="text-base font-bold" style={{ color: 'var(--text)' }}>Fiyat Geçmişi</h2>
                <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
                  {product.nameI18n?.['tr'] ?? product.code} — ürün ve satış kanalı fiyat değişimleri
                </p>
              </div>
              <button onClick={() => setHistoryOpen(false)}
                className="p-1.5 rounded-lg hover:opacity-60 transition-opacity"
                style={{ color: 'var(--text-s)' }}>
                <EyeOff size={16} />
              </button>
            </div>

            {/* Body */}
            <div className="flex-1 overflow-y-auto">
              {historyLoading ? (
                <div className="py-16 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                  Yükleniyor…
                </div>
              ) : priceHistory.length === 0 ? (
                <div className="py-16 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                  Henüz fiyat değişikliği kaydı bulunmuyor.
                </div>
              ) : (
                <table className="w-full">
                  <thead className="sticky top-0" style={{ background: 'var(--surface2)', zIndex: 1 }}>
                    <tr style={{ borderBottom: '1px solid var(--border)' }}>
                      {['TARİH / KİŞİ', 'KAYNAK', 'ESKİ', 'YENİ', 'DEĞİŞİM'].map(h => (
                        <th key={h} className="px-4 py-2.5 text-left text-xs font-semibold tracking-wider"
                          style={{ color: 'var(--text-s)' }}>{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {priceHistory.map(h => {
                      const diff = (h.newValue ?? 0) - (h.oldValue ?? 0)
                      const pct = h.oldValue ? ((diff / h.oldValue) * 100).toFixed(1) : null
                      const isChannel = h.source === 'channel'
                      return (
                        <tr key={h.id} style={{ borderBottom: '1px solid var(--border)' }}>
                          {/* Tarih + kişi */}
                          <td className="px-4 py-3">
                            <div className="flex flex-col gap-0.5">
                              <span className="text-xs font-medium" style={{ color: 'var(--text-m)' }}>
                                {new Date(h.changedAt).toLocaleString('tr-TR', {
                                  day: 'numeric', month: 'short', year: 'numeric',
                                  hour: '2-digit', minute: '2-digit',
                                })}
                              </span>
                              {h.changedByName && (
                                <span className="text-xs" style={{ color: 'var(--text-s)' }}>
                                  {h.changedByName}
                                </span>
                              )}
                            </div>
                          </td>
                          {/* Kaynak */}
                          <td className="px-4 py-3">
                            <div className="flex flex-col gap-0.5">
                              <span className="text-xs px-2 py-0.5 rounded-md font-medium"
                                style={{
                                  background: isChannel ? '#fef3c7' : 'var(--brand-bg)',
                                  color: isChannel ? '#92400e' : 'var(--brand)',
                                  border: '1px solid var(--border)',
                                  display: 'inline-block',
                                }}>
                                {isChannel
                                  ? (h.firmPlatformCode ?? 'Kanal')
                                  : h.priceField === 'base_price' ? 'Satış Fiyatı' : 'Alış Fiyatı'}
                              </span>
                              {isChannel && h.variantSku && (
                                <code className="text-xs" style={{ color: 'var(--text-s)' }}>
                                  {h.variantSku}
                                </code>
                              )}
                            </div>
                          </td>
                          {/* Eski */}
                          <td className="px-4 py-3">
                            <span className="text-sm font-mono" style={{ color: 'var(--text-s)' }}>
                              {h.oldValue != null
                                ? h.oldValue.toLocaleString('tr-TR', { minimumFractionDigits: 2 }) + ' ₺'
                                : '—'}
                            </span>
                          </td>
                          {/* Yeni */}
                          <td className="px-4 py-3">
                            <span className="text-sm font-mono font-medium" style={{ color: 'var(--text)' }}>
                              {h.newValue != null
                                ? h.newValue.toLocaleString('tr-TR', { minimumFractionDigits: 2 }) + ' ₺'
                                : '—'}
                            </span>
                          </td>
                          {/* Değişim */}
                          <td className="px-4 py-3">
                            <span className="text-xs font-medium px-2 py-0.5 rounded-full"
                              style={{
                                background: diff > 0 ? '#fef2f2' : diff < 0 ? '#f0fdf4' : 'var(--surface2)',
                                color: diff > 0 ? '#dc2626' : diff < 0 ? '#16a34a' : 'var(--text-s)',
                                border: '1px solid',
                                borderColor: diff > 0 ? '#fecaca' : diff < 0 ? '#bbf7d0' : 'var(--border)',
                              }}>
                              {diff > 0 ? '+' : ''}
                              {diff.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺
                              {pct && ` (${diff > 0 ? '+' : ''}${pct}%)`}
                            </span>
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>
      )}

    </div>
  )
}
