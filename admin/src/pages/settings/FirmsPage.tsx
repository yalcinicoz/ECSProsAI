import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Plus } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { DataGrid, useGridState, type GridColumn } from '@/components/grid'
import { errText } from '@/components/ui/DataTable.utils'
import { useLanguages } from '@/hooks/useLanguages'
import { FL } from '@/lib/field-labels'
import { buildI18nValues } from '@/lib/i18n-helper'

// ── Types ─────────────────────────────────────────────────────────────────────

/** DataGrid satırı — /core/firms/grid (platform sayısı da gelir). */
export interface FirmRow extends Firm {
  platformCount: number
}

interface PagedResult<T> { items: T[]; totalCount: number; page: number; pageSize: number }

export interface Firm {
  id: string
  code: string
  nameI18n: Record<string, string>
  taxOffice: string
  taxNumber: string
  address: string
  phone: string
  email: string
  isMain: boolean
  isActive: boolean
  createdAt: string
}

type FirmForm = {
  code: string
  nameI18n: Record<string, string>
  taxOffice: string
  taxNumber: string
  address: string
  phone: string
  email: string
  isMain: boolean
  isActive: boolean
}

const emptyForm = (): FirmForm => ({
  code: '', nameI18n: {}, taxOffice: '', taxNumber: '',
  address: '', phone: '', email: '',
  isMain: false, isActive: true,
})

function getFirmName(f: Firm) {
  return f.nameI18n['tr'] ?? f.nameI18n[Object.keys(f.nameI18n)[0]] ?? f.code
}

// ── Component ─────────────────────────────────────────────────────────────────

export function FirmsPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  // DataGrid (2026-09-09): sunucu filtre/sıralama/arama (FirmGrid.Schema) + Excel + görünümler.
  // ★ Ayrı uç: /core/firms TÜM firmaları döner (firma seçicileri); ekran /firms/grid kullanır.
  const [sp] = useSearchParams()
  const activeOnly = sp.get('activeOnly') === 'true'
  const grid = useGridState('firms', { defaultPageSize: 20, defaultSort: 'code', defaultDir: 'asc' })
  const [createOpen, setCreateOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<FirmRow | null>(null)
  const [form, setForm] = useState<FirmForm>(emptyForm())

  const { data, isLoading, isFetching, error: listError } = useQuery<PagedResult<FirmRow>>({
    queryKey: ['firms-grid', activeOnly, ...grid.queryKey],
    queryFn: async () =>
      (await api.get(`/core/firms/grid?${grid.toParams({ activeOnly: activeOnly ? 'true' : undefined })}`)).data.data,
    placeholderData: prev => prev,
    retry: (n, e) => (e as { response?: { status?: number } })?.response?.status === 400 ? false : n < 2,
  })
  const firms = data?.items ?? []

  const sourceLang = languages.find(l => l.isDefault)?.code ?? 'tr'
  const i18nValues = useMemo(() => buildI18nValues(form.nameI18n, languages), [form.nameI18n, languages])
  const i18nFields = useMemo(() => [{ key: 'name', labels: FL.name, required: true }], [])

  const createMutation = useMutation({
    mutationFn: async () => {
      await api.post('/core/firms', {
        code: form.code.trim().toLowerCase(),
        nameI18n: form.nameI18n,
        taxOffice: form.taxOffice,
        taxNumber: form.taxNumber,
        address: form.address,
        phone: form.phone,
        email: form.email,
        isMain: form.isMain,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['firms'] })
      setCreateOpen(false)
    },
  })

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!editTarget) return
      await api.put(`/core/firms/${editTarget.id}`, {
        nameI18n: form.nameI18n,
        taxOffice: form.taxOffice,
        taxNumber: form.taxNumber,
        address: form.address,
        phone: form.phone,
        email: form.email,
        isMain: form.isMain,
        isActive: form.isActive,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['firms'] })
      setEditTarget(null)
    },
  })

  function openCreate() {
    setForm(emptyForm())
    setCreateOpen(true)
  }

  function openEdit(f: FirmRow, e: React.MouseEvent) {
    e.stopPropagation()
    setEditTarget(f)
    setForm({
      code: f.code,
      nameI18n: { ...f.nameI18n },
      taxOffice: f.taxOffice,
      taxNumber: f.taxNumber,
      address: f.address,
      phone: f.phone,
      email: f.email,
      isMain: f.isMain,
      isActive: f.isActive,
    })
  }

  if (langsLoading) return <PageSpinner />   // liste yüklemesi DataGrid'in kendi göstergesinde

  const formBody = (isEdit: boolean) => (
    <div className="space-y-4">
      {!isEdit && (
        <div>
          <label className="flbl">Kod <span className="text-red-500">*</span></label>
          <input className="inp" value={form.code}
            onChange={e => setForm(f => ({ ...f, code: e.target.value.toLowerCase() }))}
            placeholder="Örn: main, firma-a" />
          <p className="text-xs mt-1" style={{ color: 'var(--text-s)' }}>Küçük harf, boşluksuz. Sonradan değiştirilemez.</p>
        </div>
      )}
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="flbl">Vergi Dairesi</label>
          <input className="inp" value={form.taxOffice}
            onChange={e => setForm(f => ({ ...f, taxOffice: e.target.value }))}
            placeholder="Kadıköy VD" />
        </div>
        <div>
          <label className="flbl">Vergi No</label>
          <input className="inp" value={form.taxNumber}
            onChange={e => setForm(f => ({ ...f, taxNumber: e.target.value }))}
            placeholder="1234567890" />
        </div>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="flbl">Telefon</label>
          <input className="inp" value={form.phone}
            onChange={e => setForm(f => ({ ...f, phone: e.target.value }))}
            placeholder="+90 212 000 0000" />
        </div>
        <div>
          <label className="flbl">E-posta</label>
          <input className="inp" type="email" value={form.email}
            onChange={e => setForm(f => ({ ...f, email: e.target.value }))}
            placeholder="info@firma.com" />
        </div>
      </div>
      <div>
        <label className="flbl">Adres</label>
        <textarea className="ta" rows={2} value={form.address}
          onChange={e => setForm(f => ({ ...f, address: e.target.value }))}
          placeholder="Tam adres" />
      </div>
      <div className="flex items-center gap-4">
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
            checked={form.isMain}
            onChange={e => setForm(f => ({ ...f, isMain: e.target.checked }))} />
          <span className="text-sm" style={{ color: 'var(--text)' }}>Ana firma</span>
        </label>
        {isEdit && (
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" className="w-4 h-4 rounded accent-[var(--brand)]"
              checked={form.isActive}
              onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))} />
            <span className="text-sm" style={{ color: 'var(--text)' }}>Aktif</span>
          </label>
        )}
      </div>
      <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
        <I18nField sourceLang={sourceLang} languages={languages} fields={i18nFields}
          values={i18nValues}
          onChange={(lang, _key, val) => setForm(f => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: val } }))} />
      </div>
    </div>
  )

  const columns: GridColumn<FirmRow>[] = [
    { key: 'code', header: 'KOD', priority: 1, lockVisible: true, frozen: true, sortable: true, minWidth: 120,
      filter: { type: 'text', label: 'Kod', ops: ['startswith', 'contains', 'eq'] },
      cell: f => <code className="text-xs px-2 py-0.5 rounded-md font-mono"
        style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}>{f.code}</code> },
    { key: 'name', header: 'AD', priority: 1, frozen: true, sortable: true, minWidth: 220,
      filter: { type: 'text', label: 'Ad' },
      cell: f => <span className="text-sm font-medium" style={{ color: 'var(--text)' }}>{getFirmName(f)}</span> },
    { key: 'taxNumber', header: 'VERGİ NO', priority: 1, sortable: true, filter: { type: 'text', label: 'Vergi no' },
      filters: [{ field: 'taxOffice', label: 'Vergi dairesi', type: 'text' }],
      cell: f => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{f.taxNumber || '—'}</span> },
    { key: 'phone', header: 'TELEFON', priority: 2, sortable: true, filter: { type: 'text', label: 'Telefon' },
      filters: [{ field: 'email', label: 'E-posta', type: 'text' }, { field: 'address', label: 'Adres', type: 'text' }],
      cell: f => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{f.phone || '—'}</span> },
    { key: 'platformCount', header: 'KANAL', priority: 2, align: 'center', sortable: true, filter: { type: 'number', label: 'Kanal sayısı' },
      cell: f => <span className="text-sm" style={{ color: 'var(--text-m)' }}>{f.platformCount}</span> },
    { key: 'isMain', header: 'ANA', priority: 2, align: 'center', sortable: true, filter: { type: 'boolean', label: 'Ana firma' },
      cell: f => f.isMain ? <Badge variant="warning">Ana</Badge> : <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span> },
    { key: 'isActive', header: 'DURUM', priority: 1, lockVisible: true, align: 'center', sortable: true,
      filter: { type: 'boolean', label: 'Aktif' },
      filters: [{ field: 'createdAt', label: 'Oluşturma', type: 'date' }],
      cell: f => <Badge variant={f.isActive ? 'success' : 'neutral'}>{f.isActive ? 'Aktif' : 'Pasif'}</Badge> },
    { key: 'actions', header: '', priority: 3, align: 'right', exportable: false, stopRowClick: true,
      cell: f => <button className="text-xs px-2 py-1 rounded-lg transition-colors"
        style={{ color: 'var(--brand)', background: 'var(--surface2)', border: '1px solid var(--border)' }}
        onClick={e => openEdit(f, e)}>Düzenle</button> },
  ]

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>Firmalar</h1>
          <p className="text-sm mt-0.5" style={{ color: 'var(--text-s)' }}>{(data?.totalCount ?? 0).toLocaleString('tr-TR')} kayıt{grid.activeFilterCount || grid.state.search ? ' (filtreli)' : ''}</p>
        </div>
        <Button size="sm" onClick={openCreate}><Plus size={14} /> Yeni Firma</Button>
      </div>

      <DataGrid<FirmRow>
        gridId="firms"
        views
        grid={grid}
        columns={columns}
        rows={firms}
        totalCount={data?.totalCount ?? 0}
        loading={isLoading}
        fetching={isFetching}
        error={listError ? errText(listError) : null}
        onRowClick={f => navigate(`/settings/firms/${f.id}`)}
        empty="Firma bulunamadı."
        search={{ placeholder: 'Firma kodu, adı veya VKN ara…' }}
        minWidth={940}
        export={{ endpoint: '/core/firms/export', named: () => ({ activeOnly: activeOnly ? 'true' : undefined }), fallbackFileName: 'firmalar.xlsx' }}
        compact={{
          title: f => getFirmName(f),
          subtitle: f => `${f.code}${f.taxNumber ? ` · ${f.taxNumber}` : ''}`,
          right: f => `${f.platformCount} kanal`,
          badge: f => <Badge variant={f.isActive ? 'success' : 'neutral'}>{f.isActive ? 'Aktif' : 'Pasif'}</Badge>,
        }}
      />

      {/* Create */}
      <Modal open={createOpen} onClose={() => setCreateOpen(false)} title="Yeni Firma" size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>İptal</Button>
            <Button onClick={() => createMutation.mutate()} loading={createMutation.isPending}
              disabled={!form.code || !form.nameI18n[sourceLang]}>
              Oluştur
            </Button>
          </>
        }>
        {formBody(false)}
      </Modal>

      {/* Edit */}
      <Modal open={!!editTarget} onClose={() => setEditTarget(null)} title="Firma Düzenle" size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setEditTarget(null)}>İptal</Button>
            <Button onClick={() => updateMutation.mutate()} loading={updateMutation.isPending}>Kaydet</Button>
          </>
        }>
        {formBody(true)}
      </Modal>
    </div>
  )
}
