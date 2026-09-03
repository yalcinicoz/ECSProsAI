import { useState, useMemo } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { Plus, ChevronRight, ArrowLeft, Pencil, Trash2 } from 'lucide-react'
import api from '@/api/client'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Modal } from '@/components/ui/Modal'
import { IntegerInput } from '@/components/ui/IntegerInput'
import { I18nField } from '@/components/ui/I18nField'
import { PageSpinner } from '@/components/ui/Spinner'
import { PermissionGuard, ReadOnlyBadge } from '@/components/ui/PermissionGuard'
import { useAuthStore } from '@/store/auth'
import { useLanguages } from '@/hooks/useLanguages'
import { buildI18nValues } from '@/lib/i18n-helper'

const PLATFORM_PERM = 'catalog.platform.manage'
import { FL } from '@/lib/field-labels'

const DATA_TYPE_OPTIONS = [
  { value: 'select',       label: 'Seçim Listesi' },
  { value: 'multi_select', label: 'Çoklu Seçim' },
  { value: 'text',         label: 'Metin' },
  { value: 'number',       label: 'Sayı' },
  { value: 'boolean',      label: 'Evet/Hayır' },
]

const DATA_TYPE_LABELS: Record<string, string> = {
  select:       'Seçim Listesi',
  multi_select: 'Çoklu Seçim',
  text:         'Metin',
  number:       'Sayı',
  boolean:      'Evet/Hayır',
}

type ApiError = { response?: { data?: { error?: string } } }

// ── Types ────────────────────────────────────────────────────────────────────

interface AttributeValue {
  id: string
  nameI18n: Record<string, string>
  isActive: boolean
  sortOrder: number
  usedInProductCount: number
  hexCode: string | null
}

interface ProductByValue {
  id: string
  code: string
  nameI18n: Record<string, string>
  isActive: boolean
  usageType: 'direct' | 'variant'
}

interface AttributeType {
  id: string
  code: string
  nameI18n: Record<string, string>
  dataType: string
  isActive: boolean
  sortOrder: number
  useInFilter: boolean
  values: AttributeValue[]
}

// ── Component ─────────────────────────────────────────────────────────────────

export function AttributeTypeDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { data: languages = [], isLoading: langsLoading } = useLanguages()

  const hasPermission = useAuthStore(s => s.hasPermission)
  const canEdit = hasPermission(PLATFORM_PERM)

  const [settingsForm, setSettingsForm] = useState<{
    nameI18n: Record<string, string>
    dataType: string
    sortOrder: number
    isActive: boolean
    useInFilter: boolean
  }>({ nameI18n: {}, dataType: 'select', sortOrder: 0, isActive: true, useInFilter: false })
  const [settingsDirty, setSettingsDirty] = useState(false)
  const [settingsSaved, setSettingsSaved] = useState(false)
  const [initializedAttrTypeId, setInitializedAttrTypeId] = useState<string | null>(null)

  const [addValueOpen, setAddValueOpen] = useState(false)
  const [valueForm, setValueForm] = useState<{
    nameI18n: Record<string, string>
    sortOrder: number
    hexCode: string
  }>({ nameI18n: {}, sortOrder: 0, hexCode: '' })

  const [editValueOpen, setEditValueOpen] = useState(false)
  const [editingValue, setEditingValue] = useState<AttributeValue | null>(null)
  const [editValueForm, setEditValueForm] = useState<{
    nameI18n: Record<string, string>
    sortOrder: number
    isActive: boolean
  }>({ nameI18n: {}, sortOrder: 0, isActive: true })
  const [editValueHexCode, setEditValueHexCode] = useState('')

  const [usageValue, setUsageValue] = useState<AttributeValue | null>(null)
  const [deleteValueId, setDeleteValueId] = useState<string | null>(null)

  const { data: attrTypes = [], isLoading } = useQuery<AttributeType[]>({
    queryKey: ['attribute-types', false],
    queryFn: async () => {
      const { data } = await api.get('/catalog/attribute-types?activeOnly=false')
      return data.data
    },
  })


  const attrType = attrTypes.find((a) => a.id === id)
  const deleteValueName = attrType?.values.find((v) => v.id === deleteValueId)?.nameI18n

  // Route aynı component örneğini kullansa bile formu yalnız yeni özellik tipi için başlat.
  // Aynı ID için yapılan sorgu yenilemeleri, kullanıcının kaydedilmemiş formunu ezmez.
  if (attrType && initializedAttrTypeId !== attrType.id) {
    setInitializedAttrTypeId(attrType.id)
    setSettingsForm({
      nameI18n: attrType.nameI18n,
      dataType: attrType.dataType,
      sortOrder: attrType.sortOrder,
      isActive: attrType.isActive,
      useInFilter: attrType.useInFilter,
    })
    setSettingsDirty(false)
  } else if (!attrType && initializedAttrTypeId !== null) {
    setInitializedAttrTypeId(null)
  }

  const { data: usageProducts = [], isLoading: usageLoading } = useQuery<ProductByValue[]>({
    queryKey: ['attribute-value-products', usageValue?.id],
    enabled: !!usageValue,
    queryFn: async () => {
      const { data } = await api.get(`/catalog/attribute-values/${usageValue!.id}/products`)
      return data.data as ProductByValue[]
    },
  })

  const addValueMutation = useMutation({
    mutationFn: async () => {
      await api.post(`/catalog/attribute-types/${id}/values`, {
        valueI18n: valueForm.nameI18n,
        sortOrder: valueForm.sortOrder,
        hexCode: valueForm.hexCode || null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['attribute-types'] })
      setAddValueOpen(false)
    },
  })

  const updateValueMutation = useMutation({
    mutationFn: async () => {
      if (!editingValue) return
      await api.put(`/catalog/attribute-values/${editingValue.id}`, {
        nameI18n: editValueForm.nameI18n,
        sortOrder: editValueForm.sortOrder,
        isActive: editValueForm.isActive,
        hexCode: editValueHexCode || null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['attribute-types'] })
      setEditValueOpen(false)
      setEditingValue(null)
    },
  })

  const updateSettingsMutation = useMutation({
    mutationFn: async () => {
      if (!attrType) return
      await api.put(`/catalog/attribute-types/${id}`, {
        nameI18n: settingsForm.nameI18n,
        dataType: settingsForm.dataType,
        sortOrder: settingsForm.sortOrder,
        isActive: settingsForm.isActive,
        useInFilter: settingsForm.useInFilter,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['attribute-types'] })
      setSettingsDirty(false)
      setSettingsSaved(true)
      setTimeout(() => setSettingsSaved(false), 2500)
    },
  })

  const deleteValueMutation = useMutation({
    mutationFn: async (valueId: string) => {
      await api.delete(`/catalog/attribute-values/${valueId}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['attribute-types'] })
      setDeleteValueId(null)
    },
  })


  const sourceLang = languages.find((l) => l.isDefault)?.code ?? languages[0]?.code ?? 'tr'

  function markDirty() { setSettingsDirty(true); setSettingsSaved(false) }

  const duplicateLang = useMemo(() => {
    if (!attrType) return null
    for (const [lang, val] of Object.entries(valueForm.nameI18n)) {
      const trimmed = val.trim().toUpperCase()
      if (!trimmed) continue
      const exists = attrType.values.some(
        (v) => (v.nameI18n[lang] ?? '').trim().toUpperCase() === trimmed,
      )
      if (exists) return { lang, val: val.trim() }
    }
    return null
  }, [attrType, valueForm.nameI18n])

  const valueI18nValues = useMemo(
    () => buildI18nValues(valueForm.nameI18n, languages),
    [languages, valueForm.nameI18n],
  )

  const editValueI18nValues = useMemo(
    () => buildI18nValues(editValueForm.nameI18n, languages),
    [languages, editValueForm.nameI18n],
  )

  const i18nFields = useMemo(
    () => [{ key: 'name', labels: FL.name, required: true }],
    [],
  )

  const typeI18nValues = useMemo(
    () => buildI18nValues(settingsForm.nameI18n, languages),
    [settingsForm.nameI18n, languages],
  )

  function getValueName(v: AttributeValue): string {
    return v.nameI18n['tr'] ?? v.nameI18n[Object.keys(v.nameI18n)[0]] ?? '—'
  }

  function openAddValue() {
    setValueForm({ nameI18n: {}, sortOrder: (attrType?.values.length ?? 0) * 10, hexCode: '' })
    addValueMutation.reset()
    setAddValueOpen(true)
  }

  function openEditValue(v: AttributeValue) {
    setEditingValue(v)
    setEditValueForm({ nameI18n: { ...v.nameI18n }, sortOrder: v.sortOrder, isActive: v.isActive })
    setEditValueHexCode(v.hexCode ?? '')
    updateValueMutation.reset()
    setEditValueOpen(true)
  }

  if (isLoading || langsLoading) return <PageSpinner />

  if (!attrType) {
    return (
      <div className="p-6">
        <div className="flex flex-col items-center justify-center py-24 gap-4">
          <p className="text-sm" style={{ color: 'var(--text-s)' }}>Özellik tipi bulunamadı.</p>
          <Button variant="secondary" onClick={() => navigate('/catalog/attribute-types')}>
            <ArrowLeft size={14} /> Geri Dön
          </Button>
        </div>
      </div>
    )
  }

  const isSelectType = attrType.dataType === 'select' || attrType.dataType === 'multi_select'

  return (
    <div className="p-6 space-y-6">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-1 text-sm" style={{ color: 'var(--text-s)' }}>
        <Link
          to="/catalog/attribute-types"
          className="hover:text-[var(--text)] transition-colors"
        >
          Özellik Tipleri
        </Link>
        <ChevronRight size={12} />
        <span style={{ color: 'var(--text)' }}>{attrType.code}</span>
      </nav>

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-bold" style={{ color: 'var(--text)' }}>
              {attrType.nameI18n['tr'] ?? attrType.code}
            </h1>
            <PermissionGuard permission={PLATFORM_PERM} fallback={<ReadOnlyBadge />} />
          </div>
          <div className="flex items-center gap-2 mt-1">
            <code
              className="text-xs px-2 py-0.5 rounded-md font-mono"
              style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}
            >
              {attrType.code}
            </code>
            <Badge variant="info">{DATA_TYPE_LABELS[settingsForm.dataType] ?? settingsForm.dataType}</Badge>
            <Badge variant={settingsForm.isActive ? 'success' : 'neutral'}>
              {settingsForm.isActive ? 'Aktif' : 'Pasif'}
            </Badge>
            <span className="text-xs" style={{ color: 'var(--text-s)' }}>{attrType.values.length} değer</span>
          </div>
        </div>
      </div>

      {/* Settings card */}
      <div className="card p-0 overflow-hidden">
        <div className="flex items-center justify-between px-5 py-3 border-b" style={{ borderColor: 'var(--border)' }}>
          <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>Özellik Ayarları</h2>
          {canEdit && (
            <div className="flex items-center gap-2">
              {settingsSaved && <span className="text-xs" style={{ color: 'var(--brand)' }}>Kaydedildi</span>}
              {updateSettingsMutation.isError && (
                <span className="text-xs" style={{ color: '#ef4444' }}>
                  {(updateSettingsMutation.error as ApiError)?.response?.data?.error ?? 'Hata oluştu'}
                </span>
              )}
              <Button
                size="sm"
                onClick={() => updateSettingsMutation.mutate()}
                loading={updateSettingsMutation.isPending}
                disabled={!settingsDirty}
              >
                Kaydet
              </Button>
            </div>
          )}
        </div>

        <div className="p-5 space-y-5">
          {/* Veri Tipi + Sıra + Filtre + Durum */}
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div>
              <label className="flbl">Veri Tipi</label>
              {canEdit ? (
                <select
                  className="finput mt-1"
                  value={settingsForm.dataType}
                  onChange={e => { setSettingsForm(f => ({ ...f, dataType: e.target.value })); markDirty() }}
                >
                  {DATA_TYPE_OPTIONS.map(opt => (
                    <option key={opt.value} value={opt.value}>{opt.label}</option>
                  ))}
                </select>
              ) : (
                <div className="mt-1">
                  <Badge variant="info">{DATA_TYPE_LABELS[settingsForm.dataType] ?? settingsForm.dataType}</Badge>
                </div>
              )}
            </div>
            <div>
              <label className="flbl">Sıra</label>
              {canEdit ? (
                <IntegerInput
                  value={settingsForm.sortOrder}
                  onChange={v => { setSettingsForm(f => ({ ...f, sortOrder: v ?? 0 })); markDirty() }}
                />
              ) : (
                <p className="mt-1 text-sm" style={{ color: 'var(--text)' }}>{settingsForm.sortOrder}</p>
              )}
            </div>
            <div>
              <label className="flbl">Filtre</label>
              {canEdit ? (
                <label className="flex items-center gap-2 mt-2 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    className="w-4 h-4 rounded accent-[var(--brand)]"
                    checked={settingsForm.useInFilter}
                    onChange={e => { setSettingsForm(f => ({ ...f, useInFilter: e.target.checked })); markDirty() }}
                  />
                  <span className="text-sm" style={{ color: 'var(--text-m)' }}>Filtrede kullanılsın</span>
                </label>
              ) : (
                <div className="mt-1">
                  <Badge variant={settingsForm.useInFilter ? 'success' : 'neutral'}>
                    {settingsForm.useInFilter ? 'Filtrede' : 'Filtre dışı'}
                  </Badge>
                </div>
              )}
            </div>
            <div>
              <label className="flbl">Durum</label>
              {canEdit ? (
                <label className="flex items-center gap-2 mt-2 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    className="w-4 h-4 rounded accent-[var(--brand)]"
                    checked={settingsForm.isActive}
                    onChange={e => { setSettingsForm(f => ({ ...f, isActive: e.target.checked })); markDirty() }}
                  />
                  <span className="text-sm" style={{ color: 'var(--text-m)' }}>Aktif</span>
                </label>
              ) : (
                <div className="mt-1">
                  <Badge variant={settingsForm.isActive ? 'success' : 'neutral'}>
                    {settingsForm.isActive ? 'Aktif' : 'Pasif'}
                  </Badge>
                </div>
              )}
            </div>
          </div>

          {/* Ad çevirileri */}
          {languages.length > 0 && (
            <div>
              <label className="flbl mb-2">Ad (Çeviriler)</label>
              <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
                <I18nField
                  sourceLang={sourceLang}
                  languages={languages}
                  fields={i18nFields}
                  values={typeI18nValues}
                  readOnly={!canEdit}
                  onChange={(lang, _key, val) => {
                    setSettingsForm(f => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: val } }))
                    markDirty()
                  }}
                />
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Values section — only for select types */}
      {isSelectType && (
        <div className="card p-0 overflow-hidden">
          <div
            className="flex items-center justify-between px-5 py-3 border-b"
            style={{ borderColor: 'var(--border)' }}
          >
            <h2 className="text-sm font-semibold" style={{ color: 'var(--text)' }}>
              Değerler
              <span className="ml-2 font-normal" style={{ color: 'var(--text-s)' }}>
                ({attrType.values.length})
              </span>
            </h2>
            <PermissionGuard permission={PLATFORM_PERM}>
              <Button size="sm" onClick={openAddValue}>
                <Plus size={13} /> Değer Ekle
              </Button>
            </PermissionGuard>
          </div>

          <table className="w-full">
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--surface2)' }}>
                <th className="text-left px-4 py-2.5 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Ad</th>
                {attrType.code === 'filtre_rengi' && (
                  <th className="text-left px-4 py-2.5 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Renk</th>
                )}
                <th className="text-center px-4 py-2.5 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Kullanım</th>
                <th className="text-center px-4 py-2.5 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Sıra</th>
                <th className="text-center px-4 py-2.5 text-xs font-semibold uppercase tracking-wider" style={{ color: 'var(--text-s)' }}>Durum</th>
                <th className="w-20"></th>
              </tr>
            </thead>
            <tbody>
              {attrType.values.length === 0 && (
                <tr>
                  <td colSpan={attrType.code === 'filtre_rengi' ? 6 : 5} className="text-center py-8 text-sm" style={{ color: 'var(--text-s)' }}>
                    Henüz değer eklenmemiş
                  </td>
                </tr>
              )}
              {attrType.values.map((v) => (
                <tr
                  key={v.id}
                  style={{ borderBottom: '1px solid var(--border)' }}
                >
                  <td className="px-4 py-3">
                    <span className="text-sm" style={{ color: 'var(--text)' }}>{getValueName(v)}</span>
                  </td>
                  {attrType.code === 'filtre_rengi' && (
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        {v.hexCode ? (
                          <>
                            <span
                              className="w-5 h-5 rounded-full border flex-shrink-0"
                              style={{ backgroundColor: v.hexCode, borderColor: 'var(--border)' }}
                            />
                            <span className="text-xs font-mono" style={{ color: 'var(--text-m)' }}>{v.hexCode}</span>
                          </>
                        ) : (
                          <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>
                        )}
                      </div>
                    </td>
                  )}
                  <td className="px-4 py-3 text-center">
                    {v.usedInProductCount > 0 ? (
                      <button
                        onClick={() => setUsageValue(v)}
                        className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium transition-colors hover:opacity-80"
                        style={{ background: 'var(--brand-bg)', color: 'var(--brand)', border: '1px solid var(--brand-b)' }}
                        title="Kullanıldığı ürünleri gör"
                      >
                        {v.usedInProductCount} ürün
                      </button>
                    ) : (
                      <span className="text-xs" style={{ color: 'var(--text-s)' }}>—</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className="text-sm" style={{ color: 'var(--text-s)' }}>{v.sortOrder}</span>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <Badge variant={v.isActive ? 'success' : 'neutral'}>
                      {v.isActive ? 'Aktif' : 'Pasif'}
                    </Badge>
                  </td>
                  <td className="px-2 py-3 text-center">
                    <PermissionGuard permission={PLATFORM_PERM}>
                      <div className="flex items-center justify-center gap-1">
                        <button
                          onClick={() => openEditValue(v)}
                          className="w-7 h-7 rounded-lg flex items-center justify-center transition-colors hover:opacity-80"
                          style={{ background: 'var(--surface2)', color: 'var(--text-m)', border: '1px solid var(--border)' }}
                          title="Düzenle"
                        >
                          <Pencil size={12} />
                        </button>
                        {v.usedInProductCount === 0 && (
                          <button
                            onClick={() => { deleteValueMutation.reset(); setDeleteValueId(v.id) }}
                            className="w-7 h-7 rounded-lg flex items-center justify-center transition-colors hover:opacity-80"
                            style={{ background: 'var(--surface2)', color: '#ef4444', border: '1px solid var(--border)' }}
                            title="Sil"
                          >
                            <Trash2 size={12} />
                          </button>
                        )}
                      </div>
                    </PermissionGuard>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Add Value Modal */}
      <Modal
        open={addValueOpen}
        onClose={() => setAddValueOpen(false)}
        title="Değer Ekle"
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setAddValueOpen(false)}>İptal</Button>
            <Button
              onClick={() => addValueMutation.mutate()}
              loading={addValueMutation.isPending}
              disabled={
                Object.keys(valueForm.nameI18n).length === 0 ||
                !!duplicateLang
              }
            >
              Kaydet
            </Button>
          </>
        }
      >
        <div className="space-y-5">
          <div>
            <label className="flbl">Sıra</label>
            <IntegerInput
              value={valueForm.sortOrder}
              onChange={(v) => setValueForm((f) => ({ ...f, sortOrder: v ?? 0 }))}
            />
          </div>

          {languages.length > 0 && (
            <div>
              <label className="flbl mb-2">Ad</label>
              <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
                <I18nField
                  sourceLang={sourceLang}
                  languages={languages}
                  fields={i18nFields}
                  values={valueI18nValues}
                  onChange={(lang, _key, val) => {
                    setValueForm((f) => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: val } }))
                  }}
                  uppercase
                />
              </div>
            </div>
          )}

          {attrType?.code === 'filtre_rengi' && (
            <div>
              <label className="flbl">Hex Kodu</label>
              <div className="flex items-center gap-2 mt-1">
                {valueForm.hexCode && (
                  <span className="w-7 h-7 rounded-full border flex-shrink-0"
                    style={{ backgroundColor: valueForm.hexCode, borderColor: 'var(--border)' }} />
                )}
                <input
                  type="text"
                  className="finput font-mono"
                  placeholder="#000000"
                  value={valueForm.hexCode}
                  onChange={e => setValueForm(f => ({ ...f, hexCode: e.target.value }))}
                  maxLength={9}
                />
              </div>
            </div>
          )}

          {duplicateLang && (
            <p className="text-sm" style={{ color: '#f97316' }}>
              "{duplicateLang.val}" bu özellik tipinde zaten mevcut.
            </p>
          )}

          {addValueMutation.isError && !duplicateLang && (
            <p className="text-sm" style={{ color: '#ef4444' }}>
              {(addValueMutation.error as ApiError)?.response?.data?.error ?? 'Hata oluştu. Lütfen tekrar deneyin.'}
            </p>
          )}
        </div>
      </Modal>

      {/* Edit Value Modal */}
      {editingValue && (
        <Modal
          open={editValueOpen}
          onClose={() => { setEditValueOpen(false); setEditingValue(null) }}
          title="Değer Düzenle"
          size="lg"
          footer={
            <>
              <Button variant="secondary" onClick={() => { setEditValueOpen(false); setEditingValue(null) }}>İptal</Button>
              <Button
                onClick={() => updateValueMutation.mutate()}
                loading={updateValueMutation.isPending}
                disabled={
                  Object.keys(editValueForm.nameI18n).length === 0
                }
              >
                Kaydet
              </Button>
            </>
          }
        >
          <div className="space-y-5">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="flbl">Sıra</label>
                <IntegerInput
                  value={editValueForm.sortOrder}
                  onChange={(v) => setEditValueForm((f) => ({ ...f, sortOrder: v ?? 0 }))}
                />
              </div>
              <div>
                <label className="flbl">Durum</label>
                <label className="flex items-center gap-2 mt-2 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    className="w-4 h-4 rounded accent-[var(--brand)]"
                    checked={editValueForm.isActive}
                    onChange={(e) => setEditValueForm((f) => ({ ...f, isActive: e.target.checked }))}
                  />
                  <span className="text-sm" style={{ color: 'var(--text-m)' }}>Aktif</span>
                </label>
              </div>
            </div>

            {languages.length > 0 && (
              <div>
                <label className="flbl mb-2">Ad</label>
                <div className="rounded-xl overflow-hidden" style={{ border: '1px solid var(--border)' }}>
                  <I18nField
                    sourceLang={sourceLang}
                    languages={languages}
                    fields={i18nFields}
                    values={editValueI18nValues}
                    onChange={(lang, _key, val) =>
                      setEditValueForm((f) => ({ ...f, nameI18n: { ...f.nameI18n, [lang]: val } }))
                    }
                    uppercase
                  />
                </div>
              </div>
            )}

            {attrType?.code === 'filtre_rengi' && (
              <div>
                <label className="flbl">Hex Kodu</label>
                <div className="flex items-center gap-2 mt-1">
                  {editValueHexCode && (
                    <span className="w-7 h-7 rounded-full border flex-shrink-0"
                      style={{ backgroundColor: editValueHexCode, borderColor: 'var(--border)' }} />
                  )}
                  <input
                    type="text"
                    className="finput font-mono"
                    placeholder="#000000"
                    value={editValueHexCode}
                    onChange={e => setEditValueHexCode(e.target.value)}
                    maxLength={9}
                  />
                </div>
              </div>
            )}

            {updateValueMutation.isError && (
              <p className="text-sm" style={{ color: '#ef4444' }}>
                {(updateValueMutation.error as ApiError)?.response?.data?.error ?? 'Hata oluştu. Lütfen tekrar deneyin.'}
              </p>
            )}
          </div>
        </Modal>
      )}

      {/* Delete Value Confirm Modal */}
      <Modal
        open={!!deleteValueId}
        onClose={() => setDeleteValueId(null)}
        title="Değeri Sil"
        size="sm"
        footer={
          <>
            <Button variant="secondary" onClick={() => setDeleteValueId(null)}>İptal</Button>
            <Button
              variant="danger"
              onClick={() => deleteValueId && deleteValueMutation.mutate(deleteValueId)}
              loading={deleteValueMutation.isPending}
            >
              Sil
            </Button>
          </>
        }
      >
        <p className="text-sm" style={{ color: 'var(--text-m)' }}>
          <strong style={{ color: 'var(--text)' }}>
            {deleteValueName?.['tr'] ?? deleteValueName?.[Object.keys(deleteValueName ?? {})[0]] ?? ''}
          </strong>{' '}
          değeri kalıcı olarak silinecek. Devam etmek istiyor musunuz?
        </p>
        {deleteValueMutation.isError && (
          <p className="text-sm mt-3" style={{ color: '#ef4444' }}>
            {(deleteValueMutation.error as ApiError)?.response?.data?.error ?? 'Hata oluştu.'}
          </p>
        )}
      </Modal>

      {/* Kullanılan Ürünler Modal */}
      <Modal
        open={!!usageValue}
        onClose={() => setUsageValue(null)}
        title={usageValue ? `"${getValueName(usageValue)}" değerini kullanan ürünler` : ''}
        size="md"
      >
        {usageLoading ? (
          <div className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Yükleniyor...</div>
        ) : usageProducts.length === 0 ? (
          <div className="py-8 text-center text-sm" style={{ color: 'var(--text-s)' }}>Ürün bulunamadı.</div>
        ) : (
          <div className="divide-y" style={{ borderColor: 'var(--border)' }}>
            {usageProducts.map((p) => {
              const name = p.nameI18n['tr'] ?? p.nameI18n[Object.keys(p.nameI18n)[0]] ?? p.code
              return (
                <Link
                  key={p.id}
                  to={`/catalog/products/${p.code}`}
                  onClick={() => setUsageValue(null)}
                  className="flex items-center justify-between px-1 py-2.5 hover:bg-[var(--surface2)] rounded-lg transition-colors"
                >
                  <div>
                    <p className="text-sm font-medium" style={{ color: 'var(--text)' }}>{name}</p>
                    <p className="text-xs font-mono mt-0.5" style={{ color: 'var(--text-s)' }}>{p.code}</p>
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    <span
                      className="text-[10px] px-1.5 py-0.5 rounded font-medium"
                      style={{
                        background: p.usageType === 'direct' ? 'var(--surface2)' : 'var(--brand-bg)',
                        color: p.usageType === 'direct' ? 'var(--text-s)' : 'var(--brand)',
                        border: '1px solid var(--border)',
                      }}
                    >
                      {p.usageType === 'direct' ? 'Ürün özelliği' : 'Varyant'}
                    </span>
                    <Badge variant={p.isActive ? 'success' : 'neutral'}>
                      {p.isActive ? 'Aktif' : 'Pasif'}
                    </Badge>
                    <ChevronRight size={14} style={{ color: 'var(--text-s)' }} />
                  </div>
                </Link>
              )
            })}
          </div>
        )}
      </Modal>
    </div>
  )
}
