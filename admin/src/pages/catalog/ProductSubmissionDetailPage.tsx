import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { ChevronLeft } from 'lucide-react'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'

const SUBMISSION_STATUS_MAP: Record<string, { label: string; variant: BadgeVariant }> = {
  pending:  { label: 'Bekliyor',   variant: 'warning' },
  approved: { label: 'Onaylandı',  variant: 'success' },
  rejected: { label: 'Reddedildi', variant: 'danger' },
}

type ApiError = { response?: { data?: { error?: string } } }

interface VariantPayload {
  axisValues?: Record<string, string>
  sku?: string
  barcode?: string
  stock?: number
  price?: { amount?: number; currency?: string }
}
interface SubmissionDetail {
  id: string
  supplierId: string
  supplierProductCode: string
  groupCode: string
  name: Record<string, string>
  status: string
  productCode: string | null
  reviewNote: string | null
  submittedAt: string
  reviewedAt: string | null
  payload: {
    name?: Record<string, string>
    shortDescription?: Record<string, string>
    description?: Record<string, string>
    attributes?: Record<string, string | string[]>
    variants?: VariantPayload[]
    images?: { url?: string; variantRef?: string; main?: boolean }[]
  }
}

function i18n(v?: Record<string, string>): string {
  if (!v) return '—'
  return Object.entries(v).map(([k, val]) => `${val} (${k})`).join(' · ') || '—'
}

export function ProductSubmissionDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [rejectOpen, setRejectOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [err, setErr] = useState<string | null>(null)

  const { data: s, isLoading } = useQuery<SubmissionDetail>({
    queryKey: ['product-submission', id],
    queryFn: async () => (await api.get(`/catalog/product-submissions/${id}`)).data.data,
  })

  const approve = useMutation({
    mutationFn: async () => (await api.post(`/catalog/product-submissions/${id}/approve`)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product-submission', id] })
      queryClient.invalidateQueries({ queryKey: ['product-submissions'] })
      setErr(null)
    },
    onError: (e: ApiError) => setErr(e.response?.data?.error ?? 'Onay başarısız.'),
  })

  const reject = useMutation({
    mutationFn: async () => (await api.post(`/catalog/product-submissions/${id}/reject`, { reason })).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product-submission', id] })
      queryClient.invalidateQueries({ queryKey: ['product-submissions'] })
      setRejectOpen(false); setReason(''); setErr(null)
    },
    onError: (e: ApiError) => setErr(e.response?.data?.error ?? 'Red başarısız.'),
  })

  if (isLoading || !s) return <PageSpinner />

  const st = SUBMISSION_STATUS_MAP[s.status] ?? { label: s.status, variant: 'neutral' as BadgeVariant }
  const p = s.payload ?? {}
  const attrs = Object.entries(p.attributes ?? {})
  const variants = p.variants ?? []

  return (
    <div className="p-6 max-w-4xl">
      <button onClick={() => navigate('/catalog/product-submissions')}
        className="flex items-center gap-1 text-sm mb-4" style={{ color: 'var(--text-s)' }}>
        <ChevronLeft size={16} /> Gönderimler
      </button>

      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>{s.supplierProductCode}</h1>
          <Badge variant={st.variant}>{st.label}</Badge>
        </div>
        {s.status === 'pending' && (
          <div className="flex gap-2">
            <Button variant="danger" onClick={() => setRejectOpen(true)}>Reddet</Button>
            <Button onClick={() => approve.mutate()} disabled={approve.isPending}>
              {approve.isPending ? 'Onaylanıyor...' : 'Onayla'}
            </Button>
          </div>
        )}
      </div>

      {err && <div className="mb-4 px-4 py-2 rounded text-sm" style={{ background: 'var(--surface2)', color: 'var(--danger, #dc2626)' }}>{err}</div>}

      {s.status === 'approved' && s.productCode && (
        <div className="mb-4 px-4 py-2 rounded text-sm" style={{ background: 'var(--surface2)', color: 'var(--text)' }}>
          Canlı ürün oluşturuldu:{' '}
          <Link to={`/catalog/products/${s.productCode}`} className="font-medium underline">{s.productCode}</Link>
          {' '}— eksik alanlar (fiyat/kategori/görsel) ürün kartından tamamlanır.
        </div>
      )}
      {s.status === 'rejected' && s.reviewNote && (
        <div className="mb-4 px-4 py-2 rounded text-sm" style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>
          <strong>Red gerekçesi:</strong> {s.reviewNote}
        </div>
      )}

      <div className="card p-5 mb-4">
        <h2 className="text-sm font-semibold mb-3" style={{ color: 'var(--text-s)' }}>GENEL</h2>
        <dl className="grid grid-cols-[140px_1fr] gap-y-2 text-sm">
          <dt style={{ color: 'var(--text-s)' }}>Ad</dt><dd style={{ color: 'var(--text)' }}>{i18n(p.name ?? s.name)}</dd>
          <dt style={{ color: 'var(--text-s)' }}>Grup</dt><dd style={{ color: 'var(--text)' }}>{s.groupCode}</dd>
          {p.shortDescription && (<><dt style={{ color: 'var(--text-s)' }}>Kısa açıklama</dt><dd style={{ color: 'var(--text-m)' }}>{i18n(p.shortDescription)}</dd></>)}
          {p.description && (<><dt style={{ color: 'var(--text-s)' }}>Açıklama</dt><dd style={{ color: 'var(--text-m)' }}>{i18n(p.description)}</dd></>)}
        </dl>
      </div>

      {attrs.length > 0 && (
        <div className="card p-5 mb-4">
          <h2 className="text-sm font-semibold mb-3" style={{ color: 'var(--text-s)' }}>ÖZELLİKLER</h2>
          <dl className="grid grid-cols-[140px_1fr] gap-y-2 text-sm">
            {attrs.map(([k, v]) => (
              <div key={k} className="contents">
                <dt style={{ color: 'var(--text-s)' }}>{k}</dt>
                <dd style={{ color: 'var(--text)' }}>{Array.isArray(v) ? v.join(', ') : v}</dd>
              </div>
            ))}
          </dl>
        </div>
      )}

      <div className="card p-5 mb-4">
        <h2 className="text-sm font-semibold mb-3" style={{ color: 'var(--text-s)' }}>VARYANTLAR ({variants.length})</h2>
        <table className="w-full text-sm">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)' }}>
              {['Eksen', 'SKU', 'Barkod', 'Stok', 'Fiyat'].map(h => (
                <th key={h} className="px-2 py-2 text-left text-xs font-semibold" style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {variants.map((v, idx) => (
              <tr key={idx} style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-2 py-2" style={{ color: 'var(--text)' }}>
                  {Object.entries(v.axisValues ?? {}).map(([k, val]) => `${k}: ${val}`).join(' / ') || '—'}
                </td>
                <td className="px-2 py-2" style={{ color: 'var(--text-m)' }}>{v.sku ?? '—'}</td>
                <td className="px-2 py-2" style={{ color: 'var(--text-m)' }}>{v.barcode ?? '—'}</td>
                <td className="px-2 py-2" style={{ color: 'var(--text-m)' }}>{v.stock ?? '—'}</td>
                <td className="px-2 py-2" style={{ color: 'var(--text-m)' }}>{v.price?.amount != null ? v.price.amount : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {(p.images?.length ?? 0) > 0 && (
        <div className="card p-5 mb-4">
          <h2 className="text-sm font-semibold mb-3" style={{ color: 'var(--text-s)' }}>GÖRSELLER</h2>
          <div className="flex gap-2 flex-wrap">
            {p.images!.map((img, idx) => (
              <img key={idx} src={img.url} alt="" className="w-20 h-20 object-cover rounded"
                style={{ border: '1px solid var(--border)' }}
                onError={(e) => { (e.target as HTMLImageElement).src = '/images/no-image.svg' }} />
            ))}
          </div>
        </div>
      )}

      <Modal open={rejectOpen} onClose={() => setRejectOpen(false)} title="Gönderimi Reddet">
        <div className="space-y-3">
          <label className="block text-sm" style={{ color: 'var(--text-s)' }}>Red gerekçesi (zorunlu)</label>
          <textarea value={reason} onChange={e => setReason(e.target.value)} rows={3}
            className="w-full px-3 py-2 rounded text-sm"
            style={{ background: 'var(--surface)', border: '1px solid var(--border)', color: 'var(--text)' }}
            placeholder="Örn. Görseller yetersiz, ürün adı kurallara uymuyor..." />
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setRejectOpen(false)}>Vazgeç</Button>
            <Button variant="danger" disabled={!reason.trim() || reject.isPending}
              onClick={() => reject.mutate()}>{reject.isPending ? 'Reddediliyor...' : 'Reddet'}</Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}
