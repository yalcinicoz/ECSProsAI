import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { DataTable, Pager, errText, tarihSaat } from '@/components/ui/DataTable'
import { cn } from '@/lib/utils'

interface PickingPlan {
  id: string
  planNumber: string
  warehouseId: string
  planType: string
  status: string
  plannedAt: string
  startedAt?: string
  completedAt?: string
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

const DURUM: Record<string, [string, BadgeVariant]> = {
  pending:   ['Bekliyor', 'warning'],
  picking:   ['Toplanıyor', 'info'],
  completed: ['Tamamlandı', 'success'],
  cancelled: ['İptal', 'danger'],
}

const TIP: Record<string, string> = { single: 'Tekli', batch: 'Toplu', wave: 'Dalga' }

export function PickingPlansPage() {
  const queryClient = useQueryClient()
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
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Picking Planları</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{data?.totalCount ?? 0} kayıt</p>
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
          { header: 'TİP', cell: p => TIP[p.planType] ?? p.planType },
          { header: 'PLANLAMA', cell: p => tarihSaat(p.plannedAt) },
          { header: 'BAŞLAMA', cell: p => tarihSaat(p.startedAt) },
          { header: 'BİTİŞ', cell: p => tarihSaat(p.completedAt) },
          { header: 'DURUM', cell: p => { const [l, v] = DURUM[p.status] ?? [p.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{l}</Badge> } },
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
        empty="Picking planı yok. Planlar sipariş işleme akışından oluşturulur."
      />

      <Pager page={page} totalPages={totalPages} onChange={setPage} />
    </div>
  )
}
