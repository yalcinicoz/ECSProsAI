import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Pagination } from '@/components/ui/Pagination'
import { PageSpinner } from '@/components/ui/Spinner'

// E7: ürün yorumu moderasyonu — kart/detay puanları yalnız onaylı yorumlardan
// hesaplanır; reddedilirken neden yazılır (üye "Reddedilenler" sekmesinde görür).

interface ModerationReview {
  id: string
  memberName: string
  productCode: string
  rating: number
  text: string | null
  status: string
  rejectReason: string | null
  createdAt: string
}

interface PagedResult {
  items: ModerationReview[]
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

const TABS = [
  { key: 'pending',  label: 'Onay Bekleyen' },
  { key: 'approved', label: 'Onaylı' },
  { key: 'rejected', label: 'Reddedilen' },
]

export function ReviewsModerationPage() {
  const [status, setStatus] = useState('pending')
  const [page, setPage] = useState(1)
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['reviews-moderation', status, page],
    queryFn: async () => {
      const res = await api.get('/reviews', { params: { status, page } })
      return res.data.data as PagedResult
    },
  })

  const moderate = useMutation({
    mutationFn: async ({ id, approve, reason }: { id: string; approve: boolean; reason?: string }) => {
      await api.post(`/reviews/${id}/${approve ? 'approve' : 'reject'}`, approve ? undefined : { reason })
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['reviews-moderation'] }),
  })

  const reddet = (id: string) => {
    const reason = window.prompt('Red nedeni (üyeye gösterilir):', 'Yayın kriterlerine uygun değil.')
    if (reason === null) return
    moderate.mutate({ id, approve: false, reason })
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">Yorum Moderasyonu</h1>
        <p className="text-sm text-[var(--text-m)]">
          Ürün puanları yalnız onaylı yorumlardan hesaplanır; onay kuyruğu yayının kapısıdır.
        </p>
      </div>

      <div className="flex gap-2">
        {TABS.map((tab) => (
          <button
            key={tab.key}
            onClick={() => { setStatus(tab.key); setPage(1) }}
            className={`rounded-lg px-3 py-1.5 text-sm ${
              status === tab.key
                ? 'bg-[var(--brand)] text-white'
                : 'bg-[var(--surface2)] text-[var(--text-m)] hover:text-[var(--text)]'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {isLoading ? (
        <PageSpinner />
      ) : (
        <div className="overflow-hidden rounded-xl border border-[var(--border)] bg-[var(--surface)]">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--border)] text-left text-[var(--text-s)]">
                <th className="px-4 py-3">Ürün / Üye</th>
                <th className="px-4 py-3">Puan</th>
                <th className="px-4 py-3">Yorum</th>
                <th className="px-4 py-3">Tarih</th>
                <th className="px-4 py-3">Durum</th>
                <th className="px-4 py-3 text-right">İşlem</th>
              </tr>
            </thead>
            <tbody>
              {(data?.items ?? []).map((y) => (
                <tr key={y.id} className="border-b border-[var(--border)] last:border-0 align-top">
                  <td className="px-4 py-3">
                    <div className="font-medium">{y.productCode}</div>
                    <div className="text-xs text-[var(--text-m)]">{y.memberName}</div>
                  </td>
                  <td className="px-4 py-3">{'★'.repeat(y.rating)}{'☆'.repeat(5 - y.rating)}</td>
                  <td className="px-4 py-3 max-w-md">
                    <div className="text-[var(--text-m)]">{y.text || '—'}</div>
                    {y.status === 'rejected' && y.rejectReason && (
                      <div className="mt-1 text-xs text-red-600">Red nedeni: {y.rejectReason}</div>
                    )}
                  </td>
                  <td className="px-4 py-3 text-[var(--text-m)]">
                    {new Date(y.createdAt).toLocaleDateString('tr-TR')}
                  </td>
                  <td className="px-4 py-3">
                    <Badge variant={STATUS_LABELS[y.status]?.variant ?? 'neutral'}>
                      {STATUS_LABELS[y.status]?.label ?? y.status}
                    </Badge>
                  </td>
                  <td className="px-4 py-3 text-right whitespace-nowrap">
                    {y.status !== 'approved' && (
                      <Button size="sm" onClick={() => moderate.mutate({ id: y.id, approve: true })} disabled={moderate.isPending}>
                        Onayla
                      </Button>
                    )}
                    {y.status !== 'rejected' && (
                      <Button size="sm" variant="danger" className="ml-2" onClick={() => reddet(y.id)} disabled={moderate.isPending}>
                        Reddet
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
              {(data?.items ?? []).length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-10 text-center text-[var(--text-m)]">
                    Bu durumda yorum yok.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {data && data.totalPages > 1 && (
        <Pagination page={page} totalPages={data.totalPages} totalCount={data.totalCount} pageSize={data.pageSize} onChange={setPage} />
      )}
    </div>
  )
}
