import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { DataGrid, RowActions, useGridState, type GridColumn } from '@/components/grid'
import { errText, tarihSaat } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'
import { PLAN_DURUM, PLAN_TIP, dagitimDurum } from './pickingPlanHelpers'

interface PickingPlan {
  id: string
  planNumber: string
  warehouseId: string
  planType: string
  status: string
  plannedAt: string
  startedAt?: string
  completedAt?: string
  orderCount: number
  totalLines: number
  assignedLines: number
  pickedLines: number
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

const DAGITIM_SECENEK = [
  { value: 'none', label: 'Satır yok' }, { value: 'unassigned', label: 'Dağıtım yapılmadı' },
  { value: 'partial', label: 'Dağıtım eksik' }, { value: 'full', label: 'Dağıtım tamam' },
]

/** Toplanma ilerlemesi — picked/total + yüzde çubuğu. */
export function ToplanmaIlerleme({ picked, total }: { picked: number; total: number }) {
  if (total === 0) return <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>
  const pct = Math.round((picked / total) * 100)
  return (
    <div className="min-w-[110px]">
      <div className="flex justify-between text-xs mb-0.5" style={{ color: 'var(--text-s)' }}>
        <span>{picked}/{total}</span>
        <span>%{pct}</span>
      </div>
      <div className="h-1.5 rounded-full overflow-hidden" style={{ background: 'var(--surface2)' }}>
        <div className="h-full rounded-full transition-all"
          style={{ width: `${pct}%`, background: pct === 100 ? '#16a34a' : 'var(--brand)' }} />
      </div>
    </div>
  )
}

export function PickingPlansPage() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  // DataGrid (2026-09-08): sunucu filtre/sıralama/arama (PickingPlanGrid.Schema) + Excel export; durum sekmesi URL'de `?tab=` (adlandırılmış parametre).
  const grid = useGridState('picking-plans', { defaultPageSize: 20, defaultSort: 'plannedAt', defaultDir: 'desc' })
  const [sp] = useSearchParams()
  const tab = sp.get('tab') ?? ''
  const setTab = (v: string) => grid.mutate(n => { if (v) n.set('tab', v); else n.delete('tab') })
  const [error, setError] = useState('')

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<PickingPlan>>({
    queryKey: ['picking-plans', tab, ...grid.queryKey],
    queryFn: async () => (await api.get(`/fulfillment/picking-plans?${grid.toParams({ status: tab || undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const aksiyon = useMutation({
    mutationFn: async (url: string) => {
      setError('')
      await api.post(url, {})
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['picking-plans'] }),
    onError: (e: unknown) => setError(errText(e)),
  })

  const plans = data?.items ?? []
  const columns: GridColumn<PickingPlan>[] = [
    { key: 'planNumber', header: 'PLAN NO', priority: 1, lockVisible: true, frozen: true, sortable: true,
      filter: { type: 'text', label: 'Plan no', ops: ['startswith', 'contains', 'eq'] },
      cell: p => <code className="text-xs font-mono">{p.planNumber}</code> },
    {
      key: 'planType', header: 'TİP', priority: 2, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Tip', options: Object.entries(PLAN_TIP).map(([value, label]) => ({ value, label })) },
      cell: p => (
        <Badge variant={p.planType === 'single_item' ? 'info' : 'neutral'}>
          {PLAN_TIP[p.planType] ?? p.planType}
        </Badge>
      ),
    },
    { key: 'orderCount', header: 'SİPARİŞ', priority: 2, sortable: true, align: 'right', filter: { type: 'number', label: 'Sipariş sayısı' },
      cell: p => <span style={{ color: 'var(--text-m)' }}>{p.orderCount || '—'}</span> },
    {
      key: 'assignment', header: 'DAĞITIM', priority: 2,
      filter: { type: 'enum', multiple: true, label: 'Dağıtım', options: DAGITIM_SECENEK },
      cell: p => {
        const d = dagitimDurum(p.assignedLines, p.totalLines)
        return <Badge variant={d.variant}>{d.label}</Badge>
      },
    },
    { key: 'progress', header: 'TOPLANMA', priority: 1, sortable: true, filter: { type: 'number', label: 'Toplanma %' },
      filters: [{ field: 'pickedLines', label: 'Toplanan satır', type: 'number' }, { field: 'totalLines', label: 'Toplam satır', type: 'number' }],
      cell: p => <ToplanmaIlerleme picked={p.pickedLines} total={p.totalLines} /> },
    { key: 'plannedAt', header: 'PLANLAMA', priority: 3, sortable: true, filter: { type: 'date', label: 'Planlama', quick: true },
      filters: [{ field: 'startedAt', label: 'Başlangıç', type: 'date' }, { field: 'completedAt', label: 'Tamamlanma', type: 'date' }],
      cell: p => tarihSaat(p.plannedAt) },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(PLAN_DURUM).map(([value, [label]]) => ({ value, label })) },
      cell: p => { const [l, v] = PLAN_DURUM[p.status] ?? [p.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{l}</Badge> } },
    {
      key: 'actions', header: '', priority: 1, align: 'right', stopRowClick: true, exportable: false, cell: p => (
        <RowActions className="whitespace-nowrap">
          {p.status === 'pending' && (
            <button className="text-xs underline" style={{ color: 'var(--brand)' }}
              onClick={() => { if (window.confirm(`${p.planNumber} toplama başlatılsın mı?`)) aksiyon.mutate(`/fulfillment/picking-plans/${p.id}/start`) }}>
              Başlat
            </button>
          )}
          {p.status === 'picking' && (
            <button className="text-xs underline text-green-600"
              onClick={() => { if (window.confirm(`${p.planNumber} tamamlandı olarak işaretlensin mi?`)) aksiyon.mutate(`/fulfillment/picking-plans/${p.id}/complete`) }}>
              Tamamla
            </button>
          )}
        </RowActions>
      ),
    },
  ]

  return (
    <div className="p-6">
      <div className="mb-4 flex items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Toplama Görevleri</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}</p>
        </div>
        <Button size="sm" onClick={() => navigate('/fulfillment/tasks/new')}>+ Yeni Görev</Button>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {[['', 'Tümü'], ['pending', 'Bekleyen'], ['picking', 'Toplanan'], ['completed', 'Tamamlanan']].map(([v, l]) => (
          <button key={v} className={cn('stab', tab === v && 'active')}
            onClick={() => setTab(v)}>{l}</button>
        ))}
      </div>

      {error && <p className="text-sm text-red-500 mb-3">{error}</p>}

      <DataGrid<PickingPlan>
        gridId="picking-plans"
        views
        grid={grid}
        columns={columns}
        search={{ placeholder: 'Plan no ara…' }}
        export={{ endpoint: '/fulfillment/picking-plans/export', named: () => ({ status: tab || undefined }), fallbackFileName: 'toplama-planlari.xlsx' }}
        rows={plans}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        empty="Toplama görevi yok. 'Yeni Görev' ile filtreli görev oluşturabilirsiniz."
        onRowClick={p => navigate(`/fulfillment/tasks/${p.id}`)}
      />
    </div>
  )
}
