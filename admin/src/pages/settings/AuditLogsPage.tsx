import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { DataTable, Pager, tarihSaat } from '@/components/ui/DataTable'

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
  const [entityType, setEntityType] = useState('')
  const [applied, setApplied] = useState('')
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery<PagedResult<AuditLog>>({
    queryKey: ['audit-logs', applied, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '30' })
      if (applied) params.set('entityType', applied)
      return (await api.get(`/iam/audit-logs?${params}`)).data.data
    },
  })

  const logs = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 30)

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Denetim Logları</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {data?.totalCount ?? 0} kayıt — panelde yapılan işlemlerin izleri (salt okunur)
        </p>
      </div>

      <div className="flex items-center gap-2 mb-4">
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 220 }}
          placeholder="Kayıt tipi süz (ör. User, Page)…" value={entityType}
          onChange={e => setEntityType(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') { setApplied(entityType.trim()); setPage(1) } }} />
        <button onClick={() => { setApplied(entityType.trim()); setPage(1) }}
          className="px-3 py-1.5 rounded-lg text-sm"
          style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Süz</button>
      </div>

      <DataTable<AuditLog>
        columns={[
          { header: 'TARİH', cell: l => tarihSaat(l.createdAt) },
          { header: 'İŞLEM', cell: l => { const [t, v] = EYLEM[l.action] ?? [l.action, 'neutral' as BadgeVariant]; return <Badge variant={v}>{t}</Badge> } },
          { header: 'KAYIT TİPİ', cell: l => <code className="text-xs font-mono">{l.entityType}</code> },
          { header: 'KAYIT', cell: l => <code className="text-xs font-mono" style={{ color: 'var(--text-s)' }}>{l.entityId.slice(0, 8)}…</code> },
          { header: 'IP', cell: l => l.ipAddress ?? '—' },
        ]}
        rows={logs}
        loading={isLoading}
        empty="Denetim logu yok."
      />

      <Pager page={page} totalPages={totalPages} onChange={setPage} />
    </div>
  )
}
