// Satıcıya Soru Sor — moderasyon (2026-09-01, kullanıcı isteği): personel üyelerin
// ürün sorularını görür, cevaplar (cevap = ürün detayında yayına girer), yayından
// kaldırır/geri alır. Yorum Moderasyonu sayfa kalıbının kardeşi.
// 2026-09-02: hazır cevap şablonları (tarayıcıda kişisel saklanır) + canlı bekleyen
// sayacı — QuestionAlerts katmanı hub olayında listeyi kendiliğinden tazeler.
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Pager, tarihSaat } from '@/components/ui/DataTable'
import { useQuestionAlertStore } from '@/store/questionAlerts'

interface QuestionRow {
  id: string; productCode: string; question: string; answer: string | null
  status: string; memberName: string; createdAt: string; answeredAt: string | null
}
interface PagedResult { items: QuestionRow[]; totalCount: number; page: number; pageSize: number }
interface CevapSablonu { id: string; title: string; text: string }

const TABS = [
  { key: 'pending',  label: 'Cevap Bekleyen' },
  { key: 'answered', label: 'Cevaplanan (yayında)' },
  { key: 'hidden',   label: 'Yayından Kaldırılan' },
]

const SABLON_ANAHTARI = 'ecspros_soru_cevap_sablonlari'

function sablonlariOku(): CevapSablonu[] {
  try {
    const ham = localStorage.getItem(SABLON_ANAHTARI)
    return ham ? (JSON.parse(ham) as CevapSablonu[]) : []
  } catch { return [] }
}

export function ProductQuestionsPage() {
  const [status, setStatus] = useState('pending')
  const [page, setPage] = useState(1)
  const [cevaplar, setCevaplar] = useState<Record<string, string>>({})
  const [sablonlar, setSablonlar] = useState<CevapSablonu[]>(sablonlariOku)
  const [sablonPaneliAcik, setSablonPaneliAcik] = useState(false)
  const [yeniSablonBaslik, setYeniSablonBaslik] = useState('')
  const [yeniSablonMetin, setYeniSablonMetin] = useState('')
  const bekleyenSayisi = useQuestionAlertStore((s) => s.pendingCount)
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
      queryClient.invalidateQueries({ queryKey: ['product-questions-pending-count'] })
    },
  })
  const gorunurluk = useMutation({
    mutationFn: async ({ id, hidden }: { id: string; hidden: boolean }) =>
      api.post(`/product-questions/${id}/visibility`, { hidden }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['product-questions'] }),
  })

  const sablonlariKaydet = (liste: CevapSablonu[]) => {
    setSablonlar(liste)
    try { localStorage.setItem(SABLON_ANAHTARI, JSON.stringify(liste)) } catch { /* dolu/kapalı depolama — şablonsuz devam */ }
  }
  const sablonEkle = () => {
    const baslik = yeniSablonBaslik.trim()
    const metin = yeniSablonMetin.trim()
    if (!baslik || !metin) return
    sablonlariKaydet([...sablonlar, { id: `${Date.now()}`, title: baslik, text: metin }])
    setYeniSablonBaslik(''); setYeniSablonMetin('')
  }

  const rows = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / (data?.pageSize ?? 30))

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-start justify-between gap-3 flex-wrap">
        <div>
          <h1 className="text-xl font-semibold" style={{ color: 'var(--text)' }}>Ürün Soruları</h1>
          <p className="text-sm" style={{ color: 'var(--text-m)' }}>
            Üyelerin "Satıcıya Soru Sor" ile ilettiği sorular. Cevaplanan soru ürün detayında
            maskeli üye adıyla yayınlanır; ilk cevapta üyeye e-posta gider. Yayından kaldırılan
            soruyu yalnız soran üye görür.
          </p>
        </div>
        <Button size="sm" variant="secondary" onClick={() => setSablonPaneliAcik((a) => !a)}>
          Hazır Cevaplar {sablonlar.length > 0 ? `(${sablonlar.length})` : ''}
        </Button>
      </div>

      {sablonPaneliAcik && (
        <div className="card p-4 space-y-3">
          <p className="text-sm font-medium" style={{ color: 'var(--text)' }}>
            Hazır cevap şablonları
            <span className="font-normal text-xs ml-2" style={{ color: 'var(--text-s)' }}>
              Bu tarayıcıda size özel saklanır; soru kartındaki "Hazır cevap…" seçimiyle metin alanına dolar.
            </span>
          </p>
          {sablonlar.length > 0 && (
            <ul className="space-y-1.5">
              {sablonlar.map((s) => (
                <li key={s.id} className="flex items-start gap-2 text-sm rounded-lg px-3 py-2"
                    style={{ background: 'var(--surface2)' }}>
                  <div className="flex-1 min-w-0">
                    <strong style={{ color: 'var(--text)' }}>{s.title}</strong>
                    <p className="truncate" style={{ color: 'var(--text-m)' }}>{s.text}</p>
                  </div>
                  <Button size="sm" variant="secondary"
                    onClick={() => sablonlariKaydet(sablonlar.filter((x) => x.id !== s.id))}>
                    Sil
                  </Button>
                </li>
              ))}
            </ul>
          )}
          <div className="flex flex-wrap items-end gap-2">
            <input className="inp w-[200px]" placeholder="Şablon adı (örn. Kargo süresi)"
              value={yeniSablonBaslik} onChange={(e) => setYeniSablonBaslik(e.target.value)} />
            <textarea className="inp flex-1 min-w-[260px]" rows={2} maxLength={2000}
              placeholder="Şablon metni…" value={yeniSablonMetin}
              onChange={(e) => setYeniSablonMetin(e.target.value)} />
            <Button size="sm" disabled={!yeniSablonBaslik.trim() || !yeniSablonMetin.trim()} onClick={sablonEkle}>
              Şablon Ekle
            </Button>
          </div>
        </div>
      )}

      <div className="flex gap-2">
        {TABS.map((tab) => (
          <button key={tab.key} onClick={() => { setStatus(tab.key); setPage(1) }}
            className="rounded-lg px-3 py-1.5 text-sm"
            style={status === tab.key
              ? { background: 'var(--brand)', color: '#fff' }
              : { background: 'var(--surface2)', color: 'var(--text-m)' }}>
            {tab.label}{tab.key === 'pending' && bekleyenSayisi > 0 ? ` (${bekleyenSayisi})` : ''}
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
                {sablonlar.length > 0 && (
                  <select className="inp w-[160px]" value=""
                    onChange={(e) => {
                      const s = sablonlar.find((x) => x.id === e.target.value)
                      if (s) setCevaplar((m) => ({ ...m, [q.id]: ((m[q.id] ?? '').trim() ? `${m[q.id]} ${s.text}` : s.text) }))
                    }}>
                    <option value="">Hazır cevap…</option>
                    {sablonlar.map((s) => <option key={s.id} value={s.id}>{s.title}</option>)}
                  </select>
                )}
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
