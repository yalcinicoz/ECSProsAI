import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { DataTable, Pager } from '@/components/ui/DataTable'
import { errText, tarih, tarihSaat, para } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'

interface Quote {
  id: string
  quoteNumber: string
  memberId: string
  status: string
  currencyCode: string
  grandTotal: number
  validUntil: string
  sentAt?: string
  convertedOrderId?: string
  createdAt: string
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

const DURUM: Record<string, [string, BadgeVariant]> = {
  draft:     ['Taslak', 'neutral'],
  sent:      ['Gönderildi', 'info'],
  accepted:  ['Kabul Edildi', 'success'],
  rejected:  ['Reddedildi', 'danger'],
  converted: ['Siparişe Dönüştü', 'default'],
  expired:   ['Süresi Doldu', 'warning'],
}

const SEKMELER = [['', 'Tümü'], ['draft', 'Taslak'], ['sent', 'Gönderildi'], ['accepted', 'Kabul'], ['converted', 'Dönüştü']] as const

export function QuotesPage() {
  const queryClient = useQueryClient()
  const [tab, setTab] = useState('')
  const [page, setPage] = useState(1)
  const [error, setError] = useState('')

  const { data, isLoading } = useQuery<PagedResult<Quote>>({
    queryKey: ['quotes', tab, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (tab) params.set('status', tab)
      return (await api.get(`/orders/quotes?${params}`)).data.data
    },
  })

  const aksiyon = useMutation({
    mutationFn: async ({ url, body }: { url: string; body?: unknown }) => {
      setError('')
      await api.post(url, body ?? {})
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['quotes'] }),
    onError: (e: unknown) => setError(errText(e)),
  })

  const quotes = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 20)

  return (
    <div className="p-6">
      <div className="mb-4">
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Teklifler</h1>
        <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{data?.totalCount ?? 0} kayıt</p>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        {SEKMELER.map(([v, l]) => (
          <button key={v} className={cn('stab', tab === v && 'active')}
            onClick={() => { setTab(v); setPage(1) }}>{l}</button>
        ))}
      </div>

      {error && <p className="text-sm text-red-500 mb-3">{error}</p>}

      <DataTable<Quote>
        columns={[
          { header: 'TEKLİF NO', cell: q => <code className="text-xs font-mono">{q.quoteNumber}</code> },
          { header: 'TUTAR', cell: q => <span className="font-medium">{para(q.grandTotal, q.currencyCode === 'TRY' ? '₺' : q.currencyCode)}</span> },
          { header: 'GEÇERLİLİK', cell: q => tarih(q.validUntil) },
          { header: 'GÖNDERİM', cell: q => tarihSaat(q.sentAt) },
          { header: 'OLUŞTURMA', cell: q => tarih(q.createdAt) },
          { header: 'DURUM', cell: q => { const [l, v] = DURUM[q.status] ?? [q.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{l}</Badge> } },
          {
            header: '', className: 'text-right', cell: q => (
              <span className="whitespace-nowrap">
                {q.status === 'draft' && (
                  <button className="text-xs underline" style={{ color: 'var(--brand)' }}
                    onClick={e => { e.stopPropagation(); if (window.confirm(`${q.quoteNumber} müşteriye gönderilsin mi?`)) aksiyon.mutate({ url: `/orders/quotes/${q.id}/send` }) }}>
                    Gönder
                  </button>
                )}
                {q.status === 'sent' && (
                  <>
                    <button className="text-xs underline mr-2 text-green-600"
                      onClick={e => { e.stopPropagation(); if (window.confirm(`${q.quoteNumber} kabul edildi olarak işaretlensin mi?`)) aksiyon.mutate({ url: `/orders/quotes/${q.id}/respond`, body: { accepted: true } }) }}>
                      Kabul
                    </button>
                    <button className="text-xs underline text-red-600"
                      onClick={e => { e.stopPropagation(); if (window.confirm(`${q.quoteNumber} reddedildi olarak işaretlensin mi?`)) aksiyon.mutate({ url: `/orders/quotes/${q.id}/respond`, body: { accepted: false } }) }}>
                      Red
                    </button>
                  </>
                )}
                {q.status === 'accepted' && (
                  <span className="text-xs" style={{ color: 'var(--text-s)' }} title="Siparişe dönüştürme teslimat bilgisi gerektirir; sipariş oluşturma akışından yapılır.">
                    Dönüştürülmeye hazır
                  </span>
                )}
              </span>
            ),
          },
        ]}
        rows={quotes}
        loading={isLoading}
        empty="Teklif yok."
      />

      <Pager page={page} totalPages={totalPages} onChange={setPage} />
    </div>
  )
}
