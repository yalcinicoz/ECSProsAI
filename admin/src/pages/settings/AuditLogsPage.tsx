import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText, tarihSaat } from '@/components/ui/DataTable.utils'

interface AuditLog {
  id: string
  userId?: string
  entityType: string
  entityId: string
  action: string
  ipAddress?: string
  createdAt: string
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

const EYLEM: Record<string, [string, BadgeVariant]> = {
  Created: ['Oluşturma', 'success'],
  Updated: ['Güncelleme', 'info'],
  Deleted: ['Silme', 'danger'],
  Login:   ['Giriş', 'neutral'],
}

export function AuditLogsPage() {
  // DataGrid F4 (mekanik göç): entityType süzgeci URL'de `search` olarak taşınır (uca `entityType` adıyla gider), sayfa/sayfa boyu grid'de.
  const grid = useGridState('audit-logs', { defaultPageSize: 30 })
  const [entityType, setEntityType] = useState(grid.state.search)
  const applied = grid.state.search

  const { data, isLoading, isFetching, error } = useQuery<PagedResult<AuditLog>>({
    queryKey: ['audit-logs', ...grid.queryKey],
    queryFn: async () => {
      const params = grid.toParams({ entityType: applied || undefined })
      params.delete('search')
      return (await api.get(`/iam/audit-logs?${params}`)).data.data
    },
    placeholderData: prev => prev,
  })

  const logs = data?.items ?? []
  const columns: GridColumn<AuditLog>[] = [
    { key: 'createdAt', header: 'TARİH', priority: 1, lockVisible: true, cell: l => tarihSaat(l.createdAt) },
    { key: 'action', header: 'İŞLEM', priority: 1, cell: l => { const [t, v] = EYLEM[l.action] ?? [l.action, 'neutral' as BadgeVariant]; return <Badge variant={v}>{t}</Badge> } },
    { key: 'entityType', header: 'KAYIT TİPİ', priority: 1, cell: l => <code className="text-xs font-mono">{l.entityType}</code> },
    { key: 'entityId', header: 'KAYIT', priority: 2, cell: l => <code className="text-xs font-mono" style={{ color: 'var(--text-s)' }}>{l.entityId.slice(0, 8)}…</code> },
    { key: 'ip', header: 'IP', priority: 3, cell: l => l.ipAddress ?? '—' },
  ]

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Denetim Logları</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {data?.totalCount ?? 0} kayıt — panelde yapılan işlemlerin izleri (salt okunur)
        </p>
      </div>

      <DataGrid<AuditLog>
        gridId="audit-logs"
        grid={grid}
        columns={columns}
        rows={logs}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={error ? errText(error) : null}
        empty="Denetim logu yok."
        toolbarLeft={
          <div className="flex items-center gap-2">
            <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 220 }}
              placeholder="Kayıt tipi süz (ör. User, Page)…" value={entityType}
              onChange={e => setEntityType(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter') grid.setSearch(entityType) }} />
            <button onClick={() => grid.setSearch(entityType)}
              className="px-3 py-1.5 rounded-lg text-sm"
              style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Süz</button>
            {applied && (
              <button onClick={() => { setEntityType(''); grid.setSearch('') }} className="text-xs underline" style={{ color: 'var(--text-s)' }}>temizle</button>
            )}
          </div>
        }
      />
    </div>
  )
}
