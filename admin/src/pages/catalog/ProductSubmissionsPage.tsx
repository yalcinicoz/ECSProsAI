import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { cn } from '@/lib/utils'

export const SUBMISSION_STATUS_MAP: Record<string, { label: string; variant: BadgeVariant }> = {
  pending:  { label: 'Bekliyor',    variant: 'warning' },
  approved: { label: 'Onaylandı',   variant: 'success' },
  rejected: { label: 'Reddedildi',  variant: 'danger' },
}

interface SubmissionListItem {
  id: string
  supplierId: string
  supplierProductCode: string
  groupCode: string
  name: Record<string, string>
  variantCount: number
  status: string
  productCode: string | null
  reviewNote: string | null
  submittedAt: string
  reviewedAt: string | null
}

const TABS = [
  { key: 'pending', label: 'Bekleyen' },
  { key: 'approved', label: 'Onaylı' },
  { key: 'rejected', label: 'Reddedilen' },
  { key: '', label: 'Tümü' },
]

function nm(i: SubmissionListItem): string {
  return i.name?.['tr'] ?? i.name?.[Object.keys(i.name ?? {})[0]] ?? '—'
}

export function ProductSubmissionsPage() {
  const navigate = useNavigate()
  const [tab, setTab] = useState('pending')
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery({
    queryKey: ['product-submissions', tab, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (tab) params.set('status', tab)
      return (await api.get(`/catalog/product-submissions?${params}`)).data.data
    },
  })

  const items: SubmissionListItem[] = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const totalPages = Math.ceil(totalCount / 20)

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Tedarikçi Gönderimleri</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
          {totalCount} kayıt · Partner API'den gelen ürün kartı gönderimleri
        </p>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {TABS.map(t => (
          <button key={t.key} className={cn('stab', tab === t.key && 'active')}
            onClick={() => { setTab(t.key); setPage(1) }}>{t.label}</button>
        ))}
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['ÜRÜN KODU', 'AD', 'GRUP', 'VARYANT', 'DURUM', 'GÖNDERİM', 'ÜRÜN'].map(h => (
                <th key={h} className="px-4 py-3 text-xs font-semibold text-left" style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && items.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Bu durumda gönderim yok.
              </td></tr>
            )}
            {items.map(i => {
              const st = SUBMISSION_STATUS_MAP[i.status] ?? { label: i.status, variant: 'neutral' as BadgeVariant }
              return (
                <tr key={i.id} onClick={() => navigate(`/catalog/product-submissions/${i.id}`)}
                  className="cursor-pointer hover:bg-[var(--surface2)]" style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-3 text-sm font-medium" style={{ color: 'var(--text)' }}>{i.supplierProductCode}</td>
                  <td className="px-4 py-3 text-sm" style={{ color: 'var(--text)' }}>{nm(i)}</td>
                  <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{i.groupCode}</td>
                  <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>{i.variantCount}</td>
                  <td className="px-4 py-3"><Badge variant={st.variant}>{st.label}</Badge></td>
                  <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-s)' }}>{new Date(i.submittedAt).toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-s)' }}>{i.productCode ?? '—'}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2 mt-4">
          <button className="stab" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Önceki</button>
          <span className="text-sm" style={{ color: 'var(--text-s)' }}>{page} / {totalPages}</span>
          <button className="stab" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Sonraki</button>
        </div>
      )}
    </div>
  )
}
