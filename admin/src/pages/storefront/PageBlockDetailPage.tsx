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

        <div className="space-y-3 rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold">Config (JSON)</h2>
            {ORNEK_CONFIG[form.blockType] && (
              <Button size="sm" variant="secondary" onClick={ornekYukle}>Örnek iskelet yükle</Button>
            )}
          </div>
          <Textarea
            rows={10}
            value={form.configJson ?? ''}
            onChange={(e) => alan('configJson', e.target.value || null)}
            placeholder={tipDef?.requiresProductSource
              ? `{"productSource":{"source":"new-arrivals","limit":12,"inStockOnly":true,"tags":["yaz"],"discountedOnly":true}} — kaynaklar: ${KAYNAKLAR.join(', ')}`
              : tipDef?.requiresCollectionSource
                ? '{"collectionSource":{"limit":10,"sort":"popular"}}'
                : 'Tipe özgü ayarlar: tema, seeAllUrl, endsAt (flash), mobileCarousel, gorunum...'}
          />
          <p className="text-xs text-[var(--text-s)]">
            {tipDef?.requiresProductSource && 'Bu tipte productSource zorunludur (Yayınla denetler). Filtre bayrakları: inStockOnly (yalnız stokta), tags (etiket eşleşmesi), discountedOnly (yalnız indirimli) — category kaynağında desteklenmez. Üye bağlamlı kaynaklar: recently-viewed (son gezilenler), favorites (favoriler) — içerik ziyaretçinin kendi verisiyle dolar; misafirde blok görünmez. '}
            {tipDef?.requiresCollectionSource && 'Bu tipte collectionSource zorunludur; yalnız onaylı+herkese açık koleksiyonlar listelenir. '}
            Kural seviyesi: {tipDef?.ruleLevel === 'Block' ? 'blok' : tipDef?.ruleLevel === 'Item' ? 'öğe' : 'blok + öğe'} (kurallar G-M2'de canlıya bağlanır).
          </p>
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
