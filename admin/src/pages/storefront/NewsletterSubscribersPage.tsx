import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { cn } from '@/lib/utils'
import { useFirmPlatforms } from '@/pages/cms/CmsPagesPage'

interface NewsletterSubscription {
  id: string
  firmPlatformId: string
  email: string
  memberId?: string
  isActive: boolean
  createdAt: string
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export function NewsletterSubscribersPage() {
  const [tab, setTab] = useState<'active' | 'all'>('active')
  const [platformId, setPlatformId] = useState('')
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)

  const { data: platforms = [] } = useFirmPlatforms()
  const platformName = (pid?: string) =>
    platforms.find(p => p.id === pid)?.nameI18n?.['tr'] ?? '—'

  const { data, isLoading } = useQuery<PagedResult<NewsletterSubscription>>({
    queryKey: ['newsletter-subscriptions', tab, platformId, appliedSearch, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (tab === 'active') params.set('isActive', 'true')
      if (platformId) params.set('firmPlatformId', platformId)
      if (appliedSearch) params.set('search', appliedSearch)
      return (await api.get(`/store-notifications/newsletter-subscriptions?${params}`)).data.data
    },
  })

  const subs = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 20)

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Bülten Aboneleri</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {data?.totalCount ?? 0} kayıt — footer bülten formundan gelen abonelikler
          </p>
        </div>
        <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 180 }}
          value={platformId} onChange={e => { setPlatformId(e.target.value); setPage(1) }}>
          <option value="">Tüm platformlar</option>
          {platforms.map(p => (
            <option key={p.id} value={p.id}>{p.nameI18n?.['tr'] ?? p.id}</option>
          ))}
        </select>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'active' && 'active')}
          onClick={() => { setTab('active'); setPage(1) }}>Aktif</button>
        <button className={cn('stab', tab === 'all' && 'active')}
          onClick={() => { setTab('all'); setPage(1) }}>Tümü</button>
      </div>

      <div className="flex items-center gap-2 mb-4">
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 220 }}
          placeholder="E-posta ara…" value={search}
          onChange={e => setSearch(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') { setAppliedSearch(search.trim()); setPage(1) } }} />
        <button onClick={() => { setAppliedSearch(search.trim()); setPage(1) }}
          className="px-3 py-1.5 rounded-lg text-sm"
          style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Ara</button>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['E-POSTA', 'PLATFORM', 'ÜYE', 'DURUM', 'KAYIT TARİHİ'].map(h => (
                <th key={h} className="px-4 py-3 text-xs font-semibold text-left"
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={5} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && subs.length === 0 && (
              <tr><td colSpan={5} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Abone yok. Kayıtlar sitedeki footer bülten formundan gelir.
              </td></tr>
            )}
            {subs.map(s => (
              <tr key={s.id} style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text)' }}>{s.email}</td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>{platformName(s.firmPlatformId)}</td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                  {s.memberId ? <code>{s.memberId.slice(0, 8)}…</code> : 'Misafir'}
                </td>
                <td className="px-4 py-3">
                  <Badge variant={s.isActive ? 'success' : 'neutral'}>{s.isActive ? 'Aktif' : 'Pasif'}</Badge>
                </td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                  {new Date(s.createdAt).toLocaleString('tr-TR')}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 mt-4">
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>← Önceki</button>
          <span className="text-sm" style={{ color: 'var(--text-s)' }}>{page} / {totalPages}</span>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Sonraki →</button>
        </div>
      )}
    </div>
  )
}
