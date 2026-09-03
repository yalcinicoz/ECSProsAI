import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { DataTable, Pager } from '@/components/ui/DataTable'
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
  const [tab, setTab] = useState('')
  const [page, setPage] = useState(1)
  const [error, setError] = useState('')

  const { data, isLoading } = useQuery<PagedResult<PickingPlan>>({
    queryKey: ['picking-plans', tab, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (tab) params.set('status', tab)
      return (await api.get(`/fulfillment/picking-plans?${params}`)).data.data
    },
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
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 20)

  return (
    <div className="p-6">
      <div className="mb-4 flex items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Toplama Görevleri</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{data?.totalCount ?? 0} kayıt</p>
        </div>
        <Button size="sm" onClick={() => navigate('/fulfillment/tasks/new')}>+ Yeni Görev</Button>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {[['', 'Tümü'], ['pending', 'Bekleyen'], ['picking', 'Toplanan'], ['completed', 'Tamamlanan']].map(([v, l]) => (
          <button key={v} className={cn('stab', tab === v && 'active')}
            onClick={() => { setTab(v); setPage(1) }}>{l}</button>
        ))}
      </div>

      {error && <p className="text-sm text-red-500 mb-3">{error}</p>}

      <DataTable<PickingPlan>
        columns={[
          { header: 'PLAN NO', cell: p => <code className="text-xs font-mono">{p.planNumber}</code> },
          {
            header: 'TİP', cell: p => (
              <Badge variant={p.planType === 'single_item' ? 'info' : 'neutral'}>
                {PLAN_TIP[p.planType] ?? p.planType}
              </Badge>
            ),
          },
          { header: 'SİPARİŞ', cell: p => <span style={{ color: 'var(--text-m)' }}>{p.orderCount || '—'}</span> },
          {
            header: 'DAĞITIM', cell: p => {
              const d = dagitimDurum(p.assignedLines, p.totalLines)
              return <Badge variant={d.variant}>{d.label}</Badge>
            },
          },
          { header: 'TOPLANMA', cell: p => <ToplanmaIlerleme picked={p.pickedLines} total={p.totalLines} /> },
          { header: 'PLANLAMA', cell: p => tarihSaat(p.plannedAt) },
          { header: 'DURUM', cell: p => { const [l, v] = PLAN_DURUM[p.status] ?? [p.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{l}</Badge> } },
          {
            header: '', className: 'text-right', cell: p => (
              <span className="whitespace-nowrap">
                {p.status === 'pending' && (
                  <button className="text-xs underline" style={{ color: 'var(--brand)' }}
                    onClick={e => { e.stopPropagation(); if (window.confirm(`${p.planNumber} toplama başlatılsın mı?`)) aksiyon.mutate(`/fulfillment/picking-plans/${p.id}/start`) }}>
                    Başlat
                  </button>
                )}
                {p.status === 'picking' && (
                  <button className="text-xs underline text-green-600"
                    onClick={e => { e.stopPropagation(); if (window.confirm(`${p.planNumber} tamamlandı olarak işaretlensin mi?`)) aksiyon.mutate(`/fulfillment/picking-plans/${p.id}/complete`) }}>
                    Tamamla
                  </button>
                )}
              </span>
            ),
          },
        ]}
        rows={plans}
        loading={isLoading}
        empty="Toplama görevi yok. 'Yeni Görev' ile filtreli görev oluşturabilirsiniz."
        onRowClick={p => navigate(`/fulfillment/tasks/${p.id}`)}
      />

      <Pager page={page} totalPages={totalPages} onChange={setPage} />
    </div>
  )
}
