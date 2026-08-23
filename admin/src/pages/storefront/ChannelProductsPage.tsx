import { useState, useEffect, useMemo } from 'react'
import { useQuery, useQueries, useMutation, useQueryClient } from '@tanstack/react-query'
import { Search, Ban, CheckCircle2, PauseCircle, PlayCircle } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { SearchableSelect } from '@/components/ui/SearchableSelect'
import { Pagination } from '@/components/ui/Pagination'
import { PageSpinner } from '@/components/ui/Spinner'
import { cn } from '@/lib/utils'
import { ChannelScopeTab } from './ChannelScopeTab'

interface Firm { id: string; nameI18n: Record<string, string> }
interface Channel { id: string; nameI18n: Record<string, string>; code: string; firmId: string; firmName: string }

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
}
interface PagedResult {
  items: ChannelProductItem[]
  totalCount: number
  page: number
  pageSize: number
}

const STATUS_OPTIONS = [
  { value: 'all', label: 'Tümü' },
  { value: 'selected', label: 'Kanalda' },
  { value: 'excluded', label: 'Kanaldan Çıkarılan' },
  { value: 'stopped', label: 'Durdurulan' },
]

const PAGE_SIZE = 30

export function ChannelProductsPage() {
  const queryClient = useQueryClient()

  const [channelId, setChannelId] = useState<string>(
    () => sessionStorage.getItem('channelProducts.channelId') ?? ''
  )
  useEffect(() => { if (channelId) sessionStorage.setItem('channelProducts.channelId', channelId) }, [channelId])

  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [stopModalOpen, setStopModalOpen] = useState(false)
  const [stopFrom, setStopFrom] = useState('')
  const [stopUntil, setStopUntil] = useState('')

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

  // ── Ürün listesi ──────────────────────────────────────────────────────────
  const { data: pagedData, isLoading: listLoading } = useQuery<PagedResult>({
    queryKey: ['channel-products', channelId, search, status, page],
    queryFn: async () => {
      const params = new URLSearchParams({ status, page: String(page), pageSize: String(PAGE_SIZE) })
      if (search) params.set('search', search)
      const { data } = await api.get(`/navigation/channel-products/${channelId}/manage?${params}`)
      return data.data
    },
    enabled: !!channelId,
  })
  const items = pagedData?.items ?? []
  const totalCount = pagedData?.totalCount ?? 0
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))

  // Kanal/filtre değişince seçim sıfırlanır
  useEffect(() => { setSelected(new Set()) }, [channelId, search, status])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['channel-products', channelId] })

  const applySearch = () => { setPage(1); setSearch(searchInput.trim()) }

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
    onSuccess: () => { setSelected(new Set()); setStopModalOpen(false); setStopFrom(''); setStopUntil(''); invalidate() },
  })

  const selectAllMatching = async () => {
    const params = new URLSearchParams({ status })
    if (search) params.set('search', search)
    const { data } = await api.get(`/navigation/channel-products/${channelId}/manage/ids?${params}`)
    setSelected(new Set(data.data as string[]))
  }

  const selectedIds = useMemo(() => Array.from(selected), [selected])
  const busy = selectMutation.isPending || stopMutation.isPending

  const toggleRow = (id: string) => {
    setSelected(prev => { const n = new Set(prev); n.has(id) ? n.delete(id) : n.add(id); return n })
  }
  const pageAllSelected = items.length > 0 && items.every(i => selected.has(i.productId))
  const togglePageAll = () => {
    setSelected(prev => {
      const n = new Set(prev)
      if (pageAllSelected) items.forEach(i => n.delete(i.productId))
      else items.forEach(i => n.add(i.productId))
      return n
    })
  }

  const [tab, setTab] = useState<'urunler' | 'kapsam'>('urunler')   // F1: Ürünler | Kapsam sekmeleri

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
          onChange={(v) => { if (v) { setChannelId(v); setPage(1) } }}
          options={channelOptions}
          placeholder="Kanal seçin…"
          hasValue={!!channelId}
        />
      </div>

      {channelId && (
        <div className="tab-scroll mb-4">
          <button className={cn('stab', tab === 'urunler' && 'active')} onClick={() => setTab('urunler')}>Ürünler</button>
          <button className={cn('stab', tab === 'kapsam' && 'active')} onClick={() => setTab('kapsam')}>Kapsam</button>
        </div>
      )}

      {channelId && tab === 'kapsam' && <ChannelScopeTab channelId={channelId} />}

      {channelId && tab === 'urunler' && (
        <>
          {/* Filtre çubuğu */}
          <div className="card mb-4 flex flex-wrap items-end gap-3">
            <div className="flex-1 min-w-[220px]">
              <label className="flbl mb-1.5">Ara (kod veya ad)</label>
              <div className="flex gap-2">
                <input
                  className="inp flex-1"
                  value={searchInput}
                  onChange={e => setSearchInput(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && applySearch()}
                  placeholder="Ürün kodu veya adı…"
                />
                <Button variant="secondary" onClick={applySearch}><Search size={14} /> Ara</Button>
              </div>
            </div>
            <div className="min-w-[180px]">
              <label className="flbl mb-1.5">Durum</label>
              <select className="inp" value={status} onChange={e => { setPage(1); setStatus(e.target.value) }}>
                {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
          </div>

          {/* Toplu işlem çubuğu */}
          {selected.size > 0 && (
            <div className="flex flex-wrap items-center gap-2 px-4 py-3 rounded-xl mb-3 text-sm"
              style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
              <span style={{ color: 'var(--text-m)' }}>
                <strong>{selected.size}</strong> ürün seçili
              </span>
              <button className="text-xs underline" style={{ color: 'var(--brand)' }} onClick={selectAllMatching}>
                Filtreye uyan tümünü seç ({totalCount})
              </button>
              <button className="text-xs underline" style={{ color: 'var(--text-s)' }} onClick={() => setSelected(new Set())}>
                Temizle
              </button>
              <div className="flex-1" />
              <Button variant="secondary" disabled={busy}
                onClick={() => selectMutation.mutate({ productIds: selectedIds, selected: true })}>
                <CheckCircle2 size={14} /> Kanala Al
              </Button>
              <Button variant="secondary" disabled={busy}
                onClick={() => selectMutation.mutate({ productIds: selectedIds, selected: false })}>
                <Ban size={14} /> Kanaldan Çıkar
              </Button>
              <Button variant="secondary" disabled={busy} onClick={() => setStopModalOpen(true)}>
                <PauseCircle size={14} /> Satışı Durdur
              </Button>
              <Button variant="secondary" disabled={busy}
                onClick={() => stopMutation.mutate({ productIds: selectedIds, from: null, until: null })}>
                <PlayCircle size={14} /> Satışı Başlat
              </Button>
            </div>
          )}

          {/* Tablo */}
          <div className="card overflow-hidden p-0">
            {listLoading ? <div className="p-8"><PageSpinner /></div> : (
              <table className="w-full">
                <thead>
                  <tr className="text-left text-xs" style={{ color: 'var(--text-s)', borderBottom: '1px solid var(--border)' }}>
                    <th className="px-4 py-3 w-10">
                      <input type="checkbox" checked={pageAllSelected} onChange={togglePageAll} />
                    </th>
                    <th className="px-2 py-3 w-14"></th>
                    <th className="px-2 py-3">Ürün</th>
                    <th className="px-4 py-3">Kanal Durumu</th>
                    <th className="px-4 py-3 text-right">İşlem</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map(it => (
                    <tr key={it.productId} className="text-sm" style={{ borderBottom: '1px solid var(--border)' }}>
                      <td className="px-4 py-2">
                        <input type="checkbox" checked={selected.has(it.productId)} onChange={() => toggleRow(it.productId)} />
                      </td>
                      <td className="px-2 py-2">
                        {it.mainImageUrl
                          ? <img src={it.mainImageUrl} alt="" className="w-10 h-10 rounded object-cover" style={{ background: 'var(--surface2)' }} />
                          : <div className="w-10 h-10 rounded" style={{ background: 'var(--surface2)' }} />}
                      </td>
                      <td className="px-2 py-2">
                        <div style={{ color: 'var(--text)' }}>{nameOf(it.nameI18n)}</div>
                        <div className="text-xs" style={{ color: 'var(--text-s)' }}>{it.code}</div>
                      </td>
                      <td className="px-4 py-2">
                        {!it.isSelected
                          ? <Badge variant="neutral">Kanaldan çıkarıldı</Badge>
                          : it.isStoppedNow
                            ? <Badge variant="warning">Durduruldu{it.saleStoppedUntil ? ` — ${new Date(it.saleStoppedUntil).toLocaleDateString('tr-TR')} kadar` : ''}</Badge>
                            : <Badge variant="success">Kanalda</Badge>}
                      </td>
                      <td className="px-4 py-2 text-right">
                        {it.isSelected
                          ? <button className="text-xs underline" style={{ color: 'var(--text-m)' }} disabled={busy}
                              onClick={() => selectMutation.mutate({ productIds: [it.productId], selected: false })}>Çıkar</button>
                          : <button className="text-xs underline" style={{ color: 'var(--brand)' }} disabled={busy}
                              onClick={() => selectMutation.mutate({ productIds: [it.productId], selected: true })}>Kanala al</button>}
                      </td>
                    </tr>
                  ))}
                  {items.length === 0 && (
                    <tr><td colSpan={5} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                      Kayıt bulunamadı.
                    </td></tr>
                  )}
                </tbody>
              </table>
            )}
            <Pagination page={page} totalPages={totalPages} totalCount={totalCount} pageSize={PAGE_SIZE} onChange={setPage} />
          </div>
        </>
      )}

      {/* Satışı Durdur modalı */}
      <Modal open={stopModalOpen} onClose={() => setStopModalOpen(false)} title={`Satışı Durdur — ${selected.size} ürün`}>
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
              productIds: selectedIds,
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
