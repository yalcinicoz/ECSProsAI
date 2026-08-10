import { useState, useEffect, useMemo, useRef } from 'react'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import { Save, Plus } from 'lucide-react'
import api from '@/api/client'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { I18nField } from '@/components/ui/I18nField'
import { useLanguages } from '@/hooks/useLanguages'

/**
 * Ürün Kartı (F2, 2026-08-09) — iki sekme:
 *  - Yerleşim: kart elementlerinin kanal bazlı aç/kapat yönetimi + üç değişken alan
 *    (görsel altı bant / ürün adı altı / puan altı) kaynak ve öncelik ayarı.
 *    PUT /core/firm-platforms/{id}/product-card-settings ile kaydedilir.
 *  - Kart Mesajları: kanalın kart mesajı CRUD'u (/storefront/card-messages).
 * Sağdaki önizleme sitenin gerçek SSR markup'ıdır (/onizleme/urun-karti iframe'i) —
 * kaydedilmemiş yerleşim + gerçek aktif mesajlar query ile geçer, anında görünür.
 */

interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel {
  id: string; nameI18n: Record<string, string>; code: string
  firmId: string; firmName: string
  settings?: Record<string, unknown>
}

interface AreaConfig {
  enabled: boolean
  campaigns: boolean
  messages: boolean
  messagesFirst: boolean
  // Sosyal kanıt (yalnız Alan 3'te sunulur): canlı sepet/favori sayaç satırları
  showCartCount: boolean
  showFavoriteCount: boolean
}

interface CardConfig {
  videoBadge: boolean
  sponsorBadge: boolean
  colorBadge: boolean
  galleryDots: boolean
  favoriteButton: boolean
  collectionButton: boolean
  rating: boolean
  discountRow: boolean
  campaignPriceRow: boolean
  areas: { '1': AreaConfig; '2': AreaConfig; '3': AreaConfig }
}

interface CardMessage {
  id: string
  firmPlatformId: string
  slot: number
  messageI18n: Record<string, string>
  icon: string | null
  color: string | null
  scopeType: 'all' | 'category' | 'products'
  scopeCategoryIds: string[] | null
  scopeProductCodes: string[] | null
  startDate: string | null
  endDate: string | null
  sortOrder: number
  isActive: boolean
}

interface ChannelCategory { id: string; nameI18n: Record<string, string>; slug: string }

const AREA_KEYS = ['1', '2', '3'] as const
type AreaKey = (typeof AREA_KEYS)[number]

const AREA_LABELS: Record<AreaKey, { title: string; desc: string }> = {
  '1': { title: 'Alan 1 — Görsel altı bant', desc: 'Ürün görselinin hemen altındaki bant' },
  '2': { title: 'Alan 2 — Ürün adı altı satır', desc: 'Ürün adının altındaki satır' },
  '3': { title: 'Alan 3 — Puan altı satır', desc: 'Puan/yorum satırının altındaki satır' },
}

const SLOT_OPTIONS = [
  { value: 1, label: 'Alan 1 — görsel altı bant' },
  { value: 2, label: 'Alan 2 — ürün adı altı' },
  { value: 3, label: 'Alan 3 — puan altı' },
]

const COLOR_OPTIONS: { value: string; label: string; dot: string }[] = [
  { value: 'yesil', label: 'Yeşil', dot: '#16a34a' },
  { value: 'turuncu', label: 'Turuncu', dot: '#f97316' },
  { value: 'bordo', label: 'Bordo', dot: '#881337' },
  { value: 'pembe', label: 'Pembe', dot: '#ec4899' },
]

const ICON_CHIPS = ['fa-truck', 'fa-truck-fast', 'fa-percent', 'fa-tags', 'fa-ticket', 'fa-clock', 'fa-fire', 'fa-gift']

// Sıralama seçenekleri (2026-08-10) — kodlar backend ProductSortCatalog ile birebir;
// "default" kapatılamaz (sıralama seçilmediğinde düşülen seçenek).
const SORT_OPTIONS: { code: string; label: string; locked?: boolean }[] = [
  { code: 'default', label: 'Önerilen Sıralama', locked: true },
  { code: 'price_asc', label: 'En Düşük Fiyat' },
  { code: 'price_desc', label: 'En Yüksek Fiyat' },
  { code: 'newest', label: 'En Yeniler' },
  { code: 'rating_desc', label: 'En Yüksek Puanlı Ürünler' },
  { code: 'reviews_desc', label: 'En Fazla Yorum Alan Ürünler' },
  { code: 'favorites_desc', label: 'Favoriye En Çok Eklenen Ürünler' },
  { code: 'cart_desc', label: 'Sepete En Çok Atılan Ürünler' },
  { code: 'views_desc', label: 'En Çok Bakılan Ürünler' },
  { code: 'sales_desc', label: 'En Çok Satılan Ürünler' },
]

function sortOptionsFromSettings(settings: Record<string, unknown> | undefined): Record<string, boolean> {
  const sonuc: Record<string, boolean> = {}
  const pl = settings?.['productList']
  const so = pl && typeof pl === 'object' ? (pl as Record<string, unknown>)['sortOptions'] : undefined
  const kayit = so && typeof so === 'object' ? (so as Record<string, unknown>) : {}
  for (const opt of SORT_OPTIONS) sonuc[opt.code] = opt.locked ? true : kayit[opt.code] !== false
  return sonuc
}

function defaultArea(key: AreaKey): AreaConfig {
  return { enabled: true, campaigns: key === '1', messages: true, messagesFirst: true, showCartCount: false, showFavoriteCount: false }
}

const DEFAULT_CONFIG: CardConfig = {
  videoBadge: true,
  sponsorBadge: true,
  colorBadge: true,
  galleryDots: true,
  favoriteButton: true,
  collectionButton: true,
  rating: true,
  discountRow: true,
  campaignPriceRow: true,
  areas: { '1': defaultArea('1'), '2': defaultArea('2'), '3': defaultArea('3') },
}

function getName(i18n: Record<string, string> | null | undefined): string {
  if (!i18n) return ''
  return i18n['tr'] ?? i18n[Object.keys(i18n)[0]] ?? ''
}

function areaFromRaw(raw: unknown, key: AreaKey): AreaConfig {
  if (!raw || typeof raw !== 'object') return defaultArea(key)
  const o = raw as Record<string, unknown>
  return {
    enabled: o.enabled !== false,
    campaigns: key === '1' ? o.campaigns !== false : o.campaigns === true,
    messages: o.messages !== false,
    messagesFirst: o.messagesFirst !== false,
    showCartCount: o.showCartCount === true,
    showFavoriteCount: o.showFavoriteCount === true,
  }
}

function configFromSettings(settings: Record<string, unknown> | undefined): CardConfig {
  const pc = settings?.['productCard']
  if (!pc || typeof pc !== 'object') return structuredClone(DEFAULT_CONFIG)
  const o = pc as Record<string, unknown>

  let areas: CardConfig['areas']
  const rawAreas = o.areas
  if (rawAreas && typeof rawAreas === 'object') {
    const ra = rawAreas as Record<string, unknown>
    areas = {
      '1': areaFromRaw(ra['1'], '1'),
      '2': areaFromRaw(ra['2'], '2'),
      '3': areaFromRaw(ra['3'], '3'),
    }
  } else {
    // Geriye uyum: eski campaignBand + campaignBandSlot alanlarından türet
    const band = o.campaignBand !== false
    const slot = typeof o.campaignBandSlot === 'number' && o.campaignBandSlot >= 1 && o.campaignBandSlot <= 3
      ? o.campaignBandSlot : 1
    const mk = (key: AreaKey): AreaConfig => ({
      enabled: true,
      campaigns: band && Number(key) === slot,
      messages: true,
      messagesFirst: true,
      showCartCount: false,
      showFavoriteCount: false,
    })
    areas = { '1': mk('1'), '2': mk('2'), '3': mk('3') }
  }

  return {
    videoBadge: o.videoBadge !== false,
    sponsorBadge: o.sponsorBadge !== false,
    colorBadge: o.colorBadge !== false,
    galleryDots: o.galleryDots !== false,
    favoriteButton: o.favoriteButton !== false,
    collectionButton: o.collectionButton !== false,
    rating: o.rating !== false,
    discountRow: o.discountRow !== false,
    campaignPriceRow: o.campaignPriceRow !== false,
    areas,
  }
}

function ToggleRow({ label, desc, checked, locked, onChange }: {
  label: string
  desc?: string
  checked: boolean
  locked?: boolean
  onChange?: (v: boolean) => void
}) {
  return (
    <label className={`flex items-start gap-3 py-2 ${locked ? 'opacity-50' : 'cursor-pointer'}`}>
      <input
        type="checkbox"
        className="w-4 h-4 rounded accent-[var(--brand)] mt-0.5 shrink-0"
        checked={checked}
        disabled={locked}
        onChange={e => onChange?.(e.target.checked)}
      />
      <span className="min-w-0">
        <span className="text-sm font-medium block" style={{ color: 'var(--text)' }}>{label}</span>
        {desc && <span className="text-xs block mt-0.5" style={{ color: 'var(--text-s)' }}>{desc}</span>}
      </span>
    </label>
  )
}

function GroupTitle({ children }: { children: React.ReactNode }) {
  return (
    <div className="text-xs font-semibold uppercase tracking-wider pt-4 pb-1 first:pt-0"
      style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>
      {children}
    </div>
  )
}

// ── Mesaj modal formu ─────────────────────────────────────────

interface MessageForm {
  slot: number
  messageI18n: Record<string, string>
  icon: string
  color: string
  scopeType: 'all' | 'category' | 'products'
  scopeCategoryIds: string[]
  scopeProductCodes: string
  startDate: string
  endDate: string
  sortOrder: number
  isActive: boolean
}

const EMPTY_FORM: MessageForm = {
  slot: 1,
  messageI18n: {},
  icon: '',
  color: '',
  scopeType: 'all',
  scopeCategoryIds: [],
  scopeProductCodes: '',
  startDate: '',
  endDate: '',
  sortOrder: 0,
  isActive: true,
}

function formFromMessage(m: CardMessage): MessageForm {
  return {
    slot: m.slot,
    messageI18n: { ...m.messageI18n },
    icon: m.icon ?? '',
    color: m.color ?? '',
    scopeType: m.scopeType,
    scopeCategoryIds: m.scopeCategoryIds ?? [],
    scopeProductCodes: (m.scopeProductCodes ?? []).join('\n'),
    startDate: m.startDate ? m.startDate.slice(0, 10) : '',
    endDate: m.endDate ? m.endDate.slice(0, 10) : '',
    sortOrder: m.sortOrder,
    isActive: m.isActive,
  }
}

function parseProductCodes(text: string): string[] {
  return text
    .split(/[\n,;]+/)
    .map(s => s.trim())
    .filter(Boolean)
}

function fmtDate(iso: string | null): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (isNaN(d.getTime())) return iso
  return d.toLocaleDateString('tr-TR')
}

function scopeSummary(m: CardMessage): string {
  if (m.scopeType === 'category') {
    const n = m.scopeCategoryIds?.length ?? 0
    return `${n} kategori`
  }
  if (m.scopeType === 'products') {
    const n = m.scopeProductCodes?.length ?? 0
    return `${n} ürün`
  }
  return 'Tüm ürünler'
}

/** Mesaj bugün için yayın penceresinde mi? */
function isInWindow(m: CardMessage): boolean {
  const now = new Date()
  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  if (m.startDate) {
    const s = new Date(m.startDate)
    if (!isNaN(s.getTime()) && s > now) return false
  }
  if (m.endDate) {
    const e = new Date(m.endDate)
    if (!isNaN(e.getTime()) && e < todayStart) return false
  }
  return true
}

export function ProductCardPage() {
  const queryClient = useQueryClient()
  const { data: languages = [] } = useLanguages()
  const sourceLang = languages.find(l => l.isDefault)?.code ?? 'tr'

  const [activeTab, setActiveTab] = useState<'layout' | 'messages' | 'sorting'>('layout')

  const [selectedChannelId, setSelectedChannelId] = useState<string>(
    () => sessionStorage.getItem('productCard.channelId')
      ?? sessionStorage.getItem('menuPlacement.channelId') ?? ''
  )
  useEffect(() => {
    if (selectedChannelId) sessionStorage.setItem('productCard.channelId', selectedChannelId)
  }, [selectedChannelId])

  // ── Kanal listesi (firmalar × platformlar — settings dahil) ──
  const { data: firms = [], isLoading: firmsLoading } = useQuery<Firm[]>({
    queryKey: ['firms'],
    queryFn: async () => (await api.get('/core/firms')).data.data ?? [],
  })
  const platformQueries = useQueries({
    queries: firms.map(firm => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<Channel[]> => {
        const { data } = await api.get(`/core/firms/${firm.id}/platforms`)
        const firmName = getName(firm.nameI18n)
        return (data.data ?? []).map((ch: Channel) => ({ ...ch, firmId: firm.id, firmName }))
      },
      enabled: firms.length > 0,
    })),
  })
  const channels: Channel[] = platformQueries.flatMap(q => q.data ?? [])
  const chLoading = firmsLoading || platformQueries.some(q => q.isLoading)
  const channelOptions = channels.map(ch => ({
    value: ch.id,
    label: `${ch.firmName} — ${getName(ch.nameI18n)}`,
  }))
  const selectedChannel = channels.find(ch => ch.id === selectedChannelId)

  // ── Yerleşim ayarı durumu ──
  const [config, setConfig] = useState<CardConfig>(() => structuredClone(DEFAULT_CONFIG))
  const [sortOptions, setSortOptions] = useState<Record<string, boolean>>(() => sortOptionsFromSettings(undefined))
  const [dirty, setDirty] = useState(false)
  const loadedChannelId = useRef<string | null>(null)
  useEffect(() => {
    if (!selectedChannel) return
    if (loadedChannelId.current === selectedChannel.id && dirty) return // kaydedilmemiş değişikliği ezme
    setConfig(configFromSettings(selectedChannel.settings))
    setSortOptions(sortOptionsFromSettings(selectedChannel.settings))
    setDirty(false)
    loadedChannelId.current = selectedChannel.id
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedChannel?.id, selectedChannel?.settings])

  const set = (patch: Partial<CardConfig>) => {
    setConfig(prev => ({ ...prev, ...patch }))
    setDirty(true)
  }
  const setArea = (key: AreaKey, patch: Partial<AreaConfig>) => {
    setConfig(prev => ({
      ...prev,
      areas: { ...prev.areas, [key]: { ...prev.areas[key], ...patch } },
    }))
    setDirty(true)
  }

  const saveMutation = useMutation({
    mutationFn: async () => {
      await api.put(`/core/firm-platforms/${selectedChannelId}/product-card-settings`, config)
      await api.put(`/core/firm-platforms/${selectedChannelId}/product-list-settings`, { sortOptions })
    },
    onSuccess: () => {
      setDirty(false)
      if (selectedChannel) {
        queryClient.invalidateQueries({ queryKey: ['firm-platforms', selectedChannel.firmId] })
      }
    },
  })

  // ── Kart mesajları ──
  const { data: messages = [], isLoading: msgLoading } = useQuery<CardMessage[]>({
    queryKey: ['card-messages', selectedChannelId],
    queryFn: async () =>
      (await api.get(`/storefront/card-messages?firmPlatformId=${selectedChannelId}`)).data.data ?? [],
    enabled: !!selectedChannelId,
  })

  // Kanal kategorileri (modal kapsam seçimi için)
  const [modalOpen, setModalOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState<MessageForm>({ ...EMPTY_FORM })
  const [catSearch, setCatSearch] = useState('')

  const { data: categories = [], isLoading: catsLoading } = useQuery<ChannelCategory[]>({
    queryKey: ['channel-categories', selectedChannelId],
    queryFn: async () =>
      (await api.get(`/navigation/channel-categories?firmPlatformId=${selectedChannelId}`)).data.data ?? [],
    enabled: !!selectedChannelId && modalOpen && form.scopeType === 'category',
  })

  const openCreate = () => {
    setEditingId(null)
    setForm({ ...EMPTY_FORM, messageI18n: {} })
    setCatSearch('')
    messageMutation.reset()
    deleteMutation.reset()
    setModalOpen(true)
  }
  const openEdit = (m: CardMessage) => {
    setEditingId(m.id)
    setForm(formFromMessage(m))
    setCatSearch('')
    messageMutation.reset()
    deleteMutation.reset()
    setModalOpen(true)
  }

  const buildBody = () => ({
    firmPlatformId: selectedChannelId,
    slot: form.slot,
    messageI18n: form.messageI18n,
    icon: form.icon.trim() || null,
    color: form.color || null,
    scopeType: form.scopeType,
    scopeCategoryIds: form.scopeType === 'category' ? form.scopeCategoryIds : null,
    scopeProductCodes: form.scopeType === 'products' ? parseProductCodes(form.scopeProductCodes) : null,
    startDate: form.startDate || null,
    endDate: form.endDate || null,
    sortOrder: form.sortOrder,
    isActive: form.isActive,
  })

  const messageMutation = useMutation({
    mutationFn: async () => {
      if (editingId) await api.put(`/storefront/card-messages/${editingId}`, buildBody())
      else await api.post('/storefront/card-messages', buildBody())
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['card-messages', selectedChannelId] })
      setModalOpen(false)
    },
  })
  const deleteMutation = useMutation({
    mutationFn: async () => {
      if (editingId) await api.delete(`/storefront/card-messages/${editingId}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['card-messages', selectedChannelId] })
      setModalOpen(false)
    },
  })

  // ── Önizleme — kaydedilmemiş yerleşim + gerçek aktif mesajlar ──
  const previewUrl = useMemo(() => {
    const activeMessages = messages
      .filter(m => m.isActive && isInWindow(m))
      .map(m => ({
        slot: m.slot,
        text: m.messageI18n?.['tr'] ?? m.messageI18n?.[Object.keys(m.messageI18n ?? {})[0]] ?? '',
        icon: m.icon,
        color: m.color,
      }))
    const payload = { ...config, messages: activeMessages }
    return `/onizleme/urun-karti?ayar=${encodeURIComponent(JSON.stringify(payload))}`
  }, [config, messages])

  const filteredCategories = categories.filter(c => {
    if (!catSearch.trim()) return true
    const q = catSearch.toLocaleLowerCase('tr')
    return getName(c.nameI18n).toLocaleLowerCase('tr').includes(q) || c.slug.toLocaleLowerCase('tr').includes(q)
  })

  const i18nValues = useMemo(() => {
    const out: Record<string, Record<string, string>> = {}
    for (const [lang, val] of Object.entries(form.messageI18n)) out[lang] = { message: val }
    return out
  }, [form.messageI18n])

  const mutErr = (messageMutation.error ?? deleteMutation.error) as
    { response?: { data?: { error?: string } } } | null

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Ürün Kartı</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Sitedeki ürün kartı elementlerinin kanal bazlı görünürlüğü ve kart mesajları.
            Kaydedilen değişiklik sitede en geç 5 dakika içinde görünür.
          </p>
        </div>
        <div className="flex items-center gap-2">
          {dirty && (
            <span className="text-xs px-2 py-1 rounded-full"
              style={{ background: '#fef9c3', color: '#854d0e' }}>Kaydedilmemiş değişiklik</span>
          )}
          <Button onClick={() => saveMutation.mutate()} loading={saveMutation.isPending}
            disabled={!selectedChannelId || !dirty}>
            <Save size={14} /> Kaydet
          </Button>
        </div>
      </div>

      <div className="card mb-6">
        <label className="flbl mb-2">Satış Kanalı</label>
        <SearchableSelect
          value={selectedChannelId}
          onChange={(v) => { if (v) setSelectedChannelId(v) }}
          options={channelOptions}
          placeholder={chLoading ? 'Kanallar yükleniyor…' : 'Kanal seçin…'}
          hasValue={!!selectedChannelId}
        />
      </div>

      {saveMutation.isError && (
        <div className="px-4 py-3 rounded-xl mb-4 text-sm"
          style={{ background: '#fee2e2', color: '#991b1b', border: '1px solid #fecaca' }}>
          Kaydedilemedi: {(saveMutation.error as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'bilinmeyen hata'}
        </div>
      )}

      {selectedChannelId && (
        <>
          <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
            <button className={cn('stab', activeTab === 'layout' && 'active')}
              onClick={() => setActiveTab('layout')}>Yerleşim</button>
            <button className={cn('stab', activeTab === 'messages' && 'active')}
              onClick={() => setActiveTab('messages')}>Kart Mesajları</button>
            <button className={cn('stab', activeTab === 'sorting' && 'active')}
              onClick={() => setActiveTab('sorting')}>Sıralama</button>
          </div>

          <div className="flex gap-6 items-start flex-wrap">
            {activeTab === 'layout' ? (
              /* ── Sekme 1: Yerleşim ── */
              <div className="card flex-1 min-w-[320px]">
                <GroupTitle>Rozetler (görsel üstü)</GroupTitle>
                <ToggleRow label="Videolu Ürün rozeti" desc="Videosu olan ürünlerde oynatma rozeti + hover video"
                  checked={config.videoBadge} onChange={v => set({ videoBadge: v })} />
                <ToggleRow label="Sponsorlu rozeti" desc="Öne çıkarma penceresi içindeki ürünlerde"
                  checked={config.sponsorBadge} onChange={v => set({ sponsorBadge: v })} />
                <ToggleRow label="Diğer renkler rozeti" desc="Renk sayacı + farklı renk seçenekleri tooltip'i (2+ renkli ürünler)"
                  checked={config.colorBadge} onChange={v => set({ colorBadge: v })} />
                <ToggleRow label="Galeri noktaları" desc="Hover galerisinin nokta göstergeleri"
                  checked={config.galleryDots} onChange={v => set({ galleryDots: v })} />

                <GroupTitle>Değişken satırlar</GroupTitle>
                {AREA_KEYS.map(key => {
                  const area = config.areas[key]
                  return (
                    <div key={key} className="flex items-start gap-3 py-2">
                      <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)] mt-0.5 shrink-0"
                        checked={area.enabled} onChange={e => setArea(key, { enabled: e.target.checked })} />
                      <div className="min-w-0 flex-1">
                        <span className="text-sm font-medium block" style={{ color: 'var(--text)' }}>
                          {AREA_LABELS[key].title}
                        </span>
                        <span className="text-xs block mt-0.5" style={{ color: 'var(--text-s)' }}>
                          {AREA_LABELS[key].desc}
                        </span>
                        {area.enabled && (
                          <div className="mt-2 pl-3 space-y-1.5" style={{ borderLeft: '2px solid var(--border)' }}>
                            <label className="flex items-center gap-2 cursor-pointer">
                              <input type="checkbox" className="w-3.5 h-3.5 rounded accent-[var(--brand)] shrink-0"
                                checked={area.campaigns} onChange={e => setArea(key, { campaigns: e.target.checked })} />
                              <span className="text-sm" style={{ color: 'var(--text)' }}>Kampanya rozetleri</span>
                            </label>
                            <label className="flex items-center gap-2 cursor-pointer">
                              <input type="checkbox" className="w-3.5 h-3.5 rounded accent-[var(--brand)] shrink-0"
                                checked={area.messages} onChange={e => setArea(key, { messages: e.target.checked })} />
                              <span className="text-sm" style={{ color: 'var(--text)' }}>Kart mesajları</span>
                            </label>
                            {key === '3' && (
                              <>
                                <label className="flex items-center gap-2 cursor-pointer">
                                  <input type="checkbox" className="w-3.5 h-3.5 rounded accent-[var(--brand)] shrink-0"
                                    checked={area.showCartCount} onChange={e => setArea(key, { showCartCount: e.target.checked })} />
                                  <span className="text-sm" style={{ color: 'var(--text)' }}>Kaç kişinin sepetinde</span>
                                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>— canlı sayaç, 0 olan üründe gizli</span>
                                </label>
                                <label className="flex items-center gap-2 cursor-pointer">
                                  <input type="checkbox" className="w-3.5 h-3.5 rounded accent-[var(--brand)] shrink-0"
                                    checked={area.showFavoriteCount} onChange={e => setArea(key, { showFavoriteCount: e.target.checked })} />
                                  <span className="text-sm" style={{ color: 'var(--text)' }}>Kaç kişinin favorisi</span>
                                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>— canlı sayaç, 0 olan üründe gizli</span>
                                </label>
                              </>
                            )}
                            {area.campaigns && area.messages && (
                              <div className="pt-1">
                                <label className="flbl">Öncelik</label>
                                <select className="sel text-sm" value={area.messagesFirst ? 'messages' : 'campaigns'}
                                  onChange={e => setArea(key, { messagesFirst: e.target.value === 'messages' })}>
                                  <option value="messages">Mesajlar önce</option>
                                  <option value="campaigns">Kampanyalar önce</option>
                                </select>
                              </div>
                            )}
                          </div>
                        )}
                      </div>
                    </div>
                  )
                })}

                <GroupTitle>Puan &amp; Fiyat</GroupTitle>
                <ToggleRow label="Puan + yorum sayısı" desc="Onaylı yorum ortalaması ve yıldızlar"
                  checked={config.rating} onChange={v => set({ rating: v })} />
                <ToggleRow label="İndirim satırı" desc="-%X rozeti + üstü çizili eski fiyat"
                  checked={config.discountRow} onChange={v => set({ discountRow: v })} />
                <ToggleRow label="Kampanyalı fiyat satırı" desc="Ürün bazlı kampanya fiyatı (satış fiyatının altındaysa)"
                  checked={config.campaignPriceRow} onChange={v => set({ campaignPriceRow: v })} />

                <GroupTitle>Eylem butonları</GroupTitle>
                <ToggleRow label="Favori butonu" checked={config.favoriteButton}
                  onChange={v => set({ favoriteButton: v })} />
                <ToggleRow label="Koleksiyon butonu" checked={config.collectionButton}
                  onChange={v => set({ collectionButton: v })} />

                <GroupTitle>Sabit çekirdek</GroupTitle>
                <ToggleRow label="Görsel, ürün adı, fiyat, kart linki" locked checked
                  desc="Kartın temelidir — kapatılamaz" />
              </div>
            ) : activeTab === 'messages' ? (
              /* ── Sekme 2: Kart Mesajları ── */
              <div className="card flex-1 min-w-[320px] overflow-hidden">
                <div className="flex items-center justify-between mb-3">
                  <div className="text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>
                    Kart Mesajları
                  </div>
                  <Button size="sm" onClick={openCreate}>
                    <Plus size={14} /> Yeni Mesaj
                  </Button>
                </div>
                {msgLoading ? (
                  <div className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</div>
                ) : messages.length === 0 ? (
                  <div className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                    Bu kanalda tanımlı kart mesajı yok. "Yeni Mesaj" ile ekleyin.
                  </div>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="text-left text-xs uppercase tracking-wider"
                          style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>
                          <th className="py-2 pr-3 font-semibold">Mesaj</th>
                          <th className="py-2 pr-3 font-semibold">Alan</th>
                          <th className="py-2 pr-3 font-semibold">İkon</th>
                          <th className="py-2 pr-3 font-semibold">Renk</th>
                          <th className="py-2 pr-3 font-semibold">Kapsam</th>
                          <th className="py-2 pr-3 font-semibold">Tarih</th>
                          <th className="py-2 pr-3 font-semibold">Sıra</th>
                          <th className="py-2 font-semibold">Aktif</th>
                        </tr>
                      </thead>
                      <tbody>
                        {messages.map(m => {
                          const colorOpt = COLOR_OPTIONS.find(c => c.value === m.color)
                          return (
                            <tr key={m.id} onClick={() => openEdit(m)}
                              className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                              style={{ borderBottom: '1px solid var(--border)', color: 'var(--text)' }}>
                              <td className="py-2.5 pr-3 font-medium">
                                {getName(m.messageI18n) || <span style={{ color: 'var(--text-s)' }}>—</span>}
                              </td>
                              <td className="py-2.5 pr-3 whitespace-nowrap">Alan {m.slot}</td>
                              <td className="py-2.5 pr-3">
                                {m.icon ? <code className="text-xs">{m.icon}</code> : '—'}
                              </td>
                              <td className="py-2.5 pr-3 whitespace-nowrap">
                                {colorOpt ? (
                                  <span className="inline-flex items-center gap-1.5">
                                    <span className="w-2.5 h-2.5 rounded-full inline-block"
                                      style={{ background: colorOpt.dot }} />
                                    {colorOpt.label}
                                  </span>
                                ) : 'Varsayılan'}
                              </td>
                              <td className="py-2.5 pr-3 whitespace-nowrap">{scopeSummary(m)}</td>
                              <td className="py-2.5 pr-3 whitespace-nowrap text-xs" style={{ color: 'var(--text-s)' }}>
                                {m.startDate || m.endDate
                                  ? `${fmtDate(m.startDate) || '…'} – ${fmtDate(m.endDate) || '…'}`
                                  : 'Süresiz'}
                              </td>
                              <td className="py-2.5 pr-3">{m.sortOrder}</td>
                              <td className="py-2.5">
                                <span className="text-xs px-2 py-0.5 rounded-full"
                                  style={m.isActive
                                    ? { background: '#dcfce7', color: '#166534' }
                                    : { background: 'var(--surface2)', color: 'var(--text-s)' }}>
                                  {m.isActive ? 'Aktif' : 'Pasif'}
                                </span>
                              </td>
                            </tr>
                          )
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            ) : (
              /* ── Sekme 3: Sıralama seçenekleri ── */
              <div className="card flex-1 min-w-[320px]">
                <GroupTitle>Sitede Görünecek Sıralama Seçenekleri</GroupTitle>
                <p className="text-xs mb-2" style={{ color: 'var(--text-s)' }}>
                  Kapatılan seçenek sitedeki sıralama menüsünde listelenmez. Sayaç tabanlı seçenekler
                  (puan, yorum, favori, sepet, görüntülenme, satış) yaklaşık 10 dakikada bir tazelenen
                  canlı verilerle sıralar.
                </p>
                {SORT_OPTIONS.map(opt => (
                  <ToggleRow key={opt.code} label={opt.label}
                    desc={opt.locked ? 'Varsayılan seçenek — kapatılamaz' : undefined}
                    locked={opt.locked}
                    checked={sortOptions[opt.code] !== false}
                    onChange={v => { setSortOptions(prev => ({ ...prev, [opt.code]: v })); setDirty(true) }} />
                ))}
              </div>
            )}

            {/* Canlı önizleme — gerçek site markup'ı */}
            <div className="card w-[320px] shrink-0">
              <div className="text-xs font-semibold uppercase tracking-wider pb-2 mb-3"
                style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>
                Canlı Önizleme
              </div>
              <iframe
                src={previewUrl}
                title="Ürün kartı önizleme"
                className="w-full"
                style={{ height: 560, border: '1px solid var(--border)', borderRadius: 12, background: '#fff' }}
              />
              <p className="text-xs mt-2" style={{ color: 'var(--text-s)' }}>
                Önizleme, sitenin gerçek kart şablonuyla demo veriden üretilir; kaydetmeden
                yapılan yerleşim değişiklikleri ve aktif kart mesajları anında yansır.
              </p>
            </div>
          </div>
        </>
      )}

      {/* ── Mesaj düzenleme modalı ── */}
      <Modal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        title={editingId ? 'Kart Mesajını Düzenle' : 'Yeni Kart Mesajı'}
        size="lg"
        footer={
          <>
            {editingId && (
              <Button variant="danger" onClick={() => deleteMutation.mutate()}
                loading={deleteMutation.isPending} className="mr-auto">
                Sil
              </Button>
            )}
            <Button variant="secondary" onClick={() => setModalOpen(false)}>Vazgeç</Button>
            <Button onClick={() => messageMutation.mutate()} loading={messageMutation.isPending}>
              Kaydet
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          {mutErr && (
            <div className="px-4 py-3 rounded-xl text-sm"
              style={{ background: '#fee2e2', color: '#991b1b', border: '1px solid #fecaca' }}>
              İşlem başarısız: {mutErr.response?.data?.error ?? 'bilinmeyen hata'}
            </div>
          )}

          {/* Mesaj — çok dilli */}
          <div>
            <label className="flbl mb-1">Mesaj</label>
            {languages.length > 0 ? (
              <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
                <I18nField
                  sourceLang={sourceLang}
                  languages={languages}
                  fields={[{ key: 'message', labels: { tr: 'Mesaj', en: 'Message' }, required: true }]}
                  values={i18nValues}
                  onChange={(lang, _key, value) =>
                    setForm(f => ({ ...f, messageI18n: { ...f.messageI18n, [lang]: value } }))}
                />
              </div>
            ) : (
              <input className="inp" value={form.messageI18n['tr'] ?? ''}
                onChange={e => setForm(f => ({ ...f, messageI18n: { ...f.messageI18n, tr: e.target.value } }))} />
            )}
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* Alan */}
            <div>
              <label className="flbl">Alan</label>
              <select className="sel" value={form.slot}
                onChange={e => setForm(f => ({ ...f, slot: Number(e.target.value) }))}>
                {SLOT_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>

            {/* Renk */}
            <div>
              <label className="flbl">Renk</label>
              <select className="sel" value={form.color}
                onChange={e => setForm(f => ({ ...f, color: e.target.value }))}>
                <option value="">Varsayılan</option>
                {COLOR_OPTIONS.map(c => <option key={c.value} value={c.value}>{c.label}</option>)}
              </select>
            </div>
          </div>

          {/* İkon */}
          <div>
            <label className="flbl">İkon (Font Awesome sınıfı)</label>
            <input className="inp" placeholder="fa-truck" value={form.icon}
              onChange={e => setForm(f => ({ ...f, icon: e.target.value }))} />
            <div className="flex flex-wrap gap-1.5 mt-2">
              {ICON_CHIPS.map(ic => (
                <button key={ic} type="button"
                  onClick={() => setForm(f => ({ ...f, icon: ic }))}
                  className="text-xs px-2 py-1 rounded-full transition-colors"
                  style={form.icon === ic
                    ? { background: 'var(--brand)', color: '#fff', border: '1px solid var(--brand)' }
                    : { background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
                  {ic}
                </button>
              ))}
            </div>
          </div>

          {/* Kapsam */}
          <div>
            <label className="flbl">Kapsam</label>
            <select className="sel" value={form.scopeType}
              onChange={e => setForm(f => ({ ...f, scopeType: e.target.value as MessageForm['scopeType'] }))}>
              <option value="all">Tüm ürünler</option>
              <option value="category">Kanal kategorileri</option>
              <option value="products">Ürün kodları</option>
            </select>

            {form.scopeType === 'category' && (
              <div className="mt-2 rounded-xl p-3" style={{ border: '1px solid var(--border)' }}>
                <input className="inp mb-2" placeholder="Kategori ara…" value={catSearch}
                  onChange={e => setCatSearch(e.target.value)} />
                {catsLoading ? (
                  <div className="py-3 text-center text-xs" style={{ color: 'var(--text-s)' }}>
                    Kategoriler yükleniyor…
                  </div>
                ) : (
                  <div className="max-h-48 overflow-y-auto thin-scroll space-y-0.5">
                    {filteredCategories.length === 0 && (
                      <div className="py-2 text-xs text-center" style={{ color: 'var(--text-s)' }}>
                        Kategori bulunamadı
                      </div>
                    )}
                    {filteredCategories.map(c => (
                      <label key={c.id} className="flex items-center gap-2 py-1 px-1 rounded cursor-pointer hover:bg-[var(--surface2)]">
                        <input type="checkbox" className="w-3.5 h-3.5 rounded accent-[var(--brand)] shrink-0"
                          checked={form.scopeCategoryIds.includes(c.id)}
                          onChange={e => setForm(f => ({
                            ...f,
                            scopeCategoryIds: e.target.checked
                              ? [...f.scopeCategoryIds, c.id]
                              : f.scopeCategoryIds.filter(id => id !== c.id),
                          }))} />
                        <span className="text-sm" style={{ color: 'var(--text)' }}>{getName(c.nameI18n)}</span>
                        <span className="text-xs" style={{ color: 'var(--text-s)' }}>/{c.slug}</span>
                      </label>
                    ))}
                  </div>
                )}
                <div className="text-xs mt-2" style={{ color: 'var(--text-s)' }}>
                  {form.scopeCategoryIds.length} kategori seçili
                </div>
              </div>
            )}

            {form.scopeType === 'products' && (
              <div className="mt-2">
                <textarea className="ta" rows={4}
                  placeholder={'Ürün kodları — satır veya virgülle ayırın\nörn. MIS-001, MIS-002'}
                  value={form.scopeProductCodes}
                  onChange={e => setForm(f => ({ ...f, scopeProductCodes: e.target.value }))} />
                <div className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
                  {parseProductCodes(form.scopeProductCodes).length} ürün kodu
                </div>
              </div>
            )}
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="flbl">Başlangıç tarihi</label>
              <input type="date" className="inp" value={form.startDate}
                onChange={e => setForm(f => ({ ...f, startDate: e.target.value }))} />
              <span className="text-xs block mt-1" style={{ color: 'var(--text-s)' }}>Boş = hemen başlar</span>
            </div>
            <div>
              <label className="flbl">Bitiş tarihi</label>
              <input type="date" className="inp" value={form.endDate}
                onChange={e => setForm(f => ({ ...f, endDate: e.target.value }))} />
              <span className="text-xs block mt-1" style={{ color: 'var(--text-s)' }}>Boş = süresiz</span>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 items-end">
            <div>
              <label className="flbl">Sıra</label>
              <input type="number" className="inp" value={form.sortOrder}
                onChange={e => setForm(f => ({ ...f, sortOrder: Number(e.target.value) || 0 }))} />
            </div>
            <label className="flex items-center gap-2 cursor-pointer pb-2">
              <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)] shrink-0"
                checked={form.isActive}
                onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))} />
              <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>Aktif</span>
            </label>
          </div>
        </div>
      </Modal>
    </div>
  )
}
