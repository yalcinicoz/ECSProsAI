import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { PageSpinner } from '@/components/ui/Spinner'
import { cn } from '@/lib/utils'

const GROUP_TYPES = [
  { value: 'customer', label: 'Müşteri', color: 'var(--brand)' },
  { value: 'supplier', label: 'Tedarikçi', color: '#f59e0b' },
  { value: 'both',     label: 'Her İkisi', color: '#8b5cf6' },
]

export interface AccountGroup {
  id: string
  code: string
  name: string
  groupType: string
  description: string | null
  isActive: boolean
  sortOrder: number
  accountCount: number
}

type FormState = {
  code: string
  name: string
  groupType: string
  description: string
  sortOrder: number
  isActive: boolean
}

const emptyForm = (): FormState => ({
  code: '', name: '', groupType: 'customer', description: '', sortOrder: 0, isActive: true,
})

export function AccountGroupsPage() {
  const queryClient = useQueryClient()
  const [activeOnly, setActiveOnly] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<AccountGroup | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm())

  const { data: groups = [], isLoading } = useQuery<AccountGroup[]>({
    queryKey: ['account-groups', activeOnly],
    queryFn: async () => {
      const { data } = await api.get(`/accounts/groups?activeOnly=${activeOnly}`)
      return data.data
    },
  })

  const createMutation = useMutation({
    mutationFn: async () => {
      await api.post('/accounts/groups', {
        code: form.code, name: form.name, groupType: form.groupType,
        description: form.description || null, sortOrder: form.sortOrder,
      })
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['account-groups'] }); setCreateOpen(false) },
  })

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!editTarget) return
      await api.put(`/accounts/groups/${editTarget.id}`, {
        name: form.name, groupType: form.groupType,
        description: form.description || null, sortOrder: form.sortOrder, isActive: form.isActive,
      })
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['account-groups'] }); setEditTarget(null) },
  })

  function openEdit(g: AccountGroup) {
    setEditTarget(g)
    setForm({ code: g.code, name: g.name, groupType: g.groupType, description: g.description ?? '', sortOrder: g.sortOrder, isActive: g.isActive })
  }

  if (isLoading) return <PageSpinner />

  const formFields = (isEdit: boolean) => (
    <div className="space-y-4">
      {!isEdit && (
        <div>
          <label className="flbl">Kod <span className="text-red-500">*</span></label>
          <input className="inp" value={form.code} onChange={e => setForm(f => ({ ...f, code: e.target.value.toUpperCase() }))} placeholder="Örn: ONLINE-MUS" />
        </div>
      )}
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="flbl">Grup Adı <span className="text-red-500">*</span></label>
          <input className="inp" value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} placeholder="Grup adı" />
        </div>
        <div>
          <label className="flbl">Tip</label>
          <select className="inp" value={form.groupType} onChange={e => setForm(f => ({ ...f, groupType: e.target.value }))}>
            {GROUP_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </div>
      </div>
      <div>
        <label className="flbl">Açıklama <span className="text-xs" style={{ color: 'var(--text-s)' }}>(isteğe bağlı)</span></label>
        <textarea className="ta" rows={2} value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))} placeholder="Grup açıklaması" />
      </div>
      <div className="grid grid-cols-2 gap-3 items-end">
        <div>
          <label className="flbl">Sıra</label>
          <IntegerInput value={form.sortOrder} onChange={v => setForm(f => ({ ...f, sortOrder: v ?? 0 }))} />
        </div>
        {isEdit && (
          <label className="flex items-center gap-2 cursor-pointer pb-2">
            <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]" checked={form.isActive} onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))} />
            <span className="text-sm" style={{ color: 'var(--text)' }}>Aktif</span>
          </label>
        )}
      </div>
    </div>
  )

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Cari Grupları</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{groups.length} kayıt</p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1 rounded-xl p-1" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            {[false, true].map(v => (
              <button key={String(v)} onClick={() => setActiveOnly(v)}
                className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all', activeOnly === v ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
                style={activeOnly === v ? { color: 'var(--text)' } : {}}>
                {v ? 'Aktif' : 'Tümü'}
              </button>
            ))}
          </div>
          <Button size="sm" onClick={() => { setForm(emptyForm()); setCreateOpen(true) }}>+ Yeni Grup</Button>
        </div>
      </div>

      <div className="card overflow-hidden">
        <table className="w-full">
          <thead>
            <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
              {['KOD', 'AD', 'TİP', 'CARİ SAYISI', 'DURUM', ''].map(h => (
                <th key={h} className={cn('px-4 py-3 text-xs font-semibold', h === '' ? 'w-20' : 'text-left')} style={{ color: 'var(--text-s)' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {groups.length === 0 && (
              <tr><td colSpan={6} className="px-4 py-10 text-center text-sm" style={{ color: 'var(--text-s)' }}>Grup bulunamadı.</td></tr>
            )}
            {groups.map(g => {
              const typeInfo = GROUP_TYPES.find(t => t.value === g.groupType)
              return (
                <tr key={g.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td className="px-4 py-3">
                    <code className="text-xs px-2 py-0.5 rounded-md font-mono" style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>{g.code}</code>
                  </td>
                  <td className="px-4 py-3">
                    <div>
                      <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{g.name}</span>
                      {g.description && <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{g.description}</p>}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-xs font-medium px-2 py-0.5 rounded-full" style={{ color: typeInfo?.color ?? 'var(--text-m)', background: `${typeInfo?.color ?? '#888'}18` }}>
                      {typeInfo?.label ?? g.groupType}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{g.accountCount}</span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <Badge variant={g.isActive ? 'success' : 'neutral'}>{g.isActive ? 'Aktif' : 'Pasif'}</Badge>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button className="text-xs px-2 py-1 rounded-lg transition-colors"
                      style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
                      onClick={() => openEdit(g)}>Düzenle</button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <Modal open={createOpen} onClose={() => setCreateOpen(false)} title="Yeni Cari Grubu">
        {formFields(false)}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
          <Button onClick={() => createMutation.mutate()} loading={createMutation.isPending} disabled={!form.code || !form.name}>Oluştur</Button>
        </div>
      </Modal>

      <Modal open={!!editTarget} onClose={() => setEditTarget(null)} title="Grup Düzenle">
        {formFields(true)}
        <div className="flex justify-end gap-2 mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
          <Button variant="secondary" onClick={() => setEditTarget(null)}>İptal</Button>
          <Button onClick={() => updateMutation.mutate()} loading={updateMutation.isPending}>Kaydet</Button>
        </div>
      </Modal>
    </div>
  )
}
