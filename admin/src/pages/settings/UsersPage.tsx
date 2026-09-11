import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { useAuthStore } from '@/store/auth'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Modal } from '@/components/ui/Modal'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText, tarihSaat, i18nAd } from '@/components/ui/DataTable.utils'

interface User {
  id: string
  username: string
  email: string
  firstName: string
  lastName: string
  department: string
  jobTitle?: string
  phone?: string | null
  isActive: boolean
  lastLoginAt?: string
  /** Yetki grubu kodları (iam_roles) — yönetimi /settings/users/:id/permissions ekranında. */
  roles: string[]
  /** Rol (K5 sistem bayrağı): true → Süper Admin, false → Çalışan. Yetki gruplarından bağımsızdır. */
  isSuperAdmin: boolean
}

// Rol yalnız iki değer: Süper Admin (bayrak; tüm yetkiler, tüm kanallar) / Çalışan (yetkileri gruplardan alır).
const ROL_SECENEKLERI = [
  { value: 'calisan',     label: 'Çalışan' },
  { value: 'super_admin', label: 'Süper Admin' },
]

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
  const [phone, setPhone] = useState(u?.phone ?? '')
  const [isActive, setIsActive] = useState(u?.isActive ?? true)
  const [superAdmin, setSuperAdmin] = useState(u?.isSuperAdmin ?? false)
  const [error, setError] = useState('')
  const [bilgi, setBilgi] = useState('')
  const navigate = useNavigate()
  // Süper adminliği yalnız süper admin verebilir/kaldırabilir (sunucu kuralı; K5) — seçici diğerlerine kilitli.
  const benSuperAdmin = useAuthStore(s => s.user?.isSuperAdmin === true)
  const kendim = useAuthStore(s => s.user?.id) === u?.id

  const save = useMutation({
    mutationFn: async () => {
      setError(''); setBilgi('')
      let id = u?.id
      if (isNew) {
        const { data } = await api.post('/iam/users', {
          username: username.trim(), email: email.trim(), password,
          firstName: firstName.trim(), lastName: lastName.trim(),
          department: department.trim(), jobTitle: jobTitle.trim() || null,
          phone: phone.trim() || null, mustChangePassword: true,
        })
        id = data.data.id as string
      } else {
        await api.put(`/iam/users/${u!.id}`, {
          firstName: firstName.trim(), lastName: lastName.trim(),
          department: department.trim(), jobTitle: jobTitle.trim() || null,
          phone: phone.trim() || null, isActive,
        })
      }
      // Rol = süper admin bayrağı; yalnız değiştiyse ayrı uca gider (kendi denetim kaydı vardır).
      const eski = u?.isSuperAdmin ?? false
      if (superAdmin !== eski) await api.put(`/iam/users/${id}/super-admin`, { deger: superAdmin })
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
        <div>
          <label className="flbl">Telefon</label>
          <input className="inp" type="tel" value={phone} onChange={e => setPhone(e.target.value)} placeholder="05xx xxx xx xx" />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="flbl">Rol</label>
            <select className="inp" value={superAdmin ? 'super_admin' : 'calisan'} disabled={!benSuperAdmin || kendim}
              onChange={e => setSuperAdmin(e.target.value === 'super_admin')}>
              {ROL_SECENEKLERI.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
            </select>
            <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>
              {kendim ? 'Kendi rolünüzü değiştiremezsiniz.'
                : !benSuperAdmin ? 'Rolü yalnız süper admin değiştirebilir.'
                : superAdmin ? 'Tüm yetkiler ve tüm kanallar; yetki grupları uygulanmaz.'
                : 'Yetkilerini yetki gruplarından alır.'}
            </p>
          </div>
          {!isNew && (
            <div>
              <label className="flbl">Yetki Grupları</label>
              <p className="text-sm py-2" style={{ color: 'var(--text)' }}>{u!.roles.join(', ') || '—'}</p>
              <button type="button" className="text-xs underline" style={{ color: 'var(--brand)' }}
                onClick={() => { onClose(); navigate(`/settings/users/${u!.id}/permissions`) }}>Yetkileri düzenle →</button>
            </div>
          )}
        </div>
        {!isNew && (
          <div className="flex items-center justify-between gap-2 pt-2" style={{ borderTop: '1px solid var(--border)' }}>
            <label className="flex items-center gap-2 text-sm" style={{ color: 'var(--text)' }}>
              <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} />
              Aktif
            </label>
            <Button variant="secondary" size="sm" loading={sifreSifirla.isPending}
              onClick={() => sifreSifirla.mutate()}>Şifre Sıfırla</Button>
          </div>
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
  // DataGrid (2026-09-08): sunucu taraflı filtre/sıralama/global arama (UserGrid.Schema) + Excel export; her sütunda başlık filtresi.
  const navigate = useNavigate()
  const grid = useGridState('users', { defaultPageSize: 20, defaultSort: 'username', defaultDir: 'asc' })
  const [editing, setEditing] = useState<User | 'new' | null>(null)

  const { data, isLoading, isFetching, error } = useQuery<PagedUsers>({
    queryKey: ['iam-users', ...grid.queryKey],
    queryFn: async () => (await api.get(`/iam/users?${grid.toParams()}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const { data: roles } = useQuery<Role[]>({
    queryKey: ['roles-select'],
    queryFn: async () => (await api.get('/iam/roles')).data.data,
  })
  const roleOptions = useMemo(() => (roles ?? []).map(r => ({ value: r.code, label: i18nAd(r.nameI18n) || r.code })), [roles])

  const users = data?.items ?? []
  const columns: GridColumn<User>[] = [
    { key: 'username', header: 'KULLANICI ADI', frozen: true, lockVisible: true, sortable: true, filter: { type: 'text', label: 'Kullanıcı adı' },
      cell: u => <code className="text-xs font-mono font-medium">{u.username}</code> },
    { key: 'name', header: 'AD SOYAD', priority: 1, sortable: true, filter: { type: 'text', label: 'Ad', field: 'firstName' },
      filters: [{ field: 'lastName', label: 'Soyad', type: 'text' }],
      cell: u => `${u.firstName} ${u.lastName}` },
    { key: 'email', header: 'E-POSTA', priority: 2, sortable: true, filter: { type: 'text', label: 'E-posta' }, cell: u => u.email },
    { key: 'phone', header: 'TELEFON', priority: 3, sortable: true, filter: { type: 'text', label: 'Telefon', ops: ['contains', 'startswith'] }, cell: u => u.phone || '—' },
    { key: 'department', header: 'DEPARTMAN', priority: 3, defaultVisible: false, sortable: true, filter: { type: 'text', label: 'Departman' }, cell: u => u.department || '—' },
    { key: 'jobTitle', header: 'ÜNVAN', priority: 3, defaultVisible: false, sortable: true, filter: { type: 'text', label: 'Ünvan' }, cell: u => u.jobTitle || '—' },
    { key: 'isSuperAdmin', header: 'ROL', priority: 2, sortable: true, filter: { type: 'boolean', label: 'Süper admin' },
      cell: u => u.isSuperAdmin ? <Badge variant="info">Süper Admin</Badge> : <span className="text-sm">Çalışan</span> },
    { key: 'roles', header: 'YETKİ GRUPLARI', priority: 3, filter: { type: 'enum', multiple: true, label: 'Yetki grubu', field: 'role', options: roleOptions },
      cell: u => (u.roles.length ? u.roles.join(', ') : '—') },
    { key: 'lastLoginAt', header: 'SON GİRİŞ', priority: 3, sortable: true, filter: { type: 'date', label: 'Son giriş' }, cell: u => tarihSaat(u.lastLoginAt) },
    { key: 'isActive', header: 'DURUM', priority: 1, sortable: true, filter: { type: 'boolean', label: 'Aktif', quick: true },
      cell: u => <Badge variant={u.isActive ? 'success' : 'neutral'}>{u.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    // Y4 (tasarım §19): destek sorularının çoğu "şunu neden göremiyor?" olduğundan
    // kullanıcı listesinden doğrudan yetki ekranına kısayol.
    { key: 'permissions', header: '', priority: 3, align: 'right', exportable: false, cell: u => (
      <button className="text-xs underline" style={{ color: 'var(--brand)' }}
        onClick={e => { e.stopPropagation(); navigate(`/settings/users/${u.id}/permissions`) }}>Yetkileri</button>
    ) },
    { key: 'edit', header: '', priority: 3, align: 'right', exportable: false, cell: () => <span className="text-xs" style={{ color: 'var(--text-s)' }}>Düzenle →</span> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Kullanıcılar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}</p>
        </div>
        <Button size="sm" onClick={() => setEditing('new')}>+ Yeni Kullanıcı</Button>
      </div>

      <DataGrid<User>
        gridId="users"
        views
        grid={grid}
        columns={columns}
        rows={users}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={error ? errText(error) : null}
        onRowClick={u => setEditing(u)}
        empty="Kullanıcı yok."
        search={{ placeholder: 'Ad, e-posta, kullanıcı adı, telefon ara…' }}
        export={{ endpoint: '/iam/users/export', fallbackFileName: 'kullanicilar.xlsx' }}
        compact={{
          title: u => `${u.firstName} ${u.lastName}`.trim() || u.username,
          subtitle: u => `${u.email} · ${u.department}`,
          right: u => u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString('tr-TR') : 'Hiç girmedi',
          badge: u => <Badge variant={u.isActive ? 'success' : 'neutral'}>{u.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />

      {editing !== null && <UserModal user={editing} onClose={() => setEditing(null)} />}
    </div>
  )
}
