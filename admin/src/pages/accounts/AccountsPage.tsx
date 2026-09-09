import { useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
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
  supplierKind: string
  ownerType: string
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
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (CurrentAccountGrid.Schema) + Excel + görünümler.
  // Tip/sahip/grup/durum süzgeçleri URL'de adlandırılmış filtre olarak durur (derin link bozulmaz).
  const navigate = useNavigate()
  const [sp] = useSearchParams()
  const accountType = sp.get('accountType') ?? ''
  const ownerType = sp.get('ownerType') ?? ''
  const groupId = sp.get('groupId') ?? ''
  const isActive = sp.get('isActive') ?? ''
  const grid = useGridState('accounts', { defaultPageSize: 30, defaultSort: 'title', defaultDir: 'asc' })
  const setNamed = (k: string, v: string) => grid.mutate(n => { if (v) n.set(k, v); else n.delete(k) })

  const { data: groups = [] } = useQuery<AccountGroup[]>({
    queryKey: ['account-groups', false],
    queryFn: async () => {
      const { data } = await api.get('/accounts/groups?activeOnly=false')
      return data.data
    },
  })

  const named = () => ({
    accountType: accountType || undefined,
    ownerType: ownerType || undefined,
    groupId: groupId || undefined,
    isActive: isActive !== '' ? isActive : undefined,
  })

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<CurrentAccount>>({
    queryKey: ['accounts', accountType, ownerType, groupId, isActive, ...grid.queryKey],
    queryFn: async () => (await api.get(`/accounts?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const accounts = data?.items ?? []

  const columns: GridColumn<CurrentAccount>[] = [
    { key: 'code', header: 'KOD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 120,
      filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      cell: a => <code className="text-xs px-2 py-0.5 rounded-md font-mono"
        style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>{a.code}</code> },
    { key: 'title', header: 'ÜNVAN', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 240,
      filter: { type: 'text', label: 'Ünvan' },
      filters: [{ field: 'contactName', label: 'Yetkili', type: 'text' }, { field: 'email', label: 'E-posta', type: 'text' }],
      cell: a => <div>
        <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{a.title}</span>
        {a.ownerType === 'member' && (
          <span className="text-xs font-medium px-1.5 py-0.5 rounded-full ml-1.5" style={{ color: 'var(--brand)', background: 'var(--brand)18' }}>Üye</span>
        )}
        {a.contactName && <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{a.contactName}</p>}
      </div> },
    { key: 'accountType', header: 'TİP', priority: 1, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Tip', options: ACCOUNT_TYPES.filter(t => t.value).map(t => ({ value: t.value, label: t.label })) },
      filters: [
        { field: 'ownerType', label: 'Sahiplik', type: 'enum', multiple: true, options: [
          { value: 'external', label: 'Harici Cari' }, { value: 'member', label: 'Üye (cüzdan)' }, { value: 'firm', label: 'Firma' }] },
        { field: 'supplierKind', label: 'Tedarikçi türü', type: 'enum', multiple: true, options: [
          { value: 'normal', label: 'Normal' }, { value: 'marketplace', label: 'Pazaryeri' }] }],
      cell: a => { const t = TYPE_BADGE[a.accountType]; return <>
        <span className="text-xs font-medium px-2 py-0.5 rounded-full"
          style={{ color: t?.color ?? 'var(--text-m)', background: `${t?.color ?? '#888'}18` }}>{t?.label ?? a.accountType}</span>
        {a.supplierKind === 'marketplace' && (
          <span className="text-xs font-medium px-2 py-0.5 rounded-full ml-1.5" style={{ color: '#0ea5e9', background: '#0ea5e918' }}>Pazaryeri</span>
        )}
      </> } },
    { key: 'group', header: 'GRUP', priority: 2, sortable: true,
      filter: { type: 'enum', label: 'Grup', field: 'groupId', options: groups.map(g => ({ value: g.id, label: g.name })) },
      cell: a => <span className="text-sm" style={{ color: 'var(--text-s)' }}>{a.groupName ?? '—'}</span> },
    { key: 'taxNumber', header: 'VERGİ NO', priority: 2, sortable: true, filter: { type: 'text', label: 'Vergi no' },
      cell: a => <span className="text-sm font-mono" style={{ color: 'var(--text-m)' }}>{a.taxNumber ?? '—'}</span> },
    { key: 'city', header: 'ŞEHİR', priority: 2, sortable: true, filter: { type: 'text', label: 'Şehir' },
      filters: [{ field: 'country', label: 'Ülke', type: 'enum' }],
      cell: a => <span className="text-sm" style={{ color: 'var(--text-s)' }}>{a.city ?? '—'}</span> },
    { key: 'creditLimit', header: 'KREDİ LİMİTİ', priority: 3, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Kredi limiti' },
      filters: [{ field: 'hasCreditLimit', label: 'Limiti olan', type: 'boolean' }, { field: 'currency', label: 'Para birimi', type: 'enum' }],
      cell: a => <span className="text-sm" style={{ color: 'var(--text-m)' }}>
        {a.creditLimit > 0 ? `${a.creditLimit.toLocaleString('tr-TR')} ${a.currency}` : '—'}</span> },
    { key: 'phone', header: 'TELEFON', priority: 3, defaultVisible: false, sortable: true, filter: { type: 'text', label: 'Telefon' },
      cell: a => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{a.phone ?? '—'}</span> },
    { key: 'isActive', header: 'DURUM', priority: 1, lockVisible: true, align: 'center', sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [{ field: 'createdAt', label: 'Oluşturma', type: 'date' }],
      cell: a => <Badge variant={a.isActive ? 'success' : 'neutral'}>{a.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'detail', header: '', priority: 3, align: 'right', exportable: false,
      cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Cari Kartlar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}
          </p>
        </div>
        <Button size="sm" onClick={() => navigate('/accounts/new')}>+ Yeni Cari</Button>
      </div>

      <DataGrid<CurrentAccount>
        gridId="accounts"
        views
        grid={grid}
        columns={columns}
        rows={accounts}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={a => navigate(`/accounts/${a.id}`)}
        empty="Cari bulunamadı."
        search={{ placeholder: 'Ünvan, kod, vergi no, e-posta…' }}
        minWidth={1060}
        filterLeading={
          <>
            <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={accountType} aria-label="Tip"
              onChange={e => setNamed('accountType', e.target.value)}>
              {ACCOUNT_TYPES.map(t => <option key={t.value} value={t.value}>{t.value ? t.label : 'Tip: Tümü'}</option>)}
            </select>
            <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={ownerType} aria-label="Sahip"
              onChange={e => setNamed('ownerType', e.target.value)}>
              <option value="">Sahip: Tümü</option>
              <option value="external">Harici Cari</option>
              <option value="member">Üye (cüzdan)</option>
            </select>
            <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={groupId} aria-label="Grup"
              onChange={e => setNamed('groupId', e.target.value)}>
              <option value="">Tüm Gruplar</option>
              {groups.map(g => <option key={g.id} value={g.id}>{g.name}</option>)}
            </select>
            <select className="inp text-sm !py-1.5 !px-2 !h-auto !w-auto" value={isActive} aria-label="Durum"
              onChange={e => setNamed('isActive', e.target.value)}>
              <option value="">Durum: Tümü</option>
              <option value="true">Aktif</option>
              <option value="false">Pasif</option>
            </select>
          </>
        }
        export={{ endpoint: '/accounts/export', named, fallbackFileName: 'cari-hesaplar.xlsx' }}
        compact={{
          title: a => a.title,
          subtitle: a => `${a.code}${a.groupName ? ` · ${a.groupName}` : ''}`,
          right: a => a.city ?? '',
          badge: a => <Badge variant={a.isActive ? 'success' : 'neutral'}>{a.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />
    </div>
  )
}
