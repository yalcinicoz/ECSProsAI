import { useState, useEffect, useMemo, useRef } from 'react'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import { Save } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { SearchableSelect } from '@/components/ui/SearchableSelect'

/**
 * Ürün Kartı (F1, 2026-08-09) — sitedeki ürün kartı elementlerinin kanal bazlı aç/kapat
 * yönetimi. Ayarlar FirmPlatform.Settings."productCard" anahtarına scoped endpoint'le
 * yazılır (PUT /core/firm-platforms/{id}/product-card-settings — diğer ayarlara dokunmaz).
 * Sağdaki önizleme sitenin gerçek SSR markup'ıdır (/onizleme/urun-karti iframe'i) —
 * kaydedilmemiş ayar query ile geçer, anında görünür. F2'de duyuru kural motoru
 * (değişken satır 2/3 mesajları) bu ekrana eklenecek.
 */

interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel {
  id: string; nameI18n: Record<string, string>; code: string
  firmId: string; firmName: string
  settings?: Record<string, unknown>
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
  campaignBand: boolean
  campaignBandSlot: number
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
  campaignBand: true,
  campaignBandSlot: 1,
}

const SLOT_OPTIONS = [
  { value: 1, label: 'Slot 1 — görsel altı bant' },
  { value: 2, label: 'Slot 2 — ürün adı altı' },
  { value: 3, label: 'Slot 3 — puan satırı altı' },
]

function getName(i18n: Record<string, string> | null | undefined): string {
  if (!i18n) return ''
  return i18n['tr'] ?? i18n[Object.keys(i18n)[0]] ?? ''
}

function configFromSettings(settings: Record<string, unknown> | undefined): CardConfig {
  const pc = settings?.['productCard']
  if (!pc || typeof pc !== 'object') return { ...DEFAULT_CONFIG }
  const o = pc as Record<string, unknown>
  const slot = typeof o.campaignBandSlot === 'number' && o.campaignBandSlot >= 1 && o.campaignBandSlot <= 3
    ? o.campaignBandSlot : 1
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
    campaignBand: o.campaignBand !== false,
    campaignBandSlot: slot,
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

export function ProductCardPage() {
  const queryClient = useQueryClient()

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

  // ── Ayar durumu ──
  const [config, setConfig] = useState<CardConfig>({ ...DEFAULT_CONFIG })
  const [dirty, setDirty] = useState(false)
  const loadedChannelId = useRef<string | null>(null)
  useEffect(() => {
    if (!selectedChannel) return
    if (loadedChannelId.current === selectedChannel.id && dirty) return // kaydedilmemiş değişikliği ezme
    setConfig(configFromSettings(selectedChannel.settings))
    setDirty(false)
    loadedChannelId.current = selectedChannel.id
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedChannel?.id, selectedChannel?.settings])

  const set = (patch: Partial<CardConfig>) => {
    setConfig(prev => ({ ...prev, ...patch }))
    setDirty(true)
  }

  const saveMutation = useMutation({
    mutationFn: async () => {
      await api.put(`/core/firm-platforms/${selectedChannelId}/product-card-settings`, config)
    },
    onSuccess: () => {
      setDirty(false)
      if (selectedChannel) {
        queryClient.invalidateQueries({ queryKey: ['firm-platforms', selectedChannel.firmId] })
      }
    },
  })

  // Önizleme sitenin gerçek SSR kartı — kaydedilmemiş ayar query ile geçer.
  // Prod'da admin origin'inde "/" API'ye proxy'lidir; dev için vite proxy tanımlı.
  const previewUrl = useMemo(
    () => `/onizleme/urun-karti?ayar=${encodeURIComponent(JSON.stringify(config))}`,
    [config]
  )

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Ürün Kartı</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Sitedeki ürün kartı elementlerinin kanal bazlı görünürlüğü.
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
        <div className="flex gap-6 items-start flex-wrap">
          {/* Element listesi */}
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
            <div className="flex items-start gap-3 py-2">
              <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)] mt-0.5 shrink-0"
                checked={config.campaignBand} onChange={e => set({ campaignBand: e.target.checked })} />
              <div className="min-w-0 flex-1">
                <span className="text-sm font-medium block" style={{ color: 'var(--text)' }}>Kampanya bandı</span>
                <span className="text-xs block mt-0.5 mb-2" style={{ color: 'var(--text-s)' }}>
                  Ürünü kapsayan kampanyaların dönüşümlü rozeti ("Kargo Bedava" vb.)
                </span>
                {config.campaignBand && (
                  <select className="sel text-sm" value={config.campaignBandSlot}
                    onChange={e => set({ campaignBandSlot: Number(e.target.value) })}>
                    {SLOT_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                  </select>
                )}
              </div>
            </div>
            <ToggleRow label="Teslimat / kargo mesajları" locked checked={false}
              desc="Veri kaynağı yok — F2'de duyuru kural motoruyla açılacak" />

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
              yapılan değişiklikler de anında yansır.
            </p>
          </div>
        </div>
      )}
    </div>
  )
}
