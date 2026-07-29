import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ChevronLeft } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { PageSpinner } from '@/components/ui/Spinner'
import type { AccountGroup } from './AccountGroupsPage'

const ACCOUNT_TYPES = [
  { value: 'customer', label: 'Müşteri' },
  { value: 'supplier', label: 'Tedarikçi' },
  { value: 'both',     label: 'Her İkisi' },
]

// Yalnız tedarikçi (supplier/both) carilerde anlamlı
const SUPPLIER_KINDS = [
  { value: 'normal',      label: 'Normal tedarikçi (ürün temini)' },
  { value: 'marketplace', label: 'Pazaryeri satıcısı (panel + API)' },
]
const isSupplier = (t: string) => t === 'supplier' || t === 'both'

const TYPE_BADGE: Record<string, { label: string; color: string }> = {
  customer: { label: 'Müşteri',    color: 'var(--brand)' },
  supplier: { label: 'Tedarikçi', color: '#f59e0b' },
  both:     { label: 'Her İkisi', color: '#8b5cf6' },
}

interface LedgerDto {
  id: string; currency: string; description: string | null
  isDefault: boolean; balance: number; createdAt: string
}

interface AccountDetail {
  id: string; code: string; title: string; accountType: string; supplierKind: string
  groupId: string | null; groupName: string | null; groupCode: string | null
  taxNumber: string | null; taxOffice: string | null; contactName: string | null
  phone: string | null; email: string | null; address: string | null
  city: string | null; country: string | null
  creditLimit: number; currency: string; notes: string | null
  isActive: boolean; createdAt: string; updatedAt: string | null
  ledgers: LedgerDto[]
}

type FormState = {
  title: string; accountType: string; supplierKind: string; groupId: string
  taxNumber: string; taxOffice: string; contactName: string
  phone: string; email: string; address: string; city: string; country: string
  creditLimit: number; currency: string; notes: string; isActive: boolean
}

// ── Create form (for /accounts/new) ───────────────────────────────────────────

type CreateFormState = FormState & { code: string }

const emptyCreateForm = (): CreateFormState => ({
  code: '', title: '', accountType: 'customer', supplierKind: 'normal', groupId: '',
  taxNumber: '', taxOffice: '', contactName: '',
  phone: '', email: '', address: '', city: '', country: 'TR',
  creditLimit: 0, currency: 'TRY', notes: '', isActive: true,
})

export function AccountCreatePage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [form, setForm] = useState<CreateFormState>(emptyCreateForm())
  const [error, setError] = useState('')

  const { data: groups = [] } = useQuery<AccountGroup[]>({
    queryKey: ['account-groups', false],
    queryFn: async () => (await api.get('/accounts/groups?activeOnly=false')).data.data,
  })

  const createMutation = useMutation({
    mutationFn: async () => {
      const { data } = await api.post('/accounts', {
        code: form.code, title: form.title, accountType: form.accountType,
        supplierKind: isSupplier(form.accountType) ? form.supplierKind : 'normal',
        groupId: form.groupId || null, taxNumber: form.taxNumber || null,
        taxOffice: form.taxOffice || null, contactName: form.contactName || null,
        phone: form.phone || null, email: form.email || null, address: form.address || null,
        city: form.city || null, country: form.country || 'TR',
        creditLimit: form.creditLimit, currency: form.currency || 'TRY',
        notes: form.notes || null,
      })
      return data.data.id as string
    },
    onSuccess: (id) => {
      queryClient.invalidateQueries({ queryKey: ['accounts'] })
      navigate(`/accounts/${id}`)
    },
    onError: (err: any) => setError(err?.response?.data?.error ?? 'Bir hata oluştu.'),
  })

  return (
    <div className="p-6 max-w-3xl">
      <button onClick={() => navigate('/accounts')} className="flex items-center gap-1 text-sm mb-4 hover:opacity-80" style={{ color: 'var(--text-s)' }}>
        <ChevronLeft size={14} /> Cari Kartlar
      </button>
      <h1 className="text-xl font-bold mb-6" style={{ color: 'var(--text)' }}>Yeni Cari Kart</h1>

      <div className="card p-6 space-y-5">
        <AccountFormFields form={form} setForm={setForm as any} groups={groups} isCreate />
        {error && <p className="text-sm text-red-500">{error}</p>}
        <div className="flex justify-end gap-2 pt-2" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => navigate('/accounts')}>İptal</Button>
          <Button onClick={() => createMutation.mutate()} loading={createMutation.isPending}
            disabled={!form.code || !form.title}>
            Oluştur
          </Button>
        </div>
      </div>
    </div>
  )
}

// ── Detail / Edit page ─────────────────────────────────────────────────────────

const CURRENCIES = ['TRY', 'USD', 'EUR', 'GBP', 'CHF', 'JPY', 'CAD', 'AUD']

export function AccountDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState(false)
  const [form, setForm] = useState<FormState | null>(null)
  const [error, setError] = useState('')
  const [addLedgerOpen, setAddLedgerOpen] = useState(false)
  const [ledgerForm, setLedgerForm] = useState({ currency: 'USD', description: '' })
  const [ledgerError, setLedgerError] = useState('')

  const [txPage, setTxPage] = useState(1)
  const { data: txData } = useQuery<{
    ledgers: { id: string; conceptCode: string; currency: string; balance: number; isDefault: boolean }[]
    items: { id: string; transactionType: string; debit: number; credit: number; balanceAfter: number; referenceType?: string; description?: string; createdAt: string }[]
    totalCount: number; page: number; pageSize: number
  }>({
    queryKey: ['account-transactions', id, txPage],
    queryFn: async () => (await api.get(`/accounts/${id}/transactions?page=${txPage}&pageSize=25`)).data.data,
    enabled: !!id,
  })

  const { data: account, isLoading } = useQuery<AccountDetail>({
    queryKey: ['account', id],
    queryFn: async () => (await api.get(`/accounts/${id}`)).data.data,
    enabled: !!id,
  })

  const { data: groups = [] } = useQuery<AccountGroup[]>({
    queryKey: ['account-groups', false],
    queryFn: async () => (await api.get('/accounts/groups?activeOnly=false')).data.data,
  })

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!form || !id) return
      await api.put(`/accounts/${id}`, {
        title: form.title, accountType: form.accountType,
        supplierKind: isSupplier(form.accountType) ? form.supplierKind : 'normal',
        groupId: form.groupId || null,
        taxNumber: form.taxNumber || null, taxOffice: form.taxOffice || null,
        contactName: form.contactName || null, phone: form.phone || null,
        email: form.email || null, address: form.address || null,
        city: form.city || null, country: form.country || 'TR',
        creditLimit: form.creditLimit, currency: form.currency || 'TRY',
        notes: form.notes || null, isActive: form.isActive,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['account', id] })
      queryClient.invalidateQueries({ queryKey: ['accounts'] })
      setEditing(false)
      setError('')
    },
    onError: (err: any) => setError(err?.response?.data?.error ?? 'Bir hata oluştu.'),
  })

  const addLedgerMutation = useMutation({
    mutationFn: async () => {
      await api.post(`/accounts/${id}/ledgers`, {
        currency: ledgerForm.currency,
        description: ledgerForm.description || null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['account', id] })
      setAddLedgerOpen(false)
      setLedgerForm({ currency: 'USD', description: '' })
      setLedgerError('')
    },
    onError: (err: any) => setLedgerError(err?.response?.data?.error ?? 'Bir hata oluştu.'),
  })

  function startEdit() {
    if (!account) return
    setForm({
      title: account.title, accountType: account.accountType,
      supplierKind: account.supplierKind ?? 'normal', groupId: account.groupId ?? '',
      taxNumber: account.taxNumber ?? '', taxOffice: account.taxOffice ?? '',
      contactName: account.contactName ?? '', phone: account.phone ?? '',
      email: account.email ?? '', address: account.address ?? '',
      city: account.city ?? '', country: account.country ?? 'TR',
      creditLimit: account.creditLimit, currency: account.currency,
      notes: account.notes ?? '', isActive: account.isActive,
    })
    setEditing(true)
  }

  if (isLoading || !account) return <PageSpinner />

  const typeInfo = TYPE_BADGE[account.accountType]

  return (
    <div className="p-6 max-w-3xl">
      <button onClick={() => navigate('/accounts')} className="flex items-center gap-1 text-sm mb-4 hover:opacity-80" style={{ color: 'var(--text-s)' }}>
        <ChevronLeft size={14} /> Cari Kartlar
      </button>

      {/* Info bar */}
      <div className="flex items-start justify-between mb-6">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>{account.title}</h1>
            <code className="text-xs px-2 py-0.5 rounded-md font-mono" style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>{account.code}</code>
            <span className="text-xs font-medium px-2 py-0.5 rounded-full" style={{ color: typeInfo?.color ?? 'var(--text-m)', background: `${typeInfo?.color ?? '#888'}18` }}>
              {typeInfo?.label ?? account.accountType}
            </span>
            {isSupplier(account.accountType) && account.supplierKind === 'marketplace' && (
              <span className="text-xs font-medium px-2 py-0.5 rounded-full" style={{ color: '#0ea5e9', background: '#0ea5e918' }}>Pazaryeri</span>
            )}
            <Badge variant={account.isActive ? 'success' : 'neutral'}>{account.isActive ? 'Aktif' : 'Pasif'}</Badge>
          </div>
          {account.groupName && <p className="text-sm" style={{ color: 'var(--text-s)' }}>Grup: {account.groupName}</p>}
        </div>
        {!editing && <Button size="sm" onClick={startEdit}>Düzenle</Button>}
      </div>

      {editing && form ? (
        <div className="card p-6 space-y-5">
          <AccountFormFields form={form} setForm={setForm as any} groups={groups} />
          {error && <p className="text-sm text-red-500">{error}</p>}
          <div className="flex justify-end gap-2 pt-2" style={{ borderTop: '1px solid var(--border)' }}>
            <Button variant="secondary" onClick={() => { setEditing(false); setError('') }}>İptal</Button>
            <Button onClick={() => updateMutation.mutate()} loading={updateMutation.isPending}>Kaydet</Button>
          </div>
        </div>
      ) : (
        <div className="card p-6">
          <ReadOnlyFields account={account} />
        </div>
      )}

      {/* ── Hesaplar ── */}
      <div className="mt-6">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Hesaplar</h2>
          <Button size="sm" onClick={() => {
            const existing = account.ledgers.map(l => l.currency)
            const first = CURRENCIES.find(c => !existing.includes(c)) ?? 'USD'
            setLedgerForm({ currency: first, description: '' })
            setLedgerError('')
            setAddLedgerOpen(true)
          }}>+ Hesap Ekle</Button>
        </div>
        <div className="card overflow-hidden">
          <table className="w-full">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                {['PARA BİRİMİ', 'AÇIKLAMA', 'BAKİYE', 'DURUM'].map(h => (
                  <th key={h} className="px-4 py-2.5 text-xs font-semibold text-left" style={{ color: 'var(--text-s)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {account.ledgers.map(l => (
                <tr key={l.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-3">
                    <span className="text-sm font-mono font-semibold" style={{ color: 'var(--text)' }}>{l.currency}</span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm" style={{ color: 'var(--text-s)' }}>{l.description ?? '—'}</span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm font-mono" style={{ color: l.balance < 0 ? '#ef4444' : l.balance > 0 ? '#10b981' : 'var(--text-m)' }}>
                      {l.balance.toLocaleString('tr-TR', { minimumFractionDigits: 2 })}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    {l.isDefault && (
                      <span className="text-xs font-medium px-2 py-0.5 rounded-full" style={{ background: 'var(--brand)18', color: 'var(--brand)' }}>Varsayılan</span>
                    )}
                  </td>
                </tr>
              ))}
              {account.ledgers.length === 0 && (
                <tr><td colSpan={4} className="px-4 py-6 text-center text-sm" style={{ color: 'var(--text-s)' }}>Henüz hesap yok.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Hareket Dökümü (cari çatı — accounts.current_account_transactions) */}
      <div className="card overflow-hidden mt-4">
        <div className="flex items-center justify-between px-4 py-3" style={{ borderBottom: '1px solid var(--border)' }}>
          <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>
            Hareketler ({txData?.totalCount ?? 0})
          </h2>
          <div className="flex gap-1.5">
            {(txData?.ledgers ?? []).map(l => (
              <span key={l.id} className="text-xs px-2 py-0.5 rounded-full" style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>
                {l.conceptCode} · {l.balance.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} {l.currency}
              </span>
            ))}
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)' }}>
                {['Tarih', 'Tip', 'Açıklama', 'Borç', 'Alacak', 'Bakiye'].map(h => (
                  <th key={h} className="px-4 py-2 text-left text-xs font-semibold uppercase" style={{ color: 'var(--text-s)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {(txData?.items ?? []).map(t => (
                <tr key={t.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-2 text-xs whitespace-nowrap" style={{ color: 'var(--text-s)' }}>{new Date(t.createdAt).toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2 text-xs" style={{ color: 'var(--text-m)' }}>
                    {t.transactionType === 'manual_adjustment' ? 'Manuel düzeltme' : t.transactionType === 'return_refund' ? 'İade' : t.transactionType === 'storno' ? 'Ters kayıt' : t.transactionType}
                  </td>
                  <td className="px-4 py-2 text-xs max-w-64 truncate" style={{ color: 'var(--text-s)' }}>{t.description ?? '—'}</td>
                  <td className="px-4 py-2 text-sm font-mono" style={{ color: t.debit > 0 ? '#ef4444' : 'var(--text-s)' }}>{t.debit > 0 ? t.debit.toLocaleString('tr-TR', { minimumFractionDigits: 2 }) : '—'}</td>
                  <td className="px-4 py-2 text-sm font-mono" style={{ color: t.credit > 0 ? '#10b981' : 'var(--text-s)' }}>{t.credit > 0 ? t.credit.toLocaleString('tr-TR', { minimumFractionDigits: 2 }) : '—'}</td>
                  <td className="px-4 py-2 text-sm font-mono" style={{ color: 'var(--text)' }}>{t.balanceAfter.toLocaleString('tr-TR', { minimumFractionDigits: 2 })}</td>
                </tr>
              ))}
              {(txData?.items ?? []).length === 0 && (
                <tr><td colSpan={6} className="px-4 py-6 text-center text-sm" style={{ color: 'var(--text-s)' }}>Henüz hareket yok.</td></tr>
              )}
            </tbody>
          </table>
        </div>
        {(txData?.totalCount ?? 0) > 25 && (
          <div className="flex items-center justify-end gap-2 px-4 py-2" style={{ borderTop: '1px solid var(--border)' }}>
            <button disabled={txPage <= 1} onClick={() => setTxPage(p => p - 1)} className="text-xs px-2 py-1 rounded disabled:opacity-40" style={{ border: '1px solid var(--border)', color: 'var(--text-m)' }}>← Önceki</button>
            <span className="text-xs" style={{ color: 'var(--text-s)' }}>Sayfa {txPage}</span>
            <button disabled={txPage * 25 >= (txData?.totalCount ?? 0)} onClick={() => setTxPage(p => p + 1)} className="text-xs px-2 py-1 rounded disabled:opacity-40" style={{ border: '1px solid var(--border)', color: 'var(--text-m)' }}>Sonraki →</button>
          </div>
        )}
      </div>

      {/* Add Ledger Modal */}
      <Modal open={addLedgerOpen} onClose={() => setAddLedgerOpen(false)} title="Yeni Hesap Ekle">
        <div className="space-y-4">
          <div>
            <label className="flbl">Para Birimi <span className="text-red-500">*</span></label>
            <select className="inp" value={ledgerForm.currency}
              onChange={e => setLedgerForm(f => ({ ...f, currency: e.target.value }))}>
              {CURRENCIES.map(c => <option key={c} value={c}>{c}</option>)}
            </select>
          </div>
          <div>
            <label className="flbl">Açıklama <span className="text-xs" style={{ color: 'var(--text-s)' }}>(isteğe bağlı)</span></label>
            <input className="inp" value={ledgerForm.description}
              onChange={e => setLedgerForm(f => ({ ...f, description: e.target.value }))}
              placeholder="Örn: Ana EUR hesabı" />
          </div>
          {ledgerError && <p className="text-sm text-red-500">{ledgerError}</p>}
        </div>
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setAddLedgerOpen(false)}>İptal</Button>
          <Button onClick={() => addLedgerMutation.mutate()} loading={addLedgerMutation.isPending}>Ekle</Button>
        </div>
      </Modal>
    </div>
  )
}

// ── Shared form fields ─────────────────────────────────────────────────────────

function AccountFormFields({ form, setForm, groups, isCreate = false }: {
  form: CreateFormState | FormState
  setForm: (f: any) => void
  groups: AccountGroup[]
  isCreate?: boolean
}) {
  const F = form as CreateFormState

  return (
    <>
      {isCreate && (
        <div>
          <label className="flbl">Kod <span className="text-red-500">*</span></label>
          <input className="inp" value={F.code} onChange={e => setForm((f: any) => ({ ...f, code: e.target.value.toUpperCase() }))} placeholder="Örn: C-0001" />
        </div>
      )}

      <div className="grid grid-cols-2 gap-4">
        <div className="col-span-2">
          <label className="flbl">Ünvan <span className="text-red-500">*</span></label>
          <input className="inp" value={F.title} onChange={e => setForm((f: any) => ({ ...f, title: e.target.value }))} placeholder="Firma veya kişi adı" />
        </div>
        <div>
          <label className="flbl">Cari Tipi</label>
          <select className="inp" value={F.accountType} onChange={e => setForm((f: any) => ({ ...f, accountType: e.target.value }))}>
            {ACCOUNT_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </div>
        <div>
          <label className="flbl">Grup</label>
          <select className="inp" value={F.groupId} onChange={e => setForm((f: any) => ({ ...f, groupId: e.target.value }))}>
            <option value="">— Grup yok —</option>
            {groups.map(g => <option key={g.id} value={g.id}>{g.name}</option>)}
          </select>
        </div>
        {isSupplier(F.accountType) && (
          <div className="col-span-2">
            <label className="flbl">Satıcı Tipi</label>
            <select className="inp" value={F.supplierKind} onChange={e => setForm((f: any) => ({ ...f, supplierKind: e.target.value }))}>
              {SUPPLIER_KINDS.map(k => <option key={k.value} value={k.value}>{k.label}</option>)}
            </select>
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              Pazaryeri satıcısı kendi ürününü sitede satar; satıcı paneli ve API erişimi alır.
            </p>
          </div>
        )}
      </div>

      <fieldset className="space-y-3">
        <legend className="text-xs font-semibold mb-2" style={{ color: 'var(--text-s)' }}>KİMLİK BİLGİLERİ</legend>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="flbl">Vergi No / TC</label>
            <input className="inp" value={F.taxNumber} onChange={e => setForm((f: any) => ({ ...f, taxNumber: e.target.value }))} placeholder="Vergi numarası" />
          </div>
          <div>
            <label className="flbl">Vergi Dairesi</label>
            <input className="inp" value={F.taxOffice} onChange={e => setForm((f: any) => ({ ...f, taxOffice: e.target.value }))} placeholder="Vergi dairesi" />
          </div>
          <div className="col-span-2">
            <label className="flbl">İletişim Kişisi</label>
            <input className="inp" value={F.contactName} onChange={e => setForm((f: any) => ({ ...f, contactName: e.target.value }))} placeholder="Ad Soyad" />
          </div>
        </div>
      </fieldset>

      <fieldset className="space-y-3">
        <legend className="text-xs font-semibold mb-2" style={{ color: 'var(--text-s)' }}>İLETİŞİM</legend>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="flbl">Telefon</label>
            <input className="inp" value={F.phone} onChange={e => setForm((f: any) => ({ ...f, phone: e.target.value }))} placeholder="+90 555 000 00 00" />
          </div>
          <div>
            <label className="flbl">E-posta</label>
            <input className="inp" type="email" value={F.email} onChange={e => setForm((f: any) => ({ ...f, email: e.target.value }))} placeholder="ornek@firma.com" />
          </div>
          <div className="col-span-2">
            <label className="flbl">Adres</label>
            <textarea className="ta" rows={2} value={F.address} onChange={e => setForm((f: any) => ({ ...f, address: e.target.value }))} placeholder="Açık adres" />
          </div>
          <div>
            <label className="flbl">Şehir</label>
            <input className="inp" value={F.city} onChange={e => setForm((f: any) => ({ ...f, city: e.target.value }))} placeholder="İstanbul" />
          </div>
          <div>
            <label className="flbl">Ülke</label>
            <input className="inp" value={F.country} onChange={e => setForm((f: any) => ({ ...f, country: e.target.value }))} placeholder="TR" />
          </div>
        </div>
      </fieldset>

      <fieldset className="space-y-3">
        <legend className="text-xs font-semibold mb-2" style={{ color: 'var(--text-s)' }}>FİNANSAL</legend>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="flbl">Kredi Limiti</label>
            <input className="inp" type="number" min={0} value={F.creditLimit} onChange={e => setForm((f: any) => ({ ...f, creditLimit: parseFloat(e.target.value) || 0 }))} />
          </div>
          <div>
            <label className="flbl">Para Birimi</label>
            <select className="inp" value={F.currency} onChange={e => setForm((f: any) => ({ ...f, currency: e.target.value }))}>
              {['TRY', 'USD', 'EUR', 'GBP'].map(c => <option key={c} value={c}>{c}</option>)}
            </select>
          </div>
        </div>
      </fieldset>

      <div>
        <label className="flbl">Notlar</label>
        <textarea className="ta" rows={2} value={F.notes} onChange={e => setForm((f: any) => ({ ...f, notes: e.target.value }))} placeholder="İç notlar" />
      </div>

      {!isCreate && (
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={F.isActive} onChange={e => setForm((f: any) => ({ ...f, isActive: e.target.checked }))} />
          <span className="text-sm" style={{ color: 'var(--text)' }}>Aktif</span>
        </label>
      )}
    </>
  )
}

// ── Read-only display ──────────────────────────────────────────────────────────

function ReadOnlyFields({ account }: { account: AccountDetail }) {
  const fields: [string, string | null | undefined][] = [
    ...(isSupplier(account.accountType)
      ? [['Satıcı Tipi', account.supplierKind === 'marketplace' ? 'Pazaryeri satıcısı' : 'Normal tedarikçi'] as [string, string]]
      : []),
    ['İletişim Kişisi', account.contactName],
    ['Telefon', account.phone],
    ['E-posta', account.email],
    ['Adres', account.address],
    ['Şehir', account.city],
    ['Ülke', account.country],
    ['Vergi No / TC', account.taxNumber],
    ['Vergi Dairesi', account.taxOffice],
    ['Kredi Limiti', account.creditLimit > 0 ? `${account.creditLimit.toLocaleString('tr-TR')} ${account.currency}` : '—'],
    ['Notlar', account.notes],
    ['Oluşturulma', new Date(account.createdAt).toLocaleDateString('tr-TR')],
  ]

  return (
    <dl className="grid grid-cols-2 gap-x-6 gap-y-3">
      {fields.map(([label, value]) => (
        <div key={label}>
          <dt className="text-xs font-medium mb-0.5" style={{ color: 'var(--text-s)' }}>{label.toUpperCase()}</dt>
          <dd className="text-sm" style={{ color: value ? 'var(--text)' : 'var(--text-s)' }}>{value || '—'}</dd>
        </div>
      ))}
    </dl>
  )
}
