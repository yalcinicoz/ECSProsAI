import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { cn } from '@/lib/utils'

export interface MemberSummary {
  id: string
  firstName: string
  lastName: string
  email?: string
  phone?: string
  isRegistered: boolean
  isActive: boolean
  createdAt: string
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export function MembersPage() {
  const navigate = useNavigate()
  const [tab, setTab] = useState<'active' | 'all'>('active')
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery<PagedResult<MemberSummary>>({
    queryKey: ['members', tab, appliedSearch, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      params.set('activeOnly', tab === 'active' ? 'true' : 'false')
      if (appliedSearch) params.set('search', appliedSearch)
      return (await api.get(`/crm/members?${params}`)).data.data
    },
  })

  const members = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 20)

  function applySearch() {
    setAppliedSearch(search.trim())
    setPage(1)
  }

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Üyeler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{data?.totalCount ?? 0} kayıt</p>
        </div>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'active' && 'active')} onClick={() => { setTab('active'); setPage(1) }}>Aktif</button>
        <button className={cn('stab', tab === 'all' && 'active')} onClick={() => { setTab('all'); setPage(1) }}>Tümü</button>
      </div>

      <div className="flex items-center gap-2 mb-4">
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 260 }}
          placeholder="Ad, e-posta veya telefon ara…" value={search}
          onChange={e => setSearch(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') applySearch() }} />
        <button onClick={applySearch} className="px-3 py-1.5 rounded-lg text-sm"
          style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Ara</button>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['AD SOYAD', 'E-POSTA', 'TELEFON', 'ÜYELİK', 'DURUM', 'KAYIT', ''].map(h => (
                <th key={h} className={`px-4 py-3 text-xs font-semibold ${h === '' ? 'w-20' : 'text-left'}`}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && members.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Üye bulunamadı.</td></tr>
            )}
            {members.map(m => (
              <tr key={m.id} onClick={() => navigate(`/crm/members/${m.id}`)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3 text-sm font-medium" style={{ color: 'var(--text)' }}>
                  {m.firstName} {m.lastName}
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{m.email ?? '—'}</td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{m.phone ?? '—'}</td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                  {m.isRegistered ? 'Kayıtlı' : 'Misafir'}
                </td>
                <td className="px-4 py-3">
                  <Badge variant={m.isActive ? 'success' : 'neutral'}>{m.isActive ? 'Aktif' : 'Pasif'}</Badge>
                </td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                  {new Date(m.createdAt).toLocaleDateString('tr-TR')}
                </td>
                <td className="px-4 py-3 text-right">
                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span>
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
