import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge, type BadgeVariant } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { DataTable, Pager, errText, para, i18nAd } from '@/components/ui/DataTable'
import { cn } from '@/lib/utils'

interface GiftCard {
  id: string
  code: string
  originalAmount: number
  remainingAmount: number
  currencyCode: string
  validFrom: string
  validUntil?: string
  isSingleUse: boolean
  status: string
  createdAt: string
}

interface Firm { id: string; code: string; nameI18n: Record<string, string> }
interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

const DURUM: Record<string, [string, BadgeVariant]> = {
  active:    ['Aktif', 'success'],
  used:      ['Kullanıldı', 'neutral'],
  depleted:  ['Bakiye Bitti', 'neutral'],
  expired:   ['Süresi Doldu', 'warning'],
  cancelled: ['İptal', 'danger'],
}

function YeniKartModal({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient()
  const [firmId, setFirmId] = useState('')
  const [amount, setAmount] = useState('')
  const [validFrom, setValidFrom] = useState(new Date().toISOString().slice(0, 10))
  const [validUntil, setValidUntil] = useState('')
  const [singleUse, setSingleUse] = useState(false)
  const [error, setError] = useState('')

  const { data: firms } = useQuery<Firm[]>({
    queryKey: ['firms-select'],
    queryFn: async () => (await api.get('/core/firms')).data.data,
  })

  const save = useMutation({
    mutationFn: async () => {
      setError('')
      await api.post('/orders/gift-cards', {
        firmId,
        amount: parseFloat(amount) || 0,
        currencyCode: 'TRY',
        validFrom,
        validUntil: validUntil || null,
        isSingleUse: singleUse,
        createdForMemberId: null,
        createdFromOrderId: null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['gift-cards'] })
      onClose()
    },
    onError: (e: unknown) => setError(errText(e)),
  })

  const valid = firmId && parseFloat(amount) > 0 && validFrom

  return (
    <Modal open onClose={onClose} title="Yeni Hediye Kartı">
      <div className="space-y-3">
        <div>
          <label className="flbl">Firma <span className="text-red-500">*</span></label>
          <select className="inp" value={firmId} onChange={e => setFirmId(e.target.value)}>
            <option value="">Seçin…</option>
            {(firms ?? []).map(f => <option key={f.id} value={f.id}>{i18nAd(f.nameI18n)}</option>)}
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Tutar (₺) <span className="text-red-500">*</span></label>
            <input type="number" step="0.01" min="0" className="inp" value={amount}
              onChange={e => setAmount(e.target.value)} />
          </div>
          <div className="flex items-end pb-2">
            <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={singleUse} onChange={e => setSingleUse(e.target.checked)} />
              Tek kullanımlık
            </label>
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Geçerlilik Başlangıcı</label>
            <input type="date" className="inp" value={validFrom} onChange={e => setValidFrom(e.target.value)} />
          </div>
          <div>
            <label className="flbl">Bitiş <span className="text-xs" style={{ color: 'var(--text-s)' }}>(boş = süresiz)</span></label>
            <input type="date" className="inp" value={validUntil} onChange={e => setValidUntil(e.target.value)} />
          </div>
        </div>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!valid}>Oluştur</Button>
      </div>
    </Modal>
  )
}

export function GiftCardsPage() {
  const [tab, setTab] = useState<'active' | ''>('active')
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)

  const { data, isLoading } = useQuery<PagedResult<GiftCard>>({
    queryKey: ['gift-cards', tab, appliedSearch, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (tab) params.set('status', tab)
      if (appliedSearch) params.set('search', appliedSearch)
      return (await api.get(`/orders/gift-cards?${params}`)).data.data
    },
  })

  const cards = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 20)

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Hediye Kartları</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{data?.totalCount ?? 0} kayıt</p>
        </div>
        <Button size="sm" onClick={() => setCreating(true)}>+ Yeni Hediye Kartı</Button>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'active' && 'active')} onClick={() => { setTab('active'); setPage(1) }}>Aktif</button>
        <button className={cn('stab', tab === '' && 'active')} onClick={() => { setTab(''); setPage(1) }}>Tümü</button>
      </div>

      <div className="flex items-center gap-2 mb-4">
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 220 }}
          placeholder="Kart kodu ara…" value={search}
          onChange={e => setSearch(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') { setAppliedSearch(search.trim()); setPage(1) } }} />
        <button onClick={() => { setAppliedSearch(search.trim()); setPage(1) }}
          className="px-3 py-1.5 rounded-lg text-sm"
          style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Ara</button>
      </div>

      <DataTable<GiftCard>
        columns={[
          { header: 'KOD', cell: g => <code className="text-xs font-mono font-medium">{g.code}</code> },
          { header: 'TUTAR', cell: g => para(g.originalAmount) },
          { header: 'KALAN', cell: g => <span className="font-medium">{para(g.remainingAmount)}</span> },
          { header: 'GEÇERLİLİK', cell: g => `${new Date(g.validFrom).toLocaleDateString('tr-TR')} → ${g.validUntil ? new Date(g.validUntil).toLocaleDateString('tr-TR') : 'süresiz'}` },
          { header: 'TEK KULLANIM', cell: g => (g.isSingleUse ? 'Evet' : 'Hayır') },
          { header: 'DURUM', cell: g => { const [l, v] = DURUM[g.status] ?? [g.status, 'neutral' as BadgeVariant]; return <Badge variant={v}>{l}</Badge> } },
        ]}
        rows={cards}
        loading={isLoading}
        empty='Hediye kartı yok. "+ Yeni Hediye Kartı" ile oluşturun.'
      />

      <Pager page={page} totalPages={totalPages} onChange={setPage} />
      {creating && <YeniKartModal onClose={() => setCreating(false)} />}
    </div>
  )
}
