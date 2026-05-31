import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Search, ChevronRight } from 'lucide-react'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { PageSpinner } from '@/components/ui/Spinner'
import { cn } from '@/lib/utils'
import type { AccountGroup } from './AccountGroupsPage'

const ACCOUNT_TYPES = [
  { value: '',         label: 'Tümü' },
  { value: 'customer', label: 'Müşteri' },
  { value: 'supplier', label: 'Tedarikçi' },
  { value: 'both',     label: 'Her İkisi' },
]

const TYPE_BADGE: Record<string, { label: string; color: string }> = {
  customer: { label: 'Müşteri',    color: 'var(--brand)' },
  supplier: { label: 'Tedarikçi', color: '#f59e0b' },
  both:     { label: 'Her İkisi', color: '#8b5cf6' },
}

interface CurrentAccount {
  id: string
  code: string
  title: string
  accountType: string
  groupId: string | null
  groupName: string | null
  taxNumber: string | null
  contactName: string | null
  phone: string | null
  email: string | null
  city: string | null
  country: string | null
  creditLimit: number
  currency: string
  isActive: boolean
  createdAt: string
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export function AccountsPage() {
  const navigate = useNavigate()
  const [accountType, setAccountType] = useState('')
  const [groupId, setGroupId] = useState('')
  const [isActive, setIsActive] = useState<string>('')
  const [search, setSearch] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [page, setPage] = useState(1)
  const PAGE_SIZE = 30

  const { data: groups = [] } = useQuery<AccountGroup[]>({
    queryKey: ['account-groups', false],
    queryFn: async () => {
      const { data } = await api.get('/accounts/groups?activeOnly=false')
      return data.data
    },
  })

  const { data, isLoading } = useQuery<PagedResult<CurrentAccount>>({
    queryKey: ['accounts', accountType, groupId, isActive, search, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) })
      if (accountType) params.set('accountType', accountType)
      if (groupId) params.set('groupId', groupId)
      if (isActive !== '') params.set('isActive', isActive)
      if (search) params.set('search', search)
      const { data } = await api.get(`/accounts?${params}`)
      return data.data
    },
  })

  const accounts = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const totalPages = Math.ceil(totalCount / PAGE_SIZE)

  function handleSearch() {
    setSearch(searchInput)
    setPage(1)
  }

  function resetFilters() {
    setAccountType(''); setGroupId(''); setIsActive('')
    setSearch(''); setSearchInput(''); setPage(1)
  }

  if (isLoading && !data) return <PageSpinner />

  return (
    <div className="p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Cari Kartlar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{totalCount} kayıt</p>
        </div>
        <Button size="sm" onClick={() => navigate('/accounts/new')}>+ Yeni Cari</Button>
      </div>

      {/* Filters */}
      <div className="card p-4 mb-4">
        <div className="flex flex-wrap gap-3 items-end">
          {/* Search */}
          <div className="flex-1 min-w-[200px]">
            <label className="flbl">Ara</label>
            <div className="flex gap-2">
              <input className="inp flex-1" value={searchInput} placeholder="Ünvan, kod, vergi no, e-posta..."
                onChange={e => setSearchInput(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleSearch()} />
              <button onClick={handleSearch} className="px-3 py-2 rounded-xl"
                style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
                <Search size={14} style={{ color: 'var(--text-s)' }} />
              </button>
            </div>
          </div>
          {/* Type */}
          <div style={{ minWidth: 140 }}>
            <label className="flbl">Tip</label>
            <select className="inp" value={accountType} onChange={e => { setAccountType(e.target.value); setPage(1) }}>
              {ACCOUNT_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
            </select>
          </div>
          {/* Group */}
          <div style={{ minWidth: 160 }}>
            <label className="flbl">Grup</label>
            <select className="inp" value={groupId} onChange={e => { setGroupId(e.target.value); setPage(1) }}>
              <option value="">Tüm Gruplar</option>
              {groups.map(g => <option key={g.id} value={g.id}>{g.name}</option>)}
            </select>
          </div>
          {/* Status */}
          <div style={{ minWidth: 120 }}>
            <label className="flbl">Durum</label>
            <select className="inp" value={isActive} onChange={e => { setIsActive(e.target.value); setPage(1) }}>
              <option value="">Tümü</option>
              <option value="true">Aktif</option>
              <option value="false">Pasif</option>
            </select>
          </div>
          {(accountType || groupId || isActive || search) && (
            <button onClick={resetFilters} className="text-xs px-3 py-2 rounded-xl" style={{ color: 'var(--text-s)', border: '1px solid var(--border)' }}>
              Sıfırla
            </button>
          )}
        </div>
      </div>

      {/* Table */}
      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'ÜNVAN', 'TİP', 'GRUP', 'VERGİ NO', 'ŞEHİR', 'DURUM', ''].map(h => (
                <th key={h} className={cn('px-4 py-3 text-xs font-semibold', h === '' ? 'w-10' : 'text-left')} style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={8} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && accounts.length === 0 && (
              <tr><td colSpan={8} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Cari bulunamadı.</td></tr>
            )}
            {accounts.map(a => {
              const typeInfo = TYPE_BADGE[a.accountType]
              return (
                <tr key={a.id}
                  onClick={() => navigate(`/accounts/${a.id}`)}
                  className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                  style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-3">
                    <code className="text-xs px-2 py-0.5 rounded-md font-mono" style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>{a.code}</code>
                  </td>
                  <td className="px-4 py-3">
                    <div>
                      <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{a.title}</span>
                      {a.contactName && <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{a.contactName}</p>}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-xs font-medium px-2 py-0.5 rounded-full"
                      style={{ color: typeInfo?.color ?? 'var(--text-m)', background: `${typeInfo?.color ?? '#888'}18` }}>
                      {typeInfo?.label ?? a.accountType}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm" style={{ color: 'var(--text-s)' }}>{a.groupName ?? '—'}</span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm font-mono" style={{ color: 'var(--text-m)' }}>{a.taxNumber ?? '—'}</span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm" style={{ color: 'var(--text-s)' }}>{a.city ?? '—'}</span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <Badge variant={a.isActive ? 'success' : 'neutral'}>{a.isActive ? 'Aktif' : 'Pasif'}</Badge>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <ChevronRight size={14} style={{ color: 'var(--text-s)' }} />
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between mt-3">
          <p className="text-xs" style={{ color: 'var(--text-s)' }}>
            {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, totalCount)} / {totalCount}
          </p>
          <div className="flex items-center gap-1">
            <button onClick={() => setPage(1)} disabled={page === 1} className="px-2 py-1 rounded-lg text-xs disabled:opacity-40" style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>«</button>
            <button onClick={() => setPage(p => p - 1)} disabled={page === 1} className="px-2 py-1.5 rounded-lg text-xs disabled:opacity-40" style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>‹ Önceki</button>
            {Array.from({ length: totalPages }, (_, i) => i + 1)
              .filter(p => p === 1 || p === totalPages || Math.abs(p - page) <= 2)
              .reduce<(number | '…')[]>((acc, p, i, arr) => {
                if (i > 0 && (p as number) - (arr[i - 1] as number) > 1) acc.push('…')
                acc.push(p)
                return acc
              }, [])
              .map((p, i) => p === '…'
                ? <span key={`e${i}`} className="px-1 text-xs" style={{ color: 'var(--text-s)' }}>…</span>
                : <button key={p} onClick={() => setPage(p as number)}
                    className={cn('w-7 h-7 rounded-lg text-xs font-medium', page === p ? 'bg-[var(--brand)] text-white' : '')}
                    style={page !== p ? { border: '1px solid var(--border)', color: 'var(--text)' } : {}}>
                    {p}
                  </button>
              )}
            <button onClick={() => setPage(p => p + 1)} disabled={page === totalPages} className="px-2 py-1.5 rounded-lg text-xs disabled:opacity-40" style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Sonraki ›</button>
            <button onClick={() => setPage(totalPages)} disabled={page === totalPages} className="px-2 py-1 rounded-lg text-xs disabled:opacity-40" style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>»</button>
          </div>
        </div>
      )}
    </div>
  )
}
