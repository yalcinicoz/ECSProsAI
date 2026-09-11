import { useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { cn } from '@/lib/utils'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'

// Şans Oyunları (docs/BACKEND_OYUNLAR.md, 2026-09-11): Çarkıfelek · Salla Kazan · Kazı Kazan — günlük kampanya kurgusu.
// Liste sunucu grid'li (GameGrid.Schema); sekme ?tab=live (bugün yayında) | all.

export interface GameRow {
  id: string; firmPlatformId: string; code: string; type: string; titleI18n: Record<string, string>
  startsAt: string; endsAt?: string | null; isActive: boolean; alwaysWin: boolean; limitPeriod: string; limitCount: number
  prizeCount: number; playCount: number; winCount: number; createdAt: string
}
interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

export const GAME_TYPES: Record<string, string> = { wheel: 'Çarkıfelek', shake: 'Salla Kazan', scratch: 'Kazı Kazan' }
export const LIMIT_PERIODS: Record<string, string> = { day: 'Günde', week: 'Haftada', total: 'Toplam' }
const tr = (m?: Record<string, string> | null) => m?.['tr'] ?? Object.values(m ?? {})[0] ?? '—'
const yayinda = (g: GameRow) => { const t = Date.now(); return g.isActive && new Date(g.startsAt).getTime() <= t && (!g.endsAt || new Date(g.endsAt).getTime() >= t) }

export function GamesPage() {
  const navigate = useNavigate()
  const grid = useGridState('games', { defaultPageSize: 50, defaultSort: 'createdAt', defaultDir: 'desc' })
  const [sp] = useSearchParams()
  const tab = sp.get('tab') === 'live' ? 'live' : 'all'

  const { data, isLoading, isFetching, error } = useQuery<PagedResult<GameRow>>({
    queryKey: ['games', tab, ...grid.queryKey],
    queryFn: async () => (await api.get(`/promotion/games?${grid.toParams(tab === 'live' ? { 'f.live': 'true' } : {})}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const rows = data?.items ?? []
  const switchTab = (key: 'live' | 'all') => grid.mutate(n => { if (key === 'all') n.delete('tab'); else n.set('tab', key) })

  const columns: GridColumn<GameRow>[] = [
    { key: 'title', header: 'OYUN', frozen: true, lockVisible: true, sortable: true, minWidth: 200, filter: { type: 'text', label: 'Ad' },
      cell: g => <div><div className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{tr(g.titleI18n)}</div><code className="text-xs" style={{ color: 'var(--text-s)' }}>{g.code}</code></div> },
    { key: 'type', header: 'TİP', sortable: true, priority: 1, filter: { type: 'enum', multiple: true, label: 'Tip', options: Object.entries(GAME_TYPES).map(([value, label]) => ({ value, label })) },
      cell: g => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{GAME_TYPES[g.type] ?? g.type}</span> },
    { key: 'limitPeriod', header: 'HAK', sortable: true, priority: 2, filter: { type: 'enum', multiple: true, label: 'Dönem', options: Object.entries(LIMIT_PERIODS).map(([value, label]) => ({ value, label })) }, filters: [{ field: 'limitCount', label: 'Hak sayısı', type: 'number' }],
      cell: g => <span className="text-xs" style={{ color: 'var(--text-m)' }}>{LIMIT_PERIODS[g.limitPeriod] ?? g.limitPeriod} {g.limitCount}</span> },
    { key: 'alwaysWin', header: 'KAZANMA', sortable: true, priority: 2, align: 'center', filter: { type: 'boolean', label: 'Herkes kazanır' },
      cell: g => <Badge variant={g.alwaysWin ? 'success' : 'neutral'}>{g.alwaysWin ? 'Herkes kazanır' : 'Şanslı'}</Badge> },
    { key: 'prizeCount', header: 'ÖDÜL', priority: 3, align: 'center', cell: g => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{g.prizeCount}</span> },
    { key: 'playCount', header: 'OYNANIŞ', sortable: true, priority: 2, align: 'right', filter: { type: 'number', label: 'Oynanış' },
      cell: g => <span className="text-sm tabular-nums" style={{ color: 'var(--text)' }}>{g.playCount}<span className="text-xs" style={{ color: 'var(--text-s)' }}> / {g.winCount} kazanan</span></span> },
    { key: 'startsAt', header: 'TARİH', sortable: true, priority: 2, filter: { type: 'date', label: 'Başlangıç', quick: true }, filters: [{ field: 'endsAt', label: 'Bitiş', type: 'date' }],
      cell: g => <span className="text-xs" style={{ color: 'var(--text-s)' }}>{new Date(g.startsAt).toLocaleDateString('tr-TR')} → {g.endsAt ? new Date(g.endsAt).toLocaleDateString('tr-TR') : 'süresiz'}</span> },
    { key: 'isActive', header: 'DURUM', lockVisible: true, sortable: true, priority: 1, filter: { type: 'boolean', label: 'Aktif', quick: true }, filters: [{ field: 'live', label: 'Yayında', type: 'boolean' }],
      cell: g => yayinda(g) ? <Badge variant="success">Yayında</Badge> : g.isActive ? <Badge variant="warning">Aktif (tarih dışı)</Badge> : <Badge variant="neutral">Pasif</Badge> },
    { key: 'edit', header: '', priority: 3, align: 'right', exportable: false, cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Şans Oyunları</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{(data?.totalCount ?? 0).toLocaleString('tr-TR')} oyun · yayındaki oyun mobil ana sayfada ikon olarak çıkar</p>
        </div>
        <Button size="sm" onClick={() => navigate('/promotion/games/new')}>+ Yeni Oyun</Button>
      </div>
      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'live' && 'active')} onClick={() => switchTab('live')}>Yayında</button>
        <button className={cn('stab', tab === 'all' && 'active')} onClick={() => switchTab('all')}>Tümü</button>
      </div>
      <DataGrid<GameRow>
        gridId="games" views grid={grid} columns={columns}
        search={{ placeholder: 'Oyun adı veya kodu…' }}
        rows={rows} totalCount={data?.totalCount ?? 0} loading={isLoading} fetching={isFetching}
        error={error ? errText(error) : null}
        onRowClick={g => navigate(`/promotion/games/${g.id}`)}
        empty={'Oyun yok. "+ Yeni Oyun" ile çark, salla kazan ya da kazı kazan tanımlayın.'}
        minWidth={820}
        compact={{ title: g => tr(g.titleI18n), subtitle: g => `${GAME_TYPES[g.type] ?? g.type} · ${LIMIT_PERIODS[g.limitPeriod]} ${g.limitCount}`, right: g => `${g.playCount} oynanış`,
          badge: g => yayinda(g) ? <Badge variant="success">Yayında</Badge> : <Badge variant="neutral">{g.isActive ? 'Tarih dışı' : 'Pasif'}</Badge> }}
      />
    </div>
  )
}
