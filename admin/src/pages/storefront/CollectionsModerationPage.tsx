import { useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'

// E6: üye koleksiyonu moderasyonu — Faz G "Koleksiyonlar bloğu" yalnız
// onaylı+herkese açık koleksiyonları gösterebilir (onay ekranı spec şartı).

interface ModerationCollection {
  id: string
  firmPlatformId: string
  memberId: string
  name: string
  description: string | null
  isPublic: boolean
  isShareable: boolean
  status: string
  isQuickSave: boolean
  itemCount: number
  createdAt: string
  moderatedAt: string | null
}

interface PagedResult {
  items: ModerationCollection[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

const STATUS_LABELS: Record<string, { label: string; variant: 'success' | 'warning' | 'danger' }> = {
  pending:  { label: 'Onay Bekliyor', variant: 'warning' },
  approved: { label: 'Onaylı',        variant: 'success' },
  rejected: { label: 'Reddedildi',    variant: 'danger' },
}

const TABS: { key: string; label: string }[] = [
  { key: 'pending',  label: 'Onay Bekleyen' },
  { key: 'approved', label: 'Onaylı' },
  { key: 'rejected', label: 'Reddedilen' },
]

export function CollectionsModerationPage() {
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (CollectionModerationGrid.Schema) + Excel + görünümler.
  // Kuyruk sırası EN ESKİ önce (şemadaki DefaultSort) — bekleyen ilk sırada kalsın.
  const [sp] = useSearchParams()
  const status = sp.get('status') ?? 'pending'
  const grid = useGridState('collections', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'asc' })
  const queryClient = useQueryClient()

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult>({
    queryKey: ['collections-moderation', status, ...grid.queryKey],
    queryFn: async () => (await api.get(`/collections?${grid.toParams({ status })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const moderate = useMutation({
    mutationFn: async ({ id, approve }: { id: string; approve: boolean }) => {
      await api.post(`/collections/${id}/${approve ? 'approve' : 'reject'}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['collections-moderation'] }),
  })

  const columns: GridColumn<ModerationCollection>[] = [
    { key: 'name', header: 'KOLEKSİYON', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 260,
      filter: { type: 'text', label: 'Koleksiyon adı' },
      filters: [
        { field: 'description', label: 'Açıklama', type: 'text' },
        { field: 'shareCode', label: 'Paylaşım kodu', type: 'text' },
        { field: 'isQuickSave', label: 'Otomatik (Kaydedilenler)', type: 'boolean' }],
      cell: k => <div>
        <div className="font-medium">
          {k.name}
          {k.isQuickSave && <span className="ml-2 text-xs text-[var(--text-s)]">(otomatik — Kaydedilenler)</span>}
        </div>
        {k.description && <div className="text-xs text-[var(--text-m)]">{k.description}</div>}
      </div> },
    { key: 'isPublic', header: 'GÖRÜNÜRLÜK', priority: 2, sortable: true,
      filter: { type: 'boolean', label: 'Herkese açık' },
      filters: [{ field: 'isShareable', label: 'Paylaşılabilir', type: 'boolean' }],
      cell: k => <span className="text-[var(--text-m)]">
        {k.isPublic ? 'Herkese açık' : 'Gizli'}{k.isShareable ? ' · Paylaşılabilir' : ''}</span> },
    { key: 'itemCount', header: 'ÜRÜN', priority: 1, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Ürün sayısı' },
      filters: [{ field: 'viewCount', label: 'Görüntülenme', type: 'number' }],
      cell: k => k.itemCount },
    { key: 'createdAt', header: 'OLUŞTURULMA', priority: 2, sortable: true, filter: { type: 'date', label: 'Oluşturulma', quick: true },
      filters: [{ field: 'moderatedAt', label: 'Moderasyon tarihi', type: 'date' }, { field: 'moderated', label: 'Moderasyondan geçti', type: 'boolean' }],
      cell: k => <span className="text-[var(--text-m)]">{new Date(k.createdAt).toLocaleDateString('tr-TR')}</span> },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(STATUS_LABELS).map(([value, v]) => ({ value, label: v.label })) },
      cell: k => <Badge variant={STATUS_LABELS[k.status]?.variant ?? 'neutral'}>{STATUS_LABELS[k.status]?.label ?? k.status}</Badge> },
    { key: 'actions', header: 'İŞLEM', priority: 1, align: 'right', exportable: false, stopRowClick: true, minWidth: 170,
      cell: k => <span className="whitespace-nowrap">
        {k.status !== 'approved' && (
          <Button size="sm" onClick={() => moderate.mutate({ id: k.id, approve: true })} disabled={moderate.isPending}>Onayla</Button>
        )}
        {k.status !== 'rejected' && (
          <Button size="sm" variant="danger" className="ml-2" onClick={() => moderate.mutate({ id: k.id, approve: false })} disabled={moderate.isPending}>Reddet</Button>
        )}
      </span> },
  ]

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Koleksiyon Moderasyonu</h1>
          <p className="text-sm text-[var(--text-m)]">
            Onaylı + herkese açık koleksiyonlar vitrin "Koleksiyonlar bloğu"nda kullanılabilir.
          </p>
        </div>
      </div>

      <div className="flex gap-2">
        {TABS.map(tab => (
          <button key={tab.key}
            onClick={() => grid.mutate(n => n.set('status', tab.key))}
            className={`rounded-lg px-3 py-1.5 text-sm ${
              status === tab.key
                ? 'bg-[var(--brand)] text-white'
                : 'bg-[var(--surface2)] text-[var(--text-m)] hover:text-[var(--text)]'
            }`}>
            {tab.label}
          </button>
        ))}
      </div>

      <DataGrid<ModerationCollection>
        gridId="collections"
        views
        grid={grid}
        columns={columns}
        rows={data?.items ?? []}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        empty="Bu durumda koleksiyon yok."
        search={{ placeholder: 'Koleksiyon adı, açıklama veya paylaşım kodu…' }}
        minWidth={1000}
        export={{ endpoint: '/collections/export', named: () => ({ status }), fallbackFileName: 'koleksiyonlar.xlsx' }}
        compact={{
          title: k => k.name,
          subtitle: k => `${k.isPublic ? 'Herkese açık' : 'Gizli'} · ${k.itemCount} ürün`,
          right: k => new Date(k.createdAt).toLocaleDateString('tr-TR'),
          badge: k => <Badge variant={STATUS_LABELS[k.status]?.variant ?? 'neutral'}>{STATUS_LABELS[k.status]?.label ?? k.status}</Badge>,
        }}
      />
    </div>
  )
}
