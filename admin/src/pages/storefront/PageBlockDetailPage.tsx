import { useEffect, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Input } from '@/components/ui/Input'
import { Textarea } from '@/components/ui/Textarea'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'

// G6: blok detayı — alanlar + config JSON (tip bazlı örnek iskelet yüklenebilir) +
// öğe editörü (tam liste replace — SaveNavNodes deseni). Kural JSON'ları G-M2 kural
// motoru içindir; M1'de kurallar canlıda değerlendirilmez.

interface CatalogData {
  placements: { code: string; displayName: string }[]
  blockTypes: {
    code: string; displayName: string; ruleLevel: string; supportsItems: boolean
    templates: string[]; requiresProductSource: boolean; requiresCollectionSource: boolean
  }[]
  carouselThemes: string[]
}
interface ItemDto {
  id?: string
  titleI18n: Record<string, string>
  subtitleI18n: Record<string, string> | null
  imageUrl: string | null
  mobileImageUrl: string | null
  videoUrl: string | null
  linkUrl: string | null
  openInNewTab: boolean
  buttonTextI18n: Record<string, string> | null
  badgeLabel: string | null
  sortOrder: number
  isActive: boolean
  startAt: string | null
  endAt: string | null
  priority: number
  ruleJson: string | null
  configJson: string | null
}
interface BlockDetail {
  id: string; placement: string; blockType: string; template: string | null
  titleI18n: Record<string, string>; subtitleI18n: Record<string, string> | null
  sortOrder: number; isActive: boolean; startAt: string | null; endAt: string | null
  priority: number; ruleJson: string | null; configJson: string | null; items: ItemDto[]
}

const ORNEK_CONFIG: Record<string, object> = {
  carousel: { productSource: { source: 'new-arrivals', limit: 12, sort: 'newest', inStockOnly: true, discountedOnly: false, tags: [] }, tema: 'varsayilan', seeAllUrl: '/urun-listesi' },
  infinity: { productSource: { source: 'category', categoryId: '00000000-0000-0000-0000-000000000000', limit: 24 }, seeAllUrl: '/urun-listesi' },
  collection: { collectionSource: { limit: 10, sort: 'popular' } },
  banner: { mobileCarousel: true },
  categories: { gorunum: 'kapsul', mobileCarousel: true },
  'kategori-cok-satanlar': { productSource: { source: 'category', categoryId: '00000000-0000-0000-0000-000000000000', limit: 4, sort: 'best-sellers' }, seeAllUrl: '/urun-listesi' },
}
const KAYNAKLAR = ['new-arrivals', 'best-sellers', 'campaign', 'category', 'brand', 'manual', 'recently-viewed', 'favorites']
// 2026-07-22: config artık yapılandırılmış formla düzenlenir (ham JSON "Gelişmiş"te)
const KAYNAK_ETIKET: Record<string, string> = {
  'new-arrivals': 'Yeni Gelenler', 'best-sellers': 'Çok Satanlar', campaign: 'Kampanyalı Ürünler',
  category: 'Kategori', brand: 'Marka', manual: 'Manuel Ürün Listesi',
  'recently-viewed': 'Son Gezilenler (üyeye özel)', favorites: 'Favoriler (üyeye özel)',
}
const SIRALAMA_ETIKET: Record<string, string> = {
  '': 'Varsayılan', newest: 'En Yeni', price_asc: 'Fiyat Artan', price_desc: 'Fiyat Azalan',
}
interface KanalKategori { id: string; nameI18n: Record<string, string>; slug: string }

const bosOge = (sira: number): ItemDto => ({
  titleI18n: { tr: '' }, subtitleI18n: null, imageUrl: null, mobileImageUrl: null,
  videoUrl: null, linkUrl: null, openInNewTab: false, buttonTextI18n: null, badgeLabel: null,
  sortOrder: sira, isActive: true, startAt: null, endAt: null, priority: 0, ruleJson: null, configJson: null,
})

export function PageBlockDetailPage() {
  const { id } = useParams<{ id: string }>()
  const [search] = useSearchParams()
  const platformId = search.get('platformId') ?? ''
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { data: kanalKategorileri = [] } = useQuery<KanalKategori[]>({
    queryKey: ['channel-categories', platformId],
    queryFn: async () =>
      (await api.get('/navigation/channel-categories', { params: { firmPlatformId: platformId, activeOnly: true } })).data.data ?? [],
    enabled: !!platformId,
  })

  const { data: catalog } = useQuery<CatalogData>({
    queryKey: ['pages-catalog'],
    queryFn: async () => (await api.get('/pages/catalog')).data.data,
  })

  const { data: blok, isLoading } = useQuery<BlockDetail>({
    queryKey: ['page-block', id],
    queryFn: async () =>
      (await api.get(`/pages/blocks/${id}`, { params: { firmPlatformId: platformId } })).data.data,
    enabled: !!id && !!platformId,
  })

  const [form, setForm] = useState<BlockDetail | null>(null)
  const [hataMesaji, setHataMesaji] = useState('')
  const [ogeler, setOgeler] = useState<ItemDto[]>([])
  const [ogeModal, setOgeModal] = useState<{ index: number; oge: ItemDto } | null>(null)

  useEffect(() => {
    if (blok) { setForm({ ...blok }); setOgeler(blok.items.map((i) => ({ ...i }))) }
  }, [blok])

  const tipDef = catalog?.blockTypes.find((t) => t.code === form?.blockType)

  const kaydet = useMutation({
    mutationFn: async () => {
      if (!form) return
      if (form.configJson) JSON.parse(form.configJson) // erken doğrulama
      if (form.ruleJson) JSON.parse(form.ruleJson)
      await api.put(`/pages/blocks/${id}`, {
        firmPlatformId: platformId, placement: form.placement, blockType: form.blockType,
        template: form.template || null, titleI18n: form.titleI18n, subtitleI18n: form.subtitleI18n,
        sortOrder: form.sortOrder, isActive: form.isActive,
        startAt: form.startAt || null, endAt: form.endAt || null, priority: form.priority,
        ruleJson: form.ruleJson || null, configJson: form.configJson || null,
      })
      if (tipDef?.supportsItems)
        await api.put(`/pages/blocks/${id}/items`, { firmPlatformId: platformId, items: ogeler })
    },
    onSuccess: () => { setHataMesaji(''); queryClient.invalidateQueries({ queryKey: ['page-block', id] }) },
    onError: (e: unknown) => {
      const err = e as { response?: { data?: { error?: string } }; message?: string }
      setHataMesaji(err.response?.data?.error ?? err.message ?? 'Kaydedilemedi.')
    },
  })

  if (isLoading || !form) return <PageSpinner />

  const alan = <K extends keyof BlockDetail>(k: K, v: BlockDetail[K]) => setForm({ ...form, [k]: v })
  const ornekYukle = () => {
    const ornek = ORNEK_CONFIG[form.blockType]
    if (ornek) alan('configJson', JSON.stringify(ornek, null, 2))
  }

  // 2026-07-22: yapılandırılmış config formu — tek doğruluk kaynağı configJson metni;
  // kontroller onu okur/yamalar (Gelişmiş'teki ham JSON ile hep senkron).
  /* eslint-disable @typescript-eslint/no-explicit-any */
  const cfg: any = (() => { try { return JSON.parse(form.configJson || '{}') } catch { return null } })()
  const cfgGecerli = cfg !== null
  const cfgYaz = (next: any) =>
    alan('configJson', Object.keys(next).length ? JSON.stringify(next, null, 2) : null)
  const bosMu = (v: any) =>
    v === undefined || v === null || v === '' || v === false || (Array.isArray(v) && v.length === 0)
  const cfgAlan = (k: string, v: any) => {
    const next = { ...(cfg ?? {}) }
    if (bosMu(v)) delete next[k]; else next[k] = v
    cfgYaz(next)
  }
  const kaynakAlan = (kok: 'productSource' | 'collectionSource', k: string, v: any) => {
    const next = { ...(cfg ?? {}) }
    const kaynak = { ...(next[kok] ?? {}) }
    if (bosMu(v)) delete kaynak[k]; else kaynak[k] = v
    next[kok] = kaynak
    cfgYaz(next)
  }
  const ps: any = cfg?.productSource ?? null
  const cs: any = cfg?.collectionSource ?? null
  const listeMetni = (dizi: any) => (Array.isArray(dizi) ? dizi.join(', ') : '')
  const metindenListe = (metin: string) =>
    metin.split(/[\n,]/).map((x) => x.trim()).filter(Boolean)
  /* eslint-enable @typescript-eslint/no-explicit-any */

  const ogeKaydet = () => {
    if (!ogeModal) return
    const yeni = [...ogeler]
    if (ogeModal.index === -1) yeni.push(ogeModal.oge)
    else yeni[ogeModal.index] = ogeModal.oge
    setOgeler(yeni)
    setOgeModal(null)
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <button className="text-sm text-[var(--text-m)] hover:text-[var(--text)]" onClick={() => navigate('/storefront/pages')}>
            ← Vitrin Yönetimi
          </button>
          <h1 className="text-xl font-semibold">
            {form.titleI18n?.tr || 'Blok'}
            <span className="ml-2 text-sm font-normal text-[var(--text-m)]">{tipDef?.displayName ?? form.blockType}{form.template ? ` · ${form.template}` : ''}</span>
          </h1>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant={form.isActive ? 'success' : 'neutral'}>{form.isActive ? 'Aktif' : 'Pasif'}</Badge>
          <Button onClick={() => kaydet.mutate()} disabled={kaydet.isPending}>Kaydet</Button>
        </div>
      </div>

      {hataMesaji && (
        <div className="rounded-lg border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-700">{hataMesaji}</div>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="space-y-3 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
          <h2 className="text-sm font-semibold">Blok Alanları</h2>
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Başlık (TR)</label>
              <Input value={form.titleI18n?.tr ?? ''} onChange={(e) => alan('titleI18n', { ...form.titleI18n, tr: e.target.value })} />
            </div>
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Alt başlık (TR)</label>
              <Input value={form.subtitleI18n?.tr ?? ''} onChange={(e) => alan('subtitleI18n', { ...(form.subtitleI18n ?? {}), tr: e.target.value })} />
            </div>
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Yerleşim</label>
              <SearchableSelect
                value={form.placement}
                onChange={(v) => v && alan('placement', v)}
                options={(catalog?.placements ?? []).map((p) => ({ value: p.code, label: p.displayName }))}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Şablon</label>
              {tipDef && tipDef.templates.length > 0 ? (
                <SearchableSelect
                  value={form.template}
                  onChange={(v) => alan('template', v)}
                  options={tipDef.templates.map((t) => ({ value: t, label: t }))}
                />
              ) : (
                <p className="py-2 text-sm text-[var(--text-s)]">Bu tipte şablon seçilmez.</p>
              )}
            </div>
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Sıra</label>
              <Input type="number" value={form.sortOrder} onChange={(e) => alan('sortOrder', Number(e.target.value))} />
            </div>
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Öncelik</label>
              <Input type="number" value={form.priority} onChange={(e) => alan('priority', Number(e.target.value))} />
            </div>
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Başlangıç</label>
              <Input type="datetime-local" value={form.startAt?.slice(0, 16) ?? ''} onChange={(e) => alan('startAt', e.target.value ? e.target.value + ':00Z' : null)} />
            </div>
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Bitiş</label>
              <Input type="datetime-local" value={form.endAt?.slice(0, 16) ?? ''} onChange={(e) => alan('endAt', e.target.value ? e.target.value + ':00Z' : null)} />
            </div>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={form.isActive} onChange={(e) => alan('isActive', e.target.checked)} />
            Aktif (pasif blok hiçbir koşulda yayınlanmaz)
          </label>
        </div>

        <div className="space-y-4 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
          <h2 className="text-sm font-semibold">Blok Ayarları</h2>
          {!cfgGecerli && (
            <div className="rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-800">
              Config JSON'ı geçersiz — aşağıdaki "Gelişmiş (ham JSON)" bölümünden düzeltin; form o zaman aktifleşir.
            </div>
          )}

          {cfgGecerli && (tipDef?.requiresProductSource || ps) && (
            <div className="space-y-3 rounded-lg border border-[var(--border)] p-3">
              <h3 className="text-sm font-semibold">Ürün Kaynağı {tipDef?.requiresProductSource && <span className="text-xs font-normal text-[var(--text-s)]">(bu tipte zorunlu)</span>}</h3>
              <div className="grid gap-3 md:grid-cols-3">
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Kaynak</label>
                  <SearchableSelect
                    value={ps?.source ?? null}
                    onChange={(v) => kaynakAlan('productSource', 'source', v)}
                    options={KAYNAKLAR.map((k) => ({ value: k, label: KAYNAK_ETIKET[k] ?? k }))}
                    placeholder="Kaynak seç"
                  />
                </div>
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Ürün adedi (limit)</label>
                  <Input type="number" min={1} max={48} value={ps?.limit ?? ''} onChange={(e) => kaynakAlan('productSource', 'limit', e.target.value ? Number(e.target.value) : null)} placeholder="12" />
                </div>
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Sıralama</label>
                  <SearchableSelect
                    value={ps?.sort ?? ''}
                    onChange={(v) => kaynakAlan('productSource', 'sort', v || null)}
                    options={Object.entries(SIRALAMA_ETIKET).map(([v, l]) => ({ value: v, label: l }))}
                    placeholder="Varsayılan"
                  />
                </div>
              </div>

              {ps?.source === 'category' && (
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Kanal Kategorisi</label>
                  <SearchableSelect
                    value={ps?.categoryId ?? null}
                    onChange={(v) => kaynakAlan('productSource', 'categoryId', v)}
                    options={kanalKategorileri.map((k) => ({ value: k.id, label: `${k.nameI18n?.tr ?? k.slug} (/${k.slug})` }))}
                    placeholder="Kategori seç"
                  />
                </div>
              )}
              {ps?.source === 'best-sellers' && (
                <div className="md:w-56">
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Satış penceresi (gün)</label>
                  <Input type="number" min={1} value={ps?.days ?? ''} onChange={(e) => kaynakAlan('productSource', 'days', e.target.value ? Number(e.target.value) : null)} placeholder="90" />
                </div>
              )}
              {ps?.source === 'manual' && (
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Ürün kodları (virgül ya da satırla ayır — sıra korunur)</label>
                  <Textarea rows={2} value={listeMetni(ps?.productCodes)} onChange={(e) => kaynakAlan('productSource', 'productCodes', metindenListe(e.target.value))} placeholder="P-000123, P-000456" />
                </div>
              )}
              {ps?.source === 'brand' && (
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Marka değer Id (guid)</label>
                  <Input value={ps?.brandValueId ?? ''} onChange={(e) => kaynakAlan('productSource', 'brandValueId', e.target.value || null)} placeholder="marka attribute value id" />
                </div>
              )}

              <div className="grid gap-3 md:grid-cols-3">
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Min fiyat</label>
                  <Input type="number" value={ps?.priceMin ?? ''} onChange={(e) => kaynakAlan('productSource', 'priceMin', e.target.value ? Number(e.target.value) : null)} placeholder="—" />
                </div>
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Max fiyat</label>
                  <Input type="number" value={ps?.priceMax ?? ''} onChange={(e) => kaynakAlan('productSource', 'priceMax', e.target.value ? Number(e.target.value) : null)} placeholder="—" />
                </div>
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Etiketler (en az biri eşleşir)</label>
                  <Input value={listeMetni(ps?.tags)} onChange={(e) => kaynakAlan('productSource', 'tags', metindenListe(e.target.value))} placeholder="yaz, indirim" disabled={ps?.source === 'category'} />
                </div>
              </div>

              <div className="flex flex-wrap gap-5 text-sm">
                <label className="flex items-center gap-2">
                  <input type="checkbox" checked={!!ps?.inStockOnly} disabled={ps?.source === 'category'} onChange={(e) => kaynakAlan('productSource', 'inStockOnly', e.target.checked)} />
                  Yalnız stokta olanlar
                </label>
                <label className="flex items-center gap-2">
                  <input type="checkbox" checked={!!ps?.discountedOnly} disabled={ps?.source === 'category'} onChange={(e) => kaynakAlan('productSource', 'discountedOnly', e.target.checked)} />
                  Yalnız indirimliler
                </label>
              </div>
              {ps?.source === 'category' && (
                <p className="text-xs text-[var(--text-s)]">Kategori kaynağında etiket/stok/indirim bayrakları desteklenmez — kanal sorgusu kendi stok kuralını uygular.</p>
              )}
              {(ps?.source === 'recently-viewed' || ps?.source === 'favorites') && (
                <p className="text-xs text-[var(--text-s)]">Üyeye özel kaynak: içerik ziyaretçinin kendi verisiyle dolar; misafirde blok görünmez.</p>
              )}
            </div>
          )}

          {cfgGecerli && (tipDef?.requiresCollectionSource || cs) && (
            <div className="space-y-3 rounded-lg border border-[var(--border)] p-3">
              <h3 className="text-sm font-semibold">Koleksiyon Kaynağı</h3>
              <div className="grid gap-3 md:grid-cols-3">
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Adet (limit)</label>
                  <Input type="number" min={1} max={50} value={cs?.limit ?? ''} onChange={(e) => kaynakAlan('collectionSource', 'limit', e.target.value ? Number(e.target.value) : null)} placeholder="10" />
                </div>
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Sıralama</label>
                  <SearchableSelect
                    value={cs?.sort ?? ''}
                    onChange={(v) => kaynakAlan('collectionSource', 'sort', v || null)}
                    options={[{ value: '', label: 'En Yeni' }, { value: 'popular', label: 'Popüler (görüntülenme)' }]}
                    placeholder="En Yeni"
                  />
                </div>
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">Manuel seçim (ShareCode, virgüllü)</label>
                  <Input value={listeMetni(cs?.shareCodes)} onChange={(e) => kaynakAlan('collectionSource', 'shareCodes', metindenListe(e.target.value))} placeholder="boş = otomatik" />
                </div>
              </div>
              <p className="text-xs text-[var(--text-s)]">Yalnız onaylı + herkese açık koleksiyonlar listelenir.</p>
            </div>
          )}

          {cfgGecerli && (
            <div className="space-y-3 rounded-lg border border-[var(--border)] p-3">
              <h3 className="text-sm font-semibold">Görünüm</h3>
              <div className="grid gap-3 md:grid-cols-3">
                {(catalog?.carouselThemes?.length ?? 0) > 0 && form.blockType.includes('carousel') && (
                  <div>
                    <label className="mb-1 block text-sm text-[var(--text-m)]">Tema</label>
                    <SearchableSelect
                      value={cfg?.tema ?? null}
                      onChange={(v) => cfgAlan('tema', v)}
                      options={(catalog?.carouselThemes ?? []).map((t) => ({ value: t, label: t }))}
                      placeholder="varsayilan"
                    />
                  </div>
                )}
                <div>
                  <label className="mb-1 block text-sm text-[var(--text-m)]">"Tümünü Gör" linki</label>
                  <Input value={cfg?.seeAllUrl ?? ''} onChange={(e) => cfgAlan('seeAllUrl', e.target.value || null)} placeholder="/urun-listesi" />
                </div>
                {form.template === 'flash' && (
                  <div>
                    <label className="mb-1 block text-sm text-[var(--text-m)]">Flash bitişi</label>
                    <Input type="datetime-local" value={(cfg?.endsAt ?? '').slice(0, 16)} onChange={(e) => cfgAlan('endsAt', e.target.value ? e.target.value + ':00Z' : null)} />
                  </div>
                )}
                {form.blockType === 'categories' && (
                  <div>
                    <label className="mb-1 block text-sm text-[var(--text-m)]">Görünüm tipi</label>
                    <SearchableSelect
                      value={cfg?.gorunum ?? null}
                      onChange={(v) => cfgAlan('gorunum', v)}
                      options={[{ value: 'kapsul', label: 'Kapsül' }, { value: 'kare', label: 'Kare' }]}
                      placeholder="kapsul"
                    />
                  </div>
                )}
              </div>
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={!!cfg?.mobileCarousel} onChange={(e) => cfgAlan('mobileCarousel', e.target.checked)} />
                Mobilde yatay kaydırma (carousel)
              </label>
            </div>
          )}

          <details className="rounded-lg border border-[var(--border)] p-3">
            <summary className="cursor-pointer text-sm font-semibold text-[var(--text-m)]">Gelişmiş (ham JSON)</summary>
            <div className="mt-3 space-y-2">
              {ORNEK_CONFIG[form.blockType] && (
                <Button size="sm" variant="secondary" onClick={ornekYukle}>Örnek iskelet yükle</Button>
              )}
              <Textarea
                rows={8}
                value={form.configJson ?? ''}
                onChange={(e) => alan('configJson', e.target.value || null)}
                placeholder='{"productSource":{"source":"new-arrivals","limit":12}}'
              />
              <p className="text-xs text-[var(--text-s)]">
                Formla ham JSON aynı veriyi düzenler — hangisinden değiştirirseniz diğeri güncellenir.
                Kural seviyesi: {tipDef?.ruleLevel === 'Block' ? 'blok' : tipDef?.ruleLevel === 'Item' ? 'öğe' : 'blok + öğe'}.
              </p>
            </div>
          </details>

          {tipDef && tipDef.ruleLevel !== 'Item' && (
            <>
              <h3 className="text-sm font-semibold">Blok Kuralı (JSON — G-M2)</h3>
              <Textarea rows={3} value={form.ruleJson ?? ''} onChange={(e) => alan('ruleJson', e.target.value || null)}
                placeholder='{"city":["ankara","izmir"],"device":["mobile"]}' />
            </>
          )}
        </div>
      </div>

      {tipDef?.supportsItems && (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-sm font-semibold">Öğeler ({ogeler.length})</h2>
            <Button size="sm" onClick={() => setOgeModal({ index: -1, oge: bosOge(ogeler.length + 1) })}>Öğe Ekle</Button>
          </div>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--border)] text-left text-[var(--text-s)]">
                <th className="px-3 py-2 w-16">Sıra</th>
                <th className="px-3 py-2">Başlık</th>
                <th className="px-3 py-2">Görsel</th>
                <th className="px-3 py-2">Link</th>
                <th className="px-3 py-2">Durum</th>
                <th className="px-3 py-2 text-right">İşlem</th>
              </tr>
            </thead>
            <tbody>
              {ogeler.map((oge, i) => (
                <tr key={i} className="cursor-pointer border-b border-[var(--border)] last:border-0 hover:bg-[var(--surface2)]"
                    onClick={() => setOgeModal({ index: i, oge: { ...oge } })}>
                  <td className="px-3 py-2">{oge.sortOrder}</td>
                  <td className="px-3 py-2 font-medium">{oge.titleI18n?.tr || '—'}</td>
                  <td className="px-3 py-2 text-[var(--text-m)]">{oge.imageUrl ? '✓' : '—'}</td>
                  <td className="px-3 py-2 text-[var(--text-m)]">{oge.linkUrl ?? '—'}</td>
                  <td className="px-3 py-2"><Badge variant={oge.isActive ? 'success' : 'neutral'}>{oge.isActive ? 'Aktif' : 'Pasif'}</Badge></td>
                  <td className="px-3 py-2 text-right" onClick={(e) => e.stopPropagation()}>
                    <Button size="sm" variant="danger" onClick={() => setOgeler(ogeler.filter((_, x) => x !== i))}>Kaldır</Button>
                  </td>
                </tr>
              ))}
              {ogeler.length === 0 && (
                <tr><td colSpan={6} className="px-3 py-6 text-center text-[var(--text-m)]">Öğe yok — öğesiz öğeli blok yayında gösterilmez.</td></tr>
              )}
            </tbody>
          </table>
          <p className="mt-2 text-xs text-[var(--text-s)]">Öğe değişiklikleri "Kaydet" ile birlikte yazılır (tam liste).</p>
        </div>
      )}

      <Modal open={!!ogeModal} onClose={() => setOgeModal(null)} title={ogeModal?.index === -1 ? 'Öğe Ekle' : 'Öğe Düzenle'} size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setOgeModal(null)}>Vazgeç</Button>
            <Button onClick={ogeKaydet}>Tamam</Button>
          </>
        }>
        {ogeModal && (
          <div className="grid gap-3 sm:grid-cols-2">
            {([
              ['Başlık (TR)', ogeModal.oge.titleI18n?.tr ?? '', (v: string) => ({ titleI18n: { ...ogeModal.oge.titleI18n, tr: v } })],
              ['Alt başlık (TR)', ogeModal.oge.subtitleI18n?.tr ?? '', (v: string) => ({ subtitleI18n: v ? { tr: v } : null })],
              ['Görsel URL', ogeModal.oge.imageUrl ?? '', (v: string) => ({ imageUrl: v || null })],
              ['Mobil görsel URL', ogeModal.oge.mobileImageUrl ?? '', (v: string) => ({ mobileImageUrl: v || null })],
              ['Video URL (story)', ogeModal.oge.videoUrl ?? '', (v: string) => ({ videoUrl: v || null })],
              ['Link', ogeModal.oge.linkUrl ?? '', (v: string) => ({ linkUrl: v || null })],
              ['Buton metni (TR)', ogeModal.oge.buttonTextI18n?.tr ?? '', (v: string) => ({ buttonTextI18n: v ? { tr: v } : null })],
              ['Rozet', ogeModal.oge.badgeLabel ?? '', (v: string) => ({ badgeLabel: v || null })],
            ] as [string, string, (v: string) => Partial<ItemDto>][]).map(([etiket, deger, degistir]) => (
              <div key={etiket}>
                <label className="mb-1 block text-sm text-[var(--text-m)]">{etiket}</label>
                <Input value={deger} onChange={(e) => setOgeModal({ ...ogeModal, oge: { ...ogeModal.oge, ...degistir(e.target.value) } })} />
              </div>
            ))}
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Sıra</label>
              <Input type="number" value={ogeModal.oge.sortOrder}
                onChange={(e) => setOgeModal({ ...ogeModal, oge: { ...ogeModal.oge, sortOrder: Number(e.target.value) } })} />
            </div>
            <div className="flex items-end gap-4 pb-1">
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={ogeModal.oge.isActive}
                  onChange={(e) => setOgeModal({ ...ogeModal, oge: { ...ogeModal.oge, isActive: e.target.checked } })} />
                Aktif
              </label>
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={ogeModal.oge.openInNewTab}
                  onChange={(e) => setOgeModal({ ...ogeModal, oge: { ...ogeModal.oge, openInNewTab: e.target.checked } })} />
                Yeni sekmede aç
              </label>
            </div>
            <div className="sm:col-span-2">
              <label className="mb-1 block text-sm text-[var(--text-m)]">Öğe config (JSON — tab ürün kaynağı, story frames, marka görselleri...)</label>
              <Textarea rows={3} value={ogeModal.oge.configJson ?? ''}
                onChange={(e) => setOgeModal({ ...ogeModal, oge: { ...ogeModal.oge, configJson: e.target.value || null } })}
                placeholder='{"productSource":{"source":"manual","productCodes":["P-000001"]}} / {"frames":[...]} / {"images":[...],"productCount":"42"}' />
            </div>
            {tipDef && tipDef.ruleLevel !== 'Block' && (
              <div className="sm:col-span-2">
                <label className="mb-1 block text-sm text-[var(--text-m)]">Öğe kuralı (JSON — G-M2)</label>
                <Textarea rows={2} value={ogeModal.oge.ruleJson ?? ''}
                  onChange={(e) => setOgeModal({ ...ogeModal, oge: { ...ogeModal.oge, ruleJson: e.target.value || null } })}
                  placeholder='{"city":["ankara"]}' />
              </div>
            )}
          </div>
        )}
      </Modal>
    </div>
  )
}
