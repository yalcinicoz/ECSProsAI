import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'
import { useFirmPlatforms } from '@/pages/cms/cmsPageShared'

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
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (NewsletterSubscriptionGrid.Schema) + Excel + görünümler.
  // Y3: kanal kapsamı artık LİSTEYE de uygulanıyor (kapsam sunucuda kullanıcının yetkisinden çözülür).
  const [sp] = useSearchParams()
  const tab: 'active' | 'all' = sp.get('tab') === 'all' ? 'all' : 'active'
  const platformId = sp.get('firmPlatformId') ?? ''
  const grid = useGridState('newsletter', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })

  const { data: platforms = [] } = useFirmPlatforms()
  const platformName = (pid?: string) =>
    platforms.find(p => p.id === pid)?.nameI18n?.['tr'] ?? '—'

  const named = () => ({
    isActive: tab === 'active' ? 'true' : undefined,
    firmPlatformId: platformId || undefined,
  })

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<NewsletterSubscription>>({
    queryKey: ['newsletter-subscriptions', tab, platformId, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/store-notifications/newsletter-subscriptions?${grid.toParams(named())}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const subs = data?.items ?? []

  const columns: GridColumn<NewsletterSubscription>[] = [
    { key: 'email', header: 'E-POSTA', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 240,
      filter: { type: 'text', label: 'E-posta', ops: ['contains', 'startswith', 'eq'] },
      cell: s => <span className="text-sm" style={{ color: 'var(--text)' }}>{s.email}</span> },
    { key: 'firmPlatformId', header: 'PLATFORM', priority: 2, sortable: false,
      filter: { type: 'enum', label: 'Platform', options: platforms.map(p => ({ value: p.id, label: p.nameI18n?.['tr'] ?? p.id })) },
      cell: s => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{platformName(s.firmPlatformId)}</span> },
    { key: 'isMember', header: 'ÜYE', priority: 2, sortable: true,
      filter: { type: 'boolean', label: 'Üye aboneliği' },
      cell: s => <span className="text-xs" style={{ color: 'var(--text-s)' }}>
        {s.memberId ? <code>{s.memberId.slice(0, 8)}…</code> : 'Misafir'}</span> },
    { key: 'isActive', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      cell: s => <Badge variant={s.isActive ? 'success' : 'neutral'}>{s.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'createdAt', header: 'KAYIT TARİHİ', priority: 1, sortable: true, filter: { type: 'date', label: 'Kayıt tarihi', quick: true },
      cell: s => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{new Date(s.createdAt).toLocaleString('tr-TR')}</span> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Bülten Aboneleri</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''} — footer bülten formundan gelen abonelikler
          </p>
        </div>
        <select className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 180 }} aria-label="Platform"
          value={platformId}
          onChange={e => grid.mutate(n => { if (e.target.value) n.set('firmPlatformId', e.target.value); else n.delete('firmPlatformId') })}>
          <option value="">Tüm platformlar</option>
          {platforms.map(p => (
            <option key={p.id} value={p.id}>{p.nameI18n?.['tr'] ?? p.id}</option>
          ))}
        </select>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'active' && 'active')}
          onClick={() => grid.mutate(n => n.delete('tab'))}>Aktif</button>
        <button className={cn('stab', tab === 'all' && 'active')}
          onClick={() => grid.mutate(n => n.set('tab', 'all'))}>Tümü</button>
      </div>

      <DataGrid<NewsletterSubscription>
        gridId="newsletter"
        views
        grid={grid}
        columns={columns}
        rows={subs}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        empty="Abone yok. Kayıtlar sitedeki footer bülten formundan gelir."
        search={{ placeholder: 'E-posta ara…' }}
        minWidth={820}
        export={{ endpoint: '/store-notifications/newsletter-subscriptions/export', named, fallbackFileName: 'bulten-aboneleri.xlsx' }}
        compact={{
          title: s => s.email,
          subtitle: s => `${platformName(s.firmPlatformId)} · ${s.memberId ? 'Üye' : 'Misafir'}`,
          right: s => new Date(s.createdAt).toLocaleDateString('tr-TR'),
          badge: s => <Badge variant={s.isActive ? 'success' : 'neutral'}>{s.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />
    </div>
  )
}
