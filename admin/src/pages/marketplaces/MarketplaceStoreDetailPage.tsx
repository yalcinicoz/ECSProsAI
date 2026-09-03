import { useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ChevronLeft, RefreshCw, ChevronDown, ExternalLink } from 'lucide-react'
import { cn } from '@/lib/utils'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Pagination } from '@/components/ui/Pagination'
import { PageSpinner } from '@/components/ui/Spinner'
import { ChannelForm, type Firm, type FirmPlatformWithFirm } from '../settings/ChannelsPage'
import type { PlatformType } from '../settings/PlatformTypesPage'
import { CompletionModal } from './CompletionModal'
import {
  StoreLogo,
} from './MarketplacesPage'
import {
  HEALTH_COLOR,
  pickTr,
  storeHealth,
  timeAgo,
  useMarketplaceOverview,
  type MarketplaceStore,
} from './marketplaceOverview'

// ── Tipler ────────────────────────────────────────────────────────────────────

interface ProductRow {
  kind: 'listing' | 'candidate'
  marketplaceProductId: string | null
  variantId: string | null
  productId: string
  productCode: string
  productName: string | null
  sku: string | null
  barcode: string | null
  variantCount: number
  externalId: string | null
  syncStatus: string | null
  marketplacePrice: number | null
  marketplaceStock: number | null
  lastSyncedAt: string | null
  lastSyncError: string | null
  readinessStatus?: string | null
  readinessLabels?: string[] | null
  lastErrorCode?: string | null
  suggestedCategoryExternalId?: string | null
  suggestedCategoryPath?: string | null
}

interface OrderRow {
  id: string
  orderNumber: string
  status: string
  paymentStatus: string
  grandTotal: number
  currencyCode: string
  createdAt: string
  recipientName: string | null
}

interface LogRow {
  id: string
  operationType: string
  status: string
  errorMessage: string | null
  durationMs: number
  createdAt: string
}

const TABS = [
  { key: 'genel', label: 'Genel Bakış' },
  { key: 'urunler', label: 'Ürünler' },
  { key: 'siparisler', label: 'Siparişler' },
  { key: 'senkron', label: 'Senkron Geçmişi' },
  { key: 'sorunlar', label: 'Sorunlar' },
  { key: 'ayarlar', label: 'Ayarlar' },
] as const

const ORDER_STATUS: Record<string, { label: string; variant: 'success' | 'warning' | 'danger' | 'info' | 'neutral' }> = {
  pending: { label: 'Bekliyor', variant: 'warning' },
  confirmed: { label: 'Onaylandı', variant: 'info' },
  processing: { label: 'Hazırlanıyor', variant: 'info' },
  shipped: { label: 'Kargoda', variant: 'info' },
  delivered: { label: 'Teslim Edildi', variant: 'success' },
  cancelled: { label: 'İptal', variant: 'danger' },
  returned: { label: 'İade', variant: 'neutral' },
}

const OP_LABEL: Record<string, string> = {
  sync_product: 'Ürün gönderimi',
  update_stock: 'Stok güncelleme',
  fetch_orders: 'Sipariş çekme',
}

const SYNC_PILL: Record<string, { label: string; cls: string }> = {
  synced: { label: 'Senkron', cls: 'bg' },
  pending: { label: 'Bekliyor', cls: 'ba' },
  failed: { label: 'Hatalı', cls: 'br' },
  deactivated: { label: 'Pasif', cls: 'bx' },
}

// ── Sayfa ─────────────────────────────────────────────────────────────────────

export function MarketplaceStoreDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const queryClient = useQueryClient()

  const activeTab = searchParams.get('tab') ?? 'genel'
  const { data: stores = [], isLoading } = useMarketplaceOverview()
  const store = stores.find((s) => s.id === id)

  const [syncOpen, setSyncOpen] = useState(false)
  const [syncMsg, setSyncMsg] = useState<{ ok: boolean; text: string } | null>(null)

  const syncAction = useMutation({
    mutationFn: async (op: 'stocks' | 'orders') => {
      const url =
        op === 'stocks' ? `/marketplaces/${id}/update-stocks` : `/marketplaces/${id}/fetch-orders`
      const { data } = await api.post(url, op === 'stocks' ? {} : undefined)
      return { op, result: data.data }
    },
    onSuccess: ({ op, result }) => {
      setSyncMsg({
        ok: true,
        text:
          op === 'stocks'
            ? `Stok güncellendi: ${result.succeeded}/${result.requested} başarılı`
            : `Sipariş çekme tamamlandı: ${result.fetchedCount ?? 0} yeni sipariş`,
      })
      queryClient.invalidateQueries({ queryKey: ['marketplaces-overview'] })
      queryClient.invalidateQueries({ queryKey: ['marketplace-logs', id] })
    },
    onError: (err: unknown) => {
      const e = err as { response?: { data?: { error?: string } } }
      setSyncMsg({ ok: false, text: e.response?.data?.error ?? 'İşlem başarısız oldu.' })
    },
  })

  if (isLoading) return <PageSpinner />
  if (!store)
    return (
      <div className="p-6">
        <p className="text-sm mb-3" style={{ color: 'var(--text-m)' }}>
          Mağaza bulunamadı — silinmiş veya pazaryeri mağazası olmayan bir kanal olabilir.
        </p>
        <Button size="sm" variant="secondary" onClick={() => navigate('/marketplaces')}>
          <ChevronLeft size={14} /> Pazaryerleri
        </Button>
      </div>
    )

  const health = storeHealth(store)

  function setTab(tab: string) {
    const next = new URLSearchParams(searchParams)
    next.set('tab', tab)
    setSearchParams(next, { replace: true })
  }

  return (
    <div className="p-6 max-w-6xl">
      <button
        onClick={() => navigate('/marketplaces')}
        className="flex items-center gap-1 text-sm mb-4 hover:opacity-80"
        style={{ color: 'var(--text-s)' }}
      >
        <ChevronLeft size={14} /> Pazaryerleri
      </button>

      {/* Başlık */}
      <div className="flex flex-wrap items-center gap-3 mb-1">
        <StoreLogo code={store.platformTypeCode} size={42} />
        <div className="min-w-0">
          <h1 className="text-xl font-bold truncate" style={{ color: 'var(--text)' }}>
            {pickTr(store.nameI18n, store.code)}
          </h1>
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>
            {pickTr(store.platformTypeNameI18n, store.platformTypeCode)} · {store.code} ·{' '}
            {pickTr(store.firmNameI18n, store.firmCode)}
          </p>
        </div>
        <Badge variant={store.isActive ? 'success' : 'neutral'}>
          {store.isActive ? 'Aktif' : 'Pasif'}
        </Badge>
        <div className="ml-auto flex flex-wrap items-center gap-2">
          <Button size="sm" variant="secondary" onClick={() => setTab('siparisler')}>
            Siparişler ({store.openOrders})
          </Button>
          <div className="relative">
            <Button
              size="sm"
              onClick={() => setSyncOpen((o) => !o)}
              loading={syncAction.isPending}
              disabled={!store.isActive}
            >
              <RefreshCw size={13} /> Senkronize Et <ChevronDown size={12} />
            </Button>
            {syncOpen && (
              <>
                <div className="fixed inset-0 z-10" onClick={() => setSyncOpen(false)} />
                <div
                  className="absolute right-0 top-full mt-1 z-20 rounded-lg overflow-hidden shadow-lg min-w-[180px]"
                  style={{ background: 'var(--surface)', border: '1px solid var(--border)' }}
                >
                  {[
                    { key: 'send', label: 'Ürünleri Gönder…' },
                    { key: 'stocks', label: 'Stok-Fiyat Güncelle' },
                    { key: 'orders', label: 'Siparişleri Çek' },
                  ].map((it) => (
                    <button
                      key={it.key}
                      onClick={() => {
                        setSyncOpen(false)
                        if (it.key === 'send') {
                          const next = new URLSearchParams(searchParams)
                          next.set('tab', 'urunler')
                          next.set('durum', 'to_upload')
                          setSearchParams(next, { replace: true })
                        } else syncAction.mutate(it.key as 'stocks' | 'orders')
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
        </div>
      </div>

      {/* Sağlık satırı */}
      <div className="flex items-center gap-1.5 text-sm mb-4" style={{ color: 'var(--text-m)' }}>
        <span className="w-2 h-2 rounded-full shrink-0" style={{ background: HEALTH_COLOR[health.level] }} />
        {syncMsg ? (
          <span style={{ color: syncMsg.ok ? 'var(--brand)' : '#ef4444' }}>{syncMsg.text}</span>
        ) : (
          <span>{health.text}</span>
        )}
      </div>

      {/* Sekmeler */}
      <div className="tab-scroll mb-5" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map((t) => {
          const count =
            t.key === 'urunler'
              ? store.uploadedListings + store.toUploadProducts
              : t.key === 'siparisler'
                ? store.openOrders
                : t.key === 'sorunlar'
                  ? store.openIssues
                  : undefined
          return (
            <button
              key={t.key}
              onClick={() => setTab(t.key)}
              className={cn('stab', activeTab === t.key && 'active')}
            >
              {t.label}
              {count !== undefined && count > 0 && (
                <span
                  className="ml-1.5 text-xs px-1.5 py-0.5 rounded-full"
                  style={{ background: 'var(--surface2)', color: 'var(--text-s)' }}
                >
                  {count.toLocaleString('tr-TR')}
                </span>
              )}
            </button>
          )
        })}
      </div>

      {activeTab === 'genel' && <OverviewTab store={store} onGoTab={setTab} />}
      {activeTab === 'urunler' && <ProductsTab store={store} />}
      {activeTab === 'siparisler' && <OrdersTab store={store} />}
      {activeTab === 'senkron' && <LogsTab storeId={store.id} />}
      {activeTab === 'sorunlar' && <IssuesTab store={store} />}
      {activeTab === 'ayarlar' && <SettingsTab store={store} />}
    </div>
  )
}

// ── Genel Bakış ───────────────────────────────────────────────────────────────

function OverviewTab({ store, onGoTab }: { store: MarketplaceStore; onGoTab: (t: string) => void }) {
  const navigate = useNavigate()
  const { data: logs } = useQuery<{ items: LogRow[] }>({
    queryKey: ['marketplace-logs', store.id, 1, 'mini'],
    queryFn: async () =>
      (await api.get(`/marketplaces/${store.id}/logs`, { params: { page: 1, pageSize: 5 } })).data.data,
  })

  const tiles = [
    { v: store.uploadedListings, l: 'Yüklü', tab: 'urunler' },
    { v: store.toUploadProducts, l: 'Yüklenecek', tab: 'urunler', c: store.toUploadProducts > 0 ? '#f59e0b' : undefined },
    { v: store.pendingListings, l: 'Bekleyen', tab: 'urunler' },
    { v: store.failedListings, l: 'Hatalı', tab: 'urunler', c: store.failedListings > 0 ? '#ef4444' : undefined },
    { v: store.openOrders, l: 'Açık Sipariş', tab: 'siparisler' },
    { v: store.todayOrders, l: 'Bugün Gelen', tab: 'siparisler' },
  ]

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-3">
        {tiles.map((t, i) => (
          <button
            key={i}
            onClick={() => onGoTab(t.tab)}
            className="card px-4 py-3 text-left transition-all hover:shadow-md"
          >
            <p className="text-[20px] font-bold" style={{ color: t.c ?? 'var(--text)' }}>
              {t.v.toLocaleString('tr-TR')}
            </p>
            <p className="text-xs" style={{ color: 'var(--text-s)' }}>{t.l}</p>
          </button>
        ))}
      </div>

      {/* Bağlantı durumu */}
      <div className="card p-4">
        <h2 className="text-sm font-semibold mb-2" style={{ color: 'var(--text)' }}>Bağlantı</h2>
        {store.integrationId ? (
          <p className="text-sm" style={{ color: 'var(--text-m)' }}>
            <span style={{ color: 'var(--brand)', fontWeight: 600 }}>Sözleşme bağlı</span> — servis:{' '}
            <code className="font-mono">{store.serviceCode}</code>
            {store.lastSyncAt && <> · son senkron {timeAgo(store.lastSyncAt)}</>}
          </p>
        ) : (
          <p className="text-sm" style={{ color: '#b45309' }}>
            Bu mağazaya bağlı aktif bir pazaryeri sözleşmesi yok. Senkron çalışmaz — firma detayından
            pazaryeri servisi için sözleşme ekleyin.
          </p>
        )}
        <button
          onClick={() => navigate(`/settings/firms/${store.firmId}`)}
          className="mt-2 flex items-center gap-1 text-xs font-semibold hover:opacity-80"
          style={{ color: 'var(--brand)' }}
        >
          Firma sözleşmeleri <ExternalLink size={11} />
        </button>
      </div>

      {/* Son işlemler */}
      <div className="card p-4">
        <div className="flex items-center justify-between mb-2">
          <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Son İşlemler</h2>
          <button onClick={() => onGoTab('senkron')} className="text-xs font-semibold hover:opacity-80" style={{ color: 'var(--brand)' }}>
            Tümü →
          </button>
        </div>
        {!logs?.items?.length ? (
          <p className="text-sm py-4 text-center" style={{ color: 'var(--text-s)' }}>Henüz senkron işlemi yapılmamış.</p>
        ) : (
          logs.items.map((l) => (
            <div key={l.id} className="flex items-center gap-3 py-1.5 text-sm" style={{ borderBottom: '1px solid var(--border)' }}>
              <span className="w-32 shrink-0 text-xs" style={{ color: 'var(--text-s)' }}>
                {new Date(l.createdAt).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' })}
              </span>
              <span style={{ color: 'var(--text)' }}>{OP_LABEL[l.operationType] ?? l.operationType}</span>
              <span className={cn('badge ml-auto', l.status === 'success' ? 'bg' : 'br')}>
                {l.status === 'success' ? 'Başarılı' : 'Hata'}
              </span>
            </div>
          ))
        )}
      </div>
    </div>
  )
}

// ── Ürünler ───────────────────────────────────────────────────────────────────

const PRODUCT_FILTERS = [
  { key: 'synced', label: 'Yüklü' },
  { key: 'to_upload_ready', label: 'Hazır' },
  { key: 'to_upload_missing', label: 'Eksik' },
  { key: 'pending', label: 'Bekleyen' },
  { key: 'failed', label: 'Hatalı' },
  { key: 'deactivated', label: 'Pasif' },
] as const

function ProductsTab({ store }: { store: MarketplaceStore }) {
  const [searchParams, setSearchParams] = useSearchParams()
  const queryClient = useQueryClient()
  const status = searchParams.get('durum') ?? 'synced'
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [actionMsg, setActionMsg] = useState<{ ok: boolean; text: string } | null>(null)
  const PAGE_SIZE = 25

  // Denetim (readiness) sayıları — Hazır/Eksik çipleri; denetlenmemişler Eksik'e sayılır
  const { data: readinessCounts } = useQuery<{ marketplace: string; ready: number; missing: number; unchecked: number }>({
    queryKey: ['marketplace-readiness-counts', store.id],
    queryFn: async () => (await api.get(`/marketplaces/${store.id}/readiness-counts`)).data.data,
  })

  const counts: Record<string, number> = {
    synced: store.uploadedListings,
    to_upload_ready: readinessCounts?.ready ?? 0,
    to_upload_missing: (readinessCounts?.missing ?? 0) + (readinessCounts?.unchecked ?? 0),
    pending: store.pendingListings,
    failed: store.failedListings,
    deactivated: store.deactivatedListings,
  }

  const { data, isLoading } = useQuery<{ items: ProductRow[]; totalCount: number }>({
    queryKey: ['marketplace-products', store.id, status, appliedSearch, page],
    queryFn: async () =>
      (
        await api.get(`/marketplaces/${store.id}/products`, {
          params: { status, search: appliedSearch || undefined, page, pageSize: PAGE_SIZE },
        })
      ).data.data,
  })
  const rows = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const totalPages = Math.ceil(totalCount / PAGE_SIZE)
  const isCandidate = status.startsWith('to_upload')
  const [completionIds, setCompletionIds] = useState<string[]>([])

  const recompute = useMutation({
    mutationFn: async () => {
      if (!readinessCounts?.marketplace) throw new Error('Pazaryeri kodu yok')
      return (await api.post(`/marketplaces/mapping/readiness/recompute?marketplace=${readinessCounts.marketplace}`)).data.data
    },
    onSuccess: (result) => {
      setActionMsg({ ok: true, text: `Denetim bitti: ${result.ready} hazır, ${result.missing} eksik (${result.total} ürün).` })
      queryClient.invalidateQueries({ queryKey: ['marketplace-products', store.id] })
      queryClient.invalidateQueries({ queryKey: ['marketplace-readiness-counts', store.id] })
    },
    onError: (err: unknown) => {
      const e = err as { response?: { data?: { error?: string } } }
      setActionMsg({ ok: false, text: e.response?.data?.error ?? 'Denetim çalıştırılamadı.' })
    },
  })

  function switchStatus(key: string) {
    const next = new URLSearchParams(searchParams)
    next.set('durum', key)
    setSearchParams(next, { replace: true })
    setPage(1)
    setSelected(new Set())
    setActionMsg(null)
  }

  const rowKey = (r: ProductRow) => (isCandidate ? r.productId : (r.variantId ?? r.productId))
  const allSelected = rows.length > 0 && rows.every((r) => selected.has(rowKey(r)))

  // Kategori çakışması reddi: pazaryerinin işaret ettiği kategoriye tek tıkla istisna yaz
  // (genel eşlemeye dokunmaz) ve ürünü yeniden gönder (K4/K5 akışı).
  const applySuggestion = useMutation({
    mutationFn: async (r: ProductRow) => {
      const path = r.suggestedCategoryPath ?? r.suggestedCategoryExternalId!
      await api.put('/marketplaces/mapping/completion', {
        marketplace: store.platformTypeCode,
        productIds: [r.productId],
        category: {
          externalId: r.suggestedCategoryExternalId,
          name: path.split(' > ').pop(),
          path,
          source: 'rejection',
        },
      })
      return (await api.post(`/marketplaces/${store.id}/sync-products`, { productIds: [r.productId] })).data.data
    },
    onSuccess: (result) => {
      setActionMsg({ ok: true, text: `İstisna yazıldı ve ürün yeniden gönderildi (${result.submitted ?? 0} varyant).` })
      queryClient.invalidateQueries({ queryKey: ['marketplace-products', store.id] })
      queryClient.invalidateQueries({ queryKey: ['marketplace-batches', store.id] })
    },
    onError: (err: unknown) => {
      const e = err as { response?: { data?: { error?: string } } }
      setActionMsg({ ok: false, text: e.response?.data?.error ?? 'İstisna uygulanamadı.' })
    },
  })

  const sendAction = useMutation({
    mutationFn: async () => {
      const ids = [...selected]
      const body = isCandidate ? { productIds: ids } : { variantIds: ids }
      return (await api.post(`/marketplaces/${store.id}/sync-products`, body)).data.data
    },
    onSuccess: (result) => {
      if (result.mode === 'batch') {
        const skipped = [
          result.skippedNotReady > 0 ? `${result.skippedNotReady} eksik/denetimsiz` : null,
          result.skippedUnchanged > 0 ? `${result.skippedUnchanged} değişmemiş` : null,
          result.skippedNoBarcode > 0 ? `${result.skippedNoBarcode} barkodsuz` : null,
        ].filter(Boolean).join(', ')
        setActionMsg({
          ok: true,
          text:
            `${result.submitted} varyant ${result.batchCount} pakette gönderildi — sonuç arka planda sorgulanıyor (Senkron Geçmişi).` +
            (skipped ? ` Atlanan: ${skipped}.` : ''),
        })
      } else {
        setActionMsg({
          ok: result.failed === 0,
          text:
            `Gönderim bitti: ${result.succeeded}/${result.requested} başarılı` +
            (result.failed > 0 ? ` · ${result.failed} hata — ${result.errors?.[0] ?? ''}` : ''),
        })
      }
      setSelected(new Set())
      queryClient.invalidateQueries({ queryKey: ['marketplace-products', store.id] })
      queryClient.invalidateQueries({ queryKey: ['marketplace-batches', store.id] })
      queryClient.invalidateQueries({ queryKey: ['marketplaces-overview'] })
    },
    onError: (err: unknown) => {
      const e = err as { response?: { data?: { error?: string } } }
      setActionMsg({ ok: false, text: e.response?.data?.error ?? 'Gönderim başarısız oldu.' })
    },
  })

  return (
    <div>
      <div className="flex items-center gap-1.5 flex-wrap mb-3">
        {PRODUCT_FILTERS.map((f) => (
          <button
            key={f.key}
            onClick={() => switchStatus(f.key)}
            className={cn(
              'px-3 py-1.5 rounded-xl text-[13px] font-medium transition-all',
              status === f.key ? 'shadow-sm' : 'hover:opacity-80',
            )}
            style={
              status === f.key
                ? { background: 'var(--brand)', color: '#fff' }
                : {
                    background: 'var(--surface2)',
                    color: f.key === 'failed' && counts.failed > 0 ? '#ef4444' : 'var(--text-m)',
                    border: '1px solid var(--border)',
                  }
            }
          >
            {f.label} ({(counts[f.key] ?? 0).toLocaleString('tr-TR')})
          </button>
        ))}
        <div className="ml-auto flex items-center gap-2">
          <input
            className="inp"
            style={{ width: 200 }}
            placeholder="Ürün / barkod ara…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                setAppliedSearch(search.trim())
                setPage(1)
              }
            }}
          />
          {isCandidate ? (
            <Button size="sm" variant="secondary" onClick={() => recompute.mutate()} loading={recompute.isPending}>
              Denetle
            </Button>
          ) : null}
          {selected.size > 0 && status === 'to_upload_missing' ? (
            <Button size="sm" variant="secondary" onClick={() => setCompletionIds([...selected])}>
              Toplu Tamamla ({selected.size})
            </Button>
          ) : null}
          {selected.size > 0 && (
            <Button size="sm" onClick={() => sendAction.mutate()} loading={sendAction.isPending}>
              Seçilenleri Gönder ({selected.size})
            </Button>
          )}
        </div>
      </div>

      {actionMsg && (
        <p className="text-sm mb-2" style={{ color: actionMsg.ok ? 'var(--brand)' : '#ef4444' }}>
          {actionMsg.text}
        </p>
      )}

      <div className="card p-0 overflow-hidden">
        <div className="tbl-wrap">
          <table className="w-full text-sm">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)' }}>
                <th className="px-3 py-2.5 w-8">
                  <input
                    type="checkbox"
                    className="w-3.5 h-3.5 accent-[var(--brand)]"
                    checked={allSelected}
                    onChange={() =>
                      setSelected(allSelected ? new Set() : new Set(rows.map(rowKey)))
                    }
                  />
                </th>
                <th className="px-3 py-2.5 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Ürün</th>
                <th className="px-3 py-2.5 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Barkod</th>
                <th className="px-3 py-2.5 text-right text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>PY Fiyatı</th>
                <th className="px-3 py-2.5 text-right text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>PY Stok</th>
                <th className="px-3 py-2.5 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Durum</th>
                <th className="px-3 py-2.5 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Son Senkron</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
              )}
              {!isLoading && rows.length === 0 && (
                <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                  {isCandidate ? 'Yüklenecek ürün yok — kanalda açık tüm ürünler gönderilmiş.' : 'Kayıt bulunamadı.'}
                </td></tr>
              )}
              {rows.map((r) => {
                const key = rowKey(r)
                const pill = r.syncStatus ? SYNC_PILL[r.syncStatus] : null
                return (
                  <tr key={key} className="trow" style={{ borderBottom: '1px solid var(--border)' }}>
                    <td className="px-3 py-2">
                      <input
                        type="checkbox"
                        className="w-3.5 h-3.5 accent-[var(--brand)]"
                        checked={selected.has(key)}
                        onChange={() =>
                          setSelected((s) => {
                            const n = new Set(s)
                            if (n.has(key)) n.delete(key)
                            else n.add(key)
                            return n
                          })
                        }
                      />
                    </td>
                    <td className="px-3 py-2">
                      <p className="font-medium" style={{ color: 'var(--text)' }}>
                        {r.productName ?? r.productCode}
                      </p>
                      <p className="text-xs" style={{ color: 'var(--text-s)' }}>
                        {r.productCode}
                        {r.sku && r.sku !== r.productCode && ` · ${r.sku}`}
                        {isCandidate && ` · ${r.variantCount} varyant`}
                        {r.externalId && ` · PY: ${r.externalId}`}
                      </p>
                      {r.lastSyncError && (
                        <p className="text-xs" style={{ color: '#ef4444' }}>{r.lastSyncError}</p>
                      )}
                      <span>
                        {r.syncStatus === 'failed' && r.suggestedCategoryExternalId ? (
                          <button
                            onClick={() => applySuggestion.mutate(r)}
                            disabled={applySuggestion.isPending}
                            className="text-[11px] font-semibold mt-0.5 hover:opacity-75 disabled:opacity-40"
                            style={{ color: 'var(--brand)' }}
                            title={`Pazaryerinin beklediği kategori: ${r.suggestedCategoryPath ?? r.suggestedCategoryExternalId}`}
                          >
                            → "{(r.suggestedCategoryPath ?? r.suggestedCategoryExternalId)!.split(' > ').pop()}" kategorisine istisna yaz + yeniden gönder
                          </button>
                        ) : null}
                      </span>
                      <span>
                        {r.readinessLabels && r.readinessLabels.length > 0 ? (
                          <span className="flex items-center gap-1 flex-wrap mt-0.5">
                            {r.readinessLabels.slice(0, 4).map((l, i) => (
                              <span key={i} className="badge bg-amber-50 text-amber-700">{l}</span>
                            ))}
                            {r.readinessLabels.length > 4 ? (
                              <span className="text-[10px]" style={{ color: 'var(--text-s)' }}>
                                +{r.readinessLabels.length - 4}
                              </span>
                            ) : null}
                          </span>
                        ) : null}
                      </span>
                    </td>
                    <td className="px-3 py-2" style={{ color: 'var(--text-m)' }}>{r.barcode ?? '—'}</td>
                    <td className="px-3 py-2 text-right" style={{ color: 'var(--text)' }}>
                      {r.marketplacePrice != null
                        ? `${r.marketplacePrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺`
                        : '—'}
                    </td>
                    <td className="px-3 py-2 text-right" style={{ color: 'var(--text-m)' }}>
                      {r.marketplaceStock ?? '—'}
                    </td>
                    <td className="px-3 py-2">
                      {pill ? (
                        <span className={cn('badge', pill.cls)}>{pill.label}</span>
                      ) : r.readinessStatus === 'ready' ? (
                        <span className="badge bg-emerald-50 text-emerald-700">Hazır</span>
                      ) : (
                        <span className="flex items-center gap-1.5">
                          <span className="badge bg-amber-50 text-amber-700">Eksik</span>
                          <button
                            onClick={() => setCompletionIds([r.productId])}
                            className="text-[11px] font-semibold hover:opacity-75"
                            style={{ color: 'var(--brand)' }}
                          >
                            Tamamla
                          </button>
                        </span>
                      )}
                    </td>
                    <td className="px-3 py-2 text-xs" style={{ color: 'var(--text-s)' }}>
                      {r.lastSyncedAt ? timeAgo(r.lastSyncedAt) : '—'}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </div>
      <div className="mt-3">
        <Pagination page={page} totalPages={totalPages} totalCount={totalCount} pageSize={PAGE_SIZE} onChange={setPage} />
      </div>

      <CompletionModal
        open={completionIds.length > 0}
        onClose={() => setCompletionIds([])}
        marketplace={readinessCounts?.marketplace ?? store.platformTypeCode}
        productIds={completionIds}
        onSaved={() => {
          setSelected(new Set())
          queryClient.invalidateQueries({ queryKey: ['marketplace-products', store.id] })
          queryClient.invalidateQueries({ queryKey: ['marketplace-readiness-counts', store.id] })
        }}
      />
    </div>
  )
}

// ── Siparişler ────────────────────────────────────────────────────────────────

const ORDER_TABS = [
  { key: 'open', label: 'Açık', statuses: 'pending,confirmed,processing' },
  { key: 'all', label: 'Tümü', statuses: '' },
  { key: 'done', label: 'Tamamlanan', statuses: 'delivered' },
  { key: 'cancelled', label: 'İptal/İade', statuses: 'cancelled,returned' },
] as const

function OrdersTab({ store }: { store: MarketplaceStore }) {
  const navigate = useNavigate()
  const [tab, setTab] = useState<string>('open')
  const [page, setPage] = useState(1)
  const PAGE_SIZE = 20
  const statuses = ORDER_TABS.find((t) => t.key === tab)?.statuses ?? ''

  const { data, isLoading } = useQuery<{ items: OrderRow[]; totalCount: number }>({
    queryKey: ['marketplace-orders', store.id, tab, page],
    queryFn: async () =>
      (
        await api.get('/orders', {
          params: {
            firmPlatformId: store.id,
            statuses: statuses || undefined,
            page,
            pageSize: PAGE_SIZE,
          },
        })
      ).data.data,
  })
  const rows = data?.items ?? []
  const totalCount = data?.totalCount ?? 0

  return (
    <div>
      <div className="flex items-center gap-1.5 mb-3">
        {ORDER_TABS.map((t) => (
          <button
            key={t.key}
            onClick={() => { setTab(t.key); setPage(1) }}
            className={cn('px-3 py-1.5 rounded-xl text-[13px] font-medium transition-all', tab === t.key ? 'shadow-sm' : 'hover:opacity-80')}
            style={
              tab === t.key
                ? { background: 'var(--brand)', color: '#fff' }
                : { background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }
            }
          >
            {t.label}
          </button>
        ))}
      </div>

      <div className="card p-0 overflow-hidden">
        <div className="tbl-wrap">
          <table className="w-full text-sm">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)' }}>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Sipariş No</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Müşteri</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Durum</th>
                <th className="px-4 py-2.5 text-right text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Tutar</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Tarih</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr><td colSpan={5} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
              )}
              {!isLoading && rows.length === 0 && (
                <tr><td colSpan={5} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Bu mağazaya ait sipariş bulunamadı.</td></tr>
              )}
              {rows.map((o) => {
                const st = ORDER_STATUS[o.status] ?? { label: o.status, variant: 'neutral' as const }
                return (
                  <tr
                    key={o.id}
                    onClick={() => navigate(`/orders/${o.id}`)}
                    className="trow cursor-pointer"
                    style={{ borderBottom: '1px solid var(--border)' }}
                  >
                    <td className="px-4 py-2.5 font-medium" style={{ color: 'var(--text)' }}>{o.orderNumber}</td>
                    <td className="px-4 py-2.5" style={{ color: 'var(--text-m)' }}>{o.recipientName ?? '—'}</td>
                    <td className="px-4 py-2.5"><Badge variant={st.variant}>{st.label}</Badge></td>
                    <td className="px-4 py-2.5 text-right" style={{ color: 'var(--text)' }}>
                      {o.grandTotal.toLocaleString('tr-TR', { minimumFractionDigits: 2 })}{' '}
                      {o.currencyCode === 'TRY' ? '₺' : o.currencyCode}
                    </td>
                    <td className="px-4 py-2.5 text-xs" style={{ color: 'var(--text-s)' }}>
                      {new Date(o.createdAt).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' })}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </div>
      <div className="mt-3">
        <Pagination page={page} totalPages={Math.ceil(totalCount / PAGE_SIZE)} totalCount={totalCount} pageSize={PAGE_SIZE} onChange={setPage} />
      </div>
    </div>
  )
}

// ── Senkron Geçmişi ───────────────────────────────────────────────────────────

// ── Gönderim paketleri (F4 — asenkron batch takibi) ──────────────────────────

interface BatchRow {
  id: string
  externalBatchId: string | null
  batchType: string
  status: string
  itemCount: number
  resolvedCount: number
  successCount: number
  failedCount: number
  submittedAt: string
  lastPolledAt: string | null
  nextPollAt: string | null
  error: string | null
  failedItems: { barcode: string; errorCode: string | null; errorRaw: string | null }[]
}

const BATCH_STATUS: Record<string, { label: string; cls: string }> = {
  submitted: { label: 'Gönderildi', cls: 'bg-amber-50 text-amber-700' },
  polling: { label: 'Sonuç bekleniyor', cls: 'bg-amber-50 text-amber-700' },
  completed: { label: 'Tamamlandı', cls: 'bg-emerald-50 text-emerald-700' },
  completed_with_errors: { label: 'Hatalarla bitti', cls: 'bg-red-50 text-red-600' },
  timed_out: { label: 'Zaman aşımı', cls: 'bg-red-50 text-red-600' },
  failed: { label: 'Başarısız', cls: 'bg-red-50 text-red-600' },
}

function BatchesBlock({ storeId }: { storeId: string }) {
  const queryClient = useQueryClient()
  const { data: batches = [] } = useQuery<BatchRow[]>({
    queryKey: ['marketplace-batches', storeId],
    queryFn: async () => (await api.get(`/marketplaces/${storeId}/batches`)).data.data ?? [],
    refetchInterval: (q) =>
      q.state.data?.some((b) => b.status === 'submitted' || b.status === 'polling') ? 5000 : false,
  })

  const pollNow = useMutation({
    mutationFn: async () => api.post('/marketplaces/batches/poll-now'),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['marketplace-batches', storeId] })
      queryClient.invalidateQueries({ queryKey: ['marketplace-products', storeId] })
    },
  })

  if (batches.length === 0) return null
  const hasOpen = batches.some((b) => b.status === 'submitted' || b.status === 'polling')

  return (
    <div className="card p-0 overflow-hidden mb-4">
      <div className="flex items-center gap-2 px-4 py-2.5" style={{ borderBottom: '1px solid var(--border)' }}>
        <p className="text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>
          Gönderim Paketleri
        </p>
        <span className="ml-auto">
          {hasOpen ? (
            <Button size="sm" variant="ghost" onClick={() => pollNow.mutate()} loading={pollNow.isPending}>
              <RefreshCw size={12} /> Şimdi Sorgula
            </Button>
          ) : null}
        </span>
      </div>
      <table className="w-full text-sm">
        <tbody>
          {batches.map((b) => {
            const st = BATCH_STATUS[b.status] ?? { label: b.status, cls: 'bb' }
            return (
              <tr key={b.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-2 text-xs" style={{ color: 'var(--text-s)' }}>
                  {new Date(b.submittedAt).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' })}
                </td>
                <td className="px-4 py-2 text-xs" style={{ color: 'var(--text-m)' }}>
                  {b.batchType === 'product_upsert' ? 'Ürün gönderimi' : 'Stok-fiyat'}
                  {b.externalBatchId ? (
                    <span className="block truncate max-w-[160px]" title={b.externalBatchId} style={{ color: 'var(--text-s)' }}>
                      {b.externalBatchId}
                    </span>
                  ) : null}
                </td>
                <td className="px-4 py-2">
                  <span className={cn('badge', st.cls)}>{st.label}</span>
                </td>
                <td className="px-4 py-2 text-xs tabular-nums" style={{ color: 'var(--text-m)' }}>
                  {b.resolvedCount}/{b.itemCount} çözüldü
                  {b.failedCount > 0 ? <span style={{ color: '#ef4444' }}> · {b.failedCount} hata</span> : null}
                </td>
                <td className="px-4 py-2 text-xs max-w-[320px]" style={{ color: 'var(--text-s)' }}>
                  {b.error ? <span style={{ color: '#ef4444' }}>{b.error}</span> :
                    b.failedItems.length > 0 ? (
                      <span className="truncate block" title={b.failedItems.map((f) => `${f.barcode}: ${f.errorRaw}`).join('\n')}>
                        {b.failedItems[0].barcode}: {b.failedItems[0].errorRaw}
                        {b.failedItems.length > 1 ? ` (+${b.failedItems.length - 1})` : ''}
                      </span>
                    ) : b.status === 'polling' && b.nextPollAt ? `sonraki sorgu ${timeAgo(b.nextPollAt).replace(' önce', '')} içinde` : ''}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

function LogsTab({ storeId }: { storeId: string }) {
  const [page, setPage] = useState(1)
  const [op, setOp] = useState('')
  const PAGE_SIZE = 30

  const { data, isLoading } = useQuery<{ items: LogRow[]; totalCount: number }>({
    queryKey: ['marketplace-logs', storeId, page, op],
    queryFn: async () =>
      (
        await api.get(`/marketplaces/${storeId}/logs`, {
          params: { page, pageSize: PAGE_SIZE, operationType: op || undefined },
        })
      ).data.data,
  })
  const rows = data?.items ?? []
  const totalCount = data?.totalCount ?? 0

  return (
    <div>
      <BatchesBlock storeId={storeId} />
      <div className="flex items-center gap-2 mb-3">
        <select className="inp" style={{ width: 200 }} value={op} onChange={(e) => { setOp(e.target.value); setPage(1) }}>
          <option value="">Tüm işlemler</option>
          <option value="sync_product">Ürün gönderimi</option>
          <option value="update_stock">Stok güncelleme</option>
          <option value="fetch_orders">Sipariş çekme</option>
        </select>
      </div>
      <div className="card p-0 overflow-hidden">
        <div className="tbl-wrap">
          <table className="w-full text-sm">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)' }}>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Zaman</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>İşlem</th>
                <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Sonuç</th>
                <th className="px-4 py-2.5 text-right text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>Süre</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr><td colSpan={4} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
              )}
              {!isLoading && rows.length === 0 && (
                <tr><td colSpan={4} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Henüz senkron kaydı yok.</td></tr>
              )}
              {rows.map((l) => (
                <tr key={l.id} className="trow" style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-2.5 text-xs" style={{ color: 'var(--text-s)' }}>
                    {new Date(l.createdAt).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'medium' })}
                  </td>
                  <td className="px-4 py-2.5" style={{ color: 'var(--text)' }}>{OP_LABEL[l.operationType] ?? l.operationType}</td>
                  <td className="px-4 py-2.5">
                    <span className={cn('badge', l.status === 'success' ? 'bg' : 'br')}>
                      {l.status === 'success' ? 'Başarılı' : 'Hata'}
                    </span>
                    {l.errorMessage && (
                      <span className="ml-2 text-xs" style={{ color: '#ef4444' }}>{l.errorMessage}</span>
                    )}
                  </td>
                  <td className="px-4 py-2.5 text-right text-xs" style={{ color: 'var(--text-s)' }}>
                    {l.durationMs < 1000 ? `${l.durationMs} ms` : `${(l.durationMs / 1000).toLocaleString('tr-TR', { maximumFractionDigits: 1 })} sn`}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
      <div className="mt-3">
        <Pagination page={page} totalPages={Math.ceil(totalCount / PAGE_SIZE)} totalCount={totalCount} pageSize={PAGE_SIZE} onChange={setPage} />
      </div>
    </div>
  )
}

// ── Sorunlar (F5 — otomatik açılıp kapanan kuyruk) ────────────────────────────

interface IssueRow {
  id: string
  issueType: string
  title: string
  detail: string | null
  suggestedAction: string | null
  status: string
  createdAt: string
  lastSeenAt: string
}

const ISSUE_TYPE: Record<string, { label: string; variant: 'danger' | 'warning' | 'info' }> = {
  price_drift: { label: 'Fiyat sapması', variant: 'warning' },
  stock_drift: { label: 'Stok sapması', variant: 'warning' },
  missing_on_marketplace: { label: 'Pazaryerinde yok', variant: 'danger' },
  batch_timed_out: { label: 'Paket zaman aşımı', variant: 'danger' },
  upload_failed: { label: 'Gönderim hatası', variant: 'danger' },
  unlisted_remote: { label: 'Bizde kayıtsız', variant: 'info' },
}

function IssuesTab({ store }: { store: MarketplaceStore }) {
  const queryClient = useQueryClient()
  const { data: rows = [], isLoading, refetch } = useQuery<IssueRow[]>({
    queryKey: ['marketplace-issues', store.id],
    queryFn: async () => (await api.get(`/marketplaces/${store.id}/issues`)).data.data ?? [],
  })

  const dismiss = useMutation({
    mutationFn: async (id: string) => api.post(`/marketplaces/issues/${id}/dismiss`),
    onSuccess: () => {
      refetch()
      queryClient.invalidateQueries({ queryKey: ['marketplaces-overview'] })
    },
  })

  if (isLoading) return <PageSpinner />
  if (rows.length === 0)
    return (
      <div className="card py-16 text-center">
        <p className="text-sm mb-1" style={{ color: 'var(--text-m)' }}>Açık sorun yok — her şey yolunda. 🎉</p>
        <p className="text-xs" style={{ color: 'var(--text-s)' }}>
          Sorunlar mutabakat ve gönderim sonuçlarından otomatik açılır; koşul ortadan kalkınca kendiliğinden kapanır.
        </p>
      </div>
    )

  return (
    <div className="card p-0 overflow-hidden">
      <table className="w-full text-sm">
        <tbody>
          {rows.map((issue) => {
            const t = ISSUE_TYPE[issue.issueType] ?? { label: issue.issueType, variant: 'info' as const }
            return (
              <tr key={issue.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3 w-36 align-top">
                  <Badge variant={t.variant}>{t.label}</Badge>
                </td>
                <td className="px-4 py-3">
                  <p className="font-medium" style={{ color: 'var(--text)' }}>{issue.title}</p>
                  <span>
                    {issue.detail ? (
                      <p className="text-xs mt-0.5" style={{ color: 'var(--text-m)' }}>{issue.detail}</p>
                    ) : null}
                  </span>
                  <span>
                    {issue.suggestedAction ? (
                      <p className="text-xs mt-0.5" style={{ color: 'var(--brand)' }}>→ {issue.suggestedAction}</p>
                    ) : null}
                  </span>
                </td>
                <td className="px-4 py-3 w-32 text-right align-top text-xs" style={{ color: 'var(--text-s)' }}>
                  {timeAgo(issue.lastSeenAt)}
                </td>
                <td className="px-4 py-3 w-24 text-right align-top">
                  <Button size="sm" variant="ghost" onClick={() => dismiss.mutate(issue.id)} disabled={dismiss.isPending}>
                    Yoksay
                  </Button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

// ── Ayarlar ───────────────────────────────────────────────────────────────────

function SettingsTab({ store }: { store: MarketplaceStore }) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

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
  const { data: target, isLoading } = useQuery<FirmPlatformWithFirm | null>({
    queryKey: ['firm-platforms', store.firmId, 'detail', store.id],
    queryFn: async () => {
      const { data } = await api.get(`/core/firms/${store.firmId}/platforms`)
      const found = (data.data ?? []).find((p: FirmPlatformWithFirm) => p.id === store.id)
      return found ? { ...found, firmId: store.firmId, firmName: pickTr(store.firmNameI18n, store.firmCode) } : null
    },
  })

  if (isLoading || !target) return <PageSpinner />

  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <div className="lg:col-span-2 card p-5">
        <h2 className="text-sm font-semibold mb-4" style={{ color: 'var(--text)' }}>Mağaza Ayarları</h2>
        <ChannelForm
          platformTypes={platformTypes}
          firms={firms}
          initialFirmId={store.firmId}
          target={target}
          onClose={() => {}}
          onSuccess={() => {
            queryClient.invalidateQueries({ queryKey: ['marketplaces-overview'] })
            queryClient.invalidateQueries({ queryKey: ['firm-platforms', store.firmId] })
          }}
        />
      </div>
      <div className="card p-5 self-start">
        <h2 className="text-sm font-semibold mb-2" style={{ color: 'var(--text)' }}>Pazaryeri Sözleşmesi</h2>
        <p className="text-xs mb-3" style={{ color: 'var(--text-m)' }}>
          API kimlik bilgileri (şifreli) firma sözleşmesinde tutulur. Senkron işlemleri bu sözleşme
          üzerinden çalışır.
        </p>
        {store.integrationId ? (
          <p className="text-sm mb-3" style={{ color: 'var(--brand)' }}>
            ✓ Aktif sözleşme bağlı ({store.serviceCode})
          </p>
        ) : (
          <p className="text-sm mb-3" style={{ color: '#b45309' }}>
            ⚠ Aktif sözleşme yok — senkron devre dışı.
          </p>
        )}
        <Button size="sm" variant="secondary" onClick={() => navigate(`/settings/firms/${store.firmId}`)}>
          Firma sözleşmelerine git <ExternalLink size={12} />
        </Button>
      </div>
    </div>
  )
}
