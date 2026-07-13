import { useEffect, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router-dom'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { PageSpinner } from '@/components/ui/Spinner'
import { ORDER_STATUS_MAP, type OrderSummary } from '../orders/OrdersPage'

interface MemberDetail {
  id: string
  memberGroupId: string
  firstName: string
  lastName: string
  email?: string
  phone?: string
  gender?: string
  birthDate?: string
  taxOffice?: string
  taxNumber?: string
  companyName?: string
  isRegistered: boolean
  isEmailVerified: boolean
  isPhoneVerified: boolean
  isActive: boolean
  lastLoginAt?: string
  createdAt: string
  identityVerified?: boolean
  marketingConsents?: { email: boolean; sms: boolean; phone: boolean }
}

interface MemberGroup {
  id: string
  code: string
  nameI18n: Record<string, string>
}

interface MemberAddress {
  id: string
  title: string
  cityName?: string
  districtName?: string
  neighborhoodName?: string
  addressLine?: string
  postalCode?: string
  recipientName: string
  recipientPhone: string
  isDefault: boolean
}

interface MemberSession {
  id: string
  createdAt: string
  expiresAt: string
  isActive: boolean
  ipAddress?: string
  userAgent?: string
}

interface Engagement {
  favoriteCount: number
  collectionCount: number
  reviewCount: number
  savedSearchCount: number
  activeStockAlertCount: number
  viewedProductCount: number
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="card p-4">
      <h2 className="text-sm font-semibold mb-3" style={{ color: 'var(--text)' }}>{title}</h2>
      {children}
    </div>
  )
}

export function MemberDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { data: member, isLoading } = useQuery<MemberDetail>({
    queryKey: ['member', id],
    queryFn: async () => (await api.get(`/crm/members/${id}`)).data.data,
    enabled: !!id,
  })
  const { data: groups = [] } = useQuery<MemberGroup[]>({
    queryKey: ['member-groups'],
    queryFn: async () => (await api.get('/crm/member-groups?activeOnly=false')).data.data,
  })
  const { data: addresses = [] } = useQuery<MemberAddress[]>({
    queryKey: ['member-addresses', id],
    queryFn: async () => (await api.get(`/crm/members/${id}/addresses`)).data.data,
    enabled: !!id,
  })
  const { data: sessions = [] } = useQuery<MemberSession[]>({
    queryKey: ['member-sessions', id],
    queryFn: async () => (await api.get(`/crm/members/${id}/sessions`)).data.data,
    enabled: !!id,
    retry: false,
  })
  const { data: engagement } = useQuery<Engagement>({
    queryKey: ['member-engagement', id],
    queryFn: async () => (await api.get(`/crm/members/${id}/engagement`)).data.data,
    enabled: !!id,
    retry: false,
  })
  const { data: wallet } = useQuery<{ balance: number; currencyCode: string }>({
    queryKey: ['member-wallet', id],
    queryFn: async () => (await api.get(`/crm/members/${id}/wallet`)).data.data,
    enabled: !!id,
    retry: false,
  })
  const { data: loyalty } = useQuery<{ availablePoints: number }>({
    queryKey: ['member-loyalty', id],
    queryFn: async () => (await api.get(`/crm/members/${id}/loyalty`)).data.data,
    enabled: !!id,
    retry: false,
  })
  const { data: ordersData } = useQuery<{ items: OrderSummary[]; totalCount: number }>({
    queryKey: ['member-orders', id],
    queryFn: async () => (await api.get(`/orders?memberId=${id}&pageSize=10`)).data.data,
    enabled: !!id,
  })

  const [groupId, setGroupId] = useState('')
  const [isActive, setIsActive] = useState(true)
  const [error, setError] = useState('')
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    if (!member) return
    setGroupId(member.memberGroupId)
    setIsActive(member.isActive)
  }, [member])

  const save = useMutation({
    mutationFn: async () => {
      setError(''); setSaved(false)
      await api.put(`/crm/members/${id}`, {
        firstName: member!.firstName,
        lastName: member!.lastName,
        email: member!.email ?? null,
        phone: member!.phone ?? null,
        gender: member!.gender ?? null,
        birthDate: member!.birthDate ?? null,
        taxOffice: member!.taxOffice ?? null,
        taxNumber: member!.taxNumber ?? null,
        companyName: member!.companyName ?? null,
        isActive,
        memberGroupId: groupId || null,
      })
      queryClient.invalidateQueries({ queryKey: ['member', id] })
      queryClient.invalidateQueries({ queryKey: ['members'] })
    },
    onSuccess: () => setSaved(true),
    onError: (e: unknown) => {
      const err = e as { response?: { data?: { error?: string } } }
      setError(err.response?.data?.error ?? 'Kaydedilemedi.')
    },
  })

  if (isLoading || !member) return <PageSpinner />

  const orders = ordersData?.items ?? []
  const stats: { label: string; value: React.ReactNode }[] = [
    { label: 'Sipariş', value: ordersData?.totalCount ?? '—' },
    { label: 'Favori', value: engagement?.favoriteCount ?? '—' },
    { label: 'Koleksiyon', value: engagement?.collectionCount ?? '—' },
    { label: 'Yorum', value: engagement?.reviewCount ?? '—' },
    { label: 'Kayıtlı Arama', value: engagement?.savedSearchCount ?? '—' },
    { label: 'Stok Alarmı', value: engagement?.activeStockAlertCount ?? '—' },
    { label: 'Cüzdan', value: wallet ? `${wallet.balance.toLocaleString('tr-TR')} ₺` : '—' },
    { label: 'Puan', value: loyalty?.availablePoints ?? '—' },
  ]

  return (
    <div className="p-6 max-w-5xl">
      <div className="flex flex-wrap items-center gap-3 mb-1">
        <button onClick={() => navigate('/crm/members')} className="text-sm" style={{ color: 'var(--text-s)' }}>←</button>
        <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>{member.firstName} {member.lastName}</h1>
        <Badge variant={member.isActive ? 'success' : 'neutral'}>{member.isActive ? 'Aktif' : 'Pasif'}</Badge>
        {member.isPhoneVerified && <Badge variant="success">Tel ✓</Badge>}
        {member.isEmailVerified && <Badge variant="success">E-posta ✓</Badge>}
        {member.identityVerified && <Badge variant="success">TCKN ✓</Badge>}
      </div>
      <p className="text-sm mb-5" style={{ color: 'var(--text-s)' }}>
        {member.email ?? '—'} · {member.phone ?? '—'}
        {' · '}Kayıt: {new Date(member.createdAt).toLocaleDateString('tr-TR')}
        {member.lastLoginAt && <> · Son giriş: {new Date(member.lastLoginAt).toLocaleString('tr-TR')}</>}
      </p>

      {/* Özet kartları */}
      <div className="grid grid-cols-4 sm:grid-cols-8 gap-2 mb-4">
        {stats.map(s => (
          <div key={s.label} className="card p-3 text-center">
            <div className="text-lg font-bold" style={{ color: 'var(--text)' }}>{s.value}</div>
            <div className="text-xs" style={{ color: 'var(--text-s)' }}>{s.label}</div>
          </div>
        ))}
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Section title="Üyelik Yönetimi">
          <div className="space-y-3">
            <div>
              <label className="flbl">Üye Grubu</label>
              <select className="inp" value={groupId} onChange={e => setGroupId(e.target.value)}>
                {groups.map(g => (
                  <option key={g.id} value={g.id}>{g.nameI18n?.['tr'] ?? g.code}</option>
                ))}
              </select>
            </div>
            <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} />
              Aktif (pasif üye giriş yapamaz)
            </label>
            {member.marketingConsents && (
              <p className="text-xs" style={{ color: 'var(--text-s)' }}>
                Duyuru tercihleri (üye kendi belirler): e-posta {member.marketingConsents.email ? '✓' : '✗'}
                {' · '}SMS {member.marketingConsents.sms ? '✓' : '✗'}
                {' · '}telefon {member.marketingConsents.phone ? '✓' : '✗'}
              </p>
            )}
            {error && <p className="text-sm text-red-500">{error}</p>}
            {saved && <p className="text-sm" style={{ color: 'var(--brand)' }}>Kaydedildi ✓</p>}
            <div className="flex justify-end">
              <Button size="sm" onClick={() => save.mutate()} loading={save.isPending}>Kaydet</Button>
            </div>
          </div>
        </Section>

        <Section title={`Adresler (${addresses.length})`}>
          {addresses.length === 0 && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Kayıtlı adres yok.</p>}
          <div className="space-y-2 max-h-64 overflow-y-auto">
            {addresses.map(a => (
              <div key={a.id} className="text-sm p-2 rounded-lg" style={{ background: 'var(--surface2)' }}>
                <div className="flex items-center gap-2">
                  <span className="font-medium" style={{ color: 'var(--text)' }}>{a.title}</span>
                  {a.isDefault && <Badge variant="neutral">Varsayılan</Badge>}
                </div>
                <div className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>
                  {a.recipientName} · {a.recipientPhone}
                </div>
                <div className="text-xs" style={{ color: 'var(--text-m)' }}>
                  {a.addressLine} {[a.neighborhoodName, a.districtName, a.cityName].filter(Boolean).join(' / ')}
                </div>
              </div>
            ))}
          </div>
        </Section>

        <Section title={`Son Siparişler (${ordersData?.totalCount ?? 0})`}>
          {orders.length === 0 && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Sipariş yok.</p>}
          {orders.map(o => {
            const st = ORDER_STATUS_MAP[o.status] ?? { label: o.status, variant: 'neutral' as const }
            return (
              <Link key={o.id} to={`/orders/${o.id}`}
                className="flex items-center gap-2 text-sm py-1.5 px-1 rounded-lg hover:bg-[var(--surface2)]"
                style={{ borderBottom: '1px solid var(--border)' }}>
                <code className="text-xs font-mono" style={{ color: 'var(--text)' }}>{o.orderNumber}</code>
                <span className="font-medium" style={{ color: 'var(--text)' }}>
                  {o.grandTotal.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺
                </span>
                <Badge variant={st.variant}>{st.label}</Badge>
                <span className="text-xs ml-auto" style={{ color: 'var(--text-s)' }}>
                  {new Date(o.createdAt).toLocaleDateString('tr-TR')}
                </span>
              </Link>
            )
          })}
        </Section>

        <Section title="Oturumlar (son 10)">
          {sessions.length === 0 && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Oturum kaydı yok.</p>}
          <div className="space-y-1 max-h-64 overflow-y-auto">
            {sessions.map(s => (
              <div key={s.id} className="flex items-center gap-2 text-xs p-1.5 rounded"
                style={{ color: 'var(--text-s)' }}>
                <Badge variant={s.isActive ? 'success' : 'neutral'}>{s.isActive ? 'Açık' : 'Kapalı'}</Badge>
                <span>{new Date(s.createdAt).toLocaleString('tr-TR')}</span>
                {s.ipAddress && <span>· {s.ipAddress}</span>}
                {s.userAgent && <span className="truncate" title={s.userAgent}>· {s.userAgent.slice(0, 40)}…</span>}
              </div>
            ))}
          </div>
        </Section>
      </div>
    </div>
  )
}
