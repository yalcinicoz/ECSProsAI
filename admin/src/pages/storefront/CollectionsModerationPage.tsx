import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Pagination } from '@/components/ui/Pagination'
import { PageSpinner } from '@/components/ui/Spinner'

// E6: üye koleksiyonu moderasyonu — Faz G "Koleksiyonlar bloğu" yalnız
// onaylı+herkese açık koleksiyonları gösterebilir (onay ekranı spec şartı).

interface ModerationCollection {
  id: string
  firmPlatformId: string
  memberId: string
  name: string
  description: string | null
  isPublic: boolean
  isShareable: boolean
  status: string
  isQuickSave: boolean
  itemCount: number
  createdAt: string
  moderatedAt: string | null
}

interface PagedResult {
  items: ModerationCollection[]
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

const TABS: { key: string; label: string }[] = [
  { key: 'pending',  label: 'Onay Bekleyen' },
  { key: 'approved', label: 'Onaylı' },
  { key: 'rejected', label: 'Reddedilen' },
]

export function CollectionsModerationPage() {
  const [status, setStatus] = useState('pending')
  const [page, setPage] = useState(1)
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['collections-moderation', status, page],
    queryFn: async () => {
      const res = await api.get('/collections', { params: { status, page } })
      return res.data.data as PagedResult
    },
  })

  const moderate = useMutation({
    mutationFn: async ({ id, approve }: { id: string; approve: boolean }) => {
      await api.post(`/collections/${id}/${approve ? 'approve' : 'reject'}`)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['collections-moderation'] }),
  })

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Koleksiyon Moderasyonu</h1>
          <p className="text-sm text-[var(--text-m)]">
            Onaylı + herkese açık koleksiyonlar vitrin "Koleksiyonlar bloğu"nda kullanılabilir.
          </p>
        </div>
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
                <th className="px-4 py-3">Koleksiyon</th>
                <th className="px-4 py-3">Görünürlük</th>
                <th className="px-4 py-3">Ürün</th>
                <th className="px-4 py-3">Oluşturulma</th>
                <th className="px-4 py-3">Durum</th>
                <th className="px-4 py-3 text-right">İşlem</th>
              </tr>
            </thead>
            <tbody>
              {(data?.items ?? []).map((k) => (
                <tr key={k.id} className="border-b border-[var(--border)] last:border-0">
                  <td className="px-4 py-3">
                    <div className="font-medium">
                      {k.name}
                      {k.isQuickSave && (
                        <span className="ml-2 text-xs text-[var(--text-s)]">(otomatik — Kaydedilenler)</span>
                      )}
                    </div>
                    {k.description && (
                      <div className="text-xs text-[var(--text-m)]">{k.description}</div>
                    )}
                  </td>
                  <td className="px-4 py-3 text-[var(--text-m)]">
                    {k.isPublic ? 'Herkese açık' : 'Gizli'}
                    {k.isShareable ? ' · Paylaşılabilir' : ''}
                  </td>
                  <td className="px-4 py-3">{k.itemCount}</td>
                  <td className="px-4 py-3 text-[var(--text-m)]">
                    {new Date(k.createdAt).toLocaleDateString('tr-TR')}
                  </td>
                  <td className="px-4 py-3">
                    <Badge variant={STATUS_LABELS[k.status]?.variant ?? 'neutral'}>
                      {STATUS_LABELS[k.status]?.label ?? k.status}
                    </Badge>
                  </td>
                  <td className="px-4 py-3 text-right">
                    {k.status !== 'approved' && (
                      <Button
                        size="sm"
                        onClick={() => moderate.mutate({ id: k.id, approve: true })}
                        disabled={moderate.isPending}
                      >
                        Onayla
                      </Button>
                    )}
                    {k.status !== 'rejected' && (
                      <Button
                        size="sm"
                        variant="danger"
                        className="ml-2"
                        onClick={() => moderate.mutate({ id: k.id, approve: false })}
                        disabled={moderate.isPending}
                      >
                        Reddet
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
              {(data?.items ?? []).length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-10 text-center text-[var(--text-m)]">
                    Bu durumda koleksiyon yok.
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
