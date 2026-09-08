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

// Durum değerleri entity ile aynı: success | failure | pending | dry_run ("error" eski etiket, DB'de yok)
const DURUM: Record<string, [string, BadgeVariant]> = {
  success: ['Başarılı', 'success'],
  failure: ['Hata', 'danger'],
  error:   ['Hata', 'danger'],
  pending: ['Bekliyor', 'warning'],
  dry_run: ['Deneme', 'info'],
}

const SERVIS: Record<string, string> = {
  email: 'E-posta', cargo: 'Kargo', marketplace: 'Pazaryeri',
  einvoice: 'E-Fatura', visual_search: 'Görsel Arama', sms: 'SMS', legacy: 'Eski sistem',
}
const DURUM_SECENEK = ['success', 'failure', 'pending', 'dry_run'].map(value => ({ value, label: DURUM[value][0] }))

export function IntegrationLogsPage() {
  // DataGrid (2026-09-08): sunucu filtre/sıralama/arama (IntegrationLogGrid.Schema) + Excel export; durum sekmesi URL'de `?tab=` (adlandırılmış parametre).
  const grid = useGridState('integration-logs', { defaultPageSize: 50, defaultSort: 'createdAt', defaultDir: 'desc' })
  const [sp] = useSearchParams()
  const tab = sp.get('tab') ?? ''
  const setTab = (v: string) => grid.mutate(n => { if (v) n.set('tab', v); else n.delete('tab') })

  const { data, isLoading, isFetching, error } = useQuery<PagedResult<IntegrationLog>>({
    queryKey: ['integration-logs', tab, ...grid.queryKey],
    queryFn: async () => (await api.get(`/integrations/logs?${grid.toParams({ status: tab || undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const logs = data?.items ?? []
  const columns: GridColumn<IntegrationLog>[] = [
    { key: 'createdAt', header: 'TARİH', priority: 1, lockVisible: true, frozen: true, sortable: true,
      filter: { type: 'date', label: 'Tarih', quick: true }, cell: l => tarihSaat(l.createdAt) },
    { key: 'service', header: 'SERVİS', priority: 1, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Servis', options: Object.entries(SERVIS).map(([value, label]) => ({ value, label })) },
      cell: l => SERVIS[l.serviceType] ?? l.serviceType },
    { key: 'operation', header: 'İŞLEM', priority: 2, sortable: true, filter: { type: 'text', label: 'İşlem' },
      filters: [{ field: 'referenceType', label: 'Referans tipi', type: 'text' }],
      cell: l => <code className="text-xs font-mono">{l.operationType}</code> },
    { key: 'duration', header: 'SÜRE', priority: 3, sortable: true, align: 'right', filter: { type: 'number', label: 'Süre (ms)' }, cell: l => `${l.durationMs} ms` },
    { key: 'status', header: 'DURUM', priority: 1, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: DURUM_SECENEK },
      filters: [{ field: 'httpStatus', label: 'HTTP kodu', type: 'number' }],
      cell: l => { const [t, v] = DURUM[l.status] ?? [l.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{t}</Badge> } },
    {
      key: 'error', header: 'HATA', priority: 2, className: 'max-w-md', filter: { type: 'text', label: 'Hata metni', ops: ['contains', 'startswith'] },
      filters: [{ field: 'hasError', label: 'Hatası var', type: 'boolean' }],
      cell: l => (
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
          {(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''} — dış servis çağrılarının (e-posta, kargo, pazaryeri…) izleri
        </p>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {[['', 'Tümü'], ['success', 'Başarılı'], ['failure', 'Hatalı']].map(([v, l]) => (
          <button key={v} className={cn('stab', tab === v && 'active')}
            onClick={() => setTab(v)}>{l}</button>
        ))}
      </div>

      <DataGrid<IntegrationLog>
        gridId="integration-logs"
        views
        grid={grid}
        columns={columns}
        search={{ placeholder: 'İşlem, servis, hata metni ara…' }}
        export={{ endpoint: '/integrations/logs/export', named: () => ({ status: tab || undefined }), fallbackFileName: 'entegrasyon-loglari.xlsx' }}
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
