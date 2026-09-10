import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Modal } from '@/components/ui/Modal'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'

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
  topic: string | null
  photos: string[] | null
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
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (ReviewModerationGrid.Schema) + Excel + görünümler.
  // Y3: kanal kapsamı sunucuda kullanıcının yetkisinden çözülür, istemci taşımaz.
  const [sp] = useSearchParams()
  const status = sp.get('status') ?? 'pending'
  const grid = useGridState('reviews', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })
  const queryClient = useQueryClient()
  const [addOpen, setAddOpen] = useState(false)   // FAZ 15.4k: elle yorum girişi

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult>({
    queryKey: ['reviews-moderation', status, ...grid.queryKey],
    queryFn: async () => (await api.get(`/reviews?${grid.toParams({ status })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
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

  const columns: GridColumn<ModerationReview>[] = [
    { key: 'productCode', header: 'ÜRÜN / ÜYE', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 200,
      filter: { type: 'text', label: 'Ürün kodu', ops: ['startswith', 'contains', 'eq'] },
      filters: [{ field: 'memberName', label: 'Üye adı', type: 'text' }],
      cell: y => <div>
        <div className="font-medium">{y.productCode}</div>
        <div className="text-xs text-[var(--text-m)]">{y.memberName}</div>
      </div> },
    { key: 'rating', header: 'PUAN', priority: 1, sortable: true, filter: { type: 'number', label: 'Puan' },
      cell: y => <span className="whitespace-nowrap">{'★'.repeat(y.rating)}{'☆'.repeat(5 - y.rating)}</span> },
    { key: 'text', header: 'YORUM', priority: 2, sortable: true, minWidth: 320,
      filter: { type: 'text', label: 'Yorum metni' },
      filters: [{ field: 'hasText', label: 'Metni olan', type: 'boolean' }, { field: 'rejectReason', label: 'Ret sebebi', type: 'text' }],
      cell: y => <div className="max-w-md">
        <div className="text-[var(--text-m)]">{y.text || '—'}</div>
        {y.photos && y.photos.length > 0 && (
          <div className="mt-1.5 flex gap-1.5">
            {y.photos.map(foto => (
              <a key={foto} href={foto} target="_blank" rel="noreferrer" title="Fotoğrafı yeni sekmede aç">
                <img src={foto} alt="Yorum fotoğrafı" className="h-12 w-12 rounded object-cover border border-[var(--border)]" loading="lazy" />
              </a>
            ))}
          </div>
        )}
        {y.status === 'rejected' && y.rejectReason && (
          <div className="mt-1 text-xs text-red-600">Red nedeni: {y.rejectReason}</div>
        )}
      </div> },
    { key: 'topic', header: 'KONU', priority: 3, sortable: true, filter: { type: 'text', label: 'Konu' },
      cell: y => y.topic
        ? <span className="inline-block rounded bg-[var(--surface2)] px-1.5 py-0.5 text-xs text-[var(--text-m)]">{y.topic}</span>
        : <span className="text-[var(--text-s)]">—</span> },
    { key: 'createdAt', header: 'TARİH', priority: 2, sortable: true, filter: { type: 'date', label: 'Yazılma', quick: true },
      filters: [{ field: 'moderatedAt', label: 'Moderasyon tarihi', type: 'date' }, { field: 'moderated', label: 'Moderasyondan geçti', type: 'boolean' }],
      cell: y => <span className="text-[var(--text-m)]">{new Date(y.createdAt).toLocaleDateString('tr-TR')}</span> },
    { key: 'status', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Durum', options: Object.entries(STATUS_LABELS).map(([value, v]) => ({ value, label: v.label })) },
      cell: y => <Badge variant={STATUS_LABELS[y.status]?.variant ?? 'neutral'}>{STATUS_LABELS[y.status]?.label ?? y.status}</Badge> },
    { key: 'actions', header: 'İŞLEM', priority: 1, align: 'right', exportable: false, stopRowClick: true, minWidth: 170,
      cell: y => <span className="whitespace-nowrap">
        {y.status !== 'approved' && (
          <Button size="sm" onClick={() => moderate.mutate({ id: y.id, approve: true })} disabled={moderate.isPending}>Onayla</Button>
        )}
        {y.status !== 'rejected' && (
          <Button size="sm" variant="danger" className="ml-2" onClick={() => reddet(y.id)} disabled={moderate.isPending}>Reddet</Button>
        )}
      </span> },
  ]

  return (
    <div className="p-6 space-y-4">
      <div>
        <h1 className="text-xl font-semibold">Yorum Moderasyonu</h1>
        <p className="text-sm text-[var(--text-m)]">
          Ürün puanları yalnız onaylı yorumlardan hesaplanır; onay kuyruğu yayının kapısıdır.
        </p>
      </div>
      <div className="flex justify-end -mt-2 mb-2">
        <Button size="sm" onClick={() => setAddOpen(true)}>+ Yorum Ekle</Button>
      </div>

      <div className="flex gap-2">
        {TABS.map(tab => (
          <button key={tab.key}
            onClick={() => grid.mutate(n => n.set('status', tab.key))}
            className={`rounded-lg px-3 py-1.5 text-sm ${
              status === tab.key
                ? 'bg-[var(--brand)] text-white'
                : 'bg-[var(--surface2)] text-[var(--text-m)] hover:text-[var(--text)]'
            }`}>
            {tab.label}
          </button>
        ))}
      </div>

      <DataGrid<ModerationReview>
        gridId="reviews"
        views
        grid={grid}
        columns={columns}
        rows={data?.items ?? []}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        empty="Bu durumda yorum yok."
        search={{ placeholder: 'Ürün kodu, üye veya yorum metninde ara…' }}
        minWidth={1080}
        export={{ endpoint: '/reviews/export', named: () => ({ status }), fallbackFileName: 'yorumlar.xlsx' }}
        compact={{
          title: y => y.productCode,
          subtitle: y => `${y.memberName} · ${'★'.repeat(y.rating)}`,
          right: y => new Date(y.createdAt).toLocaleDateString('tr-TR'),
          badge: y => <Badge variant={STATUS_LABELS[y.status]?.variant ?? 'neutral'}>{STATUS_LABELS[y.status]?.label ?? y.status}</Badge>,
        }}
      />
      {addOpen && <ManualReviewModal onClose={() => setAddOpen(false)} onSaved={() => { setAddOpen(false); queryClient.invalidateQueries({ queryKey: ['reviews-moderation'] }) }} />}
    </div>
  )
}

// ── FAZ 15.4k (2026-09-10, eski "Ürün Yorum Ekle"): personel yorum girişi ────────────────────────
interface FirmLite { id: string; code: string; nameI18n: Record<string, string> }
interface PlatformLite { id: string; code: string; name?: string | null }
interface MemberLite { id: string; firstName: string; lastName: string; email?: string | null; phone?: string | null }

function ManualReviewModal({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [firmId, setFirmId] = useState('')
  const [platformId, setPlatformId] = useState('')
  const [productCode, setProductCode] = useState('')
  const [memberSearch, setMemberSearch] = useState('')
  const [member, setMember] = useState<MemberLite | null>(null)
  const [memberName, setMemberName] = useState('')
  const [rating, setRating] = useState(5)
  const [text, setText] = useState('')
  const [topic, setTopic] = useState('')
  const [approve, setApprove] = useState(true)
  const [error, setError] = useState('')

  const { data: firms = [] } = useQuery<FirmLite[]>({ queryKey: ['firms-lite'], queryFn: async () => (await api.get('/core/firms')).data.data })
  const { data: platforms = [] } = useQuery<PlatformLite[]>({ queryKey: ['firm-platforms', firmId], queryFn: async () => (await api.get(`/core/firms/${firmId}/platforms`)).data.data ?? [], enabled: !!firmId })
  const { data: members = [] } = useQuery<MemberLite[]>({
    queryKey: ['members-lite', memberSearch],
    queryFn: async () => (await api.get(`/crm/members?search=${encodeURIComponent(memberSearch)}&pageSize=8`)).data.data?.items ?? [],
    enabled: memberSearch.trim().length >= 3,
  })

  const save = useMutation({
    mutationFn: async () => {
      await api.post('/reviews', { firmPlatformId: platformId, productCode: productCode.trim(), memberId: member?.id ?? null,
        memberName: memberName.trim(), rating, text: text.trim() || null, topic: topic.trim() || null, approve })
    },
    onSuccess: onSaved,
    onError: (e) => setError(errText(e)),
  })

  return (
    <Modal open onClose={onClose} title="Yorum Ekle (personel girişi)">
      <p className="text-xs mb-3" style={{ color: 'var(--text-s)' }}>
        Mağazadan/telefondan gelen müşteri geri bildirimi ya da ithal yorum. Üye seçilirse ona bağlanır; seçilmezse yalnız görünen ad yayınlanır.
      </p>
      <div className="grid grid-cols-2 gap-3">
        <div><label className="flbl">Firma <span className="text-red-500">*</span></label>
          <select className="inp" value={firmId} onChange={e => { setFirmId(e.target.value); setPlatformId('') }}>
            <option value="">Seçin</option>{firms.map(f => <option key={f.id} value={f.id}>{f.nameI18n?.tr ?? f.code}</option>)}
          </select></div>
        <div><label className="flbl">Kanal <span className="text-red-500">*</span></label>
          <select className="inp" value={platformId} onChange={e => setPlatformId(e.target.value)} disabled={!firmId}>
            <option value="">Seçin</option>{platforms.map(p => <option key={p.id} value={p.id}>{p.name ?? p.code}</option>)}
          </select></div>
        <div><label className="flbl">Ürün kodu <span className="text-red-500">*</span></label><input className="inp font-mono" value={productCode} onChange={e => setProductCode(e.target.value)} placeholder="P-00012345" /></div>
        <div><label className="flbl">Puan <span className="text-red-500">*</span></label>
          <select className="inp" value={rating} onChange={e => setRating(parseInt(e.target.value))}>{[5, 4, 3, 2, 1].map(n => <option key={n} value={n}>{'★'.repeat(n)} ({n})</option>)}</select></div>
        <div className="col-span-2">
          <label className="flbl">Üye (isteğe bağlı — ad, e-posta ya da telefonla ara)</label>
          {member ? (
            <div className="flex items-center gap-2 text-sm"><span style={{ color: 'var(--text)' }}>{member.firstName} {member.lastName}</span>
              <button type="button" className="text-xs underline" onClick={() => setMember(null)}>değiştir</button></div>
          ) : (
            <>
              <input className="inp" value={memberSearch} onChange={e => setMemberSearch(e.target.value)} placeholder="en az 3 karakter" />
              {members.length > 0 && (
                <div className="mt-1 rounded-lg overflow-hidden" style={{ border: '1px solid var(--border)' }}>
                  {members.map(m => (
                    <button key={m.id} type="button" className="block w-full text-left px-3 py-1.5 text-sm hover:bg-[var(--surface2)]"
                      onClick={() => { setMember(m); setMemberName(`${m.firstName} ${(m.lastName ?? '').slice(0, 1)}.`.trim()); setMemberSearch('') }}>
                      {m.firstName} {m.lastName} <span className="text-xs" style={{ color: 'var(--text-s)' }}>{m.email ?? m.phone ?? ''}</span>
                    </button>
                  ))}
                </div>
              )}
            </>
          )}
        </div>
        <div><label className="flbl">Görünen ad <span className="text-red-500">*</span></label><input className="inp" value={memberName} onChange={e => setMemberName(e.target.value)} placeholder="Ayşe K." /></div>
        <div><label className="flbl">Konu</label><input className="inp" value={topic} onChange={e => setTopic(e.target.value)} placeholder="Kalıp / Kumaş / Kargo…" /></div>
        <div className="col-span-2"><label className="flbl">Yorum</label><textarea className="ta" rows={4} value={text} onChange={e => setText(e.target.value)} /></div>
        <label className="col-span-2 flex items-center gap-2 text-sm cursor-pointer" style={{ color: 'var(--text)' }}>
          <input type="checkbox" checked={approve} onChange={e => setApprove(e.target.checked)} /> Onaylı olarak yayınla (kapalıysa onay kuyruğuna düşer)
        </label>
      </div>
      {error && <p className="text-sm mt-2 text-red-500">{error}</p>}
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!platformId || !productCode.trim() || !memberName.trim()}>Kaydet</Button>
      </div>
    </Modal>
  )
}
