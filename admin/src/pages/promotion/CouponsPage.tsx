import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { cn } from '@/lib/utils'

interface Coupon {
  id: string
  campaignId?: string
  memberId?: string
  code: string
  nameI18n: Record<string, string>
  couponType: string
  discountValue: number
  usageLimitTotal?: number
  usageLimitPerMember?: number
  usageCount: number
  minimumCartTotal?: number
  validForFirstOrderOnly: boolean
  startsAt: string
  endsAt?: string
  isActive: boolean
  createdAt: string
}

interface CouponUsage {
  id: string
  memberId: string
  orderId: string
  discountAmount: number
  usedAt: string
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

function errText(e: unknown) {
  const err = e as { response?: { data?: { error?: string } } }
  return err.response?.data?.error ?? 'İşlem başarısız oldu.'
}

function indirimYazisi(c: Coupon) {
  return c.couponType === 'percentage'
    ? `%${c.discountValue}`
    : `${c.discountValue.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺`
}

function CouponModal({ coupon, onClose }: { coupon: Coupon | 'new'; onClose: () => void }) {
  const queryClient = useQueryClient()
  const isNew = coupon === 'new'
  const c = isNew ? undefined : coupon

  const [code, setCode] = useState(c?.code ?? '')
  const [name, setName] = useState(c?.nameI18n?.['tr'] ?? '')
  const [couponType, setCouponType] = useState(c?.couponType ?? 'percentage')
  const [discountValue, setDiscountValue] = useState(c ? String(c.discountValue) : '')
  const [limitTotal, setLimitTotal] = useState(c?.usageLimitTotal != null ? String(c.usageLimitTotal) : '')
  const [limitPerMember, setLimitPerMember] = useState(c?.usageLimitPerMember != null ? String(c.usageLimitPerMember) : '')
  const [minCart, setMinCart] = useState(c?.minimumCartTotal != null ? String(c.minimumCartTotal) : '')
  const [firstOnly, setFirstOnly] = useState(c?.validForFirstOrderOnly ?? false)
  const [starts, setStarts] = useState((c?.startsAt ?? new Date().toISOString()).slice(0, 10))
  const [ends, setEnds] = useState(c?.endsAt ? c.endsAt.slice(0, 10) : '')
  const [isActive, setIsActive] = useState(c?.isActive ?? true)
  const [error, setError] = useState('')

  const remove = useMutation({
    mutationFn: async () => {
      setError('')
      await api.delete(`/promotion/coupons/${c!.id}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['coupons'] })
      onClose()
    },
    onError: (e: unknown) => setError(errText(e)),
  })

  const save = useMutation({
    mutationFn: async () => {
      setError('')
      const body = {
        code: code.trim().toUpperCase(),
        nameI18n: { ...(c?.nameI18n ?? {}), tr: name.trim() },
        couponType,
        discountValue: parseFloat(discountValue) || 0,
        usageLimitTotal: limitTotal ? parseInt(limitTotal) : null,
        usageLimitPerMember: limitPerMember ? parseInt(limitPerMember) : null,
        minimumCartTotal: minCart ? parseFloat(minCart) : null,
        validForFirstOrderOnly: firstOnly,
        startsAt: new Date(`${starts}T00:00:00`).toISOString(),
        endsAt: ends ? new Date(`${ends}T23:59:59`).toISOString() : null,
        isActive,
      }
      if (isNew) await api.post('/promotion/coupons', body)
      else await api.put(`/promotion/coupons/${c!.id}`, body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['coupons'] })
      onClose()
    },
    onError: (e: unknown) => setError(errText(e)),
  })

  const valid = code.trim().length >= 3 && name.trim() && parseFloat(discountValue) > 0

  return (
    <Modal open onClose={onClose} title={isNew ? 'Yeni Kupon' : `Kupon: ${c?.code}`}>
      <div className="space-y-3">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Kupon Kodu <span className="text-red-500">*</span></label>
            <input className="inp font-mono" value={code} disabled={!isNew}
              onChange={e => setCode(e.target.value.toUpperCase())} placeholder="HOSGELDIN10" />
          </div>
          <div>
            <label className="flbl">Ad <span className="text-red-500">*</span></label>
            <input className="inp" value={name} onChange={e => setName(e.target.value)}
              placeholder="Hoş geldin indirimi" />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">İndirim Tipi</label>
            <select className="inp" value={couponType} onChange={e => setCouponType(e.target.value)}>
              <option value="percentage">Yüzde (%)</option>
              <option value="fixed">Tutar (₺)</option>
            </select>
          </div>
          <div>
            <label className="flbl">İndirim Değeri <span className="text-red-500">*</span></label>
            <input type="number" step="0.01" min="0" className="inp" value={discountValue}
              onChange={e => setDiscountValue(e.target.value)} />
          </div>
        </div>
        <div className="grid grid-cols-3 gap-3">
          <div>
            <label className="flbl">Toplam Limit</label>
            <input type="number" min="0" className="inp" value={limitTotal}
              onChange={e => setLimitTotal(e.target.value)} placeholder="sınırsız" />
          </div>
          <div>
            <label className="flbl">Üye Başı Limit</label>
            <input type="number" min="0" className="inp" value={limitPerMember}
              onChange={e => setLimitPerMember(e.target.value)} placeholder="sınırsız" />
          </div>
          <div>
            <label className="flbl">En Az Sepet (₺)</label>
            <input type="number" step="0.01" min="0" className="inp" value={minCart}
              onChange={e => setMinCart(e.target.value)} placeholder="yok" />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Başlangıç</label>
            <input type="date" className="inp" value={starts} onChange={e => setStarts(e.target.value)} />
          </div>
          <div>
            <label className="flbl">Bitiş <span className="text-xs" style={{ color: 'var(--text-s)' }}>(boş = süresiz)</span></label>
            <input type="date" className="inp" value={ends} onChange={e => setEnds(e.target.value)} />
          </div>
        </div>
        <div className="flex items-center gap-4">
          <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
            <input type="checkbox" checked={firstOnly} onChange={e => setFirstOnly(e.target.checked)} />
            Yalnız ilk siparişte geçerli
          </label>
          {!isNew && (
            <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} />
              Aktif
            </label>
          )}
        </div>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
      <div className="flex items-center gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        {!isNew && c!.usageCount === 0 && (
          <button
            className="text-sm text-red-600 hover:underline disabled:opacity-50"
            disabled={remove.isPending}
            onClick={() => {
              if (window.confirm(`'${c!.code}' kuponu silinsin mi? Bu işlem geri alınamaz.`)) remove.mutate()
            }}>
            Sil
          </button>
        )}
        {!isNew && c!.usageCount > 0 && (
          <span className="text-xs" style={{ color: 'var(--text-s)' }}>
            Kullanılmış kupon silinemez; pasife alabilirsiniz.
          </span>
        )}
        <div className="flex-1" />
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!valid}>Kaydet</Button>
      </div>
    </Modal>
  )
}

function UsagesModal({ coupon, onClose }: { coupon: Coupon; onClose: () => void }) {
  const [page, setPage] = useState(1)
  const { data, isLoading } = useQuery<PagedResult<CouponUsage>>({
    queryKey: ['coupon-usages', coupon.id, page],
    queryFn: async () =>
      (await api.get(`/promotion/coupons/${coupon.id}/usages?page=${page}&pageSize=20`)).data.data,
  })
  const usages = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 20)

  return (
    <Modal open onClose={onClose} title={`Kullanımlar: ${coupon.code} (${data?.totalCount ?? 0})`}>
      {isLoading && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</p>}
      {!isLoading && usages.length === 0 && (
        <p className="text-sm" style={{ color: 'var(--text-s)' }}>Bu kupon henüz kullanılmadı.</p>
      )}
      <div className="space-y-1 max-h-96 overflow-y-auto">
        {usages.map(u => (
          <div key={u.id} className="flex items-center gap-3 text-sm px-2 py-1.5 rounded-lg"
            style={{ background: 'var(--surface2)' }}>
            <span style={{ color: 'var(--text)' }}>
              -{u.discountAmount.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺
            </span>
            <code className="text-xs" style={{ color: 'var(--text-s)' }}>sipariş {u.orderId.slice(0, 8)}…</code>
            <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>
              {new Date(u.usedAt).toLocaleString('tr-TR')}
            </span>
          </div>
        ))}
      </div>
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 mt-3">
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
            className="px-2 py-1 rounded text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>←</button>
          <span className="text-sm" style={{ color: 'var(--text-s)' }}>{page} / {totalPages}</span>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}
            className="px-2 py-1 rounded text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>→</button>
        </div>
      )}
      <div className="flex justify-end mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Kapat</Button>
      </div>
    </Modal>
  )
}

export function CouponsPage() {
  const [tab, setTab] = useState<'active' | 'all'>('active')
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<Coupon | 'new' | null>(null)
  const [usagesFor, setUsagesFor] = useState<Coupon | null>(null)

  const { data, isLoading } = useQuery<PagedResult<Coupon>>({
    queryKey: ['coupons', tab, appliedSearch, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (tab === 'active') params.set('isActive', 'true')
      if (appliedSearch) params.set('search', appliedSearch)
      return (await api.get(`/promotion/coupons?${params}`)).data.data
    },
  })

  const coupons = data?.items ?? []
  const totalPages = Math.ceil((data?.totalCount ?? 0) / 20)

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kuponlar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{data?.totalCount ?? 0} kayıt</p>
        </div>
        <Button size="sm" onClick={() => setEditing('new')}>+ Yeni Kupon</Button>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'active' && 'active')} onClick={() => { setTab('active'); setPage(1) }}>Aktif</button>
        <button className={cn('stab', tab === 'all' && 'active')} onClick={() => { setTab('all'); setPage(1) }}>Tümü</button>
      </div>

      <div className="flex items-center gap-2 mb-4">
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 220 }}
          placeholder="Kupon kodu ara…" value={search}
          onChange={e => setSearch(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') { setAppliedSearch(search.trim()); setPage(1) } }} />
        <button onClick={() => { setAppliedSearch(search.trim()); setPage(1) }}
          className="px-3 py-1.5 rounded-lg text-sm"
          style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Ara</button>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'AD', 'İNDİRİM', 'KULLANIM', 'GEÇERLİLİK', 'DURUM', ''].map(h => (
                <th key={h} className={`px-4 py-3 text-xs font-semibold ${h === '' ? 'w-28' : 'text-left'}`}
                  style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</td></tr>
            )}
            {!isLoading && coupons.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>
                Kupon yok. "+ Yeni Kupon" ile tanımlayın; müşteriler sepette kodu girerek kullanır.
              </td></tr>
            )}
            {coupons.map(cp => (
              <tr key={cp.id} onClick={() => setEditing(cp)}
                className="cursor-pointer hover:bg-[var(--surface2)] transition-colors"
                style={{ borderBottom: '1px solid var(--border)' }}>
                <td className="px-4 py-3">
                  <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{cp.code}</code>
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text)' }}>{cp.nameI18n?.['tr'] ?? '—'}</td>
                <td className="px-4 py-3 text-sm font-medium" style={{ color: 'var(--text)' }}>
                  {indirimYazisi(cp)}
                  {cp.minimumCartTotal != null && (
                    <span className="text-xs ml-1" style={{ color: 'var(--text-s)' }}>
                      (min {cp.minimumCartTotal.toLocaleString('tr-TR')} ₺)
                    </span>
                  )}
                </td>
                <td className="px-4 py-3 text-sm" style={{ color: 'var(--text-m)' }}>
                  {cp.usageCount}{cp.usageLimitTotal != null ? ` / ${cp.usageLimitTotal}` : ''}
                </td>
                <td className="px-4 py-3 text-xs" style={{ color: 'var(--text-s)' }}>
                  {new Date(cp.startsAt).toLocaleDateString('tr-TR')}
                  {' → '}{cp.endsAt ? new Date(cp.endsAt).toLocaleDateString('tr-TR') : 'süresiz'}
                </td>
                <td className="px-4 py-3">
                  <Badge variant={cp.isActive ? 'success' : 'neutral'}>{cp.isActive ? 'Aktif' : 'Pasif'}</Badge>
                </td>
                <td className="px-4 py-3 text-right whitespace-nowrap">
                  <button className="text-xs underline mr-2" style={{ color: 'var(--brand)' }}
                    onClick={e => { e.stopPropagation(); setUsagesFor(cp) }}>Kullanımlar</button>
                  <span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 mt-4">
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>← Önceki</button>
          <span className="text-sm" style={{ color: 'var(--text-s)' }}>{page} / {totalPages}</span>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}
            className="px-3 py-1.5 rounded-lg text-sm disabled:opacity-40"
            style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Sonraki →</button>
        </div>
      )}

      {editing !== null && <CouponModal coupon={editing} onClose={() => setEditing(null)} />}
      {usagesFor && <UsagesModal coupon={usagesFor} onClose={() => setUsagesFor(null)} />}
    </div>
  )
}
