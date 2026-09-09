import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
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

/** DataGrid satırı — /accounts/groups/grid (createdAt de gelir). */
export interface AccountGroupRow extends AccountGroup {
  createdAt: string
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

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
  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (AccountGroupGrid.Schema) + Excel + görünümler.
  // ★ Ayrı uç: /accounts/groups TÜM grupları döner (cari listesinin süzgeci); ekran /groups/grid kullanır.
  const [sp] = useSearchParams()
  const activeOnly = sp.get('activeOnly') === 'true'
  const grid = useGridState('account-groups', { defaultPageSize: 20, defaultSort: 'sortOrder', defaultDir: 'asc' })
  const [createOpen, setCreateOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<AccountGroupRow | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm())

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<AccountGroupRow>>({
    queryKey: ['account-groups-grid', activeOnly, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/accounts/groups/grid?${grid.toParams({ activeOnly: activeOnly ? 'true' : undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const groups = data?.items ?? []

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

  function openEdit(g: AccountGroupRow) {
    setEditTarget(g)
    setForm({ code: g.code, name: g.name, groupType: g.groupType, description: g.description ?? '', sortOrder: g.sortOrder, isActive: g.isActive })
  }

  // Liste yüklemesi DataGrid'in kendi göstergesinde; sayfa iskeleti hemen çizilir.

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

  const columns: GridColumn<AccountGroupRow>[] = [
    { key: 'code', header: 'KOD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 120,
      filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      cell: g => <code className="text-xs px-2 py-0.5 rounded-md font-mono"
        style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>{g.code}</code> },
    { key: 'name', header: 'AD', priority: 1, frozen: true, sortable: true, minWidth: 220,
      filter: { type: 'text', label: 'Ad' },
      filters: [{ field: 'description', label: 'Açıklama', type: 'text' }],
      cell: g => <div>
        <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{g.name}</span>
        {g.description && <p className="text-xs mt-0.5" style={{ color: 'var(--text-s)' }}>{g.description}</p>}
      </div> },
    { key: 'groupType', header: 'TİP', priority: 1, sortable: true,
      filter: { type: 'enum', multiple: true, label: 'Tip', options: GROUP_TYPES.map(t => ({ value: t.value, label: t.label })) },
      cell: g => { const t = GROUP_TYPES.find(x => x.value === g.groupType); return <span className="text-xs font-medium px-2 py-0.5 rounded-full"
        style={{ color: t?.color ?? 'var(--text-m)', background: `${t?.color ?? '#888'}18` }}>{t?.label ?? g.groupType}</span> } },
    { key: 'accountCount', header: 'CARİ SAYISI', priority: 1, align: 'center', sortable: true,
      filter: { type: 'number', label: 'Cari sayısı' },
      filters: [{ field: 'hasAccounts', label: 'Carisi olan', type: 'boolean' }],
      cell: g => <span className="text-sm font-semibold" style={{ color: 'var(--text)' }}>{g.accountCount}</span> },
    { key: 'sortOrder', header: 'SIRA', priority: 3, align: 'center', sortable: true, filter: { type: 'number', label: 'Sıra' },
      cell: g => <span className="text-sm" style={{ color: 'var(--text-s)' }}>{g.sortOrder}</span> },
    { key: 'isActive', header: 'DURUM', priority: 1, lockVisible: true, align: 'center', sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [{ field: 'createdAt', label: 'Oluşturma', type: 'date' }],
      cell: g => <Badge variant={g.isActive ? 'success' : 'neutral'}>{g.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'actions', header: '', priority: 3, align: 'right', exportable: false, stopRowClick: true,
      cell: g => <button className="text-xs px-2 py-1 rounded-lg transition-colors"
        style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
        onClick={() => openEdit(g)}>Düzenle</button> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Cari Grupları</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}</p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1 rounded-xl p-1" style={{ background: 'var(--surface2)', border: '1px solid var(--border)' }}>
            {[false, true].map(v => (
              <button key={String(v)} onClick={() => grid.mutate(n => { if (v) n.set('activeOnly', 'true'); else n.delete('activeOnly') })}
                className={cn('px-3 py-1 rounded-lg text-sm font-medium transition-all', activeOnly === v ? 'bg-white shadow-sm' : 'text-[var(--text-s)]')}
                style={activeOnly === v ? { color: 'var(--text)' } : {}}>
                {v ? 'Aktif' : 'Tümü'}
              </button>
            ))}
          </div>
          <Button size="sm" onClick={() => { setForm(emptyForm()); setCreateOpen(true) }}>+ Yeni Grup</Button>
        </div>
      </div>

      <DataGrid<AccountGroupRow>
        gridId="account-groups"
        views
        grid={grid}
        columns={columns}
        rows={groups}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        empty="Grup bulunamadı."
        search={{ placeholder: 'Grup kodu veya adıyla ara…' }}
        minWidth={820}
        export={{ endpoint: '/accounts/groups/export', named: () => ({ activeOnly: activeOnly ? 'true' : undefined }), fallbackFileName: 'cari-gruplari.xlsx' }}
        compact={{
          title: g => g.name,
          subtitle: g => `${g.code} · ${GROUP_TYPES.find(t => t.value === g.groupType)?.label ?? g.groupType}`,
          right: g => `${g.accountCount} cari`,
          badge: g => <Badge variant={g.isActive ? 'success' : 'neutral'}>{g.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />

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
