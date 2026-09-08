import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText, tarihSaat } from '@/components/ui/DataTable.utils'
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
  // DataGrid F4 (mekanik göç): durum sekmesi URL'de `?tab=`, sayfa/sayfa boyu grid'de.
  const grid = useGridState('integration-logs', { defaultPageSize: 50 })
  const [sp] = useSearchParams()
  const tab = sp.get('tab') ?? ''
  const setTab = (v: string) => grid.mutate(n => { if (v) n.set('tab', v); else n.delete('tab') })

  const { data, isLoading, isFetching, error } = useQuery<PagedResult<IntegrationLog>>({
    queryKey: ['integration-logs', tab, ...grid.queryKey],
    queryFn: async () => (await api.get(`/integrations/logs?${grid.toParams({ status: tab || undefined })}`)).data.data,
    placeholderData: prev => prev,
  })

  const logs = data?.items ?? []
  const columns: GridColumn<IntegrationLog>[] = [
    { key: 'createdAt', header: 'TARİH', priority: 1, lockVisible: true, frozen: true, cell: l => tarihSaat(l.createdAt) },
    { key: 'service', header: 'SERVİS', priority: 1, cell: l => SERVIS[l.serviceType] ?? l.serviceType },
    { key: 'operation', header: 'İŞLEM', priority: 2, cell: l => <code className="text-xs font-mono">{l.operationType}</code> },
    { key: 'duration', header: 'SÜRE', priority: 3, cell: l => `${l.durationMs} ms` },
    { key: 'status', header: 'DURUM', priority: 1, cell: l => { const [t, v] = DURUM[l.status] ?? [l.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{t}</Badge> } },
    {
      key: 'error', header: 'HATA', priority: 2, className: 'max-w-md', cell: l => (
        l.errorMessage
          ? <span className="text-xs text-red-600" title={l.errorMessage}>{l.errorMessage.slice(0, 120)}</span>
          : <span style={{ color: 'var(--text-s)' }}>—</span>
      ),
    },
  ]

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
            onClick={() => setTab(v)}>{l}</button>
        ))}
      </div>

      <DataGrid<IntegrationLog>
        gridId="integration-logs"
        grid={grid}
        columns={columns}
        rows={logs}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={error ? errText(error) : null}
        empty="Entegrasyon logu yok — dış servis çağrısı yapıldıkça burada listelenir."
      />
    </div>
  )
}
