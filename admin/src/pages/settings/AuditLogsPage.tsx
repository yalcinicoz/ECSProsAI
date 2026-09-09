import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText, tarihSaat } from '@/components/ui/DataTable.utils'

interface AuditLog {
  id: string
  userId?: string
  userName?: string
  entityType: string
  entityId: string
  action: string
  ipAddress?: string
  createdAt: string
}
interface UserOpt { id: string; username: string }
interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

const EYLEM: Record<string, [string, BadgeVariant]> = {
  Created: ['Oluşturma', 'success'],
  Updated: ['Güncelleme', 'info'],
  Deleted: ['Silme', 'danger'],
  Published: ['Yayınlama', 'success'],
  Previewed: ['Önizleme', 'neutral'],
  Activated: ['Etkinleştirme', 'success'],
  Deactivated: ['Pasifleştirme', 'warning'],
  CredentialsRevealed: ['Kimlik bilgisi görüntüleme', 'warning'],
  grid_export: ['Excel dışa aktarma', 'neutral'],
  Login:   ['Giriş', 'neutral'],
}
// `action` sunucuda açık küme (izinli liste yok) — bilinen değerler seçenek olarak sunulur
const ACTION_OPTIONS = Object.entries(EYLEM).map(([value, [label]]) => ({ value, label }))

export function AuditLogsPage() {
  // DataGrid (2026-09-08): sunucu taraflı filtre/sıralama/global arama (AuditLogGrid.Schema) + Excel export; her sütunda başlık filtresi.
  const grid = useGridState('audit-logs', { defaultPageSize: 30, defaultSort: 'createdAt', defaultDir: 'desc' })

  const { data, isLoading, isFetching, error } = useQuery<PagedResult<AuditLog>>({
    queryKey: ['audit-logs', ...grid.queryKey],
    queryFn: async () => (await api.get(`/iam/audit-logs?${grid.toParams()}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const { data: users } = useQuery<PagedResult<UserOpt>>({
    queryKey: ['iam-users', 'audit-select'],
    queryFn: async () => (await api.get('/iam/users?pageSize=250&sort=username&dir=asc')).data.data,
    staleTime: 5 * 60_000,
  })
  const userOptions = useMemo(() => (users?.items ?? []).map(u => ({ value: u.id, label: u.username })), [users])

  const logs = data?.items ?? []
  const columns: GridColumn<AuditLog>[] = [
    { key: 'createdAt', header: 'TARİH', priority: 1, lockVisible: true, sortable: true, filter: { type: 'date', label: 'Tarih', quick: true }, cell: l => tarihSaat(l.createdAt) },
    { key: 'action', header: 'İŞLEM', priority: 1, sortable: true, filter: { type: 'enum', multiple: true, label: 'İşlem', options: ACTION_OPTIONS },
      cell: l => { const [t, v] = EYLEM[l.action] ?? [l.action, 'neutral' as BadgeVariant]; return <Badge variant={v}>{t}</Badge> } },
    { key: 'entityType', header: 'KAYIT TİPİ', priority: 1, sortable: true, filter: { type: 'text', label: 'Kayıt tipi', ops: ['contains', 'eq', 'startswith'] },
      cell: l => <code className="text-xs font-mono">{l.entityType}</code> },
    { key: 'entityId', header: 'KAYIT', sortable: true, priority: 2, filter: { type: 'text', label: 'Kayıt kimliği', ops: ['eq', 'contains'] },
      cell: l => <code className="text-xs font-mono" style={{ color: 'var(--text-s)' }} title={l.entityId}>{l.entityId.slice(0, 8)}…</code> },
    { key: 'user', header: 'KULLANICI', priority: 2, filter: { type: 'enum', label: 'Kullanıcı', field: 'userId', options: userOptions },
      cell: l => l.userName ?? (l.userId ? <span title={l.userId}>{l.userId.slice(0, 8)}…</span> : '—') },
    { key: 'ip', header: 'IP', priority: 3, sortable: true, filter: { type: 'text', label: 'IP', ops: ['startswith', 'contains'] }, cell: l => l.ipAddress ?? '—' },
  ]

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Denetim Logları</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''} — panelde yapılan işlemlerin izleri (salt okunur)
        </p>
      </div>

      <DataGrid<AuditLog>
        gridId="audit-logs"
        views
        grid={grid}
        columns={columns}
        rows={logs}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={error ? errText(error) : null}
        empty="Denetim logu yok."
        search={{ placeholder: 'Kayıt tipi, işlem, kayıt kimliği, IP ara…' }}
        export={{ endpoint: '/iam/audit-logs/export', fallbackFileName: 'denetim-loglari.xlsx' }}
        compact={{
          title: l => l.entityType,
          subtitle: l => `${l.userName ?? '—'} · ${new Date(l.createdAt).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' })}`,
          right: l => l.ipAddress ?? '',
          badge: l => { const [t, v] = EYLEM[l.action] ?? [l.action, 'neutral' as BadgeVariant]; return <Badge variant={v}>{t}</Badge> },
        }}
      />
    </div>
  )
}
