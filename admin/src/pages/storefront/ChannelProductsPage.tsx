import { useState, useEffect, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import { Ban, CheckCircle2, PauseCircle, PlayCircle } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { PageSpinner } from '@/components/ui/Spinner'
import { ChannelProductDrawer, type DrawerProduct } from './ChannelProductDrawer'

interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel { id: string; nameI18n: Record<string, string>; code: string; firmId: string; firmName: string; capabilities?: { pushListing?: boolean } }

function channelLabel(ch: Channel): string {
  return ch.nameI18n?.['tr'] ?? ch.nameI18n?.[Object.keys(ch.nameI18n ?? {})[0]] ?? ch.code
}
function nameOf(i18n: Record<string, string>): string {
  return i18n?.['tr'] ?? i18n?.[Object.keys(i18n ?? {})[0]] ?? '—'
}

interface ChannelProductItem {
  productId: string
  code: string
  nameI18n: Record<string, string>
  mainImageUrl: string | null
  isSelected: boolean
  saleStoppedFrom: string | null
  saleStoppedUntil: string | null
  isStoppedNow: boolean
  // 2026-09-09 (DataGrid): kolon/sıralama için gerçek ürün alanları
  sourceType: string
  basePrice: number
  variantCount: number
}

const SOURCE_LABELS: Record<string, string> = { own: 'Kendi', seller: 'Satıcı', supply: 'Tedarik' }
interface PagedResult {
  items: ChannelProductItem[]
  totalCount: number
  page: number
  pageSize: number
}

// F2 listeleme durumu (docs/satis-kanali-ortak-kurgu.md §3.2)
interface ListingReason { code: string; label: string }
interface ListingStatus { status: string; reasons: ListingReason[] }
interface ListingSummary { statusCounts: Record<string, number>; reasons: { code: string; label: string; count: number }[]; total: number }
const LISTING_LABELS: Record<string, { label: string; variant: 'success' | 'info' | 'warning' | 'danger' | 'neutral' }> = {
  published:    { label: 'Yayında',     variant: 'success' },
  ready:        { label: 'Hazır',       variant: 'info' },
  pending:      { label: 'Bekliyor',    variant: 'info' },
  missing_info: { label: 'Eksik bilgi', variant: 'warning' },
  blocked:      { label: 'Engelli',     variant: 'neutral' },
  failed:       { label: 'Hatalı',      variant: 'danger' },
  deactivated:  { label: 'Düşürüldü',   variant: 'neutral' },
}

const STATUS_OPTIONS = [
  { value: 'all', label: 'Tümü' },
  { value: 'selected', label: 'Kanalda' },
  { value: 'excluded', label: 'Kanaldan Çıkarılan' },
  { value: 'stopped', label: 'Durdurulan' },
]

export function ChannelProductsPage() {
  // DataGrid (2026-09-09): ÜRÜN alanlarında sunucu filtre/sıralama/arama (ChannelProductGrid.Schema)
  // + toplu seçim (DataGrid.selection) + görünümler.
  // ★ SINIR: kanal durumu (Kanalda / Çıkarıldı / Durduruldu) ve listeleme durumu Storefront tarafında
  // BELLEKTE çözülüyor (opt-out: satırı olmayan ürün de kanalda) → o kolonlar sunucuda sıralanamaz;
  // onlar eskiden olduğu gibi adlandırılmış süzgeçlerle (status / listing / reason) çalışır.
  const queryClient = useQueryClient()
  const [sp] = useSearchParams()

  // Kanal seçimi URL'de tutulur (paylaşılabilir link); ilk açılışta son kanal sessionStorage'dan gelir.
  const urlChannel = sp.get('channelId') ?? ''
  const status = sp.get('status') ?? 'all'
  const listingF = sp.get('listing') ?? ''
  const reasonF = sp.get('reason') ?? ''
  const grid = useGridState('channel-products', { defaultPageSize: 30, defaultSort: 'code', defaultDir: 'asc' })
  const setNamed = (k: string, v: string) => grid.mutate(n => { if (v) n.set(k, v); else n.delete(k) })

  const channelId = urlChannel || (sessionStorage.getItem('channelProducts.channelId') ?? '')
  useEffect(() => { if (channelId) sessionStorage.setItem('channelProducts.channelId', channelId) }, [channelId])

  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [stopModalOpen, setStopModalOpen] = useState(false)
  const [stopFrom, setStopFrom] = useState('')
  const [stopUntil, setStopUntil] = useState('')
  const [drawer, setDrawer] = useState<DrawerProduct | null>(null)
  const [stopTargetIds, setStopTargetIds] = useState<string[] | null>(null)

  // ── Kanal listesi (firma → platform) ──────────────────────────────────────
  const { data: firms = [], isLoading: firmsLoading } = useQuery<Firm[]>({
    queryKey: ['firms'],
    queryFn: async () => { const { data } = await api.get('/core/firms'); return data.data ?? [] },
  })
  const platformQueries = useQueries({
    queries: firms.map(firm => ({
      queryKey: ['firm-platforms', firm.id],
      queryFn: async (): Promise<Channel[]> => {
        const { data } = await api.get(`/core/firms/${firm.id}/platforms`)
        const firmName = nameOf(firm.nameI18n)
        return (data.data ?? []).map((ch: Channel) => ({ ...ch, firmId: firm.id, firmName }))
      },
      enabled: firms.length > 0,
    })),
  })
  const channels: Channel[] = platformQueries.flatMap(q => q.data ?? [])
  const chLoading = firmsLoading || platformQueries.some(q => q.isLoading)
  const channelOptions = channels.map(c => ({ value: c.id, label: `${channelLabel(c)} (${c.firmName})` }))

  const named = () => ({
    status,
    listing: listingF || undefined,
    reason: reasonF || undefined,
  })

  // ── Ürün listesi ──────────────────────────────────────────────────────────
  const { data: pagedData, isLoading: listLoading, isFetching, error: listError } = useQuery<PagedResult>({
    queryKey: ['channel-products', channelId, status, listingF, reasonF, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/navigation/channel-products/${channelId}/manage?${grid.toParams(named())}`)).data.data,
    enabled: !!channelId,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const items = pagedData?.items ?? []

  // F2: özet çipleri + sayfadaki ürünlerin listeleme durumu rozetleri
  const { data: listingSummary } = useQuery<ListingSummary>({
    queryKey: ['listing-summary', channelId],
    queryFn: async () => (await api.get(`/navigation/channel-products/${channelId}/listing-summary`)).data.data,
    enabled: !!channelId,
    staleTime: 60_000,
  })
  const pageIds = items.map(i => i.productId)
  const { data: listingMap = {} } = useQuery<Record<string, ListingStatus>>({
    queryKey: ['listing-status', channelId, pageIds.join(',')],
    queryFn: async () => (await api.post(`/navigation/channel-products/${channelId}/listing-status`, { productIds: pageIds })).data.data,
    enabled: !!channelId && pageIds.length > 0,
  })

  const totalCount = pagedData?.totalCount ?? 0

  // Kanal/filtre değişince seçim sıfırlanır (yanlış ürüne toplu işlem yapılmasın)
  useEffect(() => { setSelected(new Set()); setDrawer(null) }, [channelId, status, listingF, reasonF, grid.state.search])
  useEffect(() => {
    if (!drawer) return
    const it = items.find(i => i.productId === drawer.productId)
    if (it) setDrawer(d => d && ({ ...d, isSelected: it.isSelected, isStoppedNow: it.isStoppedNow, saleStoppedUntil: it.saleStoppedUntil }))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [items])

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['channel-products', channelId] })
    queryClient.invalidateQueries({ queryKey: ['listing-status', channelId] })
    queryClient.invalidateQueries({ queryKey: ['listing-summary', channelId] })
    queryClient.invalidateQueries({ queryKey: ['listing-detail', channelId] })
  }
  const selectedChannel = channels.find(c => c.id === channelId)
  const isPushChannel = selectedChannel?.capabilities?.pushListing === true

  // ── Toplu işlemler ──────────────────────────────────────────────────────────
  const selectMutation = useMutation({
    mutationFn: async (vars: { productIds: string[]; selected: boolean }) => {
      const { data } = await api.post(`/navigation/channel-products/${channelId}/bulk-select`, vars)
      return data.data.affected as number
    },
    onSuccess: () => { setSelected(new Set()); invalidate() },
  })
  const stopMutation = useMutation({
    mutationFn: async (vars: { productIds: string[]; from: string | null; until: string | null }) => {
      const { data } = await api.post(`/navigation/channel-products/${channelId}/bulk-stop`, vars)
      return data.data.affected as number
    },
    onSuccess: () => { setSelected(new Set()); setStopModalOpen(false); setStopFrom(''); setStopUntil(''); setStopTargetIds(null); invalidate() },
  })

  // "Filtreye uyan tümünü seç" — arama/sıralama dahil AYNI filtre kümesiyle id listesi alınır.
  const selectAllMatching = async () => {
    const params = grid.toParams(named())
    const { data } = await api.get(`/navigation/channel-products/${channelId}/manage/ids?${params}`)
    setSelected(new Set(data.data as string[]))
  }

  const pushMutation = useMutation({
    mutationFn: async (productIds: string[]) => (await api.post(`/marketplaces/${channelId}/sync-products`, { productIds })).data.data,
    onSuccess: () => { setSelected(new Set()); invalidate() },
  })

  const selectedIds = useMemo(() => Array.from(selected), [selected])
  const busy = selectMutation.isPending || stopMutation.isPending || pushMutation.isPending

  // F3 çekmece kanal kararı aksiyonları
  const onChannelAction = (action: 'select' | 'unselect' | 'stop' | 'start', productId: string) => {
    if (action === 'select') selectMutation.mutate({ productIds: [productId], selected: true })
    else if (action === 'unselect') selectMutation.mutate({ productIds: [productId], selected: false })
    else if (action === 'start') stopMutation.mutate({ productIds: [productId], from: null, until: null })
    else { setStopTargetIds([productId]); setStopModalOpen(true) }
  }

  const columns: GridColumn<ChannelProductItem>[] = [
    { key: 'code', header: 'ÜRÜN', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 300,
      filter: { type: 'text', label: 'Ürün kodu', ops: ['startswith', 'contains', 'eq'] },
      filters: [
        { field: 'name', label: 'Ürün adı', type: 'text' },
        { field: 'supplierProductCode', label: 'Tedarikçi ürün kodu', type: 'text' }],
      cell: it => <div className="flex items-center gap-2">
        {it.mainImageUrl
          ? <img src={it.mainImageUrl} alt="" className="w-10 h-10 rounded object-cover flex-shrink-0" style={{ background: 'var(--surface2)' }} />
          : <div className="w-10 h-10 rounded flex-shrink-0" style={{ background: 'var(--surface2)' }} />}
        <div className="min-w-0">
          <div className="truncate" style={{ color: 'var(--text)' }}>{nameOf(it.nameI18n)}</div>
          <div className="text-xs" style={{ color: 'var(--text-s)' }}>{it.code}</div>
        </div>
      </div> },
    // Kanal durumu Storefront'ta bellekte çözülüyor → sıralama YOK; süzgeç adlandırılmış "status" ile.
    { key: 'channelStatus', header: 'KANAL DURUMU', priority: 1, lockVisible: true, minWidth: 200,
      cell: it => !it.isSelected
        ? <Badge variant="neutral">Kanaldan çıkarıldı</Badge>
        : it.isStoppedNow
          ? <Badge variant="warning">Durduruldu{it.saleStoppedUntil ? ` — ${new Date(it.saleStoppedUntil).toLocaleDateString('tr-TR')} kadar` : ''}</Badge>
          : <Badge variant="success">Kanalda</Badge> },
    // Listeleme durumu ayrı serviste hesaplanıyor (sayfa başına toplu sorgu) → sıralama YOK.
    { key: 'listing', header: 'LİSTELEME', priority: 2, minWidth: 240,
      cell: it => {
        const ls = listingMap[it.productId]
        if (!ls) return <span className="text-xs" style={{ color: 'var(--text-s)' }}>…</span>
        const m = LISTING_LABELS[ls.status] ?? { label: ls.status, variant: 'neutral' as const }
        const sebep = ls.reasons.filter(r => !['channel_excluded', 'sale_stopped'].includes(r.code))
        return <div>
          <Badge variant={m.variant}>{m.label}</Badge>
          {sebep.length > 0 && (
            <div className="text-xs mt-0.5 max-w-[260px] truncate" title={sebep.map(r => r.label).join(' · ')}
              style={{ color: 'var(--text-s)' }}>{sebep.map(r => r.label).join(' · ')}</div>
          )}
        </div>
      } },
    { key: 'sourceType', header: 'KAYNAK', priority: 3, defaultVisible: false, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Kaynak', options: Object.entries(SOURCE_LABELS).map(([value, label]) => ({ value, label })) },
      cell: it => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{SOURCE_LABELS[it.sourceType] ?? it.sourceType}</span> },
    { key: 'variantCount', header: 'VARYANT', priority: 3, defaultVisible: false, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Varyant sayısı' },
      cell: it => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{it.variantCount}</span> },
    { key: 'basePrice', header: 'LİSTE FİYATI', priority: 3, defaultVisible: false, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Liste fiyatı' },
      filters: [{ field: 'isSaleOpen', label: 'Satışa açık (ürün)', type: 'boolean' },
                { field: 'createdAt', label: 'Ürün oluşturma', type: 'date' }],
      cell: it => <span className="text-xs" style={{ color: 'var(--text-s)' }}>
        {it.basePrice > 0 ? `${it.basePrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺` : '—'}</span> },
    { key: 'actions', header: 'İŞLEM', priority: 1, align: 'right', exportable: false, stopRowClick: true, minWidth: 110,
      cell: it => it.isSelected
        ? <button className="text-xs underline" style={{ color: 'var(--text-m)' }} disabled={busy}
            onClick={() => selectMutation.mutate({ productIds: [it.productId], selected: false })}>Çıkar</button>
        : <button className="text-xs underline" style={{ color: 'var(--brand)' }} disabled={busy}
            onClick={() => selectMutation.mutate({ productIds: [it.productId], selected: true })}>Kanala al</button> },
  ]

  if (chLoading) return <PageSpinner />

  return (
    <div className="p-6">
      <div className="mb-6">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kanal Ürünleri</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          Ürünlerin bu kanalda satılıp satılmayacağını (kanala al / çıkar) ve satışın anlık ya da
          tarih penceresiyle durdurulmasını toplu yönetin.
        </p>
      </div>

      {/* Kanal seçici */}
      <div className="card mb-4">
        <label className="flbl mb-2">Satış Kanalı</label>
        <SearchableSelect
          value={channelId}
          onChange={(v) => { if (v) setNamed('channelId', v) }}
          options={channelOptions}
          placeholder="Kanal seçin…"
          hasValue={!!channelId}
        />
      </div>

      {channelId && (
        <>
          {/* F3 Özet çipleri — tıklayınca listeyi o duruma süzer */}
          {listingSummary && (
            <div className="flex flex-wrap items-center gap-1.5 mb-3 text-xs">
              <span style={{ color: 'var(--text-s)' }}>Listeleme:</span>
              <button type="button" onClick={() => grid.mutate(n => { n.delete('listing'); n.delete('reason') })}
                className="px-2 py-0.5 rounded-full"
                style={{ border: '1px solid var(--border)', background: listingF === '' && reasonF === '' ? 'var(--brand)' : 'var(--surface)', color: listingF === '' && reasonF === '' ? '#fff' : 'var(--text-s)' }}>
                Tümü {listingSummary.total}
              </button>
              {Object.entries(LISTING_LABELS)
                .filter(([k]) => (listingSummary.statusCounts[k] ?? 0) > 0)
                .map(([k, m]) => (
                  <button key={k} type="button"
                    onClick={() => grid.mutate(n => { n.delete('reason'); if (listingF === k) n.delete('listing'); else n.set('listing', k) })}
                    className="rounded-full" style={{ outline: listingF === k ? '2px solid var(--brand)' : 'none', borderRadius: '9999px' }}>
                    <Badge variant={m.variant}>{m.label} {listingSummary.statusCounts[k]}</Badge>
                  </button>
                ))}
            </div>
          )}

          <DataGrid<ChannelProductItem>
            gridId="channel-products"
            views
            grid={grid}
            columns={columns}
            rows={items}
            rowKey={it => it.productId}
            totalCount={totalCount}
            loading={listLoading}
            fetching={isFetching}
            error={listError ? errText(listError) : null}
            onRowClick={it => setDrawer({
              productId: it.productId, code: it.code, name: nameOf(it.nameI18n),
              mainImageUrl: it.mainImageUrl, isSelected: it.isSelected,
              isStoppedNow: it.isStoppedNow, saleStoppedUntil: it.saleStoppedUntil,
            })}
            empty="Kayıt bulunamadı."
            search={{ placeholder: 'Ürün kodu veya adı…' }}
            minWidth={1080}
            filterLeading={
              <>
                <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={status} aria-label="Kanal durumu"
                  onChange={e => setNamed('status', e.target.value)}>
                  {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                </select>
                <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={listingF} aria-label="Listeleme"
                  onChange={e => setNamed('listing', e.target.value)}>
                  <option value="">Listeleme: Tümü{listingSummary ? ` (${listingSummary.total})` : ''}</option>
                  {Object.entries(LISTING_LABELS).map(([k, m]) => {
                    const n = listingSummary?.statusCounts[k] ?? 0
                    return <option key={k} value={k}>{m.label}{listingSummary ? ` (${n})` : ''}</option>
                  })}
                </select>
                <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={reasonF} aria-label="Sebep"
                  onChange={e => setNamed('reason', e.target.value)}>
                  <option value="">Sebep: Tümü</option>
                  {(listingSummary?.reasons ?? []).map(r => (
                    <option key={r.code} value={r.code.split(':')[0]}>{r.label} ({r.count})</option>
                  ))}
                </select>
              </>
            }
            selection={{
              selected,
              onChange: setSelected,
              actions: () => (
                <>
                  <button className="text-xs underline" style={{ color: 'var(--brand)' }} onClick={selectAllMatching}>
                    Filtreye uyan tümünü seç ({totalCount})
                  </button>
                  <Button variant="secondary" disabled={busy}
                    onClick={() => selectMutation.mutate({ productIds: selectedIds, selected: true })}>
                    <CheckCircle2 size={14} /> Kanala Al
                  </Button>
                  <Button variant="secondary" disabled={busy}
                    onClick={() => selectMutation.mutate({ productIds: selectedIds, selected: false })}>
                    <Ban size={14} /> Kanaldan Çıkar
                  </Button>
                  <Button variant="secondary" disabled={busy} onClick={() => { setStopTargetIds(null); setStopModalOpen(true) }}>
                    <PauseCircle size={14} /> Satışı Durdur
                  </Button>
                  <Button variant="secondary" disabled={busy}
                    onClick={() => stopMutation.mutate({ productIds: selectedIds, from: null, until: null })}>
                    <PlayCircle size={14} /> Satışı Başlat
                  </Button>
                  {isPushChannel && (
                    <Button disabled={busy} loading={pushMutation.isPending}
                      onClick={() => pushMutation.mutate(selectedIds)}>
                      Pazaryerine Gönder
                    </Button>
                  )}
                </>
              ),
            }}
            compact={{
              title: it => nameOf(it.nameI18n),
              subtitle: it => it.code,
              badge: it => !it.isSelected
                ? <Badge variant="neutral">Çıkarıldı</Badge>
                : it.isStoppedNow ? <Badge variant="warning">Durduruldu</Badge> : <Badge variant="success">Kanalda</Badge>,
            }}
          />
        </>
      )}

      {/* F3 sağ çekmece */}
      {drawer && channelId && (
        <ChannelProductDrawer channelId={channelId} product={drawer} busy={busy}
          onClose={() => setDrawer(null)} onChannelAction={onChannelAction} />
      )}

      {/* Satışı Durdur modalı */}
      <Modal open={stopModalOpen} onClose={() => setStopModalOpen(false)} title={`Satışı Durdur — ${(stopTargetIds ?? selectedIds).length} ürün`}>
        <div className="space-y-4">
          <p className="text-sm" style={{ color: 'var(--text-s)' }}>
            Tarih boş bırakılırsa <strong>anlık ve süresiz</strong> durdurulur. Bitiş tarihi verilirse
            o tarih geçince satış otomatik yeniden açılır.
          </p>
          <div>
            <label className="flbl mb-1.5">Başlangıç (opsiyonel)</label>
            <input type="datetime-local" className="inp" value={stopFrom} onChange={e => setStopFrom(e.target.value)} />
          </div>
          <div>
            <label className="flbl mb-1.5">Bitiş (opsiyonel)</label>
            <input type="datetime-local" className="inp" value={stopUntil} onChange={e => setStopUntil(e.target.value)} />
          </div>
          {stopMutation.isError && (
            <p className="text-sm" style={{ color: '#dc2626' }}>İşlem başarısız — tarih aralığını kontrol edin.</p>
          )}
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setStopModalOpen(false)}>Vazgeç</Button>
            <Button disabled={stopMutation.isPending} onClick={() => stopMutation.mutate({
              productIds: stopTargetIds ?? selectedIds,
              from: stopFrom ? new Date(stopFrom).toISOString() : new Date().toISOString(),
              until: stopUntil ? new Date(stopUntil).toISOString() : null,
            })}>
              <PauseCircle size={14} /> Durdur
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}
