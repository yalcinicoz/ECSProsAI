import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueries, useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Input } from '@/components/ui/Input'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { PageSpinner } from '@/components/ui/Spinner'

// G6: vitrin yönetimi — taslak bloklar (yerleşim başına sıralı liste) + Yayınla /
// rollback / yayın geçmişi. Taslak değişiklikler canlıyı ETKİLEMEZ; canlı yalnız
// aktif snapshot'ı okur (spec). Satır tıklanınca blok detayına gider.

interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel { id: string; platformTypeName?: string; name?: string; code?: string; firmId?: string; firmName?: string }
interface CatalogData {
  placements: { code: string; displayName: string }[]
  blockTypes: {
    code: string; displayName: string; ruleLevel: string; supportsItems: boolean
    templates: string[]; requiresProductSource: boolean; requiresCollectionSource: boolean
  }[]
  carouselThemes: string[]
}
interface BlockRow {
  id: string; placement: string; blockType: string; template: string | null
  titleI18n: Record<string, string>; sortOrder: number; isActive: boolean
  priority: number; startAt: string | null; endAt: string | null
  itemCount: number; hasProductSource: boolean
}
interface SnapshotRow { id: string; version: number; publishedAt: string; isActive: boolean; status: string; note: string | null }
interface PublishLogRow { id: string; version: number; previousVersion: number | null; publishedAt: string; status: string; errorMessage: string | null; note: string | null }
// G12: önizleme sonucu — taslak blokların seçilen segmentteki görünürlüğü + nedeni
interface PreviewBlock {
  id: string; blockType: string; template: string | null; title: Record<string, string>
  sortOrder: number; isActive: boolean; visible: boolean; reason: string
  itemTotal: number; itemVisible: number; productCount: number | null
}
interface PreviewResult {
  segment: { city: string | null; cityName: string | null; region: string | null; gender: string; device: string; membership: string }
  blocks: PreviewBlock[]
}
interface MemberGroup { id: string; nameI18n?: Record<string, string>; code?: string }
// 2026-07-22: canlı önizlemeli editör — taslak bloklar gerçek içerikleriyle
interface DraftItem {
  id: string; titleI18n: Record<string, string>; subtitleI18n: Record<string, string> | null
  imageUrl: string | null; mobileImageUrl: string | null; videoUrl: string | null
  linkUrl: string | null; badgeLabel: string | null; isActive: boolean
}
interface DraftUrun { code: string; ad: string | null; gorsel: string | null; fiyat: number }
interface DraftKoleksiyon { name: string; itemCount: number; viewCount: number }
interface DraftBlock {
  id: string; blockType: string; template: string | null
  titleI18n: Record<string, string>; subtitleI18n: Record<string, string> | null
  sortOrder: number; isActive: boolean; startAt: string | null; endAt: string | null
  uyeBaglamli: boolean; kaynakTipi: string | null
  items: DraftItem[]; urunler: DraftUrun[] | null; koleksiyonlar: DraftKoleksiyon[] | null
}
// G13: değişiklik geçmişi (vitrin audit kayıtları — iam.audit_logs)
interface AuditRow {
  id: string; action: string; entityType: string; entityId: string
  createdAt: string; userName: string | null; title: string | null
}

const AUDIT_ACTION_TR: Record<string, string> = {
  Created: 'Oluşturuldu', Updated: 'Güncellendi', Deleted: 'Silindi',
  Activated: 'Aktifleştirildi', Deactivated: 'Pasifleştirildi',
  Published: 'Yayınlandı', Rollback: 'Geri Dönüş', Previewed: 'Önizlendi',
}

// 81 il (plaka + ad) — önizleme segment seçicisi; storefront şehir çipiyle aynı referans veri.
const IL_LISTESI: [string, string][] = [
  ['01', 'Adana'], ['02', 'Adıyaman'], ['03', 'Afyonkarahisar'], ['04', 'Ağrı'], ['05', 'Amasya'],
  ['06', 'Ankara'], ['07', 'Antalya'], ['08', 'Artvin'], ['09', 'Aydın'], ['10', 'Balıkesir'],
  ['11', 'Bilecik'], ['12', 'Bingöl'], ['13', 'Bitlis'], ['14', 'Bolu'], ['15', 'Burdur'],
  ['16', 'Bursa'], ['17', 'Çanakkale'], ['18', 'Çankırı'], ['19', 'Çorum'], ['20', 'Denizli'],
  ['21', 'Diyarbakır'], ['22', 'Edirne'], ['23', 'Elazığ'], ['24', 'Erzincan'], ['25', 'Erzurum'],
  ['26', 'Eskişehir'], ['27', 'Gaziantep'], ['28', 'Giresun'], ['29', 'Gümüşhane'], ['30', 'Hakkari'],
  ['31', 'Hatay'], ['32', 'Isparta'], ['33', 'Mersin'], ['34', 'İstanbul'], ['35', 'İzmir'],
  ['36', 'Kars'], ['37', 'Kastamonu'], ['38', 'Kayseri'], ['39', 'Kırklareli'], ['40', 'Kırşehir'],
  ['41', 'Kocaeli'], ['42', 'Konya'], ['43', 'Kütahya'], ['44', 'Malatya'], ['45', 'Manisa'],
  ['46', 'Kahramanmaraş'], ['47', 'Mardin'], ['48', 'Muğla'], ['49', 'Muş'], ['50', 'Nevşehir'],
  ['51', 'Niğde'], ['52', 'Ordu'], ['53', 'Rize'], ['54', 'Sakarya'], ['55', 'Samsun'],
  ['56', 'Siirt'], ['57', 'Sinop'], ['58', 'Sivas'], ['59', 'Tekirdağ'], ['60', 'Tokat'],
  ['61', 'Trabzon'], ['62', 'Tunceli'], ['63', 'Şanlıurfa'], ['64', 'Uşak'], ['65', 'Van'],
  ['66', 'Yozgat'], ['67', 'Zonguldak'], ['68', 'Aksaray'], ['69', 'Bayburt'], ['70', 'Karaman'],
  ['71', 'Kırıkkale'], ['72', 'Batman'], ['73', 'Şırnak'], ['74', 'Bartın'], ['75', 'Ardahan'],
  ['76', 'Iğdır'], ['77', 'Yalova'], ['78', 'Karabük'], ['79', 'Kilis'], ['80', 'Osmaniye'],
  ['81', 'Düzce'],
]

const getChannelLabel = (c: Channel) => c.name ?? c.platformTypeName ?? c.code ?? c.id

export function PagesManagementPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [platformId, setPlatformId] = useState<string>(() => sessionStorage.getItem('pages.platformId') ?? '')
  useEffect(() => { if (platformId) sessionStorage.setItem('pages.platformId', platformId) }, [platformId])
  const [placement, setPlacement] = useState('homepage')
  const [createOpen, setCreateOpen] = useState(false)
  const [newType, setNewType] = useState<string | null>(null)
  const [newTemplate, setNewTemplate] = useState<string | null>(null)
  const [newTitle, setNewTitle] = useState('')
  const [publishNote, setPublishNote] = useState('')
  const [publishError, setPublishError] = useState('')
  // G12: önizleme segment seçimi + sonuç
  const [previewOpen, setPreviewOpen] = useState(false)
  const [prevCity, setPrevCity] = useState<string | null>(null)
  const [prevGender, setPrevGender] = useState<string | null>(null)
  const [prevDevice, setPrevDevice] = useState<string | null>('desktop')
  const [prevMember, setPrevMember] = useState<string | null>('guest')
  const [prevGroup, setPrevGroup] = useState<string | null>(null)
  const [previewResult, setPreviewResult] = useState<PreviewResult | null>(null)
  // Canlı önizleme editörü: masaüstü/mobil çerçevesi
  const [cihaz, setCihaz] = useState<'desktop' | 'mobile'>('desktop')

  const { data: firms = [] } = useQuery<Firm[]>({
    queryKey: ['firms'],
    queryFn: async () => (await api.get('/core/firms')).data.data ?? [],
  })
  const platformQueries = useQueries({
    queries: firms.map((firm) => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<Channel[]> => {
        const { data } = await api.get(`/core/firms/${firm.id}/platforms`)
        const firmName = firm.nameI18n?.['tr'] ?? Object.values(firm.nameI18n ?? {})[0] ?? ''
        return (data.data ?? []).map((ch: Channel) => ({ ...ch, firmId: firm.id, firmName }))
      },
      enabled: firms.length > 0,
    })),
  })
  const channels: Channel[] = platformQueries.flatMap((q) => q.data ?? [])

  const { data: catalog } = useQuery<CatalogData>({
    queryKey: ['pages-catalog'],
    queryFn: async () => (await api.get('/pages/catalog')).data.data,
  })

  const { data: blocks = [], isLoading } = useQuery<BlockRow[]>({
    queryKey: ['page-blocks', platformId, placement],
    queryFn: async () =>
      (await api.get('/pages/blocks', { params: { firmPlatformId: platformId, placement } })).data.data ?? [],
    enabled: !!platformId,
  })

  // Canlı önizleme verisi — taslak bloklar gerçek içerikle (öğe görselleri + ürün kartları)
  const { data: draftBlocks = [], isLoading: draftLoading } = useQuery<DraftBlock[]>({
    queryKey: ['draft-compose', platformId, placement],
    queryFn: async () =>
      (await api.get('/pages/draft-compose', { params: { firmPlatformId: platformId, placement } })).data.data ?? [],
    enabled: !!platformId,
  })

  const { data: snapshots = [] } = useQuery<SnapshotRow[]>({
    queryKey: ['page-snapshots', platformId],
    queryFn: async () => (await api.get('/pages/snapshots', { params: { firmPlatformId: platformId } })).data.data ?? [],
    enabled: !!platformId,
  })
  const { data: publishLogs = [] } = useQuery<PublishLogRow[]>({
    queryKey: ['publish-logs', platformId],
    queryFn: async () => (await api.get('/pages/publish-logs', { params: { firmPlatformId: platformId } })).data.data ?? [],
    enabled: !!platformId,
  })
  const { data: auditLogs = [] } = useQuery<AuditRow[]>({
    queryKey: ['page-audit-logs', platformId],
    queryFn: async () => (await api.get('/pages/audit-logs', { params: { firmPlatformId: platformId } })).data.data ?? [],
    enabled: !!platformId,
  })

  const yenile = () => {
    queryClient.invalidateQueries({ queryKey: ['page-blocks', platformId] })
    queryClient.invalidateQueries({ queryKey: ['draft-compose', platformId] })
    queryClient.invalidateQueries({ queryKey: ['page-snapshots', platformId] })
    queryClient.invalidateQueries({ queryKey: ['publish-logs', platformId] })
    queryClient.invalidateQueries({ queryKey: ['page-audit-logs', platformId] })
  }

  const createBlock = useMutation({
    mutationFn: async () => {
      const { data } = await api.post('/pages/blocks', {
        firmPlatformId: platformId, placement, blockType: newType, template: newTemplate,
        titleI18n: { tr: newTitle }, subtitleI18n: null,
        sortOrder: blocks.length + 1, isActive: true,
        startAt: null, endAt: null, priority: 0, ruleJson: null, configJson: null,
      })
      return data.data.id as string
    },
    onSuccess: (id) => {
      setCreateOpen(false); setNewTitle(''); setNewType(null); setNewTemplate(null)
      yenile()
      navigate(`/storefront/pages/${id}?platformId=${platformId}`)
    },
  })

  const reorder = useMutation({
    mutationFn: async (orderedIds: string[]) =>
      api.put('/pages/blocks/order', { firmPlatformId: platformId, placement, orderedIds }),
    onSuccess: yenile,
  })
  const toggleActive = useMutation({
    mutationFn: async (id: string) => {
      const { data } = await api.get(`/pages/blocks/${id}`, { params: { firmPlatformId: platformId } })
      const b = data.data
      await api.put(`/pages/blocks/${id}`, {
        firmPlatformId: platformId, placement: b.placement, blockType: b.blockType,
        template: b.template, titleI18n: b.titleI18n, subtitleI18n: b.subtitleI18n,
        sortOrder: b.sortOrder, isActive: !b.isActive, startAt: b.startAt, endAt: b.endAt,
        priority: b.priority, ruleJson: b.ruleJson, configJson: b.configJson,
      })
    },
    onSuccess: yenile,
  })
  const remove = useMutation({
    mutationFn: async (id: string) => api.delete(`/pages/blocks/${id}`, { params: { firmPlatformId: platformId } }),
    onSuccess: yenile,
  })
  const publish = useMutation({
    mutationFn: async () => api.post('/pages/publish', { firmPlatformId: platformId, note: publishNote || null }),
    onSuccess: () => { setPublishNote(''); setPublishError(''); yenile() },
    onError: (e: unknown) => {
      const err = e as { response?: { data?: { error?: string } } }
      setPublishError(err.response?.data?.error ?? 'Yayınlama başarısız.')
    },
  })
  const rollback = useMutation({
    mutationFn: async (targetVersion: number) =>
      api.post('/pages/rollback', { firmPlatformId: platformId, targetVersion }),
    onSuccess: yenile,
  })

  // G12: önizleme — taslak veri + kurgu segment; canlıyı etkilemez (spec)
  const { data: memberGroups = [] } = useQuery<MemberGroup[]>({
    queryKey: ['member-groups'],
    queryFn: async () => (await api.get('/crm/member-groups')).data.data ?? [],
    enabled: previewOpen && prevMember === 'member',
  })
  const preview = useMutation({
    mutationFn: async () => (await api.post('/pages/preview', {
      firmPlatformId: platformId, placement,
      city: prevCity, gender: prevGender, device: prevDevice,
      isMember: prevMember === 'member',
      memberGroupId: prevMember === 'member' ? prevGroup : null,
    })).data.data as PreviewResult,
    onSuccess: setPreviewResult,
  })

  const tasima = (index: number, yon: -1 | 1) => {
    const ids = blocks.map((b) => b.id)
    const hedef = index + yon
    if (hedef < 0 || hedef >= ids.length) return
    ;[ids[index], ids[hedef]] = [ids[hedef], ids[index]]
    reorder.mutate(ids)
  }

  const typeDef = (code: string) => catalog?.blockTypes.find((t) => t.code === code)
  const secilenTip = newType ? typeDef(newType) : null

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Vitrin Yönetimi</h1>
          <p className="text-sm text-[var(--text-m)]">
            Taslak bloklar canlıyı etkilemez — değişiklikler "Yayınla" ile versiyonlu snapshot olarak canlıya alınır.
          </p>
        </div>
        <div className="w-72">
          <SearchableSelect
            value={platformId || null}
            onChange={(v) => setPlatformId(v ?? '')}
            options={channels.map((c) => ({ value: c.id, label: `${getChannelLabel(c)} (${c.firmName})` }))}
            placeholder="Platform seç"
          />
        </div>
      </div>

      {!platformId ? (
        <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-10 text-center text-[var(--text-m)]">
          Blokları yönetmek için platform seçin.
        </div>
      ) : (
        <>
          <div className="flex flex-wrap gap-2">
            {(catalog?.placements ?? []).map((p) => (
              <button
                key={p.code}
                onClick={() => setPlacement(p.code)}
                className={`rounded-lg px-3 py-1.5 text-sm ${
                  placement === p.code
                    ? 'bg-[var(--brand)] text-white'
                    : 'bg-[var(--surface2)] text-[var(--text-m)] hover:text-[var(--text)]'
                }`}
              >
                {p.displayName}
              </button>
            ))}
          </div>

          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Button size="sm" onClick={() => setCreateOpen(true)}>Yeni Blok</Button>
              <Button size="sm" variant="secondary" onClick={() => { setPreviewResult(null); setPreviewOpen(true) }}>
                Önizleme
              </Button>
            </div>
            <div className="flex items-center gap-2">
              <Input
                value={publishNote}
                onChange={(e) => setPublishNote(e.target.value)}
                placeholder="Yayın notu (opsiyonel)"
                className="w-56"
              />
              <Button size="sm" onClick={() => publish.mutate()} disabled={publish.isPending}>
                Yayınla
              </Button>
            </div>
          </div>
          {publishError && (
            <div className="rounded-lg border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-700">
              {publishError}
            </div>
          )}

          {/* 2026-07-22: canlı önizlemeli blok editörü — bloklar gerçek içerikleriyle
              sayfa sırasında dikey önizlenir; her bloğun üstünde yönetim çubuğu. */}
          <div className="flex items-center justify-end gap-1">
            <button
              className={`rounded-lg px-3 py-1.5 text-sm ${cihaz === 'desktop' ? 'bg-[var(--brand)] text-white' : 'bg-[var(--surface2)] text-[var(--text-m)]'}`}
              onClick={() => setCihaz('desktop')}
            >🖥 Masaüstü</button>
            <button
              className={`rounded-lg px-3 py-1.5 text-sm ${cihaz === 'mobile' ? 'bg-[var(--brand)] text-white' : 'bg-[var(--surface2)] text-[var(--text-m)]'}`}
              onClick={() => setCihaz('mobile')}
            >📱 Mobil</button>
          </div>

          {(isLoading || draftLoading) ? (
            <PageSpinner />
          ) : (
            <div className="rounded-xl border border-[var(--border)] bg-[var(--surface2)] p-4">
              <div className={`mx-auto space-y-3 transition-all ${cihaz === 'mobile' ? 'max-w-[400px]' : 'max-w-[1080px]'}`}>
                {draftBlocks.map((b, i) => {
                  const baslik = b.titleI18n?.tr ?? Object.values(b.titleI18n ?? {})[0] ?? '—'
                  const tip = typeDef(b.blockType)?.displayName ?? b.blockType
                  const gorsel = (it: DraftItem) => (cihaz === 'mobile' ? it.mobileImageUrl || it.imageUrl : it.imageUrl || it.mobileImageUrl)
                  const aktifItems = b.items.filter((it) => it.isActive)
                  const bosGorsel = 'data:image/svg+xml;utf8,<svg xmlns=%22http://www.w3.org/2000/svg%22 width=%2280%22 height=%2260%22><rect width=%2280%22 height=%2260%22 fill=%22%23e2e8f0%22/></svg>'
                  return (
                    <div key={b.id} className={`overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)] ${!b.isActive ? 'opacity-50' : ''}`}>
                      {/* Yönetim çubuğu */}
                      <div className="flex items-center gap-2 border-b border-[var(--border)] bg-[var(--surface2)] px-3 py-2 text-sm">
                        <div className="flex items-center gap-1">
                          <button className="rounded px-1.5 py-0.5 text-[var(--text-m)] hover:bg-[var(--surface)] hover:text-[var(--text)] disabled:opacity-30" disabled={i === 0} onClick={() => tasima(i, -1)} aria-label="Yukarı taşı">↑</button>
                          <button className="rounded px-1.5 py-0.5 text-[var(--text-m)] hover:bg-[var(--surface)] hover:text-[var(--text)] disabled:opacity-30" disabled={i === draftBlocks.length - 1} onClick={() => tasima(i, 1)} aria-label="Aşağı taşı">↓</button>
                        </div>
                        <span className="font-medium">{baslik}</span>
                        <Badge variant="neutral">{tip}</Badge>
                        {b.template && <span className="text-xs text-[var(--text-s)]">{b.template}</span>}
                        {(b.startAt || b.endAt) && <span className="text-xs text-[var(--text-s)]">tarihli</span>}
                        {b.uyeBaglamli && <Badge variant="warning">üye bağlamlı</Badge>}
                        <div className="ml-auto flex items-center gap-2">
                          <Badge variant={b.isActive ? 'success' : 'neutral'}>{b.isActive ? 'Aktif' : 'Pasif'}</Badge>
                          <Button size="sm" variant="secondary" onClick={() => toggleActive.mutate(b.id)} disabled={toggleActive.isPending}>
                            {b.isActive ? 'Pasifleştir' : 'Aktifleştir'}
                          </Button>
                          <Button size="sm" variant="secondary" onClick={() => navigate(`/storefront/pages/${b.id}?platformId=${platformId}`)}>Düzenle</Button>
                          <Button size="sm" variant="danger" onClick={() => { if (confirm('Blok silinsin mi? (Canlı yayın bir sonraki Yayınla\'ya kadar etkilenmez)')) remove.mutate(b.id) }}>Sil</Button>
                        </div>
                      </div>

                      {/* Gerçek içerik önizlemesi — türe göre */}
                      <div className="p-3">
                        {b.blockType === 'story' && (
                          <div className="flex gap-3 overflow-x-auto pb-1">
                            {aktifItems.map((it) => (
                              <div key={it.id} className="flex w-16 shrink-0 flex-col items-center gap-1">
                                <img src={gorsel(it) ?? bosGorsel} alt="" className="h-14 w-14 rounded-full border-2 border-[var(--brand)] object-cover" />
                                <span className="w-full truncate text-center text-[10px] text-[var(--text-m)]">{it.titleI18n?.tr ?? ''}</span>
                              </div>
                            ))}
                            {aktifItems.length === 0 && <p className="text-xs text-[var(--text-s)]">Öğe yok</p>}
                          </div>
                        )}

                        {b.blockType === 'categories' && (
                          <div className={`grid gap-2 ${cihaz === 'mobile' ? 'grid-cols-3' : 'grid-cols-6'}`}>
                            {aktifItems.map((it) => (
                              <div key={it.id} className="flex flex-col items-center gap-1">
                                <img src={gorsel(it) ?? bosGorsel} alt="" className="h-16 w-16 rounded-xl object-cover" />
                                <span className="w-full truncate text-center text-[11px] text-[var(--text-m)]">{it.titleI18n?.tr ?? ''}</span>
                              </div>
                            ))}
                            {aktifItems.length === 0 && <p className="text-xs text-[var(--text-s)]">Öğe yok</p>}
                          </div>
                        )}

                        {(b.blockType === 'banner' || b.blockType === 'coklu-banner' || b.blockType === 'slider' || b.blockType === 'bilgi-banner') && (
                          <div className={`grid gap-2 ${b.blockType === 'slider' ? 'grid-cols-1' : cihaz === 'mobile' ? 'grid-cols-2' : 'grid-cols-4'}`}>
                            {(b.blockType === 'slider' ? aktifItems.slice(0, 1) : aktifItems).map((it) => (
                              <div key={it.id} className="relative overflow-hidden rounded-lg">
                                <img src={gorsel(it) ?? bosGorsel} alt="" className={`w-full object-cover ${b.blockType === 'slider' ? (cihaz === 'mobile' ? 'aspect-[4/5]' : 'aspect-[16/6]') : 'aspect-[16/9]'}`} />
                                {(it.titleI18n?.tr || it.badgeLabel) && (
                                  <span className="absolute bottom-1 left-1 rounded bg-black/55 px-1.5 py-0.5 text-[10px] text-white">
                                    {it.badgeLabel || it.titleI18n?.tr}
                                  </span>
                                )}
                              </div>
                            ))}
                            {b.blockType === 'slider' && aktifItems.length > 1 && (
                              <div className="flex gap-1.5 overflow-x-auto">
                                {aktifItems.slice(1).map((it) => (
                                  <img key={it.id} src={gorsel(it) ?? bosGorsel} alt="" className="h-12 w-20 shrink-0 rounded object-cover opacity-70" />
                                ))}
                              </div>
                            )}
                            {aktifItems.length === 0 && <p className="text-xs text-[var(--text-s)]">Öğe yok</p>}
                          </div>
                        )}

                        {b.blockType === 'announcement' && (
                          <div className="rounded-lg bg-slate-900 px-3 py-2 text-center text-xs text-white">
                            {aktifItems.map((it) => it.titleI18n?.tr).filter(Boolean).join('  ·  ') || 'Duyuru öğesi yok'}
                          </div>
                        )}

                        {b.koleksiyonlar && (
                          <div className={`grid gap-2 ${cihaz === 'mobile' ? 'grid-cols-1' : 'grid-cols-3'}`}>
                            {b.koleksiyonlar.map((k) => (
                              <div key={k.name} className="rounded-lg border border-[var(--border)] p-3 text-sm">
                                <div className="font-medium">{k.name}</div>
                                <div className="text-xs text-[var(--text-m)]">{k.itemCount} ürün · {k.viewCount} görüntülenme</div>
                              </div>
                            ))}
                            {b.koleksiyonlar.length === 0 && <p className="text-xs text-[var(--text-s)]">Uygun koleksiyon yok</p>}
                          </div>
                        )}

                        {b.urunler && (
                          <div className={`grid gap-2 ${cihaz === 'mobile' ? 'grid-cols-2' : 'grid-cols-6'}`}>
                            {b.urunler.map((u) => (
                              <div key={u.code} className="overflow-hidden rounded-lg border border-[var(--border)]">
                                <img src={u.gorsel ?? bosGorsel} alt="" className="aspect-[3/4] w-full object-cover" />
                                <div className="p-1.5">
                                  <div className="truncate text-[11px]">{u.ad ?? u.code}</div>
                                  <div className="text-[11px] font-semibold text-[var(--brand)]">
                                    {u.fiyat.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} TL
                                  </div>
                                </div>
                              </div>
                            ))}
                            {b.urunler.length === 0 && <p className="col-span-full text-xs text-[var(--text-s)]">Kaynak ürün döndürmedi</p>}
                          </div>
                        )}

                        {b.uyeBaglamli && (
                          <p className="text-xs text-[var(--text-s)]">Üye bağlamlı kaynak ({b.kaynakTipi}) — içerik ziyaretçinin kendi verisiyle dolar; önizlenemez.</p>
                        )}

                        {b.blockType === 'tabs' && (
                          <div className="flex gap-2 text-xs text-[var(--text-m)]">
                            {aktifItems.map((it) => (
                              <span key={it.id} className="rounded-full border border-[var(--border)] px-2 py-1">{it.titleI18n?.tr ?? 'Sekme'}</span>
                            ))}
                            <span className="self-center text-[var(--text-s)]">— sekme ürünleri detay ekranında</span>
                          </div>
                        )}

                        {!b.urunler && !b.koleksiyonlar && !b.uyeBaglamli
                          && !['story', 'categories', 'banner', 'coklu-banner', 'slider', 'bilgi-banner', 'announcement', 'tabs'].includes(b.blockType) && (
                          <p className="text-xs text-[var(--text-s)]">{tip} — bu tür için özel önizleme yok ({b.items.length} öğe).</p>
                        )}
                      </div>
                    </div>
                  )
                })}
                {draftBlocks.length === 0 && (
                  <div className="rounded-xl border border-dashed border-[var(--border)] bg-[var(--surface)] p-10 text-center text-[var(--text-m)]">
                    Bu yerleşimde taslak blok yok — "Yeni Blok" ile başlayın.
                  </div>
                )}
              </div>
            </div>
          )}

          <div className="grid gap-4 md:grid-cols-2">
            <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
              <h2 className="mb-3 text-sm font-semibold">Yayın Versiyonları</h2>
              <div className="space-y-2">
                {snapshots.map((s) => (
                  <div key={s.id} className="flex items-center justify-between rounded-lg bg-[var(--surface2)] px-3 py-2 text-sm">
                    <div>
                      <span className="font-medium">v{s.version}</span>
                      <span className="ml-2 text-[var(--text-m)]">{new Date(s.publishedAt).toLocaleString('tr-TR')}</span>
                      {s.note && <span className="ml-2 text-xs text-[var(--text-s)]">{s.note}</span>}
                    </div>
                    <div className="flex items-center gap-2">
                      {s.isActive
                        ? <Badge variant="success">Aktif Yayın</Badge>
                        : <Button size="sm" onClick={() => rollback.mutate(s.version)} disabled={rollback.isPending}>Bu versiyona dön</Button>}
                    </div>
                  </div>
                ))}
                {snapshots.length === 0 && <p className="text-sm text-[var(--text-m)]">Henüz yayın yapılmadı.</p>}
              </div>
            </div>

            <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
              <h2 className="mb-3 text-sm font-semibold">Yayın Geçmişi</h2>
              <div className="max-h-72 space-y-2 overflow-y-auto">
                {publishLogs.map((l) => (
                  <div key={l.id} className="rounded-lg bg-[var(--surface2)] px-3 py-2 text-sm">
                    <div className="flex items-center justify-between">
                      <span>
                        v{l.version}
                        {l.previousVersion != null && <span className="text-[var(--text-m)]"> (önceki v{l.previousVersion})</span>}
                      </span>
                      <Badge variant={l.status === 'success' ? 'success' : l.status === 'rollback' ? 'warning' : 'danger'}>
                        {l.status === 'success' ? 'Yayınlandı' : l.status === 'rollback' ? 'Geri Dönüş' : 'Başarısız'}
                      </Badge>
                    </div>
                    <div className="text-xs text-[var(--text-m)]">{new Date(l.publishedAt).toLocaleString('tr-TR')}{l.note ? ` · ${l.note}` : ''}</div>
                    {l.errorMessage && <div className="mt-1 text-xs text-red-600">{l.errorMessage}</div>}
                  </div>
                ))}
                {publishLogs.length === 0 && <p className="text-sm text-[var(--text-m)]">Kayıt yok.</p>}
              </div>
            </div>
          </div>

          {/* G13: değişiklik geçmişi — kim, neyi, ne zaman (spec audit ekranı) */}
          <div className="rounded-xl border border-[var(--border)] bg-[var(--surface)] p-4">
            <h2 className="mb-3 text-sm font-semibold">Değişiklik Geçmişi</h2>
            <div className="max-h-80 space-y-1 overflow-y-auto">
              {auditLogs.map((l) => (
                <div key={l.id} className="flex items-center justify-between rounded-lg bg-[var(--surface2)] px-3 py-1.5 text-sm">
                  <div className="min-w-0 truncate">
                    <Badge variant={l.action === 'Deleted' || l.action === 'Deactivated' ? 'danger' : l.action === 'Created' || l.action === 'Published' ? 'success' : 'neutral'}>
                      {AUDIT_ACTION_TR[l.action] ?? l.action}
                    </Badge>
                    <span className="ml-2 font-medium">{l.title ?? '—'}</span>
                    <span className="ml-2 text-xs text-[var(--text-m)]">{l.entityType}</span>
                  </div>
                  <div className="ml-3 shrink-0 text-xs text-[var(--text-m)]">
                    {l.userName || 'bilinmeyen'} · {new Date(l.createdAt).toLocaleString('tr-TR')}
                  </div>
                </div>
              ))}
              {auditLogs.length === 0 && <p className="text-sm text-[var(--text-m)]">Henüz kayıt yok.</p>}
            </div>
          </div>
        </>
      )}

      {/* G12: önizleme — seçilen segmentle kural motoru taslak üzerinde çalışır;
          bloklar görünür/gizli + nedenle listelenir. Canlı yayına dokunmaz. */}
      <Modal open={previewOpen} onClose={() => setPreviewOpen(false)} title="Segment Önizlemesi" size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setPreviewOpen(false)}>Kapat</Button>
            <Button onClick={() => preview.mutate()} disabled={preview.isPending}>Önizle</Button>
          </>
        }>
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-3 md:grid-cols-3">
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Şehir</label>
              <SearchableSelect
                value={prevCity}
                onChange={setPrevCity}
                options={IL_LISTESI.map(([kod, ad]) => ({ value: kod, label: `${ad} (${kod})` }))}
                placeholder="Konumsuz"
              />
            </div>
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Cinsiyet</label>
              <SearchableSelect
                value={prevGender}
                onChange={setPrevGender}
                options={[{ value: 'female', label: 'Kadın' }, { value: 'male', label: 'Erkek' }]}
                placeholder="Bilinmiyor"
              />
            </div>
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Cihaz</label>
              <SearchableSelect
                value={prevDevice}
                onChange={setPrevDevice}
                options={[
                  { value: 'desktop', label: 'Masaüstü' },
                  { value: 'mobile', label: 'Mobil' },
                  { value: 'tablet', label: 'Tablet' },
                ]}
                placeholder="Masaüstü"
              />
            </div>
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Üyelik</label>
              <SearchableSelect
                value={prevMember}
                onChange={(v) => { setPrevMember(v); if (v !== 'member') setPrevGroup(null) }}
                options={[{ value: 'guest', label: 'Misafir' }, { value: 'member', label: 'Üye' }]}
                placeholder="Misafir"
              />
            </div>
            {prevMember === 'member' && (
              <div>
                <label className="mb-1 block text-sm text-[var(--text-m)]">Üye Grubu</label>
                <SearchableSelect
                  value={prevGroup}
                  onChange={setPrevGroup}
                  options={memberGroups.map((g) => ({
                    value: g.id,
                    label: g.nameI18n?.tr ?? Object.values(g.nameI18n ?? {})[0] ?? g.code ?? g.id,
                  }))}
                  placeholder="Grupsuz"
                />
              </div>
            )}
          </div>

          {previewResult && (
            <>
              <div className="rounded-lg bg-[var(--surface2)] px-3 py-2 text-xs text-[var(--text-m)]">
                Çözülen segment: {previewResult.segment.cityName ?? 'konumsuz'}
                {previewResult.segment.region ? ` / ${previewResult.segment.region}` : ''} ·{' '}
                {previewResult.segment.gender} · {previewResult.segment.device} · {previewResult.segment.membership}
                {' '}— yerleşim: {(catalog?.placements ?? []).find((p) => p.code === placement)?.displayName ?? placement}
              </div>
              <div className="space-y-2">
                {previewResult.blocks.map((b) => (
                  <div key={b.id} className="rounded-lg border border-[var(--border)] px-3 py-2 text-sm">
                    <div className="flex items-center justify-between">
                      <span className="font-medium">
                        {b.sortOrder}. {b.title?.tr ?? Object.values(b.title ?? {})[0] ?? '—'}
                        <span className="ml-2 text-xs text-[var(--text-m)]">
                          {typeDef(b.blockType)?.displayName ?? b.blockType}{b.template ? ` / ${b.template}` : ''}
                        </span>
                      </span>
                      <Badge variant={b.visible ? 'success' : 'neutral'}>{b.visible ? 'Görünür' : 'Gizli'}</Badge>
                    </div>
                    <div className={`mt-1 text-xs ${b.visible ? 'text-[var(--text-m)]' : 'text-amber-700'}`}>
                      {b.reason}
                      {b.itemTotal > 0 && ` · öğe: ${b.itemVisible}/${b.itemTotal}`}
                      {b.productCount != null && ` · ürün: ${b.productCount}`}
                    </div>
                  </div>
                ))}
                {previewResult.blocks.length === 0 && (
                  <p className="text-sm text-[var(--text-m)]">Bu yerleşimde taslak blok yok.</p>
                )}
              </div>
            </>
          )}
          {!previewResult && !preview.isPending && (
            <p className="text-xs text-[var(--text-s)]">
              Önizleme TASLAK veriler üzerinde çalışır — yayınlanmamış değişiklikler de değerlendirilir; canlı yayını etkilemez.
            </p>
          )}
        </div>
      </Modal>

      <Modal open={createOpen} onClose={() => setCreateOpen(false)} title="Yeni Blok" size="md"
        footer={
          <>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>Vazgeç</Button>
            <Button onClick={() => createBlock.mutate()} disabled={!newType || !newTitle || createBlock.isPending}>Oluştur</Button>
          </>
        }>
        <div className="space-y-3">
          <div>
            <label className="mb-1 block text-sm text-[var(--text-m)]">Blok tipi</label>
            <SearchableSelect
              value={newType}
              onChange={(v) => { setNewType(v); setNewTemplate(null) }}
              options={(catalog?.blockTypes ?? []).map((t) => ({ value: t.code, label: t.displayName }))}
              placeholder="Tip seç"
            />
          </div>
          {secilenTip && secilenTip.templates.length > 0 && (
            <div>
              <label className="mb-1 block text-sm text-[var(--text-m)]">Şablon</label>
              <SearchableSelect
                value={newTemplate}
                onChange={setNewTemplate}
                options={secilenTip.templates.map((t) => ({ value: t, label: t }))}
                placeholder="Şablon seç"
              />
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm text-[var(--text-m)]">Başlık (TR)</label>
            <Input value={newTitle} onChange={(e) => setNewTitle(e.target.value)} placeholder="Blok başlığı" />
          </div>
          <p className="text-xs text-[var(--text-s)]">
            Yerleşim: {(catalog?.placements ?? []).find((p) => p.code === placement)?.displayName ?? placement}.
            Öğeler ve kaynak yapılandırması detay sayfasında düzenlenir.
          </p>
        </div>
      </Modal>
    </div>
  )
}
