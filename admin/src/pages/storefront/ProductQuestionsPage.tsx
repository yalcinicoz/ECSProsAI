// Satıcıya Soru Sor — moderasyon (2026-09-01, kullanıcı isteği): personel üyelerin
// ürün sorularını görür, cevaplar (cevap = ürün detayında yayına girer), yayından
// kaldırır/geri alır. Yorum Moderasyonu sayfa kalıbının kardeşi.
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Pager, tarihSaat } from '@/components/ui/DataTable'

interface QuestionRow {
  id: string; productCode: string; question: string; answer: string | null
  status: string; memberName: string; createdAt: string; answeredAt: string | null
}
interface PagedResult { items: QuestionRow[]; totalCount: number; page: number; pageSize: number }

const TABS = [
  { key: 'pending',  label: 'Cevap Bekleyen' },
  { key: 'answered', label: 'Cevaplanan (yayında)' },
  { key: 'hidden',   label: 'Yayından Kaldırılan' },
]

export function ProductQuestionsPage() {
  const [status, setStatus] = useState('pending')
  const [page, setPage] = useState(1)
  const [cevaplar, setCevaplar] = useState<Record<string, string>>({})
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['product-questions', status, page],
    queryFn: async () => {
      const res = await api.get('/product-questions', { params: { status, page } })
      return res.data.data as PagedResult
    },
  })

  const cevapla = useMutation({
    mutationFn: async ({ id, answer }: { id: string; answer: string }) =>
      api.post(`/product-questions/${id}/answer`, { answer }),
    onSuccess: (_d, v) => {
      setCevaplar((m) => ({ ...m, [v.id]: '' }))
      queryClient.invalidateQueries({ queryKey: ['product-questions'] })
    },
  })
  const gorunurluk = useMutation({
    mutationFn: async ({ id, hidden }: { id: string; hidden: boolean }) =>
      api.post(`/product-questions/${id}/visibility`, { hidden }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['product-questions'] }),
  })

  const rows = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / (data?.pageSize ?? 30))

  return (
    <div className="p-6 space-y-4">
      <div>
        <h1 className="text-xl font-semibold" style={{ color: 'var(--text)' }}>Ürün Soruları</h1>
        <p className="text-sm" style={{ color: 'var(--text-m)' }}>
          Üyelerin "Satıcıya Soru Sor" ile ilettiği sorular. Cevaplanan soru ürün detayında
          maskeli üye adıyla yayınlanır; yayından kaldırılan soruyu yalnız soran üye görür.
        </p>
      </div>

      <div className="flex gap-2">
        {TABS.map((tab) => (
          <button key={tab.key} onClick={() => { setStatus(tab.key); setPage(1) }}
            className="rounded-lg px-3 py-1.5 text-sm"
            style={status === tab.key
              ? { background: 'var(--brand)', color: '#fff' }
              : { background: 'var(--surface2)', color: 'var(--text-m)' }}>
            {tab.label}
          </button>
        ))}
      </div>

      {isLoading ? (
        <p className="text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor…</p>
      ) : rows.length === 0 ? (
        <div className="card py-12 text-center text-sm" style={{ color: 'var(--text-m)' }}>
          Bu durumda soru yok.
        </div>
      ) : (
        <div className="space-y-3">
          {rows.map((q) => (
            <article key={q.id} className="card space-y-2">
              <div className="flex flex-wrap items-center gap-2 text-xs" style={{ color: 'var(--text-s)' }}>
                <a className="font-mono underline" href={`/urun/${q.productCode}`} target="_blank" rel="noreferrer"
                   style={{ color: 'var(--brand)' }}>{q.productCode}</a>
                <span>{q.memberName}</span>
                <span>{tarihSaat(q.createdAt)}</span>
                {q.status === 'answered' && <Badge variant="success">yayında</Badge>}
                {q.status === 'hidden' && <Badge variant="neutral">yayından kaldırıldı</Badge>}
              </div>
              <p className="text-sm" style={{ color: 'var(--text)' }}>{q.question}</p>

              {q.answer && (
                <p className="text-sm rounded-lg px-3 py-2"
                   style={{ background: 'var(--surface2)', color: 'var(--text-m)' }}>
                  <strong>Cevap{q.answeredAt ? ` (${tarihSaat(q.answeredAt)})` : ''}:</strong> {q.answer}
                </p>
              )}

              <div className="flex flex-wrap items-end gap-2">
                <textarea className="inp flex-1 min-w-[260px]" rows={2} maxLength={2000}
                  placeholder={q.answer ? 'Cevabı güncelle…' : 'Cevabınızı yazın — kaydedince yayına girer…'}
                  value={cevaplar[q.id] ?? ''}
                  onChange={(e) => setCevaplar((m) => ({ ...m, [q.id]: e.target.value }))} />
                <Button size="sm" disabled={!(cevaplar[q.id] ?? '').trim() || cevapla.isPending}
                  onClick={() => cevapla.mutate({ id: q.id, answer: cevaplar[q.id] })}>
                  {q.answer ? 'Cevabı Güncelle' : 'Cevapla ve Yayınla'}
                </Button>
                {q.status === 'answered' && (
                  <Button size="sm" variant="secondary" onClick={() => gorunurluk.mutate({ id: q.id, hidden: true })}>
                    Yayından Kaldır
                  </Button>
                )}
                {q.status === 'hidden' && (
                  <Button size="sm" variant="secondary" onClick={() => gorunurluk.mutate({ id: q.id, hidden: false })}>
                    Yayına Al
                  </Button>
                )}
              </div>
            </article>
          ))}
        </div>
      )}
      {totalPages > 1 && <Pager page={page} totalPages={totalPages} onChange={setPage} />}
    </div>
  )
}
