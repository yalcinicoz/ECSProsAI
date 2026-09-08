import { useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { cn } from '@/lib/utils'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'

// Kampanyalar — DataGrid F4: `page` parametresiyle sayfalı uç (CampaignGrid.Schema); sekme ?tab=active|all (all varsayılan).
// Ad (NameI18n jsonb "tr") sunucuda GridJson DbFunction ile filtrelenir/sıralanır; arama kod + rozet + ad. Her sütun başlığında filtre.

interface CampaignType { id: string; code: string; nameI18n: Record<string, string> }
interface Campaign {
  id: string; code: string; nameI18n: Record<string, string>
  startsAt: string; endsAt?: string; isActive: boolean; priority: number
  fillType: string; campaignTypeId?: string; campaignTypeCode?: string
}
interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

const tr = (m?: Record<string, string> | null) => m?.['tr'] ?? Object.values(m ?? {})[0] ?? '—'
const FILL_LABEL: Record<string, string> = { all: 'Tüm ürünler', manual: 'Manuel', filter: 'Filtre', mixed: 'Karma' }

export function CampaignsPage() {
  const navigate = useNavigate()
  const grid = useGridState('campaigns', { defaultPageSize: 50, defaultSort: 'priority', defaultDir: 'desc' })
  const [sp] = useSearchParams()
  const tab = sp.get('tab') === 'active' ? 'active' : 'all'

  const { data, isLoading, isFetching, error } = useQuery<PagedResult<Campaign>>({
    queryKey: ['campaigns', tab, ...grid.queryKey],
    queryFn: async () => (await api.get(`/promotion/campaigns?${grid.toParams({ activeOnly: String(tab === 'active') })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const { data: types = [] } = useQuery<CampaignType[]>({
    queryKey: ['campaign-types', 'all'],
    queryFn: async () => (await api.get('/promotion/campaign-types?activeOnly=false')).data.data,
  })
  const typeName = (tid?: string, tcode?: string) => tr(types.find(t => t.id === tid)?.nameI18n) ?? tcode ?? '—'
  const campaigns = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const switchTab = (key: 'active' | 'all') => grid.mutate(n => { if (key === 'all') n.delete('tab'); else n.set('tab', key) })

  const columns: GridColumn<Campaign>[] = [
    { key: 'code', header: 'KOD', filters: [{ field: 'badgeLabel', label: 'Rozet', type: 'text' }], frozen: true, lockVisible: true, sortable: true, minWidth: 120,
      cell: c => <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{c.code}</code> },
    { key: 'name', header: 'AD', priority: 1, exportable: true, sortable: true, filter: { type: 'text', label: 'Ad' }, cell: c => <span className="text-sm" style={{ color: 'var(--text)' }}>{tr(c.nameI18n)}</span> },
    { key: 'campaignTypeCode', header: 'TİP', priority: 2, filter: { type: 'enum', multiple: true, label: 'Tip', options: types.map(t => ({ value: t.code, label: tr(t.nameI18n) })) }, cell: c => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{typeName(c.campaignTypeId, c.campaignTypeCode)}</span> },
    { key: 'fillType', header: 'KAPSAM', filters: [{ field: 'fillType', label: 'Kapsam', type: 'enum', multiple: true, options: Object.entries(FILL_LABEL).map(([value, label]) => ({ value, label })) }], priority: 3, cell: c => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{FILL_LABEL[c.fillType] ?? c.fillType}</span> },
    { key: 'startsAt', header: 'TARİH', filters: [{ field: 'endsAt', label: 'Bitiş', type: 'date' }], sortable: true, priority: 2, filter: { type: 'date', label: 'Başlangıç', quick: true },
      cell: c => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{new Date(c.startsAt).toLocaleDateString('tr-TR')} → {c.endsAt ? new Date(c.endsAt).toLocaleDateString('tr-TR') : 'süresiz'}</span> },
    { key: 'priority', header: 'ÖNCELİK', filters: [{ field: 'priority', label: 'Öncelik', type: 'number' }], sortable: true, align: 'right', priority: 2, cell: c => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{c.priority}</span> },
    { key: 'isActive', header: 'DURUM', lockVisible: true, sortable: true, priority: 1, filter: { type: 'boolean', label: 'Aktif', quick: true }, cell: c => <Badge variant={c.isActive ? 'success' : 'neutral'}>{c.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'edit', header: '', priority: 3, align: 'right', exportable: false, cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kampanyalar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{totalCount.toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}</p>
        </div>
        <Button size="sm" onClick={() => navigate('/promotion/campaigns/new')}>+ Yeni Kampanya</Button>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'active' && 'active')} onClick={() => switchTab('active')}>Yayında</button>
        <button className={cn('stab', tab === 'all' && 'active')} onClick={() => switchTab('all')}>Tümü</button>
      </div>

      <DataGrid<Campaign>
        gridId="campaigns"
        views
        grid={grid}
        columns={columns}
        search={{ placeholder: 'Kampanya kodu veya rozet etiketi…' }}
        rows={campaigns}
        totalCount={totalCount}
        loading={isLoading}
        fetching={isFetching}
        error={error ? errText(error) : null}
        onRowClick={c => navigate(`/promotion/campaigns/${c.id}`)}
        empty={'Kampanya yok. "+ Yeni Kampanya" ile tanımlayın.'}
        minWidth={820}
        export={{ endpoint: '/promotion/campaigns/export', named: () => ({ activeOnly: String(tab === 'active') }), fallbackFileName: 'kampanyalar.xlsx' }}
      />
    </div>
  )
}
