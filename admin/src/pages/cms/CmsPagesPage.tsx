import { useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'
import { PAGE_TYPE_MAP, useFirmPlatforms } from './cmsPageShared'

const TABS = [
  { key: 'legal',     label: 'Yasal' },
  { key: 'corporate', label: 'Kurumsal' },
  { key: '',          label: 'Tümü' },
]

export interface CmsPageSummary {
  id: string
  code: string
  nameI18n: Record<string, string>
  slugI18n: Record<string, string>
  pageType: string
  isActive: boolean
  publishAt?: string
  unpublishAt?: string
  firmPlatformId?: string
  lastContentUpdatedAt?: string
}

/** DataGrid satırı — /cms/pages/grid (bölüm sayısı da gelir). */
interface CmsPageRow extends CmsPageSummary {
  sectionCount: number
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

export function CmsPagesPage() {
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (PageGrid.Schema) + Excel + görünümler.
  // ★ Ayrı uç: /cms/pages TAM liste döner (sayfa seçicileri); ekran /cms/pages/grid kullanır.
  // Y3: kanal kapsamı artık LİSTEYE de uygulanıyor (önce yalnız kanal parametresi denetleniyordu).
  const navigate = useNavigate()
  const [sp] = useSearchParams()
  const tab = sp.get('pageType') ?? 'legal'
  const platformId = sp.get('firmPlatformId') ?? ''
  const grid = useGridState('cms-pages', { defaultPageSize: 20, defaultSort: 'code', defaultDir: 'asc' })
  const setNamed = (k: string, v: string) => grid.mutate(n => { if (v) n.set(k, v); else n.delete(k) })

  const { data: platforms = [] } = useFirmPlatforms()
  const platformName = (pid?: string) =>
    platforms.find(p => p.id === pid)?.nameI18n?.['tr'] ?? '—'

  const named = () => ({ pageType: tab || undefined, firmPlatformId: platformId || undefined })

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<CmsPageRow>>({
    queryKey: ['cms-pages-grid', tab, platformId, ...grid.queryKey],
    queryFn: async () => (await api.get(`/cms/pages/grid?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const pages = data?.items ?? []

  const columns: GridColumn<CmsPageRow>[] = [
    { key: 'name', header: 'AD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 240,
      filter: { type: 'text', label: 'Ad' },
      filters: [{ field: 'code', label: 'Kod', type: 'text' }, { field: 'slug', label: 'Slug', type: 'text' }],
      cell: p => <div>
        <div className="text-sm font-medium" style={{ color: 'var(--text)' }}>{p.nameI18n?.['tr'] ?? p.code}</div>
        <code className="text-xs font-mono" style={{ color: 'var(--text-s)' }}>{p.code}</code>
      </div> },
    { key: 'pageType', header: 'TÜR', priority: 1, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Tür', options: Object.entries(PAGE_TYPE_MAP).map(([value, label]) => ({ value, label: String(label) })) },
      cell: p => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{PAGE_TYPE_MAP[p.pageType] ?? p.pageType}</span> },
    { key: 'firmPlatformId', header: 'PLATFORM', priority: 2, sortable: false,
      filter: { type: 'enum', label: 'Platform', options: platforms.map(pl => ({ value: pl.id, label: pl.nameI18n?.['tr'] ?? pl.id })) },
      cell: p => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{platformName(p.firmPlatformId)}</span> },
    { key: 'sectionCount', header: 'BÖLÜM', priority: 2, align: 'center', sortable: true, filter: { type: 'number', label: 'Bölüm sayısı' },
      cell: p => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{p.sectionCount}</span> },
    { key: 'isActive', header: 'AKTİF', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [
        { field: 'publishAt', label: 'Yayın tarihi', type: 'date' },
        { field: 'scheduled', label: 'Zamanlanmış', type: 'boolean' }],
      cell: p => <Badge variant={p.isActive ? 'success' : 'neutral'}>{p.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'lastContentUpdatedAt', header: 'SON İÇERİK', priority: 2, sortable: true,
      filter: { type: 'date', label: 'Son içerik değişikliği', quick: true },
      cell: p => <span className="text-xs" style={{ color: 'var(--text-s)' }}>
        {p.lastContentUpdatedAt ? new Date(p.lastContentUpdatedAt).toLocaleString('tr-TR') : '—'}</span> },
    { key: 'detail', header: '', priority: 3, align: 'right', exportable: false,
      cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Detay →</span> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>İçerik Sayfaları</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''} — sözleşme metinleri, kurumsal sayfalar ve SSS içeriği buradan yönetilir
          </p>
        </div>
        <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 180 }} aria-label="Platform"
          value={platformId} onChange={e => setNamed('firmPlatformId', e.target.value)}>
          <option value="">Tüm platformlar</option>
          {platforms.map(p => (
            <option key={p.id} value={p.id}>{p.nameI18n?.['tr'] ?? p.id}</option>
          ))}
        </select>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => (
          <button key={t.key} className={cn('stab', tab === t.key && 'active')}
            onClick={() => setNamed('pageType', t.key)}>{t.label}</button>
        ))}
      </div>

      <DataGrid<CmsPageRow>
        gridId="cms-pages"
        views
        grid={grid}
        columns={columns}
        rows={pages}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={p => navigate(`/cms/pages/${p.id}`)}
        empty="Sayfa bulunamadı."
        search={{ placeholder: 'Sayfa kodu veya adıyla ara…' }}
        minWidth={980}
        export={{ endpoint: '/cms/pages/export', named, fallbackFileName: 'icerik-sayfalari.xlsx' }}
        compact={{
          title: p => p.nameI18n?.['tr'] ?? p.code,
          subtitle: p => `${p.code} · ${PAGE_TYPE_MAP[p.pageType] ?? p.pageType}`,
          right: p => `${p.sectionCount} bölüm`,
          badge: p => <Badge variant={p.isActive ? 'success' : 'neutral'}>{p.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />
    </div>
  )
}
