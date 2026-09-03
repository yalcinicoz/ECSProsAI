import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Package, ReceiptText, RefreshCw, Settings2, ChevronDown, DatabaseZap, GitCompareArrows } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import { ChannelForm, type Firm, type FirmPlatformWithFirm } from '../settings/ChannelsPage'
import type { PlatformType } from '../settings/PlatformTypesPage'
import { ReferenceSyncModal } from './ReferenceSyncModal'
import {
  HEALTH_COLOR,
  MP_BRAND,
  pickTr,
  storeHealth,
  useMarketplaceOverview,
  type MarketplaceStore,
} from './marketplaceOverview'

// ── Types ─────────────────────────────────────────────────────────────────────

// ── Paylaşılan yardımcılar (detay sayfası da kullanır) ────────────────────────

export function StoreLogo({ code, size = 34 }: { code: string; size?: number }) {
  const b = MP_BRAND[code] ?? { bg: 'var(--text-s)', label: code.slice(0, 2).toUpperCase() }
  return (
    <div
      className="shrink-0 rounded-lg flex items-center justify-center font-extrabold text-white"
      style={{ width: size, height: size, background: b.bg, fontSize: size * 0.34 }}
    >
      {b.label}
    </div>
  )
}

// ── Kart ──────────────────────────────────────────────────────────────────────

function StoreCard({
  store,
  showFirm,
  onEdit,
}: {
  store: MarketplaceStore
  showFirm: boolean
  onEdit: (s: MarketplaceStore) => void
}) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const health = storeHealth(store)
  const [syncOpen, setSyncOpen] = useState(false)
  const [syncMsg, setSyncMsg] = useState<{ ok: boolean; text: string } | null>(null)

  const syncAction = useMutation({
    mutationFn: async (op: 'stocks' | 'orders' | 'reconcile') => {
      const url =
        op === 'stocks'
          ? `/marketplaces/${store.id}/update-stocks`
          : op === 'reconcile'
            ? `/marketplaces/${store.id}/reconcile`
            : `/marketplaces/${store.id}/fetch-orders`
      const { data } = await api.post(url, op === 'stocks' ? {} : undefined)
      return { op, result: data.data }
    },
    onSuccess: ({ op, result }) => {
      setSyncMsg({
        ok: true,
        text:
          op === 'stocks'
            ? result.mode === 'batch'
              ? `Fiyat-stok gönderildi: ${result.submitted} varyant (${result.skippedUnchanged} değişmemiş atlandı)`
              : `Stok güncellendi: ${result.succeeded}/${result.requested}`
            : op === 'reconcile'
              ? `Mutabakat: ${result.compared} karşılaştırıldı · ${result.autoFixQueued} otomatik düzeltme · ${result.priceDriftIssues + result.missingOnMarketplace} sorun`
              : `Sipariş çekildi: ${result.fetchedCount ?? 0} yeni`,
      })
      queryClient.invalidateQueries({ queryKey: ['marketplaces-overview'] })
    },
    onError: (err: unknown) => {
      const e = err as { response?: { data?: { error?: string } } }
      setSyncMsg({ ok: false, text: e.response?.data?.error ?? 'İşlem başarısız oldu.' })
    },
  })

  const metrics: { v: number | string; l: string; color?: string }[] = [
    { v: store.uploadedListings, l: 'YÜKLÜ' },
    {
      v: store.isActive ? store.toUploadProducts : '—',
      l: 'YÜKLENECEK',
      color: store.isActive && store.toUploadProducts > 0 ? '#f59e0b' : undefined,
    },
    store.failedListings > 0
      ? { v: store.failedListings, l: 'HATALI', color: '#ef4444' }
      : { v: store.isActive ? store.openOrders : '—', l: 'AÇIK SİPARİŞ' },
  ]

  return (
    <div
      onClick={() => navigate(`/marketplaces/${store.id}`)}
      className={cn(
        'card cursor-pointer transition-all hover:shadow-md p-0 overflow-hidden',
        !store.isActive && 'opacity-60',
      )}
    >
      <div style={{ height: 4, background: HEALTH_COLOR[health.level] }} />
      <div className="px-4 pt-3 pb-2">
        <div className="flex items-center gap-2.5 mb-2.5">
          <StoreLogo code={store.platformTypeCode} />
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold truncate" style={{ color: 'var(--text)' }}>
              {pickTr(store.nameI18n, store.code)}
            </p>
            <p className="text-xs truncate" style={{ color: 'var(--text-s)' }}>
              {store.code}
            </p>
          </div>
          <div className="flex items-center gap-1.5 shrink-0">
            {showFirm && (
              <span
                className="text-[10px] font-bold px-2 py-0.5 rounded-full uppercase"
                style={{ background: '#eef2ff', color: '#4338ca', border: '1px solid #c7d2fe' }}
              >
                {pickTr(store.firmNameI18n, store.firmCode).split(' ')[0]}
              </span>
            )}
            <Badge variant={store.isActive ? 'success' : 'neutral'}>
              {store.isActive ? 'Aktif' : 'Pasif'}
            </Badge>
          </div>
        </div>

        <div className="flex items-center gap-1.5 text-xs mb-2.5" style={{ color: 'var(--text-m)' }}>
          <span
            className="w-2 h-2 rounded-full shrink-0"
            style={{ background: HEALTH_COLOR[health.level] }}
          />
          {syncMsg ? (
            <span style={{ color: syncMsg.ok ? 'var(--brand)' : '#ef4444' }}>{syncMsg.text}</span>
          ) : (
            <span
              style={
                health.level === 'err'
                  ? { color: '#ef4444', fontWeight: 600 }
                  : health.level === 'warn'
                    ? { color: '#b45309', fontWeight: 600 }
                    : undefined
              }
            >
              {health.text}
            </span>
          )}
        </div>

        <div
          className="grid grid-cols-3 rounded-lg overflow-hidden mb-2.5"
          style={{ border: '1px solid var(--border)' }}
        >
          {metrics.map((m, i) => (
            <div
              key={i}
              className="py-1.5 text-center"
              style={{
                background: 'var(--surface2)',
                borderRight: i < 2 ? '1px solid var(--border)' : undefined,
              }}
            >
              <p className="text-[15px] font-bold" style={{ color: m.color ?? 'var(--text)' }}>
                {typeof m.v === 'number' ? m.v.toLocaleString('tr-TR') : m.v}
              </p>
              <p className="text-[10px] font-semibold" style={{ color: 'var(--text-s)' }}>
                {m.l}
              </p>
            </div>
          ))}
        </div>

        <div className="flex gap-1.5 pb-1.5" onClick={(e) => e.stopPropagation()}>
          <button
            onClick={() => navigate(`/marketplaces/${store.id}?tab=urunler`)}
            className="flex-1 flex items-center justify-center gap-1 text-xs font-semibold py-1.5 rounded-lg transition-colors hover:opacity-80"
            style={{ border: '1px solid var(--border)', color: 'var(--text-m)' }}
          >
            <Package size={12} /> Ürünler
          </button>
          <button
            onClick={() => navigate(`/marketplaces/${store.id}?tab=siparisler`)}
            className="flex-1 flex items-center justify-center gap-1 text-xs font-semibold py-1.5 rounded-lg transition-colors hover:opacity-80"
            style={{ border: '1px solid var(--border)', color: 'var(--text-m)' }}
          >
            <ReceiptText size={12} /> Siparişler
          </button>
          <div className="relative flex-1">
            <button
              onClick={() => setSyncOpen((o) => !o)}
              disabled={syncAction.isPending || !store.isActive}
              className="w-full flex items-center justify-center gap-1 text-xs font-semibold py-1.5 rounded-lg transition-colors hover:opacity-80 disabled:opacity-40"
              style={{
                border: '1px solid var(--brand-b)',
                color: 'var(--brand)',
                background: 'var(--brand-bg)',
              }}
            >
              <RefreshCw size={12} className={syncAction.isPending ? 'animate-spin' : ''} />
              Senkron <ChevronDown size={11} />
            </button>
            {syncOpen && (
              <>
                <div className="fixed inset-0 z-10" onClick={() => setSyncOpen(false)} />
                <div
                  className="absolute right-0 top-full mt-1 z-20 rounded-lg overflow-hidden shadow-lg min-w-[168px]"
                  style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}
                >
                  {[
                    { key: 'send', label: 'Ürünleri Gönder…' },
                    { key: 'stocks', label: 'Stok-Fiyat Güncelle' },
                    { key: 'orders', label: 'Siparişleri Çek' },
                    { key: 'reconcile', label: 'Mutabakat Çalıştır' },
                  ].map((it) => (
                    <button
                      key={it.key}
                      onClick={() => {
                        setSyncOpen(false)
                        if (it.key === 'send')
                          navigate(`/marketplaces/${store.id}?tab=urunler&durum=to_upload_ready`)
                        else syncAction.mutate(it.key as 'stocks' | 'orders' | 'reconcile')
                      }}
                      className="block w-full text-left text-xs px-3 py-2 transition-colors hover:opacity-70"
                      style={{ color: 'var(--text)' }}
                    >
                      {it.label}
                    </button>
                  ))}
                </div>
              </>
            )}
          </div>
          <button
            onClick={() => onEdit(store)}
            title="Mağaza ayarları"
            className="flex items-center justify-center text-xs font-semibold px-2.5 py-1.5 rounded-lg transition-colors hover:opacity-80"
            style={{ border: '1px solid var(--border)', color: 'var(--text-m)' }}
          >
            <Settings2 size={13} />
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Ana Sayfa ─────────────────────────────────────────────────────────────────

export function MarketplacesPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [selectedFirmId, setSelectedFirmId] = useState<string | 'all'>('all')
  const [search, setSearch] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [refSyncOpen, setRefSyncOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<FirmPlatformWithFirm | null>(null)

  const { data: stores = [], isLoading } = useMarketplaceOverview()

  const { data: firms = [] } = useQuery<Firm[]>({
    queryKey: ['firms'],
    queryFn: async () => (await api.get('/core/firms')).data.data ?? [],
    staleTime: 10 * 60 * 1000,
  })

  const { data: platformTypes = [] } = useQuery<PlatformType[]>({
    queryKey: ['platform-types'],
    queryFn: async () => (await api.get('/core/platform-types')).data.data ?? [],
    staleTime: 10 * 60 * 1000,
  })
  const marketplaceTypes = useMemo(
    () => platformTypes.filter((pt) => pt.isMarketplace && pt.isActive),
    [platformTypes],
  )

  // Kart düzenleme ChannelForm'la yapılır — hedefi FirmPlatform DTO'su olarak çek
  const { data: allPlatforms = [] } = useQuery<FirmPlatformWithFirm[]>({
    queryKey: ['firm-platforms-flat', firms.map((f) => f.id).join(',')],
    queryFn: async () => {
      const results = await Promise.all(
        firms.map(async (f) => {
          const { data } = await api.get(`/core/firms/${f.id}/platforms`)
          return (data.data ?? []).map((ch: FirmPlatformWithFirm) => ({
            ...ch,
            firmId: f.id,
            firmName: pickTr(f.nameI18n, f.code),
          }))
        }),
      )
      return results.flat()
    },
    enabled: firms.length > 0,
    staleTime: 2 * 60 * 1000,
  })

  const visible = useMemo(() => {
    let list = stores
    if (selectedFirmId !== 'all') list = list.filter((s) => s.firmId === selectedFirmId)
    if (search.trim()) {
      const t = search.trim().toLowerCase()
      list = list.filter(
        (s) =>
          pickTr(s.nameI18n, s.code).toLowerCase().includes(t) ||
          s.code.toLowerCase().includes(t) ||
          pickTr(s.platformTypeNameI18n, s.platformTypeCode).toLowerCase().includes(t),
      )
    }
    return list
  }, [stores, selectedFirmId, search])

  // Pazaryeri tipine göre grupla — grup içinde aktifler önce
  const groups = useMemo(() => {
    const map = new Map<string, { code: string; name: string; stores: MarketplaceStore[] }>()
    for (const s of visible) {
      const g = map.get(s.platformTypeCode) ?? {
        code: s.platformTypeCode,
        name: pickTr(s.platformTypeNameI18n, s.platformTypeCode),
        stores: [],
      }
      g.stores.push(s)
      map.set(s.platformTypeCode, g)
    }
    const arr = [...map.values()]
    for (const g of arr)
      g.stores.sort((a, b) => Number(b.isActive) - Number(a.isActive) || a.code.localeCompare(b.code))
    // aktif mağazası olan gruplar önce, sonra ada göre
    arr.sort(
      (a, b) =>
        Number(b.stores.some((s) => s.isActive)) - Number(a.stores.some((s) => s.isActive)) ||
        a.name.localeCompare(b.name, 'tr'),
    )
    return arr
  }, [visible])

  const totals = useMemo(
    () => ({
      count: visible.length,
      active: visible.filter((s) => s.isActive).length,
      uploaded: visible.reduce((t, s) => t + s.uploadedListings, 0),
      toUpload: visible.reduce((t, s) => t + (s.isActive ? s.toUploadProducts : 0), 0),
      failed: visible.reduce((t, s) => t + s.failedListings, 0),
      failedStores: visible.filter((s) => s.failedListings > 0).length,
      todayOrders: visible.reduce((t, s) => t + s.todayOrders, 0),
    }),
    [visible],
  )

  function openCreate() {
    setEditTarget(null)
    setModalOpen(true)
  }
  function openEdit(store: MarketplaceStore) {
    const target = allPlatforms.find((p) => p.id === store.id)
    if (!target) return
    setEditTarget(target)
    setModalOpen(true)
  }
  function closeModal() {
    setModalOpen(false)
    setEditTarget(null)
  }

  if (isLoading) return <PageSpinner />

  return (
    <div className="p-6">
      <div className="flex items-start justify-between mb-5 gap-3 flex-wrap">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>
            Pazaryerleri
          </h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            Pazaryeri mağazalarınız — ürün yükleme, sipariş takibi ve senkronizasyon
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button size="sm" variant="secondary" onClick={() => navigate('/marketplaces/eslestirme')}>
            <GitCompareArrows size={14} /> Eşleştirme
          </Button>
          <Button size="sm" variant="secondary" onClick={() => setRefSyncOpen(true)}>
            <DatabaseZap size={14} /> Referans Verisi
          </Button>
          <Button size="sm" onClick={openCreate}>
            <Plus size={14} /> Yeni Mağaza
          </Button>
        </div>
      </div>

      {/* Firma filtresi + arama */}
      <div className="flex items-center gap-1 flex-wrap mb-4">
        <button
          onClick={() => setSelectedFirmId('all')}
          className={cn(
            'px-3 py-1.5 rounded-xl text-sm font-medium transition-all',
            selectedFirmId === 'all' ? 'shadow-sm' : 'hover:opacity-80',
          )}
          style={
            selectedFirmId === 'all'
              ? { background: 'var(--brand)', color: '#fff' }
              : { background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }
          }
        >
          Tümü
        </button>
        {firms.map((f) => (
          <button
            key={f.id}
            onClick={() => setSelectedFirmId(f.id)}
            className={cn(
              'px-3 py-1.5 rounded-xl text-sm font-medium transition-all',
              selectedFirmId === f.id ? 'shadow-sm' : 'hover:opacity-80',
            )}
            style={
              selectedFirmId === f.id
                ? { background: 'var(--surface)', color: 'var(--text)', border: '1px solid var(--brand)' }
                : { background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }
            }
          >
            {pickTr(f.nameI18n, f.code).split(' ')[0]}
            <span
              className="ml-1.5 text-xs px-1.5 py-0.5 rounded-md font-semibold"
              style={{ background: 'var(--surface)', color: 'var(--text-s)', border: '1px solid var(--border)' }}
            >
              {stores.filter((s) => s.firmId === f.id).length}
            </span>
          </button>
        ))}
        <div className="ml-auto">
          <input
            className="inp"
            style={{ width: 220 }}
            placeholder="Mağaza ara…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </div>

      {/* Özet şeridi */}
      {stores.length > 0 && (
        <div className="grid grid-cols-2 md:grid-cols-5 gap-3 mb-6">
          {[
            { v: totals.count, l: 'Mağaza', s: `${totals.active} aktif · ${totals.count - totals.active} pasif` },
            { v: totals.uploaded, l: 'Yüklü Ürün', s: 'tüm mağazalar toplamı' },
            { v: totals.toUpload, l: 'Yüklenecek Ürün', s: 'kanalda açık, gönderilmemiş', c: totals.toUpload > 0 ? '#f59e0b' : undefined },
            { v: totals.failed, l: 'Senkron Hatası', s: totals.failed > 0 ? `${totals.failedStores} mağazada` : 'sorun yok', c: totals.failed > 0 ? '#ef4444' : undefined },
            { v: totals.todayOrders, l: 'Bugün Gelen Sipariş', s: 'tüm pazaryerleri' },
          ].map((t, i) => (
            <div key={i} className="card px-4 py-3">
              <p className="text-[22px] font-bold leading-7" style={{ color: t.c ?? 'var(--text)' }}>
                {t.v.toLocaleString('tr-TR')}
              </p>
              <p className="text-xs font-medium" style={{ color: 'var(--text-m)' }}>{t.l}</p>
              <p className="text-[11px] mt-0.5" style={{ color: 'var(--text-s)' }}>{t.s}</p>
            </div>
          ))}
        </div>
      )}

      {/* Gruplar */}
      {visible.length === 0 ? (
        <div className="card py-16 text-center">
          <p className="text-sm mb-1" style={{ color: 'var(--text-m)' }}>
            {stores.length === 0
              ? 'Henüz pazaryeri mağazası tanımlanmamış.'
              : 'Filtreye uyan mağaza yok.'}
          </p>
          {stores.length === 0 && (
            <>
              <p className="text-xs mb-4" style={{ color: 'var(--text-s)' }}>
                Trendyol, Hepsiburada, n11, Amazon, Çiçeksepeti veya Pazarama mağazanızı ekleyerek başlayın.
              </p>
              <Button size="sm" onClick={openCreate}>
                <Plus size={13} /> İlk Mağazayı Ekle
              </Button>
            </>
          )}
        </div>
      ) : (
        groups.map((g) => (
          <div key={g.code} className="mb-6">
            <div className="flex items-center gap-2.5 mb-2.5">
              <StoreLogo code={g.code} size={30} />
              <span className="text-[15px] font-bold" style={{ color: 'var(--text)' }}>{g.name}</span>
              <span className="text-xs" style={{ color: 'var(--text-s)' }}>
                {g.stores.length} mağaza · {g.stores.reduce((t, s) => t + s.uploadedListings, 0).toLocaleString('tr-TR')} yüklü ürün
              </span>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
              {g.stores.map((s) => (
                <StoreCard key={s.id} store={s} showFirm={firms.length > 1} onEdit={openEdit} />
              ))}
            </div>
          </div>
        ))
      )}

      <Modal
        open={modalOpen}
        onClose={closeModal}
        title={editTarget ? `Mağaza Ayarları — ${pickTr(editTarget.nameI18n, editTarget.code)}` : 'Yeni Pazaryeri Mağazası'}
        size="md"
        footer={null}
      >
        <ChannelForm
          platformTypes={editTarget ? platformTypes : marketplaceTypes}
          firms={firms}
          initialFirmId={selectedFirmId === 'all' ? undefined : selectedFirmId}
          target={editTarget}
          onClose={closeModal}
          onSuccess={() => {
            closeModal()
            queryClient.invalidateQueries({ queryKey: ['marketplaces-overview'] })
            queryClient.invalidateQueries({ queryKey: ['firm-platforms-flat'] })
          }}
        />
      </Modal>

      <ReferenceSyncModal open={refSyncOpen} onClose={() => setRefSyncOpen(false)} />
    </div>
  )
}
