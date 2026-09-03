import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { DataTable, Pager } from '@/components/ui/DataTable'
import { errText, tarihSaat, i18nAd } from '@/components/ui/DataTable.utils'

interface User {
  id: string
  username: string
  email: string
  firstName: string
  lastName: string
  department: string
  jobTitle?: string
  isActive: boolean
  lastLoginAt?: string
  roles: string[]
}

interface Role { id: string; code: string; nameI18n: Record<string, string>; isSystem: boolean; isActive: boolean }
interface PagedUsers { items: User[]; totalCount: number; page: number; pageSize: number; totalPages: number }

function UserModal({ user, onClose }: { user: User | 'new'; onClose: () => void }) {
  const queryClient = useQueryClient()
  const isNew = user === 'new'
  const u = isNew ? undefined : user

  const [username, setUsername] = useState(u?.username ?? '')
  const [email, setEmail] = useState(u?.email ?? '')
  const [password, setPassword] = useState('')
  const [firstName, setFirstName] = useState(u?.firstName ?? '')
  const [lastName, setLastName] = useState(u?.lastName ?? '')
  const [department, setDepartment] = useState(u?.department ?? '')
  const [jobTitle, setJobTitle] = useState(u?.jobTitle ?? '')
  const [isActive, setIsActive] = useState(u?.isActive ?? true)
  const [roleId, setRoleId] = useState('')
  const [error, setError] = useState('')
  const [bilgi, setBilgi] = useState('')

  const { data: roles } = useQuery<Role[]>({
    queryKey: ['roles-select'],
    queryFn: async () => (await api.get('/iam/roles')).data.data,
  })

  const save = useMutation({
    mutationFn: async () => {
      setError(''); setBilgi('')
      if (isNew) {
        await api.post('/iam/users', {
          username: username.trim(), email: email.trim(), password,
          firstName: firstName.trim(), lastName: lastName.trim(),
          department: department.trim(), jobTitle: jobTitle.trim() || null,
          phone: null, mustChangePassword: true,
        })
      } else {
        await api.put(`/iam/users/${u!.id}`, {
          firstName: firstName.trim(), lastName: lastName.trim(),
          department: department.trim(), jobTitle: jobTitle.trim() || null,
          phone: null, isActive,
        })
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['iam-users'] })
      onClose()
    },
    onError: (e: unknown) => setError(errText(e)),
  })

  const sifreSifirla = useMutation({
    mutationFn: async () => {
      setError(''); setBilgi('')
      const yeni = window.prompt('Yeni şifre (kullanıcı ilk girişte değiştirecek):')
      if (!yeni) throw new Error('iptal')
      await api.post(`/iam/users/${u!.id}/reset-password`, { newPassword: yeni })
      return yeni
    },
    onSuccess: () => setBilgi('Şifre sıfırlandı.'),
    onError: (e: unknown) => { if ((e as Error).message !== 'iptal') setError(errText(e)) },
  })

  const rolAta = useMutation({
    mutationFn: async () => {
      setError(''); setBilgi('')
      await api.post(`/iam/users/${u!.id}/roles`, { roleId })
    },
    onSuccess: () => {
      setBilgi('Rol atandı.')
      queryClient.invalidateQueries({ queryKey: ['iam-users'] })
    },
    onError: (e: unknown) => setError(errText(e)),
  })

  const valid = isNew
    ? username.trim().length >= 3 && email.includes('@') && password.length >= 8 && firstName.trim() && lastName.trim()
    : Boolean(firstName.trim() && lastName.trim())

  return (
    <Modal open onClose={onClose} title={isNew ? 'Yeni Kullanıcı' : `Kullanıcı: ${u?.username}`}>
      <div className="space-y-3">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Kullanıcı Adı <span className="text-red-500">*</span></label>
            <input className="inp font-mono" value={username} disabled={!isNew}
              onChange={e => setUsername(e.target.value)} />
          </div>
          <div>
            <label className="flbl">E-posta <span className="text-red-500">*</span></label>
            <input className="inp" type="email" value={email} disabled={!isNew}
              onChange={e => setEmail(e.target.value)} />
          </div>
        </div>
        {isNew && (
          <div>
            <label className="flbl">Geçici Şifre <span className="text-red-500">*</span> <span className="text-xs" style={{ color: 'var(--text-s)' }}>(en az 8 karakter; ilk girişte değiştirtilir)</span></label>
            <input className="inp" type="password" value={password} onChange={e => setPassword(e.target.value)} />
          </div>
        )}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Ad <span className="text-red-500">*</span></label>
            <input className="inp" value={firstName} onChange={e => setFirstName(e.target.value)} />
          </div>
          <div>
            <label className="flbl">Soyad <span className="text-red-500">*</span></label>
            <input className="inp" value={lastName} onChange={e => setLastName(e.target.value)} />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Departman</label>
            <input className="inp" value={department} onChange={e => setDepartment(e.target.value)} />
          </div>
          <div>
            <label className="flbl">Ünvan</label>
            <input className="inp" value={jobTitle} onChange={e => setJobTitle(e.target.value)} />
          </div>
        </div>
        {!isNew && (
          <>
            <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} />
              Aktif
            </label>
            <div className="flex items-end gap-2 pt-2" style={{ borderTop: '1px solid var(--border)' }}>
              <div className="flex-1">
                <label className="flbl">Rol Ata <span className="text-xs" style={{ color: 'var(--text-s)' }}>(mevcut: {u!.roles.join(', ') || '—'})</span></label>
                <select className="inp" value={roleId} onChange={e => setRoleId(e.target.value)}>
                  <option value="">Seçin…</option>
                  {(roles ?? []).map(r => <option key={r.id} value={r.id}>{i18nAd(r.nameI18n)}</option>)}
                </select>
              </div>
              <Button variant="secondary" size="sm" disabled={!roleId} loading={rolAta.isPending}
                onClick={() => rolAta.mutate()}>Ata</Button>
              <Button variant="secondary" size="sm" loading={sifreSifirla.isPending}
                onClick={() => sifreSifirla.mutate()}>Şifre Sıfırla</Button>
            </div>
          </>
        )}
        {error && <p className="text-sm text-red-500">{error}</p>}
        {bilgi && <p className="text-sm text-green-600">{bilgi}</p>}
      </div>
      <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
        <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
        <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!valid}>Kaydet</Button>
      </div>
    </Modal>
  )
}

export function UsersPage() {
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<User | 'new' | null>(null)

  const { data, isLoading } = useQuery<PagedUsers>({
    queryKey: ['iam-users', appliedSearch, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (appliedSearch) params.set('search', appliedSearch)
      return (await api.get(`/iam/users?${params}`)).data.data
    },
  })

  const users = data?.items ?? []
  const totalPages = data?.totalPages ?? Math.ceil((data?.totalCount ?? 0) / 20)

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kullanıcılar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{data?.totalCount ?? 0} kayıt</p>
        </div>
        <Button size="sm" onClick={() => setEditing('new')}>+ Yeni Kullanıcı</Button>
      </div>

      <div className="flex items-center gap-2 mb-4">
        <input className="inp text-sm py-1.5 px-3 h-auto" style={{ minWidth: 220 }}
          placeholder="Ad, e-posta, kullanıcı adı ara…" value={search}
          onChange={e => setSearch(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter') { setAppliedSearch(search.trim()); setPage(1) } }} />
        <button onClick={() => { setAppliedSearch(search.trim()); setPage(1) }}
          className="px-3 py-1.5 rounded-lg text-sm"
          style={{ border: '1px solid var(--border)', color: 'var(--text)' }}>Ara</button>
      </div>

      <DataTable<User>
        columns={[
          { header: 'KULLANICI ADI', cell: u => <code className="text-xs font-mono font-medium">{u.username}</code> },
          { header: 'AD SOYAD', cell: u => `${u.firstName} ${u.lastName}` },
          { header: 'E-POSTA', cell: u => u.email },
          { header: 'ROLLER', cell: u => (u.roles.length ? u.roles.join(', ') : '—') },
          { header: 'SON GİRİŞ', cell: u => tarihSaat(u.lastLoginAt) },
          { header: 'DURUM', cell: u => <Badge variant={u.isActive ? 'success' : 'neutral'}>{u.isActive ? 'Aktif' : 'Pasif'}</Badge> },
          { header: '', className: 'text-right', cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span> },
        ]}
        rows={users}
        loading={isLoading}
        empty="Kullanıcı yok."
        onRowClick={u => setEditing(u)}
      />

      <Pager page={page} totalPages={totalPages} onChange={setPage} />
      {editing !== null && <UserModal user={editing} onClose={() => setEditing(null)} />}
    </div>
  )
}
