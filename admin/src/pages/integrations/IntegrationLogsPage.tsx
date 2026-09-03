import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { DataTable, Pager } from '@/components/ui/DataTable'
import { tarihSaat } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'

interface IntegrationLog {
  id: string
  firmIntegrationId: string
  serviceType: string
  operationType: string
  status: string
  errorMessage?: string
  durationMs: number
  referenceType?: string
  createdAt: string
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

const DURUM: Record<string, [string, BadgeVariant]> = {
  success: ['Başarılı', 'success'],
  error:   ['Hata', 'danger'],
  pending: ['Bekliyor', 'warning'],
}

const SERVIS: Record<string, string> = {
  email: 'E-posta', cargo: 'Kargo', marketplace: 'Pazaryeri',
  einvoice: 'E-Fatura', visual_search: 'Görsel Arama', sms: 'SMS',
}

export function IntegrationLogsPage() {
  const [tab, setTab] = useState('')
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery<PagedResult<IntegrationLog>>({
    queryKey: ['integration-logs', tab, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '50' })
      if (tab) params.set('status', tab)
      return (await api.get(`/integrations/logs?${params}`)).data.data
    },
  })

  const logs = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 50)

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Entegrasyon Logları</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {data?.totalCount ?? 0} kayıt — dış servis çağrılarının (e-posta, kargo, pazaryeri…) izleri
        </p>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {[['', 'Tümü'], ['success', 'Başarılı'], ['error', 'Hatalı']].map(([v, l]) => (
          <button key={v} className={cn('stab', tab === v && 'active')}
            onClick={() => { setTab(v); setPage(1) }}>{l}</button>
        ))}
      </div>

      <DataTable<IntegrationLog>
        columns={[
          { header: 'TARİH', cell: l => tarihSaat(l.createdAt) },
          { header: 'SERVİS', cell: l => SERVIS[l.serviceType] ?? l.serviceType },
          { header: 'İŞLEM', cell: l => <code className="text-xs font-mono">{l.operationType}</code> },
          { header: 'SÜRE', cell: l => `${l.durationMs} ms` },
          { header: 'DURUM', cell: l => { const [t, v] = DURUM[l.status] ?? [l.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{t}</Badge> } },
          {
            header: 'HATA', className: 'max-w-md', cell: l => (
              l.errorMessage
                ? <span className="text-xs text-red-600" title={l.errorMessage}>{l.errorMessage.slice(0, 120)}</span>
                : <span style={{ color: 'var(--text-s)' }}>—</span>
            ),
          },
        ]}
        rows={logs}
        loading={isLoading}
        empty="Entegrasyon logu yok — dış servis çağrısı yapıldıkça burada listelenir."
      />

      <Pager page={page} totalPages={totalPages} onChange={setPage} />
    </div>
  )
}
