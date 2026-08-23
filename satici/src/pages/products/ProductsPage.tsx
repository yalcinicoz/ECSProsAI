import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import api from '@/api/client'
import { pickName, formatDate } from '@/lib/i18n'

interface ProductRow {
  supplierProductCode: string
  productId: string | null
  productCode: string | null
  name: Record<string, string>
  groupCode: string
  groupName: Record<string, string> | null
  variantCount: number
  status: 'live' | 'pending' | 'rejected'
  pendingRevision: boolean
  reviewNote: string | null
  isSaleOpen: boolean
  lastActivityAt: string
}

interface Paged<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

// F5: kanal bazlı listeleme durumu (satis-kanali-ortak-kurgu §3.2 — satıcı kesiti)
interface ListingReason { code: string; label: string }
interface ListingPerChannel { status: string; reasons: ListingReason[] }
interface ListingData {
  channels: { id: string; code: string; name: string }[]
  statuses: Record<string, Record<string, ListingPerChannel>>
}
const LISTING_TR: Record<string, { label: string; cls: string }> = {
  published: { label: 'Yayında', cls: 'bg' },
  ready: { label: 'Hazır', cls: 'bg' },
  pending: { label: 'Bekliyor', cls: 'ba' },
  missing_info: { label: 'Eksik bilgi', cls: 'ba' },
  blocked: { label: 'Listelenmiyor', cls: 'br' },
  failed: { label: 'Hatalı', cls: 'br' },
  deactivated: { label: 'Düşürüldü', cls: 'br' },
}

const STATUS_OPTIONS = [
  { value: '', label: 'Tüm Durumlar' },
  { value: 'live', label: 'Canlı' },
  { value: 'pending', label: 'Onay Bekliyor' },
  { value: 'rejected', label: 'Reddedildi' },
  { value: 'live_pending', label: 'Revizyon Bekleyen' },
]

export function StatusBadges({ row }: { row: Pick<ProductRow, 'status' | 'pendingRevision'> }) {
  return (
    <span className="inline-flex gap-1.5 flex-wrap">
      {row.status === 'live' && <span className="badge bg">Canlı</span>}
      {row.status === 'pending' && <span className="badge ba">Onay Bekliyor</span>}
      {row.status === 'rejected' && <span className="badge br">Reddedildi</span>}
      {row.pendingRevision && <span className="badge ba">Revizyon Bekliyor</span>}
    </span>
  )
}

export function ProductsPage() {
  const navigate = useNavigate()
  const [status, setStatus] = useState('')
  const [search, setSearch] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 20

  const { data, isLoading, error } = useQuery<Paged<ProductRow>>({
    queryKey: ['supplier-products', status, search, page],
    queryFn: async () => {
      const { data } = await api.get('/supplier/products', {
        params: { status: status || undefined, search: search || undefined, page, pageSize },
      })
      return data.data as Paged<ProductRow>
    },
  })

  // F5: canlı ürünlerin kanallardaki listeleme durumu (satır rozetleri)
  const liveIds = (data?.items ?? []).map(r => r.productId).filter((x): x is string => !!x)
  const { data: listing } = useQuery<ListingData>({
    queryKey: ['supplier-listing', liveIds.join(',')],
    queryFn: async () => (await api.post('/supplier/products/listing-status', { productIds: liveIds })).data.data,
    enabled: liveIds.length > 0,
    staleTime: 60_000,
  })

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / pageSize)) : 1

  function submitSearch(e: React.FormEvent) {
    e.preventDefault()
    setPage(1)
    setSearch(searchInput.trim())
  }

  return (
    <>
      <div className="vh">
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex-1 min-w-[180px]">
            <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Ürünlerim</h1>
            <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
              Canlı ürünleriniz ve onay sürecindeki gönderimleriniz
            </p>
          </div>
          <button className="px-4 py-2 rounded-xl text-sm font-semibold text-white"
            style={{ background: 'var(--brand, #16a34a)' }}
            onClick={() => navigate('/products/new')}>+ Yeni Ürün</button>
          <select
            className="inp !w-auto"
            value={status}
            onChange={(e) => { setStatus(e.target.value); setPage(1) }}
          >
            {STATUS_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
          <form onSubmit={submitSearch} className="relative">
            <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2" style={{ color: 'var(--text-s)' }} />
            <input
              className="inp !w-56 !pl-9"
              placeholder="Ad veya kod ara…"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
            />
          </form>
        </div>
      </div>

      <div className="vc">
        <div className="card tbl-wrap">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs uppercase tracking-wide" style={{ color: 'var(--text-s)' }}>
                <th className="px-4 py-3 font-semibold">Ürün</th>
                <th className="px-4 py-3 font-semibold">Kod</th>
                <th className="px-4 py-3 font-semibold">Grup</th>
                <th className="px-4 py-3 font-semibold text-center">Varyant</th>
                <th className="px-4 py-3 font-semibold">Durum</th>
                <th className="px-4 py-3 font-semibold">Listelenme</th>
                <th className="px-4 py-3 font-semibold">Son İşlem</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr><td colSpan={7} className="px-4 py-10 text-center" style={{ color: 'var(--text-s)' }}>Yükleniyor…</td></tr>
              )}
              {!!error && (
                <tr><td colSpan={7} className="px-4 py-10 text-center text-red-500">Liste yüklenemedi.</td></tr>
              )}
              {data && data.items.length === 0 && (
                <tr><td colSpan={7} className="px-4 py-10 text-center" style={{ color: 'var(--text-s)' }}>
                  {search || status ? 'Filtreye uyan kayıt yok.' : 'Henüz ürününüz yok.'}
                </td></tr>
              )}
              {data?.items.map((r) => (
                <tr
                  key={r.supplierProductCode}
                  className="trow cursor-pointer border-t"
                  style={{ borderColor: 'var(--border)' }}
                  onClick={() => navigate(`/products/${encodeURIComponent(r.supplierProductCode)}`)}
                >
                  <td className="px-4 py-3 font-medium" style={{ color: 'var(--text)' }}>{pickName(r.name)}</td>
                  <td className="px-4 py-3" style={{ color: 'var(--text-m)' }}>
                    {r.supplierProductCode}
                    {r.productCode && <span className="block text-xs" style={{ color: 'var(--text-s)' }}>{r.productCode}</span>}
                  </td>
                  <td className="px-4 py-3" style={{ color: 'var(--text-m)' }}>
                    {r.groupName ? pickName(r.groupName) : r.groupCode}
                  </td>
                  <td className="px-4 py-3 text-center" style={{ color: 'var(--text-m)' }}>{r.variantCount}</td>
                  <td className="px-4 py-3"><StatusBadges row={r} /></td>
                  <td className="px-4 py-3">
                    {!r.productId ? <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span> : (
                      <span className="inline-flex gap-1.5 flex-wrap">
                        {(listing?.channels ?? []).map(ch => {
                          const st = listing?.statuses?.[r.productId!]?.[ch.id]
                          if (!st) return null
                          const m = LISTING_TR[st.status] ?? { label: st.status, cls: 'ba' }
                          const sebep = st.reasons.map(x => x.label).join(' · ')
                          return (
                            <span key={ch.id} className={`badge ${m.cls}`} title={sebep ? `${ch.name}: ${sebep}` : ch.name}>
                              {ch.name}: {m.label}
                            </span>
                          )
                        })}
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3 whitespace-nowrap" style={{ color: 'var(--text-s)' }}>{formatDate(r.lastActivityAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {data && data.totalCount > pageSize && (
          <div className="flex items-center justify-between mt-4 text-sm" style={{ color: 'var(--text-m)' }}>
            <span>{data.totalCount} kayıt</span>
            <div className="flex gap-2">
              <button
                className="px-3 py-1.5 rounded-lg border disabled:opacity-40"
                style={{ borderColor: 'var(--border)' }}
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
              >Önceki</button>
              <span className="px-2 py-1.5">{page} / {totalPages}</span>
              <button
                className="px-3 py-1.5 rounded-lg border disabled:opacity-40"
                style={{ borderColor: 'var(--border)' }}
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
              >Sonraki</button>
            </div>
          </div>
        )}
      </div>
    </>
  )
}
