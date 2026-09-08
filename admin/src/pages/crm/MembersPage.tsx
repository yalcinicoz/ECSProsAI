import { useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { cn } from '@/lib/utils'
import { errText } from '@/components/ui/DataTable.utils'
import { DataGrid, useGridState, type GridColumn, type GridFilterField } from '@/components/grid'

// Üyeler — DataGrid göçü (docs/datagrid-standardi-plani.md F4). Aktif/Tümü sekmesi `?tab=` ile URL'de (activeOnly named parametresi
// korunur); filtre/arama/sıralama/sayfa URL'de, kolon tercihleri localStorage'da. Sunucu beyaz listesi: MemberGrid.Schema.

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

const GENDER_OPTIONS = [
  { value: 'male', label: 'Erkek' },
  { value: 'female', label: 'Kadın' },
  { value: 'unisex', label: 'Belirtilmemiş' },
  { value: 'none', label: 'Boş' },
]

const EXTRA_FILTERS: GridFilterField[] = [
  { key: 'isRegistered', label: 'Kayıtlı üye', type: 'boolean', quick: true },
]

export function MembersPage() {
  const navigate = useNavigate()
  const grid = useGridState('members', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })
  const [sp] = useSearchParams()
  const tab: 'active' | 'all' = sp.get('tab') === 'all' ? 'all' : 'active'
  const activeOnly = tab === 'active'

  const { data, isLoading, isFetching, error } = useQuery<PagedResult<MemberSummary>>({
    queryKey: ['members', tab, ...grid.queryKey],
    queryFn: async () => (await api.get(`/crm/members?${grid.toParams({ activeOnly: String(activeOnly) })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const members = data?.items ?? []
  const totalCount = data?.totalCount ?? 0

  const columns: GridColumn<MemberSummary>[] = [
    { key: 'name', header: 'AD SOYAD', filters: [{ field: 'firstName', label: 'Ad', type: 'text' }, { field: 'lastName', label: 'Soyad', type: 'text' }, { field: 'gender', label: 'Cinsiyet', type: 'enum', multiple: true, options: GENDER_OPTIONS }, { field: 'companyName', label: 'Şirket', type: 'text' }, { field: 'taxNumber', label: 'Vergi no', type: 'text', ops: ['contains', 'startswith', 'eq'] }], frozen: true, lockVisible: true, priority: 1, minWidth: 160,
      cell: m => <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{m.firstName} {m.lastName}</span> },
    { key: 'email', header: 'E-POSTA', filters: [{ field: 'isEmailVerified', label: 'E-posta doğrulandı', type: 'boolean' }], sortable: true, priority: 1, filter: { type: 'text', label: 'E-posta' },
      cell: m => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{m.email ?? '—'}</span> },
    { key: 'phone', header: 'TELEFON', filters: [{ field: 'isPhoneVerified', label: 'Telefon doğrulandı', type: 'boolean' }], sortable: true, priority: 2, filter: { type: 'text', label: 'Telefon', ops: ['contains', 'startswith'] },
      cell: m => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{m.phone ?? '—'}</span> },
    { key: 'isRegistered', header: 'ÜYELİK', filters: [{ field: 'legacyMemberId', label: 'Eski üye no', type: 'number' }], sortable: true, priority: 3,
      cell: m => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{m.isRegistered ? 'Kayıtlı' : 'Misafir'}</span> },
    { key: 'isActive', header: 'DURUM', sortable: true, priority: 1, lockVisible: true, filter: { type: 'boolean', label: 'Aktif' },
      cell: m => <Badge variant={m.isActive ? 'success' : 'neutral'}>{m.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'createdAt', header: 'KAYIT', filters: [{ field: 'lastLoginAt', label: 'Son giriş', type: 'date' }], sortable: true, priority: 2, filter: { type: 'date', label: 'Kayıt tarihi', quick: true },
      cell: m => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{new Date(m.createdAt).toLocaleDateString('tr-TR')}</span> },
    { key: 'firstName', header: 'AD', sortable: true, priority: 3, defaultVisible: false, filter: { type: 'text', label: 'Ad' }, cell: m => m.firstName },
    { key: 'lastName', header: 'SOYAD', sortable: true, priority: 3, defaultVisible: false, filter: { type: 'text', label: 'Soyad' }, cell: m => m.lastName },
    { key: 'detail', header: '', priority: 3, align: 'right', exportable: false, cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Üyeler</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {totalCount.toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}
          </p>
        </div>
      </div>

      {/* Aktif / Tümü — URL ?tab=all (aktif = parametresiz) */}
      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'active' && 'active')} onClick={() => grid.mutate(n => n.delete('tab'))}>Aktif</button>
        <button className={cn('stab', tab === 'all' && 'active')} onClick={() => grid.mutate(n => n.set('tab', 'all'))}>Tümü</button>
      </div>

      <DataGrid<MemberSummary>
        gridId="members"
        views
        grid={grid}
        columns={columns}
        extraFilters={EXTRA_FILTERS}
        search={{ placeholder: 'Ad, soyad, e-posta, telefon, şirket…' }}
        rows={members}
        totalCount={totalCount}
        loading={isLoading}
        fetching={isFetching}
        error={error ? errText(error) : null}
        onRowClick={m => navigate(`/crm/members/${m.id}`)}
        empty="Üye bulunamadı."
        minWidth={760}
        export={{ endpoint: '/crm/members/export', named: () => ({ activeOnly: String(activeOnly) }), fallbackFileName: 'uyeler.xlsx' }}
      />
    </div>
  )
}
