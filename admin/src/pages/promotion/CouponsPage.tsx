import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText as gridErrText } from '@/components/ui/DataTable.utils'
import { cn } from '@/lib/utils'

interface Coupon {
  id: string
  campaignId?: string
  memberId?: string
  memberGroupId?: string
  memberName?: string
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

interface MemberGroup {
  id: string
  nameI18n: Record<string, string>
}

interface MemberHit {
  id: string
  firstName: string
  lastName: string
  email?: string
}

const BOS_GRUPLAR: MemberGroup[] = []

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

function uyeAdi(m: MemberHit) {
  return `${m.firstName} ${m.lastName}`.trim() || m.email || m.id.slice(0, 8)
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

  // Hedef: herkese açık / belirli üye / üye grubu (ikisi birden seçilemez — sunucuda da kontrollü)
  const [hedef, setHedef] = useState<'all' | 'member' | 'group'>(
    c?.memberId ? 'member' : c?.memberGroupId ? 'group' : 'all')
  const [memberId, setMemberId] = useState(c?.memberId ?? '')
  const [memberLabel, setMemberLabel] = useState(c?.memberName ?? '')
  const [groupId, setGroupId] = useState(c?.memberGroupId ?? '')
  const [memberSearch, setMemberSearch] = useState('')

  const { data: groups = BOS_GRUPLAR } = useQuery<MemberGroup[]>({
    queryKey: ['member-groups'],
    queryFn: async () => (await api.get('/crm/member-groups')).data.data ?? BOS_GRUPLAR,
    enabled: hedef === 'group',
  })

  const aramaTerimi = memberSearch.trim()
  const { data: memberHits, isFetching: uyeAraniyor } = useQuery<PagedResult<MemberHit>>({
    queryKey: ['coupon-member-search', aramaTerimi],
    queryFn: async () =>
      (await api.get(`/crm/members?search=${encodeURIComponent(aramaTerimi)}&activeOnly=false&pageSize=8`)).data.data,
    enabled: hedef === 'member' && aramaTerimi.length >= 2,
  })

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
        memberId: hedef === 'member' ? memberId : null,
        memberGroupId: hedef === 'group' ? groupId : null,
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

  const hedefTamam = hedef === 'all' || (hedef === 'member' ? !!memberId : !!groupId)
  const valid = code.trim().length >= 3 && name.trim() && parseFloat(discountValue) > 0 && hedefTamam

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
        <div className="rounded-lg p-3" style={{ border: '1px solid var(--border)' }}>
          <label className="flbl">Kimler Kullanabilir?</label>
          <select className="inp" value={hedef}
            onChange={e => {
              const v = e.target.value as 'all' | 'member' | 'group'
              setHedef(v)
              if (v !== 'member') { setMemberId(''); setMemberLabel(''); setMemberSearch('') }
              if (v !== 'group') setGroupId('')
            }}>
            <option value="all">Herkes (kodu bilen herkes)</option>
            <option value="member">Belirli bir üye (kişiye özel)</option>
            <option value="group">Üye grubu</option>
          </select>
          <div className="mt-2">
            {hedef === 'all' ? (
              <p className="text-xs" style={{ color: 'var(--text-s)' }}>
                Kod sepette elle girilir; kimseye özel değildir.
              </p>
            ) : hedef === 'group' ? (
              <select className="inp" value={groupId} onChange={e => setGroupId(e.target.value)}>
                <option value="">— grup seçin —</option>
                {groups.map(g => (
                  <option key={g.id} value={g.id}>{g.nameI18n?.['tr'] ?? g.id.slice(0, 8)}</option>
                ))}
              </select>
            ) : memberId ? (
              <div className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
                <span className="px-2 py-1 rounded-lg" style={{ background: 'var(--surface2)' }}>
                  {memberLabel || memberId.slice(0, 8)}
                </span>
                <button type="button" className="text-xs underline" style={{ color: 'var(--brand)' }}
                  onClick={() => { setMemberId(''); setMemberLabel('') }}>değiştir</button>
              </div>
            ) : (
              <div>
                <input className="inp" value={memberSearch} onChange={e => setMemberSearch(e.target.value)}
                  placeholder="Üye ara — ad, soyad, e-posta veya telefon (en az 2 karakter)" />
                <div className="mt-1 max-h-40 overflow-y-auto">
                  {aramaTerimi.length >= 2 && uyeAraniyor && (
                    <p className="text-xs px-1 py-1" style={{ color: 'var(--text-s)' }}>Aranıyor…</p>
                  )}
                  {aramaTerimi.length >= 2 && !uyeAraniyor && (memberHits?.items?.length ?? 0) === 0 && (
                    <p className="text-xs px-1 py-1" style={{ color: 'var(--text-s)' }}>Üye bulunamadı.</p>
                  )}
                  {(memberHits?.items ?? []).map(m => (
                    <button key={m.id} type="button"
                      className="w-full text-left text-sm px-2 py-1.5 rounded-lg hover:bg-[var(--surface2)]"
                      style={{ color: 'var(--text)' }}
                      onClick={() => { setMemberId(m.id); setMemberLabel(uyeAdi(m)) }}>
                      {uyeAdi(m)}
                      <span className="text-xs ml-2" style={{ color: 'var(--text-s)' }}>{m.email ?? ''}</span>
                    </button>
                  ))}
                </div>
              </div>
            )}
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
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (CouponGrid.Schema) + Excel export + kaydedilmiş görünümler.
  // Sekme ?tab=all (varsayılan: yalnız aktif) — named "isActive" olarak sunucuya gider.
  const [sp] = useSearchParams()
  const tab: 'active' | '' = sp.get('tab') === 'all' ? '' : 'active'
  const grid = useGridState('coupons', { defaultPageSize: 20, defaultSort: 'createdAt', defaultDir: 'desc' })
  const switchTab = (v: 'active' | '') => grid.mutate(n => { if (v === '') n.set('tab', 'all'); else n.delete('tab') })
  const [editing, setEditing] = useState<Coupon | 'new' | null>(null)
  const [usagesFor, setUsagesFor] = useState<Coupon | null>(null)

  const { data: groups = BOS_GRUPLAR } = useQuery<MemberGroup[]>({
    queryKey: ['member-groups'],
    queryFn: async () => (await api.get('/crm/member-groups')).data.data ?? BOS_GRUPLAR,
  })

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<Coupon>>({
    queryKey: ['coupons', tab, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/promotion/coupons?${grid.toParams({ isActive: tab === 'active' ? 'true' : undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })

  const coupons = data?.items ?? []

  const hedefYazisi = (cp: Coupon) => {
    if (cp.memberId) return cp.memberName ?? 'Kişiye özel'
    if (cp.memberGroupId)
      return groups.find(g => g.id === cp.memberGroupId)?.nameI18n?.['tr'] ?? 'Üye grubu'
    return 'Herkes'
  }

  const columns: GridColumn<Coupon>[] = [
    { key: 'code', header: 'KOD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 130,
      filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      cell: cp => <code className="text-xs font-mono font-medium" style={{ color: 'var(--text)' }}>{cp.code}</code> },
    { key: 'name', header: 'AD', priority: 2, sortable: true, filter: { type: 'text', label: 'Ad' },
      cell: cp => <span className="text-sm" style={{ color: 'var(--text)' }}>{cp.nameI18n?.['tr'] ?? '—'}</span> },
    { key: 'hedef', header: 'KİM KULLANABİLİR', priority: 1, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Kime açık', options: [
        { value: 'all', label: 'Herkes' }, { value: 'member', label: 'Kişiye Özel' }, { value: 'group', label: 'Üye Grubu' }] },
      cell: cp => <span className="text-sm" style={{ color: cp.memberId || cp.memberGroupId ? 'var(--text)' : 'var(--text-s)' }}>
        {hedefYazisi(cp)}
        {cp.memberId != null && cp.memberId !== '' && <span className="text-xs ml-1" style={{ color: 'var(--text-s)' }}>(kişiye özel)</span>}
      </span> },
    { key: 'discountValue', header: 'İNDİRİM', priority: 1, align: 'right', sortable: true,
      filter: { type: 'number', label: 'İndirim değeri' },
      filters: [
        { field: 'couponType', label: 'Kupon tipi', type: 'enum', multiple: true, options: [
          { value: 'percentage', label: 'Yüzde' }, { value: 'amount', label: 'Tutar' }, { value: 'free_shipping', label: 'Ücretsiz Kargo' }] },
        { field: 'minimumCartTotal', label: 'Alt sepet tutarı', type: 'number' }],
      cell: cp => <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>
        {indirimYazisi(cp)}
        {cp.minimumCartTotal != null && <span className="text-xs ml-1" style={{ color: 'var(--text-s)' }}>
          (min {cp.minimumCartTotal.toLocaleString('tr-TR')} ₺)</span>}
      </span> },
    { key: 'usageCount', header: 'KULLANIM', priority: 2, align: 'right', sortable: true,
      filter: { type: 'number', label: 'Kullanım sayısı' },
      filters: [
        { field: 'used', label: 'Kullanılmış', type: 'boolean' },
        { field: 'usageLimitTotal', label: 'Toplam limit', type: 'number' }],
      cell: cp => <span className="text-sm" style={{ color: 'var(--text-m)' }}>
        {cp.usageCount}{cp.usageLimitTotal != null ? ` / ${cp.usageLimitTotal}` : ''}</span> },
    { key: 'startsAt', header: 'GEÇERLİLİK', priority: 2, sortable: true, filter: { type: 'date', label: 'Başlangıç', quick: true },
      filters: [
        { field: 'endsAt', label: 'Bitiş', type: 'date' },
        { field: 'live', label: 'Bugün geçerli', type: 'boolean' },
        { field: 'firstOrderOnly', label: 'Yalnız ilk sipariş', type: 'boolean' }],
      cell: cp => <span className="text-xs" style={{ color: 'var(--text-s)' }}>
        {new Date(cp.startsAt).toLocaleDateString('tr-TR')} → {cp.endsAt ? new Date(cp.endsAt).toLocaleDateString('tr-TR') : 'süresiz'}</span> },
    { key: 'isActive', header: 'DURUM', priority: 1, lockVisible: true, sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [{ field: 'createdAt', label: 'Oluşturma', type: 'date' }],
      cell: cp => <Badge variant={cp.isActive ? 'success' : 'neutral'}>{cp.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'actions', header: '', priority: 3, align: 'right', exportable: false, stopRowClick: true,
      cell: cp => <>
        <button className="text-xs underline mr-2" style={{ color: 'var(--brand)' }}
          onClick={e => { e.stopPropagation(); setUsagesFor(cp) }}>Kullanımlar</button>
        <span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span>
      </> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kuponlar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>
            {(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}
          </p>
        </div>
        <Button size="sm" onClick={() => setEditing('new')}>+ Yeni Kupon</Button>
      </div>

      <div className="tab-scroll flex gap-1 mb-4" style={{ borderBottom: '1px solid var(--border)' }}>
        <button className={cn('stab', tab === 'active' && 'active')} onClick={() => switchTab('active')}>Aktif</button>
        <button className={cn('stab', tab === '' && 'active')} onClick={() => switchTab('')}>Tümü</button>
      </div>

      <DataGrid<Coupon>
        gridId="coupons"
        views
        grid={grid}
        columns={columns}
        rows={coupons}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? gridErrText(listError) : null}
        onRowClick={cp => setEditing(cp)}
        empty='Kupon yok. "+ Yeni Kupon" ile tanımlayın; müşteriler sepette kodu girerek kullanır.'
        search={{ placeholder: 'Kupon kodu ara…' }}
        minWidth={900}
        export={{ endpoint: '/promotion/coupons/export', named: () => ({ isActive: tab === 'active' ? 'true' : undefined }), fallbackFileName: 'kuponlar.xlsx' }}
        compact={{
          title: cp => cp.code,
          subtitle: cp => `${cp.nameI18n?.['tr'] ?? '—'} · ${hedefYazisi(cp)}`,
          right: cp => indirimYazisi(cp),
          badge: cp => <Badge variant={cp.isActive ? 'success' : 'neutral'}>{cp.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />

      {editing !== null && <CouponModal coupon={editing} onClose={() => setEditing(null)} />}
      {usagesFor && <UsagesModal coupon={usagesFor} onClose={() => setUsagesFor(null)} />}
    </div>
  )
}
